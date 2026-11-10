# blender -b Tobin_rigged_hair.blend -P sim_bake.py -- <chains.json> <out_dir> [frames=84]
# Verlet spring chains under the animated Head bone (head yaw +-32 deg, nod), renders key frames (Workbench). Also writes sim_params used by the Unity component.
import bpy, numpy as np, json, sys, os, math
from mathutils import Vector, Matrix, Quaternion, Euler
a=sys.argv[sys.argv.index('--')+1:]; chains=json.load(open(a[0])); out=a[1]; NF=int(a[2]) if len(a)>2 else 84
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; sc=bpy.context.scene
print('pose_position',arm.data.pose_position); arm.data.pose_position='POSE'; arm.animation_data_clear()
for o in bpy.data.objects:
    if o.type=='MESH' and o.data.shape_keys: o.data.shape_keys.animation_data_clear()
head=arm.pose.bones['mixamorig:Head']
print('head constraints',[c.type for c in head.constraints],'parent',head.parent.name); head.rotation_mode='XYZ'
for o in bpy.data.objects:
    if o.type=='MESH' and o.name not in ('Tobin_Hair','Tobin_Head','Tobin_EyeL','Tobin_EyeR'): o.hide_render=True
# chain data (armature space, rest)
C=[]
for c in chains:
    pts=np.array(c['pts_char']); C.append(dict(bones=c['bones'],rest=pts))
bpy.context.view_layer.update()
hr=np.array(arm.pose.bones['mixamorig:Head'].matrix)           # head pose matrix at rest (armature space)
hrest_inv=np.linalg.inv(hr)
for c in C: c['loc']=(hrest_inv[:3,:3]@c['rest'].T).T+hrest_inv[:3,3]; c['p']=c['rest'].copy(); c['pp']=c['rest'].copy(); c['seglen']=np.linalg.norm(c['rest'][1:]-c['rest'][:-1],axis=1)
GRAV=np.array([0,0,-9.8]); DAMP=0.93; STIFF=1500.0; DT=1/30; SUB=4
def head_mat(fr):
    t=fr/NF; yaw=math.radians(32)*math.sin(2*math.pi*t*1.5); nod=math.radians(10)*math.sin(2*math.pi*t*3)
    head.rotation_euler=Euler((nod,yaw,0),'XYZ'); bpy.context.view_layer.update(); return np.array(head.matrix)
def step(c,Hm):
    tgt=(Hm[:3,:3]@c['loc'].T).T+Hm[:3,3]      # where the chain would be if rigid
    c['p'][0]=tgt[0]; c['pp'][0]=tgt[0]
    for _ in range(SUB):
        dt=DT/SUB; p,pp=c['p'],c['pp']
        vel=(p-pp)*DAMP; new=p+vel+(GRAV+STIFF*(tgt-p)*np.array([1,1,1]))*dt*dt
        new[0]=tgt[0]; c['pp']=p.copy(); c['p']=new
        for _i in range(4):                       # keep segment lengths
            for i in range(len(new)-1):
                d=c['p'][i+1]-c['p'][i]; L=np.linalg.norm(d)+1e-9; corr=(L-c['seglen'][i])/L
                if i==0: c['p'][i+1]-=d*corr
                else: c['p'][i]+=0.5*d*corr; c['p'][i+1]-=0.5*d*corr
def apply(c):
    for i,name in enumerate(c['bones']):
        pb=arm.pose.bones[name]; r=c['rest'][i+1]-c['rest'][i]; u=c['p'][i+1]-c['p'][i]
        # rest bone rotation in armature space, rotated by the head's own motion is implicit in p
        q=Vector(r).rotation_difference(Vector(u)); m=Matrix.Translation(Vector(c['p'][i]))@q.to_matrix().to_4x4()@pb.bone.matrix_local.to_3x3().to_4x4()
        pb.matrix=m
frames_to_render={0:'rest',14:'yawL',28:'mid',42:'yawR',56:'nod',83:'settle'}
w=bpy.data.worlds.new('w'); w.color=(0.8,0.8,0.82); sc.world=w; sc.render.engine='BLENDER_WORKBENCH'; sc.render.resolution_x=sc.render.resolution_y=420
sc.display.shading.light='STUDIO'; sc.display.shading.color_type='MATERIAL'; sc.view_settings.view_transform='Standard'
cam=bpy.data.cameras.new('c'); cam.type='ORTHO'; cam.ortho_scale=0.42; co=bpy.data.objects.new('c',cam); sc.collection.objects.link(co); sc.camera=co
tgt=Vector((0,0,1.70)); 
def cam_at(az):
    a_=math.radians(az); pos=tgt+Vector((2*math.sin(a_),-2*math.cos(a_),0)); co.location=pos; co.rotation_euler=(tgt-pos).to_track_quat('-Z','Y').to_euler()
os.makedirs(out,exist_ok=True)
for fr in range(NF):
    Hm=head_mat(fr)
    for c in C: step(c,Hm); apply(c)
    bpy.context.view_layer.update()
    if fr in frames_to_render:
        for nm,az in (('f',0),('q',45)):
            cam_at(az); sc.render.filepath=os.path.join(out,f'sim_{frames_to_render[fr]}_{nm}.png'); bpy.ops.render.render(write_still=True)
print('SIM_DONE')
