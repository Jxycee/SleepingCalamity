# Mixamo clip list (for M2 and M3)

The user downloads these from mixamo.com with their own Adobe account (PHASE1 section 5, P29). Claude can't sign in for you. Pack names change over time, so each line gives the search term to type into Mixamo plus what the clip is for. Pick the closest match; any clip that reads right works.

## How to download
- **Mannequin** (the greybox stand-in for every hero, P32): download **Y Bot** once, as **FBX for Unity (.fbx)**, **With Skin**, T-pose.
- **Every clip:** keep Y Bot selected as the character, choose **FBX for Unity (.fbx)**, **Without Skin**, **30 fps**, and no keyframe reduction.
- **Locomotion clips** (walk, jog, run, strafe, turn): tick **In Place**.
- **Where to put them:** save into `C:\Dev\_private\mixamo\` with the default file names, then tell Claude. Claude imports them into the project; the clips themselves are fine to commit, since Mixamo allows use in games.

## M2: movement and lock-on (about 22 clips)
| Search term | Clips to take | For |
|---|---|---|
| idle | 1 relaxed idle, 1 combat-ready idle | free and locked stances |
| walking | walk forward | locomotion blend |
| jogging | jog forward | locomotion blend |
| running | run forward, sprint | locomotion blend |
| walk backwards | walk back | locked strafing |
| strafe | left and right strafe (walk and jog) | locked strafing |
| left turn / right turn | 90-degree turns, both sides | turning in place |
| jump | jump up, falling idle, landing | traversal |
| dodge / roll | forward roll, side dodges left and right, back step | dodge (M3 too) |
| hit reaction | 2 light flinches, 1 heavy knockback, getting up | damage reactions |
| death / dying | 1 death, 1 "knocked down" loop | downed state (P24) |
| picking up | pick up from the ground | carrying a haul piece |
| carry | walking with a carried box | carrying a haul piece |

## M3: combat (one set per heirloom)
| Heirloom (hero) | Search terms | Clips to take |
|---|---|---|
| Logging axe (Tobin) | "great sword", "axe" | 3-hit combo, a heavy overhead finisher, a charge-up attack, a combat idle, a block-break |
| Dagger and shield (Pip) | "sword and shield" | 3-4 hit combo, shield block idle, block hit reaction, shield bash, a quick dodge attack |
| Staff (Wren) | "magic", "staff" | 2-3 staff strikes, a spell cast (forward), an area cast, casting idle |
| Bow and sword (Ash) | "longbow", "sword" | draw-aim-release loop, a quick shot, aim walk forward and strafe, a 2-hit one-handed sword combo, swapping between bow and sword |
| All | "kick", "slash" | 1 generic air attack, 1 launcher (an upward strike) |

## Enemies
The broom, the jam-jar slime, the rat-king and the Dough Warden are greybox shapes in Phase 1 (P5, section 5). They don't need Mixamo.
