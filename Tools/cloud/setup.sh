#!/usr/bin/env bash
# SleepingCalamity cloud environment setup: gives a Claude Code cloud session (Ubuntu 24.04, root, no GPU) every tool this project's cloud work uses,
# and leaves the machine set up so more can be installed with one command (apt, pip, uv, npm, dotnet tool).
#
# The environment's setup script (cloud environment menu > Edit > Setup script) should be the bootstrap in docs/superpowers/handoff/cloud-environment.md,
# which cds into the repo and runs this file. It is idempotent (every step checks first) and safe to re-run. It always exits 0 so one bad step never
# blocks the session; failed steps are listed at the end and `bash Tools/cloud/setup.sh --check` shows the state at any time.
#
#   bash Tools/cloud/setup.sh            full: everything below; the 4.2 GB Unity editor download runs in the BACKGROUND so the session starts in a few minutes
#   bash Tools/cloud/setup.sh --wait     full, but wait for the Unity editor instead of backgrounding it (or, if a background run is going, wait for it)
#   bash Tools/cloud/setup.sh --lite     everything except the Unity editor (art, docs and review sessions)
#   bash Tools/cloud/setup.sh --check    print what is installed and working; installs nothing
#
# What each piece is for (docs/superpowers/handoff/cloud-environment.md has the details):
#   system tools       git-lfs, curl/aria2/xz/zstd/7z/unzip, jq, ripgrep, ffmpeg, ImageMagick, sqlite3, build-essential: downloads, archives, images, video
#   software graphics  Mesa llvmpipe (GL 4.5) and lavapipe (Vulkan), xvfb: Blender EEVEE/Cycles renders and Unity --graphics runs without a GPU
#   Blender 4.5.3      ~/apps/blender-4.5.3-linux-x64/blender, `blender` on PATH; every generator in Art/Blender runs headless (`-b --factory-startup`).
#                      Pillow and SciPy go into Blender's own Python too: the tools import PIL inside Blender
#   Python libs        numpy, pillow, pyarrow, scipy, opencv (headless), imageio, trimesh, pygltflib, requests, pyyaml, matplotlib: tools outside Blender
#   .NET 8             Tools/cloud/sc_core_tests.py (Core EditMode tests under NUnitLite); NuGet cache warmed for NUnit 3.14
#   csharp-ls, pwsh    the csharp-lsp plugin's server; PowerShell to read and dry-run Tools/*.ps1
#   uv / uvx           Python tools on demand (`uvx <tool>`, `uv tool install <tool>`)
#   Unity 6000.6.5f1   ~/unity/ed/Editor/Unity (Linux) + the registry packages + DXC: Tools/cloud/unity.sh (batchmode twin of Tools/unity.ps1, needs a
#                      licence: UNITY_LICENSE or UNITY_SERIAL secrets, see the doc), sc_cloud_compile.py (C# compile, no licence) and sc_shader_check.py
#
# NOT provisioned here (needs the user's PC): the live Blender and Unity MCP servers, AssetRipper/REA, a real GPU.
# Web search and web fetch are Claude Code tools, not packages: they need nothing here beyond the environment's network access (Full, or Trusted plus
# the domains in the doc).
set -u
MODE=full
case "${1:-}" in
    --lite) MODE=lite ;;
    --wait) MODE=wait ;;
    --check) MODE=check ;;
    "") ;;
    *) echo "usage: setup.sh [--wait|--lite|--check]" >&2; exit 4 ;;
esac

BLENDER_VER=4.5.3
BLENDER_SERIES=4.5
BLENDER_DIR="$HOME/apps/blender-$BLENDER_VER-linux-x64"
BLENDER_URL="https://download.blender.org/release/Blender$BLENDER_SERIES/blender-$BLENDER_VER-linux-x64.tar.xz"
REPO="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/../.." 2>/dev/null && pwd || pwd)}"
STATE="$HOME/.sc-setup"                    # background Unity run: unity.log, unity.status (RUNNING|DONE|FAILED), unity.pid
PY_LIBS=(numpy pillow pyarrow scipy opencv-python-headless imageio trimesh pygltflib requests pyyaml matplotlib)
BLENDER_PY_LIBS=(pillow scipy)
FAILED=()
SUDO=""
[ "$(id -u)" = "0" ] || SUDO="sudo"
export DEBIAN_FRONTEND=noninteractive
export PATH="$HOME/.local/bin:$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
mkdir -p "$STATE"

