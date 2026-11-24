  if (arch === 'wavy-bard') {
    // WB-BEGIN wavy-bard: shoulder-length full waves; many narrow round locks in 3 depth layers, S-wave bundles share phase
    const partTheta = THREE.MathUtils.clamp(config.partPosition, -1, 1) * 0.42;
    const backEnd = THREE.MathUtils.lerp(2.75, 3.65, config.backLength);
    const sideEnd = THREE.MathUtils.lerp(2.7, 3.45, config.sideLength);
    const TAU = Math.PI * 2;
    const wrap = (a: number) => { let x = a % TAU; if (x > Math.PI) x -= TAU; if (x < -Math.PI) x += TAU; return x; };
    const eqY = HEAD_CENTER_LOCAL.y + Math.cos(1.75) * SCALP_RY;
    // coherent wave: phase is a smooth function of theta and rank, so neighbours form S bundles
    const wavify = (pts: THREE.Vector3[], th: number, amp: number, phase: number, curl: number) => {
      const tx = Math.cos(th), tz = -Math.sin(th);
      const last = pts.length - 1;
      for (let n = 2; n <= last; n++) {
        const t = n / last;
        const w = (Math.sin(t * TAU * 2.2 + phase) * amp + Math.sin(t * TAU * 0.8 + phase * 0.7 + 1.3) * amp * 0.4 * Math.pow(t, 2.0)) * Math.pow(t, 0.8);
        const c = n >= last - 3 ? Math.pow((n - (last - 3)) / 3, 2) : 0;
        pts[n].x += tx * (w + curl * c * 0.05);
        pts[n].z += tz * (w + curl * c * 0.05);
      }
    };
    // push every node outward from the head (horizontal below the equator): layered parallax
    const radialPush = (pts: THREE.Vector3[], dr: number) => {
      for (let n = 1; n < pts.length; n++) {
        const p = pts[n];
        const dx = p.x - HEAD_CENTER_LOCAL.x, dz = p.z - HEAD_CENTER_LOCAL.z;
        const below = THREE.MathUtils.clamp((eqY - p.y) / 0.25, 0, 1);
        const dy = (p.y - HEAD_CENTER_LOCAL.y) * (1 - below);
        const len = Math.max(1e-4, Math.sqrt(dx * dx + dy * dy + dz * dz));
        const k = dr * Math.min(1, n / 3) / len;
        p.x += dx * k; p.y += dy * k; p.z += dz * k;
      }
    };
    // lift the hair clear of neck and shoulders so the waves sit on the body, widest at the shoulders
    const drape = (pts: THREE.Vector3[], amt: number) => {
      for (let n = 1; n < pts.length; n++) {
        const p = pts[n];
        const dx = p.x - HEAD_CENTER_LOCAL.x, dz = p.z - HEAD_CENTER_LOCAL.z;
        const len = Math.max(1e-4, Math.sqrt(dx * dx + dz * dz));
        const d = THREE.MathUtils.clamp((eqY - p.y - 0.2) / 0.45, 0, 1);
        const k = amt * d * d * (3 - 2 * d) / len;
        p.x += dx * k * 0.6; p.z += dz * k;
      }
    };
    const rMid = seededRand((config.seed || 1) + 23);
    const rFr = seededRand((config.seed || 1) + 51);

    // Zone 1: inner shell, seals the scalp (hidden fill)
    for (let tier = 0; tier < 3; tier++) {
      const count = 22;
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * TAU + tier * 0.21;
        const cosT = Math.cos(theta);
        if (tier >= 1 && cosT > 0.3) continue;
        const isBack = cosT < -0.15;
        const phiStart = tier === 0 ? 0.02 : 0.30 + tier * 0.42;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart,
          phiEnd: Math.min(isBack ? backEnd - 0.5 : sideEnd - 0.6, phiStart + 1.45, cosT > 0.3 ? 1.2 : 9),
          baseLift: 0.008, midArch: 0.010, tipFlickOut: 0.006, napeHug: 0.8,
        });
        specs.push({
          smooth: 3,
          pointsLocal: pts, width: 0.09 * wMul, thickness: 0.012,
          stiffnessBias: 0.8, layerAO: 0.7, layerOrder: 0, isBaseShell: true,
          lockNormal: false, scalpRoot: tier === 0 ? 0.45 : 0.22,
        });
      }
    }

    // Zone 1a: swept-back crown. Locks root on the hairline (phi ~1.15-1.3, no bare forehead) and flow over the apex to the back of the head.
    for (let row = 0; row < 3; row++) {
      const nS = 44;
      for (let i = 0; i < nS; i++) {
        const a = -1.05 + ((i + row * 0.35) / (nS - 1)) * 2.1;
        const dPart = a - partTheta;
        if (Math.abs(dPart) < 0.035) continue;
        const sd = dPart >= 0 ? 1 : -1;
        const aa = Math.abs(a) / 1.05;
        const phiF = 1.27 + 0.14 * aa * aa + row * 0.035;
        const phiB = 1.45 + rFr() * 0.3;
        const pts: THREE.Vector3[] = [];
        for (let n = 0; n < NODES_PER_LOCK; n++) {
          const u = n / (NODES_PER_LOCK - 1);
          const sPar = THREE.MathUtils.lerp(-phiF, phiB, u);
          const front = sPar < 0;
          const phi = Math.abs(sPar);
          const sway = Math.sin(u * Math.PI * 2.2 + a * 3 + row) * 0.035 * Math.sin(u * Math.PI);
          const th = front ? a * (1 + (1 - Math.abs(sPar) / phiF) * 0.25) + sd * sway : Math.PI - a * 0.85 - sd * 0.1 + sd * sway;
          pts.push(skullSurfacePoint(th, phi, 0.005 + row * 0.003 + Math.sin(u * Math.PI) * 0.004));
        }
        radialPush(pts, 0.0 + row * 0.003);
        specs.push({
          smooth: 3, roundness: 0.65,
          pointsLocal: pts, width: (0.034 + row * 0.004) * wMul, thickness: 0.012,
          stiffnessBias: 0.7, layerAO: 0.86, layerOrder: 3 + row, lockNormal: false, scalpRoot: 0.45,
        });
      }
    }

    // Zone 1b: face-framing locks from the part. Sweep-back pieces end behind the ear, framing pieces fall in front of it.
    for (let row = 0; row < 3; row++) {
      const nR = 34;
      for (let i = 0; i < nR; i++) {
        const th = -0.85 + ((i + row * 0.33) / nR) * 1.7 + (rFr() - 0.5) * 0.02;
        const dPart = th - partTheta;
        const side = dPart >= 0 ? 1 : -1;
        const nearPart = Math.abs(dPart) < 0.06;
        const framing = Math.abs(th) > 0.5 && (i % 3 === 0);
        if (Math.abs(th) < 0.5) continue;
        const thEnd = framing ? side * (1.5 + rFr() * 0.22) : side * (2.0 + row * 0.1 + rFr() * 0.25);
        const phiEnd = framing ? 3.05 + rFr() * 0.35 : 2.5 + row * 0.2 + rFr() * 0.3;
        const pts = buildSculptedLock({
          thetaStart: th, thetaEnd: thEnd,
          phiStart: nearPart ? 0.28 : 0.18 + row * 0.07 + rFr() * 0.08,
          phiEnd,
          baseLift: 0.012 + row * 0.004, midArch: 0.012, tipFlickOut: 0.008, napeHug: 0.7,
        });
        wavify(pts, thEnd, 0.12, TAU * th * 0.55 + row, side);
        radialPush(pts, 0.006 + row * 0.008);
        drape(pts, 0.1);
        specs.push({
          smooth: 3, roundness: 0.65,
          pointsLocal: pts, width: (0.032 + row * 0.004) * wMul, thickness: 0.012,
          stiffnessBias: 0.7, layerAO: 0.86, layerOrder: 4 + row, lockNormal: false, scalpRoot: 0.4,
        });
      }
    }

    // Zone 2: three depth layers of narrow round wavy locks; hem follows the wave phase so neighbours share S bundles
    const L = [
      { n: 70, w: 0.09, dr: 0.004, amp: 0.15, ord: 1 },   // hidden under-fill
      { n: 110, w: 0.07, dr: 0.025, amp: 0.17, ord: 2 },
      { n: 130, w: 0.062, dr: 0.04, amp: 0.19, ord: 3 },
    ];
    for (let layer = 0; layer < 3; layer++) {
      const cfg = L[layer];
      const count = Math.round(cfg.n * density / 1.15);
      for (let i = 0; i < count; i++) {
        const th = wrap((i / count) * TAU + layer * 0.04 + (rMid() - 0.5) * 0.02);
        const cosT = Math.cos(th);
        if (cosT > 0.3) continue;                         // front handled by zone 1b
        const isBack = cosT < -0.2;
        const side = th >= 0 ? 1 : -1;
        const phase = TAU * (th * 0.55 + layer * 0.12);
        const bundle = 0.5 + 0.5 * Math.sin(phase);       // 0..1, shared by neighbours: coherent hem
        let thEnd = th;
        let phiEnd: number;
        if (!isBack) {
          thEnd = Math.abs(th) < 2.05 ? side * (2.1 + rMid() * 0.2) : th;
          phiEnd = sideEnd - bundle * 0.35 - rMid() * 0.05;
        } else {
          thEnd = th + (rMid() - 0.5) * 0.04;
          phiEnd = backEnd - bundle * 0.4 - rMid() * 0.05;
        }
        const phiStart = 0.1 + layer * 0.08 + rMid() * 0.1;
        const pts = buildSculptedLock({
          thetaStart: th, thetaEnd: thEnd, phiStart, phiEnd,
          baseLift: 0.014 + layer * 0.004 + config.crownVolume * 0.008,
          midArch: 0.012 + layer * 0.004, tipFlickOut: 0.01 + layer * 0.004,
          napeHug: 0.45,
        });
        wavify(pts, thEnd, cfg.amp, phase, Math.sin(th * 5 + layer) >= 0 ? 1 : -1);
        radialPush(pts, cfg.dr);
        drape(pts, 0.17 + layer * 0.015);
        specs.push({
          smooth: 3, roundness: 0.6,
          pointsLocal: pts,
          width: cfg.w * wMul * (0.85 + rMid() * 0.3),
          thickness: 0.013,
          stiffnessBias: 0.7 - layer * 0.06, layerAO: 0.78 + layer * 0.06, layerOrder: cfg.ord,
          lockNormal: false, scalpRoot: 0.3 + 0.03 * (2 - layer),
        });
      }
    }
    return specs;
    // WB-END
  }
