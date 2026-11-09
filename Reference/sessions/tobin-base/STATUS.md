# STATUS: tobin-base (local session, 2026-11-08)
State: paused for usage limit. Branch `claude/pensive-clarke-e25tku` tip has import, materials and face work. Nothing merged to main.
Archive: not ready (hair and underlay open)

## Gates
- 1 Intake: done. Sliders GLB in LFS, file 3 on PC only, `Art/Source/MetaPerson/SOURCES.md`.
- 2a Humanoid: pass with one user-approved exception (Head slot set to `Head`; automap chose Neck2).
- 2b Shapes: pass (51 ARKit, 15 visemes, sliders move vertices).
- 2c Y Bot clips: rig tracks Y Bot on 10 clips plus arms overhead. Underlay failed: shoulder flap, armpit tears, crotch cavity, ragged collar and upper-arm seams. User chose to continue; fix after hair.
- 2d Face: pass by user ("looks solid"). Closed lids muddy, lashes speckled, jaw capped 0.5. uLipSync 3.1.5 compiles; vowel test inconclusive (stock profiles, TTS); user deferred lipsync until real dialogue exists.
- 2e Scale: 1.0425 (measured 1.8225 m source), 1.90 m. Soles sit 1.2 cm above origin.
- 3 Texture: user accepted. Brow 76% of Tripo by Laplacian; head is 2048 vs Tripo 4096. Tan tint `SkinTint` (0.95, 0.87, 0.78).

## Hair
User chose strands over rigid mesh. `claude/tobin-hair-strands` 310dcaa4: Unity Hair embedded and patched (renames, `PATCHES.md`), renders and simulates on 6000.6.5 (12k strands, RTX 5070). Flat bowl cut, fast head turns lose strands, placeholder shader. Demo: `TobinHairDemo.unity`, never played live.
Next: DiffLocks local image-to-strands (session difflocks-install): 12 GB check, install under `C:\AI\difflocks`, one test. Then Alembic into Unity, or cards with bone chains (user sent a hair-cards workflow as reference).

## Open
- Underlay fix (step 5, raised priority).
- Head template spike: session head-factory-spike, branch `claude/face-template-spike`.
- Step 6, not sent, for the user to send to support@avatarsdk.com: "Hello, I am making a personal co-op game for myself and friends, not sold. May I use MetaPerson Creator exports, including paid-coin ones, as in-game characters? Can I export with no outfit or full-body skin, or a parameter to keep hidden skin? Thank you."
