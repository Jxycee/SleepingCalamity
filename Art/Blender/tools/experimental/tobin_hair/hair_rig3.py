# blender -b -P hair_rig2.py   (MetaPerson Tobin base, Tobin_rigged_hair hair mesh fitted by skull box) -- <out_dir> [K=44] [bones_per_chain=4]
# Adds K spring chains (bones hair_<k>_<i>) under Head and re-weights Tobin_Hair: scalp-hugging vertices stay on Head, hanging ones follow their chain.
# Writes <out_dir>/Tobin_rigged_hair.blend + chains.json (chain geometry in the armature frame, for the simulator and the Unity component).
import bpy, bmesh, numpy as np, json, sys, os
from mathutils import Vector
from mathutils.kdtree import KDTree
a=sys.argv[sys.argv.index('--')+1:]; out=a[0]; kw=dict(x.split('=') for x in a[1:]); K=int(kw.get('K',44)); NB=int(kw.get('bones',4))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath='C:/AI/difflocks/head/Tobin_Base_v2_sliders.glb')
mp_arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; head=bpy.data.objects['AvatarHead']
SRC=kw.get('src','C:/AI/test_out/heroes/tobin/hair_v6/rig/Tobin_rigged_hair.blend')
with bpy.data.libraries.load(SRC) as (df,dt): dt.objects=['Tobin_Hair']
hair=dt.objects[0]; bpy.context.scene.collection.objects.link(hair)
for m in list(hair.modifiers): hair.modifiers.remove(m)
hair.vertex_groups.clear(); hair.parent=None
hv=np.array([(hair.matrix_world@v.co)[:] for v in hair.data.vertices]); hair.matrix_world=__import__('mathutils').Matrix.Identity(4)
# fit Tripo-head frame -> MetaPerson head frame (skull slab box): scale x/y, shift z by the crown height difference
SX,SY,CXT,CYT,CXM,CYM,DZ=0.1788/0.1807,0.2115/0.197,-0.0007,-0.0181,0.0001,-0.0205,1.8347-1.7562
if kw.get('fit','1')=='0': SX=SY=1.0; CXT=CXM=0.0; CYT=CYM=0.0; DZ=0.0   # strands/cards already in the MetaPerson frame
hv[:,0]=(hv[:,0]-CXT)*SX+CXM; hv[:,1]=(hv[:,1]-CYT)*SY+CYM; hv[:,2]+=DZ
# push hair out of the head: vertices inside the head surface or closer than --clear (m) are moved to that clearance along the surface normal
import bmesh as _bm
from mathutils.bvhtree import BVHTree
_bh=_bm.new(); _bh.from_mesh(head.data); _bh.transform(head.matrix_world); _bh.faces.ensure_lookup_table(); _tree=BVHTree.FromBMesh(_bh)
CLEAR=float(kw.get("clear",0.006)); _moved=0; _deep=0
for i in range(len(hv)):
    co=Vector(hv[i]); loc,nrm,_idx,dist=_tree.find_nearest(co,0.05)
    if loc is None: continue
    side=(co-loc).dot(nrm)
    if side<CLEAR:
        hv[i]=np.array(loc+nrm*CLEAR); _moved+=1; _deep+= side<0
