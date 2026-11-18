# Megascans index

Searchable list of Quixel Megascans assets (id, name, type, categories, tags, physical size), so sessions can pick assets by name and then download them in the Epic Launcher (the user owns the whole library on Fab).

- **Source:** `complete_asset_metadata.tar.zst` from github.com/WAUthethird/quixel-megascans-scripts (MIT licence). The metadata inside is dated **22 Oct 2024**. Built 2026-11-17 with `Tools/art/sc_megascans_build_index.py` (stream parse, no scripts from the source repo were run). Only facts (names, IDs, sizes, tags) are kept.
- **File:** `megascans_index.csv`, UTF-8, 18,870 rows, sorted by type then name. Columns: `id, name, type, categories (joined with /), tags (joined with ;, max 15), size_m, texel_px_per_m, pack`.
  - `size_m`: for `3d`/`3dplant` the bounding box Length x Width x Height in metres; for surfaces, atlases, decals and brushes the scan area (e.g. `2x2`). Empty when the source has none.
  - `texel_px_per_m`: Quixel's texel density (blank when 0 or missing).
- **Rows by type:** surface 8211, atlas 5528, 3d 4235, brush 618, 3dplant 278. (Decals are `atlas` or `surface` rows with a `decals`/`scatter` category.)

## Caveats
- Assets released after the Oct 2024 dump are missing. Check against the Epic Launcher library: 49 of the 98 downloaded folders matched an ID in the index; the 49 misses are all newer assets (for example the "Forest American Wild Mushroom", "Historic Medieval Church Ruin" and "Historic Medieval Farm" sets, IDs like `zltidbd`, `xjvtedp`). Search the Launcher by name for those.
- Fab titles can differ slightly from the Quixel names. Search the Launcher by name; the ID ties a download back to its row. Fab downloads keep the ID: `Basil-676fbb12/basil_vgpochzia_mid/...` is ID `vgpochzia`.
- IDs are case-sensitive in the source (for example `pjvdI2`); the finder compares lowercase.
- Names repeat a lot (`Japanese Gravestone` has several variants); use size and ID to tell them apart.

## Finder
```
python Tools/art/sc_megascans_find.py "gravestone" --type 3d --limit 20
python Tools/art/sc_megascans_find.py mossy rock --type surface --full
python Tools/art/sc_megascans_find.py --id vgpochzia
python Tools/art/sc_megascans_find.py --downloaded
```
Words are AND-matched against name, categories and tags. The `dl=` column says whether `%LOCALAPPDATA%/com.epicgames.eglv2/FabLibrary/` already holds that ID (`--library` or `SC_FAB_LIBRARY` overrides the path). Stdlib only.
