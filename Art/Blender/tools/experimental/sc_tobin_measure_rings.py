import bpy,sys,bmesh,json
import numpy as np
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=sys.argv[-2])
out={}
ob=bpy.data.objects
def loops(obj):
    bm=bmesh.new(); bm.from_mesh(obj.data); bm.verts.ensure_lookup_table()
    M=obj.matrix_world
    bedges=[e for e in bm.edges if e.is_boundary]
    adj={}
    for e in bedges:
        a,b=e.verts[0].index,e.verts[1].index; adj.setdefault(a,[]).append(b); adj.setdefault(b,[]).append(a)
    seen=set(); res=[]
    for s in adj:
        if s in seen: continue
        stack=[s]; comp=[]
        while stack:
            v=stack.pop()
            if v in seen: continue
            seen.add(v); comp.append(v); stack+= [n for n in adj[v] if n not in seen]
        pts=np.array([list(M@bm.verts[i].co) for i in comp])
        c=pts.mean(0); r=np.linalg.norm(pts-c,axis=1)
        res.append({'n':len(comp),'centre':[round(x,3) for x in c],'radius':round(float(r.mean()),3),'extent_xyz':[round(float(x),3) for x in (pts.max(0)-pts.min(0))],'ymin':round(float(pts[:,1].min()),3),'ymax':round(float(pts[:,1].max()),3)})
    return res
for n in ('AvatarBody','AvatarHead','outfit_top','outfit_bottom'):
    out[n]=loops(ob[n])
arm=[o for o in ob if o.type=='ARMATURE'][0]
out['bones']={b.name:[round(x,3) for x in (arm.matrix_world@b.head_local)] for b in arm.data.bones if b.name in ('Hips','Spine','Spine1','Spine2','Neck','Head','LeftShoulder','LeftArm','LeftForeArm','LeftHand','LeftUpLeg','LeftLeg','LeftFoot','RightArm','RightUpLeg')}
json.dump(out,open(sys.argv[-1],'w'),indent=1)
