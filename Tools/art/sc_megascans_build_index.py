#!/usr/bin/env python3
"""Build Reference/megascans/megascans_index.csv from the Quixel metadata dump.

Source: github.com/WAUthethird/quixel-megascans-scripts (MIT), complete_asset_metadata.tar.zst
unpacked to asset_metadata.json (~1.5 GB). Stream-parsed with ijson (pip install zstandard ijson).
Usage: python -I sc_megascans_build_index.py <asset_metadata.json> <out.csv>
Set SC_SITE to a site-packages dir if -I hides your user packages.
"""
import csv, os, re, sys
if os.environ.get("SC_SITE"):
    sys.path.append(os.environ["SC_SITE"])
import ijson


def meta(fm, key):
    for m in fm.get("meta") or []:
        if m.get("key") == key:
            return m.get("value")
    return None


def num(v):
    m = re.search(r"[\d.]+", str(v or ""))
    return m.group(0) if m else ""


def size_of(fm, typ):
    if typ in ("3d", "3dplant"):
        dims = [num(meta(fm, k)) for k in ("length", "width", "height")]
        if any(dims):
            return "x".join(d or "?" for d in dims)
    sa = meta(fm, "scanArea")
    if sa:
        return re.sub(r"\s*m$", "", str(sa)).strip()
    return fm.get("physicalSize") or ""


def main(src, dst):
    rows = []
    with open(src, "rb") as f:
        for aid, v in ijson.kvitems(f, "asset_metadata"):
            fm = v.get("full_metadata") or {}
            cats = fm.get("categories") or []
            typ = cats[0] if cats else "unknown"
            seen, tl = set(), []
            for t in fm.get("tags") or []:
                t = str(t).strip().lower().replace(";", ",")
                if t and t not in seen:
                    seen.add(t)
                    tl.append(t)
            texel = num(meta(fm, "texelDensity"))
            pk = fm.get("pack")
            pack = pk.get("name", "") if isinstance(pk, dict) else ""
            rows.append([aid, (v.get("name") or fm.get("name") or "").strip(), typ,
                         "/".join(c.replace("/", "-") for c in cats[1:]),
                         ";".join(tl[:15]), size_of(fm, typ),
                         texel if texel != "0" else "", pack])
    rows.sort(key=lambda r: (r[2], r[1].lower(), r[0]))
    with open(dst, "w", encoding="utf-8", newline="") as o:
        w = csv.writer(o, lineterminator="\n")
        w.writerow(["id", "name", "type", "categories", "tags", "size_m", "texel_px_per_m", "pack"])
        w.writerows(rows)
    print(len(rows), "rows")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
