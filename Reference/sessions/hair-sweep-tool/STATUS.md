# Hair sweep tool STATUS

- State: round 1 delivered (22 variants, 0 failed). Tool done; judge picks from the sheet and edits the config for round 2.
- Branch: claude/hair-sweep-tool-abafe3 (not merged).
- Run a round: `python Art/Blender/tools/experimental/tobin_hair/sweep/hair_sweep.py Art/Blender/tools/experimental/tobin_hair/sweep/config_round1.json --out C:/AI/test_out/heroes/tobin/hair_sweep`
  (copy the config to config_round2.json and set `"round": 2`; cached variants are reused; `--sheet-only` re-ranks after a weights change). Docs: sweep/README.md.
- Config: Art/Blender/tools/experimental/tobin_hair/sweep/config_round1.json
- Latest sheet: C:/AI/test_out/heroes/tobin/hair_sweep/round1/sheet.png
- Latest CSV: C:/AI/test_out/heroes/tobin/hair_sweep/round1/scores.csv (variant dirs and renders under round1/variants/)
- Round 1 took about 20 min (954 s + 247 s for 4 variants re-added after a seed-name typo), about 40 s per variant.
- Done: strands -> hd_opt fit -> hd_fit -> tinted renders -> scores -> rank -> sheet; per-stage hash cache; failures recorded. hd_opt.py gained `--fix k=v,...` and writes fit_best.npz.
- Not done / approximated: builder CLEARANCE/EARS/CROWN checks (need the full shell build) are replaced by hd_fit clearance numbers, `crown_cm` and an approximate `ear_cover`; chain count (rig_export) not run; hull masks used, not SAM 3; colour compares against the reference mask above z 1.68, which includes some forehead skin; hd_opt hard-codes the 2 mm clearance push so `fit.clear` only affects hd_fit.
- Round 1 reading: silhouette fit saturates (IoU mean 0.84-0.87 for all, since every variant is fitted to the same masks), so rank spread is small; the current-build mix is mid-pack on IoU (0.865 vs best 0.868), not clearly the top. The tint variant (base_dark) is separated clearly by dE (11.4 vs 3.5).
