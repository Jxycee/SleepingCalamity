#!/usr/bin/env bash
# Builder determinism check: runs `unity.sh build` twice and fails if the working tree is not identical to what it was before.
# Usage: bash Tools/cloud/sc_churn_check.sh [--out DIR]    (start from a clean tree: commit or stash first)
# Exit 0 clean, 1 churn (the changed files are listed), 2 the tree was dirty before, other = a build failed.
set -u
cd "$(dirname "$0")/../.."
out="${TMPDIR:-/tmp}/sc_churn"
[ "${1:-}" = "--out" ] && out="$2"
mkdir -p "$out"
if [ -n "$(git status --porcelain -uall)" ]; then echo "SC CHURN tree is not clean; commit or stash first"; git status --short -uall | head; exit 2; fi
for i in 1 2; do
  bash Tools/cloud/unity.sh build --out "$out/b$i" --timeout 120 || { echo "SC CHURN build $i failed"; exit 3; }
  if [ -n "$(git status --porcelain -uall)" ]; then
    echo "SC CHURN FAIL after build $i:"; git status --short -uall; git diff --stat | tail -15; exit 1
  fi
done
echo "SC CHURN PASS (two builds, git status clean)"
