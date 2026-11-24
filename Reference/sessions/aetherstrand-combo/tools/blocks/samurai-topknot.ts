  if (arch === 'samurai-topknot') {
    const V3 = THREE.Vector3;
    const knotTheta = Math.PI;
    const tkStart = specs.length;
    const KX = 0.72, KZ = 0.8;
    const knotPhi = 0.3;
    const knotAnchor = skullSurfacePoint(knotTheta, knotPhi, 0.012);
    // hairline: high forehead, receding temples, short sideburn in front of the ear, tapered nape
    const frontKnots: Array<[number, number]> = [[0, 1.24], [0.22, 1.14], [0.55, 1.12], [0.9, 1.26], [1.15, 1.40], [1.3, 1.50]];
    const frontHair = (a: number): number => {
      for (let k = 1; k < frontKnots.length; k++) {
        if (a <= frontKnots[k][0]) {
          const u = (a - frontKnots[k - 1][0]) / (frontKnots[k][0] - frontKnots[k - 1][0]);
          return THREE.MathUtils.lerp(frontKnots[k - 1][1], frontKnots[k][1], u * u * (3 - 2 * u));
        }
      }
      return frontKnots[frontKnots.length - 1][1];
    };
    const cutPhi = (th: number): number => {
      const v = earAlignedCutPhi(th, { hairlinePhi: frontHair, templeRecede: 0, sideburnLength: 0.5, sideburnGap: 0.12, sideburnTaper: 0.2, earClearance: 0.12, behindEarPhi: 1.95, napePhi: 2.5, napeCurve: 0.8 });
      const a = Math.abs(Math.atan2(Math.sin(th), Math.cos(th)));
      // hair comes straight down right behind the ear (no bald patch between the ear and the back hair)
      const w = THREE.MathUtils.smoothstep(a, 1.9, 1.98);
      const tgt = THREE.MathUtils.lerp(2.05, 2.52, Math.pow(Math.max(0, a - 2.0) / (Math.PI - 2.0), 0.95)); // straight V down to the nape tip
      return a > 1.9 ? Math.max(v, THREE.MathUtils.lerp(v, tgt, w)) : v;
    };

    // Zone 1: scalp cover under the pulled strands (snug, no scalp showing)
    for (let tier = 0; tier < 2; tier++) {
      const count = 26;
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + tier * 0.12;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart: 0.02 + tier * 0.4, phiEnd: Math.min(0.85 + tier * 0.3, cutPhi(theta) - 0.15),
          baseLift: 0.001, midArch: 0.001, tipFlickOut: 0.0, napeHug: 1.35,
        });
        specs.push({
          pointsLocal: pts, width: 0.07 * wMul, thickness: 0.006, stiffnessBias: 0.98, layerAO: 0.8,
          layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4,
        });
      }
    }

    // meridian cover from the temple down to the cut line: great-circle strands toward the knot leave a triangle behind the ear, this fills it
    for (let i = 0; i < 64; i++) {
      const theta = (i / 64) * Math.PI * 2 + 0.05;
      const endP = cutPhi(theta) - 0.1;
      if (endP < 1.2) continue;
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: theta, phiStart: 0.95, phiEnd: endP,
        baseLift: 0.001, midArch: 0.001, tipFlickOut: 0.0, napeHug: 1.35,
      });
      specs.push({
        pointsLocal: pts, width: 0.05 * wMul, thickness: 0.006, stiffnessBias: 0.98, layerAO: 0.8,
        layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4,
      });
    }

    // Zone 2: strands rooted on the cut line, combed back to the knot along the dome.
    const knotDir = new V3(Math.sin(knotTheta) * Math.sin(knotPhi), Math.cos(knotPhi), Math.cos(knotTheta) * Math.sin(knotPhi));
    const pullStrand = (theta: number, phi0: number, width: number, order: number, spread: number) => {
      const u0 = new V3(Math.sin(theta) * Math.sin(phi0), Math.cos(phi0), Math.cos(theta) * Math.sin(phi0));
      const end = knotDir.clone();
      end.x += (rand() - 0.5) * spread; end.z += (rand() - 0.5) * spread; end.normalize();
      const omega = Math.acos(THREE.MathUtils.clamp(u0.dot(end), -1, 1));
      const so = Math.max(Math.sin(omega), 1e-4);
      const lift0 = 0.0002 + rand() * 0.0006 + order * 0.0003;
      const pts: THREE.Vector3[] = [];
      for (let k = 0; k < NODES_PER_LOCK; k++) {
        const t = Math.pow(k / (NODES_PER_LOCK - 1), 1.5);
        const w0 = Math.sin((1 - t) * omega) / so;
        const w1 = Math.sin(t * omega) / so;
        const u = u0.clone().multiplyScalar(w0).addScaledVector(end, w1);
        const ph = Math.acos(THREE.MathUtils.clamp(u.y, -1, 1));
        const th = Math.atan2(u.x, u.z);
        pts.push(skullSurfacePoint(th, ph, lift0 + Math.sin(t * Math.PI) * 0.001));
      }
      specs.push({
        pointsLocal: pts, width, thickness: 0.005, stiffnessBias: 0.97,
        layerAO: 0.88, layerOrder: order, lockNormal: false, scalpRoot: 0.35, smooth: 3,
      });
    };
    const pullCount = Math.round(540 * density);
    for (let i = 0; i < pullCount; i++) {
      const theta = (i / pullCount) * Math.PI * 2 + (rand() - 0.5) * 0.01;
      pullStrand(theta, cutPhi(theta) - 0.02 + (rand() - 0.5) * 0.006, (0.026 + rand() * 0.008) * wMul, 0, 0.1);
    }
    for (let i = 0; i < pullCount; i++) {
      const theta = ((i + 0.5) / pullCount) * Math.PI * 2;
      pullStrand(theta, cutPhi(theta) - 0.07 - rand() * 0.03, (0.026 + rand() * 0.008) * wMul, 1, 0.1);
    }

    // Zone 2b: soft hairline + tapered nape: isFadeCap stubble ribbons that dissolve into the skin
    for (let row = 0; row < 1; row++) {
      const cols = Math.round(190 * density);
      for (let i = 0; i < cols; i++) {
        const theta = (i / cols) * Math.PI * 2 + row * 0.5 * (Math.PI * 2 / cols) + (rand() - 0.5) * 0.01;
        const cosT = Math.cos(theta);
        if (Math.abs(Math.atan2(Math.sin(theta), cosT)) < 1.75) continue; // crisp front hairline: no stubble band
        const nz = Math.max(0, Math.abs(Math.atan2(Math.sin(theta), cosT)) - 2.2) / 0.94; // 0 behind the ear .. 1 at the nape centre
        const lim = cutPhi(theta) + 0.05 + row * 0.025 + 0.1 * nz;
        const top = lim - 0.22 - 0.03 * nz;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart: top, phiEnd: lim,
          baseLift: 0.0003, midArch: 0.0, tipFlickOut: 0.0, napeHug: 1.55,
        });
        specs.push({
          pointsLocal: pts, width: 0.03 * wMul, thickness: 0.003, stiffnessBias: 0.99,
          layerAO: 0.92, layerOrder: 1, isFadeCap: true, fadeStart: 0.3, lockNormal: false, scalpRoot: 0.35,
        });
      }
    }
    // snug the dome: the scalp ellipsoid is wider than the real cranium, pull the cover toward the head axis
    for (let si = tkStart; si < specs.length; si++) {
      for (const p of specs[si].pointsLocal) {
        const lowT = THREE.MathUtils.clamp((HEAD_CENTER_LOCAL.y - p.y) / 0.1, 0, 1);
        const nT = THREE.MathUtils.clamp((HEAD_CENTER_LOCAL.y - 0.1 - p.y) / 0.15, 0, 1); // nape tuck: the low back hair hugs the narrower neck
        p.x = HEAD_CENTER_LOCAL.x + (p.x - HEAD_CENTER_LOCAL.x) * (KX - 0.12 * lowT - 0.05 * nT);
        p.z = HEAD_CENTER_LOCAL.z + (p.z - HEAD_CENTER_LOCAL.z) * (KZ - 0.08 * lowT - 0.1 * nT);
      }
    }

    // soft sideburn edge: fine thin strands along the steep temple cut so the steps blend
    for (let sd = -1; sd <= 1; sd += 2) {
      for (let i = 0; i < 70; i++) {
        const theta = sd * (0.85 + (i / 69) * 0.8);
        pullStrand(theta, cutPhi(theta) - 0.01, 0.014 * wMul, 0, 0.1);
      }
    }
    // second cap tier around the knot base (scalp pinholes)
    for (let i = 0; i < 40; i++) {
      const theta = (i / 40) * Math.PI * 2 + 0.07;
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: theta, phiStart: 0.45, phiEnd: 1.2, baseLift: 0.005, midArch: 0.001, tipFlickOut: 0.0, napeHug: 1.35,
      });
      for (const p of pts) {
        p.x = HEAD_CENTER_LOCAL.x + (p.x - HEAD_CENTER_LOCAL.x) * KX;
        p.z = HEAD_CENTER_LOCAL.z + (p.z - HEAD_CENTER_LOCAL.z) * KZ;
      }
      specs.push({
        pointsLocal: pts, width: 0.07 * wMul, thickness: 0.006, stiffnessBias: 0.98, layerAO: 0.8,
        layerOrder: 1, lockNormal: false, scalpRoot: 0.4,
      });
    }
    // crown cap: radial cover strands around the knot so no scalp pinholes show where the pulled strands converge
    for (let i = 0; i < 28; i++) {
      const theta = (i / 28) * Math.PI * 2;
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: theta, phiStart: 0.3, phiEnd: 0.95, baseLift: 0.004, midArch: 0.001, tipFlickOut: 0.0, napeHug: 1.35,
      });
      for (const p of pts) {
        p.x = HEAD_CENTER_LOCAL.x + (p.x - HEAD_CENTER_LOCAL.x) * KX;
        p.z = HEAD_CENTER_LOCAL.z + (p.z - HEAD_CENTER_LOCAL.z) * KZ;
      }
      specs.push({
        pointsLocal: pts, width: 0.06 * wMul, thickness: 0.006, stiffnessBias: 0.98, layerAO: 0.8,
        layerOrder: 1, lockNormal: false, scalpRoot: 0.4,
      });
    }

    // Zone 3A: topknot bun as a coil: helical strands wound round a short vertical axis, swirling to a top whorl
    const bunR = 0.13;
    const bunC = knotAnchor.clone();
    bunC.z = HEAD_CENTER_LOCAL.z + (bunC.z - HEAD_CENTER_LOCAL.z) * KZ; bunC.x = HEAD_CENTER_LOCAL.x + (bunC.x - HEAD_CENTER_LOCAL.x) * KX; bunC.y += bunR * 0.2; bunC.z -= bunR * 0.25;
    const tilt = 0.45; // bun leans back: top shifts toward -z
    const coilCount = Math.round(54 * density);
    for (let i = 0; i < coilCount; i++) {
      const a0 = (i / coilCount) * Math.PI * 2;
      const pts: THREE.Vector3[] = [];
      const turns = 0.9 + rand() * 0.25;
      for (let k = 0; k < NODES_PER_LOCK; k++) {
        const t = k / (NODES_PER_LOCK - 1);
        const hh = THREE.MathUtils.lerp(-0.72, 0.9, Math.sin(t * Math.PI * 0.5)); // height fraction along the bun
        const prof = Math.sqrt(Math.max(0.2, 1 - hh * hh)) * (1 - 0.48 * Math.exp(-Math.pow((hh + 0.62) / 0.12, 2))) * (hh < -0.4 ? 0.9 : 1.0);
        const rr = bunR * (prof * 1.02 + 0.004);
        const a = a0 + turns * t * Math.PI * 1.2;
        pts.push(new V3(
          bunC.x + rr * Math.cos(a),
          bunC.y + bunR * 0.92 * hh,
          bunC.z + rr * Math.sin(a) * 1.1 - tilt * bunR * hh
        ));
      }
      specs.push({
        pointsLocal: pts, width: 0.04 * wMul, thickness: 0.01, stiffnessBias: 0.95, layerAO: 0.86,
        layerOrder: 2, lockNormal: false, scalpRoot: 0.5,
      });
    }

    // Tie band: three wound rings pinching the bun neck, proud of the surface, plus two hanging ends
    const tieR = (hh: number) => bunR * (Math.sqrt(Math.max(0.2, 1 - hh * hh)) * (1 - 0.38 * Math.exp(-Math.pow((hh + 0.62) / 0.12, 2))) * 1.02 + 0.004) * 1.2 + 0.016;
    for (let ring = 0; ring < 4; ring++) {
      const hh = -0.5 - ring * 0.08;
      const ty = bunC.y + bunR * hh;
      const rad = tieR(hh);
      for (let j = 0; j < 4; j++) {
        const a0 = (j / 4) * Math.PI * 2 + ring * 0.7;
        const pts: THREE.Vector3[] = [];
        for (let k = 0; k < NODES_PER_LOCK; k++) {
          const t = k / (NODES_PER_LOCK - 1);
          const a = a0 + t * Math.PI * 0.9;
          pts.push(new V3(bunC.x + Math.cos(a) * rad, ty + (t - 0.5) * 0.004, bunC.z + Math.sin(a) * rad * 1.1 - tilt * bunR * hh));
        }
        specs.push({
          pointsLocal: pts, width: 0.04 * wMul, thickness: 0.03, stiffnessBias: 0.99, layerAO: 0.12,
          layerOrder: 3, lockNormal: false, scalpRoot: 0.5,
        });
      }
    }
    // hanging tie ends: two short ribbons draped over the back of the dome from the knot (hug the squeezed skull)
    for (let e = 0; e < 2; e++) {
      const side = e === 0 ? -1 : 1;
      for (let j = 0; j < 3; j++) {
        const pts: THREE.Vector3[] = [];
        for (let k = 0; k < NODES_PER_LOCK; k++) {
          const t = k / (NODES_PER_LOCK - 1);
          const th = Math.PI + side * (0.04 + 0.1 * t) + (j - 1) * 0.02;
          const ph = 0.72 + 0.55 * t;
          const q = skullSurfacePoint(th, ph, 0.002);
          const lowT = THREE.MathUtils.clamp((HEAD_CENTER_LOCAL.y - q.y) / 0.08, 0, 1);
          q.x = HEAD_CENTER_LOCAL.x + (q.x - HEAD_CENTER_LOCAL.x) * (KX - 0.08 * lowT);
          q.z = HEAD_CENTER_LOCAL.z + (q.z - HEAD_CENTER_LOCAL.z) * (KZ - 0.06 * lowT);
          q.addScaledVector(q.clone().sub(HEAD_CENTER_LOCAL).normalize(), 0.05 + 0.012 * t); // proud of the hair surface so the ends read
          pts.push(q);
        }
        specs.push({
          pointsLocal: pts, width: 0.05 * wMul, thickness: 0.014, stiffnessBias: 0.95, layerAO: 1.0,
          layerOrder: 4, lockNormal: false, scalpRoot: 0.4,
        });
      }
    }

    // Zone 3B: short tapered tail bundle leaving the tie, dropping behind the bun
    const tailCount = 0;
    for (let i = 0; i < tailCount; i++) {
      const f = (i / (tailCount - 1)) * 2 - 1;
      const pts: THREE.Vector3[] = [];
      const len = 0.13 + rand() * 0.03 - Math.abs(f) * 0.04;
      for (let k = 0; k < NODES_PER_LOCK; k++) {
        const t = k / (NODES_PER_LOCK - 1);
        pts.push(new V3(
          bunC.x + f * (0.014 + 0.02 * t),
          bunC.y - bunR * 0.85 + 0.01 - len * Math.pow(t, 1.4),
          bunC.z - bunR * 0.6 - 0.045 * Math.sin(Math.min(1, t * 1.2) * Math.PI * 0.5) - 0.03 * t
        ));
      }
      specs.push({
        pointsLocal: pts, width: (0.04 - Math.abs(f) * 0.012) * wMul, thickness: 0.01, stiffnessBias: 0.8,
        layerAO: 0.9, layerOrder: 3, lockNormal: false, scalpRoot: 0.4,
      });
    }

    // Zone 3C: flared tuft tail rising out of the bun top (short narrow upward-flaring locks)
    const fanN = 13;
    for (let i = 0; i < fanN; i++) {
      const f = (i / (fanN - 1)) * 2 - 1;
      for (let j = 0; j < 3; j++) {
        const pts: THREE.Vector3[] = [];
        const len = 0.21 + rand() * 0.04 - Math.abs(f) * 0.07;
        const ang = f * 0.75 + (j - 1) * 0.05;
        const dir = new V3(Math.sin(ang) * 0.9, 0.35 + 0.15 * Math.cos(ang), -0.85).normalize();
        const base = new V3(bunC.x + f * 0.05, bunC.y + bunR * 0.45, bunC.z - bunR * 0.55 + (j - 1) * 0.008);
        for (let k = 0; k < NODES_PER_LOCK; k++) {
          const t = k / (NODES_PER_LOCK - 1);
          pts.push(new V3(
            base.x + dir.x * len * t * (1 + 0.5 * t),
            base.y + dir.y * len * t - 0.02 * t * t,
            base.z + dir.z * len * t
          ));
        }
        specs.push({
          pointsLocal: pts, width: (0.05 - Math.abs(f) * 0.01) * wMul, thickness: 0.012, stiffnessBias: 0.85, smooth: 2,
          layerAO: 0.45, layerOrder: 3, lockNormal: false, scalpRoot: 0.5,
        });
      }
    }

    return specs;
  }
