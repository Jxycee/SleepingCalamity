# SleepingCalamity review guide

Used by the `reviewer`, `risk-reviewer` and `branch-reviewer` agents and by `/code-review`.

## What to look for
- **Correctness against the brief and the design docs** (`docs/STORY.md`, `docs/PHASE1.md` and the brief's own spec): wrong behaviour, missed acceptance cases, broken contracts between assemblies.
- **Networking:** host authority stated and respected; state changed only where the class comment says; late joiners and disconnects mid-action; RPC ordering; anything that desyncs peers.
- **Save data:** data loss, schema compatibility, and exceptions escaping into gameplay (the save service must never throw into gameplay).
- **Who the world belongs to:** anything written into a world for good (a recorded player, a rebind) happens when the player plays, has an undo, and survives a changed player id. A disconnect handler that frees something by player id checks for a newer connection of the same player first (audit 2026-11-12, A1 to A3).
- **A world that cannot be saved:** a read-only store or a failed open must be visible to the player before play, never a silent session with nothing written (A5, A7).
- **Tests:** the tests check the behaviour, not the implementation; nothing skipped, deleted or `[Ignore]`d to get green; Core logic is covered in EditMode.

## What not to report
- Cheat-proofing or client exploits (no anti-cheat, by user decision).
- Style a formatter or `ArchitectureTests` already enforces.
- Design changes the spec already settled.

## How to report
- Report every finding you have, each with a severity (blocking / should-fix / nit) and a confidence (high / medium / low). The controller filters; don't drop findings yourself.
- Each finding: `file:line`, what goes wrong, and a concrete path from a real caller or input to the failure. If you can't trace one, mark it low confidence.
- A sound change with no findings is a valid result; say so plainly rather than hunting for something.
- Say which tests the local Unity run must pass to confirm the change.

## Rounds
At most three fix-and-review rounds per task. If findings keep coming after the third, stop and tell the controller the approach needs rethinking rather than patching again.
