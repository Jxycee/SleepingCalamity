# T24b-life brief: life pass fix round (Area 1 world, Run 8)

(Named T24b-life because T24b is the scene split.) T24 landed (882a1913..74675fd7; brief `briefs/T24_brief.md`). It made goats, Tuppence, 3D crows, photo-textured butterflies, smoke and washing sway, but left parts of the user's direct request ("Butterflies and ambient life need real textures also", 2026-11-18) undone, and **the life does not show at the eye cameras**: 13 of 15 R19 eye frames are under the 0.5 diff gate against R18 (goats and crows a few pixels, butterflies white/yellow dots). Judges keep scoring line 8 (life) lowest.

Branch `claude/area1-world`, worktree `C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725`, tip 74675fd7. Include the controller's uncommitted Reference/ files unchanged in your first commit (never edit STATUS.md), and commit the two untracked `SOURCES.md.meta` files (`Content/Art/Town/AmbientLife/Wings/`, `Content/Art/Town/Animals/`). Scratch: `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\afea9a59-8f25-4f8e-af79-2a9f20c645f7\scratchpad\T24b`. Code: `Editor/Builders/HollowmereAnimals.cs`, `HollowmereAmbientBuilder.cs`, `Presentation/Town/AmbientAnimal.cs`, `AmbientPerchedBird.cs`, `AmbientWingMesh.cs`, `AmbientClothSway.cs`, `AmbientCreatures.cs`, `Shaders/SC_WingCard.shader`, `SC_BirdCard.shader`.

## Do
1. **Life in the eye views.** Every eye camera should show at least one living thing large enough to read (a goat at 8-20 m, crows on a near ridge or wall top, butterflies over flowers within 3-6 m, pigeons on a near roof). Re-place goats/crows/butterflies/pigeons by the camera table (`HollowmereBuilder` eye cameras) without blocking views or paths; keep goats on their story ground (ruins, churchyard, crag, orchard edge). Check with motion strips (`SC_CAPTURE_MOTION=1`).
2. **Real textures for the rest (user request).** Bees, rooks, pigeons, swallows, gulls and larks still use flat `SC_BirdCard` colour. The crow route worked (CC photo through the local pipeline). For cut-outs, use a local segmentation tool (rembg / SAM in the `C:\AI` toolchain, skill `sc-ai-model-pipeline`) on CC0/CC-BY/CC-BY-SA photos (Commons; record source + licence). Near-eye species (pigeons on roofs, rooks perched) as textured meshes like the crow; flyers (gulls, swallows, larks, rooks circling) as species-true textured wing cards with a correct silhouette and flap; bees as small textured meshes or cards over flower patches. Add a normal map where the wing or feather shading needs it.
3. **Butterfly wing edges:** the peacock crop (`R19/crops/butterfly_peacock_close.png`) shows a pale beige halo around the wing margins (specimen-photo background bleeding into the cut-out; real peacock margins are dark brown). Probe the alpha edge, then erode/decontaminate the edge colour on all four species.
4. **Crows:** smoothness down (they read glossy-plastic in sun); take-off to the circle path and return; one crow on the empty bell frame.
5. **Tuppence:** a sitting or loafing pose (bone rotations on the rig, held), placed on a real sill or step with a surface under it, plus the fountain rim; slow blink/head turn, tail flick.
6. **Goats:** check horns read (the dark goat crop shows none); if the mesh has none, add simple curved horns or pick a horned generation; mute the spiky fur look if a material change helps (roughness, normal strength).
7. **Cloth:** stall cloth and banners/bunting sway too; prefer a vertex wind keyword (pinned by vertex colour or UV) over transform swings, same wind direction as smoke.
Out of scope: audio (parked: `AudioBus` plays recipe cues only; ambient zones need clips and a positional loop, later), follow/petting, smoke lots (T25 re-picks the hero houses).

## Capture and judging
Round 0 = `R19/`. Prove item 1 on two cameras first (reduced capture). Rounds end after two in a row with no visible gain. Finals `R19b/` (all eye cameras that changed, with motion strips) + new close crops in `R19b/crops/` (a pigeon, a gull or rook, a bee, the fixed butterfly edge, the sitting cat). The controller runs the blind A/B vs R19.

## Check
Churn reverted (STATUS "Known churn" + T28 row; B9 `.fbx.meta` re-imports). Commit by path. Gate (`SC MARKER` = HEAD). Play loop `-Filter "HollowmereHub|LoopBot|LoopGuideHud|SquareLoopWalkQa|TraversalRegression|HollowmereSceneLoader"` if anything moves into walking range. Push. Delete probe scripts.

## Report (under 15 lines)
Commits; gate/play lines; per eye camera which creature now reads; species textured (source + licence each); cat/goat/crow/cloth changes; views better/worse; anything unverified.
