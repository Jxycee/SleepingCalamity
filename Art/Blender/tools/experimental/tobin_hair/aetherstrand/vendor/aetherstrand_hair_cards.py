bl_info = {
    "name": "AetherStrand — Sculpted Volumetric Hair & Unity Bone Rig Generator",
    "author": "AetherStrand Studio",
    "version": (3, 0, 0),
    "blender": (4, 0, 0),
    "location": "View3D > Sidebar (N) > AetherStrand",
    "description": "Imports exact AetherStrand PresetGeometry.json locks (or generates procedural crescent-profile clumps), clusters them into shared Unity Hair_ bone chains, and exports Unity FBX with Linear vertex colors.",
    "category": "Mesh",
}

import bpy
import bmesh
import json
import math
import os
import random
from mathutils import Vector


# Sleek Crescent sculpted cross-section profile (domed carved outer ridge + flatter hugging underside)
PROFILE_U = [0.0, 0.18, 0.36, 0.5, 0.64, 0.82, 1.0]
PROFILE_X = [0.0, 0.76, 1.0, 0.0, -1.0, -0.76, 0.0]
PROFILE_Y = [1.0, 0.54, -0.02, -0.26, -0.02, 0.54, 1.0]

ZONE_ORDER = ("Bangs", "Crown", "Sides", "Rear")


def sample_cubic_bezier(p0, p1, p2, p3, count):
    pts = []
    for i in range(count):
        t = i / max(1, count - 1)
        u = 1.0 - t
        p = (p0 * (u**3)) + (p1 * (3 * u * u * t)) + (p2 * (3 * u * t * t)) + (p3 * (t**3))
        pts.append(p)
    return pts


def resample_polyline(points, target_count):
    if not points:
        return []
    if len(points) == 1 or target_count <= 1:
        return [points[0].copy() for _ in range(max(1, target_count))]

    cum = [0.0]
    for i in range(1, len(points)):
        cum.append(cum[-1] + (points[i] - points[i - 1]).length)
    total_len = cum[-1]
    if total_len < 1e-6:
        return [points[0].copy() for _ in range(target_count)]

    out = []
    seg_idx = 0
    for k in range(target_count):
        target_d = (k / (target_count - 1)) * total_len
        while seg_idx < len(cum) - 2 and cum[seg_idx + 1] < target_d:
            seg_idx += 1
        d0, d1 = cum[seg_idx], cum[seg_idx + 1]
        alpha = (target_d - d0) / max(1e-6, d1 - d0)
        out.append(points[seg_idx].lerp(points[seg_idx + 1], alpha))
    return out


