# sc_ref_fetch.py (2026-10-25, asset skills): find and fetch REFERENCE photos and paintings for an asset the concept paintings don't show
# (a pollard willow, a dry-stone wall, a clinker boat), with the licence of every image recorded. Reference images are study material: they are
# looked at, cropped and measured (sc_ref_palette.py, sc_concept_match.py), never shipped in the game or traced into textures (sc-reference).
#
# Run (plain python3, no Blender; needs network):
#   python3 Art/Blender/tools/sc_ref_fetch.py "<query>" <out_dir> [--n 8] [--licence cc0,pdm,by] [--long 1024] [--source flickr,wikimedia]
#                                            [--paintings] [--min 800] [--dedupe] [--sheet <contact.png>] [--timeout 30] [--retries 2]
#   "<q1>|<q2>|<q3>" runs several phrasings and interleaves their results (one query rarely gives 4 good views: "clinker rowing boat|wooden
#   rowing boat|clinker dinghy"). --dedupe takes one image per creator first and only fills up with repeats when --n can't be met otherwise (one
#   Flickr user's set of the same boat is one reference, not six). --sheet writes a contact sheet of every fetched image with its index, licence
#   and query, to look at before measuring (drop signs, kayaks, graded or off-subject shots).
#   e.g. python3 Art/Blender/tools/sc_ref_fetch.py "pollarded willow" <scratch>/refs/willow --n 8
#        python3 Art/Blender/tools/sc_ref_fetch.py "willow river painting" <scratch>/refs/willow_paint --paintings
#
# Searches Openverse (api.openverse.org: Flickr, Wikimedia Commons, museum collections; anonymous, so at most 20 results a query), keeps only licences that allow study copies and
# derivatives (default CC0, public domain mark, CC BY; never ND or NC), downloads each image resized to --long px on its long side, and writes
# <out_dir>/REFS.md (one row per image: file, title, creator, licence, landing page) plus refs.json. --paintings restricts to museum and
# Wikimedia sources and adds "painting" to the query: public-domain landscape painters (Constable, Corot, Van Gogh, Daubigny) are the best
# painterly-style references for Riverlight.
# Identifies itself with a project User-Agent (Wikimedia and Flickr refuse anonymous clients); no personal data is sent. Backs off on HTTP 429.

import io, json, os, re, signal, sys, time, urllib.parse, urllib.request

UA = 'SleepingCalamity-reference-tool/1.0 (https://github.com/Jxycee/SleepingCalamity)'
API = 'https://api.openverse.org/v1/images/'

def opt(name, default, cast=str):
    return cast(sys.argv[sys.argv.index(name) + 1]) if name in sys.argv else default

TIMEOUT, RETRIES = 30, 2   # --timeout / --retries (round 28: Openverse answered in 14 s once, so the old 15 s connect timeout failed every query)

def get(url, tries=None, deadline=None, cap=20 << 20):
    tries = tries or RETRIES + 1
    deadline = deadline or max(40, TIMEOUT * 1.5)
    # a whole-download deadline and size cap: some index entries point at 30 MB originals on slow hosts
    for i in range(tries):
        try:
            req = urllib.request.Request(url, headers={'User-Agent': UA})
            t0, buf = time.time(), bytearray()
            with urllib.request.urlopen(req, timeout=TIMEOUT) as r:
                while True:
                    chunk = r.read(1 << 16)
                    if not chunk:
                        return bytes(buf)
                    buf += chunk
                    if len(buf) > cap or time.time() - t0 > deadline:
                        raise TimeoutError('too big or too slow')
        except urllib.error.HTTPError as e:
            if e.code == 429 and i < tries - 1:
                time.sleep(int(e.headers.get('Retry-After') or 0) or 2 ** (i + 1))
                continue
            raise
        except (TimeoutError, OSError) as e:   # a slow API answer: retry with backoff
            if i < tries - 1 and not isinstance(e, urllib.error.HTTPError):
                time.sleep(2 ** (i + 1)); continue
            raise
    return None

def clean(s, n=90):
    # Wikimedia titles and creators arrive as HTML (Wikidata widgets): keep the first line of text
    s = re.sub(r"<div style='display: ?none;'>.*", '', s or '', flags=re.S)
    s = re.sub(r'<[^>]+>', ' ', s)
    return re.sub(r'\s+', ' ', s).strip()[:n]

def slug(s):
    return re.sub(r'[^a-z0-9]+', '_', s.lower()).strip('_')[:40] or 'ref'

