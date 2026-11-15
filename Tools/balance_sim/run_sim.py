"""Sleeping Calamity balance simulator CLI.

  python run_sim.py [--config config.json] [--numbers data/game_numbers.json] [--out DIR] [--tag name] [--selftest]
Writes <out>/<tag>_sheet.png, <tag>_metrics.csv, <tag>_metrics.json, <tag>_config.json.
"""
import argparse
import copy
import csv
import json
import math
import os
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import torch  # noqa: E402
import sim_core as sc  # noqa: E402
from sim_core import KINDS, NF, BIN_T, BIN_F, NB_T, NB_F, NB_LVL, NB_DEATH  # noqa: E402


# ------------------------------------------------------------------------ stats from histograms
def hstats(counts, binw, total_sum=None):
    """counts [B] -> (n, mean, p10, p90). Percentiles at bin centres."""
    n = float(counts.sum())
    if n <= 0:
        return (0, float("nan"), float("nan"), float("nan"))
    cs = np.cumsum(counts) / n
    cen = (np.arange(len(counts)) + 0.5) * binw
    p10 = cen[int(np.searchsorted(cs, 0.10))]
    p90 = cen[int(np.searchsorted(cs, 0.90))]
    mean = (total_sum / n) if total_sum is not None else float((counts * cen).sum() / n)
    return (int(n), float(mean), float(p10), float(p90))


def collect(sim):
    C, N = sim.C, sim.N_DIVES
    g = lambda t: t.detach().cpu().numpy()
    ttk = g(sim.h_ttk).reshape(C, NF, 4, NB_T)
    ttk1 = g(sim.h_ttk1).reshape(C, NF, 4, NB_T)
    ttk3 = g(sim.h_ttk3).reshape(C, NF, 4, NB_T)
    sttk = g(sim.s_ttk).reshape(C, NF, 4)
    sttk1 = g(sim.s_ttk1).reshape(C, NF, 4)
    sttk3 = g(sim.s_ttk3).reshape(C, NF, 4)
    room = g(sim.h_room).reshape(C, NF, NB_T)
    room1 = g(sim.h_room1).reshape(C, NF, NB_T)
    sroom = g(sim.s_room).reshape(C, NF)
    sroom1 = g(sim.s_room1).reshape(C, NF)
    dmg = g(sim.h_dmg).reshape(C, NF, NB_F)
    death = g(sim.h_death).reshape(C, N, NB_DEATH)
    lvl = g(sim.h_lvl).reshape(C, N, NB_LVL)
    flr = g(sim.h_floor).reshape(C, N, NF + 1)
    succ = g(sim.c_succ).reshape(C, N)
    dives = g(sim.c_dive).reshape(C, N)
    out = {}
    for c, cd in enumerate(sim.case_defs):
        m = {"players": cd["players"], "profile": "+".join(sorted(set(cd.get("profiles", ["average"])))),
             "loadout": "+".join(cd.get("loadouts", ["axe"]))}
        for sc_, (th, ts_) in {"all": (ttk, sttk), "d1": (ttk1, sttk1), "d1to3": (ttk3, sttk3)}.items():
            for k in range(4):
                for f in range(NF):
                    m[("ttk_" + sc_, KINDS[k], f + 1)] = hstats(th[c, f, k], BIN_T, ts_[c, f, k])
            for f in range(NF):
                reg = th[c, f, :3].sum(0)
                tot = ts_[c, f, :3].sum()
                m[("ttk_" + sc_, "regular", f + 1)] = hstats(reg, BIN_T, tot)
        for sc_, (rh, rs) in {"all": (room, sroom), "d1": (room1, sroom1)}.items():
            for f in range(NF):
                m[("room_clear_" + sc_, "regular", f + 1)] = hstats(rh[c, f], BIN_T, rs[c, f])
        for f in range(NF):
            m[("dmg_taken_frac", "per_hero", f + 1)] = hstats(dmg[c, f], BIN_F)
        P = float(cd["players"])
        for d in range(N):
            hs = hstats(death[c, d], 1.0)
            dd = (hs[0],) + tuple(v - 0.5 for v in hs[1:])     # integer-valued: bin centre -> value
            m[("deaths_per_dive", "dive", d + 1)] = dd           # party total
            m[("downs_per_hero", "dive", d + 1)] = (dd[0],) + tuple(v / P for v in dd[1:])
            hs = hstats(lvl[c, d], 1.0)
            m[("weapon_level", "dive", d + 1)] = (hs[0],) + tuple(v - 0.5 for v in hs[1:])
            n = max(dives[c, d], 1)
            m[("dive_success", "dive", d + 1)] = (int(dives[c, d]), float(succ[c, d] / n), float("nan"), float("nan"))
            fc = flr[c, d]
            nn = max(fc.sum(), 1)
            fm = float((fc * np.arange(NF + 1)).sum() / nn)
            m[("floors_reached", "dive", d + 1)] = (int(nn), fm, float("nan"), float("nan"))
            # bank reached = floor 5 cleared in that dive = the dive ended on floor 6 or later (or succeeded)
            m[("bank_reach", "dive", d + 1)] = (int(dives[c, d]), float(fc[6:].sum() / nn), float("nan"), float("nan"))
        out[cd["name"]] = m
    return out


