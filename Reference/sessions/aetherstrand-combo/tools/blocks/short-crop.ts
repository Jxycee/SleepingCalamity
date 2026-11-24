  if (arch === 'short-crop') {
    const sideEndBase = THREE.MathUtils.lerp(1.55, 1.68, config.sideLength); // down to the ear top
    const napeEndBase = THREE.MathUtils.lerp(1.85, 2.08, config.backLength);
    const sweepX = THREE.MathUtils.clamp(config.bangsSweep, -1, 1) * 0.045;
    const topVol = 0.016 + config.crownVolume * 0.020;
    const mkR = (sd: number) => { let st = (sd >>> 0) || 1; return () => { st = (st * 1664525 + 1013904223) >>> 0; return st / 4294967296; }; };
    const rZ1 = mkR((config.seed || 1337) + 101), rZ2 = mkR((config.seed || 1337) + 202), rZ3 = mkR((config.seed || 1337) + 303);

    // Helper: map upper-dome (x, z) directly onto skull surface with custom radial lift
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

    // Hairline phi(|theta|): curved male hairline, receding temples, sideburn down to the ear top
    const hairKnots: Array<[number, number]> = [[0, THREE.MathUtils.lerp(1.42, 1.48, config.bangsLength)], [0.2, 1.37], [0.4, 1.31], [0.62, 1.30], [0.9, 1.06], [1.15, 1.16], [1.4, 1.32], [1.6, 1.50]];
    const hairPhi = (th: number): number => {
      const a = Math.abs(Math.atan2(Math.sin(th), Math.cos(th)));
      if (a >= hairKnots[hairKnots.length - 1][0]) return 1.6;
      for (let k = 1; k < hairKnots.length; k++) {
        if (a <= hairKnots[k][0]) {
          const f = (a - hairKnots[k - 1][0]) / (hairKnots[k][0] - hairKnots[k - 1][0]);
          return THREE.MathUtils.lerp(hairKnots[k - 1][1], hairKnots[k][1], f);
        }
      }
      return 1.6;
    };

    // Cut line end phi by |theta|: sideburn over the ear top, arch behind the ear, curved nape (lowest at centre)
    const cropEndPhi = (th: number, napeEnd: number): number =>
      earAlignedCutPhi(th, { hairlinePhi: (x: number) => hairPhi(x) + 0.05, templeRecede: 0, sideburnLength: THREE.MathUtils.lerp(1.1, 1.3, config.sideLength), sideburnTaper: 0.30, earClearance: 0.05, napePhi: napeEnd + 0.02, napeCurve: 1.3 });

    // Zone 1: visible short dome fill (normal locks, solid for the first 58%, staggered tiers so there is no bare scalp)
    for (let tier = 0; tier < 8; tier++) {
      const cnt = tier === 0 ? 0 : 48;
      for (let i = 0; i < cnt; i++) {
        const theta = (i / cnt) * Math.PI * 2 + tier * 0.21 + (rZ1() - 0.5) * 0.03;
        const cosT = Math.cos(theta);
        const lim = cosT > 0.5 ? hairPhi(theta) : cosT > 0.0 ? THREE.MathUtils.lerp(0.82, hairPhi(theta), Math.pow(cosT / 0.5, 1.5)) : THREE.MathUtils.lerp(0.78, 0.92, THREE.MathUtils.smoothstep(-cosT, 0.0, 0.6));
        const phiStart = tier === 0 ? 0.01 : 0.12 + tier * 0.19;
        if (phiStart >= lim - 0.12) continue;
        const pts = buildSculptedLock({ thetaStart: theta, thetaEnd: theta, phiStart, phiEnd: Math.min(lim - 0.02 - 0.07 * THREE.MathUtils.smoothstep(cosT, 0.2, 0.6), phiStart + 0.62), baseLift: 0.0025, midArch: 0.000, tipFlickOut: 0.0, napeHug: 1.4 });
        specs.push({ pointsLocal: pts, width: 0.034 * wMul, thickness: 0.005, stiffnessBias: 0.99, layerAO: 0.97, layerOrder: 0, lockNormal: false, scalpRoot: 0.5 });
      }
    }

    // Zone 1b: front hairline edge (narrow fade-cap band only at the very edge)
    for (let i = 0; i < Math.round(420 * density); i++) {
      const theta = (i / Math.round(420 * density)) * Math.PI * 2;
      if (Math.cos(theta) < 0.3) continue;
      const hp = hairPhi(theta);
      const pts = buildSculptedLock({ thetaStart: theta, thetaEnd: theta, phiStart: hp - 0.16, phiEnd: hp + 0.04, baseLift: 0.0035, midArch: 0.000, tipFlickOut: 0.0, napeHug: 1.2 });
      specs.push({ pointsLocal: pts, width: 0.04 * wMul, thickness: 0.004, stiffnessBias: 0.99, layerAO: 0.97, layerOrder: 1, isFadeCap: true, fadeStart: 0.15, lockNormal: false, scalpRoot: 0.4 });
    }

    // Zone 2: Skull-hugging stubble fade cap (isFadeCap: flat overlapping ribbons + shader stipple fade)
    const fadeCols = Math.round(170 * density);
    for (let i = 0; i < fadeCols; i++) {
      const theta = (i / fadeCols) * Math.PI * 2 + (rZ2() - 0.5) * 0.02;
      const cosT = Math.cos(theta);
      if (cosT > 0.72) continue; // forehead stays bare below curved hairline
      const isBack = cosT < -0.25;
      const backW = THREE.MathUtils.smoothstep(-cosT, 0.0, 0.7);
      const phiStart = THREE.MathUtils.lerp(0.78, 0.82, backW);
      const phiEnd = cropEndPhi(theta, napeEndBase);
      const a2 = Math.abs(Math.atan2(Math.sin(theta), Math.cos(theta)));
      const pts = buildSculptedLock({ thetaStart: theta, thetaEnd: theta, phiStart, phiEnd, baseLift: 0.0018, midArch: 0.000, tipFlickOut: 0.0, napeHug: 1.55 });
      // taper the fade toward the skull so the nape and sides do not flare into a helmet
      for (let n = 0; n < pts.length; n++) {
        const f = 0.26 * Math.pow(n / (pts.length - 1), 1.5);
        pts[n].x = HEAD_CENTER_LOCAL.x + (pts[n].x - HEAD_CENTER_LOCAL.x) * (1 - f);
        pts[n].z = HEAD_CENTER_LOCAL.z + (pts[n].z - HEAD_CENTER_LOCAL.z) * (1 - f);
      }
      specs.push({ pointsLocal: pts, width: 0.05 * wMul * (a2 > 1.5 && a2 < 1.68 ? 0.6 : 1), thickness: 0.0035, stiffnessBias: 0.99, layerAO: 0.86, layerOrder: 1, isFadeCap: true, fadeStart: THREE.MathUtils.clamp(1 - 0.2 / Math.max(0.4, phiEnd - phiStart), 0.55, 0.88), lockNormal: false, scalpRoot: 0.4 });
    }

    // front hairline in dome (x, z): z of the cut line at |x| (so the dense top ends on the soft M hairline, not a flat bar)
    const hairTab: Array<[number, number]> = [];
    for (let th = 0; th <= 1.5; th += 0.04) {
      const hp = hairPhi(th);
      hairTab.push([Math.sin(hp) * Math.sin(th) * SCALP_RX, Math.sin(hp) * Math.cos(th) * SCALP_RZ]);
    }
    const hairZ = (ax: number): number => {
      let best = hairTab[hairTab.length - 1][1];
      for (let k = 1; k < hairTab.length; k++) {
        if (ax <= hairTab[k][0]) {
          const f = (ax - hairTab[k - 1][0]) / Math.max(1e-6, hairTab[k][0] - hairTab[k - 1][0]);
          best = THREE.MathUtils.lerp(hairTab[k - 1][1], hairTab[k][1], THREE.MathUtils.clamp(f, 0, 1));
          break;
        }
      }
      return best;
    };

    // Zone 3: dense short top, many narrow locks lying forward-up in one direction
    {
      const dx = 0.008 / Math.sqrt(density), dz = 0.011 / Math.sqrt(density);
      for (let zc = -0.30; zc <= 0.26; zc += dz) {
        for (let xc = -0.24; xc <= 0.24; xc += dx) {
          const x0 = xc + (rZ3() - 0.5) * dx * 0.9;
          const z0 = zc + (rZ3() - 0.5) * dz * 0.9;
          const rr = (x0 / 0.25) * (x0 / 0.25) + ((z0 + 0.0) / 0.30) * ((z0 + 0.0) / 0.30);
          if (rr > 1.0) continue;
          const zFrac = THREE.MathUtils.clamp((z0 + 0.27) / 0.53, 0, 1);
          const crownR = Math.sqrt(x0 * x0 + z0 * z0);
          const crownK = crownR < 0.13 ? 0.72 + 0.28 * crownR / 0.13 : 1;
          const len = (0.10 - 0.03 * zFrac + config.bangsLength * 0.03 * zFrac) * (0.7 + rZ3() * 0.6) * (1 - 0.3 * Math.pow(zFrac, 1.5)) * crownK;
          const zEnd = z0 + len;
          const zCap = Math.min(0.262 - 0.06 * Math.pow(Math.abs(x0) / 0.25, 2), hairZ(Math.abs(x0))) - rZ3() * 0.022;
          const lenEff = Math.min(len, Math.max(0.05, zCap - z0));
          if (z0 > zCap - 0.01) continue;
          const jx = (rZ3() - 0.5) * 0.03;
          const upQuiff = (0.006 + config.crownVolume * 0.014) * (0.4 + 0.6 * zFrac);
          const pts: THREE.Vector3[] = [];
          for (let n = 0; n < NODES_PER_LOCK; n++) {
            const t = n / (NODES_PER_LOCK - 1);
            const x = x0 + (sweepX * 0.5 + jx) * t;
            const z = z0 + lenEff * t;
            const lift = 0.0016 + 0.0059 * THREE.MathUtils.clamp(zFrac * 2.2, 0, 1) + (0.0012 + upQuiff * 0.6) * Math.pow(t, 0.9);
            pts.push(domePt(x, z, lift));
          }
          void zEnd;
          specs.push({ pointsLocal: pts, width: (0.012 + rZ3() * 0.006) * wMul, thickness: 0.006, stiffnessBias: 0.97, layerAO: 0.985 + rZ3() * 0.015, layerOrder: 3, lockNormal: false, scalpRoot: 0.45 });
        }
      }
    }
    return specs;
  }
