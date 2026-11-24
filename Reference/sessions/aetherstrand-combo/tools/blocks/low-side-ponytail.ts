if (arch === 'low-side-ponytail') {
  // LP-BEGIN low-side-ponytail: everything sweeps diagonally to a low gather point behind one ear; the tail falls forward over that shoulder
  const tailSide = config.bangsSweep >= 0 ? 1 : -1; // +1 = tail over +X shoulder
  const TAU = Math.PI * 2;
  const lpRand = seededRand((config.seed || 1) * 5 + 17);
  const sm = (a: number, b: number, x: number) => { const t = THREE.MathUtils.clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };
  const TIE_TH = 2.35, TIE_PHI = 2.0; // in mirrored space (tail side positive)
  const PART_TH = -0.55;              // side part, opposite the tail
  // mirrored-space surface point: thm positive = tail side
  const sp = (thm: number, phi: number, lift: number) => skullSurfacePoint(tailSide * thm, phi, lift);
  const tieAnchor = sp(TIE_TH, TIE_PHI, 0.02);
  const browPhi = THREE.MathUtils.lerp(1.30, 1.46, config.bangsLength);
  const lpEqY = HEAD_CENTER_LOCAL.y + Math.cos(1.75) * SCALP_RY;
  // ear guard: above/over the ear (|theta| 1.5..2.0) the hair stays above the ear top; behind the ear it drops
  const earCap = (thm: number, phi: number) => {
    const a = Math.abs(thm);
    if (a > 1.45 && a < 2.02) return Math.min(phi, EAR_TOP_PHI - 0.07 + 0.5 * sm(1.95, 2.02, a));
    return phi;
  };
  const radialPush = (pts: THREE.Vector3[], dr: number) => {
    for (let n = 1; n < pts.length; n++) {
      const p = pts[n];
      const dx = p.x - HEAD_CENTER_LOCAL.x, dz = p.z - HEAD_CENTER_LOCAL.z;
      const below = THREE.MathUtils.clamp((lpEqY - p.y) / 0.25, 0, 1);
      const dy = (p.y - HEAD_CENTER_LOCAL.y) * (1 - below);
      const len = Math.max(1e-4, Math.sqrt(dx * dx + dy * dy + dz * dz));
      const k = dr * Math.min(1, n / 4) / len;
      p.x += dx * k; p.y += dy * k; p.z += dz * k;
    }
  };

  // quadratic Bezier on the scalp in (theta, phi) space, 12 nodes, last nodes pinned onto the gather disc
  const sweep = (th0: number, ph0: number, th1: number, ph1: number, kth: number, kph: number, lift: number, arch: number, pin?: THREE.Vector3) => {
    const pts: THREE.Vector3[] = [];
    const tm = th0 + (th1 - th0) * kth, pm = ph0 + (ph1 - ph0) * kph;
    for (let n = 0; n < NODES_PER_LOCK; n++) {
      const t = n / (NODES_PER_LOCK - 1), u = 1 - t;
      const th = u * u * th0 + 2 * u * t * tm + t * t * th1;
      let ph = u * u * ph0 + 2 * u * t * pm + t * t * ph1;
      ph = earCap(th, ph);
      pts.push(sp(th, ph, lift + Math.sin(t * Math.PI) * arch));
    }
    if (pin) { pts[NODES_PER_LOCK - 1].copy(pin); pts[NODES_PER_LOCK - 2].lerp(pin, 0.45); }
    return pts;
  };
  const gatherPin = (k: number) => {
    const a = k * 2.39996;
    const r = 0.016 + 0.020 * Math.sqrt(((k * 0.618) % 1));
    return tieAnchor.clone().add(new THREE.Vector3(Math.cos(a) * r * 0.6 * tailSide, Math.sin(a) * r, -Math.abs(Math.cos(a)) * r * 0.5));
  };

  // Layer 0: sealing shell, hugs the scalp (hidden fill), keeps clear of the ears
  for (let tier = 0; tier < 3; tier++) {
    const count = 20;
    for (let i = 0; i < count; i++) {
      const theta = (i / count) * TAU + tier * 0.21;
      const cosT = Math.cos(theta);
      if (tier >= 1 && cosT > 0.6) continue;
      const phiStart = tier === 0 ? 0.01 : 0.24 + tier * 0.36;
      const maxPhi = cosT < -0.20 ? 1.94 : 1.56;
      if (phiStart >= maxPhi - 0.14) continue;
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: theta, phiStart, phiEnd: Math.min(maxPhi, phiStart + 0.9),
        baseLift: 0.002, midArch: 0.002, tipFlickOut: 0.0, napeHug: 1.32,
      });
      specs.push({
        smooth: 2, pointsLocal: pts, width: 0.08 * wMul, thickness: 0.01,
        stiffnessBias: 0.96, layerAO: 0.78, layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4,
      });
    }
  }

  // Layer 1-2: swept scalp hair. Roots in rows; each sweeps to the gather disc. Left of the part goes round the back, right of it over the tail-side ear.
  const rows: { ph0: number; n: number; lo: number; hi: number; ord: number; lift: number; w: number }[] = [
    { ph0: 0.10, n: 22, lo: 0, hi: TAU, ord: 1, lift: 0.010, w: 0.045 },
    { ph0: 0.55, n: 44, lo: 0, hi: TAU, ord: 1, lift: 0.014, w: 0.040 },
    { ph0: 1.00, n: 64, lo: 0, hi: TAU, ord: 2, lift: 0.018, w: 0.038 },
    { ph0: 1.55, n: 36, lo: 2.2, hi: 4.3, ord: 2, lift: 0.020, w: 0.036 },  // back-of-head low roots (outside ear range)
    { ph0: 1.90, n: 26, lo: 2.6, hi: 3.9, ord: 3, lift: 0.024, w: 0.034 },
  ];
  let kk = 0;
  for (const row of rows) {
    const cnt = Math.round(row.n * density);
    for (let i = 0; i < cnt; i++) {
      let th0 = PART_TH + THREE.MathUtils.lerp(row.lo, row.hi, (i + (lpRand() - 0.5) * 0.6) / cnt);
      if (row.hi - row.lo >= TAU - 0.01 && Math.abs(th0 - PART_TH) < 0.03) continue; // keep the parting line open
      const wrapT = ((th0 + Math.PI) % TAU + TAU) % TAU - Math.PI; // -pi..pi
      const ph0 = row.ph0 + (lpRand() - 0.5) * 0.08;
      // phi cap for roots at the sides so they start above the ear
      const a = Math.abs(wrapT);
      if (a > 1.45 && a < 2.05 && ph0 > 1.45) continue;
      const startTh = wrapT < PART_TH ? wrapT + TAU : wrapT; // start in [PART_TH, PART_TH+TAU)
      const endPhi = TIE_PHI + (lpRand() - 0.5) * 0.04;
      const pts = sweep(startTh, ph0, TIE_TH, endPhi, 0.62, 0.10, row.lift, 0.006, gatherPin(kk++));
      radialPush(pts, 0.002 * row.ord);
      specs.push({
        smooth: 3, roundness: 0.6, pointsLocal: pts, width: (row.w + lpRand() * 0.008) * wMul, thickness: 0.011,
        stiffnessBias: 0.9, layerAO: 0.86, layerOrder: row.ord, lockNormal: false, scalpRoot: 0.38,
      });
    }
  }

  // Layer 3: hairline row, rooted at the hairline (phi ~1.27) and swept over the temple (the side-swept bangs + sides)
  {
    const nH = Math.round(70 * density);
    for (let i = 0; i < nH; i++) {
      const th0 = -1.75 + ((i + (lpRand() - 0.5) * 0.5) / (nH - 1)) * 3.5;
      if (Math.abs(th0 - PART_TH) < 0.03) continue;
      const aa = Math.abs(th0) / 1.75;
      const ph0 = 1.25 + 0.2 * aa * aa + (lpRand() - 0.5) * 0.06;
      const startTh = th0 < PART_TH ? th0 + TAU : th0;
      const pts = sweep(startTh, ph0, TIE_TH, TIE_PHI, 0.66, -0.06, 0.020, 0.004, gatherPin(kk++));
      radialPush(pts, 0.004);
      specs.push({
        smooth: 3, roundness: 0.6, pointsLocal: pts, width: (0.034 + lpRand() * 0.008) * wMul, thickness: 0.011,
        stiffnessBias: 0.86, layerAO: 0.9, layerOrder: 4, lockNormal: false, scalpRoot: 0.35,
      });
    }
  }

  // Layer 4: side-swept bangs rooted at the part, sweeping across the forehead toward the tail side and tucking behind the ear
  {
    const nB = Math.round(26 * density);
    for (let i = 0; i < nB; i++) {
      const f = i / (nB - 1);
      const th0 = PART_TH + 0.04 + f * 0.34 + (lpRand() - 0.5) * 0.04;
      const thMid = 0.55 + f * 0.65;
      const thEnd = 1.35 + f * 0.35;
      const pts: THREE.Vector3[] = [];
      const ph0 = 0.34 + f * 0.1;
      const phEnd = 1.50 + f * 0.04;
      const phMid = browPhi - 0.02 + (lpRand() - 0.5) * 0.04 - 0.05 * f;
      for (let n = 0; n < NODES_PER_LOCK; n++) {
        const t = n / (NODES_PER_LOCK - 1), u = 1 - t;
        const th = u * u * th0 + 2 * u * t * thMid + t * t * thEnd;
        const ph = u * u * ph0 + 2 * u * t * (phMid * 1.45 - 0.45 * ph0 * 0.5) + t * t * phEnd;
        pts.push(sp(th, Math.min(earCap(th, ph), ph), 0.026 + Math.sin(t * Math.PI) * 0.010 + f * 0.003));
      }
      specs.push({
        smooth: 3, roundness: 0.65, pointsLocal: pts, width: (0.032 + lpRand() * 0.008) * wMul, thickness: 0.011,
        stiffnessBias: 0.8, layerAO: 0.94, layerOrder: 5, lockNormal: false, scalpRoot: 0.35,
      });
    }
  }

  // Layer 5: the tail. Axis over the shoulder (mirrored space x>0), bundle of narrow tapered locks around it.
  const axisCtrl = [
    tieAnchor.clone().multiply(new THREE.Vector3(tailSide, 1, 1)),
    new THREE.Vector3(0.25, 0.17, -0.23),
    new THREE.Vector3(0.31, 0.02, -0.15),
    new THREE.Vector3(0.34, -0.10, -0.03),
    new THREE.Vector3(0.34, -0.20, 0.10),
    new THREE.Vector3(0.32, -0.32, 0.20),
    new THREE.Vector3(0.30, -0.45, 0.25),
    new THREE.Vector3(0.29, -0.56, 0.27),
  ].map((v) => new THREE.Vector3(v.x * tailSide, v.y, v.z));
  const curve = new THREE.CatmullRomCurve3(axisCtrl, false, 'catmullrom', 0.5);
  const axisAt = (s: number) => curve.getPointAt(THREE.MathUtils.clamp(s, 0, 1));
  const frame = (s: number) => {
    const T = curve.getTangentAt(THREE.MathUtils.clamp(s, 0, 1)).normalize();
    const N = new THREE.Vector3().crossVectors(T, new THREE.Vector3(0, 0, 1)).normalize();
    const B = new THREE.Vector3().crossVectors(T, N).normalize();
    return { T, N, B };
  };
  const radius = (s: number) => 0.034 + 0.030 * sm(0.0, 0.35, s) - 0.008 * sm(0.6, 1.0, s);
  const lenMul = 0.80 + 0.20 * config.backLength;
  const nT = Math.round(40 * density);
  for (let i = 0; i < nT; i++) {
    const ang0 = i * 2.39996;
    const rr = Math.sqrt((i + 0.5) / nT);
    const sEnd = lenMul * (1 - 0.26 * Math.pow(rr, 1.5) - 0.06 * lpRand());
    const ph = 0.9 + rr * 0.8 + lpRand() * 0.3;
    const pts: THREE.Vector3[] = [];
    for (let n = 0; n < NODES_PER_LOCK; n++) {
      const t = n / (NODES_PER_LOCK - 1);
      const s = t * sEnd;
      const { N, B } = frame(s);
      const taper = 1 - 0.55 * sm(0.55, 1.0, t);
      const R = radius(s) * rr * taper * (n === 0 ? 0.6 : 1);
      const a = ang0 + s * 1.1;
      const wave = Math.sin(TAU * 1.35 * s + ph) * 0.03 * sm(0.08, 0.5, s) * (0.8 + 0.4 * rr);
      const c = axisAt(s);
      const du = Math.cos(a) * R * 1.3 + wave, dv = Math.sin(a) * R * 0.8;
      pts.push(c.clone().addScaledVector(N, du).addScaledVector(B, dv));
    }
    pts[0].copy(tieAnchor).add(new THREE.Vector3((lpRand() - 0.5) * 0.03, (lpRand() - 0.5) * 0.03, (lpRand() - 0.5) * 0.02));
    specs.push({
      smooth: 3, roundness: 0.6, pointsLocal: pts, width: (0.036 + lpRand() * 0.012) * wMul, thickness: 0.014,
      stiffnessBias: 0.6, layerAO: 0.86 + rr * 0.1, layerOrder: 3 + (i % 3), lockNormal: true, scalpRoot: 0.4,
    });
  }
  // tie: two thin rings wrapped round the bundle just below the gather point
  for (let r = 0; r < 2; r++) {
    const s = 0.075 + r * 0.03;
    const c = axisAt(s);
    const { N, B } = frame(s);
    const R = radius(s) * 1.25;
    const pts: THREE.Vector3[] = [];
    for (let n = 0; n < NODES_PER_LOCK; n++) {
      const a = (n / (NODES_PER_LOCK - 1)) * TAU * 0.95 + r;
      pts.push(c.clone().addScaledVector(N, Math.cos(a) * R * 1.3).addScaledVector(B, Math.sin(a) * R * 0.85));
    }
    specs.push({
      smooth: 3, roundness: 1, pointsLocal: pts, width: 0.026 * wMul, thickness: 0.026,
      stiffnessBias: 0.99, layerAO: 0.45, layerOrder: 7, lockNormal: false, scalpRoot: 0.0,
    });
  }
  return specs;
  // LP-END
}