def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    flagvals = {sys.argv[i + 1] for i, a in enumerate(sys.argv[:-1]) if a.startswith('--') and a not in ('--paintings', '--dedupe')}
    args = [a for a in args if a not in flagvals]
    if len(args) < 2 or '-h' in sys.argv or '--help' in sys.argv:
        print(__doc__ or open(__file__).read().split('\n\n')[0]); sys.exit(0 if ('-h' in sys.argv or '--help' in sys.argv) else 2)
    query, out = args[0], args[1]
    global TIMEOUT, RETRIES
    TIMEOUT, RETRIES = opt('--timeout', 30, float), opt('--retries', 2, int)
    n, lic, long_side, min_px = opt('--n', 8, int), opt('--licence', 'cc0,pdm,by'), opt('--long', 1024, int), opt('--min', 800, int)
    paintings = '--paintings' in sys.argv
    src = opt('--source', 'wikimedia,smithsonian_american_art_museum,nga,met,clevelandmuseum,rijksmuseum' if paintings else '')
    lists = []
    for qq in [x.strip() for x in query.split('|') if x.strip()]:
        qq = qq + (' painting' if paintings and 'paint' not in qq else '')
        params = {'q': qq, 'license': lic, 'page_size': 20, 'mature': 'false'}
        if src:
            params['source'] = src
        try:
            res = json.loads(get(API + '?' + urllib.parse.urlencode(params))).get('results', [])
        except Exception as e:
            print('query failed:', qq, e); res = []
        for r in res:
            r['_q'] = qq
        lists.append(res)
        time.sleep(0.5)
    q = ' | '.join(x[0]['_q'] if x else '' for x in lists) or query
    cands, seen_id = [], set()
    for k in range(max([len(x) for x in lists] or [0])):   # interleave the phrasings, dropping the same image found twice
        for x in lists:
            if k < len(x) and x[k].get('id') not in seen_id:
                seen_id.add(x[k].get('id')); cands.append(x[k])
    if '--dedupe' in sys.argv:   # one per creator first, repeats after (used only if --n is not met)
        firsts, rest, who = [], [], set()
        for r in cands:
            c = clean(r.get('creator'), 60).lower() or r.get('id')
            (rest if c in who else firsts).append(r); who.add(c)
        cands = firsts + rest
    from PIL import Image
    os.makedirs(out, exist_ok=True)
    rows, kept = [], 0
    for r in cands:
        r['title'], r['creator'] = clean(r.get('title')), clean(r.get('creator'), 60)
        if kept >= n:
            break
        w, h = r.get('width') or 0, r.get('height') or 0
        if w and h and max(w, h) < min_px:
            continue
        try:
            thumb = False
            try:
                raw = get(r['url'], tries=1)
            except Exception:   # slow or huge original: Openverse's own thumbnail (smaller, always served)
                raw, thumb = get(API + r['id'] + '/thumb/'), True
            im = Image.open(io.BytesIO(raw)).convert('RGB')
        except Exception as e:   # dead links are common in the index: skip
            print('skip', clean(r.get('title'), 50), e); continue
        if max(im.size) < (min(min_px, 500) if thumb else min_px):
            continue
        im.thumbnail((long_side, long_side), Image.LANCZOS)
        name = '%02d_%s.jpg' % (kept + 1, slug(r.get('title') or 'ref'))
        im.save(os.path.join(out, name), quality=90)
        lic_s = (r.get('license') or '').upper() + (' ' + r['license_version'] if r.get('license_version') else '')
        rows.append({'file': name, 'title': r.get('title'), 'creator': r.get('creator'), 'licence': lic_s, 'licence_url': r.get('license_url'),
                     'landing': r.get('foreign_landing_url'), 'source': r.get('source'), 'size': list(im.size), 'query': r.get('_q')})
        kept += 1
        print(name, '|', lic_s, '|', r.get('creator'), '|', r.get('foreign_landing_url'))
        time.sleep(0.5)   # polite to the image hosts
    with open(os.path.join(out, 'refs.json'), 'w') as f:
        json.dump({'query': q, 'licence_filter': lic, 'fetched': time.strftime('%Y-%m-%d'), 'images': rows}, f, indent=1)
    with open(os.path.join(out, 'REFS.md'), 'w') as f:
        f.write('# References: %s\n\nStudy material only (sc-reference): not shipped, not traced. Fetched %s via Openverse, licence filter %s.\n\n'
                % (q, time.strftime('%Y-%m-%d'), lic))
        f.write('| file | title | creator | licence | source |\n|---|---|---|---|---|\n')
        for r in rows:
            f.write('| %s | %s | %s | %s | %s |\n' % (r['file'], (r['title'] or '').replace('|', '/'), (r['creator'] or '').replace('|', '/'), r['licence'], r['landing']))
    print('%d images -> %s' % (kept, out))
    sheet = opt('--sheet', None)
    if sheet and rows:   # a contact sheet: every image with its index, licence and query, to look at before measuring
        from PIL import ImageDraw
        cell, cols = 320, 4
        rws = (len(rows) + cols - 1) // cols
        cs = Image.new('RGB', (cell * cols, (cell + 34) * rws), (245, 245, 240))
        dr = ImageDraw.Draw(cs)
        for i, r in enumerate(rows):
            im = Image.open(os.path.join(out, r['file'])); im.thumbnail((cell - 8, cell - 8))
            x, y = (i % cols) * cell, (i // cols) * (cell + 34)
            cs.paste(im, (x + 4, y + 4))
            dr.text((x + 4, y + cell), '%s  %s  %s' % (r['file'][:2], r['licence'], (r['creator'] or '')[:24]), fill=(20, 20, 20))
            dr.text((x + 4, y + cell + 14), (r.get('query') or '')[:44], fill=(90, 90, 90))
        os.makedirs(os.path.dirname(os.path.abspath(sheet)), exist_ok=True)
        cs.save(sheet); print('sheet ->', sheet)

if __name__ == '__main__':
    if hasattr(signal, "SIGPIPE"):
        signal.signal(signal.SIGPIPE, signal.SIG_DFL)   # quiet under | head
    main()