def lock_feature_vector(pts):
    """9D deterministic descriptor (root, mid, tip) for spatial clustering within a hair zone."""
    p0 = pts[0]
    pm = pts[len(pts) // 2]
    pt = pts[-1]
    return (
        p0.x * 1.15, p0.y * 1.15, p0.z * 1.15,
        pm.x * 1.0,  pm.y * 1.0,  pm.z * 1.0,
        pt.x * 0.95, pt.y * 0.95, pt.z * 0.95,
    )


def sq_dist_nd(a, b):
    return sum((x - y) * (x - y) for x, y in zip(a, b))


def deterministic_kmeans(features, k, iterations=14):
    """Deterministic farthest-point init + Lloyd k-means (zero randomness, 100% reproducible)."""
    n = len(features)
    if n == 0 or k <= 0:
        return []
    k = min(k, n)

    # Farthest-point seeding starting from deterministic lexicographically sorted index 0
    seed_indices = [0]
    min_dists = [sq_dist_nd(features[i], features[0]) for i in range(n)]
    for _ in range(1, k):
        best_idx = max(range(n), key=lambda idx: (min_dists[idx], -idx))
        seed_indices.append(best_idx)
        cand = features[best_idx]
        for i in range(n):
            d = sq_dist_nd(features[i], cand)
            if d < min_dists[i]:
                min_dists[i] = d

    dim = len(features[0])
    centroids = [list(features[idx]) for idx in seed_indices]
    assignments = [0] * n

    for _ in range(iterations):
        changed = False
        for i, feat in enumerate(features):
            best_c = min(range(k), key=lambda c: sq_dist_nd(feat, centroids[c]))
            if assignments[i] != best_c:
                assignments[i] = best_c
                changed = True

        sums = [[0.0] * dim for _ in range(k)]
        counts = [0] * k
        for i, c in enumerate(assignments):
            counts[c] += 1
            f = features[i]
            s = sums[c]
            for d in range(dim):
                s[d] += f[d]

        for c in range(k):
            if counts[c] > 0:
                inv = 1.0 / counts[c]
                centroids[c] = [v * inv for v in sums[c]]

        if not changed:
            break

    return assignments


def allocate_zone_chains(zone_counts, target_total):
    """Distributes ~target_total shared bone chains proportionally across active hair zones."""
    active_zones = [z for z in ZONE_ORDER if zone_counts.get(z, 0) > 0]
    if not active_zones:
        return {}

    total_locks = sum(zone_counts[z] for z in active_zones)
    target = min(max(len(active_zones), target_total), total_locks)

    alloc = {}
    remainders = []
    used = 0
    for z in active_zones:
        exact = (zone_counts[z] / total_locks) * target
        base = max(1, min(zone_counts[z], int(math.floor(exact))))
        alloc[z] = base
        used += base
        remainders.append((exact - base, z))

    # Adjust up or down deterministically to hit exact target
    while used < target:
        remainders.sort(key=lambda item: (-item[0], ZONE_ORDER.index(item[1])))
        progressed = False
        for idx, (rem, z) in enumerate(remainders):
            if alloc[z] < zone_counts[z]:
                alloc[z] += 1
                used += 1
                remainders[idx] = (rem - 1.0, z)
                progressed = True
                if used >= target:
                    break
        if not progressed:
            break

    while used > target:
        candidates = sorted(
            [z for z in active_zones if alloc[z] > 1],
            key=lambda z: (-alloc[z], ZONE_ORDER.index(z)),
        )
        if not candidates:
            break
        alloc[candidates[0]] -= 1
        used -= 1

    return alloc


def create_volumetric_lock_mesh(name, spine_points, base_width, base_thickness, seed_val, layer_ao=0.95, is_base_shell=False):
    """Creates a true 3D sculpted volumetric crescent-profile hair lock with a razor-pointed tip."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    uv_layer = bm.loops.layers.uv.new("UVMap")
    color_layer = bm.loops.layers.color.new("Col")
    alpha_mask = 0.0 if is_base_shell else 1.0

    count = len(spine_points)
    ring_count = count - 1
    sides = len(PROFILE_X) - 1  # 6 faces
    rings = []

    head_center = Vector((0.0, 0.01, 0.425))
    ref_out = (spine_points[0] - head_center)
    ref_out = ref_out.normalized() if ref_out.length > 0.001 else Vector((0.0, 0.0, 1.0))

    for k in range(ring_count):
        pt = spine_points[k]
        t = k / max(1, count - 1)
        if k == 0:
            tang = (spine_points[1] - pt)
        else:
            tang = (spine_points[k + 1] - spine_points[k - 1])
        tang = tang.normalized() if tang.length > 1e-6 else Vector((0.0, 0.0, -1.0))

        side = tang.cross(ref_out)
        if side.length < 0.001:
            side = Vector((1.0, 0.0, 0.0))
        else:
            side.normalize()
        out_vec = side.cross(tang).normalized()
        ref_out = ref_out.lerp(out_vec, 0.35).normalized()

        # Exact viewport sculpted taper (cohesive root->mid body, razor-pointed tip)
        root_tuck = 0.7 + 0.3 * math.sin(min(1.0, t * 4.5) * math.pi * 0.5)
        if t < 0.44:
            width_taper = 1.0 - t * 0.14
        else:
            width_taper = (((1.0 - t) / 0.56) ** 0.94) * 0.938
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
            cols = [
                (v0, layer_ao, seed_val, alpha_mask),
                (v0, layer_ao, seed_val, alpha_mask),
                (v1, layer_ao, seed_val, alpha_mask),
                (v1, layer_ao, seed_val, alpha_mask),
            ]
            for loop, uv, col in zip(face.loops, uvs, cols):
                loop[uv_layer].uv = uv
                loop[color_layer] = col

    # Converging pointed tip cap
    v_last = (ring_count - 1) / (count - 1)
    for r in range(sides):
        rn = (r + 1) % sides
        face = bm.faces.new((rings[-1][r], rings[-1][rn], tip_vert))
        face.smooth = True
        uvs = [(PROFILE_U[r], v_last), (PROFILE_U[r + 1], v_last), (0.5, 1.0)]
        cols = [
            (v_last, layer_ao, seed_val, alpha_mask),
            (v_last, layer_ao, seed_val, alpha_mask),
            (1.0, layer_ao, seed_val, alpha_mask),
        ]
        for loop, uv, col in zip(face.loops, uvs, cols):
            loop[uv_layer].uv = uv
            loop[color_layer] = col

    bm.to_mesh(mesh)
    bm.free()
    return mesh


def resolve_default_geometry_json():
    try:
        script_dir = os.path.dirname(os.path.abspath(__file__))
        candidate = os.path.join(script_dir, "PresetGeometry.json")
        if os.path.isfile(candidate):
            return candidate
    except Exception:
        pass
    return ""


def load_lock_specs_from_json(filepath):
    with open(filepath, "r", encoding="utf-8") as fh:
        payload = json.load(fh)
    raw_locks = payload.get("locks", [])
    specs = []
    for entry in raw_locks:
        pts_raw = entry.get("points", [])
        if len(pts_raw) < 2:
            continue
        pts = [Vector((float(p[0]), float(p[1]), float(p[2]))) for p in pts_raw]
        zone = entry.get("zone", "Sides")
        if zone not in ZONE_ORDER:
            zone = "Sides"
        specs.append({
            "zone": zone,
            "width": float(entry.get("width", 0.04)),
            "thickness": float(entry.get("thickness", 0.013)),
            "seed": float(entry.get("seed", 0.5)),
            "layerAO": float(entry.get("layerAO", 0.92)),
            "isBaseShell": bool(entry.get("isBaseShell", False)),
            "points": pts,
        })
    return specs, payload.get("presetId", "layered_swept_long")


def build_procedural_fallback_specs(lock_count, part_offset_x, tip_flick, lock_width, lock_thickness, segs):
    rng = random.Random(200108)
    specs = []
    for s in range(lock_count):
        side = -1.0 if s < int(lock_count * 0.56) else 1.0
        u = (s % max(1, lock_count // 2)) / max(1, (lock_count // 2) - 1)
        y_root = -0.08 + u * 0.19
        root = Vector((part_offset_x + side * 0.012, y_root, 0.68 - ((u - 0.35) ** 2) * 0.08))

        angle = 0.24 + u * (math.pi * 0.85)
        dx = side * math.sin(angle)
        dy = -math.cos(angle)
        reach = 0.16 + math.sin(u * math.pi) * 0.05 + rng.uniform(-0.015, 0.025)
        drop = 0.22 + (s % 3) * 0.045 + rng.uniform(0.0, 0.04)

        p1 = root + Vector((dx * 0.08, dy * 0.07, 0.048))
        p2 = root + Vector((dx * reach, dy * reach, -drop * 0.38))
        p3 = root + Vector((dx * (reach + tip_flick), dy * (reach + tip_flick * 0.6), -drop))
        pts = sample_cubic_bezier(root, p1, p2, p3, segs)

        if p3.y < -0.10:
            zone = "Bangs"
        elif u < 0.30:
            zone = "Crown"
        elif p3.y > 0.08:
            zone = "Rear"
        else:
            zone = "Sides"

        specs.append({
            "zone": zone,
            "width": lock_width * rng.uniform(0.82, 1.22),
            "thickness": lock_thickness * rng.uniform(0.85, 1.15),
            "seed": rng.random(),
            "layerAO": 0.85 + (s % 2) * 0.15,
            "isBaseShell": False,
            "points": pts,
        })
    return specs


class AETHERSTRAND_OT_GenerateSculptedHair(bpy.types.Operator):
    bl_idname = "aetherstrand.generate_sculpted_hair"
    bl_label = "Generate Sculpted Hair + Clustered Rig"
    bl_description = "Auto-loads PresetGeometry.json (or procedural fallback), builds all crescent locks, and k-means clusters them onto shared Hair_<Zone>_<k>_<i> chains"
    bl_options = {'REGISTER', 'UNDO'}

    geometry_json_path: bpy.props.StringProperty(
        name="PresetGeometry.json",
        description="Optional path to PresetGeometry.json (auto-detected beside addon script if blank)",
        default="",
        subtype='FILE_PATH',
    )
    target_chains: bpy.props.IntProperty(name="Shared Bone Chains", default=44, min=8, max=96)
    bones_per_chain: bpy.props.IntProperty(name="Bones / Chain", default=5, min=3, max=8)
    fallback_lock_count: bpy.props.IntProperty(name="Fallback Locks", default=120, min=24, max=280)
    part_offset_x: bpy.props.FloatProperty(name="Part Offset X", default=0.032, min=-0.12, max=0.12)
    tip_flick: bpy.props.FloatProperty(name="Outward Tip Flick", default=0.038, min=0.0, max=0.16)
    lock_width: bpy.props.FloatProperty(name="Fallback Width", default=0.040, min=0.01, max=0.12)
    lock_thickness: bpy.props.FloatProperty(name="Fallback Thickness", default=0.013, min=0.004, max=0.06)

    def execute(self, context):
        json_path = bpy.path.abspath(self.geometry_json_path) if self.geometry_json_path else resolve_default_geometry_json()
        preset_tag = "layered_swept_long"
        loaded_from_json = False

        if json_path and os.path.isfile(json_path):
            specs, preset_tag = load_lock_specs_from_json(json_path)
            loaded_from_json = True
        else:
            specs = build_procedural_fallback_specs(
                self.fallback_lock_count,
                self.part_offset_x,
                self.tip_flick,
                self.lock_width,
                self.lock_thickness,
                self.bones_per_chain + 1,
            )

        if not specs:
            self.report({'ERROR'}, "No valid hair locks found to generate.")
            return {'CANCELLED'}

        collection = bpy.data.collections.new(f"AetherStrand_{preset_tag}")
        context.scene.collection.children.link(collection)

        arm_data = bpy.data.armatures.new("AetherStrand_HairRig")
        arm_obj = bpy.data.objects.new("AetherStrand_HairRig", arm_data)
        collection.objects.link(arm_obj)

        # Group dynamic (non-base-shell) locks by zone and k-means cluster onto ~44 shared bone chains
        zone_indices = {z: [] for z in ZONE_ORDER}
        for idx, sp in enumerate(specs):
            if not sp["isBaseShell"]:
                zone_indices[sp["zone"]].append(idx)

        zone_counts = {z: len(zone_indices[z]) for z in ZONE_ORDER}
        chain_alloc = allocate_zone_chains(zone_counts, self.target_chains)

        lock_chain_map = {}  # lock_idx -> list of bone names along its assigned shared chain
        chain_definitions = []  # (chain_bone_names, mean_spine_pts)

        for zone in ZONE_ORDER:
            indices = zone_indices[zone]
            k_target = chain_alloc.get(zone, 0)
            if not indices or k_target <= 0:
                continue

            features = [lock_feature_vector(specs[i]["points"]) for i in indices]
            assignments = deterministic_kmeans(features, k_target, iterations=14)

            clusters = {}
            for local_i, cluster_id in enumerate(assignments):
                clusters.setdefault(cluster_id, []).append(indices[local_i])

            # Deterministic cluster ordering by mean root azimuth & height
            ordered_clusters = []
            for cid, members in clusters.items():
                resampled = [resample_polyline(specs[m]["points"], self.bones_per_chain + 1) for m in members]
                mean_spine = []
                for step in range(self.bones_per_chain + 1):
                    acc = Vector((0.0, 0.0, 0.0))
                    for r_pts in resampled:
                        acc += r_pts[step]
                    mean_spine.append(acc / len(resampled))
                root_pt = mean_spine[0]
                sort_key = (math.atan2(root_pt.x, -root_pt.y), -root_pt.z)
                ordered_clusters.append((sort_key, members, mean_spine))

            ordered_clusters.sort(key=lambda item: item[0])

            for c_idx, (_, members, mean_spine) in enumerate(ordered_clusters):
                bone_names = [f"Hair_{zone}_{c_idx:02d}_{b}" for b in range(self.bones_per_chain)]
                chain_definitions.append((bone_names, mean_spine))
                for m in members:
                    lock_chain_map[m] = bone_names

        # Build Armature EditBones (5 bones/chain, use_deform = (k > 0))
        context.view_layer.objects.active = arm_obj
        bpy.ops.object.mode_set(mode='EDIT')
        head_bone = arm_data.edit_bones.new("Head")
        head_bone.head = Vector((0.0, 0.0, 0.0))
        head_bone.tail = Vector((0.0, 0.0, 0.425))
        head_bone.use_deform = True

        for bone_names, spine in chain_definitions:
            parent_eb = head_bone
            for k, b_name in enumerate(bone_names):
                eb = arm_data.edit_bones.new(b_name)
                eb.head = spine[k]
                tail_pt = spine[k + 1]
                if (tail_pt - eb.head).length < 1e-4:
                    tail_pt = eb.head + Vector((0.0, 0.0, -0.02))
                eb.tail = tail_pt
                eb.parent = parent_eb
                eb.use_connect = (k > 0)
                eb.use_deform = (k > 0)
                parent_eb = eb

        bpy.ops.object.mode_set(mode='OBJECT')

        # Build volumetric crescent lock meshes & bind weights to shared chains (base shell -> Head)
        created_meshes = []
        sides_count = len(PROFILE_X) - 1

        for idx, sp in enumerate(specs):
            pts = sp["points"]
            mesh = create_volumetric_lock_mesh(
                f"HairLock_{idx:03d}",
                pts,
                sp["width"],
                sp["thickness"],
                sp["seed"],
                sp["layerAO"],
                sp["isBaseShell"],
            )
            obj = bpy.data.objects.new(mesh.name, mesh)
            collection.objects.link(obj)

            vg_head = obj.vertex_groups.new(name="Head")
            ring_count = len(pts) - 1
            tip_vert_idx = ring_count * sides_count

            if sp["isBaseShell"] or idx not in lock_chain_map:
                all_verts = list(range(tip_vert_idx + 1))
                vg_head.add(all_verts, 1.0, 'REPLACE')
            else:
                bone_names = lock_chain_map[idx]
                deform_bones = bone_names[1:]  # use_deform=True bones (k = 1..4)
                deform_vgs = [obj.vertex_groups.new(name=bn) for bn in deform_bones]
                n_def = len(deform_vgs)

                # Pin root ring (k = 0) 100% to Head so scalp roots never detach
                vg_head.add(list(range(0, sides_count)), 1.0, 'REPLACE')

                for k in range(1, ring_count):
                    t = k / max(1, len(pts) - 1)
                    ring_indices = list(range(k * sides_count, (k + 1) * sides_count))
                    # Smooth root transition from Head into first deform bone
                    head_w = max(0.0, 1.0 - t * 4.5)
                    chain_w = 1.0 - head_w
                    if head_w > 0.001:
                        vg_head.add(ring_indices, head_w, 'REPLACE')

                    f_idx = min(n_def - 1.0, max(0.0, t * (n_def - 1)))
                    b0 = int(math.floor(f_idx))
                    b1 = min(n_def - 1, b0 + 1)
                    frac = f_idx - b0
                    if b0 == b1:
                        deform_vgs[b0].add(ring_indices, chain_w, 'REPLACE')
                    else:
                        deform_vgs[b0].add(ring_indices, chain_w * (1.0 - frac), 'REPLACE')
                        deform_vgs[b1].add(ring_indices, chain_w * frac, 'REPLACE')

                deform_vgs[-1].add([tip_vert_idx], 1.0, 'REPLACE')

            created_meshes.append(obj)

        bpy.ops.object.select_all(action='DESELECT')
        for o in created_meshes:
            o.select_set(True)
        context.view_layer.objects.active = created_meshes[0]
        bpy.ops.object.join()

        joined_obj = created_meshes[0]
        joined_obj.name = "AetherStrand_SculptedHairMesh"
        joined_obj.parent = arm_obj
        mod = joined_obj.modifiers.new(name="Armature", type='ARMATURE')
        mod.object = arm_obj

        src_label = "PresetGeometry.json" if loaded_from_json else "procedural fallback"
        self.report(
            {'INFO'},
            f"Built {len(specs)} volumetric locks ({src_label}) clustered onto {len(chain_definitions)} shared Hair_ bone chains.",
        )
        return {'FINISHED'}


class AETHERSTRAND_OT_ExportUnityFbx(bpy.types.Operator):
    bl_idname = "aetherstrand.export_unity_fbx"
    bl_label = "Export Unity FBX (Linear VCol)"
    bl_description = "Export AetherStrand hair mesh & rig to Unity FBX using linear vertex colors (colors_type='LINEAR') and add_leaf_bones=False"
    bl_options = {'REGISTER'}

    filepath: bpy.props.StringProperty(
        name="FBX Output Path",
        default="//AetherStrand_layered_swept_long.fbx",
        subtype='FILE_PATH',
    )

    def invoke(self, context, event):
        context.window_manager.fileselect_add(self)
        return {'RUNNING_MODAL'}

    def execute(self, context):
        arm_obj = bpy.data.objects.get("AetherStrand_HairRig")
        mesh_obj = bpy.data.objects.get("AetherStrand_SculptedHairMesh")
        if arm_obj is not None and mesh_obj is not None:
            bpy.ops.object.select_all(action='DESELECT')
            arm_obj.select_set(True)
            mesh_obj.select_set(True)
            context.view_layer.objects.active = arm_obj

        out_path = bpy.path.abspath(self.filepath)
        if not out_path.lower().endswith(".fbx"):
            out_path += ".fbx"

        bpy.ops.export_scene.fbx(
            filepath=out_path,
            use_selection=True,
            object_types={'ARMATURE', 'MESH'},
            apply_scale_options='FBX_SCALE_ALL',
            mesh_smooth_type='FACE',
            use_tspace=True,
            colors_type='LINEAR',
            path_mode='STRIP',
            add_leaf_bones=False,
            use_armature_deform_only=False,
            bake_anim=False,
            axis_forward='-Z',
            axis_up='Y',
        )
        self.report({'INFO'}, f"Exported Unity FBX (Linear VCol, no leaf bones) to {out_path}")
        return {'FINISHED'}


class AETHERSTRAND_OT_CurvesToSculptedLocks(bpy.types.Operator):
    bl_idname = "aetherstrand.curves_to_locks"
    bl_label = "Convert Curves to 3D Sculpted Locks"
    bl_description = "Convert selected Bezier/NURBS hair curves into volumetric crescent-profile locks"
    bl_options = {'REGISTER', 'UNDO'}

    lock_width: bpy.props.FloatProperty(name="Lock Width", default=0.05, min=0.01, max=0.2)
    lock_thickness: bpy.props.FloatProperty(name="Lock Thickness", default=0.025, min=0.005, max=0.1)

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
                    f"{obj.name}_Lock_{idx}", pts, self.lock_width, self.lock_thickness, rng.random()
                )
                lock_obj = bpy.data.objects.new(mesh.name, mesh)
                context.scene.collection.objects.link(lock_obj)
                converted += 1

        self.report({'INFO'}, f"Converted {converted} guide curves into sculpted 3D hair locks.")
        return {'FINISHED'}


class AETHERSTRAND_PT_MainPanel(bpy.types.Panel):
    bl_label = "AetherStrand Sculpted Hair"
    bl_idname = "AETHERSTRAND_PT_main_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'AetherStrand'

    def draw(self, context):
        layout = self.layout
        layout.label(text="Volumetric Sculpted Hair", icon='STRANDS')
        col = layout.column(align=True)
        col.operator("aetherstrand.generate_sculpted_hair", icon='OUTLINER_OB_HAIR')
        col.operator("aetherstrand.export_unity_fbx", icon='EXPORT')
        col.operator("aetherstrand.curves_to_locks", icon='CURVE_BEZCURVE')


classes = (
    AETHERSTRAND_OT_GenerateSculptedHair,
    AETHERSTRAND_OT_ExportUnityFbx,
    AETHERSTRAND_OT_CurvesToSculptedLocks,
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
