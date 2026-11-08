"""Tobin base: build the anatomical underlay (torso, arms, hips, thighs) for a MetaPerson export, bind it to the rig, add body sliders, clamp it inside the clothing, run the leak test and export.
Usage: blender -b --factory-startup --python sc_tobin_underlay_build.py -- <metaperson model.glb> <out dir>
Needs the MetaPerson GLB with outfit_top / outfit_bottom / AvatarBody meshes and the 73-joint rig (file 3 in docs/research/2026-11-08-character-pipeline.md section 0g).
Written by a cloud session 2026-11-08, never run in Unity. Final version of the sculpt (mannequin -> muscle forms -> clamp inside clothes -> sliders -> leak test).
"""
import bpy,sys,bmesh,os
import numpy as np
src,outdir=sys.argv[-2],sys.argv[-1]; os.makedirs(outdir,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
sc=bpy.context.scene; ob=bpy.data.objects
arm=[o for o in ob if o.type=='ARMATURE'][0]
def B(n): return np.array(arm.matrix_world@arm.data.bones[n].head_local)
SKIN=(218,167,143)
def lin(v): return (v/255)**2.2
mat=bpy.data.materials.new('BaseSkinUnderlay'); mat.use_nodes=True
mat.node_tree.nodes['Principled BSD'.replace('BSD','BSDF')].inputs['Base Color'].default_value=(lin(SKIN[0]),lin(SKIN[1]),lin(SKIN[2]),1)
mat.diffuse_color=(lin(SKIN[0]),lin(SKIN[1]),lin(SKIN[2]),1)
real=ob['AvatarBody']
def wco(o):
    n=len(o.data.vertices); a=np.empty(n*3,np.float32); o.data.vertices.foreach_get('co',a); a=a.reshape(n,3)
    M=np.array(o.matrix_world); return a@M[:3,:3].T+M[:3,3]
rw=wco(real)
# ---- measured real-skin join radius/position per limb ----
def point_at_raw(P,L,tot,s):
    d=s*tot; cum=0
    for (p,q),l in zip(P,L):
        if d<=cum+l+1e-9: return p+(q-p)*((d-cum)/l),(q-p)/l
        cum+=l
    return P[-1][1],(P[-1][1]-P[-1][0])/L[-1]
def limb(chain):
    P=[(B(a),B(b)) for a,b in chain]; L=[np.linalg.norm(q-p) for p,q in P]; tot=sum(L)
    best=np.full(len(rw),1e9); T=np.zeros(len(rw)); R=np.zeros(len(rw)); cum=0
    for (p,q),l in zip(P,L):
        v=q-p; t=np.clip(((rw-p)@v)/(v@v),0,1); c=p+t[:,None]*v; d=np.linalg.norm(rw-c,axis=1); m=d<best
        best[m]=d[m]; T[m]=(cum+t[m]*l)/tot; R[m]=d[m]; cum+=l
    ok=best<0.12; ts=T[ok]; rs=R[ok]; tmin=float(np.percentile(ts,2)); sel=(ts>=tmin)&(ts<=tmin+0.12)
    rj=float(np.median(rs[sel]))
    # ring centre/radius measured in the plane perpendicular to the limb at the join
    tsl=tmin+0.035; c0,d0=point_at_raw(P,L,tot,tsl)
    slab=(T>=tmin)&(T<=tmin+0.07)&(best<0.12); pts=rw[slab]; cm=pts.mean(0)
    perp=pts-cm; perp-=(perp@d0)[:,None]*d0[None,:]; rmeas=float(np.median(np.linalg.norm(perp,axis=1)))
    off=cm-c0; off-=(off@d0)*d0
    return P,L,tot,tmin,rmeas,off,tsl
CH={'armL':[('LeftArm','LeftForeArm'),('LeftForeArm','LeftHand')],'armR':[('RightArm','RightForeArm'),('RightForeArm','RightHand')],
    'legL':[('LeftUpLeg','LeftLeg'),('LeftLeg','LeftFoot')],'legR':[('RightUpLeg','RightLeg'),('RightLeg','RightFoot')]}
LM={k:limb(v) for k,v in CH.items()}
def point_at(P,L,tot,s):
    d=s*tot; cum=0
    for (p,q),l in zip(P,L):
        if d<=cum+l+1e-9: return p+(q-p)*((d-cum)/l), (q-p)/l
        cum+=l
    return P[-1][1], (P[-1][1]-P[-1][0])/L[-1]
N=40
def frame(d):
    u=np.array([0,-1.0,0]); e1=u-(u@d)*d
    if np.linalg.norm(e1)<1e-3: e1=np.array([1.0,0,0])-(np.array([1.0,0,0])@d)*d
    e1/=np.linalg.norm(e1); e2=np.cross(d,e1); return e1,e2
def tube(name,rings,closed_top=False):
    """rings: list of (N,3) arrays -> open tube mesh object"""
    me=bpy.data.meshes.new(name); bm=bmesh.new(); vs=[]
    for r in rings: vs.append([bm.verts.new(tuple(p)) for p in r])
    for i in range(len(rings)-1):
        for j in range(N):
            a,b=vs[i][j],vs[i][(j+1)%N]; c,d=vs[i+1][(j+1)%N],vs[i+1][j]
            bm.faces.new((a,b,c,d))
    bm.to_mesh(me); bm.free(); o=bpy.data.objects.new(name,me); sc.collection.objects.link(o); return o
# ---- Stage 1: the mannequin (clean lofted rings along the skeleton) ----
TOR=[(0.90,0.172,0.104),(0.98,0.186,0.108),(1.04,0.168,0.100),(1.08,0.140,0.092),(1.20,0.150,0.098),(1.31,0.168,0.112),(1.40,0.178,0.105),(1.46,0.176,0.092),(1.50,0.150,0.078),(1.53,0.100,0.066),(1.545,0.056,0.052),(1.565,0.046,0.046)]
zsp=[B('Hips')[2],B('Spine')[2],B('Spine1')[2],B('Spine2')[2],B('Neck')[2]]; ysp=[B('Hips')[1],B('Spine')[1],B('Spine1')[1],B('Spine2')[1],B('Neck')[1]]
def sellipse(rx,rz,k=2.6):
    ph=np.linspace(0,2*np.pi,N,endpoint=False); c,s=np.cos(ph),np.sin(ph); e=2.0/k
    return rx*np.sign(c)*np.abs(c)**e, rz*np.sign(s)*np.abs(s)**e
zs=np.linspace(0.90,1.565,40); rings=[]
for z in zs:
    rx=np.interp(z,[t[0] for t in TOR],[t[1] for t in TOR]); rz=np.interp(z,[t[0] for t in TOR],[t[2] for t in TOR])
    cy=np.interp(z,zsp,ysp); X,Y=sellipse(rx,rz); rings.append(np.stack([X,cy+Y,np.full(N,z)],1))
torso=tube('BaseUnderlay_Torso',rings)
def limb_rings(k,kind,overlap=0.025):
    P,L,tot,tmin,rj,off,tsl=LM[k]; send=tmin+overlap/tot; s0=-(0.03 if kind=='arm' else 0.06)/tot; ss=np.linspace(s0,send,int(tot*(send-s0)/0.015)+2); out=[]
    prox=0.054 if kind=='arm' else 0.092
    for s in ss:
        c,d=point_at(P,L,tot,s); f=min(max(s,0)/tmin,1.0)
        R=prox+(rj-prox)*f
        R*=1+(0.03 if kind=='arm' else 0.08)*np.exp(-((s-tmin*0.5)/(tmin*0.35))**2)
        if s>tmin: R=rj-0.004
        c=c+off*np.clip(max(s,0)/tsl,0,1)
        if s<0: R=R*(0.78+0.22*(1+s*tot/(0.03 if kind=='arm' else 0.06)))
        e1,e2=frame(d); ph=np.linspace(0,2*np.pi,N,endpoint=False)
        out.append(np.array([c+R*(np.cos(a)*e1+np.sin(a)*e2) for a in ph]))
    return out
armsL=tube('arm_L',limb_rings('armL','arm')); armsR=tube('arm_R',limb_rings('armR','arm'))
legL=tube('leg_L',limb_rings('legL','leg')); legR=tube('leg_R',limb_rings('legR','leg'))
for o in (torso,armsL,armsR,legL,legR):
    bm=bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm,faces=bm.faces); bm.to_mesh(o.data); bm.free()