say()  { printf '\n== %s\n' "$*"; }
have() { command -v "$1" >/dev/null 2>&1; }
step() { # step "name" command...   runs the command, records a failure instead of stopping
    local name="$1"; shift
    if "$@"; then echo "ok: $name"; else echo "FAILED: $name" >&2; FAILED+=("$name"); fi
}
apt_install() { $SUDO apt-get install -y -qq --no-install-recommends "$@" >/dev/null; }
unity_status() { cat "$STATE/unity.status" 2>/dev/null || echo NONE; }
unity_running() { [ "$(unity_status)" = RUNNING ] && kill -0 "$(cat "$STATE/unity.pid" 2>/dev/null)" 2>/dev/null; }

# ------------------------------------------------------------------------------------------------ check
do_check() {
    say "SleepingCalamity cloud environment check ($REPO)"
    row() { printf '%-16s %s\n' "$1" "$2"; }
    row git-lfs      "$(have git-lfs && git lfs version 2>/dev/null | head -1 || echo MISSING)"
    row blender      "$(have blender && blender --version 2>/dev/null | head -1 || echo MISSING)"
    row blender-py   "$([ -x "$BLENDER_DIR/blender" ] && "$BLENDER_DIR/$BLENDER_SERIES/python/bin/python3."* -c 'import PIL, numpy, scipy; print("PIL", PIL.__version__, "numpy", numpy.__version__)' 2>/dev/null || echo 'MISSING (pillow/scipy in Blender python)')"
    row python3      "$(have python3 && python3 --version || echo MISSING)"
    row py-libs      "$(python3 -c 'import numpy, PIL, pyarrow, scipy, cv2, trimesh; print("numpy", numpy.__version__, "pillow", PIL.__version__, "cv2", cv2.__version__, "+7 more")' 2>/dev/null || echo "MISSING (some of: ${PY_LIBS[*]})")"
    row dotnet       "$(have dotnet && dotnet --version 2>/dev/null || echo MISSING)"
    row csharp-ls    "$(have csharp-ls && echo present || echo MISSING)"
    row pwsh         "$(have pwsh && pwsh -NoProfile -Command '$PSVersionTable.PSVersion.ToString()' 2>/dev/null || echo MISSING)"
    row uv           "$(have uv && uv --version || echo MISSING)"
    row node         "$(have node && node --version || echo MISSING)"
    row ffmpeg       "$(have ffmpeg && ffmpeg -version 2>/dev/null | head -1 | cut -d' ' -f1-3 || echo MISSING)"
    row magick       "$(have convert && convert -version 2>/dev/null | head -1 | cut -d' ' -f2-3 || echo MISSING)"
    row gl/vulkan    "$(ls /usr/lib/x86_64-linux-gnu/dri/swrast_dri.so >/dev/null 2>&1 && echo -n 'llvmpipe ' || echo -n 'NO-llvmpipe '; ls /usr/share/vulkan/icd.d/lvp_icd*.json >/dev/null 2>&1 && echo lavapipe || echo NO-lavapipe)"
    row xvfb-run     "$(have xvfb-run && echo present || echo MISSING)"
    local ust; ust="$(unity_status)"
    row unity-editor "$([ -x "$HOME/unity/ed/Editor/Unity" ] && echo "$HOME/unity/ed/Editor/Unity present" || echo "MISSING (background setup: $ust; log $STATE/unity.log)")"
    row unity-pkgs   "$(ls -d "$HOME/unity/pkgs/"*/ 2>/dev/null | wc -l) registry packages in ~/unity/pkgs"
    row unity-lic    "$(ls "$HOME/.local/share/unity3d/Unity/Unity_lic.ulf" >/dev/null 2>&1 && echo 'licence file present' || echo 'none (needed only to launch Unity; see the doc)')"
    row dxc          "$([ -x "$HOME/dxc/bin/dxc" ] && echo "$HOME/dxc/bin/dxc present" || echo 'MISSING (python3 Tools/cloud/sc_shader_check.py --setup)')"
    local f; f="$(git -C "$REPO" ls-files '*.fbx' '*.png' 2>/dev/null | head -1)"; [ -n "$f" ] && f="$REPO/$f"
    row lfs-content  "$([ -f "$f" ] && { head -c 40 "$f" | grep -q 'git-lfs' && echo 'POINTERS (run git lfs pull)' || echo ok; } || echo 'n/a (file not found)')"
}
[ "$MODE" = check ] && { do_check; exit 0; }

