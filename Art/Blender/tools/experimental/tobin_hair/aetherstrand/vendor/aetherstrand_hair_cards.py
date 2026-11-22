bl_info = {
    "name": "AetherStrand — Sculpted Volumetric Hair & Bone Rig Generator",
    "author": "AetherStrand Studio",
    "version": (2, 1, 0),
    "blender": (4, 0, 0),
    "location": "View3D > Sidebar (N) > AetherStrand",
    "description": "Generates layered 3D crescent-profile sculpted hair locks with root-to-tip UVs, linear vertex color AO/seed masks, clustered Unity Hair_ bone chains, and optional PresetGeometry.json import.",
    "category": "Mesh",
}

import json
import math
import os
import random
import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree


# Crescent sculpted cross-section profile (domed carved outer ridge + flatter hugging underside)
PROFILE_U = [0.0, 0.18, 0.36, 0.5, 0.64, 0.82, 1.0]
PROFILE_X = [0.0, 0.76, 1.0, 0.0, -1.0, -0.76, 0.0]
PROFILE_Y = [1.0, 0.54, -0.02, -0.26, -0.02, 0.54, 1.0]


def sample_cubic_bezier(p0, p1, p2, p3, count):
    pts = []
    for i in range(count):
        t = i / max(1, count - 1)
        u = 1.0 - t
        p = (p0 * (u ** 3)) + (p1 * (3.0 * u * u * t)) + (p2 * (3.0 * u * t * t)) + (p3 * (t ** 3))
        pts.append(p)
    return pts


def project_root_to_scalp(root_pt, head_bvh, head_matrix_inv, head_matrix):
    if head_bvh is None:
        return root_pt
    local_pt = head_matrix_inv @ root_pt
    loc, normal, _idx, _dist = head_bvh.find_nearest(local_pt)
    if loc is None:
        return root_pt
    world_loc = head_matrix @ loc
    world_n = (head_matrix.to_3x3() @ normal).normalized()
    return world_loc - world_n * 0.004  # Tuck 4 mm into scalp to prevent root gaps


