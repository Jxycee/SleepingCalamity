# Third-party libraries for Sleeping Calamity (research, 2026-10-30)

Scope: free, permissive libraries for Phase 1 (M1-M10, see `docs/PHASE1.md`). Already in `Packages/manifest.json`: NGO 2.13.3, Multiplayer Play Mode 3.0.0, Multiplayer Tools, Cinemachine 6.6.0 (CM3), Input System, URP 17.6, AI Navigation 2.0.16, ProBuilder, Newtonsoft 3.2.2, Timeline, Test Framework 1.8.0.

Method note: web search plus GitHub README fetches. Release dates were mostly NOT visible on the fetched pages. Anything marked (verify) must be checked in the repo's LICENSE/Releases before adoption. Nothing was installed.

## Summary (top recommendations)
1. **Jolt: skip.** The only Unity binding (seep/JoltPhysicsUnity, MIT) is work-in-progress, Burst/unmanaged-struct oriented and unpublished on any registry. PhysX is enough for a melee ARPG with a CharacterController and few rigid bodies.
2. **Adopt Unity Animation Rigging** (official) for hand IK on held weapons and look-at; it replaces the paid Final IK for Phase 1.
3. **Adopt PrimeTween** (free tier; read its LICENSE first) for UI, build-zone and prop feel tweens. DOTween is the fallback.
4. **Hitstop, camera shake and feel: build in-house** on Cinemachine Impulse (already in). Feel/MMFeedbacks is paid, so skip it.
5. **Enemy AI: a data-driven state machine in Core**, not a behaviour-tree asset. Unity Behavior (1.0.x, official) is the "consider later" option for the elite and bosses.
6. **Floor generation: our own Core generator** (already specified). Edgar free is 2D-only and cannot be used; WFC is overkill for a 14x14 room-pool grid.
7. **Adopt CristianQiu's Unity-URP-Volumetric-Light** (MIT, Unity 2022.3 to 6000.6, Render Graph) for the Cellars' light shafts, with APV (built into URP) for bounce light.
8. **Networking helpers: stay on Multiplayer Play Mode**; read Boss Room (Unity Companion License, reference only, do not copy files) for approval/Relay patterns. No NGO lag-compensation library exists; our design (owner-detected hits) does not need one.
9. **Serialisation: stay on Newtonsoft** for saves and NGO's INetworkSerializable for the wire. MessagePack/MemoryPack (both MIT) are "consider later" if save size or speed hurts.
10. **Characters: MPFB2/MakeHuman output is CC0**, so Tobin's base body is clear (the add-on itself is GPLv3, used as a tool only, never shipped).

