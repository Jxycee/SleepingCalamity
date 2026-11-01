# Enemy AI for a small action RPG: tokens, spacing, roles, co-op targeting, navigation

Date: 2026-11-01. Written in our own words: patterns and numbers only. Builds on `libraries.md` (enemy AI decision: data-driven state machine in Core, AI Navigation 2.0, Unity Behavior as a later option), `combat-timings.md` (DC1 wind-ups), `combat-depth.md` (why one duel at a time) and `kh-combat-camera.md` (KH hovering crowd). It does not repeat them.

Source quality is marked in the Sources list. Several of the big-name items (Doom, Arkham, God of War, Elden Ring) only had secondhand write-ups reachable from here. Where a claim rests on one of those, the text says "reported". Numbers marked "common practice" are general industry habit, not a fetched source: treat them as starting guesses to tune.

## 1. What we already have (read first)

| Piece | Where | State |
|---|---|---|
| Attack tokens | `Core/Combat/AttackPacing.cs` `AttackTokens` | Done and tested. Per hero: max attackers (tier 1: 1, tier 2: 2), minimum gap between hit windows (0.5 s / 0.35 s), exclusive reservation for the elite's signature, lapse on their own at hit-window end. Keys are opaque ints. |
| Pacing per tier | `AttackPacing`, `PacingProfile` | Cooldown and recover scales, retry delay with jitter. |
| Attack choice | `EnemyAttackChooser.Choose` | Distance and roll, elite signature on a cadence (every 3). |
| Waiting behaviour | `Enemy.ServerUpdate` | A refused enemy sets `_waitingForToken`, picks a strafe sign, holds at about `radius + 1.75 m` and circles at 45% speed, drifting out if crowded. |
| Target pick | `Enemy.PickTarget` | Nearest living hero. No threat, no stickiness. |
| Movement | `Enemy.MoveHorizontal` | Straight steering with a capsule check against Structure. No NavMesh yet. |
| Co-op scaling | `CoopScaling` | Life x1.0/1.7/2.3/2.8, +1 enemy per room per extra player, attack unchanged. |
| Authority | `Enemy` class comment | Host owns HP, AI, movement, attacks. Clients see NetworkVariables (phase, attack serial/idx/origin, rage, flags). |

So the token manager is not a new build. The gaps are: roles, spacing rings shared between enemies (today each one circles alone), threat-based targeting, navigation on generated floors, and the AI shape (a hard-coded `Phase` switch inside a 556-line MonoBehaviour that Core tests cannot reach).

## 2. Attack tokens and aggression budgets

### 2.1 What the shipped games do

| Game | Reported mechanism | Takeaway for us |
|---|---|---|
| Doom (2016) | Each attack type (melee, ranged, charged) has its own limited token pool, scaled by difficulty. A demon asks before it attacks and returns the token after. A demon may take a token from another that is better placed (for example the one in front of the player). Pain reactions escalate from aim twitches to flinches to a stagger that allows a glory kill. | Per-category pools, not one global pool. Difficulty widens pools. Stealing a token by position is a cheap way to make the nearest enemy the attacker. |
| Arkham / freeflow | No primary source found for the AI. Design is many-vs-one with up to about six enemies, where the player flows between them. | Treat as "reported only". The counter prompts are a player-side feature, not AI. |
| Melee design article (secondhand) | About one attack per 2 to 3 seconds across the group, a small near group and a larger far group that rotate, a no-go zone around the player, enemies kept on screen before they attack, no new attack while an ally is recovering from being hit. | Matches `AttackTokens` already. The near/far rotation and the on-screen rule are new. |
| God of War (2018) | Reported by a third-party plugin: tokens allocated by threat rank. I found no primary Santa Monica talk on AI tokens. | Use threat rank as the tiebreak for who gets the token. |
| Kingdom Hearts | Few enemies swing while the rest hover (`kh-combat-camera.md`). | Already adopted. |
| Devil May Cry, Sifu, Hades, Elden Ring | No primary AI source found. Elden Ring enemies are scripted with simple distance and weight rules and groups can attack together (secondhand). | Do not copy "everyone attacks" from Souls games: fairness in a 4-player hack and slash matters more. |

