  if (arch === 'braided-crown') {
    const V3 = THREE.Vector3;
    const braidPhiAt = (aTheta: number): number => {
      const u = THREE.MathUtils.clamp((aTheta - 0.5) / 2.45, 0, 1);
      return THREE.MathUtils.lerp(0.98, 2.2, u * u * (3 - 2 * u) * 0.35 + u * 0.65);
    };
    const hairlinePhiBase = (cosT: number): number => {
      if (cosT > 0.3) return 1.28;
      if (cosT > 0) return THREE.MathUtils.lerp(1.62, 1.28, cosT / 0.3);
      if (cosT > -0.5) return THREE.MathUtils.lerp(2.12, 1.62, (cosT + 0.5) / 0.5);
      return 2.12;
    };
    // hair ends under the braid line on the sides/back (tapered tips hide beneath the plait)
    const hairlinePhi = (cosT: number): number => {
      const aT = Math.acos(THREE.MathUtils.clamp(cosT, -1, 1));
      return aT > 0.5 ? Math.min(hairlinePhiBase(cosT), braidPhiAt(aT) + 0.11) : hairlinePhiBase(cosT);
    };

    // Zone 1: dense smooth base shell, two staggered rows so tips overlap (no triangular gaps)
    for (let tier = 0; tier < 4; tier++) {
      const count = 30;
      for (let i = 0; i < count; i++) {
        const theta = ((i + (tier % 2) * 0.5) / count) * Math.PI * 2;
        const cosT = Math.cos(theta);
        const lim = hairlinePhi(cosT) - (cosT > 0.2 ? 0.1 + (tier % 2) * 0.05 : 0.0);
        const phiStart = tier === 0 ? (i % 2 === 0 ? 0.01 : 0.14) : 0.15 + tier * 0.3;
        if (phiStart > lim - 0.25) continue;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart, phiEnd: lim,
          baseLift: 0.004 + (3 - tier) * 0.0025, midArch: 0.005, tipFlickOut: 0.0, napeHug: 0.96,
        });
        specs.push({
          pointsLocal: pts, width: 0.075 * wMul, thickness: 0.012, stiffnessBias: 0.9, layerAO: 0.8,
          layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: tier === 0 ? 0.48 : 0.25,
        });
      }
    }

    // Hairline row: dense short narrow locks so tapered tips do not leave sawteeth
    for (let i = 0; i < 90; i++) {
      const theta = (i / 90) * Math.PI * 2 + 0.017;
      const cosT = Math.cos(theta);
      if (cosT > 0.2) continue;
      const lim = hairlinePhi(cosT);
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: theta, phiStart: lim - 0.45, phiEnd: lim + 0.04,
        baseLift: 0.012 + 0.014 * Math.max(0, 1 - Math.abs(cosT) * 4), midArch: 0.004, tipFlickOut: 0.0, napeHug: 0.96,
      });
      specs.push({
        pointsLocal: pts, width: 0.034 * wMul, thickness: 0.01, stiffnessBias: 0.9, layerAO: 0.85,
        layerOrder: 1, lockNormal: false, scalpRoot: 0.3,
      });
    }

    // Front filler: locks reaching under the hairline band so no scalp shows between crown tips and the band
    for (let i = 0; i < 44; i++) {
      const th = ((i / 43) * 2 - 1) * 0.95;
      const pts = buildSculptedLock({
        thetaStart: th * 0.8, thetaEnd: th, phiStart: 0.75 + (i % 3) * 0.08, phiEnd: 1.2,
        baseLift: 0.01 + (i % 2) * 0.004, midArch: 0.004, tipFlickOut: 0.0, napeHug: 0.9,
      });
      specs.push({
        pointsLocal: pts, width: 0.05 * wMul, thickness: 0.01, stiffnessBias: 0.9, layerAO: 0.85,
        layerOrder: 1, lockNormal: false, scalpRoot: 0.3,
      });
    }

    // Temple filler: wide locks ending just under the braid's front end so no scalp shows above it
    for (const side of [-1, 1]) {
      for (let i = 0; i < 12; i++) {
        const th = 0.45 + i * 0.09;
        const pts = buildSculptedLock({
          thetaStart: side * (th - 0.15), thetaEnd: side * th,
          phiStart: 0.55 + (i % 3) * 0.06, phiEnd: braidPhiAt(th) + 0.1,
          baseLift: 0.022 + (i % 2) * 0.003, midArch: 0.003, tipFlickOut: 0.0, napeHug: 0.95,
        });
        specs.push({
          pointsLocal: pts, width: 0.06 * wMul, thickness: 0.009, stiffnessBias: 0.9, layerAO: 0.85,
          layerOrder: 2, lockNormal: false, scalpRoot: 0.3,
        });
      }
    }

    // Front hairline: soft isFadeCap ribbons (no bulky lip), swept back from the centre part
    for (let r = 0; r < 0; r++) { // replaced by the smooth hairline bands in the v4 block
      for (let i = 0; i < 64; i++) {
        const f = (i + r / 2) / 63 * 2 - 1;
        if (Math.abs(f) < 0.04) continue;
        const th = f * 1.25;
        const pts = buildSculptedLock({
          thetaStart: th * 0.9, thetaEnd: th,
          phiStart: 1.12 + r * 0.03, phiEnd: (r - 0.5) * 0.012 + Math.min(1.27 + 0.2 * f * f, Math.abs(th) > 0.5 ? braidPhiAt(Math.abs(th)) + 0.14 : 9),
          baseLift: 0.004, midArch: 0.0, tipFlickOut: 0.0, napeHug: 1.2,
        });
        specs.push({
          pointsLocal: pts, width: 0.04 * wMul, thickness: 0.005, stiffnessBias: 0.95, layerAO: 0.88,
          layerOrder: 3, isFadeCap: false, lockNormal: false, scalpRoot: 0.3,
        });
      }
    }

    // Zone 2: centre-parted front hair swept diagonally back from the part toward the braids
    for (const side of [-1, 1]) {
      const nFront = Math.round(15 * density);
      for (let i = 0; i < nFront; i++) {
        const f = i / (nFront - 1);
        const pts = buildSculptedLock({
          thetaStart: side * (0.05 + f * 0.1), thetaEnd: side * (0.35 + f * 0.75),
          phiStart: 0.2 + f * 0.3, phiEnd: 1.2 + f * f * 0.3,
          baseLift: 0.013, midArch: 0.008, tipFlickOut: 0.0, napeHug: 0.9,
        });
        specs.push({
          pointsLocal: pts, width: 0.05 * wMul, thickness: 0.011, stiffnessBias: 0.8, layerAO: 0.9,
          layerOrder: 2, lockNormal: false, scalpRoot: 0.3,
        });
      }
      // back/side hair gathered toward the nape join
      const nBack = Math.round(14 * density);
      for (let i = 0; i < nBack; i++) {
        const f = i / (nBack - 1);
        const th0 = side * (0.9 + f * 1.5);
        const pts = buildSculptedLock({
          thetaStart: th0, thetaEnd: side * (Math.PI - (Math.PI - Math.abs(th0)) * 0.45),
          phiStart: 0.4 + f * 0.2, phiEnd: hairlinePhi(Math.cos(th0)) - 0.05,
          baseLift: 0.013, midArch: 0.008, tipFlickOut: 0.0, napeHug: 0.9,
        });
        specs.push({
          pointsLocal: pts, width: 0.055 * wMul, thickness: 0.011, stiffnessBias: 0.8, layerAO: 0.88,
          layerOrder: 2, lockNormal: false, scalpRoot: 0.5,
        });
      }
      // fine loose wisps framing the face at the temples
      for (let k = 0; k < 1; k++) {
        const pts = buildSculptedLock({
          thetaStart: side * (0.6 + k * 0.1), thetaEnd: side * (0.78 + k * 0.1),
          phiStart: 1.0 + k * 0.04, phiEnd: 1.5 + k * 0.08,
          baseLift: 0.016, midArch: 0.012, tipFlickOut: 0.006, napeHug: 0.7, sCurveAmp: side * (0.1 + k * 0.03),
        });
        specs.push({
          pointsLocal: pts, width: 0.011 * wMul, thickness: 0.005, stiffnessBias: 0.4, layerAO: 0.95,
          layerOrder: 3, lockNormal: false, scalpRoot: 0.2,
        });
      }
    }

    // Front tip-cover rows: wide-bodied locks whose tips end under the hairline band (no scalp teeth above it)
    for (let row = 0; row < 2; row++) {
      for (let i = 0; i < 36; i++) {
        const th = ((i + row * 0.5) / 35 * 2 - 1) * 0.98;
        const pts = buildSculptedLock({
          thetaStart: th * 0.9, thetaEnd: th * 1.04,
          phiStart: 0.85 + row * 0.05, phiEnd: 1.2 + row * 0.02,
          baseLift: 0.012 + row * 0.003, midArch: 0.003, tipFlickOut: 0.0, napeHug: 0.9,
        });
        specs.push({
          pointsLocal: pts, width: 0.045 * wMul, thickness: 0.008, stiffnessBias: 0.9, layerAO: 0.85,
          layerOrder: 2, lockNormal: false, scalpRoot: 0.3,
        });
      }
    }
    // Nape cover: staggered wide locks over the back hairline so no sawteeth show above the plait join
    for (let row = 0; row < 2; row++) {
      for (let i = 0; i < 26; i++) {
        const th = Math.PI + ((i + row * 0.5) / 25 * 2 - 1) * 0.5;
        const pts = buildSculptedLock({
          thetaStart: th, thetaEnd: th + (Math.PI - th) * 0.15,
          phiStart: 1.85, phiEnd: 2.2 + row * 0.04,
          baseLift: 0.014 + row * 0.003, midArch: 0.003, tipFlickOut: 0.0, napeHug: 1.0,
        });
        specs.push({
          pointsLocal: pts, width: 0.05 * wMul, thickness: 0.009, stiffnessBias: 0.9, layerAO: 0.8,
          layerOrder: 2, lockNormal: false, scalpRoot: 0.3,
        });
      }
    }

    // Centre part filler (closes scalp slits) and soft wavy wisps in front of the ears
    for (const side of [-1, 1]) {
      for (let k = 0; k < 4; k++) {
        const pts = buildSculptedLock({
          thetaStart: side * 0.03, thetaEnd: side * (0.1 + k * 0.03),
          phiStart: 0.45 + k * 0.1, phiEnd: 1.3,
          baseLift: 0.008 + k * 0.002, midArch: 0.003, tipFlickOut: 0.0, napeHug: 0.9,
        });
        specs.push({
          pointsLocal: pts, width: 0.034 * wMul, thickness: 0.008, stiffnessBias: 0.9, layerAO: 0.85,
          layerOrder: 1, lockNormal: false, scalpRoot: 0.3,
        });
      }
      for (let k = 0; k < 2; k++) {
        const pts = buildSculptedLock({
          thetaStart: side * (0.95 + k * 0.12), thetaEnd: side * (1.1 + k * 0.12),
          phiStart: 1.2, phiEnd: 1.62 + k * 0.08,
          baseLift: 0.02, midArch: 0.01, tipFlickOut: 0.01, napeHug: 0.6, sCurveAmp: side * 0.14,
        });
        specs.push({
          pointsLocal: pts, width: 0.015 * wMul, thickness: 0.006, stiffnessBias: 0.45, layerAO: 0.95,
          layerOrder: 5, lockNormal: false, scalpRoot: 0.2,
        });
      }
    }

    // Scalp underlayer for the sides and nape (hidden under the braids and curtains): extra skull-hugging base locks
    for (let i = 0; i < 150; i++) {
      const theta = Math.PI * 0.5 + (i / 150) * Math.PI * 1.0 + ((i % 3) - 1) * 0.01;
      const cosT = Math.cos(theta);
      const lim = hairlinePhi(cosT);
      const phiStart = 0.9 + (i % 4) * 0.12;
      if (phiStart > lim - 0.25) continue;
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: theta, phiStart, phiEnd: lim,
        baseLift: 0.006, midArch: 0.004, tipFlickOut: 0.0, napeHug: 0.96,
      });
      specs.push({
        pointsLocal: pts, width: 0.06 * wMul, thickness: 0.01, stiffnessBias: 0.9, layerAO: 0.7,
        layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.25,
      });
    }

    // Smooth hairline bands: wide ribbons laid along the hairline arc, so the edge is a straight ribbon edge (no tapered teeth);
    // the two halves taper to nothing at the centre, which leaves a thin clean part
    for (const side of [-1, 1]) {
      for (let layer = 0; layer < 2; layer++) {
        const hw = layer === 0 ? 0.075 : 0.04;
        const off = layer === 0 ? 0.12 : 0.03;
        const pts: THREE.Vector3[] = [];
        for (let j = 0; j < NODES_PER_LOCK; j++) {
          const t = j / (NODES_PER_LOCK - 1);
          const th = 0.012 + t * 1.52;
          const fr = th / 1.25;
          const hl = Math.min(1.2 + 0.36 * fr * fr, th > 0.5 ? braidPhiAt(th) + 0.14 : 9);
          pts.push(skullSurfacePoint(side * th, hl - off, 0.012 + layer * 0.004));
        }
        specs.push({
          pointsLocal: pts, width: hw * wMul, thickness: 0.006, stiffnessBias: 0.95, layerAO: layer === 0 ? 0.8 : 0.9,
          layerOrder: 3 + layer, lockNormal: false, scalpRoot: 0.5,
        });
      }
    }

    // Temple cover: hair swept back from the part over the temples toward the ear and the crown braid
    for (const side of [-1, 1]) {
      for (let i = 0; i < 9; i++) {
        const f = i / 8;
        const pts = buildSculptedLock({
          thetaStart: side * (0.9 + f * 0.3), thetaEnd: side * (1.25 + f * 0.3),
          phiStart: 0.95 + f * 0.1, phiEnd: 1.5 + f * 0.22,
          baseLift: 0.016, midArch: 0.004, tipFlickOut: 0.004, napeHug: 0.85,
        });
        specs.push({
          pointsLocal: pts, width: 0.05 * wMul, thickness: 0.009, stiffnessBias: 0.85, layerAO: 0.85,
          layerOrder: 2, lockNormal: false, scalpRoot: 0.3,
        });
      }
    }

    // Face-framing strands: hair falling in front of the ears from the temples
    for (const side of [-1, 1]) {
      for (let i = 0; i < 6; i++) {
        const f = i / 5;
        const pts = buildSculptedLock({
          thetaStart: side * (1.28 + f * 0.1), thetaEnd: side * (1.4 + f * 0.1),
          phiStart: 1.5, phiEnd: 2.35 + 0.12 * Math.sin(i * 2.1),
          baseLift: 0.034, midArch: 0.006, tipFlickOut: 0.03, napeHug: 0.5, sCurveAmp: side * 0.08 * Math.sin(i * 1.9 + 0.5),
        });
        specs.push({
          pointsLocal: pts, width: 0.04 * wMul, thickness: 0.008, stiffnessBias: 0.5, layerAO: 0.85,
          layerOrder: 4, lockNormal: false, scalpRoot: 0.5,
        });
      }
    }

    // Apex fan: thin locks radiating from the crown part line so the apex reads as a swirl, not a star
    for (const side of [-1, 1]) {
      for (let k = 0; k < 4; k++) {
        const pts = buildSculptedLock({
          thetaStart: side * 0.01, thetaEnd: side * (0.45 + k * 0.38),
          phiStart: 0.02, phiEnd: 0.72 + k * 0.06,
          baseLift: 0.011, midArch: 0.0, tipFlickOut: 0.0, napeHug: 0.95,
        });
        specs.push({
          pointsLocal: pts, width: 0.034 * wMul, thickness: 0.006, stiffnessBias: 0.95, layerAO: 0.9,
          layerOrder: 3, lockNormal: false, scalpRoot: 0.5,
        });
      }
    }

    // Loose lower hair: wavy, flared curtains of mixed length falling behind the ears toward the shoulders (under the braids)
    for (const side of [-1, 1]) {
      for (let i = 0; i < 22; i++) {
        const f = i / 21;
        const len = 2.9 + 0.26 * Math.sin(i * 2.3 + (side > 0 ? 1.1 : 0)) + 0.12 * f;
        const pts = buildSculptedLock({
          thetaStart: side * (1.85 + f * 1.0), thetaEnd: side * (1.95 + f * 0.95 + 0.07 * Math.sin(i * 1.7)),
          phiStart: 1.75, phiEnd: len,
          baseLift: 0.032, midArch: 0.004, tipFlickOut: 0.03 * (1 - f * 0.5), napeHug: 0.7,
          sCurveAmp: side * 0.09 * Math.sin(i * 2.1 + 0.7),
        });
        specs.push({
          pointsLocal: pts, width: 0.045 * wMul, thickness: 0.01, stiffnessBias: 0.5, layerAO: 0.78 + 0.17 * (i % 2),
          layerOrder: 1 + (i % 3), lockNormal: false, scalpRoot: 0.3,
        });
      }
    }

    // v4 braid builder: three sine-weaving strands along ONE shared centreline, chevron lobes built from short overlapping ribbons
    const braid3 = (
      cl: (u: number) => THREE.Vector3, nrm: (p: THREE.Vector3) => THREE.Vector3,
      halfP: number, amp: number, lobeW: number, bulge: number, stiff: number,
      out?: THREE.Vector3, endScale = 1, coreAO = 0.45, hang = false, pin = hang,
    ) => {
      const N0 = 160;
      const cum: number[] = [0];
      let prevP = cl(0);
      for (let i = 1; i <= N0; i++) { const q = cl(i / N0); cum.push(cum[i - 1] + q.distanceTo(prevP)); prevP = q; }
      const total = cum[N0];
      const uOfS = (s0: number): number => {
        const s = THREE.MathUtils.clamp(s0, 0, total);
        let i = 1;
        while (i < N0 && cum[i] < s) i++;
        const f = (s - cum[i - 1]) / Math.max(1e-9, cum[i] - cum[i - 1]);
        return (i - 1 + f) / N0;
      };
      const frame = (s: number) => {
        const u = uOfS(s);
        const p = cl(u);
        const T = cl(Math.min(1, u + 0.01)).sub(cl(Math.max(0, u - 0.01))).normalize();
        let N = nrm(p);
        const B = new V3().crossVectors(T, N).normalize();
        N = new V3().crossVectors(B, T).normalize();
        return { p, N, B };
      };
      const nHalf = Math.max(2, Math.round(total / halfP));
      const hp = total / nHalf;
      const ov = 0.3 * hp;
      const aAt = (s: number) => amp * THREE.MathUtils.lerp(1, endScale, THREE.MathUtils.clamp(s / total, 0, 1));
      // core body so no gap shows between lobes
      const nCore = Math.max(1, Math.ceil(total / (hang ? 0.2 : 0.07)));
      for (let c = 0; c < nCore; c++) {
        const pts: THREE.Vector3[] = [];
        for (let j = 0; j < NODES_PER_LOCK; j++) {
          const s = ((c + j / (NODES_PER_LOCK - 1) * 1.2) / nCore) * total;
          const f = frame(s);
          pts.push(f.p.clone().addScaledVector(f.N, 0.002));
        }
        specs.push({
          pointsLocal: pts, width: (aAt(c / nCore * total) * 2 + lobeW * 0.4) * wMul, thickness: 0.014, stiffnessBias: stiff, layerAO: coreAO,
          layerOrder: 3, lockNormal: pin, scalpRoot: 0.5, customOutDir: out,
        });
      }
      for (let r = 0; r < 3; r++) {
        const ph = r * Math.PI * 2 / 3;
        const span = 1; // plait pieces cover a full weave period each (fewer far-rooted locks)
        for (let m = -2; m <= nHalf + 1; m += span) {
          const s0 = hp * (m + 0.5 - ph / Math.PI) - ov;
          const s1 = s0 + span * hp + 2 * ov;
          if (s1 < 0 || s0 > total) continue;
          const pts: THREE.Vector3[] = [];
          for (let j = 0; j < NODES_PER_LOCK; j++) {
            const s = THREE.MathUtils.lerp(s0, s1, j / (NODES_PER_LOCK - 1));
            const f = frame(s);
            const ang = Math.PI * s / hp + ph;
            const lat = aAt(s) * Math.sin(ang);
            const dep = 0.004 + bulge * (0.5 + 0.5 * Math.cos(ang));
            pts.push(f.p.clone().addScaledVector(f.B, lat).addScaledVector(f.N, dep));
          }
          specs.push({
            pointsLocal: pts, width: lobeW * wMul, thickness: hang ? 0.028 : 0.014, stiffnessBias: stiff, layerAO: (hang ? 0.82 : 0.7) + (hang ? 0.18 : 0.3) * ((Math.round(m / span) + r) & 1),
            layerOrder: 4, lockNormal: pin, scalpRoot: 0.5, customOutDir: out,
          });
        }
      }
    };

    // Zone 3: twin side braids, temple hairline -> over the ear -> back-of-head join
    for (const side of [-1, 1]) {
      braid3(
        (u) => {
          const e = u * u * (3 - 2 * u) * 0.35 + u * 0.65;
          return skullSurfacePoint(side * THREE.MathUtils.lerp(0.5, 3.0, u), THREE.MathUtils.lerp(0.98, 2.2, e), 0.036 + 0.004 * u * u + 0.012 * Math.sin(Math.PI * Math.min(1, u * 1.15)));
        },
        (p) => p.clone().sub(HEAD_CENTER_LOCAL).normalize(),
        0.04, 0.018, 0.034, 0.014, 0.95, undefined, 1, 0.45, false, true,
      );
    }

    // Zone 4: one regular 3-strand plait down the back centre, tie near the end
    {
      const rootP = skullSurfacePoint(Math.PI, 2.05, 0.04);
      const plaitLen = 0.8;
      // web units are about 3x Tobin metres: 1.1 reaches the shoulder blades. Hang on the nape, then lean back to clear the upper back
      const plaitCl = (u: number) => {
        const d = plaitLen * u;
        const k = THREE.MathUtils.clamp(d / 0.8, 0, 1);
        const sm = k * k * k * (k * (6 * k - 15) + 10);
        return new V3(rootP.x + Math.sin(u * Math.PI * 1.2) * 0.004 * u,
          rootP.y - d,
          rootP.z - 0.012 - 0.2 * sm);
      };
      braid3(plaitCl, () => new V3(0, 0, -1), 0.075, 0.03, 0.046, 0.022, 0.97, new V3(0, 0.1, -1), 0.8, 0.6, true);
      const tp = plaitCl(0.96);
      // hair tie: two dark rings wrapped around the plait, then a short tapered tail
      for (let b2 = 0; b2 < 2; b2++) {
        const arc: THREE.Vector3[] = [];
        for (let j = 0; j < NODES_PER_LOCK; j++) {
          const ang = (j / (NODES_PER_LOCK - 1) * 2 - 1) * 1.35;
          arc.push(new V3(tp.x + 0.085 * Math.sin(ang), tp.y - b2 * 0.026, tp.z - 0.03 * Math.cos(ang) + 0.012));
        }
        specs.push({
          pointsLocal: arc, width: 0.036 * wMul, thickness: 0.034, stiffnessBias: 0.6, layerAO: 0.15,
          layerOrder: 6, lockNormal: true, scalpRoot: 0.5, customOutDir: new V3(0, 0.1, -1),
        });
      }
      for (let k = 0; k < 3; k++) {
        const pts: THREE.Vector3[] = [];
        for (let j = 0; j < NODES_PER_LOCK; j++) {
          const t = j / (NODES_PER_LOCK - 1);
          pts.push(new V3(tp.x + (k - 1) * 0.026 * (1 - t * 0.7), tp.y - 0.05 - t * 0.09, tp.z - 0.012 + t * 0.004));
        }
        specs.push({
          pointsLocal: pts, width: 0.036 * wMul, thickness: 0.012, stiffnessBias: 0.5, layerAO: 0.85,
          layerOrder: 5, lockNormal: true, scalpRoot: 0.5,
        });
      }
    }

    return specs;
  }
