# Map trailer render

A repeatable ~75 s, 30 fps, 1920x1080 smooth cinematic fly-through, each place shown once: Hollowmere square (Ruined), feathered wipe to Rebuilt (same flight, flown back), Keep Lane glide to the heroes' home, door and lit interior, the Undercroft stair descent (UndercroftStair scene), then the Cellars test floor (seed 12345, floor 1). Code: `Assets/_Project/Editor/Builders/TrailerRender.cs` (route = shot tables `SquareShot` / `HomeShot` / `StairShot` at the top; each shot is a waypoint spline resampled by arc length at constant speed under a trapezoid ease-in/out, then gaussian low-pass smoothed in position, look-at target and fov; no walk bob; Midday light).

## Re-render (one command, PC only, needs a GPU editor)

```
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.TrailerRender.Render -OutDir <scratch dir outside the repo>
```

Builds Hollowmere and the Cellars test floor first, unless `SC_CAPTURE_NOBUILD` is set. Full run = 2250 frames; check the wrapper log for `SC TRAILER DONE`.

## Output

- `<main checkout>\Reference\private	railers\<yyyy-MM-dd>-<sha>	railer.mp4` (main checkout, gitignored, survives worktree removal; override with SC_TRAILER_OUT), burned in bottom right: short sha and date.
- `key_01..key_06.png` in the same folder: ruined square, rebuilt square, lane, home door, stair, cellar.
- Frames: `%TEMP%\sc_trailer_frames` (cleaned at start; `seq/` holds the numbered PNGs ffmpeg read).

## Knobs (environment variables)

| Variable | Effect |
| --- | --- |
| `SC_TRAILER_FPS_SKIP=N` | render every Nth frame, encode at 30/N fps, file `trailer_preview.mp4` |
| `SC_TRAILER_FRAMES="a-b"` | only frames a..b (also writes `trailer_preview.mp4`) |
| `SC_TRAILER_CHECKONLY=1` | no rendering (no `-Graphics` needed): only the per-frame camera clearance check, about 2 minutes |
| `SC_CAPTURE_NOBUILD=1` | reuse the built scenes |
| `SC_FFMPEG` | path to ffmpeg.exe |
| `SC_TRAILER_OUT`, `SC_TRAILER_FRAMEDIR` | override the output and frame folders |

## Clearance check

Every frame's camera is tested against temporary MeshColliders added to the scene's meshes (never saved): inside a mesh (0.25 m sphere) or a mesh within 0.4 m ahead logs `SC TRAILER CLEARANCE t=.. pos=.. <reason>`. Run `SC_TRAILER_CHECKONLY=1` after editing waypoints until it prints none. Waypoint `pos.y` and Hollowmere `look.y` are heights above ground at that spot; the Cellars shots use absolute positions taken from `CellarFloorTestBuilder.CorridorView/RoomView`.

## ffmpeg

Looked for in this order: `SC_FFMPEG`, `C:\Dev\_tools\ffmpeg\**\bin\ffmpeg.exe`, PATH. Installed from the official BtbN build (https://github.com/BtbN/FFmpeg-Builds/releases, `ffmpeg-master-latest-win64-gpl.zip`) into `C:\Dev\_tools\ffmpeg`. Encode: libx264, preset slow, crf 26 (about 30-60 MB for 75 s), yuv420p, faststart, drawtext with `C:/Windows/Fonts/arial.ttf`.

## Notes

- A graphics run (and even a check-only run) dirties tracked files: `.mat`, texture `.meta`, scenes, `ProjectSettings/QualitySettings.asset`. Restore with `git checkout -- Assets ProjectSettings` and never commit them.
- Timeline (s): 0-12.2 ruined square sweep (arc round the fountain, crane 3.6-6.2 m), 12.2-13.8 wipe to Rebuilt (camera holds), 13.8-26 rebuilt sweep (the same flight in reverse), dip, 26-47 Keep Lane glide from its east end to the home door, in, toward the stair (4.6 m down to 1.5 m eye), dip, 47-60 stair scene descent (camera lamp, trailer-only), dip, 60-67 Cellars corridor dolly, dip, 67-74.5 Cellars room, fade out. Fade in 0.7 s, out 0.9 s.
- The run logs `SC TRAILER SEGMENT <name> ... maxStep ... maxRot` per segment: the biggest per-frame camera move and turn. Keep them small after editing waypoints.
- The stair segment adds a point light that rides with the camera (created at run time, never saved).
