  if (arch === 'elder-scholar') {
    const tieTheta = Math.PI;
    const tiePhi = 1.72; // low scholar tie at the nape
    const tieAnchor = skullSurfacePoint(tieTheta, tiePhi, 0.018);
    const clamp01 = (v: number) => THREE.MathUtils.clamp(v, 0, 1);
    // bald-top boundary: bare forehead/top, hair from temple recession back; wider bald patch at the crown
    const phiB = (th: number) => {
      const c = Math.cos(th);
      return 0.42 + 0.62 * clamp01((c - 0.1) / 0.65) + 0.43 * clamp01((-c - 0.3) / 0.6);
    };
    const thLo = 0.72;
    const thHi = Math.PI * 2 - 0.72;
    const randS = seededRand((config.seed || 1337) + 11);
    const randT = seededRand((config.seed || 1337) + 23);

    // Zone 1: base shell below the bald boundary
    const shellCount = 60;
    for (let i = 0; i < shellCount; i++) {
      const u = i / (shellCount - 1);
      const theta = THREE.MathUtils.lerp(thLo, thHi, u);
      const isRear = Math.cos(theta) < -0.40;
      const earCut = earAlignedCutPhi(theta, { hairlinePhi: 1.38, templeRecede: 0, sideburnLength: 0.8, sideburnTaper: 0.32, earClearance: 0.06, napePhi: 1.85 });
      const pb = phiB(theta);
      const pts = buildSculptedLock({
        thetaStart: theta,
        thetaEnd: theta > Math.PI ? Math.min(thHi, theta + 0.45) : Math.max(thLo, theta - 0.45),
        phiStart: pb + 0.05,
        phiEnd: isRear ? 1.85 : earCut,
        baseLift: 0.004,
        midArch: 0.003,
        tipFlickOut: 0.0,
        napeHug: 1.35,
      });
      specs.push({
        pointsLocal: pts,
        width: 0.075 * wMul * (0.4 + 0.6 * Math.min(1, Math.min(u, 1 - u) / 0.10)),
        thickness: 0.008,
        stiffnessBias: 0.96,
        layerAO: 0.74,
        layerOrder: 0,
        isBaseShell: true,
        lockNormal: false,
        scalpRoot: 0.4,
      });
    }

    // Zone 1b: real sideburns in front of the ears (shared ear-aligned helper)
    sideburnLocks(specs, { sideburnLength: 0.8, sideburnTaper: 0.32, count: 8, width: 0.024 * wMul, layerOrder: 2, rootPhi: 1.05 });

    // Zone 2: soft thinning fade at the bald edge
    const rimCount = 64;
    for (let i = 0; i < rimCount; i++) {
      const u = i / (rimCount - 1);
      const theta = THREE.MathUtils.lerp(thLo - 0.28, thHi + 0.28, u);
      const pb = phiB(theta);
      const pts = buildSculptedLock({
        thetaStart: theta,
        thetaEnd: theta,
        phiStart: pb + 0.10,
        phiEnd: pb - 0.14,
        baseLift: 0.002,
        midArch: 0.0,
        tipFlickOut: 0.0,
        napeHug: 1.45,
      });
      specs.push({
        pointsLocal: pts,
        width: 0.05 * wMul,
        thickness: 0.004,
        stiffnessBias: 0.99,
        layerAO: 0.88,
        layerOrder: 1,
        isFadeCap: true,
        fadeStart: 0.15,
        lockNormal: false,
        scalpRoot: 0.45,
      });
    }

    // Zone 2c: wispy fade along the lower nape edge
    const napeCount = 34;
    for (let i = 0; i < napeCount; i++) {
      const u = i / (napeCount - 1);
      const theta = THREE.MathUtils.lerp(Math.PI - 1.15, Math.PI + 1.15, u);
      const pts = buildSculptedLock({
        thetaStart: theta,
        thetaEnd: THREE.MathUtils.lerp(theta, Math.PI, 0.35),
        phiStart: 1.62,
        phiEnd: 2.02,
        baseLift: 0.006,
        midArch: 0.004,
        tipFlickOut: 0.0,
        napeHug: 1.35,
      });
      specs.push({
        pointsLocal: pts,
        width: 0.05 * wMul,
        thickness: 0.005,
        stiffnessBias: 0.95,
        layerAO: 0.85,
        layerOrder: 1,
        isFadeCap: true,
        fadeStart: 0.35,
        lockNormal: false,
        scalpRoot: 0.42,
      });
    }

    // Zone 2b: long sweep-back locks from the temple/side rim into the low tie
    const sideCount = Math.round(80 * density);
    for (let i = 0; i < sideCount; i++) {
      const u = i / (sideCount - 1);
      const theta = THREE.MathUtils.lerp(thLo, thHi, u);
      const pb = phiB(theta);
      const phiStart = pb + 0.02 + randS() * 0.30;
      const pts = buildSculptedLock({
        thetaStart: theta,
        thetaEnd: THREE.MathUtils.lerp(theta, Math.PI, 0.80),
        phiStart,
        phiEnd: tiePhi,
        baseLift: 0.016 + config.crownVolume * 0.026 * (0.4 + 0.6 * clamp01((phiStart - 0.45) / 0.5)),
        midArch: 0.010 + 0.024 * clamp01((phiStart - 0.45) / 0.5),
        tipFlickOut: 0.0,
        napeHug: 1.25,
      });
      pts[NODES_PER_LOCK - 1].lerp(tieAnchor, 0.92);
      pts[NODES_PER_LOCK - 2].lerp(tieAnchor, 0.55);
      specs.push({
        pointsLocal: pts,
        width: (0.040 + randS() * 0.012) * wMul,
        thickness: 0.012,
        stiffnessBias: 0.92,
        layerAO: 0.90,
        layerOrder: 2,
        lockNormal: false,
        scalpRoot: 0.38,
      });
    }

    // Zone 2d: lower side/occipital locks behind the ear that gather into the tie (fills the shelf)
    const lowCount = Math.round(34 * density);
    for (let i = 0; i < lowCount; i++) {
      const u = i / (lowCount - 1);
      const theta = THREE.MathUtils.lerp(Math.PI - 1.35, Math.PI + 1.35, u);
      const pts = buildSculptedLock({
        thetaStart: theta,
        thetaEnd: THREE.MathUtils.lerp(theta, Math.PI, 0.88),
        phiStart: 1.22 + randS() * 0.32,
        phiEnd: tiePhi,
        baseLift: 0.010,
        midArch: 0.008,
        tipFlickOut: 0.0,
        napeHug: 1.30,
      });
      pts[NODES_PER_LOCK - 1].lerp(tieAnchor, 0.92);
      pts[NODES_PER_LOCK - 2].lerp(tieAnchor, 0.55);
      specs.push({
        pointsLocal: pts,
        width: (0.045 + randS() * 0.012) * wMul,
        thickness: 0.011,
        stiffnessBias: 0.92,
        layerAO: 0.86,
        layerOrder: 2,
        lockNormal: false,
        scalpRoot: 0.38,
      });
    }

    // Zone 4: low tied scholar tail at the nape (lockNormal: true off skull)
    const tailCount = Math.round(56 * density);
    for (let i = 0; i < tailCount; i++) {
      const ang = (i / tailCount) * Math.PI * 2;
      const pts = buildGatheredTailLock({
        anchorTheta: tieTheta + Math.cos(ang) * 0.05,
        anchorPhi: tiePhi + Math.sin(ang) * 0.03,
        outDir: new THREE.Vector3(Math.cos(ang) * 0.18, -0.6, -0.55),
        spreadX: Math.cos(ang) * 0.090,
        spreadZ: Math.sin(ang) * 0.075,
        length: 0.22 + config.backLength * 0.10,
        archOut: 0.07,
        waveAmp: 0.010,
        wavePhase: ang,
      });
      specs.push({
        pointsLocal: pts,
        width: (0.042 + randT() * 0.010) * wMul,
        thickness: 0.013,
        stiffnessBias: 0.72,
        layerAO: 0.92,
        layerOrder: 3,
        lockNormal: true,
        scalpRoot: 0.4,
      });
    }

    // Zone 5: dark hair-tie ring just below the gather point
    const ringCount = 14;
    for (let i = 0; i < ringCount; i++) {
      const ang = (i / ringCount) * Math.PI * 2;
      const pts = buildGatheredTailLock({
        anchorTheta: tieTheta + Math.cos(ang) * 0.06,
        anchorPhi: tiePhi + 0.06 + Math.sin(ang) * 0.02,
        outDir: new THREE.Vector3(Math.cos(ang) * 0.18, -0.25, -0.94),
        spreadX: Math.cos(ang) * 0.030,
        spreadZ: Math.sin(ang) * 0.022,
        length: 0.022,
        archOut: 0.05,
        waveAmp: 0.0,
        wavePhase: ang,
      });
      specs.push({
        pointsLocal: pts,
        width: 0.05 * wMul,
        thickness: 0.02,
        stiffnessBias: 0.9,
        layerAO: 0.0,
        layerOrder: 4,
        lockNormal: true,
        scalpRoot: 0.4,
      });
    }

    return specs;
  }
