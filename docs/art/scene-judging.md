# Scene judging v2 (whole-area world views)

Decided 2026-11-17 (judging-v2 session); the current direction, open to change after playtest or the user's calibration. Use it for eye-level views of a whole place (the Run 7 `HM_Eye_*` cameras, corner captures, dungeon rooms). Single assets keep the per-asset rubric in `.claude/skills/sc-asset-review/references/rubric.md`.

## Why v2
World views were scored with the per-asset rubric (silhouette, edges, micro-detail, glass/metal). It doesn't measure what makes a world feel AAA (depth layering, density, weathering in place, light zones, life), it states a built-in ceiling ("harsher than shipped games' own art scored blind"), the builder graded its own work, on downscaled tiled sheets, and no real AAA frame was ever scored on the same card. Scores stalled at 20-25/40 and stopped telling rounds apart.

## 1. Scorecard (10 lines, 1-5 each, total /50)
5 = what the best shipped AAA worlds show at eye level; 4 = shipped quality; 3 = competent but plainly behind; 2 = clearly unfinished; 1 = greybox or broken. A real AAA frame lands around 4-5 on most lines: there is no ceiling. Halves allowed. Each line gets a note of at most 8 words naming the visible thing that set it.

| # | Line | 5 looks like | 3 looks like | 1 looks like |
|---|---|---|---|---|
| 1 | **Depth and atmosphere layering** | 3+ depth planes (near, mid, far) each separated by a visible step in value, contrast or haze; far plane lighter, cooler and softer; silhouettes overlap and the eye travels into the distance | 2 planes read; the far plane is as crisp and dark as the near, or the haze is one uniform veil | everything at one value and sharpness; a flat wall or a void behind the subject |
| 2 | **Light direction and light zones** | key direction obvious from cast shadows; shadows shape the forms; pools of light and shade alternate along the view, so the eye has lit places to go | direction readable but the frame is evenly lit, or the shadows are blobs that don't shape anything | flat ambient light with no direction, or one shadow covering everything |
| 3 | **Shade fill and highlight handling** | shade has coloured fill (sky-blue, bounce-warm) with detail still readable; brightest areas roll off softly with texture left in them; small bright accents; nothing pure black or blown white in large areas | shade readable but grey or muddy, or a large area (sky, ground, wall) clipped flat white or crushed | shade is black holes or milky; highlights blown in big flat patches |
| 4 | **Density and natural placement where the eye lands** | the surfaces near the path and the focal area carry dressing at 2-3 scales (structures, props, small clutter, plants) in natural clusters with breathing room; every item sits where it would have fallen, been dropped or been put to use (against walls, in corners, by doors), at a believable size; no bare stretch over about a quarter of the frame | the hero spots are dressed, gaps between them are bare; scatter looks evenly sprinkled or repeated; one or two items look oddly placed or oddly sized | big bare planes; props stand singly in the open, as if placed in an editor |
| 5 | **Weathering and ground contact** | things grow out of the ground: moss, damp and grime at wall feet, root flare at trees, rubble drifts and spill, grass in joints, soot or char where fire was; wear follows water and hands | wear is present but even, like a mask; some objects meet the ground on a hard clean line | showroom clean; hard intersection lines; objects float or slice into the ground |
| 6 | **Material realism at mid distance (5-30 m)** | each material identifiable at mid distance; roughness varies (wet vs dry, worn vs rough); no visible repeat; large surfaces broken by macro variation | materials read, but one repeats visibly or looks waxy or plastic; large surfaces are uniform | obvious tiling grid, plastic sheen, or smeared low-resolution surfaces |
| 7 | **Composition and focal point** | one clear focal point the eye hits first, framed by leading lines and edges; 3-4 big value masses still read when the frame is squinted at thumbnail size | something to look at, but it competes or sits dead centre without support; masses muddle at thumbnail | no subject: a wall in the face, sky and ground halves, or noise |
| 8 | **Life and motion cues** | visible signs of ongoing life and time: smoke, birds, motes or pollen, wind-bent grass or trees, ripples, lit windows, cloth, animals or people (three or more independent cues) | one or two cues | frozen and sterile |
| 9 | **Cohesion: one visual language** | every element shares one shape language, material fidelity and palette; nothing looks imported from another game | mostly coherent; one element clashes (a photoscan rock beside toy shapes, a neon or grey-photo material, a different texel density) | kit-bash: elements from several styles or fidelities |
| 10 | **Play-path and invitation to explore** | where you can walk reads at once (ground value or material contrast, edges, openings, a lit destination), and the frame promises things to find: side openings, nooks, half-hidden corners, a path bending out of sight, spots where herbs or a small treasure could be | the path is findable, but the view is a display to look at from one spot; nothing pulls you off the main line | can't tell walkable from blocked, or a dead end with nothing to do |

