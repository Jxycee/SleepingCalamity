# tools/extract_hairdeck_to_preset_json.py
import json
import math
from collections import deque
import bpy
import bmesh
from mathutils import Vector

# --- Configure for Tobin's Blender scene ---
HAIR_OBJ_NAME = "Tobin_HairDeck"         # HairDeck mesh object name
HEAD_PIVOT_WORLD = Vector((0.0, 0.0, 0.0))  # World position of Tobin's Head bone head
HEAD_FORWARD = Vector((0.0, -1.0, 0.0))  # Blender forward axis of Tobin's face
OUTPUT_JSON = "//PresetGeometry.json"    # Saved next to the .blend file
MIN_CARD_LEN = 0.02                      # Skip degenerate micro-strips (< 2 cm)
MIN_CARD_WIDTH = 0.004                   # Skip ultra-thin noise strips (< 4 mm)
WIDTH_SCALE = 0.85                       # Compensates for 3D crescent thickness


def extract_islands(bm):
    visited = set()
    islands = []
    for f in bm.faces:
        if f.index in visited:
            continue
        comp_verts = set()
        comp_faces = []
        q = deque([f])
        visited.add(f.index)
        while q:
            cur = q.popleft()
            comp_faces.append(cur)
            for v in cur.verts:
                comp_verts.add(v)
                for lf in v.link_faces:
                    if lf.index not in visited:
                        visited.add(lf.index)
                        q.append(lf)
        islands.append((comp_verts, comp_faces))
    return islands


def classify_zone(root_local, head_fwd):
    fwd = root_local.dot(head_fwd.normalized())
    if fwd > 0.03:
        return "Bangs"
    if root_local.z > 0.14 and abs(root_local.x) < 0.055:
        return "Crown"
    if fwd < -0.03:
        return "Rear"
    return "Sides"


def main():
    obj = bpy.data.objects.get(HAIR_OBJ_NAME)
    if obj is None or obj.type != 'MESH':
        raise RuntimeError(f"Mesh object '{HAIR_OBJ_NAME}' not found.")

    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    bm.verts.ensure_lookup_table()

    uv0_layer = bm.loops.layers.uv.active
    uv1_layer = bm.loops.layers.uv[1] if len(bm.loops.layers.uv) > 1 else None
    if uv0_layer is None:
        bm.free()
        raise RuntimeError("HairDeck mesh has no UV0 layer.")

    mat_w = obj.matrix_world
    islands = extract_islands(bm)
    locks = []

    for idx, (verts, faces) in enumerate(islands):
        # Gather per-vertex average UV0, UV1, and Head-local position
        v_data = {}
        u_vals = []
        seed_vals = []
        for f in faces:
            for loop in f.loops:
                v = loop.vert
                uv0 = loop[uv0_layer].uv
                u_vals.append(uv0.x)
                if uv1_layer is not None:
                    seed_vals.append(loop[uv1_layer].uv.x)
                if v.index not in v_data:
                    v_data[v.index] = [mat_w @ v.co - HEAD_PIVOT_WORLD, uv0.x, uv0.y, 1]
                else:
                    v_data[v.index][1] += uv0.x
                    v_data[v.index][2] += uv0.y
                    v_data[v.index][3] += 1

        # Group vertices into cross-section rows by quantized V (root 0 -> tip 1)
        rows = {}
        for pos, u_sum, v_sum, cnt in v_data.values():
            u = u_sum / cnt
            v = v_sum / cnt
            v_key = round(v, 2)
            rows.setdefault(v_key, []).append((u, pos))

        sorted_keys = sorted(rows.keys())
        if len(sorted_keys) < 2:
            continue

        spine_pts = []
        row_widths = []
        for k in sorted_keys:
            row = sorted(rows[k], key=lambda item: item[0])
            center = sum((p for _, p in row), Vector((0, 0, 0))) / len(row)
            spine_pts.append(center)
            if len(row) >= 2:
                row_widths.append((row[-1][1] - row[0][1]).length)

        total_len = sum((spine_pts[i + 1] - spine_pts[i]).length for i in range(len(spine_pts) - 1))
        max_w = max(row_widths) if row_widths else 0.0
        if total_len < MIN_CARD_LEN or max_w < MIN_CARD_WIDTH:
            continue

        is_core = (sum(u_vals) / max(1, len(u_vals))) > 1.5
        width = max_w * WIDTH_SCALE
        thickness = max(0.006, min(0.018, width * 0.38))
        seed = (sum(seed_vals) / len(seed_vals)) if seed_vals else ((idx * 0.6180339) % 1.0)

        locks.append({
            "zone": classify_zone(spine_pts[0], HEAD_FORWARD),
            "points": [[round(p.x, 5), round(p.y, 5), round(p.z, 5)] for p in spine_pts],
            "width": round(width, 5),
            "thickness": round(thickness, 5),
            "seed": round(float(seed), 4),
            "layerAO": 0.72 if is_core else 0.94,
        })

    bm.free()

    out_path = bpy.path.abspath(OUTPUT_JSON)
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump({"schemaVersion": "2.1", "source": HAIR_OBJ_NAME, "locks": locks}, f, indent=2)
    print(f"[AetherStrand] Extracted {len(locks)} lock spines to {out_path}")


if __name__ == "__main__":
    main()