### 2.2 Pool shape that works (one third-party write-up, plus common practice)

Default per target: 1 melee token, 2 ranged tokens, 1 heavy token. One 0 to 1 "aggression" value scales every pool: 0.0 gives 0.5x, 0.5 gives 1.0x, 1.0 gives 1.5x. The write-up suggests deriving aggression from player count in co-op and keying pools per player.

Rules from the same source that matter for bugs:

- Claim on entering the attack state. If the claim fails, the state does not start.
- Release on every exit: finished, interrupted (stagger, knockback), claimant died, target died, AI stopped.
- Claims are idempotent per claimant (a holder that already has one reuses it).
- A periodic audit (about every 2 s) frees tokens held by dead claimants or past a timeout. A leaked token shrinks the pool for good and silently stops attacks.

Our `AttackTokens` already lapses by time (`StrikeEnd`), which covers the leak case. Keep it.

### 2.3 Where our tokens should grow

| Change | Why |
|---|---|
| Add a category: `Melee`, `Ranged`, `Heavy` | Today one cap covers all. Slime globs (ranged) should not block a rat lunge. Heavy (Warden slam) stays exclusive, as it is. |
| Pool sizes by category and tier, in data | Tier 1: melee 1, ranged 1, heavy 1. Tier 2: melee 2, ranged 2, heavy 1. Start numbers, tune in M3/M7. |
| Aggression from player count: no | Pools are per hero, so more heroes already means more attacks in total. Do not also widen each pool; use the global cap in section 6.3. |
| Token swap by position | If a waiting enemy is nearer and better angled than a holder that has not yet started its wind-up, it may take the token. Removes the "far enemy holds the token while a near one idles" look. |
| Minimum gap stays | The 0.35 to 0.5 s gap between hit windows is what makes hits readable. |
| `Audit(now, isAlive)` | For a missed `Release` on death. Time lapse already makes this safe, so low priority. |

## 3. Spacing rings, circling and slots

### 3.1 Rings

Three bands around the target (one write-up; its centimetres converted):

| Ring | Distance in that write-up | Who stands there |
|---|---|---|
| Melee | 1.5 to 2.5 m | Token holders closing in |
| Mid | 5 to 8 m | Strafing melee waiting for a token |
| Far | 12 to 20 m | Ranged and suppressors |

Our rooms are 14x14 m cells, so far is cut to room size. Ours: **Strike ring** (reach of the attack), **Hover ring** (`stop + 1.2 m`, as now, about 2.5 to 3.5 m), **Far ring** (5 to 8 m, for ranged and overflow).

### 3.2 Slots

- Slots are angles around the target, not world positions. They move with the target.
- A claiming agent takes the free slot nearest its current approach angle, not the first free slot. This stops a crowd swapping sides.
- An agent holds a ring slot or a cover spot, never both. Claiming one releases the other. This prevents jitter.
- Agents without a token keep moving: strafe, shuffle, advance a step, back off. Standing still reads as broken AI.
- Spread: separate angular slots by at least `360 / max(N, 4)` degrees so two enemies never line up from the camera. Keep part of the arc toward the camera clear so the player can see the near group (the "no-go zone" rule).

### 3.3 Circling (common practice)

Circle at 40 to 60% of run speed (ours: 45%). Flip strafe direction at random every 1.5 to 3 s, with an occasional short pause. Never strafe and back off hard in the same step. Our code picks one sign when the wait begins and never flips it; add the flip timer.

### 3.4 Separation

Ring slots alone do not stop overlap. Add a soft push between enemies (radius sum plus 0.2 m, strength proportional to overlap). With NavMeshAgent this is the agent's own avoidance (section 8). With the current mover it is a small loop over the room's enemy list on the host.

## 4. Telegraphs and readability

`combat-timings.md` and `combat-depth.md` already set wind-up floors (0.45 s small, 0.9 s heavy) and recovery (0.5 s). AI-side rules to add:

