"""Export Tobin template meshes (basis, shape keys, faces, UVs, bones) to npz.
Run: blender -b --factory-startup --python export_template.py -- <glb> <out.npz>
"""
import sys, json
import bpy
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
glb, out = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)

short = {"AvatarHead": "head", "AvatarEyelashes": "lashes", "AvatarLeftEyeball": "eyeL",
         "AvatarRightEyeball": "eyeR", "AvatarTeethLower": "teethLower",
         "AvatarTeethUpper": "teethUpper", "AvatarBody": "body"}
data = {}
info = {}
for o in bpy.data.objects:
    print("OBJ", o.name, o.type, o.parent.name if o.parent else None)
for o in bpy.data.objects:
    if o.type != 'MESH':
        continue
    key = None
    for k, v in short.items():
        if o.name == k or o.name.startswith(k):
            key = v
    if key is None:
        print("skip mesh", o.name)
        continue
    me = o.data
    n = len(me.vertices)
    co = np.empty(n * 3, np.float32)
    me.vertices.foreach_get("co", co)
    data[f"{key}/basis"] = co.reshape(-1, 3)
    nf = len(me.polygons)
    tri_ok = all(p.loop_total == 3 for p in me.polygons)
    if tri_ok:
        f = np.empty(nf * 3, np.int32)
        me.polygons.foreach_get("vertices", f)
        data[f"{key}/faces"] = f.reshape(-1, 3)
    else:
        data[f"{key}/faces_flat"] = np.array([v for p in me.polygons for v in p.vertices], np.int32)
        data[f"{key}/face_sizes"] = np.array([p.loop_total for p in me.polygons], np.int32)
    # per-loop uv + loop->vertex
    lv = np.empty(len(me.loops), np.int32)
    me.loops.foreach_get("vertex_index", lv)
    data[f"{key}/loop_vertex"] = lv
    if me.uv_layers:
        uv = np.empty(len(me.loops) * 2, np.float32)
        me.uv_layers[0].data.foreach_get("uv", uv)
        data[f"{key}/loop_uv"] = uv.reshape(-1, 2)
        info[f"{key}/uv_name"] = me.uv_layers[0].name
    data[f"{key}/matrix_world"] = np.array(o.matrix_world, np.float32)
    names = []
    if me.shape_keys:
        kb = me.shape_keys.key_blocks
        arr = np.empty((len(kb), n, 3), np.float32)
        for i, k in enumerate(kb):
            c = np.empty(n * 3, np.float32)
            k.data.foreach_get("co", c)
            arr[i] = c.reshape(-1, 3)
            names.append(k.name)
        data[f"{key}/keys"] = arr  # index 0 = Basis
        data[f"{key}/key_names"] = np.array(names)
        info[f"{key}/basis_vs_mesh_maxdiff"] = float(np.abs(arr[0] - data[f"{key}/basis"]).max())
    info[f"{key}/obj"] = o.name
    info[f"{key}/nverts"] = n
    info[f"{key}/nfaces"] = nf
    info[f"{key}/tris_only"] = tri_ok
    info[f"{key}/nkeys"] = len(names)
    info[f"{key}/parent"] = o.parent.name if o.parent else None

bones = {}
for o in bpy.data.objects:
    if o.type == 'ARMATURE':
        info["armature"] = o.name
        data["armature_matrix_world"] = np.array(o.matrix_world, np.float32)
        allb = []
        for b in o.data.bones:
            allb.append(b.name)
            p = o.matrix_world @ b.head_local
            t = o.matrix_world @ b.tail_local
            bones[b.name] = [list(p), list(t)]
        info["n_bones"] = len(allb)
        data["bone_names"] = np.array(allb)
        data["bone_heads_world"] = np.array([bones[b][0] for b in allb], np.float32)
        data["bone_tails_world"] = np.array([bones[b][1] for b in allb], np.float32)
        data["bone_heads_arm"] = np.array([list(o.data.bones[b].head_local) for b in allb], np.float32)
sel = {k: v for k, v in bones.items() if any(s in k.lower() for s in ("eye", "jaw", "head", "neck"))}
info["selected_bones_world"] = sel
np.savez_compressed(out, **data)
json.dump(info, open(out.replace(".npz", "_info.json"), "w"), indent=1, default=str)
print("INFO", json.dumps({k: v for k, v in info.items() if k != "selected_bones_world"}, default=str))
print("SELBONES", json.dumps(sel))
