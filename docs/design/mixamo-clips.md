# Animation clip list (for M2 and M3)

Two sources, both fine to ship:
- **Quaternius Universal Animation Library 1 and 2** (CC0, free "Standard" tier; Claude imports these, no account needed). Pages: https://quaternius.itch.io/universal-animation-library and https://quaternius.itch.io/universal-animation-library-2.
- **Mixamo** (the user downloads with their own Adobe account, PHASE1 section 5, P29). Only for what Quaternius' free tier lacks.

Unity's Humanoid retargeting plays both rigs on the same hero, so mixing is fine. The free-tier lists below were read from Quaternius' own anim viewer (quaternius.com/animviewer.html) on 2026-10-30: 44 clips in UAL1, 42 in UAL2. Their paid tiers (UAL1 Pro about $10, UAL2 Source about $15) add strafes, turns, side dodges, knocked-down loops, light/heavy sword combos, aerial attacks and a full bow set; buying those would remove almost every Mixamo line below.

## Mixamo download settings
- **Mannequin:** Y Bot, FBX for Unity (.fbx), With Skin, T-pose. *(Done 2026-10-30.)*
- **Every clip:** Y Bot selected, FBX for Unity (.fbx), Without Skin, 30 fps, no keyframe reduction. Tick **In Place** for walk/jog/run/strafe/turn.
- Save into `C:\Dev\_private\mixamo\` (or Downloads; Claude moves them). Default file names are fine.

Already downloaded (2026-10-30): Y Bot, Warrior Idle, Standing Idle To Action Idle, Action Idle To Standing Idle, Action Idle To Fight Idle.

## M2: movement and lock-on
| Need | Quaternius free | Still from Mixamo (search term: clips) |
|---|---|---|
| Relaxed idle, combat idle | Idle, Sword_Idle, Idle_Shield (+ your Warrior Idle) | - |
| Walk, jog, run, sprint | Walk, Jog_Fwd, Sprint | - |
| Walk back | - | "walk backwards": walk back |
| Locked strafing | - | "strafe": left and right strafe, walk and jog (4 clips) |
| Turn in place | - | "left turn" / "right turn": 90-degree turns, both sides |
| Jump | Jump_Start, Jump (air loop), Jump_Land | - |
| Dodge | Roll (forward) | "dodge": side dodge left, side dodge right, back step |
| Hit reactions | Hit_Chest, Hit_Head (light), Hit_Knockback (heavy), LayToIdle (get up) | - |
| Death, downed | Death01 | "knocked down" or "lying": 1 downed loop |
| Pick up from the ground | - (PickUp_Table is table height) | "picking up": pick up from the ground |
| Carry a haul piece | Walk_Carry | - |

## M3: combat (one set per heirloom)
| Heirloom (hero) | Quaternius free | Still from Mixamo |
|---|---|---|
| Logging axe (Tobin) | Idle_Shield_Break (block-break) | "great sword": 3-hit two-handed combo, heavy overhead finisher, charge-up attack, two-handed combat idle |
| Dagger and shield (Pip) | Sword_Regular_A/B/C + Combo, Idle_Shield, Sword_Block, Shield_OneShot (bash), Sword_Dash_RM / Shield_Dash_RM (dodge attack) | "sword and shield": 1 block hit reaction |
| Staff (Wren) | Spell_Simple_Enter / Idle / Shoot / Exit (forward cast, casting idle) | "staff" or "magic": 2-3 staff strikes, 1 area cast |
| Bow and sword (Ash) | Sword_Regular_A/B (2-hit one-hand combo) | "longbow": draw-aim-release loop, quick shot, aim walk forward, aim strafe; "sheath" or "draw sword": 1 bow/sword swap |
| All | - | "jump attack": 1 air attack; "uppercut" or "upward slash": 1 launcher |

Mixamo still owed in total: about 21 clips (M2 11, M3 about 10-12) instead of about 45.

## Third source: Unity Asset Store free packs (research 2026-10-30)
Kevin Iglesias' free packs (publisher "Kevin Iglesias" on the Asset Store; Standard Asset Store EULA, commercial use fine): Human Basic Motions FREE (8-direction walk/run including back and diagonals, turn-in-place left/right, jump, fall loop), Archer FREE (bow shot, sheathe/unsheathe, strafe run), Spellcasting FREE (cast enter/idle/exit plus 5 spells), Melee FREE (one two-handed attack, a polearm attack). Also Explosive's Warrior Pack Bundle 2 FREE and RPG Character Mecanim Animation Pack FREE. The user adds them to "My Assets" with their Unity ID; Claude imports them through the Package Manager. Take the Asset Store copies, not itch (licence page empty there, and one itch FBX had a hierarchy mismatch).
If you add these, skip from Mixamo: walk back, the 4 strafes, the 2 turns, the area cast. Mixamo then still covers: side dodges and back step, the downed loop, pick-up from the ground, the greatsword combo/overhead/charge, staff strikes, bow draw-aim loop and aim walk, the air attack and the launcher (about 12 clips). No free source has those (full notes: `docs/research/animation-sources.md`).

## Enemies
The broom, the jam-jar slime, the rat-king and the Dough Warden are greybox shapes in Phase 1 (P5, section 5). They don't need humanoid clips.
