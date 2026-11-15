"""Sleeping Calamity balance simulator core: time-stepped, vectorised over independent dive lanes.

No game number is hard-coded here: everything comes from game_numbers.json (+ config overrides and the
explicitly named assumptions in config.json "assumptions"/"skillProfiles").  Sync-free tick (no .item(),
no dynamic shapes) so it can run eagerly or inside a CUDA graph.
"""
import copy
import json
import math

import torch

KINDS = ["Slime", "RatKing", "Broom", "Warden"]
K = 4
NF = 7          # floors
H = 4           # hero slots
BIN_T, NB_T = 0.5, 600       # seconds histogram (0..300 s)
BIN_F, NB_F = 0.01, 300      # fraction-of-max-HP histogram (0..3)
NB_LVL = 100
NB_DEATH = 21


# ----------------------------------------------------------------------------- config helpers
def load_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def _strip_comments(o):
    if isinstance(o, dict):
        return {k: _strip_comments(v) for k, v in o.items() if not k.startswith("_comment")}
    if isinstance(o, list):
        return [_strip_comments(v) for v in o]
    return o


def apply_overrides(nums, overrides):
    """Dotted-path overrides applied on top of game_numbers.json.
    value forms: number/bool/str = absolute set; {"mult": x}; {"add": x}.  List elements by index
    ("coop.lifeMult.2"). Unknown paths raise (a typo must not silently do nothing)."""
    out = copy.deepcopy(nums)
    for path, spec in (overrides or {}).items():
        if path.startswith("_comment"):
            continue
        parts = path.split(".")
        cur = out
        for p in parts[:-1]:
            cur = cur[int(p)] if isinstance(cur, list) else cur[p]
        last = parts[-1]
        key = int(last) if isinstance(cur, list) else last
        if isinstance(cur, dict) and key not in cur:
            raise KeyError("override path not in game numbers: " + path)
        old = cur[key]
        if isinstance(spec, dict):
            if "mult" in spec:
                cur[key] = old * spec["mult"]
            elif "add" in spec:
                cur[key] = old + spec["add"]
            else:
                raise ValueError("bad override spec for " + path)
        else:
            cur[key] = spec
    return out


def wmatch(heroWeapon, key):
    a, b = heroWeapon.lower(), key.lower()
    return a == b or b.startswith(a) or a.startswith(b.split("_")[0])


# ----------------------------------------------------------------------------- damage formula
def damage_formula(atk, mult, deff, fire_pts, weak_fire, pest_pts, is_pest, crit_hit, prior, is_fin, D):
    """DamageFormula.cs: max(min, ceil(ATK*move*100/(100+DEF) * (1+element) * tag * crit * combo)).
    All args broadcastable tensors; D = numbers['damage']."""
    base = atk * mult * 100.0 / (100.0 + deff)
    el = 1.0 + D["elementPerPoint"] * fire_pts * weak_fire
    tg = 1.0 + D["tagPerPoint"] * pest_pts * is_pest
    cr = torch.where(crit_hit, torch.full_like(base, D["critMultiplier"]), torch.ones_like(base))
    cb = 1.0 + D["comboBonusPerHit"] * prior * is_fin
    raw = base * el * tg * cr * cb
    return torch.clamp(torch.ceil(raw - 1e-6), min=D["minDamage"])


