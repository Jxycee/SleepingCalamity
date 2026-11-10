# blender -b Tobin_rigged_v2.blend -P hair_rig.py -- <out_dir> [K=44] [bones_per_chain=4]
# Adds K spring chains (bones hair_<k>_<i>) under mixamorig:Head and re-weights Tobin_Hair: scalp-hugging vertices stay on Head, hanging ones follow their chain.
# Writes <out_dir>/Tobin_rigged_hair.blend + chains.json (chain geometry in the armature frame, for the simulator and the Unity component).
import bpy, bmesh, numpy as np, json, sys, os
from mathutils import Vector
from mathutils.kdtree import KDTree
a=sys.argv[sys.argv.index('--')+1:]; out=a[0]; kw=dict(x.split('=') for x in a[1:]); K=int(kw.get('K',44)); NB=int(kw.get('bones',4))
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; hair=bpy.data.objects['Tobin_Hair']; head=bpy.data.objects['Tobin_Head']
M=np.array(hair.matrix_world); V=np.array([v.co[:] for v in hair.data.vertices]); W=(M[:3,:3]@V.T).T+M[:3,3]
Mh=np.array(head.matrix_world); HV=np.array([v.co[:] for v in head.data.vertices]); HW=(Mh[:3,:3]@HV.T).T+Mh[:3,3]
kd=KDTree(len(HW))
for i,p in enumerate(HW): kd.insert(Vector(p),i)
kd.balance()
d=np.array([kd.find(Vector(p))[2] for p in W])
print('hair verts',len(W),'dist to head: pct',np.percentile(d,[5,25,50,75,95]).round(4),'max',d.max().round(3))
# free amount: near the scalp rigid, far hangs free (smoothstep 0.6 cm -> 3.5 cm)
t=np.clip((d-0.006)/0.029,0,1); f=t*t*(3-2*t)
rng=np.random.default_rng(3); cen=W[rng.choice(len(W),K,replace=False)].copy()
for _ in range(25):
    lab=np.argmin(((W[:,None,:]-cen[None])**2).sum(2),1)
    for k in range(K):
        if (lab==k).any(): cen[k]=W[lab==k].mean(0)
inv=np.array(arm.matrix_world.inverted())
tochar=lambda P:(inv[:3,:3]@np.asarray(P).T).T+inv[:3,3]
chains=[]
for k in range(K):
    m=lab==k
    if m.sum()<30: continue
    P=W[m]; dd=d[m]; root=P[dd<=np.percentile(dd,10)].mean(0); far=P[dd>=np.percentile(dd,92)].mean(0)
    if np.linalg.norm(far-root)<0.012: continue
    pts=np.array([root+(far-root)*i/NB for i in range(NB+1)]); chains.append(dict(k=k,pts=pts,idx=np.where(m)[0]))
print('chains',len(chains))
bpy.context.view_layer.objects.active=arm; bpy.ops.object.mode_set(mode='EDIT'); eb=arm.data.edit_bones; head_b=eb['mixamorig:Head']
for c in chains:
    prev=head_b
    for i in range(NB):
        b=eb.new(f'hair_{c["k"]}_{i}'); b.head=Vector(tochar(c['pts'][i])); b.tail=Vector(tochar(c['pts'][i+1])); b.parent=prev; b.use_connect=False; b.use_deform=(i>0); prev=b
bpy.ops.object.mode_set(mode='OBJECT')
# weights
for g in list(hair.vertex_groups):
    if g.name.startswith('hair_'): hair.vertex_groups.remove(g)
vg={c['k']:[hair.vertex_groups.new(name=f'hair_{c["k"]}_{i}') for i in range(NB)] for c in chains}
headg=hair.vertex_groups['mixamorig:Head'] if 'mixamorig:Head' in hair.vertex_groups else hair.vertex_groups.new(name='mixamorig:Head')
N=len(W); wt=np.zeros((N,len(chains)*NB)); 
for ci,c in enumerate(chains):
    P=c['pts']; seg=P[1:]-P[:-1]; sl=np.linalg.norm(seg,axis=1); cum=np.concatenate([[0],np.cumsum(sl)]); L=cum[-1]
    X=W[c['idx']]; ax=(P[-1]-P[0])/L; s=np.clip(((X-P[0])@ax)/L,0,1)*NB      # 0..NB along the chain
    for j,vi in enumerate(c['idx']):
        sj=s[j]; lo=int(min(np.floor(sj),NB-1)); fr=sj-lo
        wt[vi,ci*NB+lo]+=(1-fr)*f[vi]; 
        if lo+1<NB: wt[vi,ci*NB+lo+1]+=fr*f[vi]
        else: wt[vi,ci*NB+lo]+=fr*f[vi]
# smooth over mesh adjacency so chain borders do not tear
E=np.array([e.vertices[:] for e in hair.data.edges]); dg=np.maximum(np.bincount(np.concatenate([E[:,0],E[:,1]]),minlength=N),1)
for _ in range(6):
    acc=np.zeros_like(wt); np.add.at(acc,E[:,0],wt[E[:,1]]); np.add.at(acc,E[:,1],wt[E[:,0]]); wt=0.5*wt+0.5*acc/dg[:,None]
for vi in range(N):
    row=wt[vi]; top=np.argsort(-row)[:3]; tot=row[top].sum(); tot=min(tot,1.0)
    sc=(tot/row[top].sum()) if row[top].sum()>0 else 0
    headw=1-tot
    for ti in top:
        w=row[ti]*sc
        if w>0.004: ci,bi=divmod(ti,NB); vg[chains[ci]['k']][bi].add([vi],float(w),'REPLACE')
    headg.add([vi],float(headw),'REPLACE')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out,'Tobin_rigged_hair.blend'))
json.dump([dict(k=c['k'],bones=[f'hair_{c["k"]}_{i}' for i in range(NB)],pts_char=tochar(c['pts']).tolist()) for c in chains],open(os.path.join(out,'chains.json'),'w'))
print('RIG_DONE')