def build_five_zone_locks(head_height, part_offset_x, tip_flick, lock_width, lock_thickness, segs, rng):
    """Generates a 5-zone medium layered sculpted cut (Bangs, Crown, Sides, Rear Upper, Nape)."""
    scale = head_height / 0.23
    locks = []

    def add_lock(zone, root, p1, p2, p3, w_mul, t_mul, layer_ao):
        pts = sample_cubic_bezier(root * scale, p1 * scale, p2 * scale, p3 * scale, segs)
        locks.append({
            "zone": zone,
            "points": pts,
            "width": lock_width * scale * w_mul * rng.uniform(0.84, 1.18),
            "thickness": lock_thickness * scale * t_mul * rng.uniform(0.86, 1.14),
            "seed": rng.random(),
            "layer_ao": layer_ao,
        })

    # Zone 1: Parted Front Bangs (22 locks)
    for i in range(22):
        side = -1.0 if i < 13 else 1.0
        u = (i % 13) / 12.0 if side < 0 else ((i - 13) / 8.0)
        root = Vector((part_offset_x + side * (0.010 + u * 0.055), 0.045 + (1.0 - u) * 0.045, 0.235 - u * 0.028))
        sweep_x = side * (0.055 + u * 0.045)
        fwd_y = 0.075 + (1.0 - u) * 0.030
        drop_z = 0.145 + (i % 3) * 0.022 + rng.uniform(-0.01, 0.015)
        p1 = root + Vector((sweep_x * 0.35, fwd_y * 0.55, 0.022))
        p2 = root + Vector((sweep_x * 0.85, fwd_y * 0.95, -drop_z * 0.45))
        p3 = root + Vector((sweep_x + side * tip_flick * 0.45, fwd_y * 0.72, -drop_z))
        add_lock("Bangs", root, p1, p2, p3, 0.88, 0.85, 0.94)

    # Zone 2: Crown Volume Layers (24 locks)
    for i in range(24):
        az = (i / 24.0) * math.tau + rng.uniform(-0.08, 0.08)
        r_ring = 0.022 + (i % 3) * 0.018
        root = Vector((part_offset_x * 0.6 + math.cos(az) * r_ring, math.sin(az) * r_ring, 0.252 - r_ring * 0.25))
        dx, dy = math.cos(az), math.sin(az)
        reach = 0.105 + rng.uniform(-0.01, 0.02)
        drop_z = 0.125 + (i % 2) * 0.025
        p1 = root + Vector((dx * 0.045, dy * 0.045, 0.036))
        p2 = root + Vector((dx * reach, dy * reach, -drop_z * 0.35))
        p3 = root + Vector((dx * (reach + tip_flick * 0.6), dy * (reach + tip_flick * 0.4), -drop_z))
        add_lock("Crown", root, p1, p2, p3, 1.05, 1.05, 0.98)

    # Zone 3: Left & Right Side Sweeps (32 locks)
    for i in range(32):
        side = -1.0 if i < 16 else 1.0
        u = (i % 16) / 15.0
        root = Vector((side * (0.068 + (1.0 - abs(u - 0.5) * 2.0) * 0.012), 0.040 - u * 0.115, 0.205 - (i % 2) * 0.022))
        dx = side * (0.060 + rng.uniform(-0.008, 0.012))
        dy = (0.025 - u * 0.065)
        drop_z = 0.190 + (i % 3) * 0.028 + rng.uniform(0.0, 0.02)
        p1 = root + Vector((dx * 0.50, dy * 0.40, 0.018))
        p2 = root + Vector((dx * 0.95, dy * 0.85, -drop_z * 0.42))
        p3 = root + Vector((dx + side * tip_flick * 0.85, dy - 0.015, -drop_z))
        add_lock("Sides", root, p1, p2, p3, 0.96, 0.95, 0.88 + (i % 2) * 0.10)

    # Zone 4: Rear Upper Layers (26 locks)
    for i in range(26):
        u = i / 25.0
        az = math.pi * (0.65 + u * 0.70)
        dx, dy = math.cos(az), math.sin(az)
        root = Vector((dx * 0.072, dy * 0.088, 0.215 - (i % 3) * 0.018))
        reach = 0.115 + math.sin(u * math.pi) * 0.025
        drop_z = 0.225 + (i % 3) * 0.030 + rng.uniform(0.0, 0.02)
        p1 = root + Vector((dx * 0.048, dy * 0.055, 0.026))
        p2 = root + Vector((dx * reach, dy * reach, -drop_z * 0.38))
        p3 = root + Vector((dx * (reach + tip_flick), dy * (reach + tip_flick * 0.8), -drop_z))
        add_lock("Rear", root, p1, p2, p3, 1.08, 1.02, 0.86 + (i % 2) * 0.12)

    # Zone 5: Nape Under-Layers (16 locks — darker interior AO for volume depth)
    for i in range(16):
        u = i / 15.0
        az = math.pi * (0.72 + u * 0.56)
        dx, dy = math.cos(az), math.sin(az)
        root = Vector((dx * 0.060, dy * 0.078, 0.135 - (i % 2) * 0.020))
        reach = 0.085
        drop_z = 0.155 + (i % 2) * 0.025
        p1 = root + Vector((dx * 0.032, dy * 0.038, 0.008))
        p2 = root + Vector((dx * reach, dy * reach, -drop_z * 0.45))
        p3 = root + Vector((dx * (reach + tip_flick * 0.5), dy * (reach + tip_flick * 0.5), -drop_z))
        add_lock("Rear", root, p1, p2, p3, 0.92, 0.90, 0.72)

    return locks


