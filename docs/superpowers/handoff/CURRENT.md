# CURRENT: Sleeping Calamity running handoff

## Now
- **2026-11-03 (dungeon design signed off; dive build starts).** The Hearthdeep dungeon design is **signed off by the user** (spec `docs/superpowers/specs/2026-11-03-hearthdeep-dungeon-design.md`, 47 decisions in section 2; merged `67d814e8`, STORY 8.2/8.4/8.5 and the PHASE1 dungeon amendment applied in `95bee0c4`). Undercroft = 10 floors (bank at the floor-5 halfway set piece with the Brazen Ox and the innkeeper), time is the stake, one-time checkpoint with floor pick, lost satchel, momentum, one paid boss retry (idol or gold), stir per half, Atla-style pieces, bread ladder, per-player loot. The **dive build roadmap** (`docs/superpowers/plans/2026-11-03-dive-build-roadmap.md`, merged `e9336b5b`) splits section 12 steps 1-3 plus a metrics gym into 4 plans, one per fresh session: (1) dive rules in Core, written in full (`2026-11-03-dive-rules-core.md`, 9 tasks, 38 tests already verified in a scratch worktree per `e9336b5b`); (2) per-player loot (risk-reviewer); (3) the new 7-floor dive playable end to end (retires `DiveRoute`/`DiveState`/`StirMeter`); (4) metrics gym + room data v2 (depends on nothing). Plans 2-4 write their detailed plan in their own session. Subagent rules since `4bc84913`: pinned agent types only, branch-reviewer at high effort (`9dcc9a39`), branch review skipped for pure Core/docs/data/tests/art (`eda9d021`).
- **Assets merge freeze LIFTED** (user, 2026-11-03: "Yes, lift it"). Normal merge rules apply again: a tip with a passing check merges into fresh main when git merges it cleanly.
- Nov 2 evening state (from `41639c00`): final main check PASS on `5d87a82b` (build, EditMode 1221/1221, PlayMode 117 passed 0 failed 19 skipped, port 7791); PC port unblocked by test-port-fallback `6f94e48b`. Parked, unmerged: `claude/backdrop-shade` (`aa70f707`, check PASS, ~20/40, no clear gain), `claude/stall-lods` (needs a PC capture). Reboot sometime to clear zombie Unity 33336 (harmless).
- Older entries: `docs/superpowers/handoff/history-2026-11-02.md` (Oct 31 evening to Nov 2) and `history-2026-10-31.md`.

## Next
1. **Plan 1 session**: execute `docs/superpowers/plans/2026-11-03-dive-rules-core.md` task by task (implementer per task, reviewer between tasks), Task 9 metas + gate, `unity.ps1 soak` (Core/Cellars), check, merge.
2. **Plan 4 session** in parallel: write the metrics gym + room data v2 plan (writing-plans), then build it (builder-made gym scene, room data v2 contract, one pilot room).
3. Then plan 2 (per-player loot), then plan 3 (new dive playable). Still open from Nov 2: Town review 2, backdrop shading redo, Terrace stairs reading as boards, `claude/stall-lods` PC capture, traversal islands/holes, DiveBot pace, M7 open questions.
4. Cloud churn: Linux builds rewrite last-digit floats in Hollowmere/BeautyCorner/CellarFloorTest .unity and `M_HM_Surf_WornRun.mat`; never commit (cloud-environment.md). Pace 0.1-0.2%/min. User owes: `USER_TODO.md` (Mixamo clips, 2-player walks), animal models for ambient life phase 2.

## Branches and worktrees
- `main` at `e6dc7114` + this handoff; last main check PASS on `5d87a82b` (EditMode 1221, PlayMode 117 + 19 skipped).
- 2026-11-03 sessions, each in its own worktree: plan 1 `claude/dive-rules-core-ef7cd2` (on task 6 of 9), plan 4 `claude/metrics-gym-aac07e` (writing its plan), town art leftovers (backdrop shading, Terrace stairs, stall LODs; takes over `claude/backdrop-shade` and `claude/stall-lods`; user asked 2026-11-03).
- Board STATUS for cellars-visual-b, traversal-4, ambient-polish corrected to DONE (`7172621b`; merges `ec29e0d1`, `40de7684`, `0c02367a`).

## Blockers and open questions
- The asset skills were rewritten on 2026-10-30. Some legacy FishGame tools still need rework before the art batches; see the tool table in `sc-asset-craft` section 0b.
- `sc-walk` documents WalkDriver helpers that don't exist here yet; they arrive with the App/Dev port.

## Read next
- **Dive build:** `docs/superpowers/specs/2026-11-03-hearthdeep-dungeon-design.md` (signed off), `docs/superpowers/plans/2026-11-03-dive-build-roadmap.md`, `docs/superpowers/plans/2026-11-03-dive-rules-core.md`.
- `docs/PHASE1.md`, then `docs/superpowers/handoff/decisions-for-review.md`.
- `docs/STORY.md`, `docs/design/dark-cloud-lessons.md`, `docs/research/` (start with each note's summary).
- `docs/superpowers/handoff/2026-10-29-new-project-handoff.md`: the founding plan and the parts bin.
- Private research material, outside the repo:
  - `C:\Dev\_private\ps2\` holds the extracted ELFs and the DC1 Ghidra project and decompile (setup in `docs/research/dc1-rea.md`).
  - `C:\Dev\_research\` holds the ChronicleTwo, OpenCloud, OpenKH and KH2FM decomp clones.
  - The ISOs are in `C:\Users\jayce\Desktop\Games\ps2\`.

## Inbox
- Builder bug (bakery session's usage review, 2026-10-31): materials flip their MotionVectors pass between graphics captures and `-nographics` builds, so post-check commits break the check marker. Fix in the builders (write identical bytes both modes).
- Trial ramenvr/autopilot (AI playtester in the editor) as an automated smoke tester once M3 combat settles; evaluated 2026-10-30 by a side session, nothing installed.
