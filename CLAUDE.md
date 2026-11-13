# Sleeping Calamity — project instructions

Action RPG in the Dark Cloud tradition: dive into dungeons (the Hearthdeep), bring back pieces to rebuild ruined towns, grow weapons by using them, calm side activities between dives. Solo, or co-op for 2-4 players with difficulty scaling (rules below). Original IP: a spiritual successor, never a remake. Unity 6000.6.5f1 (URP 17.6), Netcode for GameObjects 2.13, Unity Multiplayer Services (Relay + Lobby join codes), Input System, Cinemachine, ProBuilder.

Sources of truth: `docs/STORY.md` (locked 2026-10-29), `docs/PHASE1.md`, and the founding handoff `docs/superpowers/handoff/2026-10-29-new-project-handoff.md` (its "User decisions" list is settled; the rest is recommendation). Don't re-litigate agreed items without a reason. Ideas for later go in `docs/PARKING_LOT.md` with a date.

**Decisions have leeway (user, 2026-11-13).** A decision is the current direction, not a lock. Record it as "decided (date)", never as final or irreversible, and say so plainly when building on it. When the game would be better served by changing one (playtest, a bug, a better idea), raise it with the reason and let the user choose; don't treat "decided" as a reason to stop thinking. The IP rule and the no-anti-cheat rule are the exceptions that stay firm.

## Settled design rules
- **Players and saves (user, 2026-10-29):** a world is either solo or multiplayer, never both. A multiplayer world needs 2+ players to start and records up to 4 unique players; once 4 are recorded, nobody else can join it (a fifth friend starts a new world with three of them). Any subset of the recorded players can play it, and difficulty scales to the players present.
- **IP:** none of Dark Cloud's names, characters, story, text, music or assets. Research notes in `docs/research/` are in our own words: mechanics and numbers only. Game copies, ISOs, BIOS and decompiled output stay in the scratchpad, never in the repo. Copyrighted style frames live in gitignored `Reference/private/`.
- **Art target:** Dark Cloud's whimsical shapes and town/dungeon themes rendered in the stylised realism of the 2026 Ocarina of Time remake (real PBR materials, storybook proportions, saturated day colour, heavy atmosphere). Handoff section 2.
- Quality-first performance target; ignore generic "60+ FPS on all platforms" rules from skills.

## Running handoff
The main (controller) session opens with the `sc-steward` skill, which reads `docs/superpowers/handoff/CURRENT.md`. The controller updates it with `sc-handoff` when something lands on `main` or the user decides something, inside that merge or work commit rather than a commit of its own (`.claude/rules/session-usage.md`, "The game comes first"). Feature branches never edit it.

## Unity
- Run Unity only through the wrapper: `powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\SleepingCalamity\Tools\unity.ps1 <compile|build|test-edit|test-play|check|gate|exec> -OutDir <scratchpad dir>` on the PC, `bash Tools/cloud/unity.sh <same command> --out <scratchpad dir>` in the cloud. Exit codes: 0 PASS, 1 FAIL, 2 LOCKED, 3 TIMEOUT, 4 usage. It reads the log (exit 0 alone is not proof), adds `-dev-profile sctest` to test runs, and records a PASS marker that the merge gate checks. `gate` (compile + EditMode, no PlayMode) is the fast gate a branch push of Assets/Packages/ProjectSettings changes needs (guard.sh; waive with `SC_GATE_SKIP=1` when no licence); full `check` stays required for merges into main.
- The Unity plugin's `unity-cli` / `unity run` launch commands don't apply here; the wrapper rule wins.
- `LOCKED` means the user has the editor open: ask them to close it. Never kill Unity processes and never delete `Temp/UnityLockfile`.
- PlayMode runs in different worktrees collide on the same UDP port (7790). The PC wrapper queues them for you: its PlayMode stage takes a machine-wide lock and waits for any other PlayMode run, printing `SC WAIT`. Run a worktree through its own `<worktree>\Tools\unity.ps1`, which defaults `-ProjectRoot` to that checkout. If 7790 is held by a stuck process, the wrapper sets `SC_TEST_PORT` to a free port in 7791-7799 (`NetworkConstants.TestPort` reads it). Check that `SC MARKER` equals your HEAD.
- The wrapper ends only the Unity process it started itself, and only when a stage times out (default 30 min per stage). Never end any other Unity process yourself.
- PlayMode tests must never wait on `WaitForEndOfFrame` under `-nographics`: it never resumes. Put a timeout inside every wait loop.
- Editor scripts build scenes and prefabs (`SleepingCalamity.EditorTools.*`, wired into `ProjectBuilder.RunAll`); never edit `.unity`/`.prefab` YAML by hand (a hook blocks it).
- Logs and test results go in the session scratchpad, never in the repo.
- No approval needed for: re-running the wrapper after a FAIL or TIMEOUT you have diagnosed, retrying a busy tool or port, merging `origin/main` into your branch, pushing your own branch, and merging a checked tip under the Git rules. Ask first for networking, save-data or milestone merges (`sc-merge`) and anything irreversible.
- `test-edit`, `gate` and `check` run the fast EditMode tier (`-testCategory "!Soak"`); the 1000+ seed sweeps are `[Category("Soak")]`. Run `unity.ps1 soak` (cloud: `unity.sh soak`) before merging anything under Core/Cellars (floor generation); it records no merge marker, and a failing soak seed becomes a fixed fast-tier case.