def load_prev(path):
    """Read a previous <tag>_metrics.csv back into the structure collect() returns."""
    M = {}
    with open(path, newline="") as f:
        for r in csv.DictReader(f):
            m = M.setdefault(r["case"], {"players": int(r["players"]), "profile": r["profile"]})
            fl, kd = int(r["floor"]), r["kind"]
            key = (r["metric"], "dive", int(kd[4:])) if (fl == 0 and kd.startswith("dive")) else (r["metric"], kd, fl)
            m[key] = (int(float(r["n"])), float(r["mean"]), float(r["p10"]), float(r["p90"]))
    tag = os.path.basename(path)
    return M, (tag[:-len("_metrics.csv")] if tag.endswith("_metrics.csv") else tag)


# ------------------------------------------------------------------------ verdicts
def gv(M, case, metric, kind, x, nmin=1):
    """Mean of one metric, or nan (no such case or key, or fewer than nmin samples)."""
    v = M.get(case, {}).get((metric, kind, x))
    return v[1] if (v and v[0] >= nmin and v[1] == v[1]) else float("nan")


def pooled(m, metric, f):
    v = m.get((metric, "regular", f))
    return v[1] if v and v[0] > 0 else float("nan")


BIG = 99.0


def verdicts(M, cfg, nd):
    """One verdict per target value of the Run 5 schema: check, case, sub, value (float or "no data"), band, pass."""
    T = cfg["targets"]
    V = []

    def add(check, case, sub, val, band):
        if val is None or val != val:
            V.append({"check": check, "case": case, "sub": sub, "value": "no data", "band": list(band), "pass": False})
        else:
            V.append({"check": check, "case": case, "sub": sub, "value": round(float(val), 3), "band": list(band),
                      "pass": bool(band[0] <= val <= band[1])})

    def ratio(a, b):
        return a / b if (a == a and b == b and b > 0) else float("nan")

    # ttkKindSoloDive1: regular rooms, dive 1, per kind per floor where the kind appears (n >= 20)
    t = T.get("ttkKindSoloDive1")
    if t:
        cn = t["case"]
        for kind, band in t.items():
            if kind in ("case", "floors") or kind.startswith("_"):
                continue
            got = False
            for f in range(1, NF + 1):
                if "floors" in t and f not in t["floors"]:
                    continue
                v = gv(M, cn, "ttk_d1", kind, f, nmin=20)
                if v == v:
                    got = True
                    add("ttkKind", cn, "%s f%d" % (kind, f), v, band)
            if not got:
                add("ttkKind", cn, kind, float("nan"), band)
    t = T.get("partyTtkRatio")        # pooled regular TTK dive 1, case / vs, per floor where both have data
    if t:
        for cn in t["cases"]:
            got = False
            for f in range(1, NF + 1):
                r = ratio(pooled(M.get(cn, {}), "ttk_d1", f), pooled(M.get(t["vs"], {}), "ttk_d1", f))
                if r == r:
                    got = True
                    add("partyTtkRatio", cn, "f%d" % f, r, t["band"])
            if not got:
                add("partyTtkRatio", cn, "", float("nan"), t["band"])
    t = T.get("wardenTtkSecondsDives1to3")
    if t:
        for cn in t["cases"]:
            add("wardenTtk", cn, "f5", gv(M, cn, "ttk_d1to3", "Warden", 5), t["band"])
    t = T.get("damageTakenFractionOfMaxHp")
    if t:
        for cn in t["cases"]:
            for f in range(1, NF + 1):
                if str(f) in t:
                    add("damageFrac", cn, "f%d" % f, gv(M, cn, "dmg_taken_frac", "per_hero", f), t[str(f)])
    t = T.get("coopDamageRatio")
    if t:
        for cn in t["cases"]:
            got = False
            for f in range(1, NF + 1):
                r = ratio(gv(M, cn, "dmg_taken_frac", "per_hero", f), gv(M, t["vs"], "dmg_taken_frac", "per_hero", f))
                if r == r:
                    got = True
                    add("coopDamageRatio", cn, "f%d" % f, r, t["band"])
            if not got:
                add("coopDamageRatio", cn, "", float("nan"), t["band"])
    for key, check, metric in (("bankReach", "bankReach", "bank_reach"), ("fullClear", "fullClear", "dive_success"),
                               ("weaponLevelAfterDive", "weaponLevel", "weapon_level")):
        for cn, per in (T.get(key) or {}).items():
            if cn.startswith("_"):
                continue
            for ds, band in per.items():
                if int(ds) <= nd:
                    add(check, cn, "d" + ds, gv(M, cn, metric, "dive", int(ds)), band)
    for cn, lo in (T.get("floorsReachedDive1Min") or {}).items():
        if not cn.startswith("_"):
            add("floorsReached", cn, "d1", gv(M, cn, "floors_reached", "dive", 1), [lo, BIG])
    for cn, mx in (T.get("downsPerHeroPerDiveMaxFromDive3") or {}).items():
        if cn.startswith("_"):
            continue
        vals = [gv(M, cn, "downs_per_hero", "dive", d) for d in range(3, nd + 1)]
        vals = [v for v in vals if v == v]
        add("downsPerHero", cn, "d3+", float(np.mean(vals)) if vals else float("nan"), [0, mx])
    t = T.get("coopWeaponLevelRatioDive10")
    if t:
        d = 10 if nd >= 10 else nd
        for cn in t["cases"]:
            add("coopWeaponRatio", cn, "d%d" % d, ratio(gv(M, cn, "weapon_level", "dive", d), gv(M, t["vs"], "weapon_level", "dive", d)), t["band"])
    return V


