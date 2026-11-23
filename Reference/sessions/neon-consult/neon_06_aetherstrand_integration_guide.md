# AetherStrand Integration Guide (Neon, exchange 6, 2026-11-22)
Copied from Neon's Canvas. The plugin ZIP is `AetherStrand_SculptedHair_URP_Plugin.zip` (download pending). Neon wrote this after the user gave it the plugin context; it supersedes `neon_05_tobin_hair.md` (which assumed no plugin).

**What it proposes:** replace the HairDeck card route for Tobin end to end. This changes the hair route decided on 2026-11-10, so it needs the user's call.

## Changes from the first archive
- **Shader `AetherStrand/SculptedHair_URP`:** DepthOnly pass added; DepthNormals fixed (Cull Off, tip-only alpha clip) for SSAO; Forward+ LIGHT_LOOP; optional `_HeadCenterWS` radial normal blend (`_HeadCenterBlend`); non-MSAA tip dither (`_DitherStrength`); one UnityPerMaterial CBUFFER across all 4 passes.
- **Physics `AetherStrandPhysics.cs`:** solverSubsteps (default 3), exponential damping, an XPBD compliance tether, per-zone multipliers (Bangs, Crown, Sides, Rear), deterministic `Simulate(float dt, float simTime)`, and `ComputeAudit()` returning `AetherStrandAuditMetrics`.
- **Colliders:** lossyScale-aware, with `SignedDistance()`.
- **Editor setup:** headless static API (`CreateMaterialAsset`, `AutoSetupPhysics`), in-place material updates (keeps GUIDs), sorted chains, 6 colliders auto-scaled to head height H.
- **Blender addon:** 5-zone generator (Bangs, Crown, Sides, Rear Upper, Nape), optional PresetGeometry.json import, scalp projection onto the active head mesh (BVHTree), k-means of 120 locks onto about 44 `Hair_<Zone>_<k>_<i>` chains (5 bones, k=0 non-deforming root under Head), linear vertex-colour FBX export.

## Steps
1. **Unity:** copy `AetherStrand_Unity/` to `Packages/com.aetherstrand.sculptedhair/` or `Assets/Plugins/AetherStrand/`. Textures: GrooveAO, FlowIDShift and AlphaCard linear + Repeat; Normal as a normal map.
2. **Blender 4.x:** run the addon with Tobin's MetaPerson head active (roots project, tucked 4 mm). Generate, then use Export Selected Hair FBX (Linear VCol). In Unity: Normals Import, Tangents Mikktspace; the rig goes under Tobin's Head (or merge the Hair_ bones in the character build script).
3. **Build script:**
```csharp
using AetherStrand; using AetherStrand.Editor; using UnityEngine;
public static void SetupTobinHair(GameObject tobinRoot, SkinnedMeshRenderer hairMeshRenderer)
{
    const string presetPath = "Assets/Plugins/AetherStrand/Presets/PartedShag_Medium.json";
    const string matPath    = "Assets/Characters/Tobin/Materials/Tobin_Hair.mat";
    Material hairMat = AetherStrandSetupWindow.CreateMaterialAsset(matPath, presetPath);
    if (hairMat != null && hairMeshRenderer != null) hairMeshRenderer.sharedMaterial = hairMat;
    AetherStrandPhysics physics = AetherStrandSetupWindow.AutoSetupPhysics(
        characterRoot: tobinRoot, bonePrefix: "Hair_", presetJsonPath: presetPath,
        headHeightOverride: 0f, hairRenderer: hairMeshRenderer);
}
```
   Tuning: `_DitherStrength` 0.65 under TAA/no MSAA; `_HeadCenterBlend` 0.30 (0.45 softer, 0 sharper). Remove `TobinHairSpringChains` and the SC_HairCards material from Tobin once this is bound.
4. **PlayMode audit test:** ResetToRestPose, 90 frames `Simulate(1/60, t)`, then `ComputeAudit()`: totalChains > 0, penetratingNodes == 0, maxKinkAngleDeg < 28, maxStretchErrorPct < 3.5, meanTipSwayCm in 0.2-5.0.

## Merge checklist
Wrapper compile + EditMode + PlayMode with the audit passing; a build-twice byte check on Tobin's prefab and material; close-ups (front, 3/4, back) in Hollowmere daylight and under a torch light.

## Coordinator notes (to check before any merge)
- The cards come from AetherStrand's own lock generator, not HairDeck, so Tobin's hair look changes. The user said "keep the look" on 2026-11-10.
- Paths should follow our layout: an embedded package under `Packages/` or a third-party folder, with asmdefs checked against `ArchitectureTests`.
- Verify the IP-clean claim by grepping the ZIP for preset and franchise names.
- Hero art runs in an Opus session (memory `opus-for-hero-art`).