Line 9 judges internal cohesion so anchors from other styles can sit on the same card. Fit to our own target (PHASE1 section 3: Dark Cloud shapes in OoT-2026 stylised realism) stays the user's call on captures.

The old /40 numbers are not comparable with this /50; don't convert between them.

## 2. Method
1. **Frames.** Full-resolution single frames (1280x720 captures), never tiled sheets. Anchors are normalised to the same size and JPEG quality (crop letterbox, HUD and watermarks, resize to 1280x720, q92, no EXIF), so the reviewer sees like for like. Downscale only on sheets made for the user.
2. **Anchors in every pass.** The scoring pass always holds the anchor set (`Reference/private/judging-v2/anchors/A01-A12`, sources in its `SOURCES.md`) beside our frames, shuffled together under opaque names (`X01.jpg`...), with a new shuffle per run. The reviewer looks at the whole set once to set the scale, then scores every frame. The key stays with the session, never in the brief.
3. **Separate reviewer.** A fresh `reviewer` agent (Sonnet) that did not build the work scores; the builder never grades its own round. It reads only the scorecard and the folder, judges by looking (no scripts), and writes JSON.
4. **Two runs, mean.** Two independent reviewer runs (different shuffles, run in parallel, neither sees the other). Report the mean per frame. When a frame's two totals differ by more than 4, run a third pass on that frame (with the anchors) and take the median.
5. **Pairwise A/B for rounds.** To ask "did this round help?", show the same camera before and after as A/B, order shuffled (and reversed between runs), tie allowed. The verdict must name the region (pixel box) and the visible difference.
   - **Diff gate first.** Run `python Art/Blender/tools/sc_imgdiff.py a b` on the pair. Under 0.5 the pair counts as no change and is not sent to a reviewer: our proof showed reviewers invent differences on identical frames (section 4).
   - **Check the box.** Above the gate, the region the reviewer names must contain changed pixels; a verdict pointing at an unchanged region is discarded.
   - **Null pairs in every batch.** Mix one or two null pairs (identical or render-noise-only) into each A/B batch and tell the reviewer some pairs don't differ. A batch whose reviewer prefers a side on a null pair is not trusted.
6. **Diagnostics support, never score.** `python Tools/art/sc_scene_diag.py <frames...> --json out.json --sheet out.png` (Pillow + numpy). It prints band luminance (sky/far/mid/near as image rows: a proxy for depth planes, not a depth buffer), clip and crush, a haze proxy (far vs near contrast and saturation), an FFT autocorrelation tiling peak, the empty-cell ratio and largest bare patch, and the radial-spectrum energy split. Read each number against an anchor or the previous round of the same camera. Use it to point the reviewer or the builder at something, never to pass or fail.
7. **Calibrate to the user.** Each time the anchor set or the card changes, the user scores 4-6 of the same frames (mixed ours and anchors) on a calibration sheet. The offset between their scale and ours is recorded in section 5 and applied when reporting to them.

## 3. Same-camera toggles (from Epic's Abbey doc)
Epic's Shadows of the Abbey page proves each technique with a before/after from one camera: shell only vs shell + dressing, bounce lights off vs on, grass layer vs painted mud, Epic vs handheld tier. Do the same: every Run 7 change that targets a scorecard line gets a same-camera toggle pair (the feature on vs off, or round N vs N-1), judged blind under section 2.5. The private copies are in `Reference/private/abbey/docs/` and double as known-improvement tests for the A/B method.

Abbey numbers that inform the 5-descriptors (our reading; the page is a night horror island built on Nanite, so take its layering and reuse ideas, not its palette or triangle counts): 306 unique meshes placed 12,747 times (about 42 each); about 970 off-grid, tilted dressing pieces on top of 675 snapped shell pieces in the catacombs; 351 decals (damage, moss, mud, leaks, brick); baked AO and a moss mask in vertex colour; darker roots on every plant; one world-space detail layer shared by cliffs, rocks and walls, so rocks blend into the ground; overlay detail fading out past about 40 m through one shared setting; 48 shadowless bounce lights for fill; lights flickering by world position; 44 fog-card groups; one colour grade per zone. Deeper notes: `abbey-fab-deep-dive.md` in the coordinator's side-session notes folder.

