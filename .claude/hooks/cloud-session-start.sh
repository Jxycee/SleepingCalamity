#!/usr/bin/env bash
# SessionStart hook: in cloud sessions only, fetch Git LFS content so models and textures are real files.
# The environment's setup script installs git-lfs (docs/superpowers/handoff/cloud-environment.md).
[ "$CLAUDE_CODE_REMOTE" = "true" ] || exit 0
cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
if ! command -v git-lfs >/dev/null 2>&1; then
    echo "git-lfs is not installed; LFS files are pointers. Add the setup script from docs/superpowers/handoff/cloud-environment.md to the environment."
    exit 0
fi
git lfs install --local >/dev/null 2>&1
# The cloud agent proxy injects the real GitHub token, but git-lfs only sends a request once it has some credential.
git config credential.helper >/dev/null 2>&1 \
    || git config --local credential.helper '!f() { echo username=x-access-token; echo password=proxy-injected; }; f'
if git lfs pull >/dev/null 2>&1; then
    echo "Git LFS content pulled."
else
    echo "git lfs pull failed; LFS files may still be pointers."
fi
exit 0
