# Neon consult (Gemini chat, started 2026-11-22, coordinator 17)

User invited "Neon" (a Gemini chat; author of the AetherStrand hair plugin WIP) to help. Neon has no repo access; everything it knows comes from our messages. Its suggestions are proposals: they go through the normal pipeline (implementer in a worktree, gate/check, reviewer) and recorded decisions change only with the user's OK.

## Exchange 1: brief sent (cohesion gap, Tobin hair, later items)

Neon's reply, summarised:

### Problem 1: cohesion / toy-like gap (ranked)
1. **Asset triage.** Swap for Megascans 3D: card hedges (overlapping bush/bramble/ivy assemblies with jitter), rocks (with calibration), fieldstone revetment, low walls, piers, stairs (kitbash scanned masonry). Keep the procedural shells but break every straight silhouette: eroded top courses, chamfered and noise-displaced corners, timber/lintel stubs, scanned rubble caps on corners and wall tops so no straight edge meets the sky. Backdrop hills: mid-ground silhouette break (treeline cards, scanned cliff outcrops) and more height haze.
2. **Ground contact.** Bake a top-down terrain height + splat-albedo texture at the end of the build and bind it globally. In the ruin/rock/pier shader, blend albedo, smoothness AND world normal toward the terrain sample over about 0.35 m above the ground, with noise. Bake vertex colours in Blender (R cavity/AO, G ground proximity, B up mask, A convex curvature) to drive spec crush, edge wear and moss.
3. **SSAO.** Depth Normals source, High samples, radius 0.40-0.55, intensity 1.6-2.2, Direct Lighting Strength 0.25-0.35 (default 0 leaves sunlit bases floating). Plus a tight grounding pass: a second small SSAO at radius about 0.12, or baked AO decal skirts in prop prefabs.
4. **Detail normals.** A 1-2 m tiled stone detail normal blended with RNM (`BlendNormalsRNM`), faded by camera distance.
5. **Prop clusters.** Anchor, then satellites, then micro-debris. Anchors only at wall corners, thresholds, landings, pier edges and trunks. One hero prop aligned to the wall (±8-15°), 2-4 satellites within 0.4-1.2 m, 4-8 micro pieces welding the footprint. Moss, dirt and weeds masked within 0.8 m of walls and curbs (SDF from footprints).
6. **Clumped grass.** Thomas cluster process: clump centres first, then 3-6 mixed-species tufts each, scale 0.65-1.55, bare gaps between clumps, density x2-3 within 0.5 m of walls, rocks and trunks.
7. **PBR calibration.** Dry rock smoothness 0.05-0.30 (higher only through a wet mask); dark rock albedo lifted to 0.08-0.35 linear luminance; a world-space macro tint (±6-8 % over 15-25 m) on all environment shaders.
8. **APV.** A 4 GB cache suggests the bounds include the distant heightfield. Bound it to the playable village with coarser bricks (claims 2-4 min, under 150 MB). Without a bake: per-zone reflection probes plus a hemisphere bounce term.

### Problem 2: Tobin hair
- Replace both the solver (XPBD: no stretch on fast snaps, per-zone stiffness, drag, analytic capsule collision) and the shader (Kajiya-Kay dual lobe, alpha-to-coverage or a dithered depth prepass), as two separate changes.
- Needs from us: the 42 chains' bone names and hierarchy; head, neck, shoulder (and cheek/jaw) collider shapes; HairDeck UV convention (root V=0 or 1), what the vertex colours mean, which baked maps exist; two motion clips (run-to-stop, dodge/turn snap).
- Agreed to generic preset names (for example LayeredHero_Medium) and an IP-free README before anything enters the repo.

### Later
- Rat-king: use Hunyuan only as a blockout; multires/crease pass, fur cards, kitbashed props, baked cavity/normal maps instead of raw TRELLIS albedo.
- Build loop: dirty-layer sub-scene rebuilds, and all cameras captured in one PlayMode pass. Already partly done in Run 8 T24b/T26.

Neon asked next for: OrchardGap, Orchard and Spawn renders, our SSAO settings, and the ruin shader's roughness/ground-blend section. Anchor frames are not sent: they are third-party captures.

## Exchange 2: R19 frames (Spawn, Orchard, OrchardGap), SSAO values, shader summary

