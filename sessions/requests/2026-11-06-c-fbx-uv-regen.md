Title: Regenerate Inn/Cottage/Townhouse FBX with fixed kit UVs
Target: local
Status: started (local session, coordinator 10)
Model: sonnet
From: coordinator session 10 (QUEUE.md "Ready: Hollowmere art" top item)
Status file: Reference/sessions/fbx-uv-regen/STATUS.md

Brief:
Kit UV-stretch regen merged bc520027 (UV-stretch rows 33 -> 4). The Inn, Cottage and Townhouse building FBX were built from the old kit meshes and still carry the old stretched UVs. Also open: RetainWallLow LK0727 / LK0669 sandstone stretch 2.53.

Task: find the Blender generators for those three buildings (Art/Blender/tools, sc-asset-craft), regenerate them on the fixed kit UVs with headless `blender -b`, export FBX with path_mode='STRIP', re-import via the builders, and fix the two RetainWallLow stretches. Re-run the UV-stretch audit (the tool used in bc520027; see its commit/STATUS) and report before/after rows.

Rules:
- New worktree: run `git lfs checkout` first. Binaries via Git LFS.
- Art rules: .claude/rules/art-assets.md; Poly Haven rule: CC0 textures/models only through Art/Blender/tools/sc_fetch_textures.py / sc_fetch_models.py if anything new is needed.
- Captures: SC_CAPTURE_ONLY with only the cameras that show these three buildings (cloud captures are ~40 min/set and Hollowmere can OOM: one reduced camera; if it fails, set board needs: coordinator for a PC capture). Blender previews 960-1000 px ~20 samples. At most 3 rounds.
- Unity only via `bash Tools/cloud/unity.sh <gate|check> --out <scratchpad>`; gate while working, check on the final tip. Builders only (never hand-edit .unity/.prefab).
- Generators/renders run in Sonnet subagents (session-usage.md). reviewer (Sonnet) before merge. Report via board profile.
- Merge: merge origin/main, check, merge the passing tip into fresh main (merge commit, no PR, never force-push). On builder-output conflicts take main's side, one build, then check. Never edit CURRENT.md or tick QUEUE.md.

Done when: merged with a green check, UV audit before/after in STATUS, one before/after frame per building in Reference/sessions/fbx-uv-regen/, STATUS DONE, Archive: ready.
