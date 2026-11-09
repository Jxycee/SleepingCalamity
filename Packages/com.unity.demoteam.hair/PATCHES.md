# Local patches (Sleeping Calamity)

Base: com.unity.demoteam.hair 0.19.0-preview.1, commit 3798ece0516c7e844dd1ea548f552f28f8789c5f, copied from the git package cache on 2026-11-08. Licence: Unity Companion License (see LICENSE.md).

Why: Unity 6000.6 turned `Object.GetInstanceID()` and `RenderParams.instanceID` into compile errors (CS0619).

Changes (API renames only, no behaviour change):
- Added `Runtime/EntityIdCompat.cs`: `GetInstanceKey()` returns `(int)obj.GetEntityId().GetRawData()`.
- Every `GetInstanceID()` call in `Editor/HairAssetBuilder.cs`, `Runtime/HairBoundary.cs`, `Runtime/HairInstance.cs`, `Runtime/HairWind.cs`, `Runtime/Utility/DerivedCache.cs` now calls `GetInstanceKey()`.
- `Runtime/HairInstance.cs`: `rparams.instanceID = 0` became `rparams.entityId = EntityId.None`; `rparams.instanceID = this.GetInstanceID()` became `rparams.entityId = this.GetEntityId()`.
