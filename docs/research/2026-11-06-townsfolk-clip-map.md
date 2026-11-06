# Townsfolk idle-work clip map (2026-11-06)

Goal: loops that make the rebuilt towns feel lived in. Residents: baker (Hollis Rye, placeholder name), innkeeper (Marta), smith (Dun), Saltwake fisherman (Teague); names are the research note's placeholders, not locked. Rig: the UAL humanoid (Unity Humanoid retarget), so every pack below plays on every townsperson.

Clip names below were read from the repo's own `UAL1_Standard.fbx.meta` and `UAL2_Standard.fbx.meta` (Standard tier), not guessed. KayKit lists come from the pack page (categories only, no file names); verify names after download. Licences: `docs/research/2026-11-06-open-rigged-assets.md` (UAL, KayKit `cc0`; HY-Motion and MoMask `personal-use`).

## Available now in the repo (UAL1 + UAL2 Standard)
| Clip | Use |
|---|---|
| `Idle_Loop`, `Idle_Talking_Loop`, `Idle_FoldArms_Loop`, `Idle_Lantern_Loop`, `Idle_TalkingPhone_Loop` (skip the phone), `Idle_Rail_Loop`, `Idle_Rail_Call` | standing base idles; Rail = leaning on a counter or rail, Call = hailing a customer |
| `Sitting_Enter`, `Sitting_Idle_Loop`, `Sitting_Talking_Loop`, `Sitting_Exit` | inn patrons, resting fisherman |
| `Walk_Loop`, `Walk_Formal_Loop`, `Walk_Carry_Loop`, `Idle_Talking_Loop` | errands, delivery |
| `Farm_Harvest`, `Farm_PlantSeed`, `Farm_Watering` | allotments, orchard; stand-in for kneading or stirring only if held still |
| `TreeChopping_Loop` | woodpile; closest swing motion we have for the smith's hammering until a real hammer clip lands |
| `Fixing_Kneeling`, `PickUp_Table`, `Interact`, `Push_Loop`, `Chest_Open`, `Consume`, `OverhandThrow`, `Dance_Loop`, `Yes` | one-shots: repair, take an item off the counter, open a door/chest, serve a drink or loaf (Consume is eating), celebrate when a building is rebuilt |

## Per resident
| Resident | Loops we can build today | Missing |
|---|---|---|
| **Baker (Hollis)** | counter: `Idle_Rail_Loop` + `Idle_Rail_Call` (hail), `PickUp_Table` (hand over a loaf), `Walk_Carry_Loop` (tray to the window), `Farm_Harvest` as a reach-and-pick | knead dough, shape/slide loaves into the oven, wipe brow, ring the shop bell |
| **Innkeeper (Marta)** | `Idle_Rail_Loop`, `Idle_Talking_Loop`, `Walk_Carry_Loop` (plates), `PickUp_Table`, `Chest_Open` (cellar hatch), `Consume` for patrons, `Sitting_*` for patrons | wipe a mug, pour a drink, scrub a table, stir a pot |
| **Smith (Dun)** | `TreeChopping_Loop` (hammer stand-in), `Fixing_Kneeling`, `Idle_FoldArms_Loop`, `Walk_Carry_Loop` (ingots) | hammer on anvil (KayKit "hammering" covers it), work the bellows, quench, grind wheel, inspect a blade |
| **Fisherman (Teague, Saltwake)** | `Sitting_Idle_Loop` (waiting), `Idle_Lantern_Loop` (dawn dock), `Walk_Carry_Loop` (nets/baskets), `Fixing_Kneeling` (mending net) | cast, wait with a rod, reel in, haul a net, gut a fish. UAL2 names fishing as a category but the Standard FBX has no fishing clip; options: KayKit "fishing" (CC0, check the clip), UAL2 Source ($14.99+, user's call), else generate |

## Gaps and how to fill them
Priority by visibility in the square. Cost is S/M/L for retargeting and cleanup, not generation.
1. **Hammering on anvil** (smith): KayKit Character Animations "hammering" first; if it is too slight, generate. S.
2. **Fishing set** (cast, wait loop, reel, net haul): KayKit "fishing" then UAL2 Source; generate the rest. M.
3. **Kneading/baking**: kneading loop, oven loaf-slide, wipe brow. Generate. M.
4. **Inn service**: wipe mug, pour, scrub table, stir pot. Generate. M.
5. **Sweeping, hanging washing, sorting goods** (any resident, filler). KayKit has "generic working"; sweeping is not listed, generate. S.

Generation routes (all `personal-use`, record in the source record; retarget to Unity Humanoid; keep clips under 5 s, loop-trim in Blender or the Unity clip editor):
- **HY-Motion 1.0** via the official Hugging Face Space `tencent/HY-Motion-1.0` (ZeroGPU, free daily quota). Prompts under 30 words, `--num_seeds=1`, under 5 s. No local run on the 12 GB RTX 5070. Example: "A baker kneads dough on a counter with both hands, rhythmic, looping."
- **MoMask** locally (well under 1 GB VRAM; AMASS-trained weights, personal-use): good for simple cycles (scrub, sweep, stir), weaker on fine hand actions. Needs a loop-closing pass.
- Hand-key the last 10%: hand poses and prop contact (mug, hammer, rod) need cleanup; props attach to the hand bone, so one clip serves many tools.

Style note: all sources are realistic-proportion mocap or generated motion; townsfolk are storybook proportions, so exaggerate timing a little (slower anticipation, bigger hammer arc) in Unity or Blender after retarget.

## Next steps (not started)
- Pick the fishing source (KayKit check first, it costs nothing).
- Generate the 3-4 missing loops above in one HY-Motion sitting and log each as a `personal-use` source record.
- Wire into the townsfolk animator once townsfolk models exist (none today).
