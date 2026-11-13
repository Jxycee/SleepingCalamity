# Asset search pipeline (built 2026-11-13)

Needs list -> one cheap search per need -> GPU style rank against our reference frames -> contact sheet -> Fable (or you) judges the sheet instead of browsing Fab. **Free only (user, 2026-11-13):** paid listings are dropped; a listing counts as free when any licence tier prices at 0 (a free personal option). `--include-paid` exists but must not be used for picks.

## Tools
- `fab_search.py "<query>" [--megascans] [--type 3d-model|material|decal ...] [--limit 30] [--download-thumbs DIR] [--include-paid]`
  Calls fab.com's public `https://www.fab.com/i/listings/search` JSON endpoint (found by watching the site's own search in the browser pane). No sign-in; query text only, nothing personal. Plain `Mozilla/5.0` user agent (the CDN 403s custom UAs; do not spoof browsers or add referers). JSON lines: title, url, creator, type, formats, unreal_only, licences, price, free, tags, thumb, uid (+ thumb_file with `--download-thumbs`, which first clears old jpgs in DIR). `--megascans` = `seller=Quixel Megascans`. ~1 s per 24 results (cursor paging).
- `rank_style.py --refs DIR --cands DIR --top 8 --meta need.jsonl --out need.csv [--text "query"]`
  SigLIP (`google/siglip-base-patch16-224`, official HF repo, downloaded 2026-11-13 into `~/.cache/huggingface`; ~800 MB). Score = 0.5 x cosine to mean reference embedding + 0.5 x max cosine over references. Run with `C:\AI\trellis2\venv\Scripts\python.exe -W ignore` (torch 2.7 + transformers 5.22, RTX 5070). 40 candidates rank in a few seconds. `--text` needs sentencepiece + protobuf, absent from that venv, so it is off by default.
- `make_sheet.py --ranked need.csv --meta need.jsonl --refs r1 r2 r3 --title "need" --out sheet.jpg`
  References on top, ranked tiles below with score, price, UE-ONLY flag, title, creator, formats.

## How the Sunday Fable run calls it
1. Write the needs list (one line per need: id, search query, which `--type`, reference frames to use).
2. Launch **one Haiku search agent per need, all in one message**. Each runs `fab_search.py` (maybe 2 queries: plain, and `--megascans`, merged) into `<scratch>/<need>_cands` + `<need>.jsonl`, then `rank_style.py` and `make_sheet.py`, and returns only the sheet path and top-3 lines.
3. Fable reads the sheets (one image per need) and picks. Open the listing URL only for the final pick to confirm licence and formats.

## Costs and limits
- Search + rank + sheet per need: about 1 minute, a few Haiku turns, no Fab sign-in, GPU about 1.5 GB.
- Style similarity is coarse: scores cluster (0.58-0.72) and mean "looks like our references", not "is the right object". Material spheres and texture previews rank high on texture queries; use `--type 3d-model` or `--type material` to separate them. Always look at the sheet.
- References must be single frames (not contact sheets), 3-5 of them, matching the need (cellar frames for Cellars, town frames for Hollowmere). Our own captures work as refs too.
- Thumbnails are 320-640 px marketing renders on dark backdrops; scans look alike at that size. `unreal_only` listings (formats only unreal-engine) are flagged; check Unity-usable formats (fbx/gltf) before picking.
- Fab can change or block the unofficial endpoint. Fallback: read search results in the browser pane (`get_page_text`) and fetch thumbs from the same page.
- Tested needs and sheets: `Reference/private/asset-search-test/` (gitignored).

## Changes 2026-11-13 (Fable Run 3)
- `--type` fixed: the list was url-encoded as text and Fab answered HTTP 400. Types are now joined with commas.
- For objects, judge sheets in Fab relevance order with the type filter (Megascans first); keep style rank for materials. A scene-like thumbnail outranks the right object otherwise.
- Search Megascans by set name ("Historic Medieval Farm", "Historic Bazaar", "Old Mine") to list a whole prop family.
