  if (arch === 'wavy-bard') {
    const partTheta = THREE.MathUtils.clamp(config.partPosition, -1, 1) * 0.42;
    const backEnd = THREE.MathUtils.lerp(3.1, 4.0, config.backLength);
    const sideEnd = THREE.MathUtils.lerp(3.0, 3.8, config.sideLength);
    const TAU = Math.PI * 2;
    const wrap = (a: number) => { let x = a % TAU; if (x > Math.PI) x -= TAU; if (x < -Math.PI) x += TAU; return x; };
    const eqY = HEAD_CENTER_LOCAL.y + Math.cos(1.75) * SCALP_RY;
    // coherent wave: phase is a smooth function of theta and rank, so neighbours form S bundles
    const wavify = (pts: THREE.Vector3[], th: number, amp: number, phase: number, layerOut: number, clear: number, curl: number) => {
      const tx = Math.cos(th), tz = -Math.sin(th), ox = Math.sin(th), oz = Math.cos(th);
      const last = pts.length - 1;
      for (let n = 2; n <= last; n++) {
        const t = n / last;
        const w = (Math.sin(t * TAU * 1.8 + phase) * amp + Math.sin(t * TAU * 3.1 + phase * 1.9) * amp * 0.18) * Math.pow(t, 0.7);
        const dd = Math.max(0, eqY - pts[n].y - 0.24);
        const c = n >= last - 3 ? Math.pow((n - (last - 3)) / 3, 2) : 0;
        const out = layerOut * Math.min(1, t * 2.2) + clear * 0.3 * (1 - Math.exp(-dd * 4.2)) + c * 0.02;
        pts[n].x += tx * (w + curl * c * 0.035) + ox * out;
        pts[n].z += tz * (w + curl * c * 0.05) + oz * out;
      }
    };
    const rShell = seededRand((config.seed || 1) + 11);
    const rMid = seededRand((config.seed || 1) + 23);
    const rFr = seededRand((config.seed || 1) + 51);
    void rShell;

    // Zone 1: inner shell, seals the scalp
    for (let tier = 0; tier < 3; tier++) {
      const count = 22;
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * TAU + tier * 0.21;
        const cosT = Math.cos(theta);
        if (tier >= 1 && cosT > 0.3) continue;
        const isBack = cosT < -0.15;
        const phiStart = tier === 0 ? 0.02 : 0.30 + tier * 0.42;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart,
          phiEnd: Math.min(isBack ? backEnd - 0.5 : sideEnd - 0.6, phiStart + 1.45, cosT > 0.3 ? 1.2 : 9),
          baseLift: 0.008, midArch: 0.010, tipFlickOut: 0.006, napeHug: 0.8,
        });
        specs.push({
          pointsLocal: pts, width: 0.09 * wMul, thickness: 0.012,
          stiffnessBias: 0.8, layerAO: 0.7, layerOrder: 0, isBaseShell: true,
          lockNormal: false, scalpRoot: tier === 0 ? 0.45 : 0.22,
        });
      }
    }

    // Zone 1b: hairline fringe along the part (smooth lift, swept from the part)
    for (let i = 0; i < 44; i++) {
      const th = -0.95 + (i / 43) * 1.9 + (rFr() - 0.5) * 0.03;
      const side = th - partTheta >= 0 ? 1 : -1;
      const near = Math.abs(th - partTheta) < 0.25;
      const pts = buildSculptedLock({
        thetaStart: th, thetaEnd: th + side * (near ? 0.1 : 0.32 + rFr() * 0.2),
        phiStart: 0.12 + rFr() * 0.1, phiEnd: 1.5 + rFr() * 0.12 + (near ? 0.12 : 0),
        baseLift: 0.022, midArch: 0.016, tipFlickOut: 0.006, napeHug: 0.8,
      });
      specs.push({
        pointsLocal: pts, width: 0.085 * wMul, thickness: 0.012,
        stiffnessBias: 0.7, layerAO: 0.9, layerOrder: 2, lockNormal: false, scalpRoot: 0.4,
      });
    }

    // Zone 2: three depth layers of wide overlapping wavy locks
    const layerCount = [56, 76, 92];
    const layerOut = [0.0, 0.04, 0.085];
    for (let layer = 0; layer < 3; layer++) {
      const count = Math.round(layerCount[layer] * density / 1.15);
      for (let i = 0; i < count; i++) {
        const th = wrap((i / count) * TAU + layer * 0.06 + (rMid() - 0.5) * 0.05);
        const cosT = Math.cos(th);
        const isFront = cosT > 0.35;
        const isBack = cosT < -0.2;
        const side = th - partTheta >= 0 ? 1 : -1;
        const phiStart = isFront ? 0.30 + rMid() * 0.25 : 0.10 + layer * 0.10 + rMid() * 0.14;
        const step = Math.floor(rMid() * 4) / 3; // 4 length steps: rounded flowing ends
        let thEnd = th;
        let phiEnd: number;
        if (isFront) {
          // face-framing waves: front locks fall in front of the ear; others swept behind it
          const framing = Math.abs(th) > 0.6 && Math.abs(th) < 1.2 && layer >= 1 && i % 5 === 0;
          thEnd = side * (framing ? 1.45 + rMid() * 0.1 : 2.05 + rMid() * 0.3);
          phiEnd = (framing ? 3.4 : THREE.MathUtils.lerp(2.9, 3.5, config.bangsLength)) - step * 0.25;
        } else if (!isBack) {
          const s = th >= 0 ? 1 : -1;
          thEnd = Math.abs(th) < 2.05 ? s * (2.1 + rMid() * 0.3) : th;
          phiEnd = sideEnd - step * 0.28;
        } else {
          thEnd = th + (rMid() - 0.5) * 0.1;
          phiEnd = backEnd - step * 0.32;
        }
        const phase = TAU * (th * 0.55 + layer * 0.12) + step * 0.6;
        const pts = buildSculptedLock({
          thetaStart: th, thetaEnd: thEnd, phiStart, phiEnd,
          baseLift: 0.016 + layer * 0.006 + config.crownVolume * 0.010,
          midArch: 0.016 + layer * 0.008, tipFlickOut: 0.012 + layer * 0.006,
          napeHug: 0.7,
        });
        wavify(pts, thEnd, 0.105 + layer * 0.012, phase, layerOut[layer], 1.0, Math.sin(th * 5 + layer) >= 0 ? 1 : -1);
        specs.push({
          pointsLocal: pts,
          width: (0.11 + layer * 0.025 + rMid() * 0.03) * wMul,
          thickness: 0.014,
          stiffnessBias: 0.7 - layer * 0.06, layerAO: 0.8 + layer * 0.07, layerOrder: 1 + layer,
          lockNormal: layer >= 1, scalpRoot: 0.3 + 0.03 * (2 - layer),
        });
      }
    }
    return specs;
  }
