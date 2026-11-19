# T24b brief: split Hollowmere into additive scenes (Area 1 world, Run 8)

Goal (user decision 2026-11-18, relayed by the coordinator): Hollowmere.unity is ~46-48 MB of plain YAML, and its build is nearly all of the build time (T26: scoped build 426 s vs BuildAll 512 s). Split it into additive scenes so (a) each file stays well under 20 MB (GitHub refuses files over 100 MB), and (b) the scoped build rebuilds only the touched sub-scene. Same game, same frames.

Done means:
- every scene file is well under 20 MB;
- the gate and play loop PASS;
- the captures are unchanged on the diff gate (blurred mean abs diff under 0.5 per eye camera vs the pre-split frames);
- you report a measured build time per sub-scene, and a scoped rebuild of one sub-scene is clearly faster than today's 426 s.

Branch `claude/area1-world`, worktree `C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725`, current tip. Uncommitted `Reference/sessions/area1-world/STATUS.md`, `verdicts/` and this brief from the controller: commit them unchanged with your work, never edit STATUS.md; leave the T23 brief alone. Do not merge main. Scratch: `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\54c4d5da-7980-453e-9883-d2236b06f4b2\scratchpad\T24b`.

## Risk: networking and scene flow (read the `sc-netcode` skill first)
Hollowmere is the gameplay scene that NGO's NetworkSceneManager loads (`App/SceneFlow.cs`, `SceneNames`, `ArenaPlayerSpawner`, `AppBootstrap`). Keep every NetworkObject, spawn point, trigger, traversal space, collider the play loop relies on, the capture cameras, and anything a test finds by name in the main Hollowmere scene.

Move only static, non-networked art into the additive scenes: dressing, foliage, decals, combined meshes, the backdrop, ambient life. Load the additive scenes locally on every peer, host and clients alike, when Hollowmere becomes active, through a small local loader. Do not register them with NGO scene management: a static environment needs no network sync. Unload them with Hollowmere.

Decide one thing and state it: whether colliders that live on art objects (shell BoxColliders, props over 0.5 m) stay in the main scene or move with their art. If they move, the loader must finish before players can move. Check the spawn and teleport paths, and the late-join path, against that.

This is networking-adjacent work: the controller will have it reviewed by `risk-reviewer` after you report.

## Split
Research the common practice first (Unity additive/multi-scene for static environment chunks, lighting and probes across scenes, `SceneManager.LoadSceneAsync` additive with `allowSceneActivation`, build settings entries). State it in one line.

The split should follow the builder sections where that keeps scoped rebuilds clean. For example:
- `Hollowmere` (gameplay core: terrain, colliders, network objects, cameras, lighting, volumes);
- `Hollowmere_Town` (shells, streets dressing, square);
- `Hollowmere_Outskirts` (orchard, allotments, shore, mill yard, roadside, banks);
- `Hollowmere_Backdrop` (ring, ridges, mountain, landmark: T22's work);
- optionally `Hollowmere_Life` (ambient birds, smoke, mist).

Lighting stays defined once, in the main scene, with RenderSettings and the volumes; make sure the additive scenes don't override it. Reflection probes and light probes must still apply.

Builders write each sub-scene. `HollowmereCombine`, `HollowmereLods` and the mesh externalize pass run per sub-scene. Builds stay deterministic.

Extend T26's `HollowmereScoped`: `SC_SCOPED_SCENES=<names>` rebuilds only those sub-scenes when that gives the same bytes as a full build. Prove it with an empty git diff against a full BuildAll at the same tip. If a sub-scene depends on main-scene build state (caches, placement against colliders), rebuild the main scene's state in memory without saving it, or say why that can't be done.

Embedded meshes: the coordinator mentioned ~200, but T09b and T13a counted 0. Count them (`--- !u!43` blocks per scene file) and externalize any you find.

Any other large builder scene: `CellarFloorTest.unity` is 3.6 MB, so no split is needed; list any others over 10 MB.

## Check
- BuildAll twice and check the outputs are identical, because scene ordering changes.
- Churn reverted (STATUS "Known churn", B9 `.fbx.meta`).
- Gate with `SC MARKER` = HEAD.
- Play loop `-Filter "HollowmereHub|LoopBot|LoopGuideHud|SquareLoopWalkQa|TraversalRegression"` (11 passed, 7 ignores).
- Run any tests that load Hollowmere by name: grep the tests for `Hollowmere`.
- Capture all 23 eye cameras plus Allotment with T26's capture, before the split (or reuse the latest committed set from the same tip) and after it. Diff each against before and list the values.
- Commit by path, push. Delete probe scripts.

## Report (under 18 lines)
- commits; gate, play loop and other test lines; build twice;
- scene files with sizes;
- per-sub-scene build time and scoped rebuild times; byte-identity proof;
- the collider decision;
- loader design (when it loads, how clients get it, late join);
- capture diffs (max and any over 0.5);
- anything not done.
