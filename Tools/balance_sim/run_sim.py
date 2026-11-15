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
    sttk = g(sim.s_ttk).reshape(C, NF, 4)
    sttk1 = g(sim.s_ttk1).reshape(C, NF, 4)
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
        for sc_, (th, ts_, rh, rs) in {"all": (ttk, sttk, room, sroom), "d1": (ttk1, sttk1, room1, sroom1)}.items():
            for k in range(4):
                for f in range(NF):
                    m[("ttk_" + sc_, KINDS[k], f + 1)] = hstats(th[c, f, k], BIN_T, ts_[c, f, k])
            for f in range(NF):
                reg = th[c, f, :3].sum(0)
                tot = ts_[c, f, :3].sum()
                m[("ttk_" + sc_, "regular", f + 1)] = hstats(reg, BIN_T, tot)
                m[("room_clear_" + sc_, "regular", f + 1)] = hstats(rh[c, f], BIN_T, rs[c, f])
        for f in range(NF):
            m[("dmg_taken_frac", "per_hero", f + 1)] = hstats(dmg[c, f], BIN_F)
        for d in range(N):
            for nm, arr in (("deaths_per_dive", death), ("weapon_level", lvl)):
                hs = hstats(arr[c, d], 1.0)
                m[(nm, "dive", d + 1)] = (hs[0],) + tuple(v - 0.5 for v in hs[1:])   # integer-valued: centre -> value
            n = max(dives[c, d], 1)
            m[("dive_success", "dive", d + 1)] = (int(dives[c, d]), float(succ[c, d] / n), float("nan"), float("nan"))
            fc = flr[c, d]
            nn = max(fc.sum(), 1)
            fm = float((fc * np.arange(NF + 1)).sum() / nn)
            m[("floors_reached", "dive", d + 1)] = (int(nn), fm, float("nan"), float("nan"))
        out[cd["name"]] = m
    return out


# ------------------------------------------------------------------------ verdicts
def pooled(m, metric, f):
    v = m.get((metric, "regular", f))
    return v[1] if v and v[0] > 0 else float("nan")


def verdicts(M, cfg, nd):
    T = cfg["targets"]
    V = []

    def add(case, check, floor, val, band):
        if val != val:
            return
        ok = band[0] <= val <= band[1]
        V.append({"case": case, "check": check, "floor": floor, "value": round(float(val), 3), "band": band, "pass": bool(ok)})

    for cn in T["scoredCases"]:
        if cn not in M:
            continue
        m = M[cn]
        for f in range(1, NF + 1):
            fs = str(f)
            tk = m.get(("ttk_d1", "Warden", f)) if f == 5 else None
            v = tk[1] if (tk and tk[0] > 0) else pooled(m, "ttk_d1", f)
            add(cn, "ttk", f, v, T["ttkSeconds"][fs])
            rc = pooled(m, "room_clear_d1", f)
            if f == 5:  # Warden room clear time = its fight + adds; use all-kind room via ttk of warden
                rc = float("nan")
            add(cn, "room_clear", f, rc, T["roomClearSeconds"][fs])
            d = m[("dmg_taken_frac", "per_hero", f)]
            if d[0] > 0:
                add(cn, "damage_frac", f, d[1], T["damageTakenFractionOfMaxHp"][fs])
        dm = np.mean([m[("deaths_per_dive", "dive", d)][1] for d in range(1, nd + 1)]) / m["players"]  # per hero
        add(cn, "deaths_per_dive", 0, dm, [0, T["deathsPerDiveMax"]])
        for ds, band in T["weaponLevelAfterDive"].items():
            d = int(ds)
            if d <= nd:
                add(cn, "weapon_level_d" + ds, 0, m[("weapon_level", "dive", d)][1], band)
    if "p1_average" in M:
        for cn in T["scoredCases"]:
            if cn == "p1_average" or cn not in M:
                continue
            for f in range(1, NF + 1):
                a, b = pooled(M[cn], "ttk_d1", f), pooled(M["p1_average"], "ttk_d1", f)
                if b == b and a == a and b > 0:
                    add(cn, "party_ttk_ratio", f, a / b, T["partyTtkRatio"])
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