print("PUSH moved",_moved,"of",len(hv),"inside",_deep)
for i,v in enumerate(hair.data.vertices): v.co=hv[i]
hair.data.update()
# orient every strip face outward (away from the head centre): hair material is single-sided now, back faces were the black/navy chunks
_bo=_bm.new(); _bo.from_mesh(hair.data); _bo.faces.ensure_lookup_table(); _hc=Vector((0.0,-0.005,1.70)); _flip=[f for f in _bo.faces if f.normal.dot(f.calc_center_median()-_hc)<0]
print("FLIPPED",len(_flip),"of",len(_bo.faces)); bmesh.ops.reverse_faces(_bo,faces=_flip); _bo.to_mesh(hair.data); _bo.free(); hair.data.update()
# debris removal: tiny faces, sliver triangles and faces whose texture sample is near-black (atlas holes) - all read as dirt on the finished hair
if float(kw.get("debris",1)):
    _img=bpy.data.images.load("C:/AI/test_out/heroes/tobin/hair_v6/tex/Hair_BaseColor.png"); _W,_H=_img.size; _px=np.array(_img.pixels[:],dtype=np.float32).reshape(_H,_W,4)
    _bd=_bm.new(); _bd.from_mesh(hair.data); _bd.faces.ensure_lookup_table(); _uv=_bd.loops.layers.uv.active; _del=[]; _nt=_ns=_nd=0
    for f in _bd.faces:
        e=[(f.verts[1].co-f.verts[0].co).length,(f.verts[2].co-f.verts[1].co).length,(f.verts[0].co-f.verts[2].co).length]
        a=f.calc_area(); q=4*3**0.5*a/max(sum(x*x for x in e),1e-18)
        u=sum((l[_uv].uv.x for l in f.loops))/3; v=sum((l[_uv].uv.y for l in f.loops))/3
        lum=float(_px[min(_H-1,max(0,int(v*_H))),min(_W-1,max(0,int(u*_W))),:3].mean())
        tiny=a<float(kw.get("tiny_a",1.0e-6)); sl=q<float(kw.get("shard_q",0.05)); dk=lum<float(kw.get("dark_l",0.085))
        c=f.calc_center_median(); face_zone=(c.z<float(kw.get("fz",1.745)) and c.y<-0.03 and abs(c.x)<float(kw.get("fx",0.058)))
        if face_zone:   # strips lying on the eye / cheek area (they used to poke through the face; the clearance push flattened them into decals)
            loc,nr,_i,dd=_tree.find_nearest(c,0.05)
            if loc is not None and dd<float(kw.get("fd",0.014)): _del.append(f); _nd+=1; continue
        if c.z<1.745 and c.y<-0.02 and abs(c.x)<0.095:   # outer eye corner / temple: anything hugging the skin within 7 mm
            loc,nr,_i,dd=_tree.find_nearest(c,0.05)
            if loc is not None and dd<0.007: _del.append(f); _nd+=1; continue
        if tiny or sl or dk: _del.append(f); _nt+=tiny; _ns+=sl; _nd+=dk
    print("DEBRIS removed",len(_del),"tiny",_nt,"sliver",_ns,"dark",_nd,"of",len(_bd.faces))
    bmesh.ops.delete(_bd,geom=_del,context="FACES"); bmesh.ops.delete(_bd,geom=[v for v in _bd.verts if not v.link_faces],context="VERTS"); _bd.to_mesh(hair.data); _bd.free(); hair.data.update()
    hv=np.array([v.co[:] for v in hair.data.vertices])
    # drop small isolated pieces left behind by the removal (welded components of <= --iso faces in the lower half of the hair)
    _bi=_bm.new(); _bi.from_mesh(hair.data); _bi.faces.ensure_lookup_table(); _kf=lambda v:(round(v.co.x/2e-6),round(v.co.y/2e-6),round(v.co.z/2e-6)); _par={}
    def _fd(a):
        while _par.setdefault(a,a)!=a: _par[a]=_par[_par[a]]; a=_par[a]
        return a
    for f in _bi.faces:
        ks=[_kf(v) for v in f.verts]; r0=_fd(ks[0])
        for k in ks[1:]: _par[_fd(k)]=r0
    _grp={}
    for f in _bi.faces: _grp.setdefault(_fd(_kf(f.verts[0])),[]).append(f)
    _isol=[f for g in _grp.values() if len(g)<=int(kw.get("iso",10)) and (sum(x.calc_center_median().z for x in g)/len(g))<float(kw.get("iso_z",1.78)) for f in g]
    print("ISOLATED removed",len(_isol),"in",sum(1 for g in _grp.values() if len(g)<=int(kw.get("iso",10))),"small pieces")
    bmesh.ops.delete(_bi,geom=_isol,context="FACES"); bmesh.ops.delete(_bi,geom=[v for v in _bi.verts if not v.link_faces],context="VERTS"); _bi.to_mesh(hair.data); _bi.free(); hair.data.update()
    hv=np.array([v.co[:] for v in hair.data.vertices])
