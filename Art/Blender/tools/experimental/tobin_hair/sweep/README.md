# Tobin hair sweep-and-score

One command turns a config of HairDeck variants into per-variant numeric scores, a ranked CSV and one comparison sheet (top 4 beside the
reference). A judge reads the sheet, edits the config (add / drop / tweak variants) and re-runs: only new or changed variants are computed.

    python Art/Blender/tools/experimental/tobin_hair/sweep/hair_sweep.py <config.json> --out C:/AI/test_out/heroes/tobin/hair_sweep
    # flags: --force (ignore cache)  --only id,id  --sheet-only (re-rank + redraw from cached scores, e.g. after changing weights)

Driver is stdlib-only python; it launches the HairDeck venv (hd_strands), the trellis venv (hd_mix, hd_opt, scoring, sheet) and Blender 5.2
(hd_fit, sweep_render) one variant at a time. Tool paths default to the PC install; override under `tools` in the config.
Outputs go to `<out>/round<N>/`: `scores.csv` (ranked, one row per variant, failed ones last with `error`), `ranking.json`, `sheet.png`,
`variants/<id>/` (cards.json, strands.npz, opt/, hd_fit.blend, prev_*.png, hair_*.png, scores.json, logs, `.key_*` cache keys). Nothing is written into the repo.

## Config
- `round`, `top` (variants on the sheet), `jut_mm` (jutting threshold, default 6), `tools` (path overrides), `weights`, `defaults`, `variants`.
- variant: `id`, `label`, and one of
  - `cards`: a HairDeck cards json (relative to `tools.cards_root` = `C:/AI/test_out/heroes/tobin/hairdeck`),
  - `mix`: `{a, b, args:[...]}` = `hd_mix.py a b out args` (the current build's recipe is in `config_round1.json`, `base_*`).
  - `strands`: hd_strands options, e.g. `{"side_ext": 0.5, "per": 0.0062}`.
  - `opt`: `w` (silhouette weights front,side,back,frame), `rounds`, `fix` (e.g. `{"sx":0.85,"lt":0.03}`: those parameters start there and stay fixed; fix all 7 with `rounds: 0` to reproduce a known build).
  - `fit`: hd_fit args, e.g. `{"clear": 0.004}`.
  - `tint`: `{"root":"#1c130d","tip":"#33231a"}` gradient along the card for the preview material (the Unity shader is not used).
  `defaults` holds the same sections and is merged under each variant.
- New HairDeck variants: `C:/AI/hairdeck/.venv/Scripts/python.exe C:/AI/hairdeck/scripts/image_to_hair.py <crop.png> --out <dir> --n 4 --seed S`, then point `cards` at the new json.

## Scores (scores.csv columns)
- `iou_front/side/back/frame`, `iou_mean`, `over_*`, `miss_*`: hd_sil.py against the hull masks (above z 1.68; frame = cheek band outside the face).
- `jut_<view>_frac`, `jut_<view>_max_mm`: hair vertices (z > 1.60) farther than `jut_mm` outside the reference silhouette. `jut_side_*` is the front-card-sticking-out-in-side-view number; back and front are reported too.
- `dE_<view>`, `dL_<view>`, `dE_mean`: Lab distance (L weighted 0.5) between the mean colour of the rendered hair (hair-only alpha pass, head as holdout) and the reference hair region (reference mask above z 1.68, includes some forehead skin). `hue_diff_back`, `chroma_ratio_back`, `warm_back` (1 = back hue > 8 degrees warmer or chroma > 1.4x the reference).
- `clear_pushed_frac`, `clear_worst_mm` (hd_fit: verts inside the head before the 2 mm push), `cover` (rays hitting hair), `tris`, `verts`, `crown_cm` (hair top over skull top), `ear_cover` (share of ear verts with hair outside them; approximates the builder's EARS test; the builder's own CLEARANCE/EARS/CROWN need the full shell build and are not run). Chain count needs rig_export and is not run.
- `rough_front/side/back`, `rough_ref_<view>`: outline roughness = perimeter / convex-hull perimeter of the largest blob of the hair mask (5 px open first) for the render and the reference hull mask; smooth bowl about 1.0-1.1, spiky layered outline more. Component `spike` = clip((mean(rough_side, rough_back) - 1.05) / 0.6, 0, 1).
- `rank` = weighted mean of components in 0..1 (config `weights`; missing = 0): `iou_mean`, `iou_frame`, `iou_min` (worst view), `jut` (1 - jut_side_frac/0.10), `color` (1 - dE_mean/30), `warm` (0 if flagged), `clear` (1 - pushed_frac/0.5), `cover`, `spike`.

## Other characters
Copy `config_round1.json`, change `tools` (`head` npz from `hd_dump.py`, `hull` dir with mask_front/side/back.npy + hull_cal.json from `shell/hull_prep.py`, `refs` dir with crop_front/side/back.png whose size equals the masks, `cards_root`). hd_fit.py hard-codes TobinBase.fbx and the cranium z ranges; those need parameters for a new head.

## Approximations
Reference masks are the existing hull masks (no SAM masks). Preview lighting is a fixed sun + fill, Standard view transform; colour distance is meaningful for ranking, not absolute. Previews are EEVEE, 700 px.

## Unity colour round (one blocking command)
`unity_look.json` holds the Unity hair look (shadow_lift, hue_shift, saturation, value, root/mid/tip tint, alpha_cut; defaults = the installed look). `TobinHairMaterialBuilder.BuildCards` reads it (override the path with env `SC_HAIR_LOOK`) and sets the `SC_HairCards` knobs.

    C:/AI/trellis2/venv/Scripts/python.exe Art/Blender/tools/experimental/tobin_hair/sweep/unity_look_iter.py --tag r1   # edit unity_look.json between runs

It shoots rest-pose front/side/back (`SC_CAPTURE_ONLY=rest_f,rest_s,rest_b`, hair-hidden twins via `SC_HAIRMASK=1`) through this worktree's `Tools/unity.ps1` (about 3-6 min, blocks), then writes `<out>/<tag>/sheet.png` (reference row over capture row, ~1100 px) and prints hue/sat/val of the hair region, capture vs reference, per view (`colour.txt`/`colour.json`). Default out: `C:/AI/test_out/heroes/tobin/hair_sweep/look`. Mesh shape is not touched; the reference region includes some forehead skin.
