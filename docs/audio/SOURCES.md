# Audio sources

Placeholder combat sounds (docs/PHASE1.md 6.6, P42).

## How a cue gets its sound (2026-10-30)

Every cue id has a generated recipe (`Assets/_Project/Core/Audio/CombatSfx.cs`, `SfxRecipes.cs`; ours, no licence). At boot `SfxGenerator.GenerateAll` makes the
clips, then `SfxGenerator.ApplyFileClips` replaces each one that has a recorded file named exactly like the cue id in
`Assets/_Project/Content/Audio/Resources/Sfx/` (loaded with `Resources.LoadAll`). The recipe still supplies the mixing (category, base volume, rate limit), so
gameplay code is unchanged. To swap a sound, replace the `.ogg` with the same name and update the row below. `SfxFileClipsTests` checks the list.

## Kenney CC0 packs (downloaded 2026-10-30)

All three are by Kenney (https://kenney.nl), licence **Creative Commons Zero (CC0 1.0)**, free for commercial use, credit not required (`License.txt` in each pack
says so, and each kenney.nl page shows "Creative Commons CC0"). Downloaded from the kenney.nl asset pages through the browser (the site refuses scripted requests),
archives kept outside the repo; only the files below are copied in. Chosen from the file names and categories, not yet auditioned in game: tune by ear and edit the rows.

| Pack | URL |
|---|---|
| Impact Sounds 1.0 | https://kenney.nl/assets/impact-sounds |
| RPG Audio | https://kenney.nl/assets/rpg-audio |
| Interface Sounds | https://kenney.nl/assets/interface-sounds |

| Cue id (= file in Resources/Sfx, `.ogg`) | Used for | Kenney source file | Pack |
|---|---|---|---|
| Combat_Hit | a normal hit on an enemy | impactPunch_medium_001 | Impact Sounds |
| Combat_HitHeavy | finisher / charge hit | impactPunch_heavy_001 | Impact Sounds |
| Combat_HitCrit | critical hit accent (plays with the hit) | impactMetal_heavy_001 | Impact Sounds |
| Combat_Whoosh | every swing | knifeSlice | RPG Audio |
| Combat_Block | enemy blocks; the hero's guard (pitched down) | impactPlate_medium_000 | Impact Sounds |
| Combat_Parry | a parry | impactMetal_medium_002 | Impact Sounds |
| Combat_Slam | enemy slam | impactSoft_heavy_001 | Impact Sounds |
| Combat_Hurt | the hero is hit | impactPunch_medium_003 | Impact Sounds |
| Combat_Pop | tech pop (bonus ABS) | pluck_002 | Interface Sounds |
| Combat_Heal | heal (pickup, clear heal) | confirmation_004 | Interface Sounds |
| Combat_Spell | staff spell cast | maximize_005 | Interface Sounds |
| Combat_Shot | bow shot | impactTin_medium_001 | Impact Sounds |
| Combat_Dodge | a visible dodge ignoring a hit | cloth3 | RPG Audio |
| Combat_Telegraph | unused since the enemy tells (kept for the cue list) | bong_001 | Interface Sounds |
| Combat_WindUp | slime wind-up (hop, glob), pitched up | impactSoft_medium_002 | Impact Sounds |
| Combat_WindUpScratch | rat-king wind-up (lunge, tail spin) | scratch_002 | Interface Sounds |
| Combat_WindUpWood | broom wind-up (sweep, poke): a wooden creak | creak1 | RPG Audio |
| Combat_WindUpHeavy | Dough Warden wind-up, pitched down | creak3 | RPG Audio |
| Combat_Glint | the glint at an enemy attack's commit (12-24 frames before its hitbox) | drawKnife2 | RPG Audio |
| Combat_EliteToll | the Warden's signature attack, at the peak of its wind-up | impactBell_heavy_001 | Impact Sounds |
| Combat_Die | an enemy dies | impactGlass_medium_002 | Impact Sounds |
| Ui_Click | UI click | click_001 | Interface Sounds |
| Ui_Confirm | UI confirm | confirmation_001 | Interface Sounds |
| Ui_Error | UI error | error_001 | Interface Sounds |

## Still generated (ours, no licence)

- `Wind_Ambient`, `Water_Lap`, `Bell_Ring`: the M1 ambience set, no Kenney equivalent chosen (the packs above have no loops).
- Hero downed and revive: `CombatCue.Down` and `CombatCue.Revive` exist but nothing raises them and no cue id maps to them yet, so there is no sound to replace. When
  they get ids, add rows here (candidates: `impactSoft_heavy_*` for down, `confirmation_00*` for revive).
