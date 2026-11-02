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
if git lfs pull >/dev/null 2>&1 && git lfs checkout >/dev/null 2>&1; then   # checkout: a fresh worktree holds pointer stubs
    echo "Git LFS content pulled."
else
    echo "git lfs pull failed; LFS files may still be pointers."
fi
# art environment: lift the blender MCP deny again if a fresh checkout or a merge reset settings.json (Tools/cloud/sc_art_profile.sh)
[ "$(cat "$HOME/.sc-setup/profile" 2>/dev/null)" = art ] && [ -f Tools/cloud/sc_art_profile.sh ] && bash Tools/cloud/sc_art_profile.sh on
# Unity licence from the environment's secrets (the setup script cannot read them); serial activation waits for unity.sh
bash Tools/cloud/sc_unity_license.sh --file-only 2>/dev/null | grep -v '^$' || true
# .csproj + .sln for the csharp-lsp plugin once the Unity editor files are in (the PC gets these from Unity; warm runs take about 15 s)
# (only after the setup job is DONE: before that the editor may be half-extracted, and the job writes them itself)
if [ "$(cat "$HOME/.sc-setup/unity.status" 2>/dev/null)" = DONE ] && [ -f Tools/cloud/sc_cloud_compile.py ]; then
    timeout 300 python3 Tools/cloud/sc_cloud_compile.py --csproj 2>&1 | grep -E '^(wrote|FAIL )' | head -5
fi
# cloud Claude Code offers no LSP tool even with the csharp-lsp plugin loaded (tested 2026-10-30); say what stands in for it
[ -f Tools/cloud/sc_lsp.py ] && echo "No LSP tool in cloud sessions: navigate C# with python3 Tools/cloud/sc_lsp.py def|refs|hover <file>:<line> <name>, or symbols <query>"
[ "$(cat "$HOME/.sc-setup/unity.status" 2>/dev/null)" = RUNNING ] && kill -0 "$(cat "$HOME/.sc-setup/unity.pid" 2>/dev/null)" 2>/dev/null && echo "Unity editor still downloading in the background: bash Tools/cloud/setup.sh --wait"
exit 0
