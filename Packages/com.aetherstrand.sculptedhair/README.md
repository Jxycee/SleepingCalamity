# AetherStrand — Sculpted Volumetric Hair Plugin for Unity (URP 17) & Blender 4.x

Preset Included: **PartedShag_Medium** (Layered chestnut shag with wispy tips)
Target Render Pipeline: **Unity 6 / URP 17.x (Forward & Forward+)**

## Contents
- `AetherStrand_Unity/Shaders/AetherStrand_Hair.shader` — Volumetric Carved-Fiber Kajiya-Kay Dual-Lobe Shader (`AetherStrand/SculptedHair_URP`) with `ForwardLit` (Forward+ compatible), `ShadowCaster`, `DepthOnly`, `DepthNormals` (clipped, two-sided), tip-only feathered cutout, and optional radial head-center normal blending.
- `AetherStrand_Unity/Runtime/AetherStrandPhysics.cs` — Substepped, framerate-independent XPBD/Verlet bone-chain solver with per-zone multipliers (`Bangs`, `Crown`, `Sides`, `Rear`), collider-projected rest poses, rest-curvature bending smoothness, zero-restitution sliding friction, deterministic `Simulate(dt, simTime)`, and `ComputeAudit()` PlayMode telemetry.
- `AetherStrand_Unity/Runtime/AetherStrandCollider.cs` & `AetherStrandWindZone.cs` — Scale-aware analytical Sphere/Capsule colliders (with `SignedDistance`) and deterministic turbulent wind zone.
- `AetherStrand_Unity/Editor/AetherStrandSetupWindow.cs` — Interactive setup window plus headless static batchmode API (`CreateMaterialAsset`, `AutoSetupPhysics`) that builds 6 head-height-scaled colliders (Cranium, Face/Jaw, Neck, Upper Chest, Left/Right Shoulders).
- `AetherStrand_Unity/Presets/PartedShag_Medium.json` — Calibrated shader & physics preset.
- `AetherStrand_Unity/Textures/` — Seamless `GrooveAO`, `FlowIDShift`, `Normal`, and `AlphaCard` PNG maps.
- `AetherStrand_Blender/aetherstrand_hair_cards.py` — Blender 4.x addon with 5-zone sculpted lock generator, optional `PresetGeometry.json` loader, scalp-mesh projection, deterministic k-means bone-chain clustering (~44 chains for 120 locks), curve conversion, and Linear Vertex Color Unity FBX exporter.
