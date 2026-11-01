# Session requests

One file here starts one cloud working session through the launcher routine "SC session launcher (cloud)" (`trig_01Rj58EHgmhr5QziTTwy2zee`, environment SleepingCalamity Cloud, Sonnet 5.5). Launch paths:

- **A session handing off at 90-95% context** writes its successor's request here, pushes it to main, then runs the launcher with `RemoteTrigger {action: "run", trigger_id: "trig_01Rj58EHgmhr5QziTTwy2zee"}`. If the session doesn't have the RemoteTrigger tool, it asks the coordinator to run it with one board `report` line ("handoff request <file> pushed").
- **The user** can push a file from anywhere: GitHub web, the phone app, or git. The coordinator sees it on main and runs the launcher, or the user presses Run on the routine at https://claude.ai/code/routines.

The launcher takes the oldest `pending` cloud request, marks it `started`, becomes that session, and sets `done` or `blocked` at the end. Name files `YYYY-MM-DD-<short-slug>.md`.

## Template

```
Title: <short session title>
Target: cloud
Status: pending
Model: sonnet
From: <who asked: the user, or the session handing off and its branch>
Status file: Reference/sessions/<slug>/STATUS.md

Brief:
<The task in your own words: files and area, what done looks like, rules. For a handoff: the branch and tip,
what is merged, what is open, the last check result with its commit, the next steps in order, and any pending
decisions. The new session starts with no context, so include everything it needs.>
```
