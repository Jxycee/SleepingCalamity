---
name: sc-merge
description: Merge a reviewed SleepingCalamity branch into main (no pull requests) - check the tip, merge commit with the handoff folded in, push, tell the user. Use when a branch's review is done.
---
# Merging a SleepingCalamity branch into main

Current state:
- Branch: !`git rev-parse --abbrev-ref HEAD`
- Uncommitted: !`git status --short --untracked-files=no`

Steps:
1. **Preconditions.** The branch's review is done with no open blocking findings. Networking, save-data and milestone merges also need the user's one-line go: ask, and wait for it.
2. **Bring main in.** (Cloud art profile: `bash Tools/cloud/sc_art_profile.sh off` first, `on` after step 5.) On the branch: `git fetch origin main` and `git merge origin/main` (merge commit, never rebase). Resolve conflicts; regenerate generated files with their builders, not by hand.
3. **Check the branch tip.** Run the checkout's own wrapper (`<worktree>\Tools\unity.ps1 check`, which tests that checkout and queues behind any other PlayMode run); in the cloud, run `bash Tools/cloud/unity.sh check --out <scratchpad>` in the session's repo, which records the same marker. It must print `SC RESULT check PASS` and an `SC MARKER <sha>` line matching `git rev-parse HEAD`. A FAIL is work: fix it on the branch and repeat. Skip this step when `git diff --name-only main...<branch> -- Assets Packages ProjectSettings` is empty (art or docs only); the guard hook allows that merge without a marker.
4. **Merge, with the bookkeeping in the same commit.** `git checkout main` and `git pull origin main` (as their own commands), then `git merge --no-ff --no-commit <branch>`. If git can't merge cleanly, abort, go back to step 2 and check the new tip. Otherwise update `CURRENT.md` with `sc-handoff` (merge, check counts) and tick the item in `docs/superpowers/handoff/QUEUE.md`, `git add` those two files, and `git commit -m "Merge <branch>: <one-line summary>"`. The guard hook checks the `git merge` command itself (with or without `--no-commit`) and blocks it if step 3's marker is missing for the branch tip. No second `check` on `main` after a clean merge of a checked tip, even if main moved since the check (`session-usage.md`).
5. **Push.** `git push origin main`, then the branch if it changed.
6. **Tell the user** what was merged: branch, merge commit, and the check result line.