Neon, per camera (checked against OrchardGap by the coordinator: all three faults are in the frame):
- **OrchardGap:** replace the faceted cube rubble in the breach with sunken scanned rubble and ferns (+4-6 claimed). Bugs: a lifted block on the right parapet, a stray "L" artifact left of it, a flat green bottom-left patch with a glossy pasted rock.
- **Orchard:** the foreground slabs float (upper above lower, glossy top); the blocky procedural cart and the bright flat butterfly cards clash. Fix: feather the razor grass-to-dirt edge with clumped weeds, leaves and stones; lift the crushed-black barrel and crate shadows.
- **Spawn:** cobbles cover 45 % of the frame. A hard rectangular lighter patch sits centre-left, the Megascans smoothness reads as plastic, and a flat blue puddle decal sits on top. Second fix: the smooth "Play-Doh" cone hill on the left needs cliff outcrops and ridge trees, plus more haze.
- **Terrain blend:** a top-down ortho bake (Terrain + cobble street meshes), not TerrainData sampling: TerrainData doesn't see the street meshes and would need 12 texture binds. Two global textures (`_SC_GroundHeightTex` R16, `_SC_GroundAlbedoTex` RGBA8 with smoothness in A) plus `_SC_GroundBounds`. Ground normal from 3-tap finite differences; lerp normalWS over the bottom 0.25-0.35 m.
- **Silhouettes:** both. The Blender generator handles coarse shape (eroded courses, rough breach edges, no disconnected teeth, vertex colours) and emits socket empties; a Unity pass drops scanned caps and rubble on the sockets. Hard rule: raycast each piece onto its host and sink it 15-25 % of its height along the normal.
- **Quick wins:** clamp dry masonry and cobble smoothness to 0.28 in SC_WearLit; SSAO radius 0.35 to 0.45, samples Medium to High.

## Exchange 3: anchors A02/A05/A15 (user OK: Neon has private-folder access), full RUINSKIN fragment, HF-energy diagnostic
Asked Neon for: (1) a shader patch (ground blend, smoothness clamp, SRP-batcher safe); (2) the C# bake (deterministic, batchmode); (3) a raycast-and-sink placement helper. The user also attached the `area1-rework` branch in the chat. Foot band noted as wrong on slopes: it measures from the object pivot, not the ground.

### Neon's reply to exchange 3 (code saved in `code/`, not applied)
- Diagnosis: our ruin shader itself makes the 2-3x high-frequency energy (17/m grain normal plus the 9/m edge sparkle), and unclamped smoothness turns that into specular aliasing.
- `code/SC_WearForwardPass_patch.hlsl`: ground height/albedo/normal blend from globals (falls back to the pivot when there is no bake), the foot band measured from the real ground, smoothness clamped to 0.28, grain 0.22 to 0.08, sparkle reduced, the old +0.06 foot gloss removed.
- `code/SCGroundBaker.cs.txt`: a CPU bake (works under -nographics; a GPU Camera.Render is blank there). TerrainData.SampleHeight plus raycasts on the street colliders; R16 height and RGBA32 albedo written as .asset; deterministic. Resolution 512 to start.
- `code/SCGroundBlendBinder_and_Placement.cs.txt`: an [ExecuteAlways] binder that sets the globals, and `SCPlacementGrounding.TryGroundAndSink`: centre plus 4 inset corner rays, a min-support count, a slope limit, snap to the lowest support, sink 20 % (caps: 4 supports, 0.22, no tilt).
- Our review notes sent back: .asset size vs LFS (raw .bytes/EXR?), `textureCoord` needs non-convex MeshColliders, and the 0.28 clamp needs a wet band at the waterline later.

## Exchange 4
Asked Neon for single-markdown-block replies from now on (user request). Asked whether AetherStrand can drive our existing hair_<k>_<i> transforms. The hair rig facts follow in the next message (scanner gathering them).

