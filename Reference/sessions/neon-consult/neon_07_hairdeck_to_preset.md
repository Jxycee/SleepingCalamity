# Matching Tobin's Existing Cut: HairDeck Mesh → `PresetGeometry.json`
(Neon, exchange 7, 2026-11-22. Verbatim from the Gemini thread.)

## 1. Packaging Note
* Noted on `.asmdef`—v2.2 will ship both `AetherStrand_Unity/Runtime/AetherStrand.Runtime.asmdef` and `AetherStrand_Unity/Editor/AetherStrand.Editor.asmdef` (editor-only, referencing `AetherStrand.Runtime`).

## 2. Yes—Extracting `PresetGeometry.json` From HairDeck Is Straightforward
Because Tobin's HairDeck mesh already encodes `uv0.y` (`0` = root, `1` = tip), `uv0.x > 1.5` (core ribbons), and `uv1.x` (per-strand random), each disconnected quad-strip island gives you an exact spine centerline, width, core/outer `layerAO`, and `seed`.

### Key Rules for a 1:1 Cut Match
* **Do not run scalp BVH re-projection** when loading the extracted JSON (leave no head mesh active in Blender before running `Generate Sculpted Hair + Clustered Rig`): HairDeck's card roots are already fitted to Tobin's MetaPerson skull.
* **Width & thickness scaling:** Multiply extracted card width by `0.85` (3D crescent locks have volume, so 100% flat-card width reads slightly thicker) and set `thickness = clamp(width * 0.38, 0.006, 0.018)`.
* **Core vs. outer ribbons:** Map `uv0.x > 1.5` (core ribbons) to `layerAO = 0.72` and outer ribbons to `layerAO = 0.94` so interior volume depth carries over automatically.
* **Chain clustering stays fast:** Even if HairDeck has 180–300 cards, `aetherstrand_hair_cards.py`'s `cluster_locks_to_chains` still clusters all of them onto `target_chain_count = 44` shared bone chains, keeping runtime physics cost flat.
* **Axis check:** Verify which Blender axis Tobin's MetaPerson face points along (`HEAD_FORWARD = Vector((0, -1, 0))` is Blender default; flip to `(0, 1, 0)` if facing `+Y`) so `Bangs` vs. `Rear` zone labels classify accurately.

## 3. Blender Extraction Script (`HairDeck` → `PresetGeometry.json`)

```python
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
```

## 4. Handoff Flow for the Side-by-Side Trial
1. Run `extract_hairdeck_to_preset_json.py` on Tobin's existing HairDeck `.blend` (setting `HEAD_PIVOT_WORLD` to his `Head` bone world position).
2. Deselect all objects (so BVH scalp re-projection stays off), run **Generate Sculpted Hair + Clustered Rig** pointing `preset_geometry_json` at the generated `PresetGeometry.json` (`target_chain_count = 44`, `bones_per_chain = 5`).
3. Export via **Export Selected Hair FBX (Linear VCol)** and run `AetherStrandSetupWindow.CreateMaterialAsset` + `AutoSetupPhysics` on the trial Tobin prefab.
4. Send back the feedback file (compile errors, audit numbers, daylight/torch captures, and motion strips) and I'll fold every fix plus the two `.asmdef` files into v2.2.

## Coordinator note
The zone classifier treats Blender Z as up (`root_local.z > 0.14`) and its thresholds are in metres from the Head pivot. Check them against the MetaPerson head scale. The `v_key = round(v, 2)` row grouping assumes rows of cards sit on shared V values; HairDeck cards with jittered V would merge or split rows, so check the lock count against the card count.
