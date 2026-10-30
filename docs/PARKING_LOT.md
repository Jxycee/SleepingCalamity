# Parking lot

Ideas worth keeping that aren't in the approved Phase 1 plan. Each line gives the idea, its source, and the date. Promote an idea into a plan deliberately; never build one silently.

## From the Kingdom Hearts sweeps (2026-10-30)
**Promoted into Phase 1 on 2026-10-30 (user):** growth verbs, tech bonus, hero abilities from weapon milestones, spell pips, and boss parts. See the PHASE1 change log. The rest stays parked.
Sources: `docs/research/kh1-sweep.md` and `docs/research/kh2-sweep.md`.

### Weapon growth (fits M4)
- **Growth verbs per heirloom** (KH2 Drive Forms): each weapon also levels from its own style of play.
  - Axe: finishers and guard breaks.
  - Dagger: hits and parries.
  - Staff: multi-hits.
  - Bow: weak-point shots.
- **Tech bonus** (KH1): a parry, deflect, weak-part finisher or element match pays extra weapon experience, with a small pop. Teaches the enemy answers. Fits M3 and M4.
- **Hero abilities from weapon milestones** (KH2 form levels feeding base Sora): a weaker copy of the ability stays when the hero switches weapons.

### Combat
- **Spell pips refilled by landing hits** (KH1 MP): for Wren's staff and other active skills. Start at 3; weapon growth raises the cap.
- **Boss parts with their own HP** (KH1 Guard Armor): breaking a part drops healing. In co-op, players naturally split the parts between them. Candidate for the floor-4 Dough Warden.

### Dungeons and exploration
- **Co-op "hearth marks"** (KH1 Trinity Marks): two or more players push, lift or brace together to open secrets and shortcuts.
  - Each colour unlocks with the story.
  - Every mark also has a solo route, and nothing can be missed.
- **Movement tools unlocked by progress** (KH2 High Jump and Glide, the Cavern of Remembrance):
  - Optional room-pool branches are tagged with the tool they need, and gates carry colour-coded hints.
  - One player who has the tool opens a shortcut for the whole party.

### Progression and rewards
- **Permanent capacity from story beats and optional superbosses** (KH2 bonus levels, Absent Silhouettes): an extra slot, a gauge bar or max HP, each listed in the journal.
- **Rule-twist challenge gauntlets built from existing rooms** (KH2 Coliseum cups): no healing, one weapon, a shared damage budget.
- **Keepsakes returned to named residents** (KH1 Dalmatians): found three to a chest, each return pays a milestone reward. Ties into rebuilding home.

### Things to avoid (from both sweeps)
- Hidden random penalties, such as KH2's Anti Points.
- Irreversible early build choices, or hidden permanent choices.
- Features only a full group can trigger.
- Team attacks or summons that take a player out of play.
- Prompts that reward mashing.
- Enemies that respawn after leaving a room.
- A forced travel mini-game.
- A hard mode that only multiplies damage.

## Tooling (2026-10-30)
- ~~Cloud `.csproj` generation~~ built 2026-10-30 (`sc_cloud_compile.py --csproj`).
