Title: Tobin base character: Unity gates, hair, and file intake (local, PC)
Target: local
Status: pending
Model: sonnet
From: cloud session on branch claude/pensive-clarke-e25tku (user asked 2026-11-08 for a handoff to a local session)
Status file: Reference/sessions/tobin-base/STATUS.md

Brief:
READ FIRST: docs/research/2026-11-08-character-pipeline.md, sections 0 to 0k (the newest sections are at the top; ignore sections 1-7 where they disagree: they assumed an MPFB2 route). Branch `claude/pensive-clarke-e25tku`; nothing is merged to main. The cloud session had no Unity, no GPU and could not download Wikimedia images.

GOAL (user): a clean, rigged, customisable base model of the hero Tobin, face and body, with texture as clean as the original face, ready for later clothing and hair. Clothes do not matter yet. The user dislikes inventing new paths: prefer standard tools.

WHAT WAS DECIDED
- The Tripo head/body route (UniRig weights, transferred ARKit shapes, rig_v1/v2, face_v8) is dropped as the base. Tripo stays for likeness, textures, rigid parts, clothing.
- Base = a MetaPerson Creator export (Avatar SDK), "file 3": the Tobin face from the user's isolated face image, 73-joint Mixamo-style rig (no prefix, Hips/Spine/.../LeftEye/RightEye, no jaw bone), AvatarHead with 51 of 52 ARKit shapes + 15 Oculus visemes + 83 head sliders, 14 body sliders, teeth/eyes/lashes, A-pose clip, 2048 head/body textures. Torso and thighs are not in the export (hidden under clothes), so a built "underlay" fills them; it is tagged `sc_role = underlay_hide_when_clothed` and must be switched off whenever clothes are worn.
- Licence: free exports are labelled non-commercial; file 3 used paid coins; game-use terms unconfirmed. Tag `licence: personal-use (unconfirmed)` in the source record.
- Hair: Tripo hair (hair.glb, meshopt-compressed) to go on the head; Plan A mesh on the Head bone, Plan B Unity strand hair (experiment).

FILES (none are in the repo; ask the user to put them on the PC, e.g. C:\AI\test_out\heroes\tobin\metaperson_v1\)
- MetaPerson file 3, avatar.zip: https://drive.google.com/file/d/1yknfAKwu5kC2uBEcOQoNrAQRucLqrClA/view?usp=sharing
- Tobin_Base_v2_sliders.glb (base + underlay + sliders): the user has it on the phone from the cloud session; ask for it (Drive).
- hair.glb: the user's upload to the cloud session; ask for it again, or an uncompressed FBX/OBJ re-export from Tripo.
- Scripts in the repo: Art/Blender/tools/experimental/sc_tobin_underlay_build.py (final build), sc_tobin_measure_rings.py.

STEPS, IN ORDER (stop at the first failure and report; one change per version)
1. Intake: unzip file 3, record the source (account, settings, licence) in an `Art/Source/MetaPerson/SOURCES.md` row; commit binaries through Git LFS on a branch, not main (ask the user first: the GLBs are 36 to 150 MB).
2. Unity gates on Tobin_Base_v2_sliders.glb (import settings Humanoid): (a) the avatar maps with no manual bone fixes; (b) the ARKit and viseme shapes move on the SkinnedMeshRenderer; (c) the same-frame Y Bot comparison on the nine real clips plus arms overhead; (d) face playback with eye look-at, micro-saccades, blinks, jaw capped (jawOpen at 1.0 shows a flat tongue and little upper teeth; closed lids render muddy), and uLipSync on the five vowels; (e) scale: avatar is 1.80 m, Tobin is 1.90 m, so import scale 1.0556.
3. Texture check: 1:1 crops of eye, lip, brow, cheek against the original Tripo face texture; accept at 95% of the original's sharpness and no halos.
4. Hair: Plan A first (see note section 0k). Then Plan B in a separate branch: install Alembic + Hair (git URL), confirm it compiles on 6000.6.5 / URP 17.6, make a procedural strand asset on a scalp mesh. Report compile errors.
5. Underlay tidy (low priority): ragged overlaps at the back of each shoulder; muscle detail was flattened by the clamp.
6. Email support@avatarsdk.com for written game-use terms (draft in the chat history of the cloud session: personal co-op game; no-outfit or full-body export; hidden-skin parameter).

RULES: sc-delegate and the pinned agent types; Unity only through the wrapper; never edit .unity or .prefab YAML by hand; no new transfer or conform scripts between AI meshes; report via the STATUS file; never edit CURRENT.md.

DONE WHEN: gates 2 and 3 pass or fail with evidence, hair is on the head or the failure is reported, STATUS says Archive: ready.
