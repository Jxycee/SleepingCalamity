---
name: sc-merge
description: Merge a reviewed SleepingCalamity branch into main (no pull requests) - check, merge commit, re-check on main, push, tell the user. Use when a branch's review is done.
---
# Merging a SleepingCalamity branch into main

Current state:
- Branch: !`git rev-parse --abbrev-ref HEAD`
- Uncommitted: !`git status --short --untracked-files=no`

Steps:
1. **Preconditions.** The branch's review is done with no open blocking findings. Networking, save-data and milestone merges also need the user's one-line go: ask, and wait for it.
2. **Bring main in.** (Cloud art profile: `bash Tools/cloud/sc_art_profile.sh off` first, `on` after step 6.) On the branch: `git fetch origin main` and `git merge origin/main` (merge commit, never rebase). Resolve conflicts; regenerate generated files with their builders, not by hand.
3. **Check the branch tip.** Check the branch out in `C:\Dev\SleepingCalamity` (the wrapper always tests that checkout, never a worktree) and run `Tools/unity.ps1 check` after the PlayMode guard (CLAUDE.md); in the cloud, check it out in the session's repo and run `bash Tools/cloud/unity.sh check --out <scratchpad>`, which records the same marker. It must print `SC RESULT check PASS` and an `SC MARKER <sha>` line matching `git rev-parse HEAD`. A FAIL is work: fix it on the branch and repeat. Skip this step and step 5 when `git diff --name-only main...<branch> -- Assets Packages ProjectSettings` is empty (art or docs only); the guard hook allows that merge without a marker.
4. **Merge.** `git checkout main` and `git pull origin main` (as their own commands), then `git merge --no-ff <branch> -m "Merge <branch>: <one-line summary>"`. The guard hook blocks this if step 3's marker is missing for the branch tip.
5. **Check main.** Run `check` again on `main`; it must PASS before pushing.
6. **Push.** `git push origin main`, then the branch if it changed.
7. **Record it.** Update `CURRENT.md` with `sc-handoff` (merge commit, check counts), commit and push it to `main`.
8. **Tell the user** what was merged: branch, merge commit, and the check result line.
