# Neon round 11 spec (verbatim essentials). Wren verified: AetherApplyTipAlphaClip and vertColor.a (TipAlphaClipMask) exist in our shader.

## 1. isFadeCap (stubble / short fade) design
Per-lock `isFadeCap?: boolean` (LockBuildSpec field already added to hairBuilder.ts by Wren). Flat ribbon width (no pointy tips), encoded in vertex colour alpha: 0.0 = isBaseShell, 0.25 = isFadeCap, 1.0 = normal lock. Shader dissolves fade caps into stubble with a stochastic stipple.

Data + ring taper (web hairPhysics.ts, inside the per-ring loop of updateVolumetricBuffers; Unity ports: AetherStrandMeshGenerator.cs BuildVolumetricMesh AND Runtime/AetherStrandWebMesh.cs; also the PresetGeometry.json export (tools web_export.ts / web_geometry.ts) must carry isFadeCap and the Unity loaders (AetherStrandMeshGenerator LockSpec loader, Runtime/AetherStrandWebData.cs) must read it):
  const isFade = Boolean(lock.isFadeCap);
  const widthTaper = isFade ? 1.0 : (t < 0.44 ? 1.0 - t * 0.14 : Math.pow((1.0 - t) / 0.56, 0.94) * 0.938);
  const thickTaper = isFade ? 0.55 * (1.0 - t * 0.65) : Math.pow(1.0 - t, 0.86);
  const tipClipFlag = lock.isBaseShell ? 0.0 : isFade ? 0.25 : 1.0;   // -> vertColor.a (maskVal in Unity)
  (C#: same formulas with Mathf.Pow; tipClipFlag replaces maskVal/mask for fade locks.) Keep web and Unity numerically identical (parity tests exist).

Shader (Shaders/AetherStrand_Hair.shader, all passes that call AetherApplyTipAlphaClip, the Built-in fallback and the web GLSL fragment shader): add a Mode 1 branch at the top of AetherApplyTipAlphaClip:
    if (vertColor.a > 0.15 && vertColor.a < 0.45)
    {
        float coverage = 1.0 - smoothstep(0.12, 0.96, t);              // t = saturate(uv.y): 100% at parietal blend -> 0% at the hairline
        float2 cell = floor(float2(uv.x * 42.0 + vertColor.b * 17.0, t * 28.0));
        float cellNoise = frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
        float ign = AetherInterleavedGradientNoise(screenPosXY);
        float threshold = lerp(cellNoise, ign, 0.55);
        clip(coverage - threshold);
        return;
    }
  keep the existing wispy-tip branch (vertColor.a > 0.5 && t > 0.58) as Mode 2. In the fragment shading pass also add: if (vertColor.a > 0.15 && vertColor.a < 0.45) ao = lerp(ao, 0.92, t * 0.70);  so stubble softens toward the skin instead of crushing to black. Caveat: the existing mask test elsewhere in the shader (`float mask = input.vertColor.a;` around line 260) must keep working for 0 / 0.25 / 1.0.

Apply to archetype Zone 2 of undercut-fade and short-crop: isFadeCap: true, width 0.052 * wMul, thickness 0.004, baseLift 0.0018, midArch 0, napeHug 1.55.

## 2. Why crown locks were invisible in short-crop and the fix
 - Zone 1 base shell (baseLift 0.002-0.005, thickness 0.009) buried the narrow Zone 3 tufts. Fix: shell baseLift 0.000, thickness 0.005, midArch 0.000; raise top tufts to baseLift 0.014-0.026.
 - Meridian growth (constant theta, phi increasing) gives vertical striations, never a forward-up brush. Build top tufts in Cartesian rows running back-to-front: x/z on the skull dome via a domePt(x,z,lift) helper (below), tuft length along z.
 - tipDropExtra -0.008 is ~2.6 mm on Tobin, invisible: ramp lift from 0.010 at the root to 0.032-0.048 at the front tip (10-15 mm on Tobin), plus a y pitch.

## 3. Neon's short-crop branch (replace the short-crop block in buildCustomHairSpecs). NOTE: uses SCALP_RX/RY/RZ, HEAD_CENTER_LOCAL: check they exist in hairBuilder.ts/hairPhysics.ts (names may differ; adapt, do not invent). `isFadeCap` Zone 2 only works after section 1 is wired.