# ------------------------------------------------------------------------------------------------ system packages
say "System packages"
if have apt-get; then
    step "apt update" $SUDO apt-get update -qq
    # downloads and archives; dev basics; image and video tools for review sheets and turntables
    step "apt: core tools" apt_install git git-lfs curl wget aria2 ca-certificates gnupg xz-utils zstd unzip zip p7zip-full file tree rsync jq ripgrep \
        sqlite3 build-essential pkg-config procps lsof htop
    step "apt: python" apt_install python3 python3-pip python3-venv python3-dev pipx
    step "apt: image + video" apt_install ffmpeg imagemagick webp fonts-dejavu-core
    # software graphics: Mesa llvmpipe (GL) and lavapipe (Vulkan) under xvfb, plus the X/GL runtime libraries Blender loads even in -b
    step "apt: graphics runtime" apt_install xvfb xauth mesa-utils libgl1 libgl1-mesa-dri libegl1 libgles2 libosmesa6 libglu1-mesa libvulkan1 mesa-vulkan-drivers \
        libxi6 libxrender1 libxkbcommon0 libxkbcommon-x11-0 libsm6 libice6 libxxf86vm1 libxfixes3 libxext6 libx11-6 libx11-xcb1 libxcb1
    # the Unity editor's own runtime libraries; a name that does not exist on this distro version is skipped, the rest install
    for pkg in libgtk-3-0 libgtk-3-0t64 libnss3 libgbm1 libcups2 libcups2t64 libxss1 libxcursor1 libxrandr2 libxcomposite1 libxdamage1 libxtst6 \
               libatk-bridge2.0-0 libatk-bridge2.0-0t64 libcurl4 libcurl4t64 libssl3 libssl3t64 libpulse0 libasound2 libasound2t64 libicu74 libglib2.0-0 libglib2.0-0t64; do
        apt-cache show "$pkg" >/dev/null 2>&1 && apt_install "$pkg" >/dev/null 2>&1 || true
    done
else
    echo "no apt-get on this image: install the packages above by hand" >&2
    FAILED+=("no apt-get")
fi

# ------------------------------------------------------------------------------------------------ installing more later: no friction
say "Install defaults"
install_defaults() {
    # pip installs into the system Python without a venv dance (Ubuntu 24.04 blocks it by default: PEP 668)
    $SUDO mkdir -p /etc/pip && printf '[global]\nbreak-system-packages = true\nroot-user-action = ignore\ndisable-pip-version-check = true\n' | $SUDO tee /etc/pip.conf >/dev/null
    # PATH for every shell, login or not; the agent's Bash tool may not read .bashrc, so binaries are also linked into /usr/local/bin below
    printf 'export PATH="$HOME/.local/bin:$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"\nexport DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"\nexport DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1\n' \
        | $SUDO tee /etc/profile.d/sc-paths.sh >/dev/null
    grep -q sc-paths "$HOME/.bashrc" 2>/dev/null || echo '[ -f /etc/profile.d/sc-paths.sh ] && . /etc/profile.d/sc-paths.sh' >> "$HOME/.bashrc"
    mkdir -p "$HOME/.local/bin" "$HOME/apps"
}
step "pip.conf, PATH" install_defaults

# ------------------------------------------------------------------------------------------------ git lfs (global hooks; the repo's SessionStart hook pulls the content)
say "Git LFS"
have git-lfs && step "git lfs install" git lfs install --skip-repo

# ------------------------------------------------------------------------------------------------ python libraries
say "Python libraries"
have python3 && step "pip ${PY_LIBS[*]}" python3 -m pip install --quiet --upgrade "${PY_LIBS[@]}"

