# AetherStrand Automation & Verification Playbook

This plugin is engineered for headless visual verification, deterministic XPBD physics auditing, and one-shot Unity 6 / Blender 4.x production handoff.

## 1. Unity 6 Batch / Headless Setup API
Both editor setup entry points are public static headless-safe methods (zero modal dialogs):

```csharp
using AetherStrand;
using AetherStrand.Editor;

// 1. Create or update material in-place (preserves GUIDs)
var mat = AetherStrandSetupWindow.CreateMaterialAsset(
    "Assets/AetherStrand_layered_swept_long_Mat.mat",
    "Packages/com.aetherstrand.sculptedhair/Presets/ExportedLook.json"
);

// 2. Bind Hair_ bone chains + 6 head-height-scaled colliders deterministically
var phys = AetherStrandSetupWindow.AutoSetupPhysics(
    characterRoot,
    "Hair_",
    "Packages/com.aetherstrand.sculptedhair/Presets/ExportedLook.json",
    headHeightOverride: 0f,
    hairRenderer: hairSkinnedMeshRenderer
);

// 3. Deterministic physics audit loop
phys.ResetToRestPose();
for (int f = 0; f < 180; f++) phys.Simulate(1f / 60f, f / 60f);
PhysicsAuditResult audit = phys.ComputeAudit();
```

## 2. Blender 4.x Exact Geometry Import & Unity FBX Export
1. Keep `PresetGeometry.json` beside `aetherstrand_hair_cards.py`.
2. Run **AetherStrand > Generate Sculpted Hair + Clustered Rig** (imports all viewport locks and k-means clusters them onto ~44 shared `Hair_<Zone>_<k>_<i>` bone chains with `5` bones/chain and `use_deform = k > 0`).
3. Run **AetherStrand > Export Unity FBX (Linear VCol)** (exports with `colors_type='LINEAR'` and `add_leaf_bones=False`).

## 3. Coordinate & Rig Conventions (Active Preset: Layered Swept Long / `layered_swept_long`)
- **Viewport Space**: Right-handed Y-up (`+Z` front, relative to head pivot `(0, 0.44, 0)`).
- **Blender Export Space (`PresetGeometry.json`)**: Right-handed Z-up (`-Y` front): `[x - px, -(z - pz), y - py]`.
- **Target Unity Pipeline**: `Unity 6 URP 17.6 (HLSL, Forward+, SRP Batcher)`.
- **Solver Defaults**: `stiffness=0.74`, `bendingSmoothness=0.64`, `collisionMarginMm=9`, `solverSubsteps=3`.