# ----------------------------------------------------------------------------- simulator
class Sim:
    def __init__(self, nums, cfg, device, lanes=None, n_dives=None, seed=None, cases=None):
        self.dev = torch.device(device)
        self.cfg = cfg
        self.nums = apply_overrides(nums, cfg.get("overrides"))
        N = self.nums
        run = cfg["run"]
        A = cfg["assumptions"]
        self.A = A
        self.lanes = lanes or run["lanesPerCase"]
        self.N_DIVES = n_dives or run["nDives"]
        self.dt = run.get("dt", 1.0 / 30.0)
        self.fr = float(run.get("frameRate", 60))
        self.E = int(run.get("maxEnemies", 10))
        self.case_defs = cases if cases is not None else cfg["cases"]
        self.C = len(self.case_defs)
        self.timeout = float(A["roomTimeoutSeconds"])
        dev = self.dev
        self.seed = run["seed"] if seed is None else seed
        torch.manual_seed(self.seed)
        if dev.type == "cuda":
            torch.cuda.manual_seed_all(self.seed)

        def T(x, dt=torch.float32):
            return torch.tensor(x, dtype=dt, device=dev)

        self.T = T
        # ---- enemies
        en = N["enemies"]
        self.eHp = T([en[k]["hp"] for k in KINDS])
        self.eAtk = T([en[k]["atk"] for k in KINDS])
        self.eDef = T([en[k]["def"] for k in KINDS])
        self.eWeak = T([float(bool(en[k]["weakFire"])) for k in KINDS])
        self.ePest = T([float(bool(en[k]["isPest"])) for k in KINDS])
        self.eBlock = T([float(bool(en[k]["frontBlock"])) for k in KINDS])
        ts = N["tierScale"]
        self.ts = ts
        # attack tables [K, A=4]
        ea = N["enemyAttacks"]
        AMAX = 4
        tot = [[1.0] * AMAX for _ in KINDS]
        hit = [[1.0] * AMAX for _ in KINDS]
        mul = [[1.0] * AMAX for _ in KINDS]
        gb = [[0.0] * AMAX for _ in KINDS]
        nat = []
        for i, k in enumerate(KINDS):
            ids = en[k]["attacks"][:AMAX]
            nat.append(max(1, len(ids)))
            for j, aid in enumerate(ids):
                a = ea[aid]
                tot[i][j] = a["totalFrames"] / self.fr
                hit[i][j] = a["hitOnFrame"] / self.fr
                mul[i][j] = a["mult"]
                gb[i][j] = float(bool(a["guardBreak"]))
        self.aTot, self.aHit, self.aMul, self.aGB = T(tot), T(hit), T(mul), T(gb)
        self.nAtk = T(nat, torch.long)
        pc = N["enemyPacing"]
        cd = [[0, 0] for _ in KINDS]
        rc_ = [[0, 0] for _ in KINDS]
        for i, k in enumerate(KINDS):
            c = pc["cooldown"].get(k, pc["cooldown"]["default"])
            r = pc["recover"].get(k, pc["recover"]["default"])
            cd[i] = [c * pc["tier1Scale"], c * pc["tier2CooldownScale"]]
            rc_[i] = [r * pc["tier1Scale"], r * pc["tier2RecoverScale"]]
        self.cdT, self.recT = T(cd), T(rc_)
        # ---- hero weapons
        wk = [k for k, v in N["weapons"].items() if v.get("buildUpTarget")]
        self.W = wk
        nW = len(wk)
        wp = N["weapons"]

        def tgt(k, f, d=0.0):
            t = wp[k].get("buildUpTarget")
            return wp[t][f] if t and t in wp else d

        self.wBaseAtk = T([wp[k]["atk"] for k in wk])
        self.wBaseFire = T([wp[k]["fire"] for k in wk])
        self.wBasePest = T([wp[k]["pest"] for k in wk])
        self.wTgtAtk = T([tgt(k, "atk", 1e9) for k in wk])
        self.wTgtFire = T([tgt(k, "fire") for k in wk])
        self.wTgtPest = T([tgt(k, "pest") for k in wk])
        cyc_cfg = cfg["weaponCycles"]
        CM = 4
        hat = N["heroAttacks"]
        tt = [[1.0] * CM for _ in wk]
        th = [[1.0] * CM for _ in wk]
        tm = [[1.0] * CM for _ in wk]
        tf = [[0.0] * CM for _ in wk]
        cl = []
        sty = [[0.0] * CM for _ in wk]
        G = N["growth"]
        for i, k in enumerate(wk):
            kinds = cyc_cfg.get(k, cyc_cfg["default"])[:CM]
            cl.append(len(kinds))
            for j, kd in enumerate(kinds):
                a = next(v for v in hat.values() if wmatch(v["weapon"], k) and v["kind"] == kd)
                tt[i][j] = a["totalFrames"] / self.fr
                th[i][j] = a["hitOnFrame"] / self.fr
                tm[i][j] = a["mult"]
                tf[i][j] = float(kd == "finisher")
                fin = kd == "finisher"
                S_ = G["styleAbs"]
                if "axe" in k:
                    sty[i][j] = (S_["axeFinisherHit"] + A["axeGuardBreakProb"] * S_["axeGuardBreak"]) if fin else 0.0
                elif "dagger" in k:
                    sty[i][j] = S_["daggerHit"] + A["daggerParryProbPerHit"] * S_["daggerParry"]
                elif "staff" in k:
                    sty[i][j] = A["staffExtraSwingProbPerHit"] * S_["staffExtraSwing"]
                elif "bow" in k:
                    sty[i][j] = A["bowWeakShotProbPerHit"] * S_["bowWeakShot"]
        self.tTot, self.tHit, self.tMul, self.tFin = T(tt), T(th), T(tm), T(tf)
        self.cycLen = T(cl, torch.long)
        self.tSty = T(sty)
        # ---- growth
        self.G = G
        self.absKill = T([G["absPerKill"][k] for k in KINDS[:3]] + [G["absPerKill"].get("Elite", 40)])
        # real file: cost(L -> L+1) = levelCostBase + levelCostPerLevel*L  (40 + 20L = 40*(1+0.5L)); cum(n) = b*n + p*n(n-1)/2
        self.lvlA = G["levelCostPerLevel"] / 2.0
        self.lvlB = G["levelCostBase"] - G["levelCostPerLevel"] / 2.0
        self.maxLevel = float(A["maxWeaponLevel"])
        # ---- floors
        fs, fl, fm = N["floorStats"], N["floors"], N["floorMix"]
        z = [0.0] * (NF + 1)
        self.fRooms = T([0.0] + [fs[str(f)]["meanCombatRooms"] for f in range(1, NF + 1)])
        self.fEn = T([0.0] + [fs[str(f)]["meanEnemiesPerRoom"] for f in range(1, NF + 1)])
        t2 = [0.0]
        for f in range(1, NF + 1):
            s = fs[str(f)]
            tot_e = s["meanTier1Enemies"] + s["meanTier2Enemies"]
            p2 = s["meanTier2Enemies"] / tot_e if tot_e > 0 else 0.0
            if ts.get("cellarsMaxTier", 1) >= 2:
                pass
            t2.append(p2)
        self.fT2 = T(t2)
        self.fGuard = T([0.0] + [float(bool(fl[str(f)]["guardian"])) for f in range(1, NF + 1)]) > 0.5
        self.fCalm = T([0.0] + [float(bool(fl[str(f)]["calm"])) for f in range(1, NF + 1)]) > 0.5
        hf = N["healing"].get("healPointFloors")
        if hf:
            heal = [0.0] + [float(f in hf) for f in range(1, NF + 1)]
        else:
            heal = [0.0] + [float(bool(fl[str(f)]["hasHealPoint"])) for f in range(1, NF + 1)]
        self.fHeal = T(heal)
        mix = [[0.0] * 3 for _ in range(NF + 1)]
        for f in range(1, NF + 1):
            w = [float(fm[str(f)].get(k, 0)) for k in KINDS[:3]]
            s = sum(w) or 1.0
            c = 0.0
            for j in range(3):
                c += w[j] / s
                mix[f][j] = c
        self.fMixCum = T(mix)       # cumulative probs for Slime, RatKing, Broom
        # ---- coop / damage
        self.D = N["damage"]
        co = N["coop"]
        self.lifeMult = T(co["lifeMult"][1:5] if len(co["lifeMult"]) == 5 else co["lifeMult"][:4])
        sc = co["strikeCap"][1:5] if len(co["strikeCap"]) == 5 else co["strikeCap"][:4]
        pm = N["enemyPacing"].get("tier1MaxAttackersPerHero", 99)
        # strikeCap 0 = no global cap; per-hero attacker limit (tier-1) still applies (assumption: min of both)
        self.strikeCap = T([min(c if c > 0 else 99, pm * (i + 1)) for i, c in enumerate(sc)], torch.long)
        self.coop = co
        self.heal = N["healing"]
        # loaves need the bakery (real source): 0 unless config assumptions.bakeryBuilt
        self.loavesPer = float(self.heal["loavesPerDive"]) if A.get("bakeryBuilt", False) else 0.0
        self.maxHp = float(self.D["playerMaxHp"])
        self._build_lanes()
        self._alloc()
        self._alloc_hist()
        self.ticks = 0
        self.prepare()

    # ------------------------------------------------------------------ lanes
    def _build_lanes(self):
        dev, T = self.dev, self.T
        cfg = self.cfg
        prof = cfg["skillProfiles"]
        pnames = [k for k in prof if not k.startswith("_")]
        mixw = cfg["partySkillMix"]
        P, ci, wpn, pidx = [], [], [], []
        for c, cd in enumerate(self.case_defs):
            p = int(cd["players"])
            for _ in range(self.lanes):
                P.append(p)
                ci.append(c)
        self.L = len(P)
        L = self.L
        self.P = T(P, torch.long)
        self.ci = T(ci, torch.long)
        self.present = (torch.arange(H, device=dev)[None, :] < self.P[:, None])
        wi = torch.zeros(L, H, dtype=torch.long, device=dev)
        pi = torch.zeros(L, H, dtype=torch.long, device=dev)
        mixp = torch.tensor([mixw.get(n, 0.0) for n in pnames], dtype=torch.float32, device=dev)
        for c, cd in enumerate(self.case_defs):
            m = self.ci == c
            profs = cd.get("profiles", ["average"])
            loads = cd.get("loadouts", ["axe"])
            for h in range(H):
                pn = profs[h % len(profs)]
                if pn == "mix":
                    pick = torch.multinomial(mixp, L, replacement=True)
                else:
                    pick = torch.full((L,), pnames.index(pn), dtype=torch.long, device=dev)
                pi[:, h] = torch.where(m, pick, pi[:, h])
                wi[:, h] = torch.where(m, torch.full((L,), self.W.index(loads[h % len(loads)]), dtype=torch.long, device=dev), wi[:, h])
        self.wpn = wi
        self.pnames = pnames

        def col(key):
            return T([prof[n][key] for n in pnames])[pi]
        self.hitRate, self.uptime = col("hitRate"), col("uptime")
        self.dodge, self.guard = col("dodgeRate"), col("guardRate")
        self.revW, self.loafThr = col("reviveWillingness"), col("loafThresholdHpFrac")
        self.prof_idx = pi
        self.ar = torch.arange(L, device=dev)

    def _alloc(self):
        dev, L, E = self.dev, self.L, self.E
        f32 = torch.float32
        z = lambda *s, dtype=f32: torch.zeros(*s, device=dev, dtype=dtype)
        # lane state
        self.ph = z(L, dtype=torch.long)            # 0 advance/spawn, 1 combat, 2 finished
        self.dive = z(L, dtype=torch.long)
        self.floor = torch.ones(L, device=dev, dtype=torch.long)
        self.room = z(L, dtype=torch.long)
        self.nreg = z(L, dtype=torch.long)
        self.ntot = z(L, dtype=torch.long)
        self.rc = z(L)
        self.healAvail = z(L, dtype=torch.bool)
        self.deaths = z(L)
        # heroes
        self.hhp = torch.full((L, H), self.maxHp, device=dev)
        self.hst = z(L, H, dtype=torch.long)       # 0 up 1 downed 2 out
        self.hdl = z(L, H)
        self.hrev = z(L, H)
        self.hwill = z(L, H, dtype=torch.bool)
        self.hsw = z(L, H)
        self.hhit = z(L, H)
        self.hpend = z(L, H, dtype=torch.bool)
        self.hslot = z(L, H, dtype=torch.long)
        self.hcyc = z(L, H, dtype=torch.long)
        self.hcombo = z(L, H)
        self.hdmgF = z(L, H)
        self.loaves = torch.full((L, H), self.loavesPer, device=dev)
        self.hloafCd = z(L, H)
        # weapon growth (persists across dives)
        self.abs = z(L, H)
        self.bup = z(L, H, dtype=torch.bool)
        self.bupLvl = z(L, H)
        self.lvl = z(L, H)
        self.curAtk = z(L, H)
        self.curFire = z(L, H)
        self.curPest = z(L, H)
        self.refresh()
        # enemies
        self.ealive = z(L, E, dtype=torch.bool)
        self.ehp = z(L, E)
        self.edef = z(L, E)
        self.eatk = z(L, E)
        self.ekind = z(L, E, dtype=torch.long)
        self.et2 = z(L, E, dtype=torch.bool)
        self.ecd = z(L, E)
        self.ewind = z(L, E)
        self.epend = z(L, E, dtype=torch.bool)
        self.eamul = z(L, E)
        self.egb = z(L, E)
        self.ecyc = z(L, E, dtype=torch.long)
        self.erage = z(L, E)
        self.ehitc = z(L, E)
        self.efirst = torch.full((L, E), -1.0, device=dev)
        self.elast = torch.full((L, E, H), -1e9, device=dev)
        self.estyle = z(L, E, H)
        self.iswarden = z(L, dtype=torch.bool)
        # start-of-dive init for all lanes
        allm = torch.ones(L, dtype=torch.bool, device=dev)
        self._start_dive(allm)

    def _alloc_hist(self):
        dev, C, N = self.dev, self.C, self.N_DIVES
        zl = lambda *s: torch.zeros(*s, device=dev, dtype=torch.long)
        zf = lambda *s: torch.zeros(*s, device=dev, dtype=torch.float64)
        self.h_ttk = zl(C * NF * K * NB_T)
        self.h_ttk1 = zl(C * NF * K * NB_T)
        self.s_ttk = zf(C * NF * K)
        self.s_ttk1 = zf(C * NF * K)
        self.h_room = zl(C * NF * NB_T)
        self.h_room1 = zl(C * NF * NB_T)
        self.s_room = zf(C * NF)
        self.s_room1 = zf(C * NF)
        self.h_dmg = zl(C * NF * NB_F)
        self.h_death = zl(C * N * NB_DEATH)
        self.h_lvl = zl(C * N * NB_LVL)
        self.h_floor = zl(C * N * (NF + 1))
        self.c_succ = zl(C * N)
        self.c_dive = zl(C * N)

    # ------------------------------------------------------------------ helpers
    def R(self, *shape):
        return torch.rand(*shape, device=self.dev)

    def growth(self, l):
        g = self.G
        return torch.clamp(l, max=g["earlyLevels"]) * g["atkPerLevelEarly"] + torch.clamp(l - g["earlyLevels"], min=0) * g["atkPerLevelLate"]

    def refresh(self):
        G, A = self.G, self.A
        a, b = self.lvlA, self.lvlB
        if a > 0:
            n = (-b + torch.sqrt(b * b + 4 * a * self.abs)) / (2 * a)
        else:
            n = self.abs / max(b, 1e-6)
        lvl = torch.clamp(torch.floor(n + 1e-4), 0, self.maxLevel)
        self.lvl = lvl
        w = self.wpn
        baseA = self.wBaseAtk[w] + self.growth(lvl)
        if A.get("buildUpEnabled", True):
            can = baseA >= G["buildUpThreshold"] * self.wTgtAtk[w]
            new = can & ~self.bup
            self.bupLvl = torch.where(new, lvl, self.bupLvl)
            self.bup = self.bup | can
        bb = 1.0 + G["buildUpBonus"]
        self.curAtk = torch.where(self.bup, self.wTgtAtk[w] * bb + self.growth(lvl) - self.growth(self.bupLvl), baseA)
        self.curFire = torch.where(self.bup, self.wTgtFire[w] * bb, self.wBaseFire[w])
        self.curPest = torch.where(self.bup, self.wTgtPest[w] * bb, self.wBasePest[w])

    def _sample_floor(self, mask, floor):
        """(re)roll rooms and heal point for lanes in mask arriving on `floor` [L]."""
        m = self.fRooms[floor]
        base = torch.floor(m)
        nr = (base + (self.R(self.L) < (m - base)).float()).long()
        # 'calm' in the real file does not mean no combat (floor 1 is calm with 2 rooms): rooms come from meanCombatRooms only.
        # Guardian floor: its combat room(s) include the Warden (last room = Warden + adds).
        self.nreg = torch.where(mask, nr, self.nreg)
        self.ntot = torch.where(mask, nr, self.ntot)
        self.room = torch.where(mask, torch.zeros_like(self.room), self.room)
        ha = (self.R(self.L) * 100.0 < self.heal["healPointChancePercent"]) & (self.fHeal[floor] > 0.5)
        self.healAvail = torch.where(mask, ha, self.healAvail)
        self.hdmgF = torch.where(mask[:, None], torch.zeros_like(self.hdmgF), self.hdmgF)

    def _start_dive(self, mask):
        m2 = mask[:, None]
        self.floor = torch.where(mask, torch.ones_like(self.floor), self.floor)
        self.hhp = torch.where(m2, torch.full_like(self.hhp, self.maxHp), self.hhp)
        self.hst = torch.where(m2, torch.zeros_like(self.hst), self.hst)
        self.loaves = torch.where(m2, torch.full_like(self.loaves, self.loavesPer), self.loaves)
        self.hloafCd = torch.where(m2, torch.zeros_like(self.hloafCd), self.hloafCd)
        self.deaths = torch.where(mask, torch.zeros_like(self.deaths), self.deaths)
        self.ph = torch.where(mask, torch.zeros_like(self.ph), self.ph)
        self._sample_floor(mask, self.floor)

    def _end_dive(self, mask, success):
        """record dive stats for lanes in mask, advance dive counter, reset or finish."""
        C, N = self.C, self.N_DIVES
        m = mask.long()
        d = torch.clamp(self.dive, max=N - 1)
        base = self.ci * N + d
        self.c_dive.index_add_(0, base, m)
        self.c_succ.index_add_(0, base, (mask & success).long())
        dn = torch.clamp(self.deaths, max=NB_DEATH - 1).long()
        self.h_death.index_add_(0, base * NB_DEATH + dn, m)
        fr = torch.clamp(self.floor, 1, NF)
        fr = torch.where(success, torch.full_like(fr, NF), fr)
        self.h_floor.index_add_(0, base * (NF + 1) + fr, m)
        lv = torch.clamp(self.lvl, 0, NB_LVL - 1).long()
        idx = (base[:, None] * NB_LVL + lv)
        w = (mask[:, None] & self.present).long()
        self.h_lvl.index_add_(0, idx.reshape(-1), w.reshape(-1))
        self.dive = torch.where(mask, self.dive + 1, self.dive)
        fin = mask & (self.dive >= N)
        self.ph = torch.where(fin, torch.full_like(self.ph, 2), self.ph)
        self._start_dive(mask & ~fin)

    def _rec_floor_dmg(self, mask):
        frac = (self.hdmgF * self.present).sum(1) / (self.P.float() * self.maxHp)
        b = torch.clamp((frac / BIN_F).long(), 0, NB_F - 1)
        idx = (self.ci * NF + torch.clamp(self.floor - 1, 0, NF - 1)) * NB_F + b
        self.h_dmg.index_add_(0, idx, mask.long())

    def _spawn(self, mask):
        """spawn the next room for lanes in mask."""
        L, E, dev = self.L, self.E, self.dev
        f = self.floor
        isw = self.fGuard[f] & (self.room >= self.ntot - 1)
        m = self.fEn[f]
        base = torch.floor(m)
        n = (base + (self.R(L) < (m - base)).float()).long() + (self.P - 1) * int(self.coop["extraEnemiesPerExtraPlayer"])
        n = torch.clamp(n, 1, E)
        slot = torch.arange(E, device=dev)[None, :]
        wslot = isw[:, None] & (slot == 0)                     # Warden = slot 0, rest are tier-1 adds
        alive = slot < n[:, None]
        r = self.R(L, E)
        cum = self.fMixCum[f]                                    # [L,3]
        kind = (r[:, :, None] > cum[:, None, :]).sum(-1).clamp(max=2)
        kind = torch.where(wslot, torch.full_like(kind, 3), kind)
        t2 = (self.R(L, E) < self.fT2[f][:, None]) & (self.ts.get("cellarsMaxTier", 1) >= 2) & ~isw[:, None]
        t2 = t2 | wslot
        ts = self.ts
        hp = self.eHp[kind] * torch.where(t2, torch.full_like(r, ts["tier2Life"]), torch.ones_like(r)) * self.lifeMult[self.P - 1][:, None]
        dfn = self.eDef[kind] * torch.where(t2, torch.full_like(r, ts["tier2Def"]), torch.ones_like(r))
        atk = self.eAtk[kind] * torch.where(t2, torch.full_like(r, ts["tier2Atk"]), torch.full_like(r, ts["tier1Atk"]))
        cd0 = self.cdT[kind, t2.long()] * self.R(L, E) + self.A["engageDelaySeconds"]
        mE = mask[:, None]
        w = lambda new, old: torch.where(mE, new, old)
        self.ealive = w(alive, self.ealive)
        self.ehp = w(hp, self.ehp)
        self.edef = w(dfn, self.edef)
        self.eatk = w(atk, self.eatk)
        self.ekind = w(kind, self.ekind)
        self.et2 = w(t2, self.et2)
        self.ecd = w(cd0, self.ecd)
        self.ewind = w(torch.zeros_like(r), self.ewind)
        self.epend = w(torch.zeros_like(self.epend), self.epend)
        self.ecyc = w(torch.zeros_like(self.ecyc), self.ecyc)
        self.erage = w(torch.zeros_like(r), self.erage)
        self.ehitc = w(torch.zeros_like(r), self.ehitc)
        self.efirst = w(torch.full_like(r, -1.0), self.efirst)
        self.elast = torch.where(mask[:, None, None], torch.full_like(self.elast, -1e9), self.elast)
        self.estyle = torch.where(mask[:, None, None], torch.zeros_like(self.estyle), self.estyle)
        self.iswarden = torch.where(mask, isw, self.iswarden)
        self.rc = torch.where(mask, torch.zeros_like(self.rc), self.rc)
        m2 = mask[:, None]
        self.hsw = torch.where(m2, self.A["engageDelaySeconds"] + 0.5 * self.R(L, H), self.hsw)
        self.hpend = torch.where(m2, torch.zeros_like(self.hpend), self.hpend)
        self.hcyc = torch.where(m2, torch.zeros_like(self.hcyc), self.hcyc)
        self.hcombo = torch.where(m2, torch.zeros_like(self.hcombo), self.hcombo)
        self.ph = torch.where(mask, torch.ones_like(self.ph), self.ph)

    def _revive_all(self, mask):
        """downed / out heroes rejoin at reviveHpFraction (room end, bank)."""
        m = mask[:, None] & (self.hst > 0)
        self.hhp = torch.where(m, torch.full_like(self.hhp, self.coop["reviveHpFraction"] * self.maxHp), self.hhp)
        self.hst = torch.where(m, torch.zeros_like(self.hst), self.hst)

    # ------------------------------------------------------------------ one tick
    def step(self):
        dt, D, A = self.dt, self.D, self.A
        L, E, dev = self.L, self.E, self.dev
        ar = self.ar
        act = self.ph == 1
        up = (self.hst == 0) & self.present
        actH = act[:, None] & up
        self.rc = self.rc + dt * act
        # ---------------- target selection (lowest hp alive enemy)
        alive = self.ealive
        anyAlive = alive.any(1)
        tgt = torch.where(alive, self.ehp, torch.full_like(self.ehp, 1e18)).argmin(1)
        tk = self.ekind[ar, tgt]
        tgt1 = tgt[:, None]
        # ---------------- hero swing start
        self.hsw = torch.clamp(self.hsw - dt * actH, min=-dt)
        self.hhit = self.hhit - dt
        start = actH & (self.hsw <= 0) & anyAlive[:, None]
        w = self.wpn
        slot = self.hcyc
        tot = self.tTot[w, slot]
        hon = self.tHit[w, slot]
        repos = self.reposExtra[tk][:, None]
        dur = tot / torch.clamp(self.uptime, min=0.05) + repos
        self.hsw = torch.where(start, self.hsw + dur, self.hsw)
        self.hslot = torch.where(start, slot, self.hslot)
        self.hhit = torch.where(start, hon, self.hhit)
        self.hpend = self.hpend | start
        self.hcyc = torch.where(start, (slot + 1) % self.cycLen[w], self.hcyc)
        # ---------------- hero hits
        due = self.hpend & (self.hhit <= 0) & actH & anyAlive[:, None]
        self.hpend = self.hpend & ~due
        hr = self.hitRate * self.kindHit[tk][:, None]
        connect = due & (self.R(L, H) < hr)
        mult = self.tMul[w, self.hslot]
        isfin = self.tFin[w, self.hslot]
        crit = self.R(L, H) < D["critChance"]
        tdef = self.edef[ar, tgt][:, None]
        dmg = damage_formula(self.curAtk, mult, tdef, self.curFire, self.eWeak[tk][:, None], self.curPest,
                             self.ePest[tk][:, None], crit, self.hcombo, isfin, D)
        blocked = (self.eBlock[tk][:, None] > 0.5) & (self.R(L, H) < A["broomBlockFraction"])
        dmg = torch.where(blocked, torch.clamp(torch.ceil(dmg * A["broomBlockedMult"] - 1e-6), min=D["minDamage"]), dmg)
        dmg = dmg * connect
        tot_dmg = dmg.sum(1)
        hit_any = connect.any(1)
        # combo bookkeeping
        self.hcombo = torch.where(due & (isfin > 0.5), torch.zeros_like(self.hcombo), self.hcombo + connect.float())
        # apply to target enemy
        cur_first = self.efirst[ar, tgt]
        self.efirst[ar, tgt] = torch.where((cur_first < 0) & hit_any, self.rc, cur_first)
        self.ehp[ar, tgt] = self.ehp[ar, tgt] - tot_dmg
        self.elast[ar, tgt] = torch.where(connect, self.rc[:, None], self.elast[ar, tgt])
        # style ABS (expected value), per-enemy cap
        want = self.tSty[w, self.hslot] * connect
        cum = self.estyle[ar, tgt]
        add = torch.clamp(torch.minimum(want, self.G["styleAbsCapPerEnemy"] - cum), min=0)
        self.estyle[ar, tgt] = cum + add
        self.abs = self.abs + add
        # rage
        hc = self.ehitc[ar, tgt] + connect.sum(1).float()
        notw = tk != 3
        trig = (hc >= D["rageHits"]) & notw
        self.ehitc[ar, tgt] = torch.where(trig, torch.zeros_like(hc), hc)
        self.erage[ar, tgt] = torch.where(trig, torch.full_like(hc, D["rageSeconds"]), self.erage[ar, tgt])
        # kills
        thp = self.ehp[ar, tgt]
        killed = hit_any & (thp <= 0) & self.ealive[ar, tgt]
        self.ealive[ar, tgt] = self.ealive[ar, tgt] & ~killed
        finKill = (connect & (isfin > 0.5)).any(1)
        amt = self.absKill[tk] * torch.where(finKill, torch.full_like(thp, self.G["finisherKillMult"]), torch.ones_like(thp))
        cred = (self.elast[ar, tgt] >= (self.rc - self.G["creditWindowSeconds"])[:, None]) & self.present
        self.abs = self.abs + cred * (amt * killed)[:, None]
        self.refresh()
        # kill metrics
        ttk = self.rc - self.efirst[ar, tgt]
        fidx = torch.clamp(self.floor - 1, 0, NF - 1)
        kb = (self.ci * NF + fidx) * K + tk
        tb = torch.clamp((ttk / BIN_T).long(), 0, NB_T - 1)
        km = killed.long()
        self.h_ttk.index_add_(0, kb * NB_T + tb, km)
        self.s_ttk.index_add_(0, kb, ttk.double() * killed)
        k1 = (killed & (self.dive == 0)).long()
        self.h_ttk1.index_add_(0, kb * NB_T + tb, k1)
        self.s_ttk1.index_add_(0, kb, ttk.double() * k1)
        # ---------------- enemy attacks
        alive = self.ealive
        self.ecd = self.ecd - dt * alive
        self.ewind = self.ewind - dt
        self.erage = self.erage - dt
        self.epend = self.epend & alive
        ready = alive & act[:, None] & (self.ecd <= 0) & ~self.epend & up.any(1)[:, None]
        nwind = self.epend.sum(1)
        cap = self.strikeCap[self.P - 1]
        starts = ready & ((torch.cumsum(ready.long(), 1) + nwind[:, None]) <= cap[:, None])
        aidx = self.ecyc % self.nAtk[self.ekind]
        k_ = self.ekind
        self.ewind = torch.where(starts, self.aHit[k_, aidx], self.ewind)
        self.epend = self.epend | starts
        self.eamul = torch.where(starts, self.aMul[k_, aidx], self.eamul)
        self.egb = torch.where(starts, self.aGB[k_, aidx], self.egb)
        t2l = self.et2.long()
        newcd = self.aTot[k_, aidx] + self.cdT[k_, t2l] + self.recT[k_, t2l]
        self.ecd = torch.where(starts, newcd, self.ecd)
        self.ecyc = torch.where(starts, self.ecyc + 1, self.ecyc)
        # resolve landing hits
        land = self.epend & (self.ewind <= 0) & act[:, None]
        self.epend = self.epend & ~land
        rr = torch.where(up[:, None, :], self.R(L, E, H), torch.full((L, E, H), -1.0, device=dev))
        th = rr.argmax(-1)                                      # [L,E] target hero
        raw = torch.ceil(self.eatk * self.eamul * 100.0 / (100.0 + D["playerDef"]) - 1e-6)
        raw = raw * torch.where(self.erage > 0, torch.full_like(raw, D["rageMult"]), torch.ones_like(raw))
        dodged = self.R(L, E) < self.dodge.gather(1, th)
        guarded = (self.R(L, E) < self.guard.gather(1, th)) & (self.egb < 0.5)
        raw = torch.where(guarded, raw * D["guardMult"], raw)
        raw = torch.clamp(torch.ceil(raw - 1e-6), min=D["minDamage"])
        raw = raw * (land & ~dodged & up.any(1)[:, None])
        onehot = torch.nn.functional.one_hot(th, H).float()
        dmgH = (onehot * raw[:, :, None]).sum(1)                # [L,H]
        dmgH = dmgH * up
        self.hhp = self.hhp - dmgH
        self.hdmgF = self.hdmgF + dmgH
        # ---------------- loaves
        self.hloafCd = self.hloafCd - dt
        eat = actH & (self.hhp > 0) & (self.hhp / self.maxHp < self.loafThr) & (self.loaves > 0) & (self.hloafCd <= 0)
        self.hhp = torch.where(eat, torch.clamp(self.hhp + self.heal["loafHeal"], max=self.maxHp), self.hhp)
        self.loaves = self.loaves - eat.float()
        self.hloafCd = torch.where(eat, torch.full_like(self.hloafCd, A["loafCooldownSeconds"]), self.hloafCd)
        # ---------------- deaths, revive, timeout
        died = actH & (self.hhp <= 0)
        self.hst = torch.where(died, torch.ones_like(self.hst), self.hst)
        self.hdl = torch.where(died, torch.full_like(self.hdl, self.coop["deathDownedSeconds"]), self.hdl)
        self.hwill = torch.where(died, self.R(L, H) < self.revW, self.hwill)
        self.hrev = torch.where(died, torch.full_like(self.hrev, self.coop["reviveHoldSeconds"] + A["reviveTravelSeconds"]), self.hrev)
        self.hhp = torch.where(died, torch.zeros_like(self.hhp), self.hhp)
        self.deaths = self.deaths + died.sum(1).float()
        upnow = ((self.hst == 0) & self.present)
        anyUp = upnow.any(1)
        down = (self.hst == 1) & act[:, None]
        revving = down & self.hwill & anyUp[:, None]
        self.hrev = self.hrev - dt * revving
        done_rev = revving & (self.hrev <= 0)
        self.hhp = torch.where(done_rev, torch.full_like(self.hhp, self.coop["reviveHpFraction"] * self.maxHp), self.hhp)
        self.hst = torch.where(done_rev, torch.zeros_like(self.hst), self.hst)
        self.hsw = torch.where(done_rev, torch.full_like(self.hsw, 0.5), self.hsw)
        down2 = (self.hst == 1) & act[:, None]
        self.hdl = self.hdl - dt * down2
        out = down2 & (self.hdl <= 0)
        self.hst = torch.where(out, torch.full_like(self.hst, 2), self.hst)
        # ---------------- room end / wipe
        anyUp = ((self.hst == 0) & self.present).any(1)
        cleared = act & ~self.ealive.any(1)
        wipe = act & ~anyUp
        tmo = act & (self.rc > self.timeout) & ~cleared
        fail = wipe | tmo
        cleared = cleared & ~fail
        # room clear metrics (regular rooms only, warden fight is in TTK[3])
        reg = cleared & ~self.iswarden
        fidx = torch.clamp(self.floor - 1, 0, NF - 1)
        rb = (self.ci * NF + fidx)
        tb = torch.clamp((self.rc / BIN_T).long(), 0, NB_T - 1)
        self.h_room.index_add_(0, rb * NB_T + tb, reg.long())
        self.s_room.index_add_(0, rb, self.rc.double() * reg)
        r1 = (reg & (self.dive == 0)).long()
        self.h_room1.index_add_(0, rb * NB_T + tb, r1)
        self.s_room1.index_add_(0, rb, self.rc.double() * r1)
        # heal point (one use per floor) then revive downed
        low = ((self.hhp < A["healPointThresholdFrac"] * self.maxHp) & self.present).any(1)
        useheal = cleared & self.healAvail & low
        self.healAvail = self.healAvail & ~useheal
        self.hhp = torch.where(useheal[:, None] & self.present, torch.full_like(self.hhp, self.maxHp), self.hhp)
        self._revive_all(cleared)
        self.room = torch.where(cleared, self.room + 1, self.room)
        self.ph = torch.where(cleared, torch.zeros_like(self.ph), self.ph)
        # ---------------- advance: floor finish / spawn / dive end
        adv = self.ph == 0
        fin_floor = adv & (self.room >= self.ntot)
        spawn = adv & ~fin_floor
        # wipes: record floor damage, end dive failed
        self._rec_floor_dmg(fail)
        # floor finished (only floors that had rooms record damage)
        self._rec_floor_dmg(fin_floor & (self.ntot > 0))
        bank = fin_floor & self.fGuard[self.floor] & bool(self.heal.get("bankFullHeal", True))
        self.hhp = torch.where(bank[:, None] & self.present, torch.full_like(self.hhp, self.maxHp), self.hhp)
        self._revive_all(bank)
        self.floor = torch.where(fin_floor, self.floor + 1, self.floor)
        done_ok = fin_floor & (self.floor > NF)
        self.floor = torch.where(done_ok, torch.full_like(self.floor, NF), self.floor)
        newfloor = fin_floor & ~done_ok
        self._sample_floor(newfloor, torch.clamp(self.floor, 1, NF))
        self._spawn(spawn)
        self._end_dive(fail, torch.zeros_like(fail))
        self._end_dive(done_ok, torch.ones_like(done_ok))
        self.ticks += 1

    # ------------------------------------------------------------------ run
    def prepare(self):
        A = self.A
        pn = self.pnames
        # ratKing assumptions per kind
        self.reposExtra = self.T([0.0, A["ratKingReposExtraSeconds"], 0.0, 0.0])
        self.kindHit = self.T([1.0, A["ratKingHitRateMult"], 1.0, 1.0])

    _STATE = ("ph dive floor room nreg ntot rc healAvail deaths hhp hst hdl hrev hwill hsw hhit hpend hslot hcyc hcombo "
              "hdmgF loaves hloafCd abs bup bupLvl lvl curAtk curFire curPest ealive ehp edef eatk ekind et2 ecd ewind "
              "epend eamul egb ecyc erage ehitc efirst elast estyle iswarden").split()

    def _capture(self):
        """CUDA graph of one tick. State tensors are updated in place (copy_ back at the end of capture)."""
        for _ in range(3):
            self.step()
        torch.cuda.synchronize()
        orig = {n: getattr(self, n) for n in self._STATE}
        g = torch.cuda.CUDAGraph()
        with torch.cuda.graph(g):
            self.step()
            for n in self._STATE:
                orig[n].copy_(getattr(self, n))
        for n in self._STATE:
            setattr(self, n, orig[n])
        return g

    def run(self, max_ticks=400000, check_every=500, graph=None):
        use_graph = (self.dev.type == "cuda") if graph is None else graph
        step = self._capture().replay if use_graph else self.step
        while self.ticks < max_ticks:
            for _ in range(check_every):
                step()
            if use_graph:
                self.ticks += check_every
            if bool((self.ph == 2).all().item()):
                break
        return self.ticks
