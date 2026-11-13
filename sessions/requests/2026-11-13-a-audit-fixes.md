Title: Audit fixes: world players, rejoin, unsaved worlds
Target: cloud
Status: started
Started: 2026-11-13T13:23:56Z (cloud launcher)
Model: sonnet
From: coordinator session 11 on the user's go (2026-11-13, "Cloud")
Status file: Reference/sessions/audit-fixes/STATUS.md

Brief:
Title: Audit fixes (A1, A2, A3, A5 + A9)
Status file: Reference/sessions/audit-fixes/STATUS.md

Goal: fix the audit's two blockers and the two user-decided should-fixes so multiplayer worlds keep their four places for real players and no session is silently unsaved. Read docs/superpowers/audits/2026-11-12-save-netcode-audit.md sections A1, A2, A3, A5, A9 and section 6 (risk review); they give file:line, failure paths and test names. Audit merged 33b0a364; decisions in CURRENT.md Now (abc19849).

Decisions (user, 2026-11-13):
- A2: record a joiner only when the session starts (the start gate records connected players before `CanStart` checks them, `WorldPlayers.cs:178`) or, for a join into a running session, when their hero spawns; AND a host "remove player" action in the world picker (front door, with a confirm step). The first lobby of a new world is covered too (`WorldLibrary.Create:76`). Update the three tests that pin today's behaviour (named in A2).
- A3: host-side "this player is now that player" action that renames one multiplayer record, moving weapons, bags and suspend; reuse the Solo rebind code (`HostWorld.cs:258-268`).
- A5: a world that cannot be saved (newer file or load fault) asks "This world can't be saved. Play anyway?" in the front door and starts only on yes.
- A1 (+A9): a takeover disconnect of the same key must not free the rejoiner's seat; add the pure seam the audit proposes and un-ignore its test. The live Relay check stays the user's (USER_TODO item 3a).

Files you OWN: Networking/ConnectionApprover.cs, Networking/UgsNetworkSession*.cs, Runtime/Net/HostWorld.cs, HostRoster.cs, Core/World/WorldPlayers.cs, WorldLibrary, App/WorldSaveFlow.cs, Runtime/Save/*, App/Menus/FrontDoorModel.cs, FrontDoorView.cs and the front-door .uxml/.uss, Core/Game/UiText.cs, their tests.
NOT yours (PL-4 owns): new pause-menu files, App/SceneFlow.cs, Presentation/Cellars/DiveDirector.cs, Core/Input/*, Runtime/Input/*, Editor/Builders/*, Editor/Dev/PlayerBuild.cs, docs/superpowers/handoff/USER_TODO.md. Need one? Message the coordinator.

Delegate to `implementer` (effort high), two runs: A1+A9+A2 (connection and recording), then A3+A5 (save and front door). Each test fails first; un-ignore the audit's AUDIT tests. Reviews: `risk-reviewer` once on the whole diff, `branch-reviewer` (Opus high) before merge; findings back to the same implementer. Read `sc-netcode` first. No anti-cheat: host authority is for sync and save integrity only.
Out of scope: other should-fixes (A4, A6-A8, A10, A11) unless under 30 lines and in your files; CURRENT.md.
Branch: `claude/<slug>` from origin/main; `git lfs checkout`, `python3 Tools/cloud/sc_cloud_compile.py --csproj` (never commit its output). Check: `bash Tools/cloud/unity.sh gate --out <scratchpad>` on pushes, `check` on the tip (SC MARKER = HEAD). Cloud session: wait for the editor with `bash Tools/cloud/setup.sh --wait` first; long runs in background, no polling. Don't pause for the 5-hour pace.
Merge (sc-merge, merge commit; if PL-4 merged first, merge origin/main in, builder-output conflicts take main's side + one build, then check): pre-approved once branch-reviewer says MERGE-OK and check is green; tick the audit's queue items in the merge commit.
Context: hand off at ~250k tokens at a task boundary.
Report (STATUS + board profile): tests added, check line, merge commit, open findings. Done when merged and STATUS says `Archive: ready`.

Cloud notes: you run in the cloud (Linux, no GPU); a coordinator on the PC watches the board. Branch from origin/main, push your branch; `unity.sh check` records the merge marker. Never commit Linux float churn in .unity/.mat (cloud-environment.md). Report through STATUS and the board profile.
