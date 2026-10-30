# Cloud environment (Claude Code on the web)

How a cloud session gets the PC's toolchain and works like a PC session (user, 2026-10-30: controller, `CURRENT.md`, push to `main`, merge on its own green `unity.sh check`; CLAUDE.md has the PC-to-cloud command map). The script is `Tools/cloud/setup.sh`; this page is what to paste into the environment settings and what you get.

## 1. Environment settings (one time, by the user)

Cloud environment menu in the session's title bar > **Edit**.

**Setup script**: paste the whole of `Tools/cloud/setup.sh`. It is self-contained (needs no other repo file and finds the repo itself), so the pasted copy works on any branch. After changing the script in the repo, paste it again.

**Two environments.** Make a second environment named **SleepingCalamity Art** with the same settings, and in its pasted script change the line `SC_PROFILE="${SC_PROFILE:-dev}"` to `SC_PROFILE="${SC_PROFILE:-art}"`. Art sessions get the live Blender MCP; dev sessions keep it denied, as on the PC (CLAUDE.md). Pick the environment when you start a session. The art profile edits `.claude/settings.json` in that checkout only (hidden from git with skip-worktree), so git refuses merges and pulls that touch the file: `bash Tools/cloud/sc_art_profile.sh off` before them, `on` after (sc-merge says when; the SessionStart hook re-applies it).

