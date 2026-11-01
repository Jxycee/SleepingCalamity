<!-- Covers: blind A/B and parity-test procedure for heroes and first-of-family items, with lessons from the stand rounds. Moved from SKILL.md section 3. -->

- **Blind A/B (T4 heroes and the first item of a new family, e.g. the first B4 facade):**
  - Working heuristics from FishGame's rounds, not rules: run `sc_imgdiff.py a.png b.png` first (under about 0.5 on 0-255 the pair looked identical at review size and reviewers invented a preference); treat margins of about 2 points as ties; use a third reviewer to break a split.
  - Two fresh reviewers, one sheet: real references in the top row, the candidates shuffled and unlabelled below. The reviewer opens only the sheet: no repo files, no prior scores.
  - A result is more trustworthy when the stated reason matches the change.
  - Self-judged and one-reviewer results are weak evidence (the cottage's A/B was self-judged: treat it as unconfirmed).
  - Compare scores only within one rubric version and one reference row.
- **Lessons from the stand's A/B rounds:**
  - A defect named in a review is not a preference: removing it can still lose. Improve the features reviewers credit instead of replacing them. Visible beats correct.
  - Reviewers punish whatever is labelled "candidate": KCD2's own art scored 21/40 blind. A same-sheet A/B of two candidates removes most of that noise.
  - **Parity test:** to ask "is it at the target's level", show our render beside a crop of a similar subject from the target game (private frames, scratchpad only), shuffled, and ask which is the shipped game. Parity is picks near 50%.
  - Snapshot the baked asset with `sc_snap.py` (the .blend and its maps) before rebaking anything you will A/B.
