# Builder churn (graphics vs -nographics bytes)

- Session: builder_churn
- Job: root-cause and fix builders writing different bytes in graphics vs -nographics runs (material pass flags such as MotionVectors / _SrcBlend, scene bytes); guard with a test; merge after the lock lifts; update session-usage.md capture rule.
- Progress: (1) BuildAll in graphics vs -nographics wrote identical bytes (3 runs, xvfb/llvmpipe vs Null device), but that subagent worktree branched from a stale a4609f2 (323 commits behind), so it is being redone. (2) History: the recurring churn alternates between PC (-0500) and cloud (+0000) commits: BeautyCorner_Hollowmere.unity quaternion last-digit noise and HeroAnimator.controller signature rewrites, so platform, not graphics mode, looks like a main cause. The MotionVectors flips in history (e.g. 420f0be) came from capture-era WIP; builders since set the pass explicitly. (3) Running now: on origin/main b4db85f, -nographics build, graphics build and single-camera graphics captures (Hollowmere, DungeonCorner, BeautyCorner), each diffed. Waiting on those Unity runs.
- Status: IN PROGRESS
- Blocker: none
- Archive: no
- Branch: claude/beautiful-thompson-pvb6cv
- SC RESULT: -
- Merge commit: -
