# Map trailer render

A repeatable ~75 s, 30 fps, 1920x1080 fly-through: Hollowmere square (Ruined), wipe to Rebuilt, Market Street up to the heroes' home, in at the door to the Undercroft stair, then the Cellars test floor (seed 12345, floor 1). Code: `Assets/_Project/Editor/Builders/TrailerRender.cs` (route = waypoint tables `SquareShot` / `LaneShot` at the top, Catmull-Rom evaluated at t = frame / 30, Midday light).

## Re-render (one command, PC only, needs a GPU editor)

```
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.TrailerRender.Render -OutDir <scratch dir outside the repo>
```

Builds Hollowmere and the Cellars test floor first, unless `SC_CAPTURE_NOBUILD` is set. Full run = 2250 frames; check the wrapper log for `SC TRAILER DONE`.

## Output

- `Reference/private/trailers/<yyyy-MM-dd>-<sha>/trailer.mp4` (gitignored), burned in bottom right: short sha and date.
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

Looked for in this order: `SC_FFMPEG`, `C:\Dev\_tools\ffmpeg\**\bin\ffmpeg.exe`, PATH. Installed from the official BtbN build (https://github.com/BtbN/FFmpeg-Builds/releases, `ffmpeg-master-latest-win64-gpl.zip`) into `C:\Dev\_tools\ffmpeg`. Encode: libx264, crf 16, yuv420p, drawtext with `C:/Windows/Fonts/arial.ttf`.

## Notes

- A graphics run (and even a check-only run) dirties tracked files: `.mat`, texture `.meta`, scenes, `ProjectSettings/QualitySettings.asset`. Restore with `git checkout -- Assets ProjectSettings` and never commit them.
- Timeline: 0-15 s ruined square (wipe 13.5-15 s), 15-30 rebuilt square, dip, 30-60 lane and home, dip, 60-67.5 cellar corridor, dip, 67.5-75 cellar room. Fade in 0.7 s, out 0.8 s.
