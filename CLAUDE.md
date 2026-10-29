# Sleeping Calamity — project instructions

Action RPG in the Dark Cloud tradition: dive into dungeons (the Hearthdeep), bring back pieces to rebuild ruined towns, grow weapons by using them, calm side activities between dives. Solo, or co-op for 2-4 players with difficulty scaling (rules below). Original IP: a spiritual successor, never a remake. Unity 6000.6.5f1 (URP 17.6), Netcode for GameObjects 2.13, Unity Multiplayer Services (Relay + Lobby join codes), Input System, Cinemachine, ProBuilder.

Sources of truth: `docs/STORY.md` (once locked), `docs/PHASE1.md` (once written), and the founding handoff `docs/superpowers/handoff/2026-10-29-new-project-handoff.md` (its "User decisions" list is settled; the rest is recommendation). Don't re-litigate agreed items. Ideas for later go in `docs/PARKING_LOT.md` with a date.

## Settled design rules
- **Players and saves (user, 2026-10-29):** a world is either solo or multiplayer, never both. A multiplayer world needs 2+ players to start and records up to 4 unique players; once 4 are recorded, nobody else can join it (a fifth friend starts a new world with three of them). Any subset of the recorded players can play it, and difficulty scales to the players present.
- **IP:** none of Dark Cloud's names, characters, story, text, music or assets. Research notes in `docs/research/` are in our own words: mechanics and numbers only. Game copies, ISOs, BIOS and decompiled output stay in the scratchpad, never in the repo. Copyrighted style frames live in gitignored `Reference/private/`.
- **Art target:** Dark Cloud's whimsical shapes and town/dungeon themes rendered in the stylised realism of the 2026 Ocarina of Time remake (real PBR materials, storybook proportions, saturated day colour, heavy atmosphere). Handoff section 2.
- Quality-first performance target; ignore generic "60+ FPS on all platforms" rules from skills.

## Running handoff
The main (controller) session opens with the `sc-steward` skill, which reads `docs/superpowers/handoff/CURRENT.md`. The controller keeps it current after every meaningful step with `sc-handoff`, then commits and pushes it to `main`. Feature branches never edit it.

## Unity (Windows PC only)
- Run Unity only through the wrapper: `powershell -NoProfile -ExecutionPolicy Bypass -File C:\Dev\SleepingCalamity\Tools\unity.ps1 <compile|build|test-edit|test-play|check|exec> -OutDir <scratchpad dir>`. Exit codes: 0 PASS, 1 FAIL, 2 LOCKED, 3 TIMEOUT, 4 usage. It reads the log (exit 0 alone is not proof), adds `-dev-profile sctest` to test runs, and records a PASS marker that the merge gate checks.
- The Unity plugin's `unity-cli` / `unity run` launch commands don't apply here; the wrapper rule wins.
- `LOCKED` means the user has the editor open: ask them to close it. Never kill Unity processes and never delete `Temp/UnityLockfile`.
- PlayMode runs in different worktrees collide on the same UDP port: before `test-play` or `check`, look for another Unity process with `-testPlatform PlayMode` and wait for it.
- Editor scripts build scenes and prefabs (`SleepingCalamity.EditorTools.*`, wired into `ProjectBuilder.RunAll`); never edit `.unity`/`.prefab` YAML by hand (a hook blocks it).
- Logs and test results go in the session scratchpad, never in the repo.

## Cloud sessions (Linux)
No Unity and no Windows paths (`Tools/cloud/` has the Linux helpers). Push a feature branch, never edit `CURRENT.md`, and report which Unity tests the local run must check. A cloud session may merge into `main` itself when the merge changes nothing under `Assets/`, `Packages/` or `ProjectSettings/`, or when the PC has reported a green `check` on the exact commit.

## Code conventions
- Code under `Assets/_Project/`, one assembly per folder: Core (pure C#, no engine), Data, Runtime, Gameplay, Presentation, UI, App, Networking, Editor, Tests/EditMode, Tests/PlayMode. `ArchitectureTests` enforces the forbidden references (Gameplay, Presentation and UI never touch Unity Services or transports).
- Pure C# logic (damage formula, weapon growth, floor generation, town scoring) lives in Core with no MonoBehaviour dependency so EditMode tests cover it.
- Host-authoritative networking; solo runs the same code as a local host. Every networked system states its authority (host vs owner) in a class-level comment. Netcode pitfalls: `sc-netcode`.
- No anti-cheat (user): modded clients and trainers are welcome. Don't add cheat-proofing or report client exploits.
- FishGame (`C:\Dev\FishGame`) is a frozen parts bin: copy from it (handoff section 4), never move or edit it. Rename `FishGame.*` to `SleepingCalamity.*`, strip fishing-specific references, and get each assembly compiling with tests green before taking the next.
- Navigate C# with the LSP tool before reading whole files. In a worktree with no `.csproj`/`.slnx`, run `Tools/sync-csproj.ps1` first (never commit its output).
- Art and asset rules live in `.claude/rules/art-assets.md`. The asset skills (`sc-asset-craft`, `-budget`, `-review`, `-adopt`, `sc-texture-paint`, `sc-foliage`, `sc-scene-look`, `sc-water-look`, `sc-reference`) came from FishGame and still hold rules for its painterly Riverlight style; rewrite those rules from the style bible (Phase 1 plan) before using them for art. Their audit, preview and review protocol stays.

## Git
- Commit and push to `main` as each piece lands (user, 2026-10-29). Never force-push. Bigger features may use a branch; merge it yourself once reviewed and `check` is green on the tip (no check needed when nothing under `Assets/`, `Packages/` or `ProjectSettings/` changes). Merge commits, never squash. No pull requests. Skill `sc-merge`.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.vscode/`, `*.csproj`, `*.slnx`. Binaries (`.blend`, FBX, PNG, JPG) go through Git LFS (`.gitattributes`).

## Agents and review
- Delegate with `sc-delegate` (brief template and routing). Roles, models and effort are pinned in `.claude/agents/`; don't set `CLAUDE_CODE_EFFORT_LEVEL`.
- Review scaled to risk: `risk-reviewer` (Opus) for networking, host sync and save data; `branch-reviewer` before a big merge; `reviewer` (Sonnet) otherwise; a fix of 30 lines or fewer gets the controller's own diff read. Reviewers follow `REVIEW.md`.
- Plan big features in a short design session with the user before code. Multi-agent workflows only with the user's go-ahead and a stated agent count.
- Long research reads, Unity runs and walks go to subagents that return short summaries; the controller keeps its own context small. Walk recipes: `sc-walk`.
- `.claude/settings.json` turns off unused plugins and MCP servers. REA is on (Phase 0 research). Blender stays denied except in art sessions: deny lists merge across settings files, so drop `blender`/`Blender` from `deniedMcpServers` in that session's own checkout and don't commit the edit.

# Compact instructions
When compacting, keep: the current branch and worktree, files modified, live agent IDs and what each is doing, the last `check`/test result with its commit, open review findings, and the next step.