## Exchanges 5-8 (hair plugin, trial, ground-blend results)
- 5: the hair advice (upgrade our solver in place) is superseded. Neon didn't have the plugin context then (memory: ask Neon what context it has first).
- 6: the AetherStrand v2.1 ZIP + integration guide (`neon_06_...`). The user chose a side-by-side trial (hair-aetherstrand session, branch `claude/hair-aetherstrand-b297e1` at 230ea4f8): integration done, blocked on v2.2 (ShiftTangent compile error, audit kink 155 deg / stretch 52 %). 20-item feedback relayed. User: one hair generator only (AI Studio); v2.2 comes through the user.
- 7: HairDeck to PresetGeometry extractor (`neon_07_...`); interim hair 554 locks = 554 cards.
- 8: ground-blend results relayed (branch `claude/ground-blend-4d3fa9`, 1ee4a6e9): HF -0.001..-0.006, A/B after wins small (Spawn +0.5, Orchard +2, OrchardGap +3, Meadow +1; weak reviews). The gains come from rubble sinking + SSAO, not the shader. Our code fixes are in that branch's `NEON-NOTES.md`. Asked Neon for its top 3 next changes.
- 9: Neon's next-pass ranking (`neon_08_...`): wall-kit regen + Megascans swaps, then ground/foliage, then backdrop. Sent Neon GitHub permalinks (user: links, not descriptions) for the hair trial (230ea4f8) and area1-world (74675fd7); ground-blend links follow its push.
- 10: wall-kit rewrite received (`neon_09_wall_kit.md`, file `code/sc_town_wall_neon_v1.py`, 97,008 bytes, copied from the Canvas Monaco model; syntax ok; +221/-98 vs 74675fd7). Not applied. The user sent Neon the R19 frames zip for the fault audit (Job 2), reply pending. Plan: one Pass A session (wall kit + Megascans breach rubble/ivy, Neon audit fixes) based on the ground-blend branch after it pushes.
- 11: Neon's eye-camera fault audit (`neon_10_eye_fault_audit.md`, all 15 R19 views, top-10 table with source files). Started Pass A (`local_60cfdd45`, Sonnet medium, branch from `claude/ground-blend-4d3fa9`): Neon's wall kit + Megascans breach rubble/ivy, Orchard slab/cart, butterflies; A/B on 6 cameras. Next passes from the top 10: square cobbles (2), backdrop peak (3), shells (4), clumped grass (5), revetment (6), gutters (7).
- 12: official AetherStrand v3.0.0 from AI Studio (user's Downloads, "layered_swept_long", 430 locks). Intake: IP clean; compile fix, Forward+, shadow/light-layer variants, asmdefs, metas, wind 0, head-mesh-bounds H are in. Handed to the hair trial (A/B: shipped preset vs HairDeck). Unverified items left to the trial.

## Exchange (2026-11-23, coordinator Wren): AetherStrand parity + Tobin judging, rounds 1-2
Sent: bug list + measured parity, then (via Drive folder "Hair Trial - AetherStrand", since the pane cannot upload) 4 renders and 5 source files. Neon replied twice:
- Decision: `unity-plugin/com.aetherstrand.sculptedhair` is the canonical zip source; the web generator supplies only PresetGeometry.json, ExportedLook.json and shader constants.
- Q1 best: swept-back, ribbon-halfup, dolphin-long. Strong with eye-clear: five presets, high-ponytail, layered-shag, spiky-hero. Mid: sleek-bob, preset-silver, long-cascade, wolf-cut. Weakest: emo-sweep, asym-cascade, twin-tails.
- Q2 fringe: cut 0 mm with +-6 mm jitter and a brow arch (-22 mm x (1-eyeWeight)); Gaussian sigma 0.030; sweep direction follows the lock's own lateral flow, 5-8 deg by |tipX|. Applied in AetherStrandFringeFit.cs.
- Q3 slider deltas for the weakest three, applied to recipes (clamped to ranges): see `renders/tobin_weakest3_before_after_neon_deltas.png` (spikes gone on emo-sweep and asym-cascade, twin-tails fuller).
- Q4 head fit: X scale floored at 94% of max(Y,Z) (0.31 -> 0.33) to stop temple pinch; nose excluded from the push-out hull; margins bangs 2 mm, crown/back 3 mm (Neon said 4 mm; 3 mm kept the root gap lower). Applied.
Measured after: all 17 styles, vertices inside head at most 0.01% (3.8 mm deep); root gap p95 worst 12.2 mm (swept-back, line was 12), roots inside head worst 0.3% (one preset, line 0%). The two small misses come from the wider temple scale.

## Round 3 (2026-11-23, Wren): post-change judging of all 17 styles
Neon judged the post-round-2 renders (`round3_pack/`): 14/17 PASS; FAIL: sleek-bob (flat unparted curtain), high-ponytail (flat bangs, jagged gap behind ear), twin-tails_n1 (side tails low and forward). Head-fit audit: PASS on all 17, no changes needed. Penetration and root-gap numbers (round3_measurements) accepted against the original pass lines.
Applied: fringe sigma 0.030 -> 0.034, arch -0.022 -> -0.026, centre-part lift +3 mm (AetherStrandFringeFit.cs); slider deltas for the three weakest as `recipes/*_n2.json` (sleek-bob bangsSweep clamped at 1.0). Neon's structural note for twin-tails (tie roots +30 mm up, -25 mm back in the archetype builder) is not applied: sliders alone already clear the ears; revisit if round 4 still fails it.
Result renders: `renders/k3_*_n2_4view.png` (parted fringe on twin-tails and high-ponytail, ears clear). Not yet re-judged by Neon (optional round 4).

## Rounds 4-5 (2026-11-23, Wren): weakest three re-judged, 17/17
- Round 4: re-measured all 17 after the fringe change (penetration worst 0.01% / 4.1 mm, root gap p95 worst 8.5 mm, roots inside 0.0%). Neon: sleek-bob_n2 PASS, high-ponytail_n2 PASS, twin-tails_n2 FAIL (tails too low and wide, boxy tie shelf). The other 14 keep their PASS (fringe change small).
- Applied Neon's twin-tail change in the web archetype builder (`hairBuilder.ts` twin-tails branch): anchorPhi 0.86 -> 0.71, anchorTheta 1.96 -> 2.08, root spread x0.7 (copy: `tools/hairBuilder_twin_tails_n3.ts`; also pushed upstream).
- Round 5: twin-tails_n3 PASS. Scorecard 17/17 PASS on Tobin; Neon cleared the canonical unity-plugin build to ship. Evidence: `round4_pack/`, `renders/k5_twin-tails_n3_4view.png`.
- Gemini UI note: the Send button works only after typing with the real `type` action and clicking by coordinate in a fresh tab; JS-inserted text never submits.

## Rounds 6-7 (2026-11-23, Wren): Neon authors new hairstyles (the AI loop, first real run)
- Round 6: Neon wrote 4 schema-2.0 recipes from character briefs (young hero, gruff veteran, village healer, rogue): `recipes/new_*.json`. No edits by us.
- Pipeline ran unchanged (web export, Tobin fit, eye-clear, push-out): penetration worst 0.01% / 4.6 mm, root gap p95 worst 8.5 mm, roots inside 0.0% (`round6_pack/`).
- Round 7 verdicts: Squire PASS, Apothecary PASS, Cutpurse PASS, Ironwatch Captain FAIL (centre inverted-V gap and wispy bangs read youthful, not a disciplined veteran cut).
- Applied Neon's Captain deltas (`new_captain2.json`): still a wide centre gap, and max penetration depth 6.0 mm (on the <6 mm line). Not re-judged: our read is the 12 archetypes cannot make a cropped veteran. Neon's ask: add a short-crop / slicked-back archetype (short tapered sides and nape, no hanging forehead bangs). That is the next build, not done.
- Loop verdict from Neon: works, 3/4 first-pass.

## Rounds 8-10 (2026-11-23, Wren): roles flip, judged against a real reference
- Round 8-9: Neon (as judge) failed short-crop v1/v2 (loops, receding hairline). Against the real crew-cut photo (`Reference/private/hair_refs/Crew_Cut_Semi_Short_Taper.jpg`, Wikimedia Commons, public domain) both were really short bobs.
- User decision: Neon now SEEDS archetypes (code) and generates multi-view reference sheets with Nano Banana; Wren judges against real/anime/game references and iterates (memory `neon-seeds-wren-judges-hair`).
- Round 10: Neon returned 8 Nano Banana reference sheets (realistic + in-game for short-crop, samurai-topknot, braided-crown, undercut-fade; images live in the Gemini thread, download button did not save in the pane) and 4 archetype code blocks. `neon_10_short_crop_seed.ts.txt` = its short-crop block (applied). Topknot, braid and undercut blocks received, not applied yet.
- Wren iteration on Neon's short-crop (judged vs the real photo): hairline raised phi 0.86-0.98 -> 1.22-1.36, side cap 1.26-1.44 -> 1.55-1.72 (ear top), nape 1.66-1.92 -> 1.95-2.15, front rings reach the hairline, foundation shell 20/30 locks width 0.095, side rows 56. Result `renders/captain_shortcrop_v3_neonseed.png`: crew-cut silhouette matches (hairline, ear-top taper, short nape); penetration 0.00%, root gap p95 7.8 mm. Gap: leaf-shaped tips read pointy and the nape band is sparse next to the photo's dense stubble.
