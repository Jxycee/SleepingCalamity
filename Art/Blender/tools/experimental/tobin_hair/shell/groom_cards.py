# Groom -> baked hair cards. exec'd inside build_shell_hair.py (groom=1); uses its namespace (cards = guide list, TREE, clearance_push,
# resample, rng, F, kw, OUT, C, Vector, bmesh, bpy, np, math).
# 1. Every guide curve gets child strands: roots spread across the guide's width on the scalp side, paths follow the guide, clump toward
#    it near the tip, plus frizz noise and length variation (Blender GN Interpolate/Clump/Frizz, done in numpy so it stays scriptable).
# 2. Optional reference hull (hull=<dir from hull_prep.py>): child strands are cut where they leave the reference silhouette (front, side),
#    so the outline follows the reference instead of tuned numbers.
# 3. One card per guide: a ribbon along the guide whose width is the children's spread at each point, outward normal + reversed twin.
# 4. Bake: each card's own children are projected into its (across, along) frame and drawn as anti-aliased strands into an atlas tile
#    (groom_bake.py, cv2) -> Hair_Cards_{BaseColor,Normal,MetallicSmooth}. Tiles go to the biggest cards; smaller cards reuse tiles.
import subprocess, json
GROOM_TEX = os.path.join(OUT, 'groom_tex')
HULL = None
if kw.get('hull'):
    _hd = kw['hull']; _cal = json.load(open(os.path.join(_hd, 'hull_cal.json')))
    _masks = {n: np.load(os.path.join(_hd, f'mask_{n}.npy')) for n in ('front', 'side')}
    def in_hull(P, dil=3):
        P = np.atleast_2d(P); ok = np.ones(len(P), bool)
        for n, m in _masks.items():
            c = _cal[n]; a = P[:, 0] if c['axis'] == 'x' else P[:, 1]
            px = np.round(c['cx'] + c['sign'] * c['s'] * a).astype(int); py = np.round(c['eye_py'] - c.get('sz', c['s']) * (P[:, 2] - _cal['eye_z'])).astype(int)
            inside = np.zeros(len(P), bool)
            for dx in (-dil, 0, dil):
                for dy in (-dil, 0, dil):
                    x = np.clip(px + dx, 0, m.shape[1] - 1); y = np.clip(py + dy, 0, m.shape[0] - 1)
                    inside |= (m[y, x] > 0) & (px + dx >= 0) & (px + dx < m.shape[1]) & (py + dy >= 0) & (py + dy < m.shape[0])
            ok &= inside
        return ok
    HULL = in_hull if kw.get('hullcut', '0') == '1' else None
    HULLQ = in_hull
    # head slice centres (y) per z, for front-contour tips
    _HV = np.array([v.co[:] for v in bpy.data.objects['AvatarHead'].data.vertices]); _zb = np.arange(1.55, 1.90, 0.005)
    _yc = np.array([(_HV[np.abs(_HV[:, 2] - z) < 0.004][:, 1].min() + _HV[np.abs(_HV[:, 2] - z) < 0.004][:, 1].max()) / 2 if (np.abs(_HV[:, 2] - z) < 0.004).any() else C.y for z in _zb])
    def r_hull(d):   # radius from C to the reference hull along unit direction d (marching inward from 25 cm)
        rs = np.linspace(0.25, 0.0, 200); P = np.array(C)[None] + rs[:, None] * d[None]; ok = in_hull(P, dil=2)
        k = np.argmax(ok); return float(rs[k]) if ok.any() else 0.0
    # ---- outline-targeted guides: tips on the reference silhouette, roots on the scalp, routed along the hair flow ----
    def to3d(view, px, py):
        c = _cal[view]; z = _cal['eye_z'] - (py - c['eye_py']) / c.get('sz', c['s']); a = (px - c['cx']) / (c['s'] * c['sign'])
        if c['axis'] == 'x': return np.array([a, float(np.interp(z, _zb, _yc)), z])
        return np.array([0.0, a, z])
    if kw.get('field', '1') == '1':
        # ---- streamline guides: one flow field (the shell's swirled meridians, part, fringe sweep), evenly spread roots, layered lifts,
        #      length = until the strand leaves the reference hull (or the region's maximum) -> coherent flow, silhouette from the reference ----
        keep_named = [c for c in cards if False]
        cards.clear()
        def ext_until_hull(p, d, steer, maxlen, ds=0.004, minc=0.006, cs=0.0):
            out = []; s_ = 0.0
            while s_ < maxlen:
                acc = steer(s_ / maxlen, p, d); d = d + acc * ds; d /= np.linalg.norm(d); p = clearance_push(p + d * ds, minc, cs); s_ += ds
                out.append(np.append(p, float(sstep(0.0, 0.03, s_))))
                if not in_hull(p[None], dil=1)[0] and s_ > 0.01: break
            return out
        nseed = int(kw.get('nseed', 440)); made = 0
        for k in range(nseed):
            a = rng.uniform(-np.pi, np.pi); t0 = rng.uniform(0.02, 0.9) ** 1.3
            q0, n0 = shell_at(a, t0); ap = a + float(swirl(a, t0)); ph = float(phi_of(shell_at(solve_a(ap, 1.0), 1.0)[0][None])[0])   # region = where the strand reaches the hair edge, not where it roots
            lam = rng.uniform(0.0, 1.0) ** F('lampow', 0.45)                                     # layer: 0 = on the scalp, 1 = out at the hull
            gap = float(np.clip(r_hull((q0 - np.array(C)) / np.linalg.norm(q0 - np.array(C))) - np.linalg.norm(q0 - np.array(C)), 0.0, 0.045))
            hmax = 0.002 + lam * gap
            front = abs(ph) < 50
            if front: ph = float(phi_of(shell_at(a, 1.0)[0][None])[0])
            ts = np.linspace(t0, 1.0, max(3, int((1.0 - t0) * 16) + 2)); pts = []
            for i, t in enumerate(ts):
                aa = solve_a(ap, t) if not front else a; q, n = shell_at(aa, t); u = i / max(len(ts) - 1, 1)
                pts.append(np.append(q + n * (-0.002 + (hmax + 0.002) * float(sstep(0.0, 0.4, u))), 0.0))
            p = pts[-1][:3].copy(); d = pts[-1][:3] - pts[-2][:3]; d /= np.linalg.norm(d) + 1e-12
            if front:      # fringe: diagonal toward the character's right, stops near the brow / eye line or the hull
                tgt = np.array([-rng.uniform(0.6, 1.15), -0.2, -1.0]); tgt /= np.linalg.norm(tgt)
                st = lambda s_, p_, d_, tgt=tgt: (tgt - d_) * 45.0; ml = 0.09
            elif abs(ph) > 130:
                fk = rng.uniform(3, 12); st = lambda s_, p_, d_, fk=fk: G * 5.0 + outward_xy(p_) * (1.0 + fk * s_); ml = 0.03
            else:
                spl = rng.uniform(3, 9); st = lambda s_, p_, d_, spl=spl: G * 7.0 + outward_xy(p_) * (spl - 10.0 * max(0, s_ - 0.6)); ml = 0.06
            pts += ext_until_hull(p, d, st, ml, cs=0.003 if front else 0.0)
            P4 = np.array(pts)
            if front:
                zs = rng.uniform(1.708, 1.742) + 0.025 * max(0.0, ph / 50.0)   # shorter left of the part: that eye stays visible
                P4 = P4[P4[:, 2] > zs]
            if len(P4) < 3: continue
            cards.append((P4, rng.uniform(0.02, 0.036), int(rng.integers(0, 8)), 1.0)); made += 1
            if kw.get('dbg') and (P4[:, 2].min() < 1.70 and P4[:, 1].min() < -0.10): print('LOWFACE', 'front' if front else 'other', round(ph), round(t0, 2), P4[:, :3].min(0).round(3), P4[-1, :3].round(3))
        print('FIELD guides', made)
    tips = []
    for view in (('front', 'side') if kw.get('outline', '0') == '1' else ()):
        for (px, py) in json.load(open(os.path.join(_hd, f'contour_{view}.json')))[::int(kw.get('tipstep', 2))]:
            T = to3d(view, px, py)
            if not (1.625 < T[2] < 1.93): continue
            if T[2] > F('toptip', 1.83) and rng.random() > 0.2: continue   # the crown outline comes from the lifted layers; only a few wisps aim at it
            loc, nr, _, d = TREE.find_nearest(Vector(T), 0.3)
            if loc is None or (Vector(T) - loc).dot(nr) < 0.006: continue        # face / neck outline, not hair
            if view == 'side' and T[1] < -0.10 and T[2] < 1.70: continue         # nose / lips region of the profile
            tips.append(T)
    def grid_coords(P):
        d = np.asarray(P) - np.array(C); d /= np.linalg.norm(d); a = math.atan2(float(d @ np.array(E2)), float(d @ np.array(E1))); p = math.acos(float(np.clip(d @ np.array(WD), -1, 1)))
        j = int(round((a + np.pi) / (2 * np.pi) * NA)) % NA; return a, p / max(pedge_t[j], 1e-3)
    nout = 0
    for T in tips:
        a, tT = grid_coords(T); t0 = float(np.clip(tT * 0.3, 0.03, 0.45)); te = float(np.clip(min(tT, 1.0) - 0.12, t0 + 0.05, 1.0))
        ts = np.linspace(t0, te, 8); on = []
        for k, t in enumerate(ts):
            q, n = shell_at(a, t); on.append(q + n * (-0.002 + 0.006 * min(1.0, k / 3)))
        P0 = on[-1]; tg = on[-1] - on[-2]; tg /= np.linalg.norm(tg) + 1e-12; Lb = float(np.linalg.norm(T - P0))
        if Lb < 0.004: continue
        P1 = P0 + tg * Lb * 0.45; P2 = T - (T - P0) / max(Lb, 1e-6) * Lb * 0.25 + G * 0.01
        bz = [(1 - u) ** 3 * P0 + 3 * (1 - u) ** 2 * u * P1 + 3 * (1 - u) * u ** 2 * P2 + u ** 3 * T for u in np.linspace(0, 1, 10)[1:]]
        bz = [clearance_push(q, 0.006, 0.004) for q in bz]
        pts = [np.append(q, 0.0) for q in on] + [np.append(q, float(sstep(0, 0.03, np.linalg.norm(q - P0)))) for q in bz]
        cards.append((np.array(pts), rng.uniform(0.022, 0.036), int(rng.integers(0, 8)), 1.0)); nout += 1
    print('OUTLINE guides', nout, 'of', len(tips), 'tips')
    # ---- mid layer: every guide again, lifted part of the way toward the hull (volume from layered cards, not a thick cap) ----
    if kw.get('midlayer', '0') == '1':
        base = list(cards)
        for (pts, w, var, tw) in base:
            if rng.random() > F('midfrac', 0.6): continue
            P = pts[:, :3]; f = rng.uniform(0.3, 0.65); newp = []
            for i, q in enumerate(P):
                d = q - np.array(C); rn = float(np.linalg.norm(d)); d /= rn; gap = max(0.0, r_hull(d) - rn)
                lift = f * gap * float(sstep(0.0, 0.25, i / max(len(P) - 1, 1)))   # roots stay at the scalp
                newp.append(clearance_push(q + d * min(lift, 0.03), 0.004))
            cards.append((np.hstack([np.array(newp), pts[:, 3:4]]), w * rng.uniform(0.8, 1.1), int(rng.integers(0, 8)), tw))
        print('MID layer guides', len(cards) - len(base))

