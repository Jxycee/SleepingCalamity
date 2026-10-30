"""Fetch free 3D models and decals into Art/Source/<Provider>/<id>/ with a source record (sc-asset-adopt).
python3 Art/Blender/tools/sc_fetch_models.py Art/Source/fetch_list.txt [--only id,id] [--res 2k] [--max-tris 150000] [--dry-run]

The list has one asset per line: `<provider> <id> <batch> <purpose...>` ('#' starts a comment). Providers:
  polyhaven   CC0 1.0. glTF at --res with its textures (api.polyhaven.com /info and /files).
  ambientcg   CC0 1.0. The <res>-JPG zip (decals, atlases, materials), extracted (ambientcg.com api v2).
  simready    CC BY 4.0, NVIDIA SimReady on Hugging Face. geometries.usd, its PBR PNGs (downscaled to --res) and LICENSE.txt.
              Attribution required: the SOURCES row carries the credit line for the game credits.
Per asset it writes the files and appends a row to Art/Source/<Provider>/SOURCES.md: asset, URL, licence, author/credit,
fetched date, triangles, batch, purpose, what we changed. Assets already on disk are skipped. A Poly Haven model over
--max-tris is recorded as 'not fetched: over budget, fetch on demand' (a bake source, not worth the LFS weight).
Downloads only from the hosts in HOSTS. Raw sources are never put under Assets/ (sc-asset-adopt)."""
import datetime, io, json, os, sys, urllib.request, zipfile

UA = {'User-Agent': 'SleepingCalamity-fetch/1.0'}
HOSTS = ('api.polyhaven.com', 'dl.polyhaven.org', 'ambientcg.com', 'huggingface.co', 'cdn-lfs.huggingface.co', 'cdn-lfs-us-1.huggingface.co',
         'cas-bridge.xethub.hf.co', 'cdn-lfs-us-1.hf.co', 'cdn-lfs.hf.co')
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
SRC = os.path.join(REPO, 'Art', 'Source')
PROVIDER_DIR = {'polyhaven': 'PolyHaven', 'ambientcg': 'ambientCG', 'simready': 'NVIDIA_SimReady'}
LICENCE = {'polyhaven': 'CC0 1.0 (polyhaven.com/license)', 'ambientcg': 'CC0 1.0 (ambientcg.com/license)',
           'simready': 'CC BY 4.0 (asset .metadata/LICENSE.txt; dataset card)'}
HEADER = ('| asset | source | licence | author / credit | fetched | tris | batch | purpose | what we changed |\n'
          '|---|---|---|---|---|---|---|---|---|\n')
SR = 'https://huggingface.co/datasets/nvidia/simready-assets'


def get(url, timeout=300):
    host = url.split('/')[2]
    if host not in HOSTS:
        raise SystemExit(f'refusing host {host}: only {HOSTS}')
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=timeout).read()


def getj(url):
    return json.loads(get(url, 120))


