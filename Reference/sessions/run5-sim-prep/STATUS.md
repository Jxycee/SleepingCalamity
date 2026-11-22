State: DONE (Run 5 prep), merged into main (merge commit: see git log "Merge claude/run5-sim-prep")
Branch: claude/run5-sim-prep
Sheet: Tools/balance_sim/baseline/base_sheet.png
CSV: Tools/balance_sim/baseline/base_metrics.csv
Config: Tools/balance_sim/config.json (overrides + targets)
Command: Tools\balance_sim\run.cmd [--tag name] [--out DIR]   (about 3.5 min GPU; --selftest)
Baseline headline (axe, average skill, ASSUMED targets): solo dive success 0% (floor 3 and 5 damage 0.62 of HP vs 0.55 cap; no heal point on floors 3 and 5, loaves 0 without bakery), 2p 0.3%, 4p 43%. TTK per regular enemy 2.4-6.6 s floors 1-4 (inside bands); Warden 75-93 s in co-op vs 20-60 assumed (life x1.7-2.8). Weapon level after dive 1/3/10: solo 1.4/3.7/8.7, 2p 2.6/6.1/13.2, 4p 5.3/10.8/22.0 (4p over-grows: each hero gets full ABS credit). 54 PASS / 19 FAIL vs assumed bands.
Not done: QUEUE 53 (enemy attack ids tunable) stays open.
