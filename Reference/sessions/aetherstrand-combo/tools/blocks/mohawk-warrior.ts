if (arch === 'mohawk-warrior') {
  // Shaved sides (wide stubble fade, sharp boundary along the ridge), dense tall clumped ridge from the front hairline over the crown
  // to the nape, optional braided warrior tail. Everything is rooted on the skull midline strip (no cross-head locks).
  const mkU = (sd: number) => { let st = (sd >>> 0) || 1; return () => { st = (st * 1664525 + 1013904223) >>> 0; return st / 4294967296; }; };
  const seedU = config.seed || 1337;
  const rF = mkU(seedU + 11), rR = mkU(seedU + 22), rT = mkU(seedU + 33), rB = mkU(seedU + 44);
  const ridgeHW = 0.082 + config.layerShag * 0.03; // half width of the ridge strip at the crown (web units)
  const ridgeH = 0.15 + config.crownVolume * 0.12 + spikeAmt * 0.06; // peak lock length (web units, about 50-90 mm Tobin)
  const frontPhi = 1.12; // ridge front hairline
  const backPhi = 2.0; // ridge end at the nape
  const cutPhi = (th: number): number => earAlignedCutPhi(th, { hairlinePhi: 1.25, templeRecede: 0.1, sideburnLength: THREE.MathUtils.lerp(1.1, 1.5, config.sideLength), sideburnTaper: 0.16, earClearance: 0.06, napePhi: 2.12, napeCurve: 1.4 });
  const sm = (x: number) => { const c = THREE.MathUtils.clamp(x, 0, 1); return c * c * (3 - 2 * c); };

  // surface point on the midline strip: s > 0 = front half (theta 0, phi = s), s < 0 = back half (theta pi, phi = -s), xo = lateral offset
  const ellipsoidPt = (p: THREE.Vector3, lift: number): THREE.Vector3 => {
    const q = new THREE.Vector3((p.x - HEAD_CENTER_LOCAL.x) / SCALP_RX, (p.y - HEAD_CENTER_LOCAL.y) / SCALP_RY, (p.z - HEAD_CENTER_LOCAL.z) / SCALP_RZ).normalize();
    return new THREE.Vector3(HEAD_CENTER_LOCAL.x + q.x * (SCALP_RX + lift), HEAD_CENTER_LOCAL.y + q.y * (SCALP_RY + lift), HEAD_CENTER_LOCAL.z + q.z * (SCALP_RZ + lift));
  };
  const stripPt = (s: number, xo: number, lift: number): THREE.Vector3 => {
    const base = s >= 0 ? skullSurfacePoint(0, s, 0) : skullSurfacePoint(Math.PI, -s, 0);
    base.x += xo;
    return ellipsoidPt(base, lift);
  };
  // s runs front hairline (+frontPhi) -> crown (0) -> nape (-backPhi); u = 0..1 along it
  const sOf = (u: number): number => frontPhi - u * (frontPhi + backPhi);
  const hwAt = (u: number): number => ridgeHW * (0.78 + 0.22 * Math.sin(Math.min(1, u * 1.15) * Math.PI)) * (1 - 0.35 * sm((u - 0.7) / 0.3));

  // Layer 0: dark solid shell on the shaved sides (dark stubble above the fade gradient), boundary just outside the ridge
  for (let tier = 0; tier < 3; tier++) {
    const cnt = 34;
    for (let i = 0; i < cnt; i++) {
      const theta = (i / cnt) * Math.PI * 2 + tier * 0.11;
      const sTh = Math.abs(Math.sin(theta));
      if (sTh < 0.3) continue;
      const top = Math.asin(Math.min(1, (ridgeHW * 1.0 + 0.012) / (SCALP_RX * sTh)));
      const end = Math.min(cutPhi(theta) - 0.30, 1.35);
      if (top >= end - 0.05) continue;
      const pts = buildSculptedLock({ thetaStart: theta, thetaEnd: theta, phiStart: top, phiEnd: end, baseLift: 0.0018, midArch: 0.0, tipFlickOut: 0.0, napeHug: 1.4 });
      specs.push({ pointsLocal: pts, width: 0.10 * wMul, thickness: 0.006, stiffnessBias: 0.99, layerAO: 0.7, layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4 });
    }
  }

  // Layer 1: stubble fade (isFadeCap), 5 staggered rows, wide band, sharp boundary along the ridge edge
  {
    const cols = Math.round(120 * density);
    const rows = 5;
    for (let r = 0; r < rows; r++) {
      const sr = r / (rows - 1);
      for (let i = 0; i < cols; i++) {
        const theta = (i / cols) * Math.PI * 2 + r * 0.37 * (Math.PI * 2 / cols) + (rF() - 0.5) * 0.015;
        const sTh = Math.abs(Math.sin(theta));
        if (sTh < 0.3) continue;
        const top = Math.asin(Math.min(1, (ridgeHW * 1.0 + 0.012) / (SCALP_RX * sTh))) + rF() * 0.03;
        const cutL = cutPhi(theta);
        const band = 0.55 * (cutL - top);
        const end = cutL - sr * band + (rF() - 0.5) * 0.03;
        if (top >= end - 0.15) continue;
        const pts = buildSculptedLock({ thetaStart: theta, thetaEnd: theta, phiStart: top, phiEnd: end, baseLift: 0.0016 + 0.0004 * r, midArch: 0.0, tipFlickOut: 0.0, napeHug: 1.55 });
        specs.push({ pointsLocal: pts, width: 0.05 * wMul, thickness: 0.004, stiffnessBias: 0.99, layerAO: 0.8, layerOrder: 1, isFadeCap: true, fadeStart: 0.3 + rF() * 0.2, lockNormal: false, scalpRoot: 0.35 });
      }
    }
    sideburnLocks(specs, { sideburnLength: 1.5, sideburnGap: 0.08, count: 4, width: 0.014, layerOrder: 2, baseLift: 0.004, rootPhi: 1.35 });
  }

  // Layer 2: base strip under the ridge (seals the centre scalp, follows the midline, no wrapping)
  for (let c = 0; c < 3; c++) {
    const cf = c - 1;
    const pts: THREE.Vector3[] = [];
    for (let n = 0; n < NODES_PER_LOCK; n++) {
      const u = n / (NODES_PER_LOCK - 1);
      pts.push(stripPt(sOf(u * 0.93), cf * hwAt(u) * 0.55, 0.006));
    }
    specs.push({ pointsLocal: pts, width: hwAt(0.4) * 0.9 * wMul, thickness: 0.008, stiffnessBias: 0.98, layerAO: 0.75, layerOrder: 1, isBaseShell: true, lockNormal: false, scalpRoot: 0.45 });
  }

  // Layer 3: the ridge. Rows along the midline, narrow tapered locks across the width, grouped into spiky clumps (sawtooth length profile)
  const nClumps = 4;
  const rows = Math.round(26 * density);
  const colsR = 7;
  for (let r = 0; r < rows; r++) {
    const u = 0.01 + (r / (rows - 1)) * 0.9;
    const s = sOf(u);
    const hw = hwAt(u);
    // clump profile: position inside the clump 0..1 (tips at 1), sawtooth with a soft peak
    const cu = u * nClumps;
    const ci = Math.floor(cu);
    const cph = cu - ci;
    const clumpPeak = Math.pow(Math.sin(Math.min(1, cph * 1.25) * Math.PI * 0.5), 1.4) * (1 - 0.25 * sm((cph - 0.85) / 0.15));
    // overall height envelope: low at front hairline, tall over forehead/crown, falling toward the nape
    const env = 0.5 + 0.5 * Math.sin(Math.PI * Math.pow(Math.min(1, u * 1.1), 0.75));
    const tailEnv = 1 - 0.7 * sm((u - 0.62) / 0.36);
    for (let c = 0; c < colsR; c++) {
      const cf = c / (colsR - 1) * 2 - 1; // -1..1
      const xo = cf * hw + (rT() - 0.5) * 0.012;
      const rootLift = 0.006;
      const root = stripPt(s, xo, rootLift);
      const nrm = root.clone().sub(HEAD_CENTER_LOCAL).normalize();
      const side = new THREE.Vector3(Math.sign(cf || 1), 0, 0);
      const backDir = new THREE.Vector3(0, 0.1, -1).normalize();
      const upDir = new THREE.Vector3(0, 1, 0);
      const inFront = s > 0;
      // stand-up direction: on the crown straight up and slightly back, front half tilts forward-up, nape half lies back-down
      let dir = nrm.clone().lerp(upDir, inFront ? 0.55 : THREE.MathUtils.lerp(0.55, 0.1, sm(-s / 1.2))).normalize();
      dir.addScaledVector(backDir, 0.22 + 0.5 * sm(u - 0.35));
      dir.addScaledVector(side, 0.18 * cf * (1 - Math.abs(cf) * 0.2));
      dir.normalize();
      const centreK = 1 - 0.35 * Math.abs(cf);
      const lenMul = (0.52 + 0.48 * clumpPeak) * (0.82 + 0.36 * rT()) * centreK;
      const L = ridgeH * env * Math.max(0.28, tailEnv) * lenMul;
      const pts: THREE.Vector3[] = [];
      const flick = 0.05 + 0.06 * clumpPeak;
      for (let n = 0; n < NODES_PER_LOCK; n++) {
        const t = n / (NODES_PER_LOCK - 1);
        const p = root.clone().addScaledVector(dir, L * t);
        p.addScaledVector(backDir, flick * Math.pow(t, 2.2) * (L / ridgeH + 0.3));
        p.y -= 0.02 * Math.pow(t, 3) * (1 - 0.6 * (config.spikeLift));
        p.x *= 1 - 0.1 * t * Math.abs(cf);
        pts.push(p);
      }
      specs.push({ pointsLocal: pts, width: (0.030 - 0.007 * Math.abs(cf)) * wMul, thickness: 0.013, stiffnessBias: 0.92, layerAO: 0.86 + 0.08 * clumpPeak, layerOrder: 2 + ((r + c) % 3), lockNormal: true, customOutDir: nrm, scalpRoot: 0.45, smooth: 2, roundness: 0.4 });
    }
  }

  // Layer 4: braided warrior tail from the nape (3 interwoven strands, each a few narrow locks), tied at the end
  if (config.backLength > 0.1) {
    const tailLen = 0.22 + config.backLength * 0.5;
    const anchor = skullSurfacePoint(Math.PI, 1.95, 0.012);
    const strands = 3, per = 4;
    for (let sidx = 0; sidx < strands; sidx++) {
      for (let k = 0; k < per; k++) {
        const ph0 = (sidx / strands) * Math.PI * 2;
        const lockOff = (k - (per - 1) / 2) * 0.007;
        const pts: THREE.Vector3[] = [];
        for (let n = 0; n < NODES_PER_LOCK; n++) {
          const t = n / (NODES_PER_LOCK - 1);
          const drop = tailLen * Math.pow(t, 1.1);
          const out = 0.055 * Math.sin(Math.min(1, t * 1.6) * Math.PI * 0.5) + 0.035 * t;
          const weave = Math.sin(t * Math.PI * 5.2 + ph0) * 0.02 * Math.min(1, t * 3) * (1 - 0.7 * sm((t - 0.85) / 0.15));
          const weaveZ = Math.cos(t * Math.PI * 5.2 + ph0) * 0.012 * Math.min(1, t * 3);
          pts.push(new THREE.Vector3(anchor.x + weave + lockOff * (1 - t * 0.7), anchor.y - drop * 0.95 + 0.01, anchor.z - out + weaveZ));
        }
        specs.push({ pointsLocal: pts, width: 0.026 * wMul, thickness: 0.016, stiffnessBias: 0.7, layerAO: 0.92, layerOrder: 5, lockNormal: true, scalpRoot: 0.5, smooth: 2, roundness: 0.5 });
      }
    }
  }

  return specs;
}
