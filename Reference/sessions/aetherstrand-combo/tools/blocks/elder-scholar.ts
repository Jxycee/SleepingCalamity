  if (arch === 'elder-scholar') {
    const tieTheta = Math.PI;
    const tiePhi = 1.72; // low scholar tie at the nape
    const tieAnchor = skullSurfacePoint(tieTheta, tiePhi, 0.018).add(new THREE.Vector3(0, -0.012, -0.045));
    const clamp01 = (v: number) => THREE.MathUtils.clamp(v, 0, 1);
    const randS = seededRand((config.seed || 1337) + 11);
    const randT = seededRand((config.seed || 1337) + 23);
    const randB = seededRand((config.seed || 1337) + 41);

    // receding M-shaped hairline: phi at which hair starts, as a function of |theta| (high forehead, temples recede)
    const hsKnots: Array<[number, number]> = [[0, 1.06], [0.4, 1.03], [0.8, 0.82], [1.1, 0.98], [1.3, 1.12], [1.6, 1.3], [Math.PI, 1.9]];
    const hs = (a: number) => {
      const x = Math.abs(Math.atan2(Math.sin(a), Math.cos(a)));
      for (let k = 1; k < hsKnots.length; k++) {
        if (x <= hsKnots[k][0]) {
          const u = clamp01((x - hsKnots[k - 1][0]) / (hsKnots[k][0] - hsKnots[k - 1][0]));
          return THREE.MathUtils.lerp(hsKnots[k - 1][1], hsKnots[k][1], u * u * (3 - 2 * u));
        }
      }
      return hsKnots[hsKnots.length - 1][1];
    };
    const cutOpts = { hairlinePhi: hs, templeRecede: 0, sideburnLength: 1.0, sideburnTaper: 0.34, earClearance: 0.06, napePhi: 2.0 };

    // unit direction on the skull (normalised ellipsoid space) and great-circle lock builder (crosses the apex safely)
    const dirOf = (th: number, ph: number) => new THREE.Vector3(Math.sin(th) * Math.sin(ph), Math.cos(ph), Math.cos(th) * Math.sin(ph));
    const slerpDir = (a: THREE.Vector3, b: THREE.Vector3, t: number) => {
      const d = THREE.MathUtils.clamp(a.dot(b), -1, 1);
      const om = Math.acos(d);
      if (om < 1e-4) return a.clone();
      const s = Math.sin(om);
      return a.clone().multiplyScalar(Math.sin((1 - t) * om) / s).add(b.clone().multiplyScalar(Math.sin(t * om) / s));
    };
    const arcLock = (dS: THREE.Vector3, dE: THREE.Vector3, f0: number, f1: number, lift: number, arch: number, jitter = 0) => {
      const pts: THREE.Vector3[] = [];
      for (let i = 0; i < NODES_PER_LOCK; i++) {
        const t = i / (NODES_PER_LOCK - 1);
        const f = THREE.MathUtils.lerp(f0, f1, t);
        const v = slerpDir(dS, dE, f);
        if (jitter) {
          v.x += Math.sin(t * 4.1 + jitter * 7) * jitter * 0.03 * Math.sin(t * Math.PI);
          v.normalize();
        }
        const th = Math.atan2(v.x, v.z);
        const ph = Math.acos(THREE.MathUtils.clamp(v.y, -1, 1));
        const rootSink = -0.004 * Math.pow(1.0 - Math.min(1.0, t * 4.5), 2.0);
        pts.push(skullSurfacePoint(th, ph, lift + rootSink + Math.sin(t * Math.PI) * arch));
      }
      return pts;
    };
    const tieDir = dirOf(tieTheta, tiePhi);
    const gatherTip = (pts: THREE.Vector3[]) => {
      pts[NODES_PER_LOCK - 1].lerp(tieAnchor, 0.92);
      pts[NODES_PER_LOCK - 2].lerp(tieAnchor, 0.62);
      pts[NODES_PER_LOCK - 3].lerp(tieAnchor, 0.28);
      pts[NODES_PER_LOCK - 4].lerp(tieAnchor, 0.08);
    };

    // Zone 1a: front-top base shell, rooted at the receding hairline and flowing back over the crown
    const nF = 30;
    for (let i = 0; i < nF; i++) {
      const u = i / (nF - 1);
      const th0 = THREE.MathUtils.lerp(-1.3, 1.3, u);
      const dS = dirOf(th0, hs(th0) + 0.02);
      const dE = dirOf(th0 + Math.PI, 0.30 + 0.12 * randS());
      const pts = arcLock(dS, dE, 0, 1, 0.004 + 0.0006 * (i % 4), 0.003);
      specs.push({
        pointsLocal: pts, width: 0.068 * wMul * (0.55 + 0.45 * Math.min(1, Math.min(u, 1 - u) / 0.15)), thickness: 0.008,
        stiffnessBias: 0.96, layerAO: 0.74, layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4,
      });
    }

    // Zone 1b: side/rear base shell from the crown down to the ear-aligned cut and nape
    const shellCount = 60;
    const thLo = 0.95;
    const thHi = Math.PI * 2 - 0.95;
    for (let i = 0; i < shellCount; i++) {
      const u = i / (shellCount - 1);
      const theta = THREE.MathUtils.lerp(thLo, thHi, u);
      const isRear = Math.cos(theta) < -0.40;
      const earCut = earAlignedCutPhi(theta, cutOpts);
      const pts = buildSculptedLock({
        thetaStart: theta,
        thetaEnd: Math.abs(Math.atan2(Math.sin(theta), Math.cos(theta))) < 1.5 ? theta : (theta > Math.PI ? Math.min(thHi, theta + 0.45) : Math.max(thLo, theta - 0.45)),
        phiStart: 0.26 + 0.1 * randS(),
        phiEnd: isRear ? 2.12 : Math.min(earCut, Math.abs(Math.atan2(Math.sin(theta), Math.cos(theta))) < 1.35 ? hs(theta) - 0.14 : 9),
        baseLift: 0.0045 + 0.0006 * (i % 4),
        midArch: 0.003,
        tipFlickOut: 0.0,
        napeHug: 1.35,
      });
      specs.push({
        pointsLocal: pts, width: 0.075 * wMul * (0.4 + 0.6 * Math.min(1, Math.min(u, 1 - u) / 0.10)), thickness: 0.008,
        stiffnessBias: 0.96, layerAO: 0.74, layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4,
      });
    }

    // Zone 2: swept-back top, SHORT-to-MEDIUM locks in overlapping tiers (front tiers lie over the next), gathered at the tie
    const tierStart = [0.0, 0.2, 0.42, 0.62];
    const tierLen = 0.36;
    const tierCount = [Math.round(40 * density), Math.round(36 * density), Math.round(30 * density), Math.round(24 * density)];
    for (let t = 0; t < 4; t++) {
      const n = tierCount[t];
      for (let i = 0; i < n; i++) {
        const u = (i + 0.5) / n;
        const th0 = THREE.MathUtils.lerp(-1.28, 1.28, u) + (randS() - 0.5) * 0.05;
        const dS = dirOf(th0, hs(th0) + 0.02);
        const f0 = tierStart[t] + (randS() - 0.5) * 0.04;
        const f1 = Math.min(1, f0 + tierLen + (randS() - 0.5) * 0.05);
        const kEnd = [0.7, 0.55, 0.4, 0.1][t];
        const dT = dirOf(Math.PI - th0 * kEnd, tiePhi - 0.12 + 0.12 * (t / 3));
        const pts = arcLock(dS, dT, f0, f1, 0.014 - 0.0033 * t + randS() * 0.0010, 0.008, (randS() - 0.5) * 0.9);
        if (f1 >= 0.99 && t === 3) gatherTip(pts);
        specs.push({
          pointsLocal: pts, width: (0.042 + randS() * 0.010) * wMul, thickness: 0.012, stiffnessBias: 0.9,
          layerAO: 0.9, layerOrder: t < 2 ? 3 : 2, lockNormal: false, scalpRoot: 0.55, smooth: 3, roundness: 0.4,
        });
      }
    }

    // Zone 2b: side and behind-the-ear hair, longer, sweeping back and up into the tie
    for (const sd of [-1, 1]) {
      const nS = Math.round(22 * density);
      for (let i = 0; i < nS; i++) {
        const u = (i + 0.5) / nS;
        const th0 = sd * THREE.MathUtils.lerp(1.15, 2.7, u);
        const startPhi = hs(th0) + 0.02 + randS() * 0.22 - (u > 0.5 ? 0.2 : 0);
        const dS = dirOf(th0, Math.max(0.5, Math.min(startPhi, 1.8)));
        const f0 = randS() * 0.08;
        const pts = arcLock(dS, tieDir, f0, 1, 0.010 + randS() * 0.003, 0.008, (randS() - 0.5) * 1.2);
        gatherTip(pts);
        specs.push({
          pointsLocal: pts, width: (0.050 + randS() * 0.012) * wMul, thickness: 0.012, stiffnessBias: 0.92,
          layerAO: 0.88, layerOrder: 2, lockNormal: false, scalpRoot: 0.55, smooth: 3, roundness: 0.4,
        });
      }
    }
    // nape: lower hair under the ear line feeding up/in to the tie
    const lowCount = Math.round(30 * density);
    for (let i = 0; i < lowCount; i++) {
      const u = i / (lowCount - 1);
      const theta = THREE.MathUtils.lerp(Math.PI - 1.35, Math.PI + 1.35, u);
      const pts = buildSculptedLock({
        thetaStart: theta,
        thetaEnd: THREE.MathUtils.lerp(theta, Math.PI, 0.9),
        phiStart: 1.35 + randS() * 0.25,
        phiEnd: tiePhi,
        baseLift: 0.010,
        midArch: 0.008,
        tipFlickOut: 0.0,
        napeHug: 1.30,
      });
      gatherTip(pts);
      specs.push({
        pointsLocal: pts, width: (0.045 + randS() * 0.012) * wMul, thickness: 0.011, stiffnessBias: 0.92,
        layerAO: 0.86, layerOrder: 2, lockNormal: false, scalpRoot: 0.38, smooth: 3, roundness: 0.4,
      });
    }
    // soft fade along the lower nape edge
    const napeCount = 30;
    for (let i = 0; i < napeCount; i++) {
      const u = i / (napeCount - 1);
      const theta = THREE.MathUtils.lerp(Math.PI - 1.1, Math.PI + 1.1, u);
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: THREE.MathUtils.lerp(theta, Math.PI, 0.35),
        phiStart: 1.7, phiEnd: 2.14, baseLift: 0.006, midArch: 0.004, tipFlickOut: 0.0, napeHug: 1.35,
      });
      specs.push({
        pointsLocal: pts, width: 0.05 * wMul, thickness: 0.005, stiffnessBias: 0.95, layerAO: 0.85,
        layerOrder: 1, isFadeCap: true, fadeStart: 0.35, lockNormal: false, scalpRoot: 0.42,
      });
    }

    // nape curtain: short locks hanging below the tie line either side of the tail (V-shaped lower edge)
    for (let i = 0; i < 34; i++) {
      const u = i / 33;
      const dth = (u - 0.5) * 2.2;
      const theta = Math.PI + dth;
      const endPhi = 2.42 - 0.34 * Math.pow(Math.abs(dth) / 1.1, 1.3);
      const pts = buildSculptedLock({ thetaStart: theta, thetaEnd: THREE.MathUtils.lerp(theta, Math.PI, 0.25), phiStart: 1.5 + randS() * 0.1, phiEnd: endPhi, baseLift: 0.014, midArch: 0.006, tipFlickOut: 0.0, napeHug: 1.3 });
      specs.push({
        pointsLocal: pts, width: (0.07 + randS() * 0.012) * wMul, thickness: 0.011, stiffnessBias: 0.92,
        layerAO: 0.86, layerOrder: 1, lockNormal: false, scalpRoot: 0.55, smooth: 3, roundness: 0.4,
      });
    }

    // Zone 3: real silver sideburns in front of the ears: wide, dense wedge of tapered locks down to the lobe line
    const sbTip = earSideburnTip({ sideburnLength: 1.0 });
    for (const sd of [-1, 1]) {
      for (let i = 0; i < 30; i++) {
        const u = i / 29; // 0 front edge .. 1 ear side
        const th = sd * (sbTip.theta - 0.17 * (1 - u) + (randS() - 0.5) * 0.012);
        const endPhi = THREE.MathUtils.lerp(sbTip.phi - 0.42, sbTip.phi + 0.24, Math.pow(u, 0.9)) + (randS() - 0.5) * 0.08;
        const pts = buildSculptedLock({ thetaStart: th, thetaEnd: th + sd * 0.03, phiStart: 0.7 + randS() * 0.15, phiEnd: endPhi, baseLift: 0.012 + 0.0012 * (i % 3), midArch: 0.003, tipFlickOut: 0.0, napeHug: 1.4 });
        specs.push({
          pointsLocal: pts, width: (0.028 + 0.006 * u) * wMul, thickness: 0.012, stiffnessBias: 0.97, layerAO: 0.45,
          layerOrder: 3, lockNormal: false, scalpRoot: 0.8, smooth: 3, roundness: 0.5, isFadeCap: true, fadeStart: 0.55,
        });
      }
    }

    // Zone 4: thick tied tail: parallel tapered bundle (no fray), swelling then tapering to a rounded point
    const tailN = Math.round(34 * density);
    const ringR = 0.068; // dark tie ring radius
    const tailTop = tieAnchor.clone().add(new THREE.Vector3(0, 0.02, 0));
    const tailL = 0.36 + config.backLength * 0.04;
    const radius = (t: number) => ringR * (0.55 + 0.85 * Math.sin(Math.PI * Math.pow(t, 1.45))) * (1 - Math.pow(t, 3.2));
    for (let i = 0; i < tailN; i++) {
      const rho = Math.sqrt((i + 0.5) / tailN);
      const ang = i * 2.39996;
      const pts: THREE.Vector3[] = [];
      for (let k = 0; k < NODES_PER_LOCK; k++) {
        const t = k / (NODES_PER_LOCK - 1);
        const R = Math.max(0.003, radius(t));
        const cy = tailTop.y - 0.012 - tailL * Math.pow(t, 1.12);
        const cz = tailTop.z - 0.01 - 0.05 * Math.sin(Math.min(1, t * 1.5) * Math.PI * 0.5) - 0.02 * t;
        pts.push(new THREE.Vector3(tailTop.x + Math.cos(ang) * rho * R, cy, cz + Math.sin(ang) * rho * R * 0.8));
      }
      specs.push({
        pointsLocal: pts, width: (0.034 + randT() * 0.008) * wMul, thickness: 0.026, stiffnessBias: 0.8, layerAO: 0.9,
        layerOrder: 3, lockNormal: true, scalpRoot: 0.4, smooth: 3, roundness: 0.6,
      });
    }

    // Zone 5: dark hair-tie ring: wound spiral of short locks (the shader's root colour is dark, so each piece shows its dark root)
    const ringN = 36;
    for (let j = 0; j < ringN; j++) {
      const a0 = (j / ringN) * Math.PI * 2;
      const rr = ringR * 1.08 + 0.0007 * j;
      const ty = tailTop.y - 0.028 + 0.0006 * j;
      const cz = tailTop.z - 0.01;
      const pts: THREE.Vector3[] = [];
      for (let k = 0; k < NODES_PER_LOCK; k++) {
        const t = k / (NODES_PER_LOCK - 1);
        const a = a0 + t * 0.62;
        pts.push(new THREE.Vector3(tailTop.x + Math.cos(a) * rr, ty + (t - 0.5) * 0.002, cz + Math.sin(a) * rr * 0.85));
      }
      specs.push({
        pointsLocal: pts, width: 0.05 * wMul, thickness: 0.032, stiffnessBias: 0.99, layerAO: 0.2,
        layerOrder: 4, lockNormal: false, scalpRoot: 1.0, isFadeCap: true, fadeStart: 0.1,
      });
    }

    // Zone 6: no facial hair (a clean game-style beard could not be built with tapered locks; none is the safe default)

    return specs;
  }
