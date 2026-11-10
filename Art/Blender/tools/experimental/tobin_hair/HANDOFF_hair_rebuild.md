# Handoff: rebuild Tobin's hair (sheet shell + wisp locks)

Written 2026-11-10 for a fresh session. Branch `claude/face-template-spike`, worktree `C:/Dev/SleepingCalamity/.claude/worktrees/procgen-next-73e90d` (last commit bb07181a).

## Goal (user's words, summarized)
Rebuild Tobin's hair so it matches the reference image of Tobin. Model the hair **separately and stitch it to the skull**. It must **lay on the head almost like a sheet** (hugging the scalp so nothing clips through the head) while keeping the **volume and wispiness** of the reference. Tobin's base head (MetaPerson TobinBase) is final; only the hair remains. The user said "stop stopping": iterate on your own until it is good; do not pause for check-ins. The previous attempt (DiffLocks strands from a photoreal portrait -> cards, bb07181a) was rejected as "looks nothing like the reference": do not use DiffLocks strands as the style source.

## Reference (look at it first)
Contact sheet already made: `C:/AI/test_out/heroes/v2_eval/ref_heads.png`. Sources: `C:/AI/test_out/heroes/tobin/difflocks_v1/head_crop_in.png` (front crop), `C:/AI/test_out/heroes/tobin/refs/ref_front2.webp`, `ref_side_left.webp`, `ref_back.webp`, `C:/AI/test_out/heroes/tobin/reference_front.webp`.
What it shows:
- Short, messy, layered brown hair (mid chestnut with lighter highlights), clearly voluminous on top (about 3-4 cm above the skull).
- Part a bit to the character's left of centre. The fringe sweeps from the part diagonally across the forehead to the character's right, covering that brow and falling to about eye level with pointed tips. A few shorter locks on the other side of the part frame the other brow.
- Top: 2-3 thin wispy spikes/flicks sticking up and slightly back.
- Sides: layered pointed locks that cover the ears and hang to about cheekbone/jaw, tips pointing down and slightly inward, flared outline; a few thin strands hang longer in front of the ears.
- Back: short layered cap ending at the top of the nape in flicked pointed layers; the nape/neck is visible below. Side view: hair sits back over the skull, fringe falls forward in front of the ear.

## Build plan (all in Blender, headless python)
Blender 5.2: `C:/Program Files/Blender Foundation/Blender 5.2/blender.exe`, run `-b --factory-startup -P script.py -- args`. Blender's python has numpy but no scipy/cv2 (the trellis venv python `C:/AI/trellis2/venv/Scripts/python.exe` has cv2/numpy/scipy for image work).
1. **Hair shell (the sheet).** Take the scalp region of `AvatarHead`, build a smooth layer offset outward from the head surface along normals with a hand-authored thickness profile: thin at the hairline, thickest at crown and upper sides, flaring over the ears, with a layered lower edge at jaw level on the sides and at the top of the nape at the back. Minimum clearance about 3-5 mm everywhere, so it cannot clip the head. Add clump structure (lock separation as geometry and/or normal+albedo streaks) and a visible part line.
2. **Wisp locks.** Authored guide curves written in code to match the reference (swept fringe locks, side locks, crown spikes, nape flicks) -> tapered ribbon cards with alpha-cut tips, rooted inside the shell, pushed off the head by BVH clearance (as in `hair_rig2.py`/`hair_rig3.py`). Roughly: shell 10-15k tris, cards about 10k.
3. **Texture.** Authored strand/clump atlas with alpha. The procedural one in `cards/hair_atlas.py` is too stair-stepped: make smoother, natural clump streaks, chestnut with warm highlights, darker roots, soft tip fade. Shell uses an opaque clump texture, cards use alpha cut.
4. **Rig.** Reuse the spring-chain pipeline: `hair_rig3.py` (args `src=<blend> fit=0 debris=0 shards=0 clear=0.005 K=48`; scalp-near parts stay rigid; outward orientation; head clearance push; scalp cap `Tobin_HairCap` that may be unnecessary once the shell exists) and `export_fbx2.py` (writes `TobinHairMesh.fbx` with a minimal armature: Head + chains). Single-sided material with every face oriented outward: back faces were the black/navy chunks earlier.
5. **Unity.** `Assets/_Project/Content/Characters/TobinBase/HairMesh/` (TobinHairMesh.fbx, Textures/Hair_Cards_*.png, Materials), importer `TobinHairMeshImporter` (scale 1.0425), `TobinHairMaterialBuilder` (URP Lit alpha-clip card material; add an opaque material for the shell), `Presentation/Characters/TobinHairSpringChains.cs` (hair receives no shadows by default; `weight` 0 = rigid).
6. **Verify and iterate.** Unity capture through the wrapper:
   `powershell -NoProfile -ExecutionPolicy Bypass -File Tools\unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.TobinHairMotionCapture.Run -OutDir <scratch dir>` with env `SC_OUT_DIR=<dir> SC_REAL=1 SC_HQ=1 SC_SIZE=1400`.
   It renders rest/yawL/mid/yawR/nod/settle x front/3-4/side/back with three-point light, 8x MSAA and the real TobinBase skin. Diagnostics: `SC_MATVAR=cullback|cullfront|nonorm|nomr`, `SC_NOSHAD=1`. Run Unity only through the wrapper; if it reports LOCKED ask the user to close the editor; never kill Unity.
   Compare beside the reference with `Art/Blender/tools/experimental/face_template/pipeline/multiview/v2/contact_sheet.py` (args `out.png cell_px "title" label=path ... "|" label=path ...`). Judge silhouette and flow from front, 3/4, side and back, plus mid head-turn frames. Done = matches the reference's volume, fringe sweep, ear coverage, nape and wisps, with no black/silver chunks and no clipping.

