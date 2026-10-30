# CURRENT: Sleeping Calamity running handoff

## Now
2026-10-30, end of session 1 (a long session). Phase 0 is done and Phase 1 is underway.
- **Merged to main** (`5a1e011`):
  - M1, the foundation: world library with 10 Solo + 10 Multiplayer worlds; the creator hosts.
  - M2, the networked hero: camera and lock-on.
  - M4, weapon growth, with the Kingdom Hearts additions.
  - M8, the art tools: AAA prop tools ported, B1-B3 tileables, look-test scenes tuned (day OK with the user; cellar retuned warm).
  - Asset skills aligned with the style bible (`a67ad7e`).
  - Last green check on a merged branch: `393a2e3` (M2) and `3c1b8cd` (cellar). Main is those plus docs.
- **M3 combat greybox is built but NOT reviewed or merged:** branch `m3-combat`, tip `ee915ed`. `SC RESULT check PASS` (EditMode 483, PlayMode 22), worktree `.claude/worktrees/agent-a3f4b7d611d7e2923`. The play steps are in `docs/design/m3-combat-greybox.md` and the agent's notes. Under the network simulator (148 ms RTT, 2% loss), about half the hits land within one round trip and the rest within about two.
- **User decisions this session:** the PHASE1.md change log and `decisions-for-review.md` A1-A24.
  - A24: the user's cellar reference image is the mood target. It lives at `Reference/private/user/cellar_mood_ref_01.webp`: warm local light pools on neutral grey-brown stone, deep shadow, candle highlights. The cellar band was updated to match.
- **Cloud parity (2026-10-30, cloud session, merged from `claude/eloquent-brown-fopwq1`):** cloud sessions may be the controller and merge on a green `Tools/cloud/unity.sh check` (CLAUDE.md "Cloud sessions" has the PC-to-cloud map). Verified in the cloud: C# compile 11/11, Core tests 194/194, shader check, csharp-ls navigation, live Blender MCP (art environment). Unity launches still need the `UNITY_LICENSE` secret. Setup: `docs/superpowers/handoff/cloud-environment.md`.
- **The user owes:** `docs/superpowers/handoff/USER_TODO.md`.

## Next
1. **M3:** risk-review `m3-combat` (the hit-intent RPC flow, host validation, enemy AI authority, downed/revive, wave director). Fix, re-check, merge. Then ask the user to play the 5-minute combat arena (M3 gate: "the feel is right").
   - The M3 agent made procedural sounds because it believed downloads needed approval. The user allows downloads from trusted sources (memory `web-access-downloads`), so swap in Kenney CC0 packs from kenney.nl.
2. **M9 (user go-ahead given, up to 3 art agents at once, P51):** the beauty corner first (B4 facade kit + B6 scatter, B7 props + B8 stall, B10 fountain hero, B9 foliage). Then the dungeon corner (B5 vault kit, B11 lantern and grate) with the cellar retuned toward the user's reference image: light pools from candles, sconces and hearth; unlit stone neutral; less orange cast. Follow the skills (rewritten and aligned).
3. **M5** (Cellars floors 1-4 generator) can start alongside: room-pool authoring.
4. **M10:** Tobin through the pipeline in the chosen direction (a young viking build plus timber-yard worker; `docs/design/tobin-concepts.md`).
5. Open from earlier reviews: the M2 manual 2-player walk (gamepad, latency, late join). The Mixamo clips are still owed by the user.

## Branches and worktrees
- `main` at `5a1e011`.
- `m3-combat` (`ee915ed`, unmerged) in worktree `agent-a3f4b7d611d7e2923`.
- Worktree `agent-a884291bf0ad9ef08` (M8, finished) and `agent-ad25d53219a200b3c` (M4, finished) can be reused or cleaned up. They have imported Libraries, which saves about 30 minutes each.
- Merged branches: `m1-foundation`, `m4-*`, `m2-hero-camera`, `m8-*`.

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
