# Audio sources

Placeholder combat sounds (docs/PHASE1.md 6.6, P42).

## M3 combat cues: generated, no third-party audio (2026-10-30)

All fifteen combat cues (`Combat_Hit`, `Combat_HitHeavy`, `Combat_HitCrit`, `Combat_Whoosh`, `Combat_Block`, `Combat_Parry`, `Combat_Slam`, `Combat_Hurt`,
`Combat_Pop`, `Combat_Heal`, `Combat_Spell`, `Combat_Shot`, `Combat_Dodge`, `Combat_Telegraph`, `Combat_Die`) are procedural recipes in
`Assets/_Project/Core/Audio/CombatSfx.cs`, generated at boot by the same `SfxGenerator` as the M1 tones. They are ours: no licence to record, nothing to
attribute, nothing in LFS.

**Not done: a Kenney (CC0) pack.** The brief allowed one from kenney.nl. Downloading a file needs the user's own go-ahead, and the session that built M3
did not have it, so nothing was fetched. If the user wants a pack after hearing the procedural cues:

1. Download the pack from https://kenney.nl/assets (the "Impact Sounds" and "RPG Audio" packs are CC0) into an empty folder outside the repo.
2. Copy only the chosen `.ogg` files into `Assets/_Project/Content/Audio/Combat/` (Git LFS takes `*.ogg`).
3. Add one row per file below: file, pack, URL, licence (CC0 1.0), date.
4. Point the cue table at the clips (`AudioBus.ApplyCueTable` overrides a recipe by id), keeping the ids above, so no gameplay code changes.

| File | Pack | Source URL | Licence | Date |
|---|---|---|---|---|
| (none yet) | | | | |
