if (arch === 'twin-buns') {
  const V3 = THREE.Vector3;
  const tbStart = specs.length;
  const KX = 0.74, KZ = 0.82;
  const snugP = (p: THREE.Vector3, kx = KX, kz = KZ) => {
    const lowT = THREE.MathUtils.clamp((HEAD_CENTER_LOCAL.y - p.y) / 0.1, 0, 1);
    const nT = THREE.MathUtils.clamp((HEAD_CENTER_LOCAL.y - 0.1 - p.y) / 0.15, 0, 1);
    p.x = HEAD_CENTER_LOCAL.x + (p.x - HEAD_CENTER_LOCAL.x) * (kx - 0.12 * lowT - 0.12 * nT);
    p.z = HEAD_CENTER_LOCAL.z + (p.z - HEAD_CENTER_LOCAL.z) * (kz - 0.08 * lowT - 0.08 * nT);
    return p;
  };
  // hairline: high forehead, hair covers the temples, short sideburn in front of the ear, tapered nape
  const frontKnots: Array<[number, number]> = [[0, 1.24], [0.3, 1.2], [0.6, 1.2], [0.9, 1.3], [1.15, 1.42], [1.3, 1.5]];
  const frontHair = (a: number): number => {
    for (let k = 1; k < frontKnots.length; k++) {
      if (a <= frontKnots[k][0]) {
        const u = (a - frontKnots[k - 1][0]) / (frontKnots[k][0] - frontKnots[k - 1][0]);
        return THREE.MathUtils.lerp(frontKnots[k - 1][1], frontKnots[k][1], u * u * (3 - 2 * u));
      }
    }
    return frontKnots[frontKnots.length - 1][1];
  };
  const cutPhi = (th: number): number => {
    const v = earAlignedCutPhi(th, { hairlinePhi: frontHair, templeRecede: 0, sideburnLength: 0.3, sideburnGap: 0.12, sideburnTaper: 0.12, earClearance: 0.12, behindEarPhi: 1.95, napePhi: 2.4, napeCurve: 0.8 });
    const a = Math.abs(Math.atan2(Math.sin(th), Math.cos(th)));
    const w = THREE.MathUtils.smoothstep(a, 1.9, 1.98);
    const tgt = THREE.MathUtils.lerp(1.92, 2.4, Math.pow(Math.max(0, a - 2.0) / (Math.PI - 2.0), 0.9));
    return a > 1.9 ? Math.max(v, THREE.MathUtils.lerp(v, tgt, w)) : v;
  };

  // ---- bun geometry (snugged space) ----
  const bunR = 0.07 + config.crownVolume * 0.03;
  const bunTheta = 1.5, bunPhi = 0.72;
  const buns = [-1, 1].map((side) => {
    const surf = snugP(skullSurfacePoint(side * bunTheta, bunPhi, 0.0));
    const A = new V3(side * 0.7, 0.62, -0.38).normalize();
    const C = surf.clone().addScaledVector(A, bunR * 0.55);
    const U = new V3().crossVectors(A, new V3(0, 0, 1)).normalize();
    const W = new V3().crossVectors(A, U).normalize();
    return { side, A, C, U, W };
  });

  // Zone 1: snug scalp cover under the pulled strands
  for (let tier = 0; tier < 2; tier++) {
    const count = 26;
    for (let i = 0; i < count; i++) {
      const theta = (i / count) * Math.PI * 2 + tier * 0.12;
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: theta, phiStart: 0.02 + tier * 0.4, phiEnd: Math.min(0.85 + tier * 0.3, cutPhi(theta) - 0.15),
        baseLift: 0.001, midArch: 0.001, tipFlickOut: 0.0, napeHug: 1.35,
      });
      specs.push({
        pointsLocal: pts, width: 0.07 * wMul, thickness: 0.006, stiffnessBias: 0.98, layerAO: 0.95,
        layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4,
      });
    }
  }
  for (let i = 0; i < 64; i++) {
    const theta = (i / 64) * Math.PI * 2 + 0.05;
    const endP = cutPhi(theta) - 0.1;
    if (endP < 1.2) continue;
    const pts = buildSculptedLock({
      thetaStart: theta, thetaEnd: theta, phiStart: 0.95, phiEnd: endP,
      baseLift: 0.001, midArch: 0.001, tipFlickOut: 0.0, napeHug: 1.35,
    });
    specs.push({
      pointsLocal: pts, width: 0.05 * wMul, thickness: 0.006, stiffnessBias: 0.98, layerAO: 0.95,
      layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4,
    });
  }

  // Zone 2: strands rooted on the cut line, combed up along the dome into the nearer bun neck
  const bunDirs = buns.map((b) => {
    const N = b.C.clone().addScaledVector(b.A, -bunR * 0.6);
    const d = new V3((N.x - HEAD_CENTER_LOCAL.x) / KX / SCALP_RX, (N.y - HEAD_CENTER_LOCAL.y) / SCALP_RY, (N.z - HEAD_CENTER_LOCAL.z) / KZ / SCALP_RZ);
    return d.normalize();
  });
  const pullStrand = (theta: number, phi0: number, width: number, order: number, spread: number, side: number) => {
    const u0 = new V3(Math.sin(theta) * Math.sin(phi0), Math.cos(phi0), Math.cos(theta) * Math.sin(phi0));
    const end = bunDirs[side > 0 ? 1 : 0].clone();
    end.x += (rand() - 0.5) * spread; end.y += (rand() - 0.5) * spread * 0.6; end.z += (rand() - 0.5) * spread; end.normalize();
    const omega = Math.acos(THREE.MathUtils.clamp(u0.dot(end), -1, 1));
    const so = Math.max(Math.sin(omega), 1e-4);
    const lift0 = 0.0002 + rand() * 0.0006 + order * 0.0003;
    const pts: THREE.Vector3[] = [];
    for (let k = 0; k < NODES_PER_LOCK; k++) {
      const t = Math.pow(k / (NODES_PER_LOCK - 1), 1.3);
      const w0 = Math.sin((1 - t) * omega) / so;
      const w1 = Math.sin(t * omega) / so;
      const u = u0.clone().multiplyScalar(w0).addScaledVector(end, w1);
      const ph = Math.acos(THREE.MathUtils.clamp(u.y, -1, 1));
      const th = Math.atan2(u.x, u.z);
      pts.push(skullSurfacePoint(th, ph, lift0 + Math.sin(t * Math.PI) * 0.001));
    }
    specs.push({
      pointsLocal: pts, width, thickness: 0.005, stiffnessBias: 0.97,
      layerAO: 0.97, layerOrder: order, lockNormal: Math.abs(Math.atan2(Math.sin(theta), Math.cos(theta))) > 1.9 && phi0 > 1.85, scalpRoot: 0.35, smooth: 3, roundness: 0.5,
    });
  };
  const sideOf = (th: number) => (Math.sin(th) >= 0 ? 1 : -1);
  const pullCount = Math.round(440 * density);
  for (let i = 0; i < pullCount; i++) {
    const theta = ((i + 0.5) / pullCount) * Math.PI * 2 + (rand() - 0.5) * 0.01;
    pullStrand(theta, cutPhi(theta) - 0.02 + (rand() - 0.5) * 0.006, (0.026 + rand() * 0.008) * wMul, 0, 0.14, sideOf(theta));
  }
  for (let i = 0; i < pullCount; i++) {
    const theta = ((i + 1) / pullCount) * Math.PI * 2;
    pullStrand(theta, cutPhi(theta) - 0.07 - rand() * 0.03, (0.026 + rand() * 0.008) * wMul, 1, 0.14, sideOf(theta));
  }

  // Zone 2b: tapered nape, soft stubble fade caps
  {
    const cols = Math.round(190 * density);
    for (let i = 0; i < cols; i++) {
      const theta = (i / cols) * Math.PI * 2 + (rand() - 0.5) * 0.01;
      const cosT = Math.cos(theta);
      const aa = Math.abs(Math.atan2(Math.sin(theta), cosT));
      if (aa < 1.75) continue;
      const nz = Math.max(0, aa - 2.2) / 0.94;
      const lim = cutPhi(theta) + 0.05 + 0.1 * nz;
      const top = lim - 0.22 - 0.03 * nz;
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: theta, phiStart: top, phiEnd: lim,
        baseLift: 0.0003, midArch: 0.0, tipFlickOut: 0.0, napeHug: 1.55,
      });
      specs.push({
        pointsLocal: pts, width: 0.03 * wMul, thickness: 0.003, stiffnessBias: 0.99,
        layerAO: 0.92, layerOrder: 1, isFadeCap: true, fadeStart: 0.3, lockNormal: true, scalpRoot: 0.35,
      });
    }
  }
  // snug the dome cover
  for (let si = tbStart; si < specs.length; si++) {
    if (specs[si].isFadeCap) {
      for (const p of specs[si].pointsLocal) snugP(p);
      continue;
    }
    for (const p of specs[si].pointsLocal) snugP(p);
  }

  // cover caps around the bun bases (no scalp pinholes where strands converge)
  for (let i = 0; i < 28; i++) {
    const theta = (i / 28) * Math.PI * 2 + 0.03;
    const pts = buildSculptedLock({ thetaStart: theta, thetaEnd: theta, phiStart: 0.3, phiEnd: 1.0, baseLift: 0.004, midArch: 0.001, tipFlickOut: 0.0, napeHug: 1.35 });
    for (const p of pts) snugP(p);
    specs.push({ pointsLocal: pts, width: 0.06 * wMul, thickness: 0.006, stiffnessBias: 0.98, layerAO: 0.95, layerOrder: 1, lockNormal: false, scalpRoot: 0.4 });
  }

  // Zone 3: the two buns, helical coils around the bun axis, pinched neck, tie rings
  for (const b of buns) {
    const coilCount = Math.round(54 * density);
    for (let i = 0; i < coilCount; i++) {
      const a0 = (i / coilCount) * Math.PI * 2;
      const pts: THREE.Vector3[] = [];
      const turns = 0.9 + rand() * 0.25;
      for (let k = 0; k < NODES_PER_LOCK; k++) {
        const t = k / (NODES_PER_LOCK - 1);
        const hh = THREE.MathUtils.lerp(-0.72, 0.9, Math.sin(t * Math.PI * 0.5));
        const prof = Math.sqrt(Math.max(0.2, 1 - hh * hh)) * (1 - 0.48 * Math.exp(-Math.pow((hh + 0.62) / 0.12, 2))) * (hh < -0.4 ? 0.9 : 1.0);
        const rr = bunR * (prof * 1.02 + 0.004);
        const a = a0 + turns * t * Math.PI * 1.2 * b.side;
        pts.push(b.C.clone().addScaledVector(b.A, bunR * 0.92 * hh).addScaledVector(b.U, Math.cos(a) * rr).addScaledVector(b.W, Math.sin(a) * rr * 1.1));
      }
      specs.push({ pointsLocal: pts, width: 0.04 * wMul, thickness: 0.01, stiffnessBias: 0.95, layerAO: 1.0, layerOrder: 2, lockNormal: false, scalpRoot: 0.5, smooth: 2 });
    }
    const tieR = (hh: number) => bunR * (Math.sqrt(Math.max(0.2, 1 - hh * hh)) * (1 - 0.38 * Math.exp(-Math.pow((hh + 0.62) / 0.12, 2))) * 1.02 + 0.004) * 1.2 + 0.012;
    for (let ring = 0; ring < 3; ring++) {
      const hh = -0.5 - ring * 0.08;
      const rad = tieR(hh);
      for (let j = 0; j < 4; j++) {
        const a0 = (j / 4) * Math.PI * 2 + ring * 0.7;
        const pts: THREE.Vector3[] = [];
        for (let k = 0; k < NODES_PER_LOCK; k++) {
          const t = k / (NODES_PER_LOCK - 1);
          const a = a0 + t * Math.PI * 0.9;
          pts.push(b.C.clone().addScaledVector(b.A, bunR * hh + (t - 0.5) * 0.004).addScaledVector(b.U, Math.cos(a) * rad).addScaledVector(b.W, Math.sin(a) * rad * 1.1));
        }
        specs.push({ pointsLocal: pts, width: 0.035 * wMul, thickness: 0.025, stiffnessBias: 0.99, layerAO: 0.12, layerOrder: 3, lockNormal: false, scalpRoot: 0.5 });
      }
    }
  }

  // Zone 4: loose short tendrils in front of the ears and at the nape (thin wavy locks)
  for (const side of [-1, 1]) {
    for (let k = 0; k < 3; k++) {
      const pts = buildSculptedLock({
        thetaStart: side * (1.28 + k * 0.06), thetaEnd: side * (1.4 + k * 0.05),
        phiStart: 1.2, phiEnd: 2.25 + k * 0.1 + config.bangsLength * 0.2,
        baseLift: 0.012, midArch: 0.012, tipFlickOut: 0.01, sCurveAmp: side * 0.03, napeHug: 0.95,
      });
      for (const p of pts) snugP(p, 0.84, 0.9);
      specs.push({
        pointsLocal: pts, width: (0.02 - k * 0.003) * wMul, thickness: 0.008, stiffnessBias: 0.7, layerAO: 0.94,
        layerOrder: 4, lockNormal: true, scalpRoot: 0.25, smooth: 3, roundness: 0.5,
      });
    }
    for (let k = 0; k < 2; k++) {
      const pts = buildSculptedLock({
        thetaStart: side * (2.15 + k * 0.08), thetaEnd: side * (2.2 + k * 0.1),
        phiStart: 1.95, phiEnd: 2.6 + k * 0.12,
        baseLift: 0.012, midArch: 0.008, tipFlickOut: 0.01, sCurveAmp: side * 0.03, napeHug: 0.9,
      });
      for (const p of pts) snugP(p);
      specs.push({
        pointsLocal: pts, width: 0.018 * wMul, thickness: 0.008, stiffnessBias: 0.7, layerAO: 0.94,
        layerOrder: 4, lockNormal: true, scalpRoot: 0.25, smooth: 3, roundness: 0.5,
      });
    }
  }

  return specs;
}