## 4. Proof run (2026-11-17)
Frames: 12 anchors (OoT 2026 remake x4, a UE5 Megascans village x2, NieR Replicant x3, Fable III, Fable Anniversary, TotK; no Abbey eye-level frames: Epic's doc has none, and the island can't be downloaded) and 6 of ours, the latest round holding each camera: Postcard, MarketStreet, Spawn, Bakery (R7), Orchard, HillWest (R4; R5-R7 only recapture about ten inside-wall cameras). Known compromises: Link is visible in three OoT frames, one NieR frame is slightly raised, one Fable frame was upscaled 1.42x.

**Absolute scores (two blind Sonnet runs, /50):**

| Frame | Run 1 | Run 2 | Mean |
|---|---|---|---|
| A01 OoT market square | 45.5 | 44.0 | 44.8 |
| A03 OoT stone street | 44.0 | 41.0 | 42.5 |
| A02 OoT ivy ruin arch | 43.0 | 42.5 | 42.8 |
| A06 UE5 village lantern path | 41.5 | 39.5 | 40.5 |
| A08 NieR sandstone ledge | 37.0 | 42.5 | 39.8 |
| A04 OoT lake at dusk | 41.0 | 38.5 | 39.8 |
| A05 UE5 village at dusk | 40.0 | 39.0 | 39.5 |
| A07 NieR arched plaza | 37.5 | 38.0 | 37.8 |
| **ours Postcard R7** | 35.5 | 37.5 | **36.5** |
| **ours HillWest R4** | 31.5 | 41.0 | **36.3** (outlier, see below) |
| **ours MarketStreet R7** | 34.5 | 36.5 | **35.5** |
| **ours Spawn R7** | 35.0 | 34.0 | **34.5** |
| A09 NieR desert town (raised) | 34.0 | 33.5 | 33.8 |
| **ours Bakery R7** | 32.5 | 33.0 | **32.8** |
| A11 Fable Anniversary town | 32.5 | 32.5 | 32.5 |
| **ours Orchard R4** | 33.5 | 30.0 | **31.8** |
| A10 Fable III village gate | 32.0 | 31.5 | 31.8 |
| A12 TotK cottage field | 31.5 | 32.0 | 31.8 |

- **Reliability:** mean |run1 - run2| = 2.0/50 (pass, target 3); per line 0.17-0.33. Spearman rank correlation 0.70 (target 0.8: miss), pulled down by one frame: HillWest 31.5 vs 41 (run 2 read the bare slope as "rocks, grass, wall, trees at scales"). Hence the third-pass rule in 2.4.
- **Separation:** anchor median 39.6, ours 35.0. The four OoT frames and the UE5 village all sit above every frame of ours; the older or plainer anchors (Fable, TotK field, raised NieR) sit level with our middle. Order within ours matches the builder's own order except HillWest: Postcard best, Orchard worst.
- **Where the gap is (mean line scores, ours vs top five anchors):** weathering and ground contact (ours 2.8-3.5, anchors 4.0-4.5: "props float, clean cobbles, no grime"), cohesion (3.0-3.8 vs 4.2-4.5: "oversized toy props clash with realistic trees"), density (3.2-3.5 vs 4.2-5: "props evenly sprinkled"), material realism (3.2-4.0 vs 4.2-4.5: "cobble repeats"). Light direction and composition are already near anchor level.
- **Diagnostics agree:** our frames carry 2-3x the high-frequency energy of the anchors (HF share 0.08-0.23 vs 0.01-0.10): busy surface noise everywhere rather than detail grouped into masses with rests. Abbey's 40 m detail fade and vertex AO are the matching fixes.

**Pairwise A/B:**
- Known improvement Meadow R1 -> R4 (opposite order in the two runs): R4 picked both times, naming the dense grass, textured wall and rocks at the wall foot. Pass.
- Null pair Postcard R1 -> R2 (sc_imgdiff 0.24, only grass jitter changed): both runs picked a side and described a difference that isn't there (and contradicted each other). Fail; fixed by the diff gate and null pairs in 2.5.
- Subtle Postcard R6 -> R7 (sc_imgdiff 0.65, a local change): one run picked R6, one called a tie. Inconclusive, as expected for a change this small.
- Second batch (one reviewer, seven pairs normalised to 1280x720, told that several pairs may not differ): Epic's three toggles (shell vs dressed, bounce off vs on, grass vs mud) and Meadow R1 -> R4 all picked the right way (three at high confidence, the mud pair at low), naming the right region and line (density, shade fill, density, weathering); both null pairs (a pixel-identical Spawn pair and Postcard R1/R2) called ties. Pass: telling the reviewer that nulls exist removed the confabulation. Postcard R6 -> R7 again went to R6 ("B: broken, stacked rubble blocks with ragged edges" vs "smooth intact brick walls" in R7, medium confidence).
- **Signal for Run 7:** across four looks, Postcard R6 -> R7 (shell round 3c, flat-faced stacks) was judged worse three times and a tie once: at that distance the flatter stacks read as cleaner, less ruined walls. Check it on a same-camera toggle before building more on round 3c.