def frame_at(Pp, i):
    n = len(Pp) - 1; p = Pp[i]
    tng = Pp[min(i + 1, n)] - Pp[max(i - 1, 0)]; tng /= np.linalg.norm(tng) + 1e-12
    loc, nr, _, d = TREE.find_nearest(Vector(p), 0.3); out = np.array(nr) if loc is not None else (p - np.array(C))
    out = out - tng * out.dot(tng); out /= np.linalg.norm(out) + 1e-12
    b = np.cross(tng, out); b /= np.linalg.norm(b) + 1e-12
    return tng, out, b

def build_groom():
    NS = int(kw.get('gseg', 20)); groups = []; ncut = 0; nchild = 0
    for gi, (pts, w0, var, tw) in enumerate(cards):
        Pp4, L = resample(pts, NS + 1)
        if L < 0.008: continue
        Pp = Pp4[:, :3]; FR = Pp4[:, 3]; W = w0 * tw
        fr = [frame_at(Pp, i) for i in range(NS + 1)]
        nc = int(np.clip(W / 0.00032, 20, int(kw.get('gmax', 130))))
        kids = []
        for c in range(nc):
            off = rng.uniform(-0.5, 0.5) * W; dep = rng.uniform(-0.25, 0.25) * W * 0.5     # across / depth offset at the root
            clump = rng.uniform(0.72, 0.97); lenf = rng.uniform(0.72, 1.04); ph = rng.uniform(0, 6.3); fz = rng.uniform(0.0, F('frizz', 0.0008))
            pts_c = []
            for i in range(NS + 1):
                s = i / NS
                if s > lenf: break
                k = 1.0 - clump * s ** 0.9                                                 # clumps toward the guide near the tip
                t_, o_, b_ = fr[i]
                q = Pp[i] + b_ * off * k + o_ * dep * k + (b_ * math.sin(ph + s * 9.0) + o_ * math.cos(ph * 1.7 + s * 7.0)) * fz * s
                pts_c.append(q)
            if len(pts_c) < 3: continue
            pts_c = np.array(pts_c)
            if HULL is not None:   # cut at the first point outside the reference silhouette (only past the first third: roots stay)
                ok = HULL(pts_c); bad = np.where(~ok[len(ok) // 3:])[0]
                if len(bad): pts_c = pts_c[:len(ok) // 3 + bad[0]]; ncut += 1
                if len(pts_c) < 3: continue
            pts_c = np.array([clearance_push(q, 0.0025) for q in pts_c])
            kids.append((pts_c, off, len(pts_c) - 1))
        if len(kids) < 4: continue
        nchild += len(kids)
        # card width per row = children's spread across the guide (+10 %), tapering where children end
        widths = []
        for i in range(NS + 1):
            t_, o_, b_ = fr[i]; xs = [abs(np.dot(kc[0][i] - Pp[i], b_)) for kc in kids if kc[2] >= i]
            widths.append(2.2 * max(xs) if xs else 0.0005)
        wsm = np.convolve(np.pad(np.array(widths), 2, mode='edge'), np.ones(5) / 5, mode='valid')   # smooth along the card (row-to-row width jumps baked as stair steps)
        groups.append(dict(P=Pp, FR=FR, fr=fr, W=np.maximum(wsm, 0.0008), kids=kids, area=float(np.sum(widths)) * L))
    print('GROOM guides', len(groups), 'children', nchild, 'cut by hull', ncut)
    # atlas tiles: 16 x 4 tiles (128 x 512 px of a 2048 atlas); biggest cards get their own tile, the rest reuse one at random
    order = np.argsort([-g['area'] for g in groups]); NT = 64
    for r, gi in enumerate(order): groups[gi]['tile'] = int(r) if r < NT else int(rng.integers(0, NT))
    # bake input: per owning card, children in (u across 0..1, v along 0..1, depth) coordinates
    bake = []
    for r, gi in enumerate(order[:NT]):
        g = groups[gi]; strands = []
        for (pc, off, last) in g['kids']:
            uv = []
            for i, q in enumerate(pc):
                t_, o_, b_ = g['fr'][i]; d = q - g['P'][i]
                uv.append((0.5 + np.dot(d, b_) / g['W'][i], i / (len(g['P']) - 1), float(np.dot(d, o_) / (g['W'][i] + 1e-9))))
            strands.append(uv)
        bake.append(dict(tile=r, strands=strands))
    os.makedirs(GROOM_TEX, exist_ok=True); jp = os.path.join(GROOM_TEX, 'bake.json'); json.dump(bake, open(jp, 'w'))
    PY = kw.get('py', 'C:/AI/trellis2/venv/Scripts/python.exe')
    r = subprocess.run([PY, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'groom_bake.py'), jp, GROOM_TEX], capture_output=True, text=True)
    print('BAKE', r.stdout.strip()[-200:], r.stderr.strip()[-400:])
    # cards (front + reversed twin, both with the outward normal), UV into the card's tile
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap'); FREE = []; NRM = []
    for g in groups:
        tx, ty = g['tile'] % 16, g['tile'] // 16; u0, u1 = tx / 16 + 0.002, (tx + 1) / 16 - 0.002; v0, v1 = ty / 4 + 0.002, (ty + 1) / 4 - 0.002
        rows = []
        for i in range(len(g['P'])):
            t_, o_, b_ = g['fr'][i]; p = g['P'][i]; w = g['W'][i]; v = v1 - (v1 - v0) * i / (len(g['P']) - 1)
            rows.append(([clearance_push(p - b_ * w / 2, 0.003), clearance_push(p + o_ * w * 0.10, 0.003), clearance_push(p + b_ * w / 2, 0.003)],
                         [(u0, v), ((u0 + u1) / 2, v), (u1, v)], o_, float(g['FR'][i])))
        for back in (0, 1):
            vs = []
            for (qs, uvs, o_, frv) in rows:
                row = []
                for q, uv in zip(qs, uvs):
                    if back: q = q - o_ * 0.0005
                    row.append((bm.verts.new(q), uv)); FREE.append(frv); NRM.append(tuple(o_))
                vs.append(row)
            for i in range(len(vs) - 1):
                for c in range(2):
                    quad = [vs[i][c], vs[i][c + 1], vs[i + 1][c + 1], vs[i + 1][c]]
                    if back: quad = quad[::-1]
                    try: f = bm.faces.new([q[0] for q in quad])
                    except ValueError: continue
                    for l, (_, uv) in zip(f.loops, quad): l[uvl].uv = uv
    bm.normal_update()
    me = bpy.data.meshes.new('HairGroomCards'); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    me.normals_split_custom_set_from_vertices(NRM)
    ob = bpy.data.objects.new('Tobin_Hair', me); bpy.context.scene.collection.objects.link(ob)
    if 'HULLQ' in globals():
        sil_metric([ob, shell, bpy.data.objects['AvatarHead']])
    gv = ob.vertex_groups.new(name='free')
    for i, w in enumerate(FREE): gv.add([i], w, 'REPLACE')
    return ob

def sil_metric(objs):
    """silhouette IoU per view vs the reference masks (rows above z 1.625): splats vertices + face centres, 2 px dilation."""
    pts = []
    for o in objs:
        V = np.array([v.co[:] for v in o.data.vertices]); pts.append(V)
        pts.append(np.array([p.center[:] for p in o.data.polygons]))
    P = np.vstack(pts); P = P[P[:, 2] > 1.60]
    for n in ('front', 'side'):
        m = _masks[n] > 0; c = _cal[n]; a = P[:, 0] if c['axis'] == 'x' else P[:, 1]
        px = np.round(c['cx'] + c['sign'] * c['s'] * a).astype(int); py = np.round(c['eye_py'] - c.get('sz', c['s']) * (P[:, 2] - _cal['eye_z'])).astype(int)
        A = np.zeros_like(m); ok = (px >= 2) & (px < m.shape[1] - 2) & (py >= 2) & (py < m.shape[0] - 2)
        for dx in (-2, -1, 0, 1, 2):
            for dy in (-2, -1, 0, 1, 2): A[py[ok] + dy, px[ok] + dx] = True
        cut = int(round(c['eye_py'] - c.get('sz', c['s']) * (1.625 - _cal['eye_z']))); roi = np.zeros_like(m); roi[:cut] = True
        inter = (A & m & roi).sum(); uni = ((A | m) & roi).sum()
        print('SIL %-5s IoU %.3f  ours-outside-ref %.3f  ref-missed %.3f' % (n, inter / max(uni, 1), (A & ~m & roi).sum() / max((A & roi).sum(), 1), (m & ~A & roi).sum() / max((m & roi).sum(), 1)))
