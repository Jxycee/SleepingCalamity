"""Fetch CC0 scans from Poly Haven (official API) into a source folder and write the source record.
python sc_fetch_textures.py <out_dir> <asset_id> [<asset_id> ...] [--res 2k] [--record <SOURCES.md>] [--maps diff,nor_gl,rough,ao,disp] [--purpose id=text ...]
Per asset it writes <id>_<map>_<res>.<ext> for the maps that exist (diff, nor_gl, rough, ao, disp, arm), and appends a record row
(id, URL, licence "CC0 1.0 per polyhaven.com/license", author, fetched date) to --record. Downloads only from api.polyhaven.com / dl.polyhaven.org.
Ported from the FishGame cottage fetch_textures.py (M8): generic ids instead of a fixed list.
Scans stay OUT of git when large: point <out_dir> at C:/Dev/_private/textures_src/<batch>/ and commit only the records and the graded outputs."""
import json, os, sys, urllib.request, datetime

UA = {'User-Agent': 'SleepingCalamity-fetch/1.0'}
MAPS = {'diff': ['Diffuse', 'diff'], 'nor_gl': ['nor_gl'], 'rough': ['Rough', 'rough'], 'ao': ['AO', 'ao'],
        'disp': ['Displacement', 'disp'], 'arm': ['arm']}
HOSTS = ('api.polyhaven.com', 'dl.polyhaven.org')


def get(url):
    host = url.split('/')[2]
    if host not in HOSTS:
        raise SystemExit(f'refusing host {host}: only {HOSTS}')
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=180).read()


def fetch(out, assets, res='2k', record=None, purpose=None, maps=None):
    os.makedirs(out, exist_ok=True)
    day = datetime.date.today().isoformat()
    rows = []
    for a in assets:
        info = json.loads(get(f'https://api.polyhaven.com/info/{a}'))
        files = json.loads(get(f'https://api.polyhaven.com/files/{a}'))
        authors = ', '.join(info.get('authors', {}).keys())
        got = []
        for short, cands in MAPS.items():
            if maps and short not in maps:
                continue
            k = next((c for c in cands if c in files), None)
            if not k:
                continue
            ent = files[k].get(res, {})
            fmt = ent.get('jpg') or ent.get('png') or ent.get('exr')
            if not fmt:
                continue
            ext = fmt['url'].rsplit('.', 1)[-1]
            p = os.path.join(out, f'{a}_{short}_{res}.{ext}')
            if not os.path.exists(p):
                open(p, 'wb').write(get(fmt['url']))
            got.append(short)
        rows.append(f'| {a} | https://polyhaven.com/a/{a} | CC0 1.0 (Poly Haven) | {authors} | {day} | '
                    f'{" ".join(got)} | {(purpose or {}).get(a, "")} |')
        print(a, 'maps:', ' '.join(got), '| authors:', authors)
    if record:
        new = not os.path.exists(record)
        with open(record, 'a', encoding='utf-8') as f:
            if new:
                f.write('| asset | url | licence | author | fetched | maps | used for |\n|---|---|---|---|---|---|---|\n')
            f.write('\n'.join(rows) + '\n')
    return rows


if __name__ == '__main__':
    args = sys.argv[1:]
    if len(args) < 2 or '-h' in args or '--help' in args:
        print(__doc__)
        sys.exit(0 if '--help' in args or '-h' in args else 2)
    res = args[args.index('--res') + 1] if '--res' in args else '2k'
    rec = args[args.index('--record') + 1] if '--record' in args else None
    purpose = {}
    ids = []
    skip = 0
    for i, t in enumerate(args[1:], 1):
        if skip:
            skip -= 1
            continue
        if t in ('--res', '--record', '--maps'):
            skip = 1
        elif t == '--purpose':
            skip = 1
            k, _, v = args[i + 1].partition('=')
            purpose[k] = v
        else:
            ids.append(t)
    mp = args[args.index('--maps') + 1].split(',') if '--maps' in args else None
    fetch(args[0], ids, res, rec, purpose, mp)