def load_preset_geometry_json(json_path, segs):
    with open(json_path, "r", encoding="utf-8") as f:
        data = json.load(f)
    locks = []
    for entry in data.get("locks", []):
        raw_pts = [Vector(p) for p in entry["points"]]
        if len(raw_pts) < 2:
            continue
        # Resample to uniform segment count if needed
        if len(raw_pts) != segs:
            resampled = []
            for k in range(segs):
                u = (k / max(1, segs - 1)) * (len(raw_pts) - 1)
                i0 = int(math.floor(u))
                i1 = min(len(raw_pts) - 1, i0 + 1)
                resampled.append(raw_pts[i0].lerp(raw_pts[i1], u - i0))
            raw_pts = resampled
        locks.append({
            "zone": entry.get("zone", "Sides"),
            "points": raw_pts,
            "width": float(entry.get("width", 0.028)),
            "thickness": float(entry.get("thickness", 0.011)),
            "seed": float(entry.get("seed", 0.5)),
            "layer_ao": float(entry.get("layerAO", 0.92)),
        })
    return locks


def cluster_locks_to_chains(locks, target_chain_count, rng):
    """Deterministic zone-aware k-means clustering so 120+ locks share ~44 bone chains."""
    n = len(locks)
    k_total = max(1, min(target_chain_count, n))

    # Deterministic furthest-point seeding by root+mid position
    features = [l["points"][0] * 0.6 + l["points"][len(l["points"]) // 2] * 0.4 for l in locks]
    centers = [features[0].copy()]
    chosen = {0}
    while len(centers) < k_total:
        best_idx = -1
        best_dist = -1.0
        for i, feat in enumerate(features):
            if i in chosen:
                continue
            d = min((feat - c).length_squared for c in centers)
            if d > best_dist:
                best_dist = d
                best_idx = i
        chosen.add(best_idx)
        centers.append(features[best_idx].copy())

    assignments = [0] * n
    for _ in range(10):
        for i, feat in enumerate(features):
            assignments[i] = min(range(k_total), key=lambda c: (feat - centers[c]).length_squared)
        for c in range(k_total):
            members = [features[i] for i in range(n) if assignments[i] == c]
            if members:
                centers[c] = sum(members, Vector((0, 0, 0))) / len(members)

    chains = []
    segs = len(locks[0]["points"])
    for c in range(k_total):
        member_indices = [i for i in range(n) if assignments[i] == c]
        if not member_indices:
            continue
        avg_pts = []
        for s in range(segs):
            avg_pts.append(sum((locks[i]["points"][s] for i in member_indices), Vector((0, 0, 0))) / len(member_indices))
        # Majority zone label
        zone_counts = {}
        for i in member_indices:
            z = locks[i]["zone"]
            zone_counts[z] = zone_counts.get(z, 0) + 1
        zone = max(zone_counts.items(), key=lambda kv: kv[1])[0]
        chains.append({"zone": zone, "points": avg_pts, "members": member_indices})
    return chains


def create_volumetric_lock_mesh(name, spine_points, base_width, base_thickness, seed_val, layer_ao=0.95):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    uv_layer = bm.loops.layers.uv.new("UVMap")
    color_layer = bm.loops.layers.color.new("Col")

    count = len(spine_points)
    ring_count = count - 1
    sides = len(PROFILE_X) - 1
    rings = []

    ref_out = spine_points[0].normalized() if spine_points[0].length > 0.001 else Vector((0, 0, 1))

    for k in range(ring_count):
        pt = spine_points[k]
        t = k / max(1, count - 1)
        if k == 0:
            tang = (spine_points[1] - pt).normalized()
        else:
            tang = (spine_points[k + 1] - spine_points[k - 1]).normalized()

        side = tang.cross(ref_out)
        side = Vector((1, 0, 0)) if side.length < 0.001 else side.normalized()
        out_vec = side.cross(tang).normalized()
        ref_out = ref_out.lerp(out_vec, 0.35).normalized()

        root_tuck = 0.7 + 0.3 * math.sin(min(1.0, t * 4.5) * math.pi * 0.5)
        width_taper = (1.0 - t * 0.14) if t < 0.44 else ((((1.0 - t) / 0.56) ** 0.94) * 0.938)
        thick_taper = (1.0 - t) ** 0.86
        half_w = base_width * 0.5 * root_tuck * max(0.04, width_taper)
        half_t = base_thickness * 0.47 * root_tuck * thick_taper

        ring_verts = []
        for r in range(sides):
            pos = pt + side * (PROFILE_X[r] * half_w) + out_vec * (PROFILE_Y[r] * half_t)
            ring_verts.append(bm.verts.new(pos))
        rings.append(ring_verts)

    tip_vert = bm.verts.new(spine_points[-1])
    bm.verts.ensure_lookup_table()

    for k in range(ring_count - 1):
        v0 = k / (count - 1)
        v1 = (k + 1) / (count - 1)
        for r in range(sides):
            rn = (r + 1) % sides
            face = bm.faces.new((rings[k][r], rings[k][rn], rings[k + 1][rn], rings[k + 1][r]))
            face.smooth = True
            u0, u1 = PROFILE_U[r], PROFILE_U[r + 1]
            uvs = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
            cols = [(v0, layer_ao, seed_val, 1.0), (v0, layer_ao, seed_val, 1.0),
                    (v1, layer_ao, seed_val, 1.0), (v1, layer_ao, seed_val, 1.0)]
            for loop, uv, col in zip(face.loops, uvs, cols):
                loop[uv_layer].uv = uv
                loop[color_layer] = col

    v_last = (ring_count - 1) / (count - 1)
    for r in range(sides):
        rn = (r + 1) % sides
        face = bm.faces.new((rings[-1][r], rings[-1][rn], tip_vert))
        face.smooth = True
        uvs = [(PROFILE_U[r], v_last), (PROFILE_U[r + 1], v_last), (0.5, 1.0)]
        cols = [(v_last, layer_ao, seed_val, 1.0), (v_last, layer_ao, seed_val, 1.0), (1.0, layer_ao, seed_val, 1.0)]
        for loop, uv, col in zip(face.loops, uvs, cols):
            loop[uv_layer].uv = uv
            loop[color_layer] = col

    bm.to_mesh(mesh)
    bm.free()
    return mesh


class AETHERSTRAND_OT_GenerateSculptedHair(bpy.types.Operator):
    bl_idname = "aetherstrand.generate_sculpted_hair"
    bl_label = "Generate Sculpted Hair + Clustered Rig"
    bl_description = "Generate 5-zone sculpted volumetric hair locks (or load PresetGeometry.json) skinned to clustered Unity Hair_ bone chains"
    bl_options = {'REGISTER', 'UNDO'}

    preset_geometry_json: bpy.props.StringProperty(name="PresetGeometry.json (Optional)", subtype='FILE_PATH', default="")
    head_height: bpy.props.FloatProperty(name="Head Height (m)", default=0.23, min=0.14, max=0.35)
    part_offset_x: bpy.props.FloatProperty(name="Part Offset X", default=0.032, min=-0.12, max=0.12)
    tip_flick: bpy.props.FloatProperty(name="Outward Tip Flick", default=0.038, min=0.0, max=0.16)
    lock_width: bpy.props.FloatProperty(name="Lock Width", default=0.028, min=0.01, max=0.12)
    lock_thickness: bpy.props.FloatProperty(name="Lock Thickness", default=0.011, min=0.004, max=0.06)
    target_chain_count: bpy.props.IntProperty(name="Bone Chains (Clustered)", default=44, min=12, max=120)
    bones_per_chain: bpy.props.IntProperty(name="Bones / Chain", default=5, min=3, max=8)

    def execute(self, context):
        rng = random.Random(200108)
        segs = self.bones_per_chain + 1

        # Optional scalp projection if a head mesh is currently selected
        head_obj = context.active_object if (context.active_object and context.active_object.type == 'MESH') else None
        head_bvh = None
        head_mat = None
        head_mat_inv = None
        if head_obj is not None:
            depsgraph = context.evaluated_depsgraph_get()
            head_bvh = BVHTree.FromObject(head_obj, depsgraph)
            head_mat = head_obj.matrix_world.copy()
            head_mat_inv = head_mat.inverted()

        if self.preset_geometry_json and os.path.isfile(bpy.path.abspath(self.preset_geometry_json)):
            locks = load_preset_geometry_json(bpy.path.abspath(self.preset_geometry_json), segs)
        else:
            locks = build_five_zone_locks(
                self.head_height, self.part_offset_x, self.tip_flick,
                self.lock_width, self.lock_thickness, segs, rng)

        if head_bvh is not None:
            for l in locks:
                delta = project_root_to_scalp(l["points"][0], head_bvh, head_mat_inv, head_mat) - l["points"][0]
                l["points"] = [p + delta for p in l["points"]]

        chains = cluster_locks_to_chains(locks, self.target_chain_count, rng)

        collection = bpy.data.collections.new("AetherStrand_SculptedHair")
        context.scene.collection.children.link(collection)

        arm_data = bpy.data.armatures.new("AetherStrand_HairRig")
        arm_obj = bpy.data.objects.new("AetherStrand_HairRig", arm_data)
        collection.objects.link(arm_obj)

        context.view_layer.objects.active = arm_obj
        bpy.ops.object.mode_set(mode='EDIT')
        head_bone = arm_data.edit_bones.new("Head")
        head_bone.head = Vector((0, 0, 0))
        head_bone.tail = Vector((0, 0, self.head_height))

        chain_bone_names = []
        for c_idx, ch in enumerate(chains):
            parent_eb = head_bone
            b_names = []
            pts = ch["points"]
            for k in range(segs - 1):
                b_name = f"Hair_{ch['zone']}_{c_idx:02d}_{k}"
                eb = arm_data.edit_bones.new(b_name)
                eb.head = pts[k]
                eb.tail = pts[k + 1]
                eb.parent = parent_eb
                eb.use_connect = (k > 0)
                eb.use_deform = (k > 0)
                parent_eb = eb
                b_names.append(b_name)
            chain_bone_names.append(b_names)

        bpy.ops.object.mode_set(mode='OBJECT')

        created_meshes = []
        sides_count = len(PROFILE_X) - 1
        for c_idx, ch in enumerate(chains):
            b_names = chain_bone_names[c_idx]
            for l_idx in ch["members"]:
                lk = locks[l_idx]
                mesh = create_volumetric_lock_mesh(
                    f"HairLock_{l_idx:03d}", lk["points"], lk["width"], lk["thickness"], lk["seed"], lk["layer_ao"])
                obj = bpy.data.objects.new(mesh.name, mesh)
                collection.objects.link(obj)

                for b_name in b_names:
                    obj.vertex_groups.new(name=b_name)
                for k in range(segs - 1):
                    vg_idx = max(1, min(k, len(b_names) - 1))
                    vg = obj.vertex_groups[b_names[vg_idx]]
                    ring_indices = list(range(k * sides_count, (k + 1) * sides_count))
                    vg.add(ring_indices, 1.0, 'REPLACE')
                obj.vertex_groups[b_names[-1]].add([(segs - 1) * sides_count], 1.0, 'REPLACE')

                mod = obj.modifiers.new(name="Armature", type='ARMATURE')
                mod.object = arm_obj
                created_meshes.append(obj)

        bpy.ops.object.select_all(action='DESELECT')
        for o in created_meshes:
            o.select_set(True)
        context.view_layer.objects.active = created_meshes[0]
        bpy.ops.object.join()
        created_meshes[0].name = "AetherStrand_SculptedHairMesh"
        created_meshes[0].parent = arm_obj

        self.report({'INFO'}, f"Generated {len(locks)} volumetric locks skinned to {len(chains)} clustered Unity Hair_ bone chains.")
        return {'FINISHED'}


class AETHERSTRAND_OT_CurvesToSculptedLocks(bpy.types.Operator):
    bl_idname = "aetherstrand.curves_to_locks"
    bl_label = "Convert Curves to 3D Sculpted Locks"
    bl_description = "Convert selected Bezier/NURBS hair curves into volumetric crescent-profile locks"
    bl_options = {'REGISTER', 'UNDO'}

    lock_width: bpy.props.FloatProperty(name="Lock Width", default=0.032, min=0.01, max=0.2)
    lock_thickness: bpy.props.FloatProperty(name="Lock Thickness", default=0.014, min=0.005, max=0.1)

    def execute(self, context):
        curve_objs = [o for o in context.selected_objects if o.type == 'CURVE']
        if not curve_objs:
            self.report({'WARNING'}, "Select one or more Curve objects first.")
            return {'CANCELLED'}

        rng = random.Random(77)
        converted = 0
        for obj in curve_objs:
            for idx, spline in enumerate(obj.data.splines):
                pts = [obj.matrix_world @ bp.co for bp in spline.bezier_points] if spline.type == 'BEZIER' else [
                    obj.matrix_world @ Vector((p.co.x, p.co.y, p.co.z)) for p in spline.points
                ]
                if len(pts) < 2:
                    continue
                mesh = create_volumetric_lock_mesh(
                    f"{obj.name}_Lock_{idx}", pts, self.lock_width, self.lock_thickness, rng.random())
                lock_obj = bpy.data.objects.new(mesh.name, mesh)
                context.scene.collection.objects.link(lock_obj)
                converted += 1

        self.report({'INFO'}, f"Converted {converted} guide curves into sculpted 3D locks.")
        return {'FINISHED'}


class AETHERSTRAND_OT_ExportUnityFBX(bpy.types.Operator):
    bl_idname = "aetherstrand.export_unity_fbx"
    bl_label = "Export Selected Hair FBX (Linear VCol)"
    bl_description = "Export selected hair rig + mesh to Unity FBX with Linear vertex colors preserved"

    filepath: bpy.props.StringProperty(subtype='FILE_PATH', default="AetherStrand_Hair.fbx")

    def invoke(self, context, event):
        context.window_manager.fileselect_add(self)
        return {'RUNNING_MODAL'}

    def execute(self, context):
        bpy.ops.export_scene.fbx(
            filepath=self.filepath,
            use_selection=True,
            object_types={'ARMATURE', 'MESH'},
            apply_scale_options='FBX_SCALE_ALL',
            add_leaf_bones=False,
            colors_type='LINEAR',
            bake_anim=False,
        )
        self.report({'INFO'}, f"Exported Unity FBX (Linear VCol) to {self.filepath}")
        return {'FINISHED'}


class AETHERSTRAND_PT_MainPanel(bpy.types.Panel):
    bl_label = "AetherStrand Sculpted Hair"
    bl_idname = "AETHERSTRAND_PT_main_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'AetherStrand'

    def draw(self, context):
        layout = self.layout
        layout.label(text="Volumetric Hair & Rig", icon='STRANDS')
        col = layout.column(align=True)
        col.operator("aetherstrand.generate_sculpted_hair", icon='OUTLINER_OB_HAIR')
        col.operator("aetherstrand.curves_to_locks", icon='CURVE_BEZCURVE')
        col.operator("aetherstrand.export_unity_fbx", icon='EXPORT')


classes = (
    AETHERSTRAND_OT_GenerateSculptedHair,
    AETHERSTRAND_OT_CurvesToSculptedLocks,
    AETHERSTRAND_OT_ExportUnityFBX,
    AETHERSTRAND_PT_MainPanel,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)


if __name__ == "__main__":
    register()