| Rule | Value or note |
|---|---|
| Tell comes first | The enemy's first visible action is the tell. |
| Tell scales with damage | Bigger hit, longer and louder tell (pose, flash, sound). |
| Tell must be on camera | An enemy outside the camera view may not start a token attack. Move it into view first, or let it wait. |
| No overlapping tells on one hero | Minimum gap on hit windows covers this. Also forbid a new tell while another enemy's hit window is open on that hero. |
| Variable choice, not variable timing | Delayed swings that vary per use frustrate players (a reported Elden Ring complaint). Keep each attack's timing fixed; vary which attack and when it starts. |
| Recovery is the reward | 0.5 s or longer, already in `AttackCatalog`. |
| Chase | Enemies follow a fleeing hero up to a leash limit, then return. |

## 5. Group roles

| Role | Job | Token category | Spacing | Our enemy |
|---|---|---|---|---|
| Rusher | Close in, take turns | Melee | Strike ring when holding, hover when not | Rat-king |
| Swarmer | Many, weak, hop at the hero | Melee (light) | Hover ring, noisy jitter | Slime |
| Ranged / lobber | Keep distance, pressure position | Ranged | Far ring, strafes, retreats if hero closes under 4 m | Slime glob |
| Shield / blocker | Front guard, forces flanking | Melee | Hover ring, turns to face the hero, slow | Broom |
| Heavy / elite | Exclusive, big tell | Heavy | Follows hero, long recovery | Dough Warden |
| Support (later) | Buff or rally | none | Far ring behind the group | Not in Phase 1 |

Role is per-enemy data, not a subclass. It sets the preferred ring, the token category and the utility weights.

Group sizing: many weak, fewer mid, fewest elite. Never mix more than two roles on floor 1.

## 6. Target selection and threat in co-op

Today: nearest living hero. With 2 to 4 players that piles every enemy on one player, and a player who walks away drags the whole room.

### 6.1 Goals

- Spread pressure across players without it feeling random.
- Keep one target long enough that its attacks read (stickiness).
- Be explainable: the player can see why an enemy came for them.
- Never target a downed hero while an up hero is in the room.

### 6.2 Threat score (utility, in Core)

For each enemy and each up hero:

`score = W_dist * (1 - dist / aggroRange) + W_hit * recentDamageShare + W_stick * isCurrentTarget + W_load * (1 - attackersOnHero / maxAttackers) + W_role * rolePreference`

| Term | Start weight | Note |
|---|---|---|
| Distance | 1.0 | Nearer is higher. |
| Damage dealt to this enemy (decays over 6 s) | 0.8 | Hitting an enemy pulls it. |
| Stickiness | 0.6 | Switch only when another beats the current by this margin. Re-evaluate at most every 0.75 s. |
| Load balance | 0.7 | Heroes with fewer holders and fewer waiting enemies score higher. This is the fairness term. |
| Role | 0.3 | Ranged prefer the farthest hero; shield prefers the flanker. |

Hard rule: no target switch during wind-up or attack.

### 6.3 Difficulty by player count

| Lever | 1 | 2 | 3 | 4 | Source |
|---|---|---|---|---|---|
| Enemy life | 1.0 | 1.7 | 2.3 | 2.8 | PHASE1 6.5 (existing) |
| Extra enemies per room | 0 | +1 | +2 | +3 | existing |
| Tokens per hero | per tier | same | same | same | total attacks already grow with heroes |
| Global cap on simultaneous strikes | n/a | 3 | 4 | 5 | new: stops 4 heroes x 2 tokens = 8 strikes on screen |
| Attack damage | unchanged | | | | existing rule |

The global cap is the only new lever. It keeps the screen readable and the network cost low. Do not buff enemy accuracy or speed with player count: that reads as cheating, and the team rule is life and count only.

### 6.4 Edge cases

- Hero revived: enemies re-score within 0.75 s, no snap.
- Hero out of the room: consider only heroes in the same room cell plus a short leash.
- All enemies on one hero: load balance pushes extras to hover rings, then to other heroes.
- Everyone down ends the dive; no targeting needed after that.

## 7. FSM, behaviour tree or utility: what fits us

