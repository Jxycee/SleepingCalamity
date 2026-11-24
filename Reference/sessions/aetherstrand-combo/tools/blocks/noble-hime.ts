  if (arch === 'noble-hime') {
    // NH-BEGIN noble-hime (long straight sleek, blunt bangs, hime sidelocks)
    const backEnd = THREE.MathUtils.lerp(3.7, 4.6, config.backLength);
    const himeCheekEnd = THREE.MathUtils.lerp(2.10, 2.45, config.sideLength);
    const bluntBrowPhi = THREE.MathUtils.lerp(1.62, 1.82, config.bangsLength);
    const nhRandB = seededRand(config.seed * 7 + 11);

    // Zone 1: smooth base shell
    for (let tier = 0; tier < 3; tier++) {
      const count = 18;
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + tier * 0.20;
        const cosT = Math.cos(theta);
        if (tier >= 1 && cosT > 0.58) continue;
        const phiStart = tier === 0 ? 0.20 : 0.22 + tier * 0.40;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart,
          phiEnd: Math.min(backEnd - 0.10, phiStart + 1.45),
          baseLift: 0.004, midArch: 0.005, tipFlickOut: 0.001, napeHug: 0.88,
        });
        specs.push({
          pointsLocal: pts, width: 0.088 * wMul, thickness: 0.012, stiffnessBias: 0.78, layerAO: 0.82,
          layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: tier === 0 ? 0.48 : 0.22,
        });
      }
    }

    // Zone 2: long straight back/rear-side curtain, blunt hem
    const backCount = Math.round(60 * density);
    for (let i = 0; i < backCount; i++) {
      const u = i / (backCount - 1);
      const theta = THREE.MathUtils.lerp(0.8, Math.PI * 2 - 0.8, u);
      const hemEnd = backEnd - 0.05 * Math.abs(Math.sin(theta * 0.5)) + (nhRandB() - 0.5) * 0.03;
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: theta,
        phiStart: 0.30 + (i % 2) * 0.10, phiEnd: hemEnd,
        baseLift: 0.006, midArch: 0.008, tipFlickOut: 0.002, napeHug: 0.86,
      });
      specs.push({
        pointsLocal: pts, width: 0.065 * wMul, thickness: 0.012, stiffnessBias: 0.68, layerAO: 0.90,
        layerOrder: 2, lockNormal: true, scalpRoot: 0.35, isFadeCap: true, fadeStart: 0.97,
      });
    }

    // Zone 3: hime panels, 8 wide overlapping parallel ribbons per side, common straight hem
    for (const side of [-1, 1]) {
      for (let k = 0; k < 8; k++) {
        const theta = side * (0.60 + k * 0.045);
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta,
          phiStart: 0.36 + k * 0.02, phiEnd: himeCheekEnd,
          baseLift: 0.02, midArch: 0.006, tipFlickOut: 0.0, napeHug: 1.05,
        });
        specs.push({
          pointsLocal: pts, width: 0.06 * wMul, thickness: 0.012, stiffnessBias: 0.85, layerAO: 0.94,
          layerOrder: 3, lockNormal: true, scalpRoot: 0.32, isFadeCap: true, fadeStart: 0.97,
        });
      }
    }

    // Zone 4: blunt straight bangs
    const bangCount = Math.round(50 * density);
    for (let i = 0; i < bangCount; i++) {
      const frac = (i / (bangCount - 1)) * 2 - 1;
      const theta = frac * 0.58 + (frac >= 0 ? 0.02 : -0.02);
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: theta,
        phiStart: 0.24 + Math.abs(frac) * 0.12, phiEnd: bluntBrowPhi + frac * frac * 0.06,
        baseLift: 0.03, midArch: 0.018, tipFlickOut: 0.012, napeHug: 1.15,
      });
      specs.push({
        pointsLocal: pts, width: 0.05 * wMul, thickness: 0.011, stiffnessBias: 0.90, layerAO: 0.92,
        layerOrder: 3, lockNormal: false, scalpRoot: 0.36, isFadeCap: true, fadeStart: 0.97,
      });
    }

    return specs;
    // NH-END
  }

