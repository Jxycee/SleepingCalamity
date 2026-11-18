#!/usr/bin/env python3
"""Search Reference/megascans/megascans_index.csv (stdlib only).

  python Tools/art/sc_megascans_find.py "gravestone" --type 3d --limit 20
  python Tools/art/sc_megascans_find.py mossy rock wall --type surface
  python Tools/art/sc_megascans_find.py --id vgpochzia
  python Tools/art/sc_megascans_find.py --downloaded          # list what is already in the Epic Launcher library

Every word must appear (AND) in name, categories or tags. 'dl' column: yes when a folder or file under
%LOCALAPPDATA%/com.epicgames.eglv2/FabLibrary/ carries the asset's Quixel ID as a '_<id>_' token.
Override the library path with --library or SC_FAB_LIBRARY.
"""
import argparse, csv, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
INDEX = os.path.join(HERE, "..", "..", "Reference", "megascans", "megascans_index.csv")


def library_dir(arg):
    if arg:
        return arg
    if os.environ.get("SC_FAB_LIBRARY"):
        return os.environ["SC_FAB_LIBRARY"]
    base = os.environ.get("LOCALAPPDATA") or os.path.expanduser("~/AppData/Local")
    return os.path.join(base, "com.epicgames.eglv2", "FabLibrary")


def downloaded_ids(lib):
    """Lowercase tokens found between underscores in folder and file names of the library."""
    toks = set()
    if not os.path.isdir(lib):
        return toks
    for _root, dirs, files in os.walk(lib):
        for n in dirs + files:
            parts = os.path.splitext(n)[0].lower().split("_")
            toks.update(parts[1:])
    return toks


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("words", nargs="*", help="search words, all must match")
    ap.add_argument("--type", help="3d, surface, 3dplant, atlas, brush (comma list ok)")
    ap.add_argument("--id", help="look up one Quixel ID")
    ap.add_argument("--limit", type=int, default=30)
    ap.add_argument("--downloaded", action="store_true", help="only assets already downloaded")
    ap.add_argument("--not-downloaded", action="store_true")
    ap.add_argument("--library", help="FabLibrary folder")
    ap.add_argument("--full", action="store_true", help="also print categories and tags")
    a = ap.parse_args()

    with open(INDEX, encoding="utf-8", newline="") as f:
        rows = list(csv.DictReader(f))
    types = set(a.type.lower().split(",")) if a.type else None
    words = [w.lower() for w in a.words]
    have = downloaded_ids(library_dir(a.library))

    out = []
    for r in rows:
        if a.id and r["id"].lower() != a.id.lower():
            continue
        if types and r["type"] not in types:
            continue
        hay = " ".join((r["name"], r["categories"].replace("/", " "), r["tags"].replace(";", " "))).lower()
        if not all(w in hay for w in words):
            continue
        dl = r["id"].lower() in have
        if (a.downloaded and not dl) or (a.not_downloaded and dl):
            continue
        out.append((r, dl))

    for r, dl in out[: a.limit]:
        line = "%-10s %-6s %-40s %-18s dl=%s" % (r["id"], r["type"], r["name"][:40],
                                                  r["size_m"] or "-", "yes" if dl else "no")
        print(line)
        if a.full:
            print("           cat: %s | tags: %s" % (r["categories"], r["tags"]))
    print("%d match(es)%s" % (len(out), "" if len(out) <= a.limit else ", showing %d (raise --limit)" % a.limit))


if __name__ == "__main__":
    sys.exit(main())
