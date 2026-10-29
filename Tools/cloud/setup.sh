#!/usr/bin/env bash
# SleepingCalamity cloud environment setup: one script that gives a Claude Code cloud session (Linux, no GPU; launching the Unity editor needs a licence) everything this project's cloud-capable tools need.
#
# Use it as the environment's setup script (cloud environment settings), either by pasting this file or, when the repo is already cloned, `bash Tools/cloud/setup.sh`.
# It is idempotent (every step checks first), cached by the environment, and safe to re-run. Always exits 0 so one bad step never blocks the session; any failed step is listed at the end.
#
#   bash Tools/cloud/setup.sh            full: system packages, Git LFS, Python libs, Blender 4.5.3, .NET 8, uv, Unity 6000.6.5f1 editor files (C# + shader compile), DXC
#   bash Tools/cloud/setup.sh --lite     everything except the 4.2 GB Unity editor download (art, docs and review sessions)
#   bash Tools/cloud/setup.sh --check    print what is installed and working; installs nothing
#
# What each piece is for (docs/superpowers/handoff/cloud-environment.md has the details):
#   git-lfs            models and textures are real files, not pointers (the SessionStart hook runs `git lfs pull`)
#   Blender 4.5.3      `~/apps/blender-4.5.3-linux-x64/blender` (also `blender` on PATH): every generator in Art/Blender runs headless (`-b --factory-startup`)
#   numpy, pillow, pyarrow   the Blender-side tools and the asset finder (Art/Blender/tools/sc_adopt_find.py) import them outside Blender too
#   dotnet 8           Tools/cloud/sc_core_tests.py (Core EditMode tests under NUnitLite) and sc_zone_check.py
#   Unity editor ~/unity/ed/Editor/Unity (6000.6.5f1 Linux; the download needs no licence, launching it does: see cloud-environment.md): Tools/cloud/unity.sh runs it in batchmode (compile, build, test-edit, test-play, check, exec: the Linux twin of
#                      Tools/unity.ps1), and Tools/cloud/sc_cloud_compile.py (C# compile, never launches Unity) and sc_shader_check.py (every shader pass via DXC) use its files
#   uv / uvx           the project's Unity MCP server entry (.mcp.json, mcpforunityserver==10.3.0) is pre-fetched; it only works against a running editor, so it is idle in the cloud
#
# NOT provisioned here (needs the user's PC): the live Blender MCP, AssetRipper/REA, the crew-watch board, a GPU (renders under xvfb are software-only).
# The Unity Linux editor downloads without a licence, but batchmode launches fail without one (2026-10-29: exit 198, 'No valid Unity Editor license found'). Merges follow CLAUDE.md: a green `check` on the exact commit (unity.sh check records it like unity.ps1).
set -u
MODE=full
case "${1:-}" in
    --lite) MODE=lite ;;
    --check) MODE=check ;;
    "") ;;
    *) echo "usage: setup.sh [--lite|--check]" >&2; exit 4 ;;
esac

BLENDER_VER=4.5.3
BLENDER_DIR="$HOME/apps/blender-$BLENDER_VER-linux-x64"
BLENDER_URL="https://download.blender.org/release/Blender4.5/blender-$BLENDER_VER-linux-x64.tar.xz"
REPO="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/../.." 2>/dev/null && pwd || pwd)}"
FAILED=()
SUDO=""
[ "$(id -u)" = "0" ] || SUDO="sudo"

say()  { printf '\n== %s\n' "$*"; }
have() { command -v "$1" >/dev/null 2>&1; }
step() { # step "name" command...   runs the command, records a failure instead of stopping
    local name="$1"; shift
    if "$@"; then echo "ok: $name"; else echo "FAILED: $name" >&2; FAILED+=("$name"); fi
}
apt_install() { $SUDO apt-get install -y -qq --no-install-recommends "$@"; }