def join(objs,name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]; bpy.ops.object.join(); objs[0].name=name; objs[0].data.name=name; return objs[0]
UT=join([torso,armsL,armsR],'BaseUnderlay_Torso'); UL=join([legL,legR],'BaseUnderlay_Legs')
# make faces point outward (test against the nearest bone axis)
def orient_out(o):
    segs=[(B(a),B(b)) for a,b in [('Hips','Spine'),('Spine','Spine1'),('Spine1','Spine2'),('Spine2','Neck'),('LeftArm','LeftForeArm'),('LeftForeArm','LeftHand'),('RightArm','RightForeArm'),('RightForeArm','RightHand'),('LeftUpLeg','LeftLeg'),('LeftLeg','LeftFoot'),('RightUpLeg','RightLeg'),('RightLeg','RightFoot')]]
    bm=bmesh.new(); bm.from_mesh(o.data); fl=[]
    for f in bm.faces:
        c=np.array(f.calc_center_median()); n=np.array(f.normal); best=1e9; cp=None
        for p,q in segs:
            v=q-p; t=np.clip(((c-p)@v)/(v@v),0,1); pt=p+t*v; d=np.linalg.norm(c-pt)
            if d<best: best=d; cp=pt
        if n@(c-cp)<0: fl.append(f)
    bmesh.ops.reverse_faces(bm,faces=fl); bm.to_mesh(o.data); bm.free(); return len(fl)
