#!/usr/bin/env python3
"""Search fab.com via its public JSON endpoint (no sign-in). One JSON object per line on stdout.
  fab_search.py "mossy stone wall" [--include-paid] [--megascans] [--type 3d-model|material|decal|atlas-brush ...] [--limit 30] [--download-thumbs DIR]
Fields: title, url, creator, formats, unreal_only, licences, price, free, type, tags, thumb, uid.
Query text only; nothing personal is sent. Be polite: <=1 request/sec."""
import argparse, json, os, sys, time, urllib.parse, urllib.request

BASE = 'https://www.fab.com/i/listings/search'
UA = 'Mozilla/5.0'  # the CDN 403s custom UAs; no browser impersonation or referer tricks

def fetch(params):
    req = urllib.request.Request(BASE + '?' + urllib.parse.urlencode(params), headers={'User-Agent': UA, 'Accept': 'application/json'})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.load(r)

def norm(r):
    fmts = [f['assetFormatType']['code'] for f in r.get('assetFormats', [])]
    portable = {'fbx', 'gltf', 'obj', 'unity', 'blend', 'usd', 'usdz', 'substance-painter', 'texture', 'converted-files'}
    thumb = ''
    for t in r.get('thumbnails', []):
        imgs = t.get('images') or []
        if imgs: thumb = min(imgs, key=lambda i: abs((i.get('width') or 0) - 480))['url']; break
    price = (r.get('startingPrice') or {}).get('price')
    return {
        'title': r['title'], 'url': 'https://www.fab.com/listings/' + r['uid'], 'uid': r['uid'],
        'creator': (r.get('user') or {}).get('sellerName', ''), 'type': r.get('listingType'),
        'formats': fmts, 'unreal_only': bool(fmts) and not (set(fmts) & portable),
        'licences': [l['name'] for l in r.get('licenses', [])],
        'price': price, 'free': bool(r.get('isFree')) or price == 0, 'tags': [t['name'] for t in r.get('tags', [])][:8],
        'thumb': thumb,
    }

def search(q, free=False, megascans=False, types=None, limit=30):
    p = {'q': q}
    if free: p['is_free'] = 1
    if megascans: p['seller'] = 'Quixel Megascans'
    if types: p['listing_types'] = types
    out, cursor = [], None
    if free: p['is_free'] = 1
    while len(out) < limit:
        if cursor: p['cursor'] = cursor
        d = fetch(p)
        out += [n for n in map(norm, d.get('results', [])) if n['free'] or not free]  # free-only: paid listings dropped (price 0 on any tier = free personal option)
        cursor = (d.get('cursors') or {}).get('next')
        if not cursor or not d.get('results'): break
        time.sleep(1)
    return out[:limit]

if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    ap = argparse.ArgumentParser()
    ap.add_argument('query'); ap.add_argument('--include-paid', action='store_true', help='default is FREE ONLY (project rule 2026-11-13)'); ap.add_argument('--megascans', action='store_true')
    ap.add_argument('--type', action='append', help='listing type, repeatable (3d-model, material, decal...)')
    ap.add_argument('--limit', type=int, default=30); ap.add_argument('--download-thumbs', metavar='DIR')
    a = ap.parse_args()
    res = search(a.query, not a.include_paid, a.megascans, a.type, a.limit)
    if a.download_thumbs:
        os.makedirs(a.download_thumbs, exist_ok=True)
        for old in os.listdir(a.download_thumbs):  # no stale thumbs from earlier runs
            if old.lower().endswith('.jpg'): os.remove(os.path.join(a.download_thumbs, old))
        for i, r in enumerate(res):
            if not r['thumb']: continue
            f = os.path.join(a.download_thumbs, f"{i:02d}_{r['uid'][:8]}.jpg")
            try:
                open(f, 'wb').write(urllib.request.urlopen(urllib.request.Request(r['thumb'], headers={'User-Agent': UA}), timeout=30).read()); r['thumb_file'] = f
            except Exception as e: print('thumb fail', r['uid'], e, file=sys.stderr)
    for r in res: print(json.dumps(r, ensure_ascii=False))