# ------------------------------------------------------------------------------------------------ check
do_check() {
    say "SleepingCalamity cloud environment check ($REPO)"
    row() { printf '%-22s %s\n' "$1" "$2"; }
    row git-lfs      "$(have git-lfs && git lfs version 2>/dev/null | head -1 || echo MISSING)"
    local B; B="$(command -v blender || true)"; [ -z "$B" ] && [ -x "$BLENDER_DIR/blender" ] && B="$BLENDER_DIR/blender"
    row blender      "$([ -n "$B" ] && "$B" --version 2>/dev/null | head -1 || echo MISSING)"
    row python3      "$(have python3 && python3 --version || echo MISSING)"
    row py-libs      "$(python3 -c 'import numpy, PIL, pyarrow; print("numpy", numpy.__version__, "pillow", PIL.__version__, "pyarrow", pyarrow.__version__)' 2>/dev/null || echo 'MISSING (numpy, pillow, pyarrow)')"
    row dotnet       "$(have dotnet && dotnet --version || echo MISSING)"
    row uv           "$(have uv && uv --version || echo MISSING)"
    row unity-editor "$([ -d "$HOME/unity/ed/Editor" ] && echo "$HOME/unity/ed/Editor present" || echo 'MISSING (python3 Tools/cloud/sc_cloud_compile.py --setup)')"
    row dxc          "$([ -x "$HOME/dxc/bin/dxc" ] && echo "$HOME/dxc/bin/dxc present" || echo 'MISSING (python3 Tools/cloud/sc_shader_check.py --setup)')"
    local f; f="$(git -C "$REPO" ls-files '*.fbx' '*.png' 2>/dev/null | head -1)"; [ -n "$f" ] && f="$REPO/$f"
    row lfs-content  "$([ -f "$f" ] && { head -c 40 "$f" | grep -q 'git-lfs' && echo 'POINTERS (run git lfs pull)' || echo ok; } || echo 'n/a (file not found)')"
}
[ "$MODE" = check ] && { do_check; exit 0; }

# ------------------------------------------------------------------------------------------------ system packages
say "System packages"
if have apt-get; then
    step "apt update" $SUDO apt-get update -qq
    # git-lfs; xz/curl/unzip for the downloads; the X/GL runtime libraries Blender's headless renderer loads; python; xvfb for any tool that wants a display
    step "apt packages" apt_install git git-lfs curl ca-certificates xz-utils unzip jq \
        python3 python3-pip python3-venv \
        libgl1 libegl1 libxi6 libxrender1 libxkbcommon0 libsm6 libxxf86vm1 libxfixes3 libxext6 libx11-6 xvfb
    # the Unity editor's own runtime libraries (batchmode, -nographics); a name that does not exist on this distro version is skipped, the rest install
    for pkg in libgtk-3-0 libnss3 libgbm1 libglu1-mesa libcups2 libxss1 libxcursor1 libxrandr2 libxcomposite1 libxdamage1 libxtst6 libatk-bridge2.0-0 libcurl4 libssl3 libpulse0 libasound2 libasound2t64; do
        apt-cache show "$pkg" >/dev/null 2>&1 && apt_install "$pkg" >/dev/null 2>&1 || true
    done
else
    echo "no apt-get on this image: install git-lfs, curl, xz, python3-pip, the Blender X/GL libraries and dotnet 8 by hand" >&2
    FAILED+=("no apt-get")
fi

# ------------------------------------------------------------------------------------------------ git lfs (global hooks; the repo's SessionStart hook pulls the content)
say "Git LFS"
if have git-lfs; then
    step "git lfs install" git lfs install --skip-repo
fi

# ------------------------------------------------------------------------------------------------ python libraries
say "Python libraries"
if have python3; then
    pip_libs() {
        python3 -m pip install --quiet --user --break-system-packages numpy pillow pyarrow 2>/dev/null \
            || python3 -m pip install --quiet --user numpy pillow pyarrow
    }
    step "pip numpy pillow pyarrow" pip_libs
fi

