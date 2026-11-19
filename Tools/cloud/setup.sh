#!/usr/bin/env bash
# SleepingCalamity cloud environment setup: gives a Claude Code cloud session (Ubuntu 24.04, root, no GPU) every tool this project's cloud work uses,
# and leaves the machine set up so more can be installed with one command (apt, pip, uv, npm, dotnet tool).
#
# SELF-CONTAINED: paste this whole file into the environment's setup script (cloud environment menu > Edit > Setup script); it needs no other repo
# file, and finds the repo itself (it starts one directory above it). Inside a session, `bash Tools/cloud/setup.sh [flag]` runs the same thing.
# It is idempotent (every step checks first) and safe to re-run. It always exits 0 so one bad step never blocks the session; failed steps are listed
# at the end and `bash Tools/cloud/setup.sh --check` shows the state at any time. Keep it in step with UNITY_* in sc_cloud_compile.py and DXC_URL
# in sc_shader_check.py.
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
#   csharp-ls, pwsh    the csharp-lsp plugin's server; PowerShell to read and dry-run Tools/*.ps1. Cloud Claude Code never offers the LSP tool
#                      (tested 2026-10-30: plugin installed and loaded, no tool, server never started), so sessions query csharp-ls through
#                      Tools/cloud/sc_lsp.py instead (def / refs / hover / symbols); the SessionStart hook tells each session so
#   uv / uvx           Python tools on demand (`uvx <tool>`, `uv tool install <tool>`)
#   Unity 6000.6.8f1   ~/unity/ed/Editor/Unity (Linux) + the registry packages + DXC: Tools/cloud/unity.sh (batchmode twin of Tools/unity.ps1, needs a
#                      licence: UNITY_EMAIL + UNITY_PASSWORD secrets, see the doc), sc_cloud_compile.py (C# compile, no licence) and sc_shader_check.py
#
#   blender MCP        Tools/cloud/sc_blender_mcp.py (a live, windowed Blender under xvfb) registered as the user-scope `blender` MCP server. The repo's
#                      deniedMcpServers keeps it off, as on the PC, except with SC_PROFILE=art, which lifts the deny in this checkout only
#   .csproj + .sln     sc_cloud_compile.py --csproj once Unity is in, for the csharp-lsp plugin (the SessionStart hook refreshes them)
#
# NOT provisioned here (needs the user's PC): the Unity MCP and a windowed Unity editor, AssetRipper/REA, a real GPU.
# Web search and web fetch are Claude Code tools, not packages: they need nothing here beyond the environment's network access (Full, or Trusted plus
# the domains in the doc).