# shard removal: faces that face INTO the head, sit within --shard_d of it and meet a neighbour at a sharp angle (torn strip tips, flipped fragments), plus sliver triangles
if float(kw.get("shards",0)):
    _bs=_bm.new(); _bs.from_mesh(hair.data); _bs.faces.ensure_lookup_table()
    _key=lambda v:(round(v.co.x/2e-6),round(v.co.y/2e-6),round(v.co.z/2e-6)); _vf={}
    for f in _bs.faces:
        for v in f.verts: _vf.setdefault(_key(v),[]).append(f.index)
    _nr=np.array([f.normal[:] for f in _bs.faces]); _bad=[]
    for f in _bs.faces:
        c=f.calc_center_median(); loc,nr,_i,d=_tree.find_nearest(c,0.05)
        e=[(f.verts[1].co-f.verts[0].co).length,(f.verts[2].co-f.verts[1].co).length,(f.verts[0].co-f.verts[2].co).length]; q=4*3**0.5*f.calc_area()/max(sum(x*x for x in e),1e-18)
        sliver=q<float(kw.get("shard_q",0.03)); inward=False; sharp=False
        if loc is not None and d<float(kw.get("shard_d",0.012)):
            inward=float(np.dot(_nr[f.index],np.array(nr)))<-0.2 and (c-loc).dot(nr)>0
            nb=set()
            for v in f.verts: nb.update(_vf[_key(v)])
            nb.discard(f.index); sharp=any(float(np.dot(_nr[f.index],_nr[j]))<float(np.cos(np.radians(float(kw.get("shard_a",60))))) for j in nb)
        if sliver or (inward and sharp): _bad.append(f)
    print("SHARDS removed",len(_bad),"of",len(_bs.faces))
    bmesh.ops.delete(_bs,geom=_bad,context="FACES"); bmesh.ops.delete(_bs,geom=[v for v in _bs.verts if not v.link_faces],context="VERTS")
    _bs.to_mesh(hair.data); _bs.free(); hair.data.update()
    hv=np.array([v.co[:] for v in hair.data.vertices])
hb=mp_arm.data.bones['Head']; HEAD_HEAD=np.array(mp_arm.matrix_world@hb.head_local); HEAD_TAIL=np.array(mp_arm.matrix_world@hb.tail_local)
# minimal armature: Head + chains, in world frame
bpy.ops.object.armature_add(enter_editmode=True,location=(0,0,0)); arm=bpy.context.active_object; arm.name='HairArmature'
for e in list(arm.data.edit_bones): arm.data.edit_bones.remove(e)
eb0=arm.data.edit_bones.new('Head'); eb0.head=Vector(HEAD_HEAD); eb0.tail=Vector(HEAD_TAIL); bpy.ops.object.mode_set(mode='OBJECT')
M=np.array(hair.matrix_world); V=np.array([v.co[:] for v in hair.data.vertices]); W=(M[:3,:3]@V.T).T+M[:3,3]
Mh=np.array(head.matrix_world); HV=np.array([v.co[:] for v in head.data.vertices]); HW=(Mh[:3,:3]@HV.T).T+Mh[:3,3]
kd=KDTree(len(HW))
for i,p in enumerate(HW): kd.insert(Vector(p),i)
kd.balance()
d=np.array([kd.find(Vector(p))[2] for p in W])
print('hair verts',len(W),'dist to head: pct',np.percentile(d,[5,25,50,75,95]).round(4),'max',d.max().round(3))
# free amount: near the scalp rigid, far hangs free (smoothstep 0.6 cm -> 3.5 cm)
t=np.clip((d-float(kw.get("r0",0.014)))/float(kw.get("rw",0.040)),0,1); f=t*t*(3-2*t)
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
bpy.context.view_layer.objects.active=arm; bpy.context.view_layer.objects.active=arm; bpy.ops.object.mode_set(mode='EDIT'); eb=arm.data.edit_bones; head_b=eb['Head']
for c in chains:
    prev=head_b
    for i in range(NB):
        b=eb.new(f'hair_{c["k"]}_{i}'); b.head=Vector(tochar(c['pts'][i])); b.tail=Vector(tochar(c['pts'][i+1])); b.parent=prev; b.use_connect=False; b.use_deform=(i>0); prev=b
