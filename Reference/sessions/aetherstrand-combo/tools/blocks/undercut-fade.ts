  if (arch === 'undercut-fade') {
    const sweepSign = config.bangsSweep >= 0 ? 1 : -1;
    const fadeNapeEnd = THREE.MathUtils.lerp(1.9, 2.1, config.backLength);
    const frontHairline = 1.28;
    const smooth01 = (x: number) => { const c = THREE.MathUtils.clamp(x, 0, 1); return c * c * (3 - 2 * c); };
    const mkU = (sd: number) => { let st = (sd >>> 0) || 1; return () => { st = (st * 1664525 + 1013904223) >>> 0; return st / 4294967296; }; };
    const seedU = (config.seed || 1337);
    const rS1 = mkU(seedU + 11), rS2 = mkU(seedU + 22), rS3 = mkU(seedU + 33), rS4 = mkU(seedU + 44), rS5 = mkU(seedU + 55);
    // cut line phi(|theta|): shared ear-aligned helper (pointed sideburn in front of the ear, arch over the ear, curved nape)
    const cutPhi = (th: number): number => earAlignedCutPhi(th, { hairlinePhi: frontHairline, templeRecede: 0.12, sideburnLength: THREE.MathUtils.lerp(1.1, 1.3, config.sideLength), sideburnTaper: 0.30, earClearance: 0.06, napePhi: fadeNapeEnd, napeCurve: 1.3 });
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

    // Zone 1: skin-tight dark scalp shell (dense: no scalp showing)
    for (let tier = 0; tier < 3; tier++) {
      const count = 28;
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + tier * 0.2;
        const maxPhi = Math.min(cutPhi(theta) - 0.04, 0.98 + 0.08 * (1 - Math.abs(Math.cos(theta))));
        const phiStart = tier === 0 ? 0.01 : 0.22 + tier * 0.34;
        if (phiStart >= maxPhi - 0.05) continue;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart, phiEnd: Math.min(maxPhi, phiStart + 0.7),
          baseLift: 0.002, midArch: 0.001, tipFlickOut: 0.0, napeHug: 1.4,
        });
        specs.push({
          pointsLocal: pts, width: 0.095 * wMul, thickness: 0.008, stiffnessBias: 0.99, layerAO: 0.76,
          layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4,
        });
      }
    }

    // Zone 2: dark dense stubble fade (isFadeCap): overlapping ribbons from the fade top to the cut line,
    // dense for most of their length (per-lock fadeStart), then the stipple dissolves them to skin at the cut line.
    {
      const fadeCols = Math.round(190 * density);
      const fadeRowsN = 3;
      for (let r = 0; r < fadeRowsN; r++) {
        for (let i = 0; i < fadeCols; i++) {
          const theta = (i / fadeCols) * Math.PI * 2 + r * 0.5 * (Math.PI * 2 / fadeCols) + (rS2() - 0.5) * 0.015;
          const cosT = Math.cos(theta);
          if (cosT > 0.88) continue; // forehead is the fringe's job
          const topPhi = 0.82 + 0.08 * (1 - Math.abs(cosT));
          const lim = THREE.MathUtils.lerp(cutPhi(theta), topPhi + 0.12, smooth01((cosT - 0.4) / 0.45)) + (rS2() - 0.5) * 0.03;
          const pts = buildSculptedLock({
            thetaStart: theta, thetaEnd: theta, phiStart: topPhi, phiEnd: lim,
            baseLift: 0.0018, midArch: 0.0, tipFlickOut: 0.0, napeHug: 1.55,
          });
          specs.push({
            pointsLocal: pts, width: 0.05 * wMul, thickness: 0.004, stiffnessBias: 0.99,
            layerAO: 0.8, layerOrder: 1, isFadeCap: true, fadeStart: 0.65 + rS2() * 0.2, lockNormal: false, scalpRoot: 0.35,
          });
        }
      }
    }

    // Zone 3: soft textured quiff. Voronoi clump seeds, each with a diagonal sweep direction and length;
    // many narrow overlapping locks follow the nearest seed, longest near the seed core so clumps taper to points.
    {
      type Seed = { x: number; z: number; ang: number; len: number; ao: number; lift: number };
      const seeds: Seed[] = [];
      const nSeeds = 30;
      for (let s = 0; s < nSeeds; s++) {
        let x = 0, z = 0, ok = false;
        for (let tries = 0; tries < 20 && !ok; tries++) {
          x = (rS3() - 0.5) * 0.5; z = (rS3() - 0.5) * 0.58 + (s % 2 === 0 ? 0.06 : 0);
          ok = (x / 0.25) * (x / 0.25) + (z / 0.30) * (z / 0.30) <= 1.0;
        }
        const zf = THREE.MathUtils.clamp((z + 0.28) / 0.56, 0, 1);
        // angle of sweep away from straight-forward, toward the sweep side; back of the crown sweeps back/out
        const fwdAng = sweepSign * (0.55 + (rS3() - 0.5) * 0.7);
        const backAng = Math.PI - sweepSign * 0.35 + (rS3() - 0.5) * 0.35;
        const ang = zf > 0.35 ? fwdAng : THREE.MathUtils.lerp(backAng, fwdAng, smooth01(zf / 0.35));
        const len = (0.1 + 0.16 * zf * zf) * (0.75 + rS3() * 0.5) * (zf < 0.3 ? 1.3 : 1);
        seeds.push({ x, z, ang, len, ao: 0.68 + rS3() * 0.32, lift: 0.7 + rS3() * 0.5 });
      }
      const dx = 0.0105 / Math.sqrt(density), dz = 0.0125 / Math.sqrt(density);
      for (let zc = -0.28; zc <= 0.28; zc += dz) {
        for (let xc = -0.25; xc <= 0.25; xc += dx) {
          const x0 = xc + (rS3() - 0.5) * dx;
          const z0 = zc + (rS3() - 0.5) * dz;
          const rr = (x0 / 0.24) * (x0 / 0.24) + (z0 / 0.295) * (z0 / 0.295);
          if (rr > 1.0) continue;
          let best = 0, bd = 1e9, sd = 1e9;
          for (let s = 0; s < seeds.length; s++) {
            const ddx = x0 - seeds[s].x, ddz = z0 - seeds[s].z; const d = ddx * ddx + ddz * ddz;
            if (d < bd) { sd = bd; bd = d; best = s; } else if (d < sd) sd = d;
          }
          const sc = seeds[best]; const zFracG = (z0 + 0.28) / 0.56;
          const dist = Math.sqrt(bd);
          if (Math.sqrt(sd) - dist < 0.012 && zFracG > 0.3) continue; // gap between clumps
          const core = 1 - THREE.MathUtils.clamp(dist / 0.065, 0, 1); // 1 at seed centre
          // locks of one clump converge on the clump tip, so the clump is a leaf shape that ends in a point
          const sA = Math.sin(sc.ang), cA = Math.cos(sc.ang);
          const along = (x0 - sc.x) * sA + (z0 - sc.z) * cA;
          if (along > sc.len * 0.55) continue;
          const tx = sc.x + sA * sc.len * 0.85 + (rS3() - 0.5) * 0.012, tz = sc.z + cA * sc.len * 0.85 + (rS3() - 0.5) * 0.012;
          const vx = tx - x0, vz = tz - z0; const vL = Math.sqrt(vx * vx + vz * vz);
          const ang = Math.atan2(vx, vz) + (rS3() - 0.5) * 0.1;
          const len = Math.min(vL * (0.9 + rS3() * 0.2), sc.len * 1.5) * (0.8 + 0.2 * core);
          const zFrac = THREE.MathUtils.clamp((z0 + 0.28) / 0.56, 0, 1);
          // keep the front end off the forehead (hairline in dome z)
          const zCap = 0.255 - 0.05 * Math.pow(Math.abs(x0) / 0.25, 2);
          const dirx = Math.sin(ang), dirz = Math.cos(ang);
          const endZ = z0 + dirz * len;
          let lenEff = len;
          if (endZ > zCap) lenEff = Math.max(0.03, len * Math.max(0.0, (zCap - z0) / Math.max(1e-4, dirz * len)));
          if (z0 > zCap) continue;
          const up = (0.014 + 0.13 * Math.pow(zFrac, 1.5)) * (0.6 + config.crownVolume * 0.6) * sc.lift * (0.55 + 0.6 * core);
          const pts: THREE.Vector3[] = [];
          for (let n = 0; n < NODES_PER_LOCK; n++) {
            const t = n / (NODES_PER_LOCK - 1);
            const x = x0 + dirx * lenEff * t;
            const z = z0 + dirz * lenEff * t;
            const lift = 0.0075 + (0.001 + up) * Math.pow(t, 1.6);
            pts.push(domePt(x, z, lift));
          }
          specs.push({ pointsLocal: pts, width: (0.024 + rS3() * 0.014 + 0.012 * zFrac) * wMul, thickness: 0.008, stiffnessBias: 0.96, layerAO: sc.ao * (0.9 + core * 0.1), layerOrder: 3, lockNormal: false, scalpRoot: 0.45 });
        }
      }
    }

    // Zone 4: short choppy asymmetric fringe: clumps of pointed locks swept toward the sweep side,
    // hanging lower on the sweep side, receding on the far side.
    {
      const nClumps = 12;
      for (let c = 0; c < nClumps; c++) {
        const f = (c + (rS4() - 0.5) * 0.6) / (nClumps - 1);
        const th0 = (f - 0.5) * 1.75 - sweepSign * 0.16;
        const side = sweepSign * (f - 0.5); // -0.5 far side .. +0.5 sweep side
        const endBase = frontHairline - 0.02 + config.bangsLength * 0.08 + (side > 0 ? side * 0.4 : side * 0.06) + (rS4() - 0.5) * 0.08;
        const sweepTh = sweepSign * (0.38 + 0.25 * rS4());
        const perC = 6;
        for (let k = 0; k < perC; k++) {
          const kk = k - (perC - 1) / 2;
          const th = th0 + kk * 0.033 + (rS4() - 0.5) * 0.01;
          const tipShort = Math.abs(kk) * 0.035 + (rS4() - 0.5) * 0.02;
          const pts = buildSculptedLock({
            thetaStart: th - sweepSign * 0.2, thetaEnd: th + sweepTh, phiStart: 0.5 + rS4() * 0.15,
            phiEnd: endBase - tipShort,
            baseLift: 0.04 + config.crownVolume * 0.03, midArch: 0.02, tipFlickOut: 0.01 + rS4() * 0.012, napeHug: 1.15,
          });
          specs.push({
            pointsLocal: pts, width: (0.017 + rS4() * 0.005) * wMul, thickness: 0.011, stiffnessBias: 0.9,
            layerAO: 0.92, layerOrder: 4, lockNormal: false, scalpRoot: 0.3,
          });
        }
      }
      // sparse fine strands between the clumps to soften the edge
      for (let i = 0; i < 40; i++) {
        const f = rS5();
        const th = (f - 0.5) * 1.7;
        const side = sweepSign * (f - 0.5);
        const pts = buildSculptedLock({
          thetaStart: th - sweepSign * 0.15, thetaEnd: th + sweepSign * 0.3, phiStart: 0.55 + rS5() * 0.1,
          phiEnd: frontHairline - 0.1 + config.bangsLength * 0.08 + (side > 0 ? side * 0.25 : side * 0.1) + (rS5() - 0.5) * 0.1,
          baseLift: 0.015, midArch: 0.008, tipFlickOut: 0.01, napeHug: 1.15,
        });
        specs.push({ pointsLocal: pts, width: 0.012 * wMul, thickness: 0.008, stiffnessBias: 0.9, layerAO: 0.94, layerOrder: 4, lockNormal: false, scalpRoot: 0.3 });
      }
    }

    return specs;
  }