# Profile: "dev" for normal sessions; "art" for the "SleepingCalamity Art" environment (live Blender MCP on). Setup scripts can't read the
# environment's variables, so an art environment's pasted copy changes this line.
SC_PROFILE="${SC_PROFILE:-dev}"

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
UNITY_VERSION=6000.6.8f1
UNITY_REVISION=237b6593792b
UNITY_URL="https://download.unity3d.com/download_unity/$UNITY_REVISION/LinuxEditorInstaller/Unity-$UNITY_VERSION.tar.xz"
DXC_URL="https://github.com/microsoft/DirectXShaderCompiler/releases/download/v1.8.2407/linux_dxc_2024_07_31.x86_64.tar.gz"
find_repo() { # run from the repo, from Tools/cloud, or pasted as the environment's setup script (which starts one directory above the repo)
    local d
    for d in "${CLAUDE_PROJECT_DIR:-}" "$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/../.." 2>/dev/null && pwd)" "$PWD" "$PWD/SleepingCalamity" /home/user/SleepingCalamity "$PWD"/*; do
        [ -n "$d" ] && [ -f "$d/Packages/packages-lock.json" ] && [ -d "$d/Assets/_Project" ] && { echo "$d"; return; }
    done
    echo "$PWD"
}
REPO="$(find_repo)"
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
    row csharp-ls    "$(have csharp-ls && echo 'present (no LSP tool in the cloud: use python3 Tools/cloud/sc_lsp.py)' || echo MISSING)"
    row pwsh         "$(have pwsh && pwsh -NoProfile -Command '$PSVersionTable.PSVersion.ToString()' 2>/dev/null || echo MISSING)"
    row uv           "$(have uv && uv --version || echo MISSING)"
    row node         "$(have node && node --version || echo MISSING)"
    row ffmpeg       "$(have ffmpeg && ffmpeg -version 2>/dev/null | head -1 | cut -d' ' -f1-3 || echo MISSING)"
    row magick       "$(have convert && convert -version 2>/dev/null | head -1 | cut -d' ' -f2-3 || echo MISSING)"
    row gl/vulkan    "$(ls /usr/lib/x86_64-linux-gnu/dri/swrast_dri.so >/dev/null 2>&1 && echo -n 'llvmpipe ' || echo -n 'NO-llvmpipe '; ls /usr/share/vulkan/icd.d/lvp_icd*.json >/dev/null 2>&1 && echo lavapipe || echo NO-lavapipe)"
    row xvfb-run     "$(have xvfb-run && echo present || echo MISSING)"
    local ust; ust="$(unity_status)"
    row unity-editor "$([ -f "$HOME/unity/ed/.extracted" ] && echo "$HOME/unity/ed/Editor/Unity present" || echo "MISSING (background setup: $ust; log $STATE/unity.log)")"
    row unity-pkgs   "$(ls -d "$HOME/unity/pkgs/"*/ 2>/dev/null | wc -l) registry packages in ~/unity/pkgs"
    row unity-lic    "$(ls "$HOME/.local/share/unity3d/Unity/Unity_lic.ulf" >/dev/null 2>&1 && echo 'licence file present' || echo 'none yet (activated on the first unity.sh run from UNITY_EMAIL + UNITY_PASSWORD)')"
    row dxc          "$([ -x "$HOME/dxc/bin/dxc" ] && echo "$HOME/dxc/bin/dxc present" || echo 'MISSING (python3 Tools/cloud/sc_shader_check.py --setup)')"
    row csproj       "$(ls "$REPO"/*.csproj >/dev/null 2>&1 && echo "$(ls "$REPO"/*.csproj | wc -l) projects + $(ls "$REPO"/*.sln 2>/dev/null | wc -l) sln" || echo 'none yet (python3 Tools/cloud/sc_cloud_compile.py --csproj)')"
    row blender-mcp  "$( (cd "$HOME" && claude mcp get blender >/dev/null 2>&1) && echo -n 'registered' || echo -n 'not registered'; grep -qi '"blender"' "$REPO/.claude/settings.json" 2>/dev/null && echo ', denied in this checkout (dev profile)' || echo ', allowed in this checkout (art profile)')"
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

# ------------------------------------------------------------------------------------------------ live blender MCP
say "Blender MCP (profile: $SC_PROFILE)"
register_blender_mcp() { # user scope, added from $HOME: inside the repo the project's deny list refuses the add
    [ -f "$REPO/Tools/cloud/sc_blender_mcp.py" ] || { echo "Tools/cloud/sc_blender_mcp.py not on this branch yet"; return 0; }
    have claude || { echo "claude CLI not found"; return 1; }
    (cd "$HOME" && claude mcp get blender >/dev/null 2>&1) && return 0
    (cd "$HOME" && claude mcp add -s user blender -- python3 "$REPO/Tools/cloud/sc_blender_mcp.py" >/dev/null)
}
step "register blender MCP (user scope)" register_blender_mcp
art_profile() { # lift the blender deny in this checkout only; skip-worktree keeps the edit out of commits (CLAUDE.md: never commit it).
    # Same edit as Tools/cloud/sc_art_profile.sh on (inlined: this script must run pasted); merges and pulls run sc_art_profile.sh off/on around them
    local f="$REPO/.claude/settings.json"
    [ -f "$f" ] || return 1
    python3 - "$f" <<'PY' || return 1
import json, sys
p = sys.argv[1]
d = json.load(open(p))
d["deniedMcpServers"] = [e for e in d.get("deniedMcpServers", []) if e.get("serverName", "").lower() != "blender"]
json.dump(d, open(p, "w"), indent=2)
open(p, "a").write("\n")
PY
    git -C "$REPO" update-index --skip-worktree .claude/settings.json
}
echo "$SC_PROFILE" > "$STATE/profile"   # the SessionStart hook re-applies the art profile after a fresh checkout
if [ "$SC_PROFILE" = art ] && ! git -C "$REPO" ls-files -v .claude/settings.json 2>/dev/null | grep -q '^S'; then
    step "art profile: blender MCP allowed in this checkout" art_profile
fi

# ------------------------------------------------------------------------------------------------ unity editor + packages + dxc
# The same layout sc_cloud_compile.py --setup and sc_shader_check.py --setup produce (~/unity/ed/Editor, ~/unity/pkgs/<name>/package + .version, ~/dxc),
# so those scripts see it as done. Written to a file so it can run detached from this script.
write_unity_job() { # to a temp file, then mv: bash reads a running job's script as it goes, so it is never rewritten in place
    cat > "$STATE/unity-job.sh.new" <<JOB
#!/usr/bin/env bash
set -euo pipefail
echo RUNNING > "$STATE/unity.status"
trap 'echo FAILED > "$STATE/unity.status"' ERR
mkdir -p "\$HOME/unity/ed" "\$HOME/dxc"
if [ ! -f "\$HOME/unity/ed/.extracted" ]; then   # not Editor/Unity: tar writes that binary long before the rest
    tarball="\$HOME/unity/Unity-$UNITY_VERSION.tar.xz"
    if [ ! -f "\$tarball" ]; then
        echo "downloading $UNITY_URL"
        curl -fL --retry 4 -C - --no-progress-meter -o "\$tarball.part" "$UNITY_URL" \
            || { rm -f "\$tarball.part"; curl -fL --retry 4 --no-progress-meter -o "\$tarball.part" "$UNITY_URL"; }   # a finished .part answers 416
        mv "\$tarball.part" "\$tarball"
    fi
    echo "extracting the editor (about 10 minutes)"
    tar -xJf "\$tarball" -C "\$HOME/unity/ed" --exclude='*/Documentation/*'
    touch "\$HOME/unity/ed/.extracted"
    rm -f "\$tarball"   # 4.2 GB the session's disk allowance needs back
fi
if [ ! -x "\$HOME/dxc/bin/dxc" ]; then
    echo "downloading DXC"
    curl -fsSL --retry 4 "$DXC_URL" | tar -xz -C "\$HOME/dxc"
fi
if [ -f "$REPO/Packages/packages-lock.json" ]; then
    python3 - "$REPO/Packages/packages-lock.json" <<'PY'
import json, os, shutil, subprocess, sys
pkgs = os.path.expanduser("~/unity/pkgs")
for name, info in json.load(open(sys.argv[1]))["dependencies"].items():
    if info.get("source") != "registry":
        continue
    dest, ver = os.path.join(pkgs, name), info["version"]
    stamp = os.path.join(dest, ".version")
    if os.path.exists(stamp) and open(stamp).read() == ver:
        continue
    url = info.get("url", "https://packages.unity.com")
    meta = json.loads(subprocess.run(["curl", "-sSf", "--retry", "4", f"{url}/{name}"], capture_output=True, text=True, check=True).stdout)
    tar = meta["versions"][ver]["dist"]["tarball"]
    shutil.rmtree(dest, ignore_errors=True)
    os.makedirs(dest)
    subprocess.run(f"curl -sSfL --retry 4 '{tar}' | tar -xz -C '{dest}'", shell=True, check=True)
    open(stamp, "w").write(ver)
    print(f"package {name} {ver}")
PY
else
    echo "no Packages/packages-lock.json found: registry packages skipped (python3 Tools/cloud/sc_cloud_compile.py --setup fetches them)"
fi
if [ -f "$REPO/Tools/cloud/sc_cloud_compile.py" ]; then
    echo "writing .csproj files for the C# LSP"
    python3 "$REPO/Tools/cloud/sc_cloud_compile.py" --project "$REPO" --csproj || echo "csproj: compile errors on this commit (projects still written for the assemblies that built)"
fi
echo "unity setup done"
echo DONE > "$STATE/unity.status"
JOB
    mv "$STATE/unity-job.sh.new" "$STATE/unity-job.sh"
}
if [ "$MODE" = full ] || [ "$MODE" = wait ]; then
    say "Unity $UNITY_VERSION editor, registry packages and DXC (4.2 GB download, ~9 GB unpacked)"
    unity_running || write_unity_job
    if [ -f "$HOME/unity/ed/.extracted" ] && [ -x "$HOME/dxc/bin/dxc" ] && [ "$(unity_status)" = DONE ]; then
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
        step "unity editor + packages + dxc" bash -c "bash '$STATE/unity-job.sh' > '$STATE/unity.log' 2>&1"
    else
        echo RUNNING > "$STATE/unity.status"   # before launch, so a second run in the gap sees it
        setsid nohup bash "$STATE/unity-job.sh" > "$STATE/unity.log" 2>&1 < /dev/null &
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