## Coordinates and facts
`C:/AI/difflocks/head/Tobin_Base_v2_sliders.glb` (import with `bpy.ops.import_scene.gltf`): object `AvatarHead`, armature `AvatarRoot`, Head bone `Head`. World frame: z up, face toward -y, head centre about (0, -0.005, 1.70), crown top z about 1.835, brow about 1.745, eyes about 1.72, hairline about 1.775, ears z 1.65-1.73 at |x| about 0.075-0.09, chin about 1.58-1.60, head half width about 0.085-0.09. Unity applies globalScale 1.0425 to both TobinBase.fbx and the hair FBX.

## Existing tools and scratch
`Art/Blender/tools/experimental/tobin_hair/`: README.md (history and root causes), hair_rig2.py (Tripo-mesh version with debris removal), hair_rig3.py, flag.py, sim_bake2.py, export_fbx2.py, cards/cards_from_strands.py, cards/hair_atlas.py, difflocks_trim_experiment.py, chains_metaperson.json. Scratch outputs: `C:/AI/test_out/heroes/tobin/hair_v7` (cards, DiffLocks) and `hair_v6` (rig, Unity tiles).

## Lessons / pitfalls
- Black/silver chunks = back faces of single-sheet strips lit as flipped faces. Keep the hair material single-sided and orient all faces outward; hair should not receive hard self-shadows.
- The Unity capture needs several warm-up renders; the first frames came out glossy/black otherwise (already handled in the script).
- Git Bash mangles `/mnt/c/...` args to `wsl`: use `MSYS_NO_PATHCONV=1` (only if you touch DiffLocks again).
- `ProjectSettings/QualitySettings.asset` gets modified by the capture (shadowDistance): `git checkout -- ProjectSettings/QualitySettings.asset` before committing; never commit it.
- Do not commit the untracked leftovers or the modified `face_template/export_template.py` and `occlusion.py`.
- Windows machine, PowerShell and Git Bash both available; run long jobs in the background and wait for the completion notice (no sleep-polling loops).

## Git and reporting
Commit new files only with trailer `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`; push `origin claude/face-template-spike` (merge if rejected, never force). Do not post renders to the user unless asked: view them yourself with the Read tool via contact sheets. When done: update `tobin_hair/README.md` and the memory note `face-pipeline-v2-state.md`, and report with the commit hash first and the honest remaining flaws.
