State: IN PROGRESS
Session: Q5. Builder churn fix
Progress: 5%
Sheet: none (code only)
Blocker: none

Claimed 2026-10-31 after the square merge (c66ea1f). Known churn seen on every build this session: HeroAnimator.controller (+ .meta) fully rewritten,
M_CrackWeeds.mat (_SrcBlend), BeautyCorner_Hollowmere.unity (+5 lines), LookTest M_crack/M_drip/M_moss.mat, a Content/Rendering/_ProbeTmp folder left by captures.
Plan: find the writer of each, make it write only on a real change (or write deterministically), and add an EditMode test or check script that two builds in a row leave git status clean.
