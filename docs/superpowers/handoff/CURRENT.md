# CURRENT: Sleeping Calamity running handoff

## Now
2026-10-30, session 2 (controller on the PC). Phase 1 underway.
- **Beauty corner all real art except neighbours** (`23bf811`, check PASS EditMode 528, PlayMode 31 + 5 skipped; user: "looks good, keep going" on the 4 captures). Look pass + `CobbleSpawner` (scene 0.65 MB, deterministic, builds churn-free), own `BeautyCorner_Renderer`. Open art rounds: B4 timber frame + neighbour variants, B10 muzzle/lids, B6 edge fade + far tile, B7 crate tone (then done), B9 lintel ivy/grass/climbing ivy; B8 done at 30. Rule changes: target = first score + 5 when round 1 hits 25 (`68f7ab4`), reference-beside-render sheet every round (`9d24497`). Study refs: `Reference/private/{dc1_town,kh2_twilight,totk}` (KH1 not captured; no trusted KH free cam; PCSX2 only for free-cam looks, combat data from REA/decomp). M3: 2P walks skipped for now (user); enemy polish branch `claude/m3-enemy-polish-13a61c` passed risk review, fixing 2 should-fix items before merge.
- **M9 beauty corner team running (2026-10-30 night):** brief `docs/art/beauty-corner-brief.md` (`a71392d`, decisions `4a7d8f6`: sleeping-beast spout, cream/oak/green facade, medium whimsy, produce stall; review calibrated to stop at 25/40). Cloud requested: B4 facade, B10 fountain, B8 stall, B6 ground (generators only). Local worktrees: B7 props, B9 foliage + lanterns (adopt + export to `Content/Art/Town/`). **`BeautyCornerBuilder` merged** (`9731f46` + meta `89571d6`; `SC RESULT check PASS` EditMode 525, PlayMode 31 + 5 skipped): scene `BeautyCorner_Hollowmere`, placement table `BeautyCornerTable.cs`, art drop-in from `Content/Art/Town/<Batch>/<Asset>/`, captures `BeautyCornerCapture.CaptureAll` (`SC_CAPTURE_DIR`). Score rule now moving: first score + 5 when round 1 hits 25 (`68f7ab4`). **First real art in the corner** (`d541d9b`, check PASS EditMode 525, PlayMode 31 + 5 skipped): B9 ivy/weed/moss + B8 lanterns; B9 round 4 feedback sent (ivy density/colour, bigger weeds). **B11 lantern 4K bake done** (branch `worktree-agent-a34b1608adb1299aa` `5bd381c`, user approved the render 2026-10-30), merges with the next art batch. PC bake station running B8 stall 29, B4 facade 27.5, B6 cobbles 28. Known churn: every build rewrites the corner scene and flips `M_Pollen` `_SrcBlend` (revert before commit; fix the builder). Controller subagents: PCSX2 DC1 town refs (`Reference/private/dc1_town/`), web refs.
- **Merged to main:** M1, M2, M4, M8 (session 1) and now **M3 combat greybox** (`af71ed0`, `SC RESULT check PASS` EditMode 484, PlayMode 29 + 1 skipped). Risk review, fix re-check and branch review all passed; the fixes: unique host-wide enemy attack serials, spell pip spent at cast, finisher prior from the owner, host-written weapon swap, deterministic netsim tests (0% loss: every hit within one round trip), downed heroes can't jump, out heroes stand up at wave clear. Sounds are Kenney CC0 (`docs/audio/SOURCES.md`).
- **M3 solo feel PASSED** (user, 2026-10-30: yes to all 8 retest questions; axe sits crooked in the hands; wants enemy attacks readable without ground indicators in the real game). M3 still waits only on the 2-player netsim walk (no second player yet).
- **UGS linked** (`7e9c6c7`): Authentication, Relay and Lobby on the dashboard. Packages kept per the user: Microsoft GDK, IAP 5.4.4, patch bumps (`b4563ac`, `c2fd91e`).
- **Animation sources gathered** (decisions A25): Quaternius UAL1/UAL2 free (CC0) in `C:\Dev\_private\quaternius\`; Kevin Iglesias FREE packs (Basic Motions, Archer, Spellcasting, Crafting, Throwing, Skeleton) in the Asset Store cache `%APPDATA%\Unity\Asset Store-5.x\Kevin Iglesias\`; Mixamo Y Bot + 4 idles in `C:\Dev\_private\mixamo\`. About 12 Mixamo clips still owed (`docs/design/mixamo-clips.md`). Research: `docs/research/animation-sources.md`, `docs/research/libraries.md`.
- **Decisions this session:** `decisions-for-review.md` A25-A30. PCSX2 free cam (PINE) waits for M9 look work (user).
- **The user owes:** `docs/superpowers/handoff/USER_TODO.md` (about 12 Mixamo clips; 2-player walks when a second player is available).

## Next
1. **M3 polish:** axe grip fixed (`bb027c9`, user note 2026-10-30); parked by the user ("solid enough to continue"): retune the wave 4 to 5 x3.5 attack step with `docs/research/combat-timings.md` (DC1 `4d9bbca`, KH1/KH2 `671c211`); design enemy telegraphs read from the enemy itself (wind-up pose, anticipation, sound, glint), ground footprints only as an optional assist. Readability pass `d60b4b8` (check PASS `1c44f60`); bot playtester `ea42243` + pillar fix `cf6a6e5` (check PASS `5c4bced`; `SC_BOT=1`) can verify the retune.
2. **Hero animation merged** (`e53369b`, `SC RESULT check PASS` EditMode 502, PlayMode 30 + 2 skipped; the skips are Explicit capture tools). Y Bot plays Quaternius/Kevin Iglesias/Mixamo clips via `AnimatorHeroDriver`; owed Mixamo clips drop into `Content/Animation/Mixamo/` by name hint (`docs/animation/SOURCES.md`; build log prints `SC ANIM placeholder`). Rough: bow aim pose, guard shield placement.
3. **M9** (up to 3 art agents, P51): beauty corner first (B4+B6, B7+B8, B10, B9), then the dungeon corner (B5, B11) with the cellar toward the user's reference image (A24).
4. **M5** room-pool authoring alongside; **M10** Tobin after.
5. Open: M2/M3/M7 2-player walks wait for a second player. M4/M6 persistence must key growth by the persistent player id, not the session client id (`CombatHost.cs:95`).

## Branches and worktrees
- `main` at `3d5d18d` (green; check PASS on the axe fix `bb027c9`: EditMode 519, PlayMode 31 + 5 skipped; later merges are art/docs only), local and GitHub in sync. `claude/intelligent-cray-i7vppb` (cloud unity.sh cold-Library timeout) merged 2026-10-30.
- B11 cellar lantern merged by its cloud session (`7f9adc1`, look locked 28.5/40); free asset sources in `Art/Source/`. No open branches besides its merged `claude/practical-wozniak-vk1q0j`.
- Cleanup 2026-10-30: finished agent worktrees and all merged local branches removed. 16 merged remote branches deleted on GitHub (user OK). Session worktrees `determined-boyd-9ac4b8` and `infallible-bassi-581d4e` stay until their sessions are archived.

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
- Trial ramenvr/autopilot (AI playtester in the editor) as an automated smoke tester once M3 combat settles; evaluated 2026-10-30 by a side session, nothing installed.
