  if (arch === 'undercut-fade') {
    const sweepSign = config.bangsSweep >= 0 ? 1 : -1;
    const fadeNapeEnd = THREE.MathUtils.lerp(2.12, 2.32, config.backLength);
    const frontHairline = 1.28;
    const smooth01 = (x: number) => { const c = THREE.MathUtils.clamp(x, 0, 1); return c * c * (3 - 2 * c); };
    const mkU = (sd: number) => { let st = (sd >>> 0) || 1; return () => { st = (st * 1664525 + 1013904223) >>> 0; return st / 4294967296; }; };
    const seedU = (config.seed || 1337);
    const rS1 = mkU(seedU + 11), rS2 = mkU(seedU + 22), rS3 = mkU(seedU + 33), rS4 = mkU(seedU + 44), rS5 = mkU(seedU + 55);
    // cut line phi(|theta|): shared ear-aligned helper (pointed sideburn in front of the ear, arch over the ear, curved nape)
    const cutPhi = (th: number): number => earAlignedCutPhi(th, { hairlinePhi: frontHairline, templeRecede: 0.12, sideburnLength: THREE.MathUtils.lerp(0.55, 0.7, config.sideLength), sideburnTaper: 0.14, earClearance: 0.06, napePhi: fadeNapeEnd, napeCurve: 1.3 });
    const domePt = (x: number, z: number, lift: number): THREE.Vector3 => {
      const nx = x / SCALP_RX;
      const nz = z / SCALP_RZ;
      const r2 = nx * nx + nz * nz;
      const scale = r2 > 0.88 ? Math.sqrt(0.88 / r2) : 1.0;
      const cx = nx * scale;
      const cz = nz * scale;
      const cy = Math.sqrt(Math.max(0.04, 1.0 - cx * cx - cz * cz));
      return new THREE.Vector3(
        HEAD_CENTER_LOCAL.x + cx * (SCALP_RX + lift),
        HEAD_CENTER_LOCAL.y + cy * (SCALP_RY + lift),
        HEAD_CENTER_LOCAL.z + cz * (SCALP_RZ + lift)
      );
    };

    // fade band geometry: the stubble gradient takes the lower ~45% of the top-to-cut-line height (wide on sides and back)
    const FADE_TOP = 0.85;
    const bandFrac = 0.45;
    const fadeTopAt = (th: number): number => FADE_TOP + (1 - bandFrac) * (cutPhi(th) - FADE_TOP);

    // Zone 1: dark dense scalp shell. Over the sides and back it reaches down into the fade band (solid dark stubble above the gradient).
    for (let tier = 0; tier < 4; tier++) {
      const count = 30;
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + tier * 0.17;
        const cosT = Math.cos(theta);
        const topCap = 0.98 + 0.08 * (1 - Math.abs(cosT));
        const sideDrop = 1.08;
        const frontKeep = THREE.MathUtils.smoothstep(cosT, 0.35, 0.75);
        const maxPhi = Math.min(cutPhi(theta) - 0.04, THREE.MathUtils.lerp(sideDrop, topCap, frontKeep));
        const phiStart = tier === 0 ? 0.01 : 0.22 + tier * 0.30;
        if (phiStart >= maxPhi - 0.05) continue;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart, phiEnd: Math.min(maxPhi, phiStart + 0.8),
          baseLift: 0.002, midArch: 0.001, tipFlickOut: 0.0, napeHug: 1.4,
        });
        specs.push({
          pointsLocal: pts, width: 0.095 * wMul, thickness: 0.008, stiffnessBias: 0.99, layerAO: 0.72,
          layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4,
        });
      }
    }

    // Zone 2: stubble fade (isFadeCap): five staggered rows of overlapping narrow ribbons from the upper side down to cut line - s*band.
    // Each ribbon is dark first, then the shader gradient dissolves it; the rows end at different heights, so the union reads as a
    // wide, gradual fade (about 40% of the cut height) instead of a thin dithered edge.
    {
      const fadeCols = Math.round(130 * density);
      const fadeRowsN = 5;
      for (let r = 0; r < fadeRowsN; r++) {
        const sr = r / (fadeRowsN - 1);
        for (let i = 0; i < fadeCols; i++) {
          const theta = (i / fadeCols) * Math.PI * 2 + r * 0.37 * (Math.PI * 2 / fadeCols) + (rS2() - 0.5) * 0.015;
          const cosT = Math.cos(theta);
          if (cosT > 0.88) continue; // forehead is the fringe's job
          const cutL = cutPhi(theta);
          const band = 0.5 * (cutL - FADE_TOP);
          const end = cutL - sr * band + (rS2() - 0.5) * 0.03;
          const top = FADE_TOP - 0.02 + rS2() * 0.04;
          if (top >= end - 0.15) continue;
          const pts = buildSculptedLock({
            thetaStart: theta, thetaEnd: theta, phiStart: top, phiEnd: end,
            baseLift: 0.0016 + 0.0004 * r, midArch: 0.0, tipFlickOut: 0.0, napeHug: 1.55,
          });
          specs.push({
            pointsLocal: pts, width: 0.05 * wMul, thickness: 0.004, stiffnessBias: 0.99,
            layerAO: 0.8, layerOrder: 1, isFadeCap: true, fadeStart: 0.30 + rS2() * 0.12, lockNormal: false, scalpRoot: 0.35,
          });
        }
      }
      // narrow pointed sideburns in front of each ear (opaque, tapered, tip at about mid-ear height)
      sideburnLocks(specs, { sideburnLength: 1.5, sideburnGap: 0.08, count: 5, width: 0.016, layerOrder: 2, baseLift: 0.004, rootPhi: 1.35 });
    }

    // Zone 3: slightly spiky textured top. Distinct pointed clumps of 4-6 narrow locks converging on a raised tip,
    // clear gaps between clumps (the dark shell shows in the gaps), clump lengths and lifts varied.
    {
      const dxC = 0.095, dzC = 0.098;
      for (let layer = 0; layer < 2; layer++) {
        for (let zc = -0.26; zc <= 0.22; zc += dzC) {
          for (let xc = -0.2; xc <= 0.2; xc += dxC) {
            const x0 = xc + (rS3() - 0.5) * dxC * 0.8 + layer * dxC * 0.5;
            const z0 = zc + (rS3() - 0.5) * dzC * 0.8 + layer * dzC * 0.4;
            if ((x0 / 0.235) * (x0 / 0.235) + (z0 / 0.275) * (z0 / 0.275) > 1.0) continue;
            const zf = THREE.MathUtils.clamp((z0 + 0.28) / 0.56, 0, 1);
            const fwdAng = sweepSign * (0.42 + (rS3() - 0.5) * 0.6);
            const backAng = Math.PI - sweepSign * 0.3 + (rS3() - 0.5) * 0.4;
            let ang = zf > 0.4 ? fwdAng : THREE.MathUtils.lerp(backAng, fwdAng, smooth01(zf / 0.4));
            ang += Math.sign(x0 || 1) * 0.55 * smooth01((Math.abs(x0) - 0.09) / 0.1);
            const sA = Math.sin(ang), cA = Math.cos(ang);
            const len = (layer === 0 ? 0.09 + 0.13 * zf : 0.06 + 0.07 * zf) * (0.7 + rS3() * 0.6);
            const nL = 3 + Math.floor(rS3() * 3);
            const tl = (0.04 + 0.03 * rS3()) * (0.8 + 0.5 * config.crownVolume) * (0.75 + 0.4 * zf) * (layer === 0 ? 1 : 0.65);
            const zCap = 0.26 - 0.06 * Math.pow(Math.abs(x0) / 0.25, 2);
            const tipZ = z0 + cA * len;
            const lenK = tipZ > zCap ? Math.max(0.3, (zCap - z0) / Math.max(1e-4, cA * len)) : 1;
            if (z0 > zCap - 0.015) continue;
            for (let k = 0; k < nL; k++) {
              const off = (k - (nL - 1) / 2) / Math.max(1, nL - 1) * 0.06 + (rS3() - 0.5) * 0.006;
              const rx = x0 + cA * off - sA * 0.02 * rS3();
              const rz = z0 - sA * off - cA * 0.02 * rS3();
              const mid = 1 - 0.3 * Math.abs(off) / 0.025; // central locks reach furthest, so the clump ends in a point
              const tx = x0 + sA * len * lenK * mid + cA * off * 0.15;
              const tz = z0 + cA * len * lenK * mid - sA * off * 0.15;
              const pts: THREE.Vector3[] = [];
              for (let n = 0; n < NODES_PER_LOCK; n++) {
                const t = n / (NODES_PER_LOCK - 1);
                const lift = 0.008 + tl * Math.pow(t, 1.9) + 0.008 * Math.pow(t, 6);
                pts.push(domePt(THREE.MathUtils.lerp(rx, tx, t), THREE.MathUtils.lerp(rz, tz, t), lift));
              }
              specs.push({ pointsLocal: pts, width: (0.038 + rS3() * 0.014) * wMul, thickness: 0.009, stiffnessBias: 0.96, layerAO: 0.78 + rS3() * 0.22, layerOrder: layer === 0 ? 3 : 2, smooth: 2, roundness: 0.4, lockNormal: false, scalpRoot: 0.45 });
            }
          }
        }
      }
    }

    // Zone 4: short choppy asymmetric fringe: clumps of pointed locks swept toward the sweep side,
    // hanging lower on the sweep side, receding on the far side.
    {
      const nClumps = 11;
      for (let c = 0; c < nClumps; c++) {
        const f = (c + (rS4() - 0.5) * 0.6) / (nClumps - 1);
        const th0 = (f - 0.5) * 1.75 - sweepSign * 0.16;
        const side = sweepSign * (f - 0.5); // -0.5 far side .. +0.5 sweep side
        const endBase = frontHairline + 0.14 + config.bangsLength * 0.08 + (side > 0 ? side * 0.16 : side * 0.04) + (rS4() - 0.5) * 0.08;
        const sweepTh = sweepSign * (0.25 + 0.2 * rS4());
        const perC = 4;
        for (let k = 0; k < perC; k++) {
          const kk = k - (perC - 1) / 2;
          const th = th0 + kk * 0.04 + (rS4() - 0.5) * 0.01;
          const tipShort = Math.abs(kk) * 0.05 + (rS4() - 0.5) * 0.02;
          const pts = buildSculptedLock({
            thetaStart: th - sweepSign * 0.2, thetaEnd: th + sweepTh, phiStart: 0.5 + rS4() * 0.15,
            phiEnd: endBase - tipShort,
            baseLift: 0.04 + config.crownVolume * 0.03, midArch: 0.02, tipFlickOut: 0.02 + rS4() * 0.016, napeHug: 1.15,
          });
          specs.push({
            pointsLocal: pts, width: (0.03 + rS4() * 0.008) * wMul, thickness: 0.011, stiffnessBias: 0.9,
            layerAO: 0.92, layerOrder: 4, lockNormal: false, scalpRoot: 0.3,
          });
        }
      }
      // sparse fine strands between the clumps to soften the edge
      for (let i = 0; i < 12; i++) {
        const f = rS5();
        const th = (f - 0.5) * 1.7;
        const side = sweepSign * (f - 0.5);
        const pts = buildSculptedLock({
          thetaStart: th - sweepSign * 0.15, thetaEnd: th + sweepSign * 0.3, phiStart: 0.55 + rS5() * 0.1,
          phiEnd: frontHairline - 0.02 + config.bangsLength * 0.08 + (side > 0 ? side * 0.1 : side * 0.05) + (rS5() - 0.5) * 0.1,
          baseLift: 0.015, midArch: 0.008, tipFlickOut: 0.01, napeHug: 1.15,
        });
        specs.push({ pointsLocal: pts, width: 0.012 * wMul, thickness: 0.008, stiffnessBias: 0.9, layerAO: 0.94, layerOrder: 4, lockNormal: false, scalpRoot: 0.3 });
      }
    }

    return specs;
  }
