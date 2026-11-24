  if (arch === 'noble-hime') {
    // NH-BEGIN noble-hime (long straight sleek, skull-hugging crown, blunt bangs, hime panels)
    const backEnd = THREE.MathUtils.lerp(3.7, 4.6, config.backLength);
    const himeEnd = THREE.MathUtils.lerp(2.35, 2.55, config.sideLength);
    const bluntBrowPhi = THREE.MathUtils.lerp(1.62, 1.8, config.bangsLength);
    const nhRand = seededRand(config.seed * 7 + 11);

    // Zone 1: thin skull-hugging base shell
    for (let tier = 0; tier < 2; tier++) {
      const count = 24;
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + tier * 0.13;
        if (tier >= 1 && Math.cos(theta) > 0.6) continue;
        const phiStart = tier === 0 ? 0.12 : 0.5;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart,
          phiEnd: Math.min(backEnd - 0.10, phiStart + 1.6, Math.cos(theta) > 0.3 ? 1.25 : 9),
          baseLift: 0.002, midArch: 0.002, tipFlickOut: 0.0, napeHug: 0.9,
        });
        specs.push({
          pointsLocal: pts, width: 0.09 * wMul, thickness: 0.01, stiffnessBias: 0.8, layerAO: 0.9,
          layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: tier === 0 ? 0.5 : 0.3,
        });
      }
    }

    // Zone 2: long straight back and rear-side curtain (behind the ears), straight hem
    const backCount = Math.round(56 * density);
    for (let i = 0; i < backCount; i++) {
      const u = i / (backCount - 1);
      const theta = THREE.MathUtils.lerp(1.4, Math.PI * 2 - 1.4, u);
      const hemEnd = backEnd - 0.06 * Math.pow(Math.abs(Math.cos(theta * 0.5 - Math.PI / 2)), 2) + (nhRand() - 0.5) * 0.02;
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: theta,
        phiStart: 0.3 + (i % 2) * 0.08, phiEnd: hemEnd,
        baseLift: 0.003, midArch: 0.002, tipFlickOut: 0.0, napeHug: 0.88,
      });
      specs.push({
        pointsLocal: pts, width: 0.07 * wMul, thickness: 0.011, stiffnessBias: 0.8, layerAO: 0.9,
        layerOrder: 1, lockNormal: false, scalpRoot: 0.4, isFadeCap: true, fadeStart: 0.97,
      });
    }

    // Zone 3: hime panels, wide flat parallel ribbons from the temple to the jaw, separate from the curtain
    for (const side of [-1, 1]) {
      for (let k = 0; k < 6; k++) {
        const theta = side * (0.62 + k * 0.1);
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta,
          phiStart: 0.3 + k * 0.03, phiEnd: himeEnd + (nhRand() - 0.5) * 0.01,
          baseLift: 0.008, midArch: 0.003, tipFlickOut: 0.012, napeHug: 1.0,
        });
        specs.push({
          pointsLocal: pts, width: 0.075 * wMul, thickness: 0.011, stiffnessBias: 0.85, layerAO: 0.9,
          layerOrder: 2, lockNormal: false, scalpRoot: 0.34, isFadeCap: true, fadeStart: 0.97,
        });
      }
    }

    // Zone 4: blunt bangs from a centre part, temple to temple, straight hem
    const bangCount = Math.round(46 * density);
    for (let i = 0; i < bangCount; i++) {
      const frac = (i / (bangCount - 1)) * 2 - 1;
      if (Math.abs(frac) < 0.03) continue;
      const theta = frac * 0.52 + (frac >= 0 ? 0.03 : -0.03);
      const pts = buildSculptedLock({
        thetaStart: theta * 0.55, thetaEnd: theta,
        phiStart: 0.3 + Math.abs(frac) * 0.08, phiEnd: bluntBrowPhi + frac * frac * 0.05 + (nhRand() - 0.5) * 0.004,
        baseLift: 0.005, midArch: 0.003, tipFlickOut: 0.008, napeHug: 1.05,
      });
      specs.push({
        pointsLocal: pts, width: 0.075 * wMul, thickness: 0.011, stiffnessBias: 0.9, layerAO: 0.9,
        layerOrder: 3, lockNormal: false, scalpRoot: 0.36, isFadeCap: true, fadeStart: 0.97,
      });
    }

    return specs;
    // NH-END
  }