def plot(M, V, cfg, path, nd, timing, title_extra=""):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    T = cfg["targets"]
    fl = np.arange(1, NF + 1)
    col = {"p1_average": "#1f77b4", "p2_average": "#ff7f0e", "p4_average": "#2ca02c",
           "p1_novice": "#9467bd", "p1_expert": "#d62728", "p4_mix": "#8c564b"}
    fig = plt.figure(figsize=(12.6, 11.6), dpi=100)
    gs = fig.add_gridspec(4, 3, height_ratios=[0.55, 1, 1, 1], hspace=0.5, wspace=0.28, left=0.055, right=0.985, top=0.975, bottom=0.05)
    npass = sum(v["pass"] for v in V)
    nfail = len(V) - npass
    ax = fig.add_subplot(gs[0, :])
    ax.axis("off")
    byc = {}
    for v in V:
        byc.setdefault(v["case"], [0, 0])[0 if v["pass"] else 1] += 1
    fails = [f"{v['case'][:6]} {v['check']}{'' if not v['floor'] else ' f%d' % v['floor']}={v['value']} {v['band']}" for v in V if not v["pass"]]
    head = f"BALANCE SIM  PASS {npass} / FAIL {nfail}   |  " + "   ".join(f"{k}: {a}P/{b}F" for k, (a, b) in byc.items())
    ax.text(0, 0.95, head, fontsize=10.5, fontweight="bold", va="top", family="monospace")
    ax.text(0, 0.55, f"{timing}  {title_extra}", fontsize=8, va="top", family="monospace")
    ax.text(0, 0.30, "FAIL: " + "; ".join(fails[:7]) + (" ..." if len(fails) > 7 else ""), fontsize=6.6, va="top", family="monospace", wrap=True)

    def band(ax, key, ylab=None):
        for f in fl:
            lo, hi = T[key][str(f)]
            ax.fill_between([f - 0.4, f + 0.4], lo, hi, color="#2ca02c", alpha=0.15, lw=0)

    # A: TTK per kind, solo average d1
    a = fig.add_subplot(gs[1, 0])
    band(a, "ttkSeconds")
    mk = {"Slime": "o", "RatKing": "s", "Broom": "^", "Warden": "D"}
    for k in KINDS:
        xs, ys, lo, hi = [], [], [], []
        for f in fl:
            v = M["p1_average"][("ttk_d1", k, int(f))]
            if v[0] > 0:
                xs.append(f); ys.append(v[1]); lo.append(v[1] - v[2]); hi.append(v[3] - v[1])
        a.errorbar(xs, ys, yerr=[np.clip(lo, 0, None), np.clip(hi, 0, None)], marker=mk[k], ms=4, lw=1.1, capsize=2, label=k)
    a.set_yscale("log"); a.set_title("Time to kill per enemy kind, solo avg, dive 1\n(bar p10-p90, green = target band)", fontsize=8)
    a.set_xlabel("floor", fontsize=7); a.set_ylabel("seconds", fontsize=7); a.legend(fontsize=6)
    # B: pooled TTK, 1/2/4
    b = fig.add_subplot(gs[1, 1])
    band(b, "ttkSeconds")
    for cn in ["p1_average", "p2_average", "p4_average"]:
        ys = [M[cn][("ttk_d1", "Warden", int(f))][1] if f == 5 and M[cn][("ttk_d1", "Warden", 5)][0] > 0 else pooled(M[cn], "ttk_d1", int(f)) for f in fl]
        b.plot(fl, ys, marker="o", ms=4, color=col[cn], label=cn[:2])
    b.set_yscale("log"); b.set_title("Mean TTK pooled (floor 5 = Warden), dive 1\n1 vs 2 vs 4 players", fontsize=8)
    b.set_xlabel("floor", fontsize=7); b.legend(fontsize=6)
    # C: room clear
    c = fig.add_subplot(gs[1, 2])
    band(c, "roomClearSeconds")
    for cn in ["p1_average", "p2_average", "p4_average"]:
        c.plot(fl, [pooled(M[cn], "room_clear_d1", int(f)) for f in fl], marker="o", ms=4, color=col[cn], label=cn[:2])
    c.set_title("Regular room clear time (s), dive 1", fontsize=8); c.set_xlabel("floor", fontsize=7); c.legend(fontsize=6)
    # D: damage taken
    d = fig.add_subplot(gs[2, 0])
    band(d, "damageTakenFractionOfMaxHp")
    for cn in ["p1_average", "p2_average", "p4_average"]:
        ys = [M[cn][("dmg_taken_frac", "per_hero", int(f))] for f in fl]
        d.errorbar(fl, [y[1] for y in ys], yerr=[[max(y[1] - y[2], 0) if y[1] == y[1] else 0 for y in ys], [max(y[3] - y[1], 0) if y[1] == y[1] else 0 for y in ys]],
                   marker="o", ms=4, capsize=2, color=col[cn], label=cn[:2], lw=1)
    d.set_title("Damage taken per hero per floor (fraction of max HP)\nbar p10-p90, all dives", fontsize=8); d.set_xlabel("floor", fontsize=7); d.legend(fontsize=6)
    # E: deaths per dive
    e = fig.add_subplot(gs[2, 1])
    dv = np.arange(1, nd + 1)
    for cn in M:
        e.plot(dv, [M[cn][("deaths_per_dive", "dive", int(x))][1] / M[cn]["players"] for x in dv], marker="o", ms=3, color=col.get(cn), label=cn, lw=1)
    e.axhline(T["deathsPerDiveMax"], color="#2ca02c", ls="--", lw=1)
    e.set_title("Deaths per hero per dive, by dive number\ngreen = target max", fontsize=8); e.set_xlabel("dive", fontsize=7); e.legend(fontsize=5.5, ncol=2)
    # F: weapon level
    g = fig.add_subplot(gs[2, 2])
    for ds, bd in T["weaponLevelAfterDive"].items():
        if int(ds) <= nd:
            g.fill_between([int(ds) - 0.35, int(ds) + 0.35], bd[0], bd[1], color="#2ca02c", alpha=0.18, lw=0)
    for cn in ["p1_average", "p2_average", "p4_average", "p1_novice", "p1_expert"]:
        ys = [M[cn][("weapon_level", "dive", int(x))] for x in dv]
        g.plot(dv, [y[1] for y in ys], marker="o", ms=3, color=col[cn], label=cn, lw=1)
        if cn in ("p1_average", "p4_average"):
            g.fill_between(dv, [y[2] for y in ys], [y[3] for y in ys], color=col[cn], alpha=0.12, lw=0)
    g.set_title("Weapon level after dive N (mean; shade p10-p90)\ngreen = target at dives 1, 3, 10", fontsize=8); g.set_xlabel("dive", fontsize=7); g.legend(fontsize=5.5)
    # G: success / floors reached
    h = fig.add_subplot(gs[3, 0])
    for cn in M:
        h.plot(dv, [M[cn][("dive_success", "dive", int(x))][1] for x in dv], marker="o", ms=3, color=col.get(cn), label=cn, lw=1)
    h.set_ylim(0, 1.02); h.set_title("Dive success rate (reach floor 7 cleared), by dive", fontsize=8); h.set_xlabel("dive", fontsize=7); h.legend(fontsize=5.5, ncol=2)
    i = fig.add_subplot(gs[3, 1])
    for cn in M:
        i.plot(dv, [M[cn][("floors_reached", "dive", int(x))][1] for x in dv], marker="o", ms=3, color=col.get(cn), label=cn, lw=1)
    i.set_title("Mean floor reached (1-7), by dive", fontsize=8); i.set_xlabel("dive", fontsize=7)
    # H: skill sensitivity room clear
    j = fig.add_subplot(gs[3, 2])
    for cn in ["p1_novice", "p1_average", "p1_expert", "p4_mix"]:
        j.plot(fl, [pooled(M[cn], "room_clear_d1", int(f)) for f in fl], marker="o", ms=3, color=col[cn], label=cn, lw=1)
    band(j, "roomClearSeconds")
    j.set_title("Room clear time vs skill (solo) and 4p mixed loadout", fontsize=8); j.set_xlabel("floor", fontsize=7); j.legend(fontsize=5.5)
    for axx in fig.axes:
        axx.tick_params(labelsize=6.5)
        axx.grid(alpha=0.25, lw=0.4)
    fig.savefig(path, dpi=100)
    plt.close(fig)


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
    timing = f"device={dev} lanes/case={sim.lanes} cases={sim.C} dives={nd} ticks={ticks} sim={t1 - t0:.1f}s"
    print(timing)
    base = os.path.join(a.out, a.tag)
    write_csv(M, base + "_metrics.csv")
    plot(M, V, cfg, base + "_sheet.png", nd, timing, f"numbers={os.path.basename(a.numbers)}")
    eff = {"config": cfg, "effective_game_numbers": sim.nums}
    json.dump(eff, open(base + "_config.json", "w"), indent=1)
    summ = {"timing": timing, "pass": sum(v["pass"] for v in V), "fail": sum(not v["pass"] for v in V), "verdicts": V,
            "headline": {cn: {"ttk_d1": {str(f): pooled(m, "ttk_d1", f) for f in range(1, NF + 1)},
                              "room_clear_d1": {str(f): pooled(m, "room_clear_d1", f) for f in range(1, NF + 1)},
                              "dmg_frac": {str(f): m[("dmg_taken_frac", "per_hero", f)][1] for f in range(1, NF + 1)},
                              "deaths_per_dive": float(np.mean([m[("deaths_per_dive", "dive", d)][1] for d in range(1, nd + 1)])),
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
    # determinism
    small = [{"name": "t1", "players": 1, "profiles": ["average"], "loadouts": ["axe"]}]
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
    print("SELFTEST", "PASS" if fails == 0 else "FAIL (%d)" % fails)
    return 1 if fails else 0


if __name__ == "__main__":
    main()
