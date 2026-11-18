# T26 brief: speed and judging tooling (Area 1 world, Run 8)

Goal: same quality, faster runs, and better judging inputs. Measured over T11-T13b (568 min): BuildAll 31% (about 7 min a run, 3-11 runs per task), captures 24% (CaptureAll 5-7 min a launch, 6 launches in T13b), editor startup and import 7%, tests 5.5%. User-approved fixes (2026-11-18): fix the top two, plus the judging and intake items below. Tooling only: no art or scene-content changes, and the Hollowmere scene must come out byte-identical from the new paths vs BuildAll.

Branch `claude/area1-world`, worktree `C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725`, current tip. This brief is untracked: commit it with your work; leave `T22_brief.md`/`T23_brief.md` alone; never edit STATUS.md. Do not merge main. Scratch: `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\54c4d5da-7980-453e-9883-d2236b06f4b2\scratchpad\T26`. Do not edit `Tools/unity.ps1` (shared wrapper): new entry points run through `unity.ps1 exec -Method <Namespace.Class.Method>`.

## 1. Scoped build (biggest win)
- New exec entry point that rebuilds only Hollowmere (and what it needs), not every scene and prefab `ProjectBuilder.BuildAll` makes (`Assets/_Project/Editor/Builders/ProjectBuilder.cs:27`). Optional: `SC_BUILD_SECTIONS=<names>` to rerun only named Hollowmere builder sections when that is safe (sections that only add children after every collider, such as the T06-T13 dressing builders); if a section split cannot give the same bytes as a full Hollowmere build, drop the section option and say so.
- Prove it: scoped build then BuildAll at the same tip write the same Hollowmere.unity and asset bytes (git diff empty). Time both and report.
- BuildAll stays for the gate/merge tip.

## 2. Build plus capture in one launch
- New exec entry point: scoped build, then capture the cameras in `SC_CAPTURE_ONLY` (`HollowmereCapture.CaptureTo`, `HollowmereCapture.cs:42`, honours `SC_CAPTURE_ONLY`/`SC_CAPTURE_PRESET`/`SC_CAPTURE_SUN`) in the same editor process, skipping the second boot and asset refresh. Report the time saved on a 3-camera round.

## 3. Changed-camera picker
- Given what a task touched (root object names or name prefixes under the Hollowmere scene, or a list of builder section names), list the capture cameras whose frustum contains any of those renderers' bounds (with a distance cut, e.g. 120 m, and an occlusion-free check is fine). Output a ready `SC_CAPTURE_ONLY` string. Exec entry point with an env var input (e.g. `SC_PICK_ROOTS=BankSteps,GroundProps`); log line `SC PICK <cameras>`.

## 4. Judging capture extras
- Frames at 1920x1080 (check what CaptureTo writes now; make 1920x1080 the eye-camera default if it is not).
- For each eye frame, two 1:1 crops (no scaling) saved beside it: a wall foot and a ground patch (fixed per camera from a small table, or picked by depth/normal: wall-foot = where a near-vertical surface meets ground in the lower half; ground = a lower-third ground patch). `_crop_wall.jpg`, `_crop_ground.jpg`.
- Optional switch `SC_CAPTURE_MOTION=1`: 4 frames over 2 s of game time for the named cameras (life line: birds, smoke, cloth), saved as a strip `_motion.jpg`.
- Optional switch for milestones: 2-3 random-position eye captures (seeded, on walkable ground inside the wall, eye height 1.65, level gaze) and dusk/overcast variants of named cameras, if the capture code already has sun presets (check `SC_CAPTURE_SUN`).

## 5. Intake 4K opt-in
- `B1TownIntake.cs:23` `const int Cap = 2048` and `B0Fab.cs:147` maxTextureSize 2048: add a per-asset 4K opt-in (a list of asset codes, default empty), 2K stays the default; log each 4K use (`SC INTAKE 4K <code>`).

## 6. Asset audit
- `Art/Blender/tools/sc_asset_audit.py`: triangle overage becomes WARN (print the tris and, if passed, the per-camera cost) instead of FAIL; material-slot FAIL stays. Keep its CLI.

## Check
compile, gate (`SC MARKER` = HEAD) and one BuildAll at the end to show Hollowmere.unity unchanged against the tip you started from (empty diff on `Assets/_Project/Scenes/Hollowmere.unity` and `Content/Generated/Hollowmere/`). No play loop needed (no ground or collider change). Revert known churn (STATUS "Known churn", B9 `.fbx.meta` reimports), commit by path, push.

## Report (under 15 lines)
Commits; gate line; the new entry points and their exact `unity.ps1 exec -Method ...` lines with env vars; timings (BuildAll vs scoped build; build+capture one launch vs two); byte-identity proof; picker output for `SC_PICK_ROOTS=BankSteps` as an example; anything dropped and why.