| Option | Strengths | Weakness | Fit |
|---|---|---|---|
| Hand FSM | Easy to test, deterministic, syncs as a phase byte | Transitions grow as states grow | Good for 4 enemy kinds |
| Hierarchical FSM | Doom (2016) reportedly uses grouped states (move to cover, ranged attack, melee attack), designers combine, programmers build | More code up front | Best match to what we are growing |
| Behaviour tree | Designer graphs, reuse | Harder tests, runtime cost, tooling | Not now. Unity Behavior stays the later option for bosses (`libraries.md`). |
| Utility scoring | Smooth choices, easy to add options, data-tunable | Can look random without hysteresis | Use for decisions, not for execution |
| HTN / GOAP | Planning | Overkill | No |

**Pick: a Core hierarchical FSM for execution, utility scoring for the choices inside it** (which target, which attack, which ring, when to reposition). The FSM keeps the attack run (tell, strike, recover) rigid and testable. Utility picks what to start. Put hysteresis (a commit time or a margin) on every choice to avoid flicker.

From the utility literature (Mike Lewis, Game AI Pro): normalise every input to 0 to 1, pick a response curve per input, and multiply (or take the min) when a single "no" should veto. A short list of weighted considerations is enough for us; the full "infinite axis" system is not needed.

## 8. Navigation on generated floors

Our movement is a custom mover with a capsule check. It works in open rooms and fails on door frames, L-shaped halls and props. Plan:

| Item | Detail |
|---|---|
| Package | AI Navigation 2.0.16 is already in the manifest. |
| Baking | One `NavMeshSurface` per floor, built at runtime once the layout is final (`BuildNavMesh()`). Collect Objects: Current Object Hierarchy under the floor root. Use Geometry: Physics Colliders, so agents reach the wall edge (the manual notes this setting). |
| Tile size | The manual says use a smaller tile size when baking at runtime, to keep peak memory low. Do it. |
| Which peers | Host only. Clients interpolate and need no NavMesh. |
| Timing | Bake behind the loading cover. Measure for a full floor. If slow, bake per room as rooms appear. The manual page did not list async or partial-update calls, so check the package API when building. |
| Agents | Use `NavMeshAgent` for paths only (`updatePosition = false`, `updateRotation = false`). Read `desiredVelocity` and move through our own `MoveHorizontal` so knockback, hitstop and the capsule check stay ours. |
| Avoidance | Built-in avoidance gives separation. `avoidancePriority` by role: heavy 20, normal 50, swarm 70 (lower wins). |
| Rings on the mesh | Sample each ring point with `NavMesh.SamplePosition` (within 1.5 m) so a slot never lands in a wall. Fall back to the last good point. |
| Doors | Doors that open mid-room: `NavMeshObstacle` with carve, or a `NavMeshLink`. |
| Cost | Do not path every frame. Recompute every 0.2 to 0.4 s per enemy, staggered by id, or when the target moves over 1 m. |
| Fallback | If the bake fails or there is no path: use the existing straight mover and log once. |

## 9. Networking AI on the host

The current design is right: the host runs everything and writes a few NetworkVariables. Keep it.

| What | Rule |
|---|---|
| Decisions | Host only. Never replicate token state, threat scores or ring slots. |
| What clients need | Position and facing (interpolated), a phase byte, attack serial, attack index and origin, HP, flags. All present already. |
| Attack start | One serial increment starts the client animation from the attack id. Clients run tell, hit window and sound locally. No per-frame attack replication. |
| Interpolation | NetworkTransform with interpolation on enemies. Common practice: render remote objects about 100 ms behind, which hides jitter at 150 ms RTT. Do not extrapolate through walls. |
| Send rate (common practice) | 20 to 30 Hz position. State on change. Enemies far from every player at 5 Hz. Stop sending dead ones. |
| Hit timing | PHASE1 6.4: victims judge incoming hits against their own dodge state. So the telegraph is what is authoritative: the host sends the tell start, the victim decides hit or miss at the window against its own i-frames. The host re-checks game rules only. |
| Late join | Read the NetworkVariables. Do not replay a wind-up already under way. |
| Host leaves | The dive ends. No AI migration. |
| Determinism | Not needed. |