print('ORIENT',orient_out(UT),orient_out(UL))
for o in (UT,UL):
    o.data.materials.clear(); o.data.materials.append(mat); o['sc_role']='underlay_hide_when_clothed'
    for p in o.data.polygons: p.use_smooth=True

# ---- bind to the skeleton: copy vertex-group weights from the clothing shells by nearest surface ----
def bind(U,shells):
    for g in arm.data.bones: U.vertex_groups.new(name=g.name)
    for sname in shells:
        sh=ob[sname]
        dt=U.modifiers.new('DT_'+sname,'DATA_TRANSFER'); dt.object=sh; dt.use_vert_data=True; dt.data_types_verts={'VGROUP_WEIGHTS'}
        dt.vert_mapping='POLYINTERP_NEAREST'; dt.layers_vgroup_select_src='ALL'; dt.layers_vgroup_select_dst='NAME'; dt.mix_mode='REPLACE'
        bpy.context.view_layer.objects.active=U
        bpy.ops.object.modifier_apply(modifier=dt.name)
    m=U.modifiers.new('Armature','ARMATURE'); m.object=arm
    U.parent=arm; U.matrix_parent_inverse=arm.matrix_world.inverted()
bind(UT,['outfit_top']); bind(UL,['outfit_bottom'])
for U in (UT,UL):
    # normalise to at most 4 influences
    bpy.context.view_layer.objects.active=U; bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL',limit=4); bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)

# ---- rendering ----
sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='STUDIO'; sc.display.shading.color_type='TEXTURE'
sc.world=bpy.data.worlds.new('w'); sc.world.color=(0.30,0.30,0.33)
cam=bpy.data.objects.new('c',bpy.data.cameras.new('c')); sc.collection.objects.link(cam); cam.data.type='ORTHO'; sc.camera=cam
def shoot(tag,res=(480,760),scale=2.1,z=0.92):
    for v,(loc,rot) in {'front':((0,-6,z),(1.5708,0,0)),'side':((6,0,z),(1.5708,0,1.5708)),'back':((0,6,z),(1.5708,0,3.14159))}.items():
        cam.data.ortho_scale=scale; cam.location=loc; cam.rotation_euler=rot; sc.render.resolution_x,sc.render.resolution_y=res
        sc.render.filepath=f'{outdir}/{tag}_{v}.png'; bpy.ops.render.render(write_still=True)
for o in ob:
    if o.type=='MESH' and o.name.startswith('outfit'): o.hide_render=True
shoot('m1_mannequin')
# ---- Stage 2: muscle forms (displacement along normals; amplitudes in metres) ----
def normals_w(o):
    n=len(o.data.vertices); a=np.empty(n*3,np.float32); o.data.vertex_normals.foreach_get('vector',a); return a.reshape(n,3)
