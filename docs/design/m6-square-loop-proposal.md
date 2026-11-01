# M6 Hollowmere square loop: proposal (2026-11-01)

Status: **PROPOSAL, for the M6 design session with the user.** No code yet. Settled inputs: PHASE1 7.1, P20-P28, P48, amendments A34-A36; the Hollowmere mass-production design (foundations by size class, four turns, frame first then parts, never block progress, path-refused buildings). This note does not reopen those. It covers what they leave open.

## 1. The loop, step by step
1. **Start in town.** The host loads the world. The town, the banked stock, the baker's status and everyone's weapons come back from the save. Players spawn by the heroes' home (the cistern house).
2. **Dive.** Take the cistern-house cellar stair (P25) into Cellars floor 1. The M5 dive runs unchanged: stairs, checkpoint after floor 2, stir meter, fail.
3. **Carry and bank.** Bakery pieces from floors 1-3 and chest loot go into the shared, host-held haul (P22). Banking at the floor-2 checkpoint or the floor-4 heart makes them safe (P20).
4. **Rescue.** At the floor-4 heart, the bank there also adds **the baker** to the haul as a resident. "Go home" opens.
5. **Home.** Going home lands the party at the stair top in Hollowmere. The banked haul moves into the **town stock** (a shared list of pieces, residents and loot), and the stir meter resets.
6. **Place.** Any player opens placement mode at a build point (proposed: the bakery's old doorstep or Wren's list wall). They pick the bakery frame from stock, see free foundations of its class (L) lit up, turn it in four steps, and place it. The host refuses it if it would cut the NPC path graph. Then they slot the oven, door and sign onto the frame's sockets; each one appears as it lands.
7. **Live wish preview.** While the frame is held, the baker's three wishes show in plain words with a live tick ("near the fountain", "door faces the square", "not right by the inn"). They stay ticked or unticked after placing.
8. **Move in.** Once all four parts are placed, the bakery counts as complete (P28) and the baker can be placed into it. He appears at his door.
9. **Wish met, service opens.** With all three wishes ticked, the baker's service opens: healing loaves for the next dive (P48; three per dive, P27).
10. **Save.** The host saves on bank, on leaving placement mode and at shutdown. Pick-up returns any building, part or resident to stock, so no layout can soft-lock the world.

Solo runs the same steps as a local host. In co-op, every step is host-authoritative: clients send requests, and the host checks every rule again.

## 2. What exists vs what's missing
| Area | Exists (main at `498924b`) | Missing for M6 |
|---|---|---|
| Haul | `Core/Cellars/DiveHaul.cs` (pieces as int ids `floor*100+idx`, loot `itemId->count`, banked vs unbanked, `FailDive` returns pieces to their floors); `DiveState.BankInTown`; `DiveHaulSync` (host owns state, `Summary` NetworkVariable, bank RPC) | A table that maps a piece id to a building and part (`bakery/frame`, `oven`...); the baker as a haul entry; banked haul to town stock; persistence (the haul dies with the session) |
| Dive flow | `DiveDirector` (go home, fail, stairs, teleports), `DiveHub` in `Arena_Dive` (stair stand-in, floors built at an offset origin) | The hub in Hollowmere at the cistern-house stair; landing in the town after go-home or fail |
| Scenes | `SceneFlow` (host Single loads), `SceneNames.BuildOrder` (no Hollowmere), `HollowmereBuilder` building Ruined and Rebuilt as editor-time presets | Hollowmere as a session scene; a runtime town view driven by saved state instead of a preset |
| Town data | `Core/Town/TownLayout.cs`: foundations (id, ring, S/M/L, yaw, door side), landmarks, the NPC path graph with `RootNode` reachability, `Validate()`; `HollowmereLayout.cs` (F02 bakery, F09 inn in the Rebuilt preset) | Runtime placement state (what stands on which foundation, its turn and its parts) |
| Placement, wishes | Nothing: no village, wish or Georama code in the project. The FishGame parts bin (`Core/Village/Placement`: `PlacementWish` AtLot/NearPoint/FarFrom) isn't cloned in this cloud checkout | The port, plus Facing, NextTo, InArea; foundation snap and path-refusal rules; a wish evaluator and score; the placement mode (input context row, ghost, turn, pick up, camera, UI) |
| Residents | Baker is a floor-4 marker material only | A greybox baker NPC, his wish list as data, the service hook |
| Save | `WorldSave` v1 (seed, ids, `players`), `WorldSaveRules.Clean` (per-section reset), `WorldSaveStore` (`Sections` table, `.bak`, `.corrupt`), `WorldSaveFlow` (host) | Sections: `weapons`, `stock`, `town`, `residents`, `dive`; their `Sections` rows, `Clean` rules and tests |
| Weapons | `Core/Weapons/WeaponsSave.cs` (keyed by `OwnerId` = `PlayerKey.Id`, round-trip tested, **not wired**); `CombatHost` keeps growth by `clientId.ToString()` | A host map from client id to `PlayerKey` (the roster already knows both at approval); growth keyed by `PlayerKey`; restore on spawn; capture on save |
| Data | `Data` assembly is empty; all content is static Core tables with int or string ids | Keep it that way for Phase 1 (Core tables: pieces, buildings, residents, wishes) |

