  if (arch === 'noble-hime') {
    // NH-BEGIN noble-hime (long straight sleek, skull-hugging crown, blunt flat fringe, hime panels)
    // Visible hair = many narrow round locks layered inner/mid/outer with 2-4 mm radial offsets.
    const backEnd = THREE.MathUtils.lerp(3.6, 4.6, config.backLength);
    const himeEnd = THREE.MathUtils.lerp(2.35, 2.55, config.sideLength);
    const bluntBrowPhi = THREE.MathUtils.lerp(1.7, 1.9, config.bangsLength);
    const nhRand = seededRand(config.seed * 7 + 11);
    const nhEqY = HEAD_CENTER_LOCAL.y + Math.cos(1.75) * SCALP_RY;
    // push every node outward from the head (horizontal below the equator) so layers stack with parallax
    const radialPush = (pts: THREE.Vector3[], dr: number) => {
      for (let n = 1; n < pts.length; n++) {
        const p = pts[n];
        const dx = p.x - HEAD_CENTER_LOCAL.x, dz = p.z - HEAD_CENTER_LOCAL.z;
        const below = THREE.MathUtils.clamp((nhEqY - p.y) / 0.25, 0, 1);
        const dy = (p.y - HEAD_CENTER_LOCAL.y) * (1 - below);
        const len = Math.max(1e-4, Math.sqrt(dx * dx + dy * dy + dz * dz));
        const k = dr * Math.min(1, n / 3) / len;
        p.x += dx * k; p.y += dy * k; p.z += dz * k;
      }
    };
    // lift the hair clear of the shoulders: below chin depth push outward so it drapes over the body, not into it
    const drape = (pts: THREE.Vector3[], amt: number) => {
      for (let n = 1; n < pts.length; n++) {
        const p = pts[n];
        const dx = p.x - HEAD_CENTER_LOCAL.x, dz = p.z - HEAD_CENTER_LOCAL.z;
        const len = Math.max(1e-4, Math.sqrt(dx * dx + dz * dz));
        const d = THREE.MathUtils.clamp((nhEqY - p.y - 0.28) / 0.5, 0, 1);
        const k = amt * d * d * (3 - 2 * d) / len;
        p.x += dx * k * 0.5; p.z += dz * k;
      }
    };
    // round strand-flow wobble: tiny coherent sideways drift so strands read as individual flow, no hard angles
    const drift = (pts: THREE.Vector3[], th: number, amp: number, ph: number) => {
      const tx = Math.cos(th), tz = -Math.sin(th), last = pts.length - 1;
      for (let n = 3; n <= last; n++) {
        const t = n / last;
        const w = Math.sin(t * Math.PI * 1.2 + ph) * amp * t;
        pts[n].x += tx * w; pts[n].z += tz * w;
      }
    };

    // Zone 1: thin skull-hugging base shell (hidden fill)
    for (let tier = 0; tier < 2; tier++) {
      const count = 24;
      for (let i = 0; i < count; i++) {
        const theta = ((i + 0.5) / count) * Math.PI * 2 + tier * 0.13;
        const aT = Math.abs(Math.atan2(Math.sin(theta), Math.cos(theta)));
        const earGap = aT > 1.15 && aT < 1.5;
        if (tier >= 1 && (Math.cos(theta) > 0.6 || earGap)) continue;
        const phiStart = tier === 0 ? 0.12 : 0.5;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart,
          phiEnd: Math.min(backEnd - 0.10, phiStart + 1.6, Math.cos(theta) > 0.3 ? 1.25 : earGap ? 1.35 : 9),
          baseLift: 0.002, midArch: 0.002, tipFlickOut: 0.0, napeHug: 0.9,
        });
        specs.push({
          smooth: 3,
          pointsLocal: pts, width: 0.09 * wMul, thickness: 0.01, stiffnessBias: 0.8, layerAO: 0.9,
          layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: tier === 0 ? 0.5 : 0.3,
        });
      }
    }

    // Zone 2: back + rear-side curtain, behind the ears; 3 depth layers, V-ish clean hem
    const earBack = 1.98;
    const layers = [
      { n: 60, w: 0.06, dr: 0.004, ord: 1, round: 0.5 },   // hidden under-fill
      { n: 100, w: 0.04, dr: 0.011, ord: 2, round: 0.65 },  // mid
      { n: 120, w: 0.036, dr: 0.019, ord: 3, round: 0.7 },   // outer
    ];
    for (const L of layers) {
      const cnt = Math.round(L.n * density);
      for (let i = 0; i < cnt; i++) {
        const u = (i + (nhRand() - 0.5) * 0.3) / (cnt - 1);
        const theta = THREE.MathUtils.lerp(earBack, Math.PI * 2 - earBack, THREE.MathUtils.clamp(u, 0, 1));
        const dV = Math.abs(theta - Math.PI) / (Math.PI - earBack);
        const hemEnd = backEnd - 0.2 * Math.pow(dV, 1.4) + (nhRand() - 0.5) * 0.012 - L.ord * 0.012;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta + (nhRand() - 0.5) * 0.02,
          phiStart: 0.12 + L.ord * 0.08 + nhRand() * 0.05, phiEnd: hemEnd,
          baseLift: 0.004 + L.ord * 0.002, midArch: 0.002, tipFlickOut: 0.0, napeHug: 0.35,
        });
        drift(pts, theta, 0.012, nhRand() * 6.28);
        radialPush(pts, L.dr);
        drape(pts, 0.34 + L.ord * 0.012);
        specs.push({
          smooth: 3, roundness: L.round,
          pointsLocal: pts, width: L.w * wMul * (0.85 + nhRand() * 0.3), thickness: 0.011,
          stiffnessBias: 0.8, layerAO: 0.82 + L.ord * 0.04, layerOrder: L.ord, lockNormal: false, scalpRoot: 0.4,
        });
      }
    }

    // Zone 2b: side-crown strands sweeping over and behind the ear (ear tucked, peeking)
    for (const side of [-1, 1]) {
      for (let k = 0; k < 14; k++) {
        const s0 = 1.0 + (k / 13) * 0.5;
        const pts = buildSculptedLock({
          thetaStart: side * s0, thetaEnd: side * (2.4 + (s0 - 1.0) * 0.4),
          phiStart: 0.1 + nhRand() * 0.1, phiEnd: 2.9 - (k % 3) * 0.08 - nhRand() * 0.06,
          baseLift: 0.012, midArch: 0.003, tipFlickOut: 0.0, napeHug: 0.9,
        });
        radialPush(pts, 0.014 + (k % 2) * 0.006);
        drape(pts, 0.3);
        specs.push({
          smooth: 3, roundness: 0.7,
          pointsLocal: pts, width: 0.034 * wMul, thickness: 0.011,
          stiffnessBias: 0.8, layerAO: 0.9, layerOrder: 3 + (k % 2), lockNormal: false, scalpRoot: 0.3,
        });
      }
    }

    // Zone 3: hime panels (jaw length), two layers of straight parallel narrow locks starting behind the fringe
    for (const side of [-1, 1]) {
      for (let layer = 0; layer < 2; layer++) {
        const n = layer === 0 ? 12 : 13;
        for (let k = 0; k < n; k++) {
          const f = k / (n - 1);
          const theta = side * (0.66 + f * 0.78 + layer * 0.03);
          const pts = buildSculptedLock({
            thetaStart: theta * 0.96, thetaEnd: theta,
            phiStart: 0.3 + f * 0.05, phiEnd: himeEnd - f * f * 0.16 - layer * 0.03,
            baseLift: 0.008 + layer * 0.002, midArch: 0.002, tipFlickOut: 0.006, napeHug: 1.0,
          });
          radialPush(pts, 0.006 + layer * 0.012);
          specs.push({
            smooth: 3, roundness: 0.65,
            pointsLocal: pts, width: (layer === 0 ? 0.06 : 0.04) * wMul, thickness: 0.011,
            stiffnessBias: 0.85, layerAO: 0.88, layerOrder: 4 + layer, lockNormal: false, scalpRoot: 0.34,
            ...(layer === 0 ? { isFadeCap: true, fadeStart: 0.97 } : {}),
          });
        }
      }
    }

    // Zone 4: blunt fringe lying flat on the forehead, temple to temple, seam at the centre, equal-length rounded locks
    for (let layer = 0; layer < 2; layer++) {
      const bangCount = Math.round((layer === 0 ? 34 : 56) * density);
      for (let i = 0; i < bangCount; i++) {
        const frac = (i / (bangCount - 1)) * 2 - 1;
        if (Math.abs(frac) < 0.025) continue;
        const theta = frac * 0.6;
        const pts = buildSculptedLock({
          thetaStart: theta * 0.5, thetaEnd: theta,
          phiStart: 0.16 + Math.abs(frac) * 0.05, phiEnd: bluntBrowPhi - frac * frac * 0.2 - layer * 0.03 - (nhRand() - 0.5) * 0.01 * layer,
          baseLift: 0.003 + layer * 0.004, midArch: 0.0, tipFlickOut: 0.0, napeHug: 1.0,
        });
        specs.push({
          smooth: 3, roundness: 0.6,
          pointsLocal: pts, width: (layer === 0 ? 0.05 : 0.032) * wMul, thickness: 0.011,
          stiffnessBias: 0.9, layerAO: 0.9, layerOrder: 6 + layer, lockNormal: false, scalpRoot: 0.36,
          ...(layer === 0 ? { isFadeCap: true, fadeStart: 0.97 } : {}),
        });
      }
    }

    return specs;
    // NH-END
  }
