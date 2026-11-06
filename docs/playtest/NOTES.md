# Playtest notes

One file per playtest session is added below (newest on top). Copy the template, fill it while playing or right after. Numbers come from the overlay and the end-of-dive summary; feel notes are plain words, no need to name a knob (Claude maps each note to a key in `docs/design/m7-tuning-notes.md`).

## Playtest overlay

- Toggle: **F4** on a keyboard, or **Back/Select + right-stick click** on a gamepad. Off by default; works in non-dev builds. Read-only: it shows what the game already syncs and writes nothing to the game.
- Shows: dive time, floor, time per floor, damage taken, heals used, loaves eaten and left, stir, bank choices, room clear times. In co-op it lists every player and a party total line.
- When a dive ends it saves `dive-<date>-<time>.txt` and `.json` in `PlaytestSummaries` under the game's persistent data folder (Windows: `%USERPROFILE%\AppData\LocalLow\<company>\<product>\PlaytestSummaries`). Paste the `.txt` into the per-floor numbers of the template.

## Template

```
### YYYY-MM-DD  (session N)
- Players: solo | 2p | 3p | 4p (who, which pads/keyboards)
- Build: <commit or build date>
- Seed / world: <world name, dive seed if shown>
- Dives: <tries, how many cleared, floor reached each try>

Per-floor numbers (paste the end-of-dive .txt for each dive):
| Dive | Floor | Time | Damage | Heals | Loaves eaten | Result |
|---|---|---|---|---|---|---|

Feel notes (plain words, one line each; tag hard / many / heals / loaves / stir / flow if obvious):
- 

Last question: did the loop want a field-repair beat for weapon wear? (default no)
Samey? verdict:
```

## Tuning-branch rule (M7 Q3 option A, approved 2026-11-05)

- One playtest, one notes entry, one tuning branch: `claude/tuning-<yyyymmdd>` (or `claude/tuning-<playtest id>`).
- The branch changes **data/JSON only** (`Assets/StreamingAssets/Tuning/*.json`, tuning tables). A note with no matching knob is reported as "no knob" and a code change goes through its own normal branch, not this one.
- Each changed key is listed in the commit message with old value, new value and the note that caused it.
- Merge after one `check` and a bot re-run showing the loop is still winnable (bars in the M7 plan). Notes are closed or moved to `docs/PARKING_LOT.md`.
- Tuning live during play (an in-game panel) is not allowed: it risks untracked changes.

Key list and complaint tags: `docs/design/m7-tuning-notes.md`.
