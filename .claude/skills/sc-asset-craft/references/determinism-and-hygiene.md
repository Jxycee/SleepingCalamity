<!-- Covers: seeded randomness, lathe/origin/bake-cage hygiene and Blender library bugs. Moved from SKILL.md section 6. -->

## 6. Determinism and hygiene
- Advice, not a gate: reproducible generators are easier to review. FBX headers carry a timestamp, so compare audit JSONs rather than FBX bytes if you want to check that a rerun changed nothing.
- Every random draw comes from `random.Random(SEED ^ crc32(stream))`, one named stream per part. Never `random.random()` or time. `mathutils.noise` is position-only, so it's safe.
- **Lathe caps, not r = 0 points:** a profile point on the axis plus a cap leaves loose vertices. End at a small radius and cap.
- **Splitting a lathe** (two materials, two bands): build two lathes from the two parts of the profile, or compact the vertex list; passing all vertices with some faces leaves loose ones.
- **A lean or a sink:** rotate about the base edge on the low side so that corner stays at y 0, and state it in the header.
- **Origin with attachments:** bounding-box centring is pulled off the body by a spill or a ladder. Centre on the body, or state the offset.
- No loose verts, no zero-area faces, no faces hidden inside other parts (they cost tris and bake garbage).
- **Bake cage:** extrusion below half the narrowest gap (3 mm extrusion, 8 mm ray for a 10 mm crack).
- **Blender library bugs** (fixed in the cottage's `sc_ck_lib.py`): call `view_layer.update()` after setting `.location` or `matrix_world` is stale; convert numpy vectors to tuples before node sockets (segfault); clamp t to 1 in `(1-t)**p` (NaN); make image paths absolute before appending. `bpy.ops.object.join` keeps the first object's transform: apply transforms first.
- Big Python heredocs through Git Bash sometimes fail with "unexpected EOF": write files with the Write tool.