# ------------------------------------------------------------------------ outputs
def write_csv(M, path):
    rows = []
    for cn, m in M.items():
        for key, v in m.items():
            if not isinstance(key, tuple):
                continue
            metric, kind, x = key
            floor, kd = x, kind
            if kind == "dive":
                floor, kd = 0, "dive%d" % x
            rows.append([cn, m["players"], m["profile"], floor, metric, kd,
                         "%.4f" % v[1], "%.4f" % v[2], "%.4f" % v[3], v[0]])
    with open(path, "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["case", "players", "profile", "floor", "metric", "kind", "mean", "p10", "p90", "n"])
        w.writerows(rows)


def bandstr(b):
    return "%g-%g" % (b[0], b[1]) if b[1] < BIG else ">=%g" % b[0]


def vstr(v):
    return v["value"] if isinstance(v["value"], str) else "%g" % v["value"]


def fail_text(v):
    return "%s %s%s=%s [%s]" % (v["check"], v["case"], (" " + v["sub"]) if v["sub"] else "", vstr(v), bandstr(v["band"]))


COL = {"p1_average": "#1f77b4", "p2_average": "#ff7f0e", "p4_average": "#2ca02c",
       "p1_novice": "#9467bd", "p1_expert": "#d62728", "p4_mix": "#8c564b"}
KCOL = {"Slime": "#2ca02c", "Broom": "#ff7f0e", "RatKing": "#d62728", "Warden": "#1f77b4"}


def plot(M, V, cfg, path, nd, timing, title_extra="", prev=None, prev_tag=None):
    import textwrap
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    T = cfg["targets"]
    fl = np.arange(1, NF + 1)
    dv = np.arange(1, nd + 1)
    npass = sum(v["pass"] for v in V)
    nfail = len(V) - npass
    byc = {}
    for v in V:
        byc.setdefault(v["case"], [0, 0])[0 if v["pass"] else 1] += 1
    fails = [fail_text(v) for v in V if not v["pass"]]
    wrapped = textwrap.wrap("FAIL: " + "; ".join(fails), width=205) if fails else ["FAIL: none"]
    hdr_in = 0.75 + 0.118 * len(wrapped)
    fig = plt.figure(figsize=(12.6, hdr_in + 3 * 3.2), dpi=100)          # 1260 px wide
    gs = fig.add_gridspec(4, 3, height_ratios=[hdr_in, 3.2, 3.2, 3.2], hspace=0.45, wspace=0.26,
                          left=0.05, right=0.985, top=0.985, bottom=0.04)
    ax = fig.add_subplot(gs[0, :])
    ax.axis("off")
    head = f"BALANCE SIM  PASS {npass} / FAIL {nfail}   |  " + "   ".join(f"{k}: {a}P/{b}F" for k, (a, b) in byc.items())
    ax.text(0, 1.0, head, fontsize=9.5, fontweight="bold", va="top", family="monospace", transform=ax.transAxes)
    line2 = f"{timing}  {title_extra}" + (f"   |  dashed = previous run: {prev_tag}" if prev else "")
    ax.text(0, 1.0 - 0.17 / hdr_in, line2, fontsize=6.8, va="top", family="monospace", transform=ax.transAxes)
    ax.text(0, 1.0 - 0.36 / hdr_in, "\n".join(wrapped), fontsize=6.3, va="top", family="monospace", transform=ax.transAxes)

    def draw(a, fn, color, label=None, marker="o", ms=3.5, lw=1.2):
        """fn(M) -> (x, y). The previous run is drawn in the same colour, dashed and fainter."""
        x, y = fn(M)
        a.plot(x, y, marker=marker, ms=ms, color=color, label=label, lw=lw)
        if prev is not None:
            x2, y2 = fn(prev)
            a.plot(x2, y2, marker=marker, ms=ms * 0.7, color=color, ls="--", alpha=0.4, lw=lw)

    def fl_curve(case, metric, kind):
        return lambda MM: (fl, [gv(MM, case, metric, kind, int(f)) for f in fl])

    def dv_curve(case, metric):
        return lambda MM: (dv, [gv(MM, case, metric, "dive", int(d)) for d in dv])

    def titled(a, t, xl):
        a.set_title(t, fontsize=8)
        a.set_xlabel(xl, fontsize=7)

    def hband(a, lo, hi, x0, x1, color="#2ca02c", alpha=0.16):
        a.fill_between([x0, x1], lo, hi, color=color, alpha=alpha, lw=0)

    # 1 per-kind TTK, solo, dive 1, per-kind T1 bands
    a1 = fig.add_subplot(gs[1, 0])
    tk = T.get("ttkKindSoloDive1", {})
    cn1 = tk.get("case", "p1_average")
    for k in ("Slime", "Broom", "RatKing"):
        if k in tk:
            hband(a1, tk[k][0], tk[k][1], 0.6, NF + 0.4, KCOL[k], 0.13)
        draw(a1, lambda MM, k=k: (fl, [gv(MM, cn1, "ttk_d1", k, int(f), nmin=5) for f in fl]), KCOL[k], k)
    a1.set_yscale("log")
    titled(a1, f"TTK per enemy kind, {cn1}, dive 1 (shade = per-kind band)", "floor")
    a1.set_ylabel("seconds", fontsize=7)
    a1.legend(fontsize=6)
    # 2 pooled TTK 1/2/4 players (floor 5 = Warden, dives 1-3)
    a2 = fig.add_subplot(gs[1, 1])
    wt = T.get("wardenTtkSecondsDives1to3", {"band": [40, 80]})
    hband(a2, wt["band"][0], wt["band"][1], 4.6, 5.4)
    for cn in ("p1_average", "p2_average", "p4_average"):
        def fn(MM, cn=cn):
            return fl, [gv(MM, cn, "ttk_d1to3", "Warden", 5) if f == 5 else pooled(MM.get(cn, {}), "ttk_d1", int(f)) for f in fl]
        draw(a2, fn, COL[cn], cn[:2])
    a2.set_yscale("log")
    titled(a2, "Mean TTK, 1/2/4 players (floor 5 = Warden dives 1-3, band 40-80 s)", "floor")
    a2.legend(fontsize=6)
    # 3 room clear time
    a3 = fig.add_subplot(gs[1, 2])
    for cn in ("p1_average", "p2_average", "p4_average"):
        draw(a3, lambda MM, cn=cn: (fl, [pooled(MM.get(cn, {}), "room_clear_d1", int(f)) for f in fl]), COL[cn], cn[:2])
    titled(a3, "Regular room clear time (s), dive 1", "floor")
    a3.legend(fontsize=6)
    # 4 damage per hero per floor with the new bands
    a4 = fig.add_subplot(gs[2, 0])
    dt_ = T.get("damageTakenFractionOfMaxHp", {})
    for f in fl:
        if str(f) in dt_:
            hband(a4, dt_[str(f)][0], dt_[str(f)][1], f - 0.4, f + 0.4)
    for cn in ("p1_average", "p2_average", "p4_average"):
        draw(a4, fl_curve(cn, "dmg_taken_frac", "per_hero"), COL[cn], cn[:2])
    titled(a4, "Damage taken per hero per floor (fraction of max HP, all dives)", "floor")
    a4.legend(fontsize=6)
    # 5 downs per hero per dive
    a5 = fig.add_subplot(gs[2, 1])
    for cn in M:
        draw(a5, dv_curve(cn, "downs_per_hero"), COL.get(cn), cn)
    dm = T.get("downsPerHeroPerDiveMaxFromDive3", {})
    dm = [v for k, v in dm.items() if not k.startswith("_")]
    if dm:
        a5.hlines(max(dm), 3, nd, color="#2ca02c", ls="--", lw=1)
    titled(a5, "Downs per hero per dive (green = co-op max from dive 3)", "dive")
    a5.legend(fontsize=5.5, ncol=2)
    # 6 weapon level after dive N
    a6 = fig.add_subplot(gs[2, 2])
    for cn, per in T.get("weaponLevelAfterDive", {}).items():
        for ds, bd in per.items():
            if int(ds) <= nd:
                hband(a6, bd[0], bd[1], int(ds) - 0.35, int(ds) + 0.35, alpha=0.2)
    for cn in ("p1_average", "p2_average", "p4_average", "p1_novice", "p1_expert"):
        draw(a6, dv_curve(cn, "weapon_level"), COL[cn], cn)
    titled(a6, "Weapon level after dive N (mean; green = p1_average band)", "dive")
    a6.legend(fontsize=5.5)

    def band_marks(a, key, case="p1_average"):
        for ds, bd in T.get(key, {}).get(case, {}).items():
            if int(ds) <= nd:
                hband(a, bd[0], bd[1], int(ds) - 0.35, int(ds) + 0.35, alpha=0.22)

    # 7 bank reach, 8 full clear, 9 mean floor reached
    a7 = fig.add_subplot(gs[3, 0])
    band_marks(a7, "bankReach")
    for cn in M:
        draw(a7, dv_curve(cn, "bank_reach"), COL.get(cn), cn)
    a7.set_ylim(0, 1.02)
    titled(a7, "Bank reach (floor 5 cleared) by dive (green = p1_average)", "dive")
    a7.legend(fontsize=5.5, ncol=2)
    a8 = fig.add_subplot(gs[3, 1])
    band_marks(a8, "fullClear")
    for cn in M:
        draw(a8, dv_curve(cn, "dive_success"), COL.get(cn), cn)
    a8.set_ylim(0, 1.02)
    titled(a8, "Full clear (floor 7 cleared) by dive (green = p1_average)", "dive")
    a9 = fig.add_subplot(gs[3, 2])
    for cn in M:
        draw(a9, dv_curve(cn, "floors_reached"), COL.get(cn), cn)
    titled(a9, "Mean floor reached (1-7), by dive", "dive")
    for axx in fig.axes:
        axx.tick_params(labelsize=6.5)
        axx.grid(alpha=0.25, lw=0.4)
    fig.savefig(path, dpi=100)
    plt.close(fig)


# ------------------------------------------------------------------------ digest
def digest(M, V, cfg, nd, timing, tag, prev=None, prev_tag=None, Vprev=None):
    """Compact text for the controller: verdicts, then one-line-per-case tables. Previous value in [brackets]."""
    L = []
    T = cfg["targets"]
    fr = range(1, NF + 1)

    def f1(v, fmt):
        return "-" if v != v else fmt % v

    def both(fn, fmt):
        s = f1(fn(M), fmt)
        if prev is not None:
            s += "[" + f1(fn(prev), fmt) + "]"
        return s

    def num(case, metric, kind, x, fmt="%.1f", nmin=1):
        return both(lambda MM: gv(MM, case, metric, kind, x, nmin), fmt)

    def rat(case, vs, metric, kind, f, fmt="%.2f"):
        def r(MM):
            a, b = gv(MM, case, metric, kind, f), gv(MM, vs, metric, kind, f)
            return a / b if (a == a and b == b and b > 0) else float("nan")
        return both(r, fmt)

    npass = sum(v["pass"] for v in V)
    L.append(f"DIGEST {tag}" + (f"   previous run in [brackets]: {prev_tag}" if prev else "") + f"   {timing}")
    pv = f" (previous run: PASS {sum(v['pass'] for v in Vprev)} FAIL {sum(not v['pass'] for v in Vprev)})" if Vprev else ""
    L.append(f"VERDICTS PASS {npass} FAIL {len(V) - npass}{pv}. Per check and case; FAIL items sub=value band [previous value]")
    pmap = {(v["check"], v["case"], v["sub"]): vstr(v) for v in (Vprev or [])}
    groups = {}
    for v in V:
        groups.setdefault((v["check"], v["case"]), []).append(v)
    for (chk, cn), vs in groups.items():
        bad = [v for v in vs if not v["pass"]]
        items = ["%s=%s band %s%s" % (v["sub"] or "-", vstr(v), bandstr(v["band"]),
                                      (" [%s]" % pmap[(chk, cn, v["sub"])]) if (chk, cn, v["sub"]) in pmap else "") for v in bad]
        L.append("%-15s %-10s PASS %2d FAIL %2d%s" % (chk, cn, len(vs) - len(bad), len(bad), (" | " + "; ".join(items)) if bad else ""))
    fh = " ".join("f%d" % f for f in fr)
    L.append("")
    L.append(f"TTK dive 1 (s), regular rooms, floors {fh}")
    kcase = T.get("ttkKindSoloDive1", {}).get("case", "p1_average")
    for k in ("Slime", "Broom", "RatKing"):
        L.append("  %-10s %-8s %s" % (kcase, k, " ".join(num(kcase, "ttk_d1", k, f, nmin=5) for f in fr)))
    for cn in M:
        L.append("  %-10s %-8s %s" % (cn, "pooled", " ".join(both(lambda MM, cn=cn, f=f: pooled(MM.get(cn, {}), "ttk_d1", f), "%.1f") for f in fr)))
    L.append("Warden TTK dives 1-3 (s): " + "  ".join("%s %s" % (cn, num(cn, "ttk_d1to3", "Warden", 5)) for cn in M))
    L.append(f"Damage taken per hero (fraction of max HP), floors {fh}")
    for cn in M:
        L.append("  %-10s %s" % (cn, " ".join(num(cn, "dmg_taken_frac", "per_hero", f, "%.2f") for f in fr)))
    vs = T.get("partyTtkRatio", {}).get("vs", "p1_average")
    L.append(f"Co-op ratios vs {vs}: TTK d1 per floor | damage frac per floor | weapon level d{min(10, nd)}")
    for cn in ("p2_average", "p4_average"):
        if cn in M:
            L.append("  %-10s %s | %s | %s" % (cn, " ".join(rat(cn, vs, "ttk_d1", "regular", f) for f in fr),
                                              " ".join(rat(cn, vs, "dmg_taken_frac", "per_hero", f) for f in fr),
                                              rat(cn, vs, "weapon_level", "dive", min(10, nd))))
    ds = [d for d in (1, 2, 3, 5, 10) if d <= nd]
    L.append("By dive " + " ".join("d%d" % d for d in ds))
    for title, metric, fmt in (("bank reach", "bank_reach", "%.2f"), ("full clear", "dive_success", "%.2f"),
                               ("floors reached", "floors_reached", "%.1f"), ("downs per hero", "downs_per_hero", "%.2f"),
                               ("weapon level", "weapon_level", "%.1f")):
        L.append(title + ":")
        for cn in M:
            L.append("  %-10s %s" % (cn, " ".join(num(cn, metric, "dive", d, fmt) for d in ds)))
    return "\n".join(L) + "\n"


# ------------------------------------------------------------------------ run
def pick_device(cfg):
    d = cfg["run"].get("device", "auto")
    if d == "auto":
        return "cuda" if torch.cuda.is_available() else "cpu"
    return d


def run_cases(nums, cfg, device, lanes=None, n_dives=None, cases=None, seed=None):
    sim = sc.Sim(nums, cfg, device, lanes=lanes, n_dives=n_dives, cases=cases, seed=seed)
    ticks = sim.run()
    return sim, ticks


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--config", default=os.path.join(HERE, "config.json"))
    ap.add_argument("--numbers", default=os.path.join(HERE, "data", "game_numbers.json"))
    ap.add_argument("--out", default=os.path.join(HERE, "out"))
    ap.add_argument("--tag", default="baseline")
    ap.add_argument("--selftest", action="store_true")
    ap.add_argument("--lanes", type=int)
    ap.add_argument("--dives", type=int)
    ap.add_argument("--device")
    ap.add_argument("--prev", help="previous run's <tag>_metrics.csv: drawn dashed on every panel, shown in [brackets] in the digest")
    a = ap.parse_args()
    cfg = sc.load_json(a.config)
    nums = sc.load_json(a.numbers)
    if a.device:
        cfg["run"]["device"] = a.device
    dev = pick_device(cfg)
    if a.selftest:
        sys.exit(selftest(nums, cfg, dev))
    os.makedirs(a.out, exist_ok=True)
    t0 = time.time()
    sim, ticks = run_cases(nums, cfg, dev, lanes=a.lanes, n_dives=a.dives)
    t1 = time.time()
    nd = sim.N_DIVES
    M = collect(sim)
    V = verdicts(M, cfg, nd)
    prev, prev_tag, Vprev = None, None, None
    if a.prev:
        prev, prev_tag = load_prev(a.prev)
        Vprev = verdicts(prev, cfg, nd)
    timing = f"device={dev} lanes/case={sim.lanes} cases={sim.C} dives={nd} ticks={ticks} sim={t1 - t0:.1f}s"
    print(timing)
    base = os.path.join(a.out, a.tag)
    write_csv(M, base + "_metrics.csv")
    plot(M, V, cfg, base + "_sheet.png", nd, timing, f"numbers={os.path.basename(a.numbers)}", prev=prev, prev_tag=prev_tag)
    with open(base + "_digest.txt", "w", encoding="utf-8") as fd:
        fd.write(digest(M, V, cfg, nd, timing, a.tag, prev, prev_tag, Vprev))
    eff = {"config": cfg, "effective_game_numbers": sim.nums}
    json.dump(eff, open(base + "_config.json", "w"), indent=1)
    summ = {"timing": timing, "pass": sum(v["pass"] for v in V), "fail": sum(not v["pass"] for v in V), "verdicts": V,
            "headline": {cn: {"ttk_d1": {str(f): pooled(m, "ttk_d1", f) for f in range(1, NF + 1)},
                              "ttk_d1to3_warden": gv(M, cn, "ttk_d1to3", "Warden", 5),
                              "room_clear_d1": {str(f): pooled(m, "room_clear_d1", f) for f in range(1, NF + 1)},
                              "dmg_frac": {str(f): m[("dmg_taken_frac", "per_hero", f)][1] for f in range(1, NF + 1)},
                              "deaths_per_dive": float(np.mean([m[("deaths_per_dive", "dive", d)][1] for d in range(1, nd + 1)])),
                              "downs_per_hero": {str(d): m[("downs_per_hero", "dive", d)][1] for d in range(1, nd + 1)},
                              "bank_reach": {str(d): m[("bank_reach", "dive", d)][1] for d in range(1, nd + 1)},
                              "success_rate": float(np.mean([m[("dive_success", "dive", d)][1] for d in range(1, nd + 1)])),
                              "weapon_level": {str(d): m[("weapon_level", "dive", d)][1] for d in range(1, nd + 1)}}
                         for cn, m in M.items()}}
    json.dump(summ, open(base + "_metrics.json", "w"), indent=1, default=lambda o: None if o != o else o)
    print(f"PASS {summ['pass']} FAIL {summ['fail']}  -> {base}_sheet.png  total {time.time() - t0:.1f}s")


# ------------------------------------------------------------------------ selftest
def selftest(nums, cfg, dev):
    fails = 0

    def check(name, ok, info=""):
        nonlocal fails
        print(("PASS " if ok else "FAIL ") + name + " " + info)
        if not ok:
            fails += 1
    D = nums["damage"]
    f = lambda **k: float(sc.damage_formula(*[torch.tensor(float(k.get(n, d))) if n != "crit_hit" else torch.tensor(bool(k.get(n, d))) for n, d in
                                              [("atk", 22), ("mult", 1.0), ("deff", 5), ("fire_pts", 0), ("weak_fire", 0), ("pest_pts", 0), ("is_pest", 0), ("crit_hit", False), ("prior", 0), ("is_fin", 0)]], D))
    check("ATK22 vs DEF5 move1.0 = 21", f() == 21, str(f()))
    check("axe finisher 1.6 = ceil(33.52)=34", f(mult=1.6) == 34, str(f(mult=1.6)))
    check("crit x1.5 = ceil(31.43)=32", f(crit_hit=True) == 32, str(f(crit_hit=True)))
    check("fire10 on weak = ceil(22*100/105*1.2)=26", f(fire_pts=10, weak_fire=1) == 26, str(f(fire_pts=10, weak_fire=1)))
    check("pest10 on pest = ceil(20.952*1.3)=28", f(pest_pts=10, is_pest=1) == 28, str(f(pest_pts=10, is_pest=1)))
    check("combo 2 prior on finisher = ceil(33.52*1.2)=41", f(mult=1.6, prior=2, is_fin=1) == 41, str(f(mult=1.6, prior=2, is_fin=1)))
    check("min damage 1", f(atk=0.01) == 1)
    check("DEF 15 Broom: ceil(22*100/115)=20", f(deff=15) == 20, str(f(deff=15)))
    g = lambda atk, mult, deff, oth: float(sc.damage_formula(torch.tensor(atk), torch.tensor(mult), torch.tensor(deff), torch.tensor(0.0), torch.tensor(0.0),
                                                              torch.tensor(0.0), torch.tensor(0.0), torch.tensor(False), torch.tensor(0.0), torch.tensor(0.0), D, other=torch.tensor(oth)))
    check("single ceil: atk 7.8 def 10 rage 1.3 = ceil(7.8*100/110*1.3=9.218) -> 10", g(7.8, 1.0, 10.0, 1.3) == 10, str(g(7.8, 1.0, 10.0, 1.3)))
    # determinism
    small =[{"name": "t1", "players": 1, "profiles": ["average"], "loadouts": ["axe"]}]
    res = []
    for _ in range(2):
        sim, _t = run_cases(nums, cfg, dev, lanes=1000, n_dives=2, cases=small, seed=777)
        res.append((sim.h_ttk.cpu().numpy().copy(), sim.h_lvl.cpu().numpy().copy(), sim.h_death.cpu().numpy().copy()))
    check("1000-lane determinism (same seed)", all((a == b).all() for a, b in zip(res[0], res[1])))
    sim3, _t = run_cases(nums, cfg, dev, lanes=1000, n_dives=2, cases=small, seed=778)
    check("different seed differs", not (res[0][0] == sim3.h_ttk.cpu().numpy()).all())
    # solo vs party sanity
    cs = [{"name": "p1_average", "players": 1, "profiles": ["average"], "loadouts": ["axe"]},
          {"name": "p4_average", "players": 4, "profiles": ["average"], "loadouts": ["axe"]}]
    sim4, _t = run_cases(nums, cfg, dev, lanes=1000, n_dives=2, cases=cs, seed=5)
    M = collect(sim4)
    lm = nums["coop"]["lifeMult"]
    ratios = []
    for f_ in (1, 2):
        a_, b_ = pooled(M["p4_average"], "ttk_d1", f_), pooled(M["p1_average"], "ttk_d1", f_)
        ratios.append(a_ / b_)
    check("4p TTK ratio below lifeMult (more dps) and above 0.2", all(0.2 < r < lm[3] for r in ratios), str(ratios))
    check("kills happened", float(sim4.h_ttk.sum()) > 1000)
    # zero-damage world: enemies cannot hurt -> no deaths, success 100%
    c2 = copy.deepcopy(cfg)
    c2["overrides"] = {"damage.playerMaxHp": 1e6}
    sim5, _t = run_cases(nums, c2, dev, lanes=300, n_dives=1, cases=small, seed=9)
    check("invulnerable party: no deaths and dive success", int(sim5.c_succ.sum()) == 300 and float((sim5.h_death.cpu().numpy()[..., 1:]).sum()) == 0)
    # ---- Run 5 model features
    def mk(assume=None, over=None, lanes=64):
        c = copy.deepcopy(cfg)
        c["overrides"] = dict(over or {})
        c["assumptions"].update(assume or {})
        return sc.Sim(nums, c, dev, lanes=lanes, n_dives=3, cases=small, seed=3)
    # 1. kinds from floorStats.kindShare (floor 1: Slime 0.8, Rat-king 0.2, no Broom) vs floorMix (no Rat-king on floor 1)
    ks = mk({"useFloorStatsKindShare": True}).fMixCum[1].cpu().tolist()
    fm = mk({"useFloorStatsKindShare": False}).fMixCum[1].cpu().tolist()
    exp = nums["floorStats"]["1"]["kindShare"]
    tot_ = exp["Slime"] + exp["RatKing"] + exp["Broom"]
    check("kind share from floorStats (floor 1)", abs(ks[0] - exp["Slime"] / tot_) < 1e-4 and abs(ks[1] - (exp["Slime"] + exp["RatKing"]) / tot_) < 1e-4, str(ks))
    check("floorMix has no Rat-king on floor 1 (old behaviour kept when off)", abs(fm[1] - fm[0]) < 1e-6, str(fm))
    # 2. bread ladder
    hl = nums["healing"]
    base_a = {"bakeryBuilt": False, "bakeryAfterFloor": 0, "breadLadderFreeLoaves": 3, "breadLadderPoorLoafHeal": 15}
    sm = mk(base_a)
    check("poor loaves without the bakery", float(sm.loaves.min()) == 3 and float(sm.loaves.max()) == 3 and float(sm.loafHealL.max()) == 15)
    sm = mk(dict(base_a, bakeryBuilt=True))
    check("bakery built: loavesPerDive x loafHeal", float(sm.loaves.min()) == hl["loavesPerDive"] and float(sm.loafHealL.min()) == hl["loafHeal"])
    sm = mk(dict(base_a, breadLadderFreeLoaves=0))
    check("0 free loaves, no bakery = no loaves (game as built)", float(sm.loaves.max()) == 0)
    sm = mk(dict(base_a, bakeryAfterFloor=3))
    allm = torch.ones(sm.L, dtype=torch.bool, device=sm.dev)
    sm.floor[:] = 3                                   # failed on floor 3: floors 1-2 cleared
    sm._end_dive(allm, torch.zeros_like(allm))
    ok1 = float(sm.loaves.max()) == 3 and not bool(sm.bakery.any())
    sm.floor[:] = 4                                   # next dive failed on floor 4: floor 3 cleared, bakery from the dive after
    sm._end_dive(allm, torch.zeros_like(allm))
    check("bakeryAfterFloor 3: poor loaves, then the bakery's bread after a dive that cleared floor 3",
          ok1 and bool(sm.bakery.all()) and float(sm.loaves.min()) == hl["loavesPerDive"] and float(sm.loafHealL.min()) == hl["loafHeal"])
    # 3. dodge by wind-up
    check("dodge ref 0 = flat rate", sc.dodge_prob(0.5, 1.2, 0) == 0.5)
    check("dodge wind-up at or under ref = flat rate", abs(sc.dodge_prob(0.5, 0.3, 0.6) - 0.5) < 1e-9 and abs(sc.dodge_prob(0.5, 0.6, 0.6) - 0.5) < 1e-9)
    check("dodge wind-up 2x ref = 1-(1-r)^2", abs(sc.dodge_prob(0.5, 1.2, 0.6) - 0.75) < 1e-9)
    check("dodge tensor form agrees", abs(float(sc.dodge_prob(torch.tensor(0.5), torch.tensor(1.2), 0.6)) - 0.75) < 1e-6)
    # 4. Warden tier (was hardcoded tier 2: t2 | wslot)
    def warden(tier):
        sm = mk(over={"tierScale.wardenTier": tier}, lanes=16)
        m_ = torch.ones(sm.L, dtype=torch.bool, device=sm.dev)
        sm.floor[:] = 5
        sm.ntot[:] = 1
        sm.room[:] = 0
        sm._spawn(m_)
        return sm
    w1, w2 = warden(1), warden(2)
    ts_ = nums["tierScale"]
    wh = nums["enemies"]["Warden"]
    exp_hp1 = math.ceil(wh["hp"] * nums["coop"]["lifeMult"][1 if len(nums["coop"]["lifeMult"]) == 5 else 0] * ts_.get("soloLife", 1.0) - 1e-4)
    check("wardenTier 1: tier-1 Warden (no tier-2 life/def, tier-1 atk, tier-1 pacing)",
          bool(w1.iswarden.all()) and not bool(w1.et2[:, 0].any()) and abs(float(w1.ehp[0, 0]) - exp_hp1) < 1e-3
          and abs(float(w1.edef[0, 0]) - wh["def"]) < 1e-4 and abs(float(w1.eatk[0, 0]) - wh["atk"] * ts_["tier1Atk"] * ts_.get("soloAtk", 1.0)) < 1e-4,
          "hp %s def %s atk %s" % (float(w1.ehp[0, 0]), float(w1.edef[0, 0]), float(w1.eatk[0, 0])))
    check("wardenTier 2 (default) keeps the tier-2 Warden", bool(w2.et2[:, 0].all()) and float(w2.ehp[0, 0]) > float(w1.ehp[0, 0]) * 2.5
          and abs(float(w2.eatk[0, 0]) - wh["atk"] * ts_["tier2Atk"] * ts_.get("soloAtk", 1.0)) < 1e-4)
    # 5. ttkKindSoloDive1 "floors" restricts the verdict to those floors
    Mf = {"c": {("ttk_d1", "Slime", f): (50, 5.0) for f in (1, 2, 3, 4)}}
    tf = lambda **k: {"targets": {"ttkKindSoloDive1": dict({"case": "c", "Slime": [1.0, 9.0]}, **k)}}
    subs_all = [v["sub"] for v in verdicts(Mf, tf(), 1) if v["check"] == "ttkKind"]
    subs_fl = [v["sub"] for v in verdicts(Mf, tf(floors=[1, 2, 3]), 1) if v["check"] == "ttkKind"]
    check("ttkKind floors key limits verdicts to those floors", len(subs_all) == 4 and subs_fl == ["Slime f1", "Slime f2", "Slime f3"], str(subs_fl))
    print("SELFTEST", "PASS" if fails == 0 else "FAIL (%d)" % fails)
    return 1 if fails else 0


if __name__ == "__main__":
    main()
