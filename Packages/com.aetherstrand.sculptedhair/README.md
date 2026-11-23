# AetherStrand — Sculpted Hair Plugin for Unity 6 & Blender 4.x

Exported Preset: **Layered Swept Long** (`layered_swept_long` — Asymmetric swept fringe with layered crown volume)
Exported Viewport Locks: **430**
Target Render Pipeline: **Unity 6 URP 17.6 (Universal Render Pipeline, Forward & Forward+)**

## Contents
- `AetherStrand_Unity/package.json` — Embedded UPM package manifest (`com.aetherstrand.sculptedhair`, Unity `6000.0`).
- `AetherStrand_Unity/Runtime/AetherStrand.Runtime.asmdef` & `Editor/AetherStrand.Editor.asmdef` — Assembly definitions for zero-config UPM compilation.
- `AetherStrand_Unity/Shaders/AetherStrand_Hair.shader` — SRP-Batcher-safe Carved-Fiber Kajiya-Kay Dual-Lobe Hair Shader (`AetherStrand/SculptedHair_URP`) with `DepthOnly`, `DepthNormals`, Forward+ `LIGHT_LOOP_BEGIN`, `_HeadCenterBlend`, and `_DitherStrength`.
- `AetherStrand_Unity/Runtime/AetherStrandPhysics.cs` — Substepped XPBD / Verlet bone-chain solver with framerate-independent damping, per-zone stiffness (`Bangs`, `Crown`, `Sides`, `Rear`), `Simulate(dt, simTime)`, `ResetToRestPose()`, and `ComputeAudit()`.
- `AetherStrand_Unity/Runtime/AetherStrandCollider.cs` & `AetherStrandWindZone.cs` — Analytical Sphere/Capsule colliders with `SignedDistance()` and turbulent wind zone.
- `AetherStrand_Unity/Editor/AetherStrandSetupWindow.cs` — Headless-safe static material updater (GUID-preserving) and deterministic 6-collider + `Hair_` bone auto-setup.
- `AetherStrand_Unity/Textures/` — Seamless `GrooveAO`, `FlowIDShift`, `Normal`, and `AlphaCard` PNG maps.
- `AetherStrand_Blender/PresetGeometry.json` — Exact 1:1 exported viewport hair locks converted to Blender Z-up (`-Y` front).
- `AetherStrand_Blender/aetherstrand_hair_cards.py` — Blender 4.x addon that auto-loads `PresetGeometry.json`, k-means clusters locks onto ~44 shared `Hair_<Zone>_<k>_<i>` bone chains (5 bones/chain, `use_deform = k > 0`), and exports Unity FBX with Linear vertex colors.