def g(x,c,s): return np.exp(-((x-c)/s)**2)
def muscles(o):
    w=wco(o); nrm=normals_w(o); x,y,z=w[:,0],w[:,1],w[:,2]; ax=np.abs(x); amp=np.zeros(len(w))
    fr=np.clip(-nrm[:,1],0,1); bk=np.clip(nrm[:,1],0,1); up=np.clip(nrm[:,2],0,1)
    tor=(z>0.93)&(z<1.56)&(ax<0.20)
    amp+=tor*fr*(0.020*g(ax,0.085,0.060)*g(z,1.385,0.050)-0.006*g(z,1.325,0.010)*g(ax,0.09,0.07))     # pecs, lower border
    amp+=tor*fr*(-0.006*g(ax,0.0,0.007)*((z>1.04)&(z<1.34)))                                         # linea alba
    for zc in (1.285,1.215,1.150,1.090): amp+=tor*fr*0.0045*g(ax,0.036,0.026)*g(z,zc,0.024)          # rectus segments
    amp+=tor*fr*0.008*g(ax,0.12,0.03)*g(z,1.24,0.055)                                                 # obliques/serratus
    amp+=tor*bk*0.018*g(ax,0.105,0.065)*g(z,1.285,0.065)                                              # lats
    amp+=tor*(bk+up)*0.015*g(ax,0.07,0.05)*g(z,1.48,0.04)                                             # traps
    amp+=tor*bk*0.008*g(ax,0.10,0.045)*g(z,1.385,0.04)                                                # scapulae
    amp-=tor*bk*0.006*g(ax,0.0,0.008)*((z>1.05)&(z<1.47))                                             # spine groove
    amp+=((z>0.90)&(z<1.08))*bk*0.026*g(ax,0.085,0.065)*g(z,0.99,0.06)                                # glutes
    th=(z<1.0)&(z>0.60)
    amp+=th*(fr*0.010*g(ax,0.105,0.05)*g(z,0.80,0.12)+bk*0.009*g(ax,0.09,0.05)*g(z,0.78,0.12))       # quads / hamstrings
    for nm in ('LeftArm','RightArm'):
        d=np.linalg.norm(w-B(nm),axis=1); amp+=0.004*np.exp(-(d/0.055)**2)*(up+0.5*fr+0.5*bk)         # deltoids
    return amp,nrm
for U in (UT,UL):
    amp,nrm=muscles(U); w=wco(U)+nrm*amp[:,None]
    U.data.vertices.foreach_set('co',w.astype(np.float32).reshape(-1)); U.data.update()
shoot('m2_muscles')


# ---- Stage 3: keep the body inside the clothes (measured against the clothing's own surface) ----
from mathutils.bvhtree import BVHTree
from mathutils import Vector
def garment_bvh(names):
    V=[];F=[]
    for n in names:
        o=ob[n]; M=o.matrix_world; base=len(V)
        V+=[tuple(M@v.co) for v in o.data.vertices]; F+=[tuple(base+i for i in p.vertices) for p in o.data.polygons]
    return BVHTree.FromPolygons(V,F)
BV=garment_bvh(['outfit_top','outfit_bottom'])
AXES=[(B(a),B(b)) for a,b in [('Hips','Spine'),('Spine','Spine1'),('Spine1','Spine2'),('Spine2','Neck'),('LeftArm','LeftForeArm'),('LeftForeArm','LeftHand'),('RightArm','RightForeArm'),('RightForeArm','RightHand'),('LeftUpLeg','LeftLeg'),('LeftLeg','LeftFoot'),('RightUpLeg','RightLeg'),('RightLeg','RightFoot')]]
GAP=0.008
def clamp_inside(o):
    w=wco(o); moved=0
    for i,p in enumerate(w):
        best=1e9; cp=None
        for a,b in AXES:
            v=b-a; t=np.clip(((p-a)@v)/(v@v),0,1); q=a+t*v; d=np.linalg.norm(p-q)
            if d<best: best=d; cp=q
        if best<1e-5: continue
        dirv=(p-cp)/best
        hit=BV.ray_cast(Vector(cp),Vector(dirv),best+0.30)
        if hit[0] is None: continue
        dfirst=(np.array(hit[0])-cp)@dirv
        if best>dfirst-GAP:
            w[i]=cp+dirv*max(dfirst-GAP,0.01); moved+=1
    o.data.vertices.foreach_set('co',w.astype(np.float32).reshape(-1)); o.data.update(); return moved
def smooth_o(o,it):
    w=wco(o); me=o.data; e=np.empty(len(me.edges)*2,np.int32); me.edges.foreach_get('vertices',e); e=e.reshape(-1,2); n=len(w)
    for _ in range(it):
        sm=np.zeros_like(w); c=np.zeros(n); np.add.at(sm,e[:,0],w[e[:,1]]); np.add.at(sm,e[:,1],w[e[:,0]]); np.add.at(c,e[:,0],1); np.add.at(c,e[:,1],1)
        w=w+0.5*(sm/np.maximum(c,1)[:,None]-w)
    o.data.vertices.foreach_set('co',w.astype(np.float32).reshape(-1)); o.data.update()
for U in (UT,UL):
    m1=clamp_inside(U); smooth_o(U,2); m2=clamp_inside(U); print('CLAMP',U.name,m1,m2)
for o in ob:
    if o.type=='MESH' and o.name.startswith('outfit'): o.hide_render=True
