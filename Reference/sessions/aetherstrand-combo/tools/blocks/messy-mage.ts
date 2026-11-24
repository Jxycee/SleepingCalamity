  if (arch === 'messy-mage') {
    // Messy Mage v3 (video-game cut): ~30 large layered pointed clumps (bundles of locks), soft hairline, sheen-friendly per-clump AO, thin wisps.
    // theta 0 = front, +-PI/2 = ear sides, PI = back.
    const mkR = (sd: number) => { let st = (sd >>> 0) || 1; return () => { st = (st * 1664525 + 1013904223) >>> 0; return st / 4294967296; }; };
    const sd0 = config.seed || 1337;
    const rB = mkR(sd0 + 22), rC = mkR(sd0 + 33), rD = mkR(sd0 + 44), rE = mkR(sd0 + 55), rF = mkR(sd0 + 66);
    const sideEnd = THREE.MathUtils.lerp(2.0, 2.5, config.sideLength);
    const backEnd = THREE.MathUtils.lerp(2.4, 2.9, config.backLength);
    const browPhi = THREE.MathUtils.lerp(1.8, 2.05, config.bangsLength);
    const sweep = THREE.MathUtils.clamp(config.bangsSweep, -1, 1);
    const dir = sweep >= 0 ? 1 : -1;
    const aT = (th: number) => Math.abs(Math.atan2(Math.sin(th), Math.cos(th)));
    // shell hairline phi by |theta|: forehead, receding temple, ear, behind ear, nape
    const hk: Array<[number, number]> = [[0, 1.34], [0.4, 1.38], [0.7, 1.52], [0.95, 1.78], [1.2, 1.84], [1.4, 1.62], [1.9, 1.55], [2.2, 1.95], [2.7, 2.05], [3.2, 2.08]];
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
      const a = aT(th);
      if (a < 1.0) return browPhi;
      if (a < 1.3) return THREE.MathUtils.lerp(browPhi + 0.1, sideEnd - 0.1, (a - 1.0) / 0.3);
      if (a < 1.9) return 1.5;
      if (a < 2.3) return THREE.MathUtils.lerp(sideEnd - 0.1, backEnd - 0.1, (a - 1.9) / 0.4);
      return backEnd;
    };
    type CO = { th: number; thEnd: number; p0: number; p1: number; n: number; spread: number; w: number; lift: number; arch: number; flick: number; s: number; layer: number; root: number; ao: number; r: () => number; len?: number; fade?: boolean; thick?: number };
    // a clump = bundle of locks converging to a pointed tip, S-curved
    const clump = (o: CO) => {
      for (let k = 0; k < o.n; k++) {
        const u = o.n > 1 ? (k / (o.n - 1)) * 2 - 1 : 0;
        const jit = (o.r() - 0.5);
        const th = o.th + u * o.spread + jit * 0.02;
        const thEnd = o.thEnd + u * o.spread * 0.12 + jit * 0.03;
        const p1 = o.p1 - Math.abs(u) * (o.len ?? 0.07) - o.r() * 0.035;
        const pts = buildSculptedLock({
          thetaStart: th, thetaEnd: thEnd, phiStart: o.p0, phiEnd: Math.max(o.p0 + 0.3, p1),
          baseLift: o.lift, midArch: o.arch * (0.85 + o.r() * 0.3), tipFlickOut: o.flick * (0.7 + o.r() * 0.6),
          sCurveAmp: o.s * (0.8 + 0.4 * o.r()), napeHug: 0.9,
        });
        const sp: any = {
          pointsLocal: pts, width: o.w * wMul * (0.85 + 0.3 * o.r()), thickness: o.thick ?? 0.013, stiffnessBias: 0.8,
          layerAO: THREE.MathUtils.clamp(o.ao + (o.r() - 0.5) * 0.04, 0.5, 1), layerOrder: o.layer, lockNormal: false, scalpRoot: o.root, smooth: 3, roundness: 0.35,
        };
        specs.push(sp);
      }
    };

    // Zone 1: foundation shell. Opaque crown, soft faded hairline on the front half.
    for (let tier = 0; tier < 2; tier++) {
      const count = 34;
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + tier * 0.09;
        const end = hairline(theta);
        const phiStart = tier === 0 ? 0.01 : 0.55;
        if (end - phiStart < 0.25) continue;
        const pts = buildSculptedLock({ thetaStart: theta, thetaEnd: theta, phiStart, phiEnd: end, baseLift: 0.008, midArch: 0.01, tipFlickOut: 0.01, napeHug: 1.05 });
        const front = aT(theta) < 1.25;
        const sp: any = { pointsLocal: pts, width: 0.095 * wMul, thickness: 0.010, stiffnessBias: 0.92, layerAO: 0.7, layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4 };
        if (front && tier === 1) { sp.isFadeCap = true; sp.fadeStart = 0.6; }
        specs.push(sp);
      }
    }
    // soft hairline: thin fade-cap ribbons riding the hairline (forehead + receding temples)
    for (let i = 0; i < 22; i++) {
      const frac = (i / 21) * 2 - 1;
      const th = frac * 1.15;
      const hp = hairline(th);
      const pts = buildSculptedLock({ thetaStart: th, thetaEnd: th, phiStart: hp - 0.5, phiEnd: hp + 0.045, baseLift: 0.008, midArch: 0.004, tipFlickOut: 0.002, napeHug: 1.0 });
      specs.push({ pointsLocal: pts, width: 0.07 * wMul, thickness: 0.010, stiffnessBias: 0.9, layerAO: 0.75, layerOrder: 1, isFadeCap: true, fadeStart: 0.55, lockNormal: false, scalpRoot: 0.4, smooth: 2 });
    }
    // baby hairs: tiny fine locks along forehead and temples
    for (let i = 0; i < 30; i++) {
      const frac = (i / 29) * 2 - 1;
      const th = frac * 1.1 + (rF() - 0.5) * 0.06;
      const hp = hairline(th);
      const side = th >= 0 ? 1 : -1;
      const pts = buildSculptedLock({ thetaStart: th, thetaEnd: th + side * (0.03 + rF() * 0.08), phiStart: hp - 0.07, phiEnd: hp + 0.05 + rF() * 0.05, baseLift: 0.008, midArch: 0.01, tipFlickOut: 0.012, sCurveAmp: (rF() - 0.5) * 0.08, napeHug: 1.0 });
      specs.push({ pointsLocal: pts, width: (0.006 + rF() * 0.004) * wMul, thickness: 0.006, stiffnessBias: 0.7, layerAO: 0.8, layerOrder: 5, lockNormal: false, scalpRoot: 0.3, smooth: 3 });
    }

    // Zone 2: back and side tiers. Three tiers (top short, mid, long under-layer), gaps between clumps.
    const tierDefs = [
      { tag: 0, lenOff: 0.36, p0: 0.3, lift: 0.014, arch: 0.04, layer: 2, ao: 1.0, n: 6 },
      { tag: 1, lenOff: 0.16, p0: 0.5, lift: 0.012, arch: 0.05, layer: 3, ao: 1.0, n: 6 },
      { tag: 2, lenOff: 0.0, p0: 0.8, lift: 0.01, arch: 0.04, layer: 4, ao: 0.96, n: 5 },
    ];
    for (const td of tierDefs) {
      const step = td.tag === 1 ? 0.5 : 0.46;
      const off = td.tag * 0.19;
      // right side then back then left side (skip the ear zone)
      for (let a0 = 1.0; a0 < Math.PI * 2 - 0.9; a0 += step) {
        const theta = a0 + off + (rB() - 0.5) * 0.08;
        const a = aT(theta);
        if (a > 1.3 && a < 1.88) continue; // leave the ear clear
        const target = lenAt(theta);
        const mid = a > 1.9;
        const end = target - td.lenOff * (mid ? 1 : 0.8) + (rB() - 0.5) * 0.14;
        clump({
          th: theta, thEnd: theta + (rB() - 0.5) * 0.12, p0: td.p0, p1: end, n: td.n, spread: 0.12 + rB() * 0.04, w: 0.06 + rB() * 0.02,
          lift: td.lift + (mid ? 0.014 : 0.024), arch: td.arch + (mid ? 0.025 : 0.02), flick: mid ? 0.1 + 0.04 * rB() : 0.07, s: (rB() - 0.5) * 0.2, layer: td.layer, root: 0.3, ao: td.ao + (rB() - 0.5) * 0.3, r: rB,
        });
      }
    }
    // temple clumps: cover the gap between fringe and ear, tucked close, receding hairline at the temple
    for (const side of [-1, 1]) {
      for (let j = 0; j < 4; j++) {
        const th0 = side * (0.55 + j * 0.15);
        clump({
          th: th0, thEnd: th0 + side * (0.08 + rB() * 0.05), p0: 0.35, p1: 1.5 + j * 0.03 + rB() * 0.06, n: 6, spread: 0.1, w: 0.055,
          lift: 0.03, arch: 0.04, flick: 0.04, s: side * (rB() - 0.3) * 0.12, layer: 3 + (j % 2), root: 0.3, ao: 0.86 - (j % 2) * 0.08, r: rB,
        });
      }
    }
    // over-ear clumps: hair rests on the top of the ear (ear stays visible below)
    for (const side of [-1, 1]) {
      for (let j = 0; j < 3; j++) {
        const th0 = side * (1.38 + j * 0.2);
        clump({
          th: th0, thEnd: th0 + side * 0.04, p0: 0.5, p1: 1.55 + 0.04 * (j % 2), n: 5, spread: 0.1, w: 0.055,
          lift: 0.05, arch: 0.04, flick: 0.03, s: side * 0.04, layer: 3 + (j % 2), root: 0.3, ao: 0.84 - (j % 2) * 0.06, r: rB,
        });
      }
    }
    // sideburn clumps in front of the ear, tucked close
    for (const side of [-1, 1]) {
      for (let j = 0; j < 3; j++) {
        const th0 = side * (1.12 + j * 0.15);
        clump({
          th: th0, thEnd: th0 + side * (0.05 + 0.04 * j), p0: 0.9, p1: THREE.MathUtils.lerp(sideEnd - 0.3, sideEnd - 0.1, j / 2), n: 7, spread: 0.08, w: 0.055,
          lift: 0.05, arch: 0.05, flick: 0.04, s: side * 0.06, layer: 3 + j, root: 0.3, ao: 0.84 - j * 0.06, r: rB,
        });
      }
      // one thin wisp in front of the ear
      const wp = buildSculptedLock({ thetaStart: side * 1.22, thetaEnd: side * 1.3, phiStart: 0.9, phiEnd: 1.9, baseLift: 0.008, midArch: 0.012, tipFlickOut: 0.03, sCurveAmp: side * 0.05, napeHug: 0.95 });
      specs.push({ pointsLocal: wp, width: 0.011 * wMul, thickness: 0.007, stiffnessBias: 0.75, layerAO: 0.9, layerOrder: 5, lockNormal: false, scalpRoot: 0.3, smooth: 3 });
    }

    // Zone 3: crown clumps radiating from a whorl, volume concentrated here
    const whorl = 2.6;
    const crownN = 9;
    for (let i = 0; i < crownN; i++) {
      const theta = (i / crownN) * Math.PI * 2 + (rC() - 0.5) * 0.2;
      const a = aT(theta);
      const phiStart = 0.04 + rC() * 0.2;
      let end = Math.min(hairline(theta) + 0.1, phiStart + 0.95 + rC() * 0.3);
      if (a < 0.9) end = Math.min(end, 1.0 + rC() * 0.25);
      const twist = 0.35 + 0.35 * Math.cos(theta - whorl) + (rC() - 0.5) * 0.3;
      clump({
        th: theta, thEnd: theta + twist, p0: phiStart, p1: end, n: 6, spread: 0.2, w: 0.04 + rC() * 0.015,
        lift: 0.024, arch: 0.07 + config.crownVolume * 0.04 + spikeAmt * 0.03, flick: 0.035 + spikeAmt * 0.05, s: (rC() - 0.5) * 0.26, layer: 4, root: 0.34, ao: 0.92 + (rC() - 0.5) * 0.1, r: rC,
      });
    }

    // Zone 4: soft side-swept fringe: 7 pointed clumps falling over the brow to one side
    const bangN = 9;
    for (let i = 0; i < bangN; i++) {
      const u = i / (bangN - 1);
      const theta = (u * 2 - 1) * 0.8;
      const edge = 1 - Math.abs(u * 2 - 1);
      const sideBias = 0.5 + 0.5 * dir * (u * 2 - 1);
      const end = browPhi - 0.02 + (rD() - 0.4) * 0.14 - Math.pow(edge, 1.2) * 0.2 * (1 - 0.5 * sideBias) + sideBias * 0.06 + (i % 3 === 0 ? 0.08 : 0);
      clump({
        th: theta, thEnd: theta + dir * (0.28 + rD() * 0.2) * (0.5 + 0.8 * sideBias) + (theta > 0 ? 0.1 : -0.1) * (1 - edge), p0: 0.3 + rD() * 0.2, p1: end, n: 6, spread: 0.12, w: 0.05 + rD() * 0.012,
        lift: 0.02, arch: 0.04 + rD() * 0.015, flick: 0.035 + rD() * 0.03, s: (rD() - 0.5) * 0.18, layer: 6 + (i % 2), root: 0.34, ao: 0.92 - (i % 3) * 0.05, r: rD, len: 0.09,
      });
    }

    // Zone 5: thin wisps flicking off the crown and falling over the fringe, plus two cowlicks
    for (let i = 0; i < 16; i++) {
      let theta: number, phiStart: number, end: number, thEnd: number, arch: number, flick: number;
      if (i < 6) { // flick up from the crown
        theta = 0.2 + i * 0.55 + (rE() - 0.5) * 0.3; phiStart = 0.06 + rE() * 0.2; end = phiStart + 0.45 + rE() * 0.25; thEnd = theta + (rE() - 0.5) * 0.9; arch = 0.13 + rE() * 0.05; flick = 0.08 + rE() * 0.04;
      } else if (i < 11) { // fall over the fringe
        theta = (rE() - 0.5) * 1.3; phiStart = 0.2 + rE() * 0.2; end = browPhi + 0.05 + rE() * 0.1; thEnd = theta + dir * (0.2 + rE() * 0.25); arch = 0.05 + rE() * 0.03; flick = 0.04;
      } else { // back and side flyaways
        theta = (i % 2 ? 1 : -1) * (1.9 + rE() * 1.1); phiStart = 0.3 + rE() * 0.3; end = phiStart + 0.7 + rE() * 0.4; thEnd = theta + (rE() - 0.5) * 0.6; arch = 0.06 + rE() * 0.04; flick = 0.06;
      }
      const pts = buildSculptedLock({ thetaStart: theta, thetaEnd: thEnd, phiStart, phiEnd: end, baseLift: 0.04 + rE() * 0.02, midArch: arch, tipFlickOut: flick, sCurveAmp: (rE() - 0.5) * 0.4, napeHug: 1.0 });
      specs.push({ pointsLocal: pts, width: (0.014 + rE() * 0.005) * wMul, thickness: 0.009, stiffnessBias: 0.6, layerAO: 0.95, layerOrder: 8, lockNormal: false, scalpRoot: 0.4, smooth: 3 });
    }

    return specs;
  }
