Title: Hollowmere stairs + traversal 3
Target: cloud
Status: blocked: partial. Job 2 fixed on branch claude/stairs-traversal-3 (38c5eb5, unmerged, needs PC build+check); job 1 shade tweak only, job 3 not done. See STATUS.md
Started: 2026-11-02T08:03:36Z (cloud launcher)
Model: sonnet
From: coordinator session 7 (board queue qwr9ux), follow-ups of town-surfaces 5a910999 and traversal-2 db3d88bb
Status file: Reference/sessions/stairs-traversal-3/STATUS.md

Brief:
Read first: Reference/sessions/town-surfaces/STATUS.md (and its round3/ frames), the traversal-2 STATUS and Reference/qa/traversal/FIXLIST.md.

Jobs, in order:
1. A9 stair read: the Hollowmere town stair flights still read as ramps. Give them a readable step rhythm (real treads and risers, a contact shadow under each nosing) in the stair builders. Keep the walk surface walkable (no new step-up blockers; the traversal test must stay green).
2. Terrace flight black sliver: a thin black ribbon runs along the Terrace flight edge in the Cap_HM_TerraceSteps capture. It is NOT a gutter or kerb (those were removed and the image stayed the same). Locate the object first (likely a terrain seam, a skirt mesh, a backface or a decal edge), then fix it at the builder.
3. Traversal-2 leftovers: islands, stuck pockets, the R2_03 and R2_08 corners, and the Blocked +3 near the lower gate. Re-run the traversal scan and report before/after counts.

Rules:
- Builders only (never hand-edit .unity/.prefab); hook into ProjectBuilder.RunAll.
- Follow .claude/rules/session-usage.md: Sonnet subagents run Unity; captures with SC_CAPTURE_ONLY and the fewest cameras (Hollowmere captures OOM in the cloud: one reduced camera, or ask the coordinator via board report for a PC capture). At most 3 art rounds; stop at ~25/40.
- Report status through the board profile. STATUS.md first line is a state word (Status: IN PROGRESS / DONE / BLOCKED).
- Merge on a green check after merging origin/main (merge commit, no PR, never force-push). On builder-output conflicts take main's side, run one build, then check.

Done when: all three jobs merged with a green check, before/after frames of the stairs and the Terrace edge in Reference/sessions/stairs-traversal-3/, traversal counts in STATUS, STATUS says DONE and Archive: ready.