sc.display.shading.light='STUDIO'
shoot('m3_clamped')

# ---- body-slider shape keys on the underlay: copy the clothing/skin deltas by nearest neighbours ----
from mathutils.kdtree import KDTree
SRC=['outfit_top','outfit_bottom','AvatarBody']
def wmat(o): return np.array(o.matrix_world)[:3,:3]
src_pts=[]; src_idx=[]
for si,n in enumerate(SRC):
    o=ob[n]; P=wco(o); src_pts.append(P); src_idx+= [(si,i) for i in range(len(P))]
allP=np.vstack(src_pts); kd=KDTree(len(allP))
for i,p in enumerate(allP): kd.insert(tuple(p),i)
kd.balance()
names=[]
for n in SRC:
    for kb in ob[n].data.shape_keys.key_blocks:
        if kb.name!='Basis' and kb.name not in names: names.append(kb.name)
print('SLIDERS',names)
def key_delta_world(o,name):
    me=o.data; n=len(me.vertices); d=np.zeros((n,3),np.float32)
    if me.shape_keys and name in me.shape_keys.key_blocks:
        kb=me.shape_keys.key_blocks[name]; b=me.shape_keys.key_blocks[0]
        a=np.empty(n*3,np.float32); c=np.empty(n*3,np.float32); kb.data.foreach_get('co',a); b.data.foreach_get('co',c)
        d=((a-c).reshape(n,3))@wmat(o).T
    return d
NN=6
def build_keys(U):
    pw=wco(U); n=len(pw); nn=[kd.find_n(tuple(p),NN) for p in pw]
    idx=np.array([[x[1] for x in r] for r in nn]); dist=np.array([[x[2] for x in r] for r in nn])
    w=1.0/np.maximum(dist,1e-3)**2; w/=w.sum(1,keepdims=True)
    if not U.data.shape_keys: U.shape_key_add(name='Basis',from_mix=False)
    basis=np.empty(n*3,np.float32); U.data.shape_keys.key_blocks[0].data.foreach_get('co',basis); basis=basis.reshape(n,3)
    Minv=np.linalg.inv(wmat(U))
    for name in names:
        full=np.vstack([key_delta_world(ob[sn],name) for sn in SRC])
        d=(full[idx]*w[:,:,None]).sum(1)
        kb=U.shape_key_add(name=name,from_mix=False); kb.data.foreach_set('co',(basis+d@Minv.T).astype(np.float32).reshape(-1))
for U in (UT,UL): build_keys(U)
print('KEYS',len(UT.data.shape_keys.key_blocks),len(UL.data.shape_keys.key_blocks))

# ---- leak test: clothes on, with and without the underlay (must be pixel-identical) ----
import numpy as _np
from PIL import Image as _I
for o in ob:
    if o.type=='MESH' and o.name.startswith('outfit'): o.hide_render=False
sc.display.shading.light='FLAT'
def hide_under(h):
    for U in (UT,UL): U.hide_render=h
def setk(vals):
    for o in ob:
        if o.type=='MESH' and o.data.shape_keys:
            for k in o.data.shape_keys.key_blocks:
                if k.name!='Basis': k.value=vals.get(k.name,0.0)
PLUS={k:1.0 for k in names if k.endswith('+')}; MINUS={k:1.0 for k in names if k.endswith('-')}
res={}
for tag,vals in (('neutral',{}),('plus',PLUS),('minus',MINUS)):
    setk(vals); hide_under(True); shoot('t_off_'+tag,res=(700,1100)); hide_under(False); shoot('t_on_'+tag,res=(700,1100)); tot=[]
    for v in ('front','side','back'):
        A=_np.array(_I.open(f'{outdir}/t_off_{tag}_{v}.png').convert('RGB')).astype(int); Bm=_np.array(_I.open(f'{outdir}/t_on_{tag}_{v}.png').convert('RGB')).astype(int)
        tot.append(int((_np.abs(A-Bm).max(2)>12).sum()))
    res[tag]=tot
print('LEAK (front,side,back) of',700*1100,res)
setk({})
# ---- export: drop the clothes, keep underlays (tagged hidden-when-clothed) ----
for n in ('outfit_top','outfit_bottom','outfit_shoes'): bpy.data.objects.remove(ob[n],do_unlink=True)
bpy.ops.export_scene.gltf(filepath=f'{outdir}/Tobin_Base_v2.glb',export_format='GLB',export_morph=True,export_skins=True,export_extras=True,export_animations=True)
print('EXPORTED')

print('DONE')