# ------------------------------------------------------------------------------------------------ uv
say "uv"
install_uv() { have uv || curl -LsSf https://astral.sh/uv/install.sh | env UV_INSTALL_DIR="$HOME/.local/bin" sh >/dev/null 2>&1; have uv; }
step "uv" install_uv

# ------------------------------------------------------------------------------------------------ blender
say "Blender $BLENDER_VER"
install_blender() {
    if [ ! -x "$BLENDER_DIR/blender" ]; then
        curl -fsSL --retry 4 "$BLENDER_URL" | tar -xJ -C "$HOME/apps" || return 1
    fi
    ln -sf "$BLENDER_DIR/blender" "$HOME/.local/bin/blender"
    "$BLENDER_DIR/blender" -b --factory-startup --python-expr "import bpy; print('BLENDER_OK', bpy.app.version_string)" 2>&1 | grep -q BLENDER_OK
}
step "blender $BLENDER_VER" install_blender
blender_py_libs() { # Blender ships its own Python with numpy; the art tools also import PIL inside Blender
    local bpy; bpy="$(ls "$BLENDER_DIR/$BLENDER_SERIES/python/bin/python3."* 2>/dev/null | head -1)"
    [ -n "$bpy" ] || return 1
    "$bpy" -c 'import PIL, scipy' 2>/dev/null && return 0
    "$bpy" -m ensurepip --upgrade >/dev/null 2>&1
    "$bpy" -m pip install --quiet "${BLENDER_PY_LIBS[@]}"
}
[ -x "$BLENDER_DIR/blender" ] && step "blender python: ${BLENDER_PY_LIBS[*]}" blender_py_libs

# ------------------------------------------------------------------------------------------------ dotnet 8 (+ csharp-ls, pwsh)
say ".NET 8"
install_dotnet() {
    have dotnet && dotnet --list-sdks 2>/dev/null | grep -q '^8\.' && return 0
    if have apt-get && apt_install dotnet-sdk-8.0 2>/dev/null; then return 0; fi
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$STATE/dotnet-install.sh" || return 1   # fallback: Microsoft's installer into ~/.dotnet
    bash "$STATE/dotnet-install.sh" --channel 8.0 --install-dir "$HOME/.dotnet" >/dev/null || return 1
    ln -sf "$HOME/.dotnet/dotnet" "$HOME/.local/bin/dotnet"
}
step "dotnet 8" install_dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet_tool() { # dotnet_tool PACKAGE VERSION COMMAND   (pinned: the latest csharp-ls and PowerShell need a newer SDK than 8)
    have "$3" && return 0
    dotnet tool install --global "$1" --version "$2" >/dev/null 2>&1 || dotnet tool update --global "$1" --version "$2" >/dev/null 2>&1
    [ -x "$HOME/.dotnet/tools/$3" ]
}
if have dotnet; then
    step "csharp-ls 0.16.0" dotnet_tool csharp-ls 0.16.0 csharp-ls
    step "pwsh 7.4.6" dotnet_tool PowerShell 7.4.6 pwsh
    warm_nuget() { # sc_core_tests.py restores NUnit/NUnitLite 3.14 and Newtonsoft.Json 13.0.2 (what Unity's newtonsoft-json 3.2 bundles) on first use; fetch them now
        local d="$STATE/nuget-warm"; mkdir -p "$d"
        printf '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include="NUnit" Version="3.14.0" /><PackageReference Include="NUnitLite" Version="3.14.0" /><PackageReference Include="Newtonsoft.Json" Version="13.0.2" /></ItemGroup></Project>' > "$d/warm.csproj"
        dotnet restore "$d/warm.csproj" >/dev/null
    }
    step "nuget warm (NUnit 3.14, Newtonsoft 13.0.2)" warm_nuget
fi

# ------------------------------------------------------------------------------------------------ links on PATH for non-login shells
say "PATH links"
link_bins() {
    local b src
    for b in blender dotnet uv uvx csharp-ls pwsh; do
        src="$(command -v "$b" 2>/dev/null || true)"
        [ -n "$src" ] && [ "$src" != "/usr/local/bin/$b" ] && $SUDO ln -sf "$src" "/usr/local/bin/$b"
    done
    return 0
}
step "link into /usr/local/bin" link_bins

# ------------------------------------------------------------------------------------------------ unity editor + packages + dxc
UNITY_JOB="python3 '$REPO/Tools/cloud/sc_cloud_compile.py' --setup --project '$REPO' && python3 '$REPO/Tools/cloud/sc_shader_check.py' --setup"
unity_job() { # runs in the foreground or under setsid; writes the status file either way
    echo RUNNING > "$STATE/unity.status"
    if bash -c "$UNITY_JOB" > "$STATE/unity.log" 2>&1; then echo DONE > "$STATE/unity.status"; else echo FAILED > "$STATE/unity.status"; return 1; fi
}
if [ "$MODE" = full ] || [ "$MODE" = wait ]; then
    say "Unity $(sed -n 's/^UNITY_VERSION = "\(.*\)"/\1/p' "$REPO/Tools/cloud/sc_cloud_compile.py" 2>/dev/null) editor, packages and DXC (4.2 GB download, ~9 GB unpacked)"
    if [ ! -f "$REPO/Tools/cloud/sc_cloud_compile.py" ]; then
        echo "repo not found at $REPO: run 'bash Tools/cloud/setup.sh' from inside it" >&2; FAILED+=("unity (repo not found)")
    elif [ -x "$HOME/unity/ed/Editor/Unity" ] && [ -x "$HOME/dxc/bin/dxc" ] && [ "$(unity_status)" = DONE ]; then
        echo "ok: already installed"
    elif unity_running; then
        if [ "$MODE" = wait ]; then
            echo "waiting for the background run (log $STATE/unity.log)"
            while unity_running; do sleep 15; done
            [ "$(unity_status)" = DONE ] && echo "ok: unity" || { echo "FAILED: unity (see $STATE/unity.log)" >&2; FAILED+=("unity"); }
        else
            echo "already running in the background (log $STATE/unity.log)"
        fi
    elif [ "$MODE" = wait ]; then
        step "unity editor + packages + dxc" unity_job
    else
        export -f unity_job; export STATE UNITY_JOB
        setsid nohup bash -c unity_job >/dev/null 2>&1 < /dev/null &
        echo $! > "$STATE/unity.pid"
        echo "started in the background (pid $!): 'bash Tools/cloud/setup.sh --wait' blocks until it is done; progress in $STATE/unity.log"
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
