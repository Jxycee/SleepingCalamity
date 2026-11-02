<!-- Covers: checking skill paths and the eval briefs after editing sc-* skills. Moved from SKILL.md. -->

## Maintaining these skills
After editing any sc-* skill or renaming a tool, check that every path the skills mention exists:
`grep -rhoE '(Art|Assets|docs|Tools|Reference)/[^ )\`,]+' .claude/skills | sort -u | while read p; do [ -e "$p" ] || echo MISSING $p; done` (placeholders in `<...>` will show; check the rest).
**Evals:** `.claude/skills/sc-asset-craft/evals/evals.json` holds 3 held-out briefs (B7 barrel, B4 facade panel, B11 lantern). Re-run them after a big skill change, and revert an edit that loses to the previous version.