## 5. Calibration to the user
2026-11-17, six frames, user out of 10 vs reviewer mean (/50 shown as /10):

| Frame | User | Reviewers |
|---|---|---|
| A01 OoT market square | 9 | 9.0 |
| A07 NieR arched plaza | 8 | 7.6 |
| A12 TotK cottage field | 6 | 6.4 |
| ours Orchard R4 | 7 | 6.4 |
| ours MarketStreet R7 | 7 | 7.1 |
| ours Postcard R7 | 5 | 7.3 |

- **Scale:** on the anchors the user and the reviewers agree within 0.4/10, and the mean offset over all six is -0.3/10 (-1.5/50). The v1 problem (our scores 8-10 points low on /40) is gone: report the reviewer mean as is.
- **What the reviewers missed (user's words, summarised):** immersion you can see before you feel it, as in the Abbey, OoT and NieR; Postcard "looks empty, nothing to do but look around from one spot, nothing says explore me", with oddly placed toy-like props on a bare plaza. Orchard looks fuller and like a place where you could find something (herbs, a small treasure), with moody light, natural cohesion and depth; its placement is still a bit unnatural, but more correct than elsewhere in Hollowmere.
- **Card change from this:** line 4 now asks for natural placement and believable size, and line 10 for an invitation to explore. Postcard was not re-scored under the new wording; the next reviewer pass will show whether the gap closes. If ours keep scoring above the user's eye, weight lines 4 and 10 double for our own frames' summary.

**Run 3 (same day, after calibration):** new card wording (lines 4 and 10) and four Abbey anchors captured in Unreal Editor for Fortnite from the template itself (A13 abbey arcade, A14 abbey nave lane, A15 cemetery lion and lamp, A16 round wall with lit statue; 170 cm eye height, line-traced ground; 16 usable views in `Reference/private/abbey/eye/`, the user's own four picks in `Reference/private/abbey/user/`). One blind run, 22 frames:
- Abbey anchors 38.5-44.5 (the cemetery view is the top frame of the set, above the OoT market at 42.5). Night and storm did not count against them.
- Ours dropped 3-5 points under the new wording, mostly on lines 4 and 10: MarketStreet 32.5, Postcard 31.5 (6.3/10, toward the user's 5), Spawn 31, Bakery 31, Orchard 30, HillWest 29.5. Orchard still sits under the user's 7.
- Separation is now clean: all twelve modern anchors (OoT, UE5 village, NieR plaza and ledge, Abbey) score above every frame of ours; ours sit level with the older or plainer anchors (Fable, TotK field, raised NieR) at 29.5-31.5.
- Pairs: Meadow R1 -> R4 right, null Postcard R1/R2 a tie, Postcard R6 -> R7 again went to R6 ("greener growth at wall bases and fuller ruins" in R6). That is three of four looks against round 3c.

Comparison sheet (all 18 frames ranked, ours marked): `Reference/private/judging-v2/sheets/comparison_18.jpg`; calibration sheet: `calibration_6.jpg` beside it.

## 6. For Run 7 (next resume brief)
- Score world views with this card, not the per-asset rubric; two blind `reviewer` runs with the anchors in the pass, mean per frame. Never self-score.
- Prove each task with a same-camera toggle or round pair through the A/B method (diff gate, null pair, named region).
- The user's eye weighs natural placement and "something to explore" most (section 5): dress for discovery (nooks, side openings, herbs, small finds against walls and in corners), never props stood alone in the open.
- Biggest visible gains, in order: ground contact and weathering (vertex AO and contact decals at every wall and prop base, grass in joints, rubble spill); clustering the dressing (clumps at corners and doorways with bare rests between) instead of even scatter; a shared detail fade at about 40 m plus quieter ground detail to bring the high-frequency energy down; one shared material language between props and trees (prop scale and chroma toward the trees' realism).
