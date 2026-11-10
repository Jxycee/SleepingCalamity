# Tobin mesh hair with spring-chain motion (2026-11-10)
Decision: keep the approved hair_v5 C mesh (look retained) and add secondary motion with spring bone chains under the Head bone; DiffLocks strand output could not be trimmed into the reference style (see `difflocks_trim_experiment.py`, C:/AI/test_out/heroes/tobin/hair_v6/README.md).
- `hair_rig.py` (Blender, on `rig_v2/Tobin_rigged_v2.blend`): 44 k-means clumps of the hair mesh, each gets a 4-bone chain hair_<k>_0..3 under mixamorig:Head (bone 0 = non-deforming root); hair verts keep Head weight near the scalp (distance to the head mesh, smoothstep 0.6 -> 3.5 cm) and follow their chain below that; weights smoothed over the mesh so clump borders do not tear. Writes Tobin_rigged_hair.blend + chains.json.
- `sim_bake.py`: Blender preview of the same verlet chains with the Head bone yawing +-32 deg and nodding (renders a contact sheet; note the rig v2 blend has `pose_position = REST`, the script sets POSE).
- `export_fbx.py`: FBX with all 260 bones (rest pose).
- Unity: `Assets/_Project/Presentation/Characters/TobinHairSpringChains.cs` (put on the Tobin root; finds the Head bone and the hair_* bones by name, same solver as sim_bake: stiffness 1500, damping 0.93, gravity 9.8, 4 substeps; `weight` = 0 gives rigid hair). Presentation only, no authority.
Outputs: C:/AI/test_out/heroes/tobin/hair_v6/rig/{Tobin_rigged_hair.blend,.fbx,chains.json,sim_sheet.png}.
Not done: Unity import / Humanoid check of the new FBX, collision with shoulders (hair is short; chains do not collide), tuning per clump (fringe vs back).