## 10. Recommended design for Sleeping Calamity

### 10.1 Shape

```
Core/Combat/Ai/
  AiBrain          FSM driver, one per enemy, no UnityEngine
  AiState          Idle, Approach, Hover, Tell, Strike, Recover, Stagger, Reposition, Dead
  AiContext        Snapshot the host fills each tick (plain floats: positions x,z; heroes; dt)
  AiAction         Output: desired move, face, start attack id, release token
  ThreatTable      Per-enemy scores per hero (6.2)
  UtilityScorer    Considerations, response curves, hysteresis
  RingPlanner      Ring and slot targets from role and crowd (section 3)
  AttackTokens     Existing; add category
Gameplay/Combat/
  Enemy            Reads AiAction and applies it (move, anim, serial)
  EnemyCrowd       Per-room registry of enemies and heroes; owns tokens and planner
```

Core uses plain floats, not `Vector3`, so EditMode tests run with no engine (project rule).

### 10.2 Per-enemy data (start in `EnemyCatalog`, move to ScriptableObjects only if the Inspector is needed)

| Field | Example (rat-king) | Note |
|---|---|---|
| role | Rusher | picks token category and ring |
| tokenCategory | Melee | |
| preferredRing | Hover, 3.0 m | |
| circleSpeedScale | 0.45 | |
| strafeFlipSeconds | 1.5 to 3.0 | |
| aggroRange, leashRange | 12 m, 20 m | |
| attackOptions | (attack id, base weight, min/max range, cooldown) | replaces the hard-coded chooser order |
| utilityWeights | dist, hit, stick, load, role | defaults from 6.2 |
| retargetInterval | 0.75 s | |
| avoidancePriority | 50 | |
| onScreenRequired | true | section 4 |

### 10.3 Decision loop (decisions at 10 Hz staggered, movement every frame)

1. Refresh the threat table. Keep or switch target (hysteresis).
2. In `Approach` or `Hover`: score attack options (range fit x weight x cooldown ready x not recently used). Take the best above a threshold.
3. `TryAcquire(category)`. Success: `Tell`. Failure: stay in `Hover`, ask again after the retry delay with jitter (existing).
4. In `Hover`: `RingPlanner` gives a slot angle and radius. Move there at circle speed. Flip strafe on the timer.
5. `Tell`, `Strike`, `Recover` follow the attack data frames from `AttackCatalog`. No re-scoring inside them.
6. Poise broken while not armoured: `Stagger`, release the token, then `Approach` with a short steady window (exists).
7. Target dies during `Hover`: re-score. During `Tell` or `Strike`: keep.

### 10.4 How it plugs into `Enemy` and `CombatHost`

| Existing | Change |
|---|---|
| `Enemy.ServerUpdate` phase switch | Build `AiContext`, call `AiBrain.Tick`, apply `AiAction`. Keep `ServerAdvanceAttack`, knock and stagger as they are (already tested). |
| `Enemy.PickTarget` | Call `ThreatTable`. |
| static `s_tokens` | Move into a per-room `EnemyCrowd` owned by `CombatHost`, so rooms do not share one table. |
| `Enemy.MoveHorizontal` | Stays as the mover. Gets a direction from the planner or from NavMesh `desiredVelocity`. |
| `NetPhase`, `NetAttack*` | No change. |
| `CoopScaling` | Add `GlobalStrikeCap(players)`. |

### 10.5 Test plan

EditMode, Core only:

| Test | Checks |
|---|---|
| Token categories | A full melee pool does not block ranged. Heavy still reserves exclusively. |
| Token swap | A nearer enemy takes an unstarted holder's token, never from a started wind-up. |
| Token leak | A holder that dies without `Release` is gone after `StrikeEnd`; audit frees a stuck one. |
| Threat spread | 3 enemies, 2 heroes at equal distance: split 2/1, never 3/0. |
| Stickiness | Target does not flip inside the retarget interval or without the margin. |
| No switch mid-attack | Locked during `Tell` and `Strike`. |
| Downed hero | Never chosen while another is up. |
| Ring planner | Slot separation holds; front arc stays half clear; deterministic for a seed. |
| Strafe flip | Flips within the configured range for a seeded random. |
| Global strike cap | With 4 heroes, simultaneous strikes never exceed the cap. |
| Utility hysteresis | A near tie does not alternate between two attacks over 100 ticks. |
| Property test | Random crowds: every tick, each hero has at most `max` holders and no two hit windows closer than the gap. |