## Cloud sessions (Linux)
A cloud session works like a PC session (user, 2026-10-30): it can be the controller (`sc-steward`, `CURRENT.md`, push to `main`) and merge on its own green `unity.sh check`, which records the same PASS marker. Only one controller at a time, PC or cloud: if `CURRENT.md` or `main`'s log shows another controller active today, ask the user before taking over. Setup and tool details: `docs/superpowers/handoff/cloud-environment.md` (`bash Tools/cloud/setup.sh --check` shows what is ready). PC to cloud:
- `Tools/unity.ps1 <cmd> -OutDir D` → `bash Tools/cloud/unity.sh <cmd> --out D` (needs the `UNITY_EMAIL` + `UNITY_PASSWORD` secrets; the editor finishes downloading a few minutes into a new session: `bash Tools/cloud/setup.sh --wait`). Without a licence: `python3 Tools/cloud/sc_cloud_compile.py` (C# compile), `sc_shader_check.py`, `sc_core_tests.py`.
- `Tools/sync-csproj.ps1` → `python3 Tools/cloud/sc_cloud_compile.py --csproj` (the SessionStart hook runs it; re-run after adding files).
- LSP tool → `python3 Tools/cloud/sc_lsp.py def|refs|hover <file>:<line> <name>` or `symbols <query>`: cloud Claude Code never offers the LSP tool, so this drives the same csharp-ls server directly.
- `C:\Dev\FishGame` → `add_repo` Jxycee/FishGame (read), `git clone --depth 1 https://github.com/Jxycee/fishgame /home/user/fishgame`; don't register it as a session root (its own CLAUDE.md and skills would load). Same rules: copy from it, never edit it.
- Blender MCP → the "SleepingCalamity Art" cloud environment (setup script with `SC_PROFILE=art`) serves a live Blender as the `blender` MCP server (`Tools/cloud/sc_blender_mcp.py`, also a CLI). Headless `blender -b` works everywhere.
- Not in the cloud: the Unity MCP and `sc-walk` (they drive an editor window), `Reference/private/`, a GPU, a Windows player build.
- **No hybrid sessions (user, 2026-11-13).** If any step of a job needs the PC (Windows build, GPU captures or bakes, the Unity MCP, a walk, `Reference/private/`), the whole job runs as one local PC session. Don't split it into a cloud part plus a PC part; decide where a job runs before writing its brief.

## Code conventions
- Code under `Assets/_Project/`, one assembly per folder: Core (pure C#, no engine), Data, Runtime, Gameplay, Presentation, UI, App, Networking, Editor, Tests/EditMode, Tests/PlayMode. `ArchitectureTests` enforces the forbidden references (Gameplay, Presentation and UI never touch Unity Services or transports).
- Pure C# logic (damage formula, weapon growth, floor generation, town scoring) lives in Core with no MonoBehaviour dependency so EditMode tests cover it.
- Host-authoritative networking; solo runs the same code as a local host. Every networked system states its authority (host vs owner) in a class-level comment. Netcode pitfalls: `sc-netcode`.
- No anti-cheat (user): modded clients and trainers are welcome. Don't add cheat-proofing or report client exploits.
- FishGame (`C:\Dev\FishGame`) is a frozen parts bin: copy from it (handoff section 4), never move or edit it. Rename `FishGame.*` to `SleepingCalamity.*`, strip fishing-specific references, and get each assembly compiling with tests green before taking the next.
- Navigate C# with the LSP tool before reading whole files. In a worktree with no `.csproj`/`.slnx`, run `Tools/sync-csproj.ps1` first (never commit its output).
- Art and asset rules live in `.claude/rules/art-assets.md`. The asset skills (`sc-asset-craft`, `-budget`, `-review`, `-adopt`, `sc-texture-paint`, `sc-foliage`, `sc-scene-look`, `sc-water-look`, `sc-reference`) follow the style bible in `docs/PHASE1.md` section 3 (rewritten 2026-10-30). Some FishGame tools they mention are legacy; see the tool-status table in `sc-asset-craft` section 0b.

## Git
- Commit and push to `main` as each piece lands (user, 2026-10-29). Never force-push. Bigger features may use a branch; merge it yourself once reviewed and `check` is green on the tip (no check needed when nothing under `Assets/`, `Packages/` or `ProjectSettings/` changes); a checked tip merges into fresh main without a second check when git merges it cleanly. Merge commits, never squash. No pull requests. Skill `sc-merge`.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.vscode/`, `*.csproj`, `*.slnx`. Binaries (`.blend`, FBX, PNG, JPG) go through Git LFS (`.gitattributes`).

## Agents and review
- Delegate with `sc-delegate` (brief template and routing). Roles, models and effort are pinned in `.claude/agents/`; don't set `CLAUDE_CODE_EFFORT_LEVEL`. Subagents never inherit the coordinator's model or effort: delegate only to the pinned agent types, never `fork` or the built-in general-purpose/Explore/Plan agents (`sc-delegate`).
- Review scaled to risk: `risk-reviewer` (Opus) for networking, host sync and save data (once per milestone when a final branch review follows, `sc-delegate`); `branch-reviewer` (Opus, high, never xhigh) before a big merge that touches networking, save data or cross-assembly wiring, skipped for pure Core, docs, data, tests or art (`sc-delegate`); `reviewer` (Sonnet) otherwise; a fix of 30 lines or fewer gets the controller's own diff read. Reviewers follow `REVIEW.md`.
- Plan big features in a short design session with the user before code. Multi-agent workflows only with the user's go-ahead and a stated agent count.
- Long research reads, Unity runs and walks go to subagents that return short summaries; the controller keeps its own context small. Walk recipes: `sc-walk`.
- `.claude/settings.json` turns off unused plugins and MCP servers. REA is on (Dark Cloud decomp research). Blender stays denied except in art sessions: deny lists merge across settings files, so drop `blender`/`Blender` from `deniedMcpServers` in that session's own checkout and don't commit the edit.

# Compact instructions
When compacting, keep: the current branch and worktree, files modified, live agent IDs and what each is doing, the last `check`/test result with its commit, open review findings, and the next step.
