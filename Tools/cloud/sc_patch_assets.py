#!/usr/bin/env python3
"""Search and fetch assets from Patch's open asset library (patch.gg) without the Patch app.

The library is an index over upstream CC0 / CC-BY sources (Kenney, Poly Haven, ambientCG, KayKit, incompetech, ...)
with a public REST API that needs no login: https://patch.gg/api/v1/openapi.json. Standard library only.

  sc_patch_assets.py search "dungeon door" --type model --where "triangles<=5000" --limit 10
  sc_patch_assets.py search sword --type audio
  sc_patch_assets.py facets                      # counts by type / source / licence
  sc_patch_assets.py info kenney:nature-kit/tree_blocks.glb
  sc_patch_assets.py get incompetech:video_dungeon_boss --batch cellar-music --purpose "boss theme"
  sc_patch_assets.py get polyhaven:cobblestone_02 --dry-run
  sc_patch_assets.py credits                     # every CC-BY credit line recorded in Art/Source

`get` writes the files to Art/Source/<Provider>/<asset>/ (LFS, never under Assets/) and appends the row sc-asset-adopt
expects to Art/Source/<Provider>/SOURCES.md (same columns as sc_fetch_models.py). Each file is checked against the sha256
the API lists. Only CC0 and CC-BY assets are fetched; anything else is refused. AI-generated assets are refused unless
--allow-ai. CC-BY assets need their credit line in the game's credits (`credits` lists them). Patch repacks some models
(glTF into one GLB, pivot moved): prefer the upstream original (sc_fetch_models.py) when you need the untouched source.
"""
import argparse
import datetime
import hashlib
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

API = "https://patch.gg/api/v1"
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Art", "Source")
ALLOWED = {"CC0-1.0", "CC-BY-4.0"}
UA = "sc-patch-assets/1"


def http(url, timeout=120):
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.read()
    except urllib.error.HTTPError as e:
        sys.exit(f"HTTP {e.code} for {url}: {e.read()[:300].decode('utf-8', 'replace')}")
    except urllib.error.URLError as e:
        sys.exit(f"network error for {url}: {e.reason}")


def api(path, **params):
    q = {k: v for k, v in params.items() if v not in (None, "")}
    url = f"{API}{path}" + ("?" + urllib.parse.urlencode(q, doseq=True) if q else "")
    return json.loads(http(url))


def asset(aid):
    return api("/assets/" + urllib.parse.quote(aid, safe=""))


def safe(s):
    return re.sub(r"[^A-Za-z0-9._-]+", "_", s).strip("_") or "asset"


def provider_dir(a):
    name = a["source"]["name"].split(".")[0]  # "incompetech.com" -> "incompetech"
    return re.sub(r"[^A-Za-z0-9]+", "", name) or "Misc"  # "Poly Haven" -> "PolyHaven", as in Art/Source


def cmd_search(args):
    r = api("/assets/search", q=args.query, type=args.type, source=args.source, license=args.license,
            where=args.where, sort=args.sort, ai="false" if not args.allow_ai else None,
            group="false" if args.all_variants else None, limit=args.limit, cursor=args.cursor)
    if args.json:
        print(json.dumps(r, indent=1))
        return
    print(f"{r['total']} hits (showing {len(r['hits'])})")
    for h in r["hits"]:
        fit = h.get("fit") or {}
        bits = [h["type"], h["license"]]
        if fit.get("triangles") is not None:
            bits.append(f"{fit['triangles']} tris")
        if fit.get("mb") is not None:
            bits.append(f"{fit['mb']} MB")
        if h.get("ai"):
            bits.append("AI")
        print(f"{h['id']}\t{h['title']}\t[{', '.join(bits)}]")
    if r.get("next_cursor"):
        print(f"next: --cursor {r['next_cursor']}")


def cmd_facets(args):
    r = api("/assets/facets", q=args.query, type=args.type, source=args.source)
    print("total", r["total"])
    for k in ("type", "source", "license"):
        print(k, json.dumps(r["global"].get(k, {})))


def cmd_info(args):
    a = asset(args.id)
    if args.json:
        print(json.dumps(a, indent=1))
        return
    print(f"{a['id']}  {a['title']}  ({a['type_label']})")
    print(f"licence: {a['license']['spdx']}  AI: {a['ai']}  creators: {', '.join(a.get('creators') or [])}")
    print(f"source: {a['source']['url']}")
    print(f"credit: {a['credit']}")
    print(f"facts: {a.get('facts')}")
    for v in a["variants"]:
        print(f"  variant {v['name']}: {v['mb']} MB  " + ", ".join(f['name'] for f in v['files']))


def pick_variant(a, want):
    names = {v["name"]: v for v in a["variants"]}
    if want:
        if want not in names:
            sys.exit(f"{a['id']}: no variant {want!r}; has {', '.join(names)}")
        return names[want]
    for n in ("web", "src"):
        if n in names:
            return names[n]
    real = [v for v in a["variants"] if v["name"] != "preview"]
    if not real:
        sys.exit(f"{a['id']}: only a preview variant exists")
    return real[0]


def licence_cell(a):
    lic = a["license"]
    ev = (lic.get("evidence") or {}).get("url") or lic.get("url")
    return f"{lic['name']} ({ev})"


def tris_cell(a):
    return str((a.get("facts") or {}).get("triangles", "?"))


