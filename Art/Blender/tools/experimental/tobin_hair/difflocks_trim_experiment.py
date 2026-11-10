# blender -b --python hair_trim.py -- <npz> <outdir> <label> [front_z=1.70 side_z=1.635 back_z=1.625 jitter=0.025 seed=1]
# DiffLocks strands -> Tobin world (same fit as render_sweep.py), trimmed to a short layered cut, rendered from 4 views (Workbench). Writes <outdir>/<label>_{front,q34,side,back}.png and <label>_world.npz
import bpy, sys, numpy as np, math, os
from mathutils import Vector, Euler
a=sys.argv[sys.argv.index('--')+1:]; npz,out,label=a[:3]; kw=dict(x.split('=') for x in a[3:]); g=lambda k,d: float(kw.get(k,d))
FZ,SZ,BZ,JIT=g('front_z',1.70),g('side_z',1.635),g('back_z',1.625),g('jitter',0.025); rng=np.random.default_rng(int(g('seed',1)))
s,dx,dy,dz,rx,rz=1.12,0,-0.03,0,6,0
NAPE_S=np.array([0.0,0.06,0.217]); NAPE_T=np.array([0.0,0.06,1.62])+np.array([dx,dy,dz]); R=np.array(Euler((math.radians(rx),0,math.radians(rz)),'XYZ').to_matrix())
P=np.load(npz)['positions'][:,::4].astype(np.float64); Q=np.stack([P[...,0],-P[...,2],P[...,1]],-1); Q=(Q-NAPE_S)@R.T*s+NAPE_T   # N,M,3 world, root first
N,M,_=Q.shape
SWEEP=g('sweep',0.0); SQ=g('squeeze',1.0)
r0=Q[:,0]; azr=np.degrees(np.arctan2(r0[:,0],-(r0[:,1])))
wf=np.clip(1-np.abs(azr)/55,0,1)*(r0[:,2]>1.74)                     # fringe roots: front, upper scalp
drop=np.clip(Q[:,0:1,2]-Q[:,:,2],0,None)                             # how far each point hangs below its root
Q[:,:,0]-= (SWEEP*wf[:,None]*drop)                                     # sweep the fringe toward image-left (-x)
cx=np.array([0.0,0.03]); low=np.clip((1.80-Q[:,:,2])/0.12,0,1)         # squeeze volume below the crown toward the head axis
Q[:,:,0]=cx[0]+(Q[:,:,0]-cx[0])*(1-(1-SQ)*low); Q[:,:,1]=cx[1]+(Q[:,:,1]-cx[1])*(1-(1-SQ)*low)
# per-strand cut height from where the root sits: fringe (front), sides, back
r=Q[:,0]; az=np.degrees(np.arctan2(r[:,0],-(r[:,1]-0.0)))   # 0 = straight front, +-90 sides, 180 back
w_front=np.clip(1-np.abs(az)/70,0,1); w_back=np.clip((np.abs(az)-110)/50,0,1)
zmin=FZ*w_front+SZ*(1-w_front)*(1-w_back)+BZ*w_back+rng.normal(0,JIT,N)
below=Q[:,:,2]<zmin[:,None]; first=np.where(below.any(1),below.argmax(1),M)   # first point under the cut
keep=first>=3
for i in np.where(keep&(first<M))[0]: Q[i,first[i]:]=Q[i,first[i]-1]               # truncate: repeat the last kept point
Q=Q[keep]; print('strands kept',keep.sum(),'of',N)
np.savez_compressed(os.path.join(out,label+'_world.npz'),positions=Q.astype(np.float32))
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath='C:/AI/difflocks/head/Tobin_Base_v2_sliders.glb')
keepn={'AvatarHead','AvatarLeftEyeball','AvatarRightEyeball','AvatarEyelashes'}
for o in list(bpy.data.objects):
    if o.type=='MESH' and o.name not in keepn: bpy.data.objects.remove(o)
def mat(n,c):
    m=bpy.data.materials.new(n); m.diffuse_color=c; m.use_nodes=True; m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=c; return m
skin=mat('skin',(0.62,0.45,0.36,1)); eye=mat('eye',(0.18,0.11,0.07,1)); lash=mat('lash',(0.05,0.03,0.02,1)); hairm=mat('hair',(0.30,0.17,0.09,1))
for o in bpy.data.objects:
    if o.type=='MESH': o.data.materials.clear(); o.data.materials.append(skin if o.name=='AvatarHead' else (lash if 'lash' in o.name.lower() else eye))
sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'; sc.render.resolution_x=sc.render.resolution_y=480; sc.display.shading.light='STUDIO'; sc.display.shading.color_type='MATERIAL'; sc.view_settings.view_transform='Standard'
w=bpy.data.worlds.new('w'); w.color=(1,1,1); sc.world=w
cd=bpy.data.hair_curves.new('h'); N2=len(Q); cd.add_curves([M]*N2); cd.points.foreach_set('position',Q.astype(np.float32).reshape(-1))
if 'radius' in cd.attributes: cd.points.foreach_set('radius',np.full(N2*M,0.0006,np.float32))
cd.materials.append(hairm); ho=bpy.data.objects.new('hair',cd); sc.collection.objects.link(ho)
cam=bpy.data.cameras.new('c'); cam.type='ORTHO'; cam.ortho_scale=0.62; co=bpy.data.objects.new('c',cam); sc.collection.objects.link(co); sc.camera=co
tgt=Vector((0,0,1.70))
for name,az_ in (('front',0),('q34',40),('side',90),('back',180)):
    a_=math.radians(az_); pos=tgt+Vector((2*math.sin(a_),-2*math.cos(a_),0)); co.location=pos; co.rotation_euler=(tgt-pos).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=os.path.join(out,f'{label}_{name}.png'); bpy.ops.render.render(write_still=True)
print('DONE')
