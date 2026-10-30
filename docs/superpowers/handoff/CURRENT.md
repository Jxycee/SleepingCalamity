# CURRENT: Sleeping Calamity running handoff

## Now
2026-10-30. Phase 0 is effectively done (the story is locked, PHASE1.md is approved, research and references are in). Phase 1 is underway.
- **M1 (foundation port) and M4 (weapon growth) are merged to main** (`c7367cd`). `SC RESULT check PASS` ran on `cdc4741` (EditMode 310+, PlayMode 11/11); the merge only adds docs on top of it.
  - M1 had two risk reviews, both applied.
  - M4 had a review with no blockers; its should-fix items are on `m4-fixes`.
- **Running:**
  - `m4-fixes`: the M4 review fixes, plus making the builders idempotent (each build re-saves three scenes).
  - `m2-hero-camera`: M2, the networked hero, camera and lock-on, on a capsule mannequin.
- **User decisions today:** recorded in the PHASE1.md change log and `decisions-for-review.md` (A11-A19).
- **The user owes:** the items in `docs/superpowers/handoff/USER_TODO.md`: Mixamo clips, linking UGS, the 2-player Relay walk, and an optional BIOS dump.

## Next
1. Land `m4-fixes` and M2 (review, then merge).
2. M3 combat greybox; M5 room-pool authoring can start during M3.
3. M8: the style bible into the tools (port the fg-aaa-prop tools, fix the `sc_asset_audit` crash, build the look-test scene). M10: Tobin through the pipeline in the chosen viking/timber-yard direction (concept brief in `docs/design/tobin-concepts.md`).

## Branches and worktrees
- `main` (`c7367cd`). In progress: `m4-fixes` (worktree `agent-ad25d53219a200b3c`) and `m2-hero-camera` (worktree `agent-a3f4b7d611d7e2923`). `m1-foundation` and `m4-weapon-growth` are merged.

## Blockers and open questions
- The asset skills were rewritten on 2026-10-30. Some legacy FishGame tools still need rework before the art batches; see the tool table in `sc-asset-craft` section 0b.
- `sc-walk` documents WalkDriver helpers that don't exist here yet; they arrive with the App/Dev port.

## Read next
- `docs/PHASE1.md`, then `docs/superpowers/handoff/decisions-for-review.md`.
- `docs/STORY.md`, `docs/design/dark-cloud-lessons.md`, `docs/research/` (start with each note's summary).
- `docs/superpowers/handoff/2026-10-29-new-project-handoff.md`: the founding plan and the parts bin.
- Private research material, outside the repo:
  - `C:\Dev\_private\ps2\` holds the extracted ELFs and the DC1 Ghidra project and decompile (setup in `docs/research/dc1-rea.md`).
  - `C:\Dev\_research\` holds the ChronicleTwo, OpenCloud, OpenKH and KH2FM decomp clones.
  - The ISOs are in `C:\Users\jayce\Desktop\Games\ps2\`.

## Inbox
- (none)