def record(a, batch, purpose, changed):
    d = os.path.join(SRC, provider_dir(a))
    os.makedirs(d, exist_ok=True)
    path = os.path.join(d, "SOURCES.md")
    name = a["source"].get("ref") or a["title"]
    if not os.path.exists(path):
        with open(path, "w", encoding="utf-8") as f:
            f.write(f"# {provider_dir(a)} sources\n\nRaw downloads for adoption (sc-asset-adopt). Written by "
                    "`Tools/cloud/sc_patch_assets.py` (Patch asset library); one row per asset.\n\n"
                    "| asset | source | licence | author / credit | fetched | tris | batch | purpose | what we changed |\n"
                    "|---|---|---|---|---|---|---|---|---|\n")
    text = open(path, encoding="utf-8").read()
    if re.search(r"^\| " + re.escape(name) + r" \|", text, re.M):
        return False
    cells = [name, a["source"]["url"], licence_cell(a), a["credit"], datetime.date.today().isoformat(),
             tris_cell(a), batch or "-", purpose or "-", changed]
    row = "| " + " | ".join(c.replace("|", "/").replace("\n", " ") for c in cells) + " |\n"
    if not text.endswith("\n"):
        row = "\n" + row
    with open(path, "a", encoding="utf-8") as f:
        f.write(row)
    return True


def cmd_get(args):
    for aid in args.ids:
        a = asset(aid)
        spdx = a["license"]["spdx"]
        if spdx not in ALLOWED:
            print(f"SKIP {aid}: licence {spdx} is not CC0 / CC-BY-4.0")
            continue
        if a.get("ai") and not args.allow_ai:
            print(f"SKIP {aid}: AI-generated (use --allow-ai to take it anyway)")
            continue
        v = pick_variant(a, args.variant)
        dest = os.path.join(SRC, provider_dir(a), safe(a["source"].get("ref") or a["title"]))
        total = sum(f["size"] for f in v["files"])
        print(f"{aid}: {spdx}, variant {v['name']}, {len(v['files'])} file(s), {total / 1e6:.2f} MB -> "
              f"{os.path.relpath(dest, ROOT)}")
        if args.dry_run:
            continue
        os.makedirs(dest, exist_ok=True)
        for f in v["files"]:
            data = http(f["url"], timeout=300)
            if hashlib.sha256(data).hexdigest() != f["sha256"]:
                sys.exit(f"{aid}: sha256 mismatch for {f['name']}; nothing recorded")
            with open(os.path.join(dest, safe(f["name"])), "wb") as out:
                out.write(data)
        changed = f"none ({v['name']} variant as served by Patch)"
        if record(a, args.batch, args.purpose, changed):
            print("  recorded in SOURCES.md" + ("  [CC-BY: credit required]" if spdx == "CC-BY-4.0" else ""))
        else:
            print("  already in SOURCES.md (files refreshed)")


def cmd_credits(args):
    rows = []
    for dp, _, fs in os.walk(SRC):
        if "SOURCES.md" in fs:
            for line in open(os.path.join(dp, "SOURCES.md"), encoding="utf-8"):
                c = [x.strip() for x in line.strip().strip("|").split(" | ")]
                if len(c) >= 4 and re.search(r"CC[ -]BY|Attribution", c[2], re.I):
                    rows.append(c[3])
    for r in sorted(set(rows)):
        print(r)
    print(f"-- {len(set(rows))} CC-BY credit line(s)", file=sys.stderr)


def main(argv):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("search")
    s.add_argument("query", nargs="?")
    s.add_argument("--type", help="model,material,hdri,audio,music,sprite,splat,shader (comma-separated)")
    s.add_argument("--source", help="e.g. kenney,polyhaven,ambientcg,kaykit,incompetech")
    s.add_argument("--license", help='SPDX ids, e.g. CC0-1.0 (default "safe")')
    s.add_argument("--where", help='facet filter, e.g. "triangles<=5000" or "texture_px between 512,2048"')
    s.add_argument("--sort", choices=["best", "light", "triangles", "name"])
    s.add_argument("--limit", type=int, default=20)
    s.add_argument("--cursor")
    s.add_argument("--allow-ai", action="store_true", help="include AI-generated assets")
    s.add_argument("--all-variants", action="store_true", help="one hit per asset, not per alternatives group")
    s.add_argument("--json", action="store_true")
    s.set_defaults(fn=cmd_search)
    f = sub.add_parser("facets")
    f.add_argument("query", nargs="?")
    f.add_argument("--type")
    f.add_argument("--source")
    f.set_defaults(fn=cmd_facets)
    i = sub.add_parser("info")
    i.add_argument("id")
    i.add_argument("--json", action="store_true")
    i.set_defaults(fn=cmd_info)
    g = sub.add_parser("get")
    g.add_argument("ids", nargs="+")
    g.add_argument("--variant", help="web (default), src or preview")
    g.add_argument("--batch", help="batch label for the SOURCES row")
    g.add_argument("--purpose", help="what it is for, for the SOURCES row")
    g.add_argument("--allow-ai", action="store_true")
    g.add_argument("--dry-run", action="store_true")
    g.set_defaults(fn=cmd_get)
    c = sub.add_parser("credits")
    c.set_defaults(fn=cmd_credits)
    args = p.parse_args(argv)
    args.fn(args)


if __name__ == "__main__":
    main(sys.argv[1:])