bpy.ops.object.mode_set(mode='OBJECT')
# weights
for g in list(hair.vertex_groups):
    if g.name.startswith('hair_'): hair.vertex_groups.remove(g)
vg={c['k']:[hair.vertex_groups.new(name=f'hair_{c["k"]}_{i}') for i in range(NB)] for c in chains}
headg=hair.vertex_groups['Head'] if 'Head' in hair.vertex_groups else hair.vertex_groups.new(name='Head')
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

# scalp cap: faces of the head under dense hair coverage, pushed out 2.5 mm, flat dark hair colour - hides the gaps behind sparse strips
import bmesh
from mathutils.kdtree import KDTree as _KD
hkd=_KD(len(W))
for i,p in enumerate(W): hkd.insert(Vector(p),i)
hkd.balance()
bmc=bmesh.new(); bmc.from_mesh(head.data); bmc.faces.ensure_lookup_table(); bmc.verts.ensure_lookup_table()
def _ok(f):
    c=f.calc_center_median(); front=(c.y<-0.03 and abs(c.x)<float(kw.get("fx",0.058)))      # face area: only above the brow line; sides and back: above the ears
    zmin=float(kw.get("capzf",1.775)) if front else float(kw.get("capzs",1.75))
    return c.z>float(kw.get("capall",1.77)) or (c.z>zmin and len(hkd.find_range(c,float(kw.get("capr",0.022))))>=int(kw.get("capn",16)))   # whole crown is capped (parting gaps), lower down only where hair is dense
keepf=[f for f in bmc.faces if _ok(f)]
print("CAP faces",len(keepf),"of",len(bmc.faces))
drop=[f for f in bmc.faces if f not in set(keepf)]
bmesh.ops.delete(bmc,geom=drop,context="FACES")
bmesh.ops.delete(bmc,geom=[v for v in bmc.verts if not v.link_faces],context="VERTS")
bmc.normal_update()
for v in bmc.verts: v.co=v.co+v.normal*float(kw.get("capmm",0.0025))
cm=bpy.data.meshes.new("HairCap"); bmc.to_mesh(cm); bmc.free()
capo=bpy.data.objects.new("Tobin_HairCap",cm); bpy.context.scene.collection.objects.link(capo)
g=capo.vertex_groups.new(name="Head"); g.add(list(range(len(cm.vertices))),1.0,"REPLACE")
mod2=capo.modifiers.new("Armature","ARMATURE"); mod2.object=arm; capo.parent=arm
mod=hair.modifiers.new('Armature','ARMATURE'); mod.object=arm; hair.parent=arm
# preview helpers: face rigidly on the Head bone (no neck deformation), body hidden
for o in bpy.data.objects:
    if o.type=='MESH' and o.name in ('AvatarHead','AvatarLeftEyeball','AvatarRightEyeball','AvatarEyelashes','AvatarTeethLower','AvatarTeethUpper'):
        for m in list(o.modifiers): o.modifiers.remove(m)
        o.parent=arm; o.parent_type='BONE'; o.parent_bone='Head'
        o.matrix_parent_inverse=(arm.matrix_world@arm.data.bones['Head'].matrix_local@__import__('mathutils').Matrix.Translation((0,arm.data.bones['Head'].length,0))).inverted()   # bone parenting is relative to the bone TAIL
    elif o.type=='MESH' and o.name not in ('Tobin_Hair','Tobin_HairCap'): o.hide_render=True; o.hide_viewport=True
mp_arm.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out,'Tobin_mp_hair.blend'))
json.dump([dict(k=c['k'],bones=[f'hair_{c["k"]}_{i}' for i in range(NB)],pts_char=tochar(c['pts']).tolist()) for c in chains],open(os.path.join(out,'chains.json'),'w'))
print('RIG_DONE')
