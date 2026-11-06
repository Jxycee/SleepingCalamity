# Dungeon-1 difficulty results (recovered from a killed first attempt, 2026-11-06)
Logs in session scratchpad (not in repo). ON = main as is (plan 5 modifiers live); OFF = modifier percents zeroed locally (not committed; provenance recovered from scratchpad dirs botOn/botOff, the OFF edit itself was not recorded).
Tough runs (host tops HP up; bar: finish floors 1-7 within 6300 s), seconds to finish / dive fails:
| seed | ON | OFF |
|---|---|---|
| 7 | 866 s, 0 fails, 7 floors | 827 s, 0 fails |
| 31 | 1187 s, 0 fails | 1697 s, 1 fail (F5 death at 653 s, then redone) |
| 12345 | 938 s / 941 s (two runs), 0 fails | 952 s, 0 fails |
Normal (no top-up) bot, 6300 s each: every seed loops F1-F3 dying on F3 (ON: fails 49/30/33 for 12345/31/7; OFF: 53/29/33). Deepest floor 3 (7: 2 cleared before F3 deaths). No difference between ON and OFF.
