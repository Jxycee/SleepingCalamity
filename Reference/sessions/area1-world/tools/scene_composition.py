# Streams a Unity YAML scene (never loads it whole) and prints what makes it big.
# usage: python scene_composition.py <scene.unity> [root GameObject name, default Hollowmere]
# Prints: total bytes; the ten biggest YAML classes (bytes, documents); the 25 largest embedded Mesh documents (!u!43) with m_Name and bytes;
# mesh bytes grouped by name stem (digits stripped); GameObject counts grouped by the path two levels below the root (from Transform parent links).
import sys, re, os, collections

CLASS_NAMES = {1: 'GameObject', 4: 'Transform', 21: 'Material', 23: 'MeshRenderer', 33: 'MeshFilter', 43: 'Mesh', 64: 'MeshCollider', 65: 'BoxCollider',
               20: 'Camera', 54: 'Rigidbody', 82: 'AudioSource', 114: 'MonoBehaviour', 137: 'SkinnedMeshRenderer', 198: 'ParticleSystem', 199: 'ParticleSystemRenderer',
               212: 'SpriteRenderer', 224: 'RectTransform', 1001: 'PrefabInstance', 111: 'Animation', 108: 'Light', 104: 'RenderSettings', 157: 'LightmapSettings', 28: 'Texture2D',
               136: 'CapsuleCollider', 135: 'SphereCollider', 120: 'LineRenderer', 205: 'LODGroup'}
hdr = re.compile(r'^--- !u!(\d+) &(-?\d+)')
path = sys.argv[1]; root_name = sys.argv[2] if len(sys.argv) > 2 else 'Hollowmere'
total = os.path.getsize(path)
cls_bytes = collections.Counter(); cls_count = collections.Counter()
meshes = []                     # (bytes, name)
go_name = {}; tr_go = {}; tr_father = {}
cur = None; cid = None; size = 0; mname = None; tgo = None; tfa = None
def close():
    global cur
    if cur is None: return
    cls_bytes[cur] += size; cls_count[cur] += 1
    if cur == 43: meshes.append((size, mname or '(unnamed)'))
    if cur in (4, 224) and tgo is not None:
        tr_go[cid] = tgo; tr_father[cid] = tfa
with open(path, 'rb') as f:
    for raw in f:
        if raw.startswith(b'--- !u!'):
            close()
            m = hdr.match(raw.decode('ascii', 'replace'))
            cur = int(m.group(1)); cid = m.group(2); size = 0; mname = None; tgo = None; tfa = None
            size += len(raw); continue
        size += len(raw)
        if cur == 1 and raw.startswith(b'  m_Name:'):
            go_name[cid] = raw[9:].decode('utf-8', 'replace').strip()
        elif cur == 43 and raw.startswith(b'  m_Name:'):
            mname = raw[9:].decode('utf-8', 'replace').strip()
        elif cur in (4, 224):
            if raw.startswith(b'  m_GameObject:'): tgo = raw.split(b'fileID:')[1].split(b'}')[0].strip().decode()
            elif raw.startswith(b'  m_Father:'): tfa = raw.split(b'fileID:')[1].split(b'}')[0].strip().decode()
close()
# GameObject paths
go_tr = {g: t for t, g in tr_go.items()}
def chain(g):
    names = []; t = go_tr.get(g); seen = 0
    while t is not None and seen < 200:
        names.append(go_name.get(tr_go.get(t), '?')); f = tr_father.get(t)
        t = f if f and f != '0' else None; seen += 1
    return names[::-1]
groups = collections.Counter(); under_root = 0
for g in go_name:
    p = chain(g)
    if not p or p[0] != root_name: continue
    under_root += 1
    groups['/'.join(p[1:3]) if len(p) > 1 else '(root)'] += 1
out = []
out.append('Scene %s: %d bytes (%.1f MB), %d documents, %d GameObjects (%d under "%s")' % (os.path.basename(path), total, total / 1048576, sum(cls_count.values()), cls_count[1], under_root, root_name))
out.append('\n## Ten biggest YAML classes')
for c, b in cls_bytes.most_common(10):
    out.append('- !u!%d %s: %d bytes (%.1f%%), %d docs' % (c, CLASS_NAMES.get(c, '?'), b, 100.0 * b / total, cls_count[c]))
out.append('\n## 25 largest Mesh documents')
for b, n in sorted(meshes, reverse=True)[:25]:
    out.append('- %s: %d bytes' % (n, b))
stem = collections.defaultdict(lambda: [0, 0])
for b, n in meshes:
    s = re.sub(r'(_?#)+$', '', re.sub(r'-?\d+', '#', n)) or n
    stem[s][0] += b; stem[s][1] += 1
out.append('\n## Mesh bytes by name stem (digit runs become #), top 15 of %d meshes, %d bytes total' % (len(meshes), sum(b for b, _ in meshes)))
for s, (b, k) in sorted(stem.items(), key=lambda kv: -kv[1][0])[:15]:
    out.append('- %s: %d bytes in %d meshes' % (s, b, k))
out.append('\n## GameObjects by path two levels below "%s" (top 40 of %d groups)' % (root_name, len(groups)))
for k, v in groups.most_common(40):
    out.append('- %s: %d' % (k, v))
print('\n'.join(out))