PlayMode (one or two): a room with 4 enemies and 2 heroes. No overlapping strikes on one hero, all strikers on screen, nobody stuck after 30 s. Networked: host plus client smoke at 150 ms RTT, client sees attack serials in order.

### 10.6 Staged build

| Stage | Work | Done when |
|---|---|---|
| 1 | Move chooser, tokens and pacing under `Core/Combat/Ai/` behind a thin `AiBrain` that reproduces today's behaviour 1:1. Tests carried over. | Existing combat tests green, no behaviour change. |
| 2 | Token categories and per-room `EnemyCrowd`. | Slime ranged and rat melee attack together without blocking each other. |
| 3 | `ThreatTable` with load balance and stickiness. Global strike cap. | A 2-player test splits enemies. |
| 4 | `RingPlanner` with shared slots, strafe flip, separation. | No stacking with 6 enemies in a room. |
| 5 | NavMeshSurface bake on floor load; agent for paths only; ring samples on the mesh. | Enemies path around a door frame on a generated floor. |
| 6 | Roles as data (shield turns, ranged retreats), on-screen rule, `Audit`. | Broom and slime behave differently from the rat in a mixed room. |
| 7 | Tune at 150 ms RTT and 2% loss; walk 1, 2 and 4 player rooms. | Walk notes recorded. |

Stage 1 first: it moves code without changing behaviour. Stages 1 to 4 give most of the gain; stop there if the walks look good.

### 10.7 Risks

- NavMesh runtime bake cost and API surface: spike early in stage 5.
- Tokens per hero plus the global cap can starve a lone distant enemy: retry jitter and "keep moving" cover it, but test it.
- Odd room shapes: the ring sampler needs its fallback.

## Sources

Fetched and read:
- Unity AI Navigation, NavMesh Surface component (collect objects, geometry source, runtime tile size note): https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/NavMeshSurface.html
- Doom (2016) AI summary, Game Developer (secondhand): https://www.gamedeveloper.com/design/cyber-demons-the-ai-of-doom-2016-
- Attack token system write-up, pool sizes and ring bands (third party): https://www.strayspark.studio/blog/attack-token-system-ue5-group-combat-that-feels-fair
- Melee enemy design rules, Game Developer: https://gamedeveloper.com/design/enemy-design-and-enemy-ai-for-melee-combat-systems

Found by search, snippets only (third-party devlogs and listings):
- https://lghts.itch.io/iron-reclamation/devlog/1092177/making-combat-feel-good-fair-readable-and-responsive-ai
- https://serfofcinder.itch.io/archetype-portal-to-hell/devlog/738495/enemy-ai-tech-talk

Pointers found but not read in full (check the primary material before relying on specifics):
- Game AI Pro, free chapters (Mike Lewis on utility considerations; Travis McIntosh on The Last of Us human enemies): https://gameaipro.com/
- The NPC AI of The Last of Us, a case study (arXiv; the PDF text did not extract): https://arxiv.org/pdf/2207.00682
- Dave Mark and Mike Lewis, "Building a Better Centaur" (GDC 2015, infinite axis utility): GDC Vault.
- Santa Monica Studio, "Preparing AI Systems for God" (members only): https://gdcvault.com/play/1029286/Preparing-AI-Systems-for-God
- Elden Ring enemy scripting analysis (teaser only): https://feedbagel.com/post/the-low-tech-ai-of-elden-ring
- AI and Games (Tommy Thompson) on Doom and Rage hierarchical FSMs: https://www.aiandgames.com/

Not found: any primary source on Arkham, Sifu, Hades, DMC or Kingdom Hearts enemy AI internals. Our own KH notes are in `kh-combat-camera.md`.
