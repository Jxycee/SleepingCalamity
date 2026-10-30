# Cloud environment (Claude Code on the web)

How a cloud session gets the same toolchain the PC has, minus the live editors. The script is `Tools/cloud/setup.sh`; this page is what to paste into the environment settings and what you get.

## 1. Environment settings (one time, by the user)

Cloud environment menu in the session's title bar > **Edit**.

**Setup script**: paste the whole of `Tools/cloud/setup.sh`. It is self-contained (needs no other repo file and finds the repo itself), so the pasted copy works on any branch. After changing the script in the repo, paste it again.

**Network access**: **Full**. Everything the script and the art tools download (Blender, Unity, Unity's package registry, Microsoft, NuGet, PyPI, GitHub releases, Ubuntu mirrors, Poly Haven, ambientCG, Hugging Face) plus anything a later session decides to install. If you prefer a narrower level, allow at least: `download.blender.org`, `download.unity3d.com`, `packages.unity.com`, `dot.net`, `builds.dotnet.microsoft.com`, `api.nuget.org`, `astral.sh`, `github.com`, `objects.githubusercontent.com`, `release-assets.githubusercontent.com`, `polyhaven.com`, `dl.polyhaven.org`, `ambientcg.com`, `huggingface.co`, with "Allow package managers" ticked.

**Environment variables** (only needed to *launch* Unity; compile and shader checks work without):

| Variable | What to put in it |
|---|---|
| `UNITY_LICENSE` | Personal licence: activate in Unity Hub on the PC, then paste the whole contents of `C:\ProgramData\Unity\Unity_lic.ulf` (XML, or base64 of it). |
| `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD` | Plus/Pro serial instead. Each new container takes a seat: `bash Tools/cloud/sc_unity_license.sh --return` before the session ends, or free seats at id.unity.com. |

A `.ulf` is tied to the machine it came from. Unity usually accepts a Personal one in a container (CI services rely on this), but if `unity.sh` logs exit 198 / "No valid Unity Editor license found", the licence was refused.

## 2. What a session gets

First session: about 7 minutes for everything except Unity, then the Unity editor (4.2 GB download, about 9 GB unpacked) finishes in the background about 5 minutes later. `bash Tools/cloud/setup.sh --check` shows the state; `--wait` blocks until Unity is ready, and restarts the download if the background run was cut off.

| Tool | Where | Used by |
|---|---|---|
| Blender 4.5.3 (+ Pillow, SciPy in its own Python) | `blender` on PATH, `~/apps/blender-4.5.3-linux-x64` | every `Art/Blender/tools/sc_*.py` generator and review render, headless: `blender -b --factory-startup --python ...` |
| Mesa llvmpipe (GL 4.5), lavapipe (Vulkan), xvfb | system | Cycles renders on CPU directly; EEVEE needs `xvfb-run -a blender -b ...` (first shader compile about 75 s); `unity.sh --graphics` runs under `xvfb-run` |
| Unity 6000.6.5f1 Linux editor + registry packages | `~/unity/ed/Editor/Unity`, `~/unity/pkgs` | `Tools/cloud/unity.sh` (batchmode twin of `unity.ps1`, needs a licence), `sc_cloud_compile.py` (C# compile, no licence) |
| DXC | `~/dxc/bin/dxc` | `sc_shader_check.py` (every shader pass) |
| .NET 8 SDK, NuGet warm for NUnit 3.14 and Newtonsoft.Json 13.0.2 | `dotnet` | `sc_core_tests.py` (Core EditMode tests under NUnitLite) |
| csharp-ls 0.16.0 | `csharp-ls` | the csharp-lsp plugin, once a checkout has `.csproj` files (see section 4) |
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

- Unity merge gate: `check` on the PC stays the gate unless `unity.sh check` passes here with a working licence on the exact commit.
- Live Blender MCP and Unity MCP (they drive a running editor with a UI), AssetRipper/REA, a real GPU (cloud renders are CPU-only and slower).
- `.csproj` files for the LSP: `Tools/sync-csproj.ps1` copies the ones Unity generates on the PC. The cloud has none yet; generating them (from `sc_cloud_compile.py`'s asmdef resolution, or a licensed Unity run) is parked work.