## Physics
| Option | Gives us | Licence | Unity 6 / URP | Maintenance | NGO / determinism | Risk | Verdict |
|---|---|---|---|---|---|---|---|
| Built-in PhysX (in engine) | CharacterController, casts/overlaps for hitboxes, triggers | Unity engine | Native | Unity | Owner moves its CC, NetworkTransform replicates; no physics sim needs to match across machines | None | **Adopt (default)** |
| [seep/JoltPhysicsUnity](https://github.com/seep/JoltPhysicsUnity) | Jolt 5.3.0 world in C# for Jobs/Burst | MIT | Uses Unity.Mathematics/Collections; Unity range not stated | 222 commits; no releases; one index shows last push Feb 2024 (verify) | Jolt can be deterministic with a build flag, but our design does not need it | High: WIP, not on a registry, mostly unmanaged structs, no CharacterController/NGO glue, a second physics world to sync with scene colliders | **Skip** |
| [OpenUPM com.seep.jolt](https://openupm.com/packages/com.seep.jolt/) | Same package via scoped registry | MIT | same | same | same | same | Skip |
| Other Jolt .NET ([JoltPhysicsSharp](https://nubrowse.com/packages/JoltPhysicsSharp) 2.22.0, July 2026) | General .NET, not Unity | MIT | Not a Unity package | Active | n/a | Would need our own native plugin build per platform | Skip |
| Unity Physics (DOTS) | ECS physics | Unity package licence | Needs Entities; our stack is GameObjects + NGO | Unity | Determinism per-architecture only; no NGO bridge | Wrong stack | Skip |
| Box2D-style (Unity 2D) | 2D | Engine | n/a | n/a | n/a | Game is 3D | Skip |

Honest take: our hit detection (frame-window hitboxes via overlap/cast), enemy movement (NavMeshAgent/CC) and Gate/chest triggers are what PhysX does well. Jolt's wins (many bodies, determinism, rollback) are not in Phase 1. Revisit only if a later phase wants big rigid-body piles or physics-based building collapse. Sources: Jolt repo above, [Unity determinism notes](https://discussions.unity.com/t/state-of-determinism-in-unity/867770).

## Animation
| Option | Gives us | Licence | Unity 6 / URP | Maintenance | NGO | Risk | Verdict |
|---|---|---|---|---|---|---|---|
| [Animation Rigging](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.2/manual/index) (`com.unity.animation.rigging`) | Two-bone IK, chain IK, multi-aim, override transform; runtime rigs | Unity package licence (free) | Official | Unity | Pure client-side cosmetics; run on every client from replicated state | Chain IK is limited; not full-body | **Adopt** (hands on weapon, look-at, M3/M10) |
| [Animancer](https://kybernetik.com.au/animancer/docs/download) Lite | Code-driven playback, state machine utilities | Lite free but most Pro features only work in the Editor, not in builds; Pro is $90 | Yes (v8.x) | Active | Fine | Lite is not a shipping-grade subset (source/features gated) | **Skip** (Mecanim + Playables are enough; our attack windows are data) |
| Final IK / Puppet Master (Root Motion) | Full-body IK | Paid | Yes | Active | n/a | Paid | Skip (no free equivalent; need not arise in Phase 1) |
| Fast IK, FABRIK/CCD GitHub snippets | Chain IK | Mixed/unvetted | Unknown | Unknown | n/a | Licence unclear | Skip |
| Motion matching (Kinematica-like, open ones) | Data-driven locomotion | None vetted; Kinematica is discontinued | n/a | n/a | n/a | High, needs mocap library | **Skip** |

## Combat and AI
| Option | Gives us | Licence | Unity 6 | Maintenance | NGO | Risk | Verdict |
|---|---|---|---|---|---|---|---|
| Hand-written FSM in Core (per PHASE1 6.1 data windows) | Telegraph/attack/recover states, rage, tier scaling; EditMode-testable | Ours | n/a | Ours | Host-only AI, trivially deterministic given tick | Slightly more code | **Adopt (build)** |
| [Unity Behavior](https://docs.unity3d.com/6/Documentation/Manual/com.unity.behavior.html) (`com.unity.behavior`) | Graph BT editor, debugging in Play mode, subgraphs | Unity package licence | Official, 1.0.13-1.0.15 on 6000.0 docs | Unity | Run on host only | Graphs live in assets (merge pain, not unit-testable in Core) | **Consider later** (Dough Warden / bosses) |
| AI Navigation 2.0.16 | NavMesh agents | In | In | Unity | Host agents | None | In (use) |
| [A* Pathfinding Project](https://forum.arongranberg.com/t/can-the-free-version-be-used-for-commercial-projects/16425) free | Better local avoidance, grids | Asset Store free licence; author says commercial use OK (informal, verify bundled licence) | Yes | Active | Host | Closed source in free tier | **Skip** (NavMesh suffices on 14x14 cell floors) |
| Fluid HTN | HTN planner | MIT (not confirmed: repo fetch failed) | Not confirmed | Unknown | n/a | Planner overkill for 3 enemy types | Skip |
| NodeCanvas | BT | Paid | n/a | n/a | n/a | Paid | Skip |
| Utility AI (hand-rolled scoring) | Rat-king scatter/regroup choice | Ours | n/a | n/a | n/a | Small | Consider inside the FSM |

## Procedural generation
| Option | Gives us | Licence | Fit | Verdict |
|---|---|---|---|---|
| Our Core generator (PHASE1 7.2: seed + floor id, room pool, 4-bit masks) | Exact spec, EditMode repetition metric over 1,000 seeds | Ours | Perfect, pure C# | **Adopt (build)** |
| [Edgar for Unity](https://github.com/OndrejNepozitek/Edgar-Unity) free | Graph-based room layouts | MIT repo, but "cannot be redistributed or resold" terms; PRO paid | **2D only**, seeds not documented | **Skip** |
| [mxgmn WaveFunctionCollapse](https://github.com/mxgmn/WaveFunctionCollapse) and Unity ports | Tiled/overlapping WFC | Repo licence not confirmed here (verify); Tessera is paid | Great for facades/terrain texture, wrong for room graph; determinism per seed is feasible | Consider later (town decor, not floors) |
| DunGen | Room-graph dungeon | Paid | n/a | Skip |

## Networking helpers
| Option | Gives us | Licence | Status | Verdict |
|---|---|---|---|---|
| Multiplayer Play Mode 3.0.0 (in) | Up to 4 virtual players in one editor | Unity | In; Unity docs say it cannot be combined with ParrelSync | **Use (already in)** |
| [ParrelSync](https://github.com/VeriorPies/ParrelSync) | Editor clones | MIT | Tested versions are 2022.3/2021.3/2020.3; Unity 6 not confirmed | **Skip** (conflicts with MPPM) |
| [Boss Room / coop samples](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop) | Connection approval, Relay session patterns, server-driven design | Unity Companion License (not OSS; copying files risky) | Targets 6000.0 LTS, NGO 2.x | **Read as reference** (M1 approval/Relay) |
| NGO Client Authority samples (`ClientNetworkTransform`) | Owner-authoritative transforms | Superseded: NGO 2.x NetworkTransform has AuthorityMode Owner | Built in | Use the built-in (already planned, PHASE1 6.4) |
| Lag-compensation / rollback libraries for NGO | none found | n/a | Only the old DOTS Netcode had it | **None needed**: attacker-side hit detection + host re-check |
| Multiplayer Tools network simulator (in) | 150 ms / 2% loss test (M3 proof) | Unity | In | Use |

## Tweening and feel
| Option | Gives us | Licence | Verdict |
|---|---|---|---|
| [PrimeTween](https://github.com/KyryloKuzyk/PrimeTween) | Zero-alloc tweens, sequences, shakes, delays | Free for commercial/derivative projects; licence text not shown on the page and one index lists "other" rather than MIT (verify LICENSE); PRO adds inspector animation | **Adopt** (UI, build-zone ghost, pickups) |
| [DOTween](https://dotween.demigiant.com/license.php) | Same, widest ecosystem | Free tier under an Artistic-style licence (commercial use yes; modified source not redistributable); Pro per-person paid | Consider (fallback if PrimeTween licence is unsuitable) |
| Feel / MMFeedbacks | Full feel stack | Paid (Asset Store EULA) | **Skip** |
| Cinemachine Impulse (in CM3) | Camera shake from hit events | In | **Use** |
| Hitstop | PHASE1 6.1: local, cosmetic, capped | Ours (animator speed + camera kick) | **Build** (about 40 lines) |

## Data and serialisation
| Option | Licence | Notes | Verdict |
|---|---|---|---|
| Newtonsoft 3.2.2 (in) | MIT | Saves already versioned this way (FishGame port) | **Keep** |
| NGO `INetworkSerializable` / FastBufferWriter (in) | Unity | Wire format for intents | **Keep** |
| [MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp) | MIT (LICENSE not shown, widely known MIT; verify) | Unity: min 2022.3.12, source generator for IL2CPP, NuGetForUnity install; v4 preview | Consider later (large saves) |
| [MemoryPack](https://github.com/Cysharp/MemoryPack) | MIT | Fast binary, min Unity 2022.3.12, NuGetForUnity, some Unity types need wrappers | Consider later |
| Odin Inspector | Paid | n/a | **Skip** |

## Rendering and look
| Option | Gives us | Licence | Unity 6 / URP | Verdict |
|---|---|---|---|---|
| [CristianQiu Unity-URP-Volumetric-Light](https://github.com/CristianQiu/Unity-URP-Volumetric-Light) | Volumetric fog/light shafts as a URP feature, Render Graph, volume-driven | MIT | 2022.3 to 6000.6; no WebGL, no multipass VR, transparent blending imperfect, wants Forward+/Deferred+ | **Adopt** (M9 dungeon corner; keep behind a quality toggle) |
| [sinnwrig urp-fog-volumes](https://github.com/sinnwrig/urp-fog-volumes) | Local fog volumes | Not confirmed | Tested only 2022.3 | Skip |
| Adaptive Probe Volumes (URP, built in) | Baked GI for the town and Cellars, sky occlusion, scenario blending | Unity | Official | **Adopt** (M9; also enables fully baked lights with the fog above) |
| SSGI for URP | No official SSGI found; community Render Graph ports exist, unvetted | Varies | Risky | Skip for now (APV + baked first) |
| Decals | URP DBuffer / Screen Space decal renderer features (built in) | Unity | Official | Use built-in |
| Stylised grass / foliage | Ours (sc-foliage card crowns, instanced grass) | Ours | n/a | Build; free packs not vetted |

## Audio
| Option | Terms | Verdict |
|---|---|---|
| Unity audio + Mixer (in) | Free | **Use now** with CC0 Kenney placeholders (PHASE1 6.6) |
| [FMOD Studio](https://www.fmod.com/licensing) | Free Indie licence, but thresholds conflict across sources (revenue or budget caps; confirm with FMOD EULA); project must be registered | Consider later (post-slice) |
| [Wwise](https://blog.audiokinetic.com/free-wwise-for-indie-developers/) | Free Indie tier reported for budgets under 250k USD, registration and periodic re-checks; unlicensed builds capped at 200 assets | Consider later; heavier workflow |

## Tooling and testing
| Option | Licence | Notes | Verdict |
|---|---|---|---|
| [UniTask](https://github.com/Cysharp/UniTask) | MIT | Unity 6's built-in `Awaitable` covers common cases; UniTask still stronger on cancellation/PlayerLoop control; maintenance not verified for 2026 | Consider later (use Awaitable first) |
| [R3](https://github.com/Cysharp/R3) | MIT | UniRx successor; Unity support not confirmed by the fetch | Skip (events are in Core, not reactive) |
| NaughtyAttributes | MIT (not re-verified) | Inspector sugar only | Skip |
| Unity Test Framework 1.8.0 (in) | Unity | EditMode for Core, PlayMode for co-op; wrapper exists | **Use** |
| Logging | Unity `Debug` + our Diagnostics (FishGame port) | n/a | Use ours |

## Characters
| Option | Licence | Notes | Verdict |
|---|---|---|---|
| MPFB2 / MakeHuman | Add-on code GPLv3; bundled assets and generated content CC0 per [CG Channel](https://www.cgchannel.com/2025/03/check-out-open-source-blender-character-generation-plugin-mpfb-2) and the [MakeHuman licence FAQ](https://static.makehumancommunity.org/oldsite/faq/what_changed_regarding_the_license_in_2020.html); older exception text required an unmodified official exporter | Use as a Blender tool only; never ship the add-on. Record the version and licence file in the asset source record (`sc-asset-adopt`) | **Adopt** (Tobin base mesh, M10) |
| Free retargeting | Unity Humanoid avatar retarget (built in) | Mixamo-style retarget is not a licence issue for our own clips but Mixamo terms differ (not vetted) | Use Unity Humanoid |
| UMA | Open source (MIT, not re-verified) | Modular character system is heavier than 4 fixed heroes | Skip |

## Adopt now (max 6)
1. **Unity Animation Rigging**, M3 (weapon grips, look-at) and M10 (Tobin through the character pipeline).
2. **PrimeTween** (after LICENSE check), M6 (build-zone placement polish, wish ticks) and UI throughout.
3. **CristianQiu volumetric light**, M9 (Cellars dungeon corner approval).
4. **Adaptive Probe Volumes + URP decals**, M9 (beauty corner and dungeon corner).
5. **Multiplayer Play Mode (already in) + Boss Room read-through**, M1 (approval, Relay, local session) and M3 (150 ms proof).
6. **MPFB2 base body**, M10 (Tobin), with the CC0 evidence stored in the source record.

## Consider later
- Unity Behavior for the Dough Warden or later bosses (graphs for multi-phase telegraphs).
- UniTask if `Awaitable` cancellation/timing gets awkward in services and scene flow.
- MessagePack or MemoryPack if saves or wire payloads outgrow Newtonsoft/INetworkSerializable.
- DOTween if PrimeTween's licence proves unsuitable.
- WFC for town decor scatter or facade variation (not floor layout).
- FMOD or Wwise after the vertical slice if mixing needs exceed Unity's mixer.
- An SSGI Render Graph pass, only after APV and baked light are judged insufficient.
- Jolt, only if a later phase needs many rigid bodies or cross-machine physics determinism.

## Skip and why
- **Jolt (Unity bindings):** WIP, unpublished, Burst/unmanaged API, no CharacterController/NGO integration; PhysX covers our needs.
- **Unity Physics (DOTS):** wrong stack (ECS) and no NGO bridge.
- **Animancer Lite:** key features gated to the Editor in builds; Pro is paid.
- **Final IK, Puppet Master, Feel/MMFeedbacks, NodeCanvas, DunGen, Odin:** paid.
- **Edgar for Unity (free):** 2D only, redistribution limited.
- **ParrelSync:** cannot be used together with Multiplayer Play Mode; Unity 6 untested.
- **A* Pathfinding Project free:** closed source and unnecessary with NavMesh on small floors.
- **R3, NaughtyAttributes, UMA:** no problem they solve here.
- **Motion matching:** no vetted free implementation; needs a mocap library.

## Licence flags (GPL/LGPL/restrictive)
- MPFB2 add-on is GPLv3 (tool only, not shipped; generated content is CC0). Not usable if bundled in a build.
- Boss Room: Unity Companion License, not OSI. Reference only.
- DOTween: custom Artistic-style licence; free tier fine, no redistributing modified source.
- Edgar free: terms forbid redistribution or resale; irrelevant since skipped.
- No LGPL libraries were found in the shortlist.

## Sources
- Jolt: https://github.com/seep/JoltPhysicsUnity , https://openupm.com/packages/com.seep.jolt/
- Animancer: https://kybernetik.com.au/animancer/docs/download
- Animation Rigging: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.2/manual/index
- Unity Behavior: https://docs.unity3d.com/6/Documentation/Manual/com.unity.behavior.html
- Edgar: https://github.com/OndrejNepozitek/Edgar-Unity
- Volumetric light: https://github.com/CristianQiu/Unity-URP-Volumetric-Light
- PrimeTween: https://github.com/KyryloKuzyk/PrimeTween ; DOTween: https://dotween.demigiant.com/license.php
- UniTask: https://github.com/Cysharp/UniTask ; R3: https://github.com/Cysharp/R3 ; MemoryPack: https://github.com/Cysharp/MemoryPack ; MessagePack: https://github.com/MessagePack-CSharp/MessagePack-CSharp
- Boss Room: https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop ; ParrelSync: https://github.com/VeriorPies/ParrelSync
- FMOD: https://www.fmod.com/licensing ; Wwise: https://blog.audiokinetic.com/free-wwise-for-indie-developers/
- MPFB2: https://www.cgchannel.com/2025/03/check-out-open-source-blender-character-generation-plugin-mpfb-2
- A*: https://forum.arongranberg.com/t/can-the-free-version-be-used-for-commercial-projects/16425
