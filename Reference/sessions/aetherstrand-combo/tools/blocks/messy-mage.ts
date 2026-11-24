  if (arch === 'messy-mage') {
    // Messy Mage v2 (tuned against ref_messy_mage sheets). theta 0 = front, +-PI/2 = ear sides, PI = back.
    const mkR = (sd: number) => { let st = (sd >>> 0) || 1; return () => { st = (st * 1664525 + 1013904223) >>> 0; return st / 4294967296; }; };
    const sd0 = config.seed || 1337;
    const rB = mkR(sd0 + 22), rC = mkR(sd0 + 33), rD = mkR(sd0 + 44), rE = mkR(sd0 + 55);
    const sideEnd = THREE.MathUtils.lerp(1.78, 2.2, config.sideLength);
    const backEnd = THREE.MathUtils.lerp(1.98, 2.36, config.backLength);
    const browPhi = THREE.MathUtils.lerp(1.62, 1.86, config.bangsLength);
    const sweep = THREE.MathUtils.clamp(config.bangsSweep, -1, 1);
    const aT = (th: number) => Math.abs(Math.atan2(Math.sin(th), Math.cos(th)));
    // shell hairline phi by |theta|: forehead, temple, ear (stay above ear top), behind ear, nape
    const hk: Array<[number, number]> = [[0, 1.38], [0.45, 1.42], [0.8, 1.64], [1.05, 1.64], [1.3, 1.56], [1.9, 1.55], [2.2, 1.95], [2.7, 2.05], [3.2, 2.08]];
    const hairline = (th: number): number => {
      const a = aT(th);
      for (let k = 1; k < hk.length; k++) {
        if (a <= hk[k][0]) {
          const f = (a - hk[k - 1][0]) / (hk[k][0] - hk[k - 1][0]);
          return THREE.MathUtils.lerp(hk[k - 1][1], hk[k][1], f);
        }
      }
      return hk[hk.length - 1][1];
    };
    const lenAt = (th: number): number => {
      // length target by |theta|: sideburn in front of ear, short over ear, longer behind and at nape
      const a = aT(th);
      if (a < 1.0) return browPhi;
      if (a < 1.35) return THREE.MathUtils.lerp(browPhi + 0.1, sideEnd - 0.1, (a - 1.0) / 0.35);
      if (a < 1.9) return 1.52;
      if (a < 2.3) return THREE.MathUtils.lerp(sideEnd - 0.1, backEnd - 0.1, (a - 1.9) / 0.4);
      return backEnd;
    };
    const push = (pts: THREE.Vector3[], w: number, layer: number, ao: number, root = 0.38, sb = 0.82) =>
      specs.push({ pointsLocal: pts, width: w * wMul, thickness: 0.012, stiffnessBias: sb, layerAO: ao, layerOrder: layer, lockNormal: false, scalpRoot: root });

    // Zone 1: foundation shell (seals scalp everywhere, ends at the hairline)
    for (let tier = 0; tier < 2; tier++) {
      const count = 34;
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + tier * 0.09;
        const end = hairline(theta);
        const phiStart = tier === 0 ? 0.01 : 0.55;
        if (end - phiStart < 0.25) continue;
        const pts = buildSculptedLock({ thetaStart: theta, thetaEnd: theta, phiStart, phiEnd: end, baseLift: 0.004, midArch: 0.004, tipFlickOut: 0.002, napeHug: 1.05 });
        specs.push({ pointsLocal: pts, width: 0.095 * wMul, thickness: 0.010, stiffnessBias: 0.92, layerAO: 0.7, layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4 });
      }
    }

    // Zone 2: voluminous layered sides and back: stepped lengths, S-curves, puffed mid-length, outward flip at jaw and nape
    const bodyCount = Math.round(110 * density);
    for (let i = 0; i < bodyCount; i++) {
      const theta = (i / bodyCount) * Math.PI * 2 + (rB() - 0.5) * 0.06;
      const a = aT(theta);
      if (a < 0.95) continue;
      const target = lenAt(theta);
      const tier = i % 3;
      const end = target - tier * 0.13 * shag + (rB() - 0.5) * 0.4 * shag + (rB() < 0.18 ? 0.14 : 0);
      const inEar = a > 1.3 && a < 1.9;
      const start = inEar ? 0.5 : 0.3 + tier * 0.2;
      const back = a > 1.9;
      const flick = (back ? 0.07 + 0.05 * shag : 0.045) + rB() * 0.04;
      const pts = buildSculptedLock({
        thetaStart: theta,
        thetaEnd: theta + (rB() - 0.5) * 0.3,
        phiStart: start,
        phiEnd: Math.max(start + 0.4, end),
        baseLift: 0.012 + tier * 0.008,
        midArch: (inEar ? 0.012 : 0.05 + config.crownVolume * 0.04) + tier * 0.012 + rB() * 0.02,
        tipFlickOut: inEar ? 0.01 : flick,
        sCurveAmp: (rB() - 0.5) * 0.26,
        napeHug: 0.82,
      });
      push(pts, inEar ? 0.04 + rB() * 0.02 : (rB() < 0.22 ? 0.1 + rB() * 0.05 : 0.045 + rB() * 0.05), 2, 0.86, 0.3, 0.78);
    }

    // Zone 2b: temple and wispy sideburn locks in front of and over the ear
    for (let side = -1; side <= 1; side += 2) {
      for (let j = 0; j < 14; j++) {
        const th0 = side * (0.72 + j * 0.041 + (rB() - 0.5) * 0.04);
        const tEnd = THREE.MathUtils.lerp(1.8, sideEnd + 0.05, j / 13) + (rB() - 0.5) * 0.2;
        const pts = buildSculptedLock({
          thetaStart: th0,
          thetaEnd: th0 + side * (0.05 + rB() * 0.12),
          phiStart: 0.45 + rB() * 0.2,
          phiEnd: tEnd,
          baseLift: 0.016 + rB() * 0.01,
          midArch: 0.035 + rB() * 0.025,
          tipFlickOut: 0.03 + rB() * 0.03,
          sCurveAmp: (rB() - 0.5) * 0.2,
          napeHug: 0.85,
        });
        push(pts, 0.035 + rB() * 0.04, 3, 0.9, 0.3, 0.8);
      }
    }

    // Zone 3: crown mass radiating from a whorl (consistent swirl), puffed, S-curved
    const whorl = 2.6;
    const crownCount = Math.round(96 * density);
    for (let i = 0; i < crownCount; i++) {
      const theta = (i / crownCount) * Math.PI * 2 + (rC() - 0.5) * 0.2;
      const a = aT(theta);
      const phiStart = 0.04 + rC() * 0.4;
      let end = Math.min(hairline(theta) + 0.1, phiStart + 0.65 + rC() * 0.5);
      if (a < 0.9) end = Math.min(end, 1.0 + rC() * 0.3);
      // swirl: tips twist around the crown in one direction; more twist near the whorl side
      const twist = 0.35 + 0.35 * Math.cos(theta - whorl) + (rC() - 0.5) * 0.4;
      const pts = buildSculptedLock({
        thetaStart: theta,
        thetaEnd: theta + twist,
        phiStart,
        phiEnd: end,
        baseLift: 0.018 + rC() * 0.01,
        midArch: 0.04 + config.crownVolume * 0.04 + spikeAmt * 0.03 + rC() * 0.02,
        tipFlickOut: 0.006 + spikeAmt * 0.03 + rC() * 0.014,
        sCurveAmp: (rC() - 0.5) * 0.28,
        napeHug: 0.96,
      });
      push(pts, 0.03 + rC() * 0.04, 3, 0.9, 0.34, 0.8);
    }

    // Zone 4: side-swept layered fringe: thin irregular pieces falling over the forehead/brow to one side
    const dir = sweep >= 0 ? 1 : -1;
    const bangCount = Math.round(38 * density);
    for (let i = 0; i < bangCount; i++) {
      const u = i / Math.max(1, bangCount - 1);
      const theta = (u * 2 - 1) * 0.95 + (rD() - 0.5) * 0.1;
      const phiStart = 0.3 + rD() * 0.3;
      const edge = 1 - Math.abs(u * 2 - 1);
      const sideBias = 0.5 + 0.5 * dir * (u * 2 - 1); // 1 on the swept side
      const layer = i % 2;
      const end = browPhi - 0.04 + (rD() - 0.45) * 0.3 + (i % 4 === 0 ? 0.12 : 0) - Math.pow(edge, 1.5) * 0.3 * (1 - 0.5 * sideBias) + sideBias * 0.05;
      const pts = buildSculptedLock({
        thetaStart: theta,
        thetaEnd: theta + dir * (0.22 + rD() * 0.3) * (0.5 + 0.8 * sideBias) + (theta > 0 ? 0.12 : -0.12) * (1 - edge),
        phiStart,
        phiEnd: end,
        baseLift: 0.02 + layer * 0.01 + rD() * 0.008,
        midArch: 0.03 + layer * 0.02 + rD() * 0.02,
        tipFlickOut: 0.02 + rD() * 0.03,
        sCurveAmp: (rD() - 0.5) * 0.22,
        napeHug: 0.96,
      });
      push(pts, 0.026 + rD() * 0.04, 3 + layer, 0.92, 0.34, 0.78);
    }

    // Zone 5: thin flyaway wisps sticking up/out of the crown
    for (let i = 0; i < 5; i++) {
      const theta = (i < 3 ? 0.3 + i * 0.5 : Math.PI + (i - 3) * 0.7) + (rE() - 0.5) * 0.4;
      const phiStart = 0.08 + rE() * 0.2;
      const pts = buildSculptedLock({
        thetaStart: theta,
        thetaEnd: theta + (rE() - 0.5) * 0.7,
        phiStart,
        phiEnd: phiStart + 0.35 + rE() * 0.25,
        baseLift: 0.06 + rE() * 0.02,
        midArch: 0.1 + rE() * 0.05,
        tipFlickOut: 0.05 + rE() * 0.04,
        sCurveAmp: (rE() - 0.5) * 0.35,
        napeHug: 1.0,
      });
      push(pts, 0.016 + rE() * 0.008, 4, 0.95, 0.4, 0.7);
    }

    return specs;
  }