**Network access**: **Full**. Everything the script and the art tools download (Blender, Unity, Unity's package registry, Microsoft, NuGet, PyPI, GitHub releases, Ubuntu mirrors, Poly Haven, ambientCG, Hugging Face) plus anything a later session decides to install. If you prefer a narrower level, allow at least: `download.blender.org`, `download.unity3d.com`, `packages.unity.com`, `dot.net`, `builds.dotnet.microsoft.com`, `api.nuget.org`, `astral.sh`, `github.com`, `objects.githubusercontent.com`, `release-assets.githubusercontent.com`, `polyhaven.com`, `dl.polyhaven.org`, `ambientcg.com`, `huggingface.co`, with "Allow package managers" ticked.

**Environment variables** (only needed to *launch* Unity; compile and shader checks work without):

| Variable | What to put in it |
|---|---|
| `UNITY_EMAIL`, `UNITY_PASSWORD` | Your Unity account. The editor's own licensing client activates each new container headlessly (Personal works; `Tools/cloud/sc_unity_license.sh`). The account must not use two-factor sign-in. Each container counts as a new machine: `bash Tools/cloud/sc_unity_license.sh --return` before a session ends keeps the activation count down. |
| `UNITY_SERIAL` | Only for a Plus/Pro seat, alongside the two above. |
| `UNITY_LICENSE` | Legacy only: the contents of a `Unity_lic.ulf`. Current Hub versions don't make one; the PC's `%LocalAppData%\Unity\licenses\UnityEntitlementLicense.xml` is bound to the PC's hardware and won't work in a container, so don't use it. |

If `unity.sh` logs exit 198 / "No valid Unity Editor license found", the activation didn't happen: `~/.sc-setup/unity-license.log` says why.

The first Unity launch in a new container imports the whole project into an empty `Library/` (about 7 minutes on 2026-10-30; later launches take 1-2 minutes). `unity.sh` sees the missing `Library/ArtifactDB` and raises its default timeout from 5 to 20 minutes for that run; an explicit `--timeout` still wins.

## 2. What a session gets

First session: about 7 minutes for everything except Unity, then the Unity editor (4.2 GB download, about 9 GB unpacked) finishes in the background about 5 minutes later. `bash Tools/cloud/setup.sh --check` shows the state; `--wait` blocks until Unity is ready, and restarts the download if the background run was cut off.

| Tool | Where | Used by |
|---|---|---|
| Blender 4.5.3 (+ Pillow, SciPy in its own Python) | `blender` on PATH, `~/apps/blender-4.5.3-linux-x64` | every `Art/Blender/tools/sc_*.py` generator and review render, headless: `blender -b --factory-startup --python ...` |
| Mesa llvmpipe (GL 4.5), lavapipe (Vulkan), xvfb | system | Cycles renders on CPU directly; EEVEE needs `xvfb-run -a blender -b ...` (first shader compile about 75 s); `unity.sh --graphics` runs under `xvfb-run` |
| Unity 6000.6.5f1 Linux editor + registry packages | `~/unity/ed/Editor/Unity`, `~/unity/pkgs` | `Tools/cloud/unity.sh` (batchmode twin of `unity.ps1`, needs a licence), `sc_cloud_compile.py` (C# compile, no licence) |
| DXC | `~/dxc/bin/dxc` | `sc_shader_check.py` (every shader pass) |
| .NET 8 SDK, NuGet warm for NUnit 3.14 and Newtonsoft.Json 13.0.2 | `dotnet` | `sc_core_tests.py` (Core EditMode tests under NUnitLite) |
| csharp-ls 0.16.0 + generated `.csproj`/`.sln` | `csharp-ls`; project root (gitignored) | the csharp-lsp plugin: `sc_cloud_compile.py --csproj` writes Unity-style projects (the setup job and the SessionStart hook run it; re-run after adding files). Tested: go-to-definition and hover across assemblies |
| Live Blender MCP (art profile) | `blender` MCP server, user scope | `Tools/cloud/sc_blender_mcp.py`: a windowed Blender under xvfb, started on the first call (about 10 s). Tools: `blender_exec`, `blender_scene`, `blender_screenshot` (whole window), `blender_viewport` (3D view only), `blender_restart`. Also a CLI: `python3 Tools/cloud/sc_blender_mcp.py exec 'CODE'`, `screenshot PATH`, `stop` |
| FishGame parts bin | `/home/user/fishgame` | not cloned by setup (a session attaches it): `add_repo` Jxycee/FishGame (read), then `git clone --depth 1 https://github.com/Jxycee/fishgame /home/user/fishgame` (1.5 GB, about 40 s). Read-only by rule |
| PowerShell 7.4 | `pwsh` | reading and dry-running `Tools/*.ps1` |
| Python 3 + numpy, pillow, pyarrow, scipy, opencv-headless, imageio, trimesh, pygltflib, requests, pyyaml, matplotlib | system | asset finder, image diffs, palettes, glTF/FBX checks outside Blender |
| ffmpeg, ImageMagick, webp, 7z, zstd, aria2, jq, ripgrep, sqlite3, build-essential | system | turntables, contact sheets, archives, downloads |
| uv / uvx, Node 22 + npm, gh, git-lfs | system | Python and JS tools on demand; LFS content is pulled by the SessionStart hook |

Web search and web fetch are Claude Code tools and need nothing installed.

## 3. Installing more

The session runs as root with network access, and the script sets things up so each of these just works (no venv, no sudo, on PATH):

```bash
apt-get install -y <pkg>             # system packages (lists are kept fresh by the setup script)
pip install <pkg>                    # system Python (/etc/pip.conf allows it)
uv tool install <pkg>  /  uvx <pkg>  # isolated Python CLIs
npm install -g <pkg>                 # Node CLIs
dotnet tool install -g <pkg> --version <v>   # pick a version that targets net8.0
~/apps/blender-4.5.3-linux-x64/4.5/python/bin/python3.11 -m pip install <pkg>   # into Blender's Python
```

Anything a session installs is gone when the container is reclaimed. To make it permanent, add it to `Tools/cloud/setup.sh` (on a branch; it changes nothing under `Assets/`, so a cloud session may merge it).

## 4. What still needs the PC

- Any Unity launch until `UNITY_EMAIL` + `UNITY_PASSWORD` are set and the activation works: build, EditMode/PlayMode tests and `check`. With it, `unity.sh check` is a full merge gate (same marker as the PC).
- The Unity MCP and `sc-walk` (they drive a windowed editor; on CPU rendering it would be too slow to use), AssetRipper/REA, `Reference/private/` (copyrighted frames never leave the PC), a real GPU (cloud renders are CPU-only and slower).
- Committed bakes and FBX exports: cloud Blender writes different bytes from the PC's, so art sessions commit generators and the PC rebakes (sc-asset-craft, sc-texture-paint).