# ------------------------------------------------------------------------------------------------ blender
say "Blender $BLENDER_VER"
install_blender() {
    mkdir -p "$HOME/apps" "$HOME/.local/bin" || return 1
    if [ ! -x "$BLENDER_DIR/blender" ]; then
        curl -fL --retry 4 "$BLENDER_URL" | tar -xJ -C "$HOME/apps" || return 1
    fi
    ln -sf "$BLENDER_DIR/blender" "$HOME/.local/bin/blender"
    "$BLENDER_DIR/blender" -b --factory-startup --python-expr "import bpy; print('BLENDER_OK', bpy.app.version_string)" 2>&1 | grep -q BLENDER_OK
}
step "blender $BLENDER_VER" install_blender

# ------------------------------------------------------------------------------------------------ dotnet 8
say ".NET 8"
install_dotnet() {
    have dotnet && dotnet --list-sdks 2>/dev/null | grep -q '^8\.' && return 0
    if have apt-get && $SUDO apt-get install -y -qq dotnet-sdk-8.0 2>/dev/null; then return 0; fi
    # fallback: Microsoft's installer script into ~/.dotnet
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh || return 1
    bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet" >/dev/null || return 1
    ln -sf "$HOME/.dotnet/dotnet" "$HOME/.local/bin/dotnet"
}
step "dotnet 8" install_dotnet

# ------------------------------------------------------------------------------------------------ uv (Unity MCP server entry in .mcp.json)
say "uv"
install_uv() {
    have uv || { curl -LsSf https://astral.sh/uv/install.sh | sh >/dev/null 2>&1; }
    export PATH="$HOME/.local/bin:$PATH"
    have uv && uvx --from mcpforunityserver==10.3.0 mcp-for-unity --help >/dev/null 2>&1 || have uv
}
step "uv (+ mcpforunityserver 10.3.0 pre-fetch)" install_uv

# ------------------------------------------------------------------------------------------------ PATH for later shells
say "PATH"
persist_path() {
    local line='export PATH="$HOME/.local/bin:$HOME/apps/blender-'"$BLENDER_VER"'-linux-x64:$HOME/.dotnet:$PATH"'
    grep -qF 'blender-'"$BLENDER_VER" "$HOME/.bashrc" 2>/dev/null || echo "$line" >> "$HOME/.bashrc"
    # non-interactive shells (the agent's Bash tool) do not read .bashrc: also link the binaries somewhere already on PATH
    for b in blender dotnet uv uvx; do
        src="$(command -v "$b" 2>/dev/null || true)"
        [ -n "$src" ] && [ -w /usr/local/bin ] && ln -sf "$src" "/usr/local/bin/$b" 2>/dev/null
    done
    return 0
}
step "PATH" persist_path

# ------------------------------------------------------------------------------------------------ unity editor + dxc (Tools/cloud/unity.sh, compile and shader checks)
if [ "$MODE" = full ]; then
    say "Unity editor files and DXC (about 9 GB unpacked, 10+ minutes the first time)"
    if [ -f "$REPO/Tools/cloud/sc_cloud_compile.py" ]; then
        step "unity editor + packages (sc_cloud_compile.py --setup)" python3 "$REPO/Tools/cloud/sc_cloud_compile.py" --setup --project "$REPO"
        step "dxc (sc_shader_check.py --setup)" python3 "$REPO/Tools/cloud/sc_shader_check.py" --setup
    else
        echo "repo not cloned yet ($REPO/Tools/cloud missing): the first session runs 'python3 Tools/cloud/sc_cloud_compile.py --setup' and 'python3 Tools/cloud/sc_shader_check.py --setup'" >&2
    fi
fi

# ------------------------------------------------------------------------------------------------ summary
say "Summary"
do_check
if [ "${#FAILED[@]}" -gt 0 ]; then
    printf '\nFAILED STEPS (the rest still ran; exit 0 so one bad step never blocks the session):\n'; printf '  - %s\n' "${FAILED[@]}"
fi
echo "SleepingCalamity cloud setup complete ($MODE)."
exit 0