## 3. Open questions for the user
**Q1. How does the party get home?** (scene shape)
- A. **Hollowmere becomes the hub scene.** Floors are built at an offset under the town, as `Arena_Dive` does now. No scene loads during play.
- B. Two scenes, Single loads between Hollowmere and a Cellars scene.
- C. Hollowmere stays loaded, and each floor loads additively.
- **Recommend A.** It reuses the M5 spine almost unchanged and avoids NGO scene-switch pitfalls (riders, late join, in-scene spawns). Trade-off: the town's grade, fog and sky must not leak into the cellar. Each area needs its own volume and its own light setup, and the town is a heavy scene to keep loaded during a dive. B is the fallback if A's look can't be separated.

**Q2. Who can place in co-op?**
- A. **One builder at a time:** any player opens placement mode, and the host gives them the builder's lock. The others keep walking and see the ghost and the wish ticks live.
- B. Everyone places at once, with a lock per object.
- C. Only the host (the world's creator) places.
- **Recommend A.** It needs one lock and one request stream, nobody fights over the same frame, and it still lets a guest do the building. Trade-off: two players can't build in parallel. In Phase 1 there is one building, so nothing is lost. B can come later if the lanes make it worth it.

**Q3. How are wishes measured?**
- A. **Geometry from the building's door and footprint:** "near" is the distance from the door socket (or the footprint edge) to the target, "facing" is the door's outward direction within 45 degrees of the target, and "far from" is the footprint-to-footprint gap.
- B. Path-graph distance: "near the fountain" means within N metres of path from the doorstep node.
- C. Tags baked per foundation and turn (F03 facing north: near the fountain, faces the square).
- **Recommend A,** computed in Core as pure functions over the layout and the placements. It works the same for the lanes and for free decorations later, and it gives the live preview its numbers. Trade-off: edge cases at the 12 m and 45-degree limits feel arbitrary. The preview shows the number ("13 m: a little closer") so the player can see why. C is cheapest but breaks once ring 2 opens.

**Q4. Is the service live or latched?**
- A. Live: breaking a wish closes the service until it's fixed (the DC1 way: rescored from the layout every time).
- B. Latched: once all wishes are met, the service stays open forever, and the ticks just show the current state.
- C. **Split:** the first-meet beat is latched (the baker's line, a gift, Wren's list tick), and the service is checked at dive start (loaves are granted only if the wishes are met then).
- **Recommend C.** Rearranging never takes back a story moment, but the wishes still matter, and there is one simple check point. Trade-off: a player who breaks a wish just before a dive loses that dive's loaves. The dive-start prompt says so.

**Q5. How are save failures isolated, and what does quitting mid-dive do?**
- A. Per section: a bad `town` section resets the whole town to Ruined.
- B. Per entry: a bad placement is dropped and its piece goes back to stock.
- C. **Per entry, plus a conservation pass:** on load, every known piece and resident must be in exactly one place (stock, placed, banked in the haul, or "left on its floor"). Anything missing goes back to stock, and duplicates are removed.
- **Recommend C, with a quit mid-dive counted as a failed dive** (unbanked haul lost, pieces back on their floors, banked haul kept). This is the "nothing is missable" rule applied to saves. Trade-off: one more Core pass and its tests. It is small next to losing the baker to a bad byte.

**Q6. Do decorations come in M6?**
- A. **Not yet:** M6 is buildings, parts and the baker. The free 0.5 m grid, decoration path breaks and undo come with the lanes, or in M7 if there's time.
- B. A starter set: 3 decoration kinds (bench, planter, lantern) bought with chest loot, on the free grid.
- C. Full decoration placement as PHASE1 7.1 describes.
- **Recommend A.** The baker loop needs none of it, and the path-break rule is the costliest part to build and test. Trade-off: chest loot has no use in town until decorations come (it is still banked and saved). Building pick-up and re-place is the "undo" for buildings in M6.

## 4. Staged plan
| Stage | Work | Done when | Tests | Review | Sessions |
|---|---|---|---|---|---|
| **S0 Player id + weapons save** (A34; must land before M7) | Host map client id to `PlayerKey` from approval; `CombatHost` growth keyed by `PlayerKey`; `weapons` section wired (row, `Clean`, restore on host spawn, capture on save) | A player who leaves and rejoins keeps weapon growth; the world reload keeps it | `WorldSaveStoreTests` round trip + "bad weapons keeps the rest"; PlayMode rejoin keeps growth | risk-reviewer | 1 |
| **S1 Core town model** | Port FishGame `Core/Village/Placement` (clone via `add_repo`); `PieceCatalog` (piece id to building and part; baker as resident); `TownState` (building on foundation, turn, parts, resident); rules: size class, occupied, frame before part, complete-before-resident, path refusal through `TownLayout` reachability; wish kinds AtLot, NearPoint, FarFrom, Facing, NextTo, InArea; evaluator and score | All of PHASE1 7.1's building rules and the baker's three wishes pass in Core | EditMode: every rule, each wish kind at its edge, pick-up always allowed, no state soft-locks (random place/pick-up fuzz) | reviewer | 1-1.5 |
| **S2 Save sections** | `stock`, `town`, `residents`, `dive` (cleared mask, checkpoint) sections; per-entry `Clean`; conservation pass; mid-dive quit = fail | A save round trip restores everything; a corrupt entry returns its piece to stock | Round trip; "bad section keeps the rest" for each; conservation cases; golden v1 fixture unchanged | risk-reviewer | 1 |
| **S3 Hollowmere session scene** | Hollowmere in `BuildOrder`; builder emits every building's frame and parts hidden, and a runtime `TownView` shows them from `TownState`; `DiveHub` moved to the cistern-house stair; go-home and fail land in town; bank in town moves the haul to stock | Solo and host+client start in Hollowmere, dive from the stair, come home to the town | PlayMode 2-peer: both see the same town from a save; go-home lands at the stair | risk-reviewer | 1-2 |
| **S4 Placement mode** | `TownSync` (host-held state in NetworkVariables/list, builder's lock, request RPCs); input context row; ghost green/red, four turns, socket slotting, pick-up; placement camera; wish panel with live ticks | A player builds the bakery and sees the wishes tick live | PlayMode 2-peer: a client places, the host refuses a path-blocking frame, late joiner sees the town; router row test | risk-reviewer | 1-2 |
| **S5 Baker and service** | Greybox baker NPC at his door; first-meet beat (latched); loaves granted at dive start | Wishes met, the next dive starts with three loaves | EditMode service rule; PlayMode loaves granted to every player | reviewer | 1 |
| **S6 Walks** | Solo and 2-player walks: dive, rescue, bank, place, wish met; `USER_TODO` steps | Both walks pass on the PC (`sc-walk`) | Manual walks | user | 1 |

S0 can run beside S1. S2 needs S1's types. S3 can start beside S2. S4 needs S2 and S3. Total about **7-9 sessions**.

## 5. Risks
- **M5 contents isn't merged yet,** and piece ids or the per-floor piece count may change. S1's `PieceCatalog` keys on whatever lands, and gets a test that every piece M5 spawns maps to a part.
- **One scene for town and cellar (Q1 A):** look leakage, memory, and cloud capture times on the heavy town. Prove it early with one reduced capture of a floor under the town.
- **The builder output isn't deterministic** (`Hollowmere.unity` changes on every check build). S3 adds runtime-toggled objects to it, so regenerate with `-nographics` before every check.
- **The Rebuilt preset is editor-only.** Turning it into a runtime view is real builder work. Bakery frame and part art may not be ready, so greybox boxes per socket stand in.
- **Losing pieces through saves** would break "nothing is missable." The conservation pass (Q5 C) and its tests are the guard.
- **Rejoin identity:** a client whose `PlayerKey` changes (an identity kind change, a solo rebind) would orphan its weapons. S0 tests the solo rebind path.
- **Scope creep from the town art:** M6 places one building and one resident. Decorations, more residents and wish interactions are later (Q6).