def write(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as f:
        f.write(data)


def downscale_png(data, res_px):
    """Shrink a texture to res_px on its long side (keeps PNG, keeps 16-bit normals as 8-bit RGB)."""
    from PIL import Image
    im = Image.open(io.BytesIO(data))
    if max(im.size) <= res_px:
        return data, im.size
    s = res_px / max(im.size)
    if im.mode not in ('RGB', 'RGBA', 'L'):
        im = im.convert('RGBA' if 'A' in im.mode else 'RGB')
    im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    out = io.BytesIO(); im.save(out, 'PNG', optimize=True)
    return out.getvalue(), im.size


def record(provider, row):
    path = os.path.join(SRC, PROVIDER_DIR[provider], 'SOURCES.md')
    if not os.path.exists(path):
        title = f'# {PROVIDER_DIR[provider]} sources\n\nRaw downloads for adoption (sc-asset-adopt). Written by `Art/Blender/tools/sc_fetch_models.py`; one row per asset.\n\n'
        write(path, (title + HEADER).encode())
    if f'| {row[0]} |' in open(path, encoding='utf-8').read():
        return                                          # one row per asset (a re-run re-checks over-budget entries)
    with open(path, 'a', encoding='utf-8') as f:
        f.write('| ' + ' | '.join(str(c).replace('|', '/') for c in row) + ' |\n')


def polyhaven(aid, res, max_tris, dry):
    info = getj(f'https://api.polyhaven.com/info/{aid}')
    tris = info.get('polycount', '?'); authors = ', '.join(info.get('authors', {}).keys())
    url = f'https://polyhaven.com/a/{aid}'
    if isinstance(tris, int) and tris > max_tris:
        return dict(url=url, author=authors, tris=tris, changed=f'not fetched: over budget ({tris} tris), fetch on demand with --max-tris')
    files = getj(f'https://api.polyhaven.com/files/{aid}')
    g = files['gltf'][res]['gltf']
    if dry:
        return dict(url=url, author=authors, tris=tris, changed='(dry run)', size=g['size'] + sum(v['size'] for v in g.get('include', {}).values()))
    out = os.path.join(SRC, 'PolyHaven', aid)
    write(os.path.join(out, os.path.basename(g['url'])), get(g['url']))
    for rel, v in g.get('include', {}).items():
        write(os.path.join(out, rel), get(v['url']))
    return dict(url=url, author=authors, tris=tris, changed=f'none (glTF {res} as published)')


def ambientcg(aid, res, max_tris, dry):
    info = getj(f'https://ambientcg.com/api/v2/full_json?id={aid}&include=downloadData')['foundAssets'][0]
    want = f'{res.upper()}-JPG'
    dl = [d for c in info['downloadFolders'].values() for cat in c['downloadFiletypeCategories'].values() for d in cat['downloads']
          if d.get('attribute') == want]
    if not dl:
        raise RuntimeError(f'no {want} download for {aid}')
    url = f'https://ambientcg.com/view?id={aid}'
    if dry:
        return dict(url=url, author='ambientCG (Lennart Demes)', tris='-', changed='(dry run)', size=dl[0].get('size', 0))
    z = zipfile.ZipFile(io.BytesIO(get(dl[0]['downloadLink'])))
    out = os.path.join(SRC, 'ambientCG', aid)
    for n in z.namelist():
        if n.lower().endswith(('.jpg', '.png')) and not n.lower().endswith('.usdc'):
            write(os.path.join(out, os.path.basename(n)), z.read(n))
    return dict(url=url, author='ambientCG (Lennart Demes)', tris='-', changed=f'none ({want} zip, textures extracted)')


def simready(aid, res, max_tris, dry):
    tree = getj(f'https://huggingface.co/api/datasets/nvidia/simready-assets/tree/main/{aid}?recursive=1')
    files = {f['path']: f.get('size', 0) for f in tree if f.get('type') == 'file'}
    keep = [p for p in files if p.endswith('/payloads/geometries.usd') or p.endswith('LICENSE.txt')
            or ('/simready_usd/Textures/' in p and p.lower().endswith('.png')) or p.endswith('_thumbnail.png')]
    url = f'{SR}/tree/main/{aid}'
    credit = f'"{aid}" by NVIDIA, SimReady Assets, CC BY 4.0, modified'
    if dry:
        return dict(url=url, author=credit, tris='?', changed='(dry run)', size=sum(files[p] for p in keep))
    px = {'1k': 1024, '2k': 2048, '4k': 4096}[res]; out = os.path.join(SRC, 'NVIDIA_SimReady', aid); sizes = []
    for p in keep:
        data = get(f'{SR}/resolve/main/{p}')
        if '/Textures/' in p:
            data, sz = downscale_png(data, px); sizes.append(sz)
        write(os.path.join(out, os.path.basename(p)), data)
    return dict(url=url, author=credit, tris='?', changed=f'textures downscaled to {px} px long side; physics payloads and MDL not kept')


FETCH = {'polyhaven': polyhaven, 'ambientcg': ambientcg, 'simready': simready}


def main(argv):
    if not argv or argv[0].startswith('--'):
        print(__doc__); return 4
    def opt(k, d):
        return argv[argv.index(k) + 1] if k in argv else d
    res = opt('--res', '2k'); max_tris = int(opt('--max-tris', '150000')); dry = '--dry-run' in argv
    only = set(opt('--only', '').split(',')) - {''}
    day = datetime.date.today().isoformat(); total = 0; failed = []
    for line in open(argv[0], encoding='utf-8'):
        line = line.split('#', 1)[0].strip()
        if not line:
            continue
        provider, aid, batch, *purpose = line.split()
        if only and aid not in only:
            continue
        out = os.path.join(SRC, PROVIDER_DIR[provider], aid)
        if os.path.isdir(out) and os.listdir(out) and not dry:
            print('skip (on disk)', provider, aid); continue
        try:
            r = FETCH[provider](aid, res, max_tris, dry)
        except Exception as e:
            print('FAIL', provider, aid, e); failed.append(aid); continue
        total += r.get('size', 0)
        print(provider, aid, r['tris'], r['changed'], f"{r.get('size', 0) / 1e6:.1f} MB" if dry else '')
        if not dry:
            record(provider, [aid, r['url'], LICENCE[provider], r['author'], day, r['tris'], batch, ' '.join(purpose), r['changed']])
    if dry:
        print(f'total {total / 1e6:.0f} MB')
    if failed:
        print('failed:', ', '.join(failed))
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
