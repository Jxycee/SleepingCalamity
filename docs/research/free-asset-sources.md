# Free asset sources (surveyed 2026-10-30)

Which free sites fit our asset rule (`.claude/rules/art-assets.md`: free, commercial-game use and modification allowed, source + licence recorded), and what we pulled. The curated pull (user, 2026-10-30: "curated set at 2K") is in `Art/Source/<Provider>/`, listed in `Art/Source/fetch_list.txt` and fetched by `Art/Blender/tools/sc_fetch_models.py`, which writes one SOURCES row per asset. Everything there is a raw source for adoption (sc-asset-adopt): it gets restyled, never dropped into `Assets/` as is.

## Verdicts
| Site | Licence | Verdict | Notes |
|---|---|---|---|
| Poly Haven | CC0 1.0 | use | Scans and models, glTF + PBR. Our B1-B3 tileables already come from it (`Art/Textures/*/SOURCES.md`). Scriptable API. |
| ambientCG | CC0 1.0 ("include the raw files in ... a video game") | use | Textures, decals, atlases. Scriptable API (`api/v2/full_json`). |
| NVIDIA SimReady (Hugging Face) | CC BY 4.0, per-asset LICENSE.txt | use with credit | Modern/industrial photoreal, USD, 8K PNGs, high tri counts. Only larder items fit. Credit line per asset is in its SOURCES row; it goes in the game credits. |
| cgbookcase | CC0 | use (manual) | Stain decals, mossy wall, battered metal, coal tiles; download URLs not scripted yet. |
| 3dtextures.me | CC0 | use (manual) | Often Google Drive links. |
| Kenney, Quaternius (free tier), OpenGameArt CC0 / CC BY 4.0 | CC0 / CC BY | blockout only | Flat-colour low-poly: shape references, not our look. OGA: reject CC-BY-SA, GPL and CC-BY 3.0 (FAQ: no DRM distribution). Useful packs: OGA "Medieval Tavern Props Pack", Quaternius "Fantasy Props MegaKit" and "Medieval Village MegaKit". |
| Sketchfab | per asset | CC0 / CC BY only | Check every asset; reject BY-SA, ND, NC. Download API needs a token. |
| Smithsonian 3D, Poly Pizza, Fab | CC0 subset / per asset / Fab Standard | unverified | Sites blocked our fetch; check by hand before use. |
| Unity Asset Store (free) | Standard EULA | use inside the Unity game | Not CC; can't be redistributed outside the project. |
| ShareTextures | "custom CC0" banning redistribution in collections and automated downloads | ask first | Not true CC0. |
| Textures.com | proprietary | reject | |

## What we pulled (2K)
- **Poly Haven models (39, CC0):** B7 barrels, crates, buckets, baskets, vases, jug, shelves; B8 lanterns; cellar dressing (chest, shield, candlesticks, oil lamp, wine bottles, tables, stools, fire pit, bowls, plate, castle door, iron gate); B6 rock and root bake sources; B9 nettle and weed sources. Recorded but not fetched (over 150k tris, fetch with `--max-tris`): root_cluster_01/02, moss_01, grass_medium_01. Skipped: decorative_book_set_01 (no glTF).
- **ambientCG (9, CC0):** Leaking006/012A/014A/016A drip decals, Grate001/002 (B11 breath grate), LeafSet017 (ivy atlas), Moss002, Metal052A.
- **SimReady (2, CC BY 4.0):** container_bottle_clay_a01, misc_bottle_corked_a01 (textures downscaled from 8K to 2K).

## Still ours to build (no free match)
Soot and coal, trim sheets, moss/crack alpha decals, wax-drip candles, the wine rack, the stone fountain (B10), iron sconces, hanging herbs, chains, the facade and vault kits, the market stall, awning, banners and sacks, ivy meshes and the orchard tree.
