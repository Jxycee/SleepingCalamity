# blender -b mp2/Tobin_mp_hair.blend -P flag.py -- <outdir>  : render hair with candidate shard sets in red
import bpy, bmesh, numpy as np, math, sys, os
from mathutils import Vector
from mathutils.bvhtree import BVHTree
out=sys.argv[sys.argv.index('--')+1]; os.makedirs(out,exist_ok=True)
hair=bpy.data.objects['Tobin_Hair']; head=bpy.data.objects['AvatarHead']
bm=bmesh.new(); bm.from_mesh(hair.data); bm.faces.ensure_lookup_table(); bm.edges.ensure_lookup_table()
N=len(bm.faces)
# welded adjacency by position to get dihedral across seams too
key=lambda v:(round(v.co.x/2e-6),round(v.co.y/2e-6),round(v.co.z/2e-6))
vf={}
for f in bm.faces:
    for v in f.verts: vf.setdefault(key(v),[]).append(f.index)
area=np.array([f.calc_area() for f in bm.faces]); nrm=np.array([f.normal[:] for f in bm.faces])
e=np.array([[ (f.verts[1].co-f.verts[0].co).length,(f.verts[2].co-f.verts[1].co).length,(f.verts[0].co-f.verts[2].co).length] for f in bm.faces])
q=4*math.sqrt(3)*area/(e**2).sum(1)
dih=np.zeros(N)
for f in bm.faces:
    nb=set()
    for v in f.verts: nb.update(vf[key(v)])
    nb.discard(f.index)
    if nb: dih[f.index]=max(math.degrees(math.acos(max(-1,min(1,float(np.dot(nrm[f.index],nrm[j])))))) if True else 0 for j in nb)   # max angle to any touching face
hb=bmesh.new(); hb.from_mesh(head.data); hb.transform(head.matrix_world); tree=BVHTree.FromBMesh(hb)
inward=np.zeros(N,bool); dist=np.zeros(N)
for f in bm.faces:
    c=f.calc_center_median(); loc,nr,_i,d=tree.find_nearest(c,0.05)
    if loc is not None: dist[f.index]=d; inward[f.index]=np.dot(nrm[f.index],np.array(nr))<-0.2 and (c-loc).dot(nr)>0
sets={'sliver':q<0.05,'sharp':dih>75,'inward':inward&(dist<0.012)}
for k,m in sets.items(): print('SET',k,int(m.sum()),'of',N)
# materials
def mat(n,c):
    m=bpy.data.materials.new(n); m.diffuse_color=c; m.use_nodes=True; m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=c; return m
hair.data.materials.clear(); hair.data.materials.append(mat('g',(0.55,0.4,0.3,1))); hair.data.materials.append(mat('r',(1,0,0,1)))
for o in bpy.data.objects:
    if o.type=='MESH' and o.name not in ('Tobin_Hair','AvatarHead','AvatarLeftEyeball','AvatarRightEyeball'): o.hide_render=True
sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'; sc.render.resolution_x=sc.render.resolution_y=700; sc.display.shading.light='STUDIO'; sc.display.shading.color_type='MATERIAL'; sc.view_settings.view_transform='Standard'
w=bpy.data.worlds.new('w'); w.color=(0.8,0.82,0.86); sc.world=w
cam=bpy.data.cameras.new('c'); cam.type='ORTHO'; cam.ortho_scale=0.34; co=bpy.data.objects.new('c',cam); sc.collection.objects.link(co); sc.camera=co
tgt=Vector((0,0,1.76))
for k,m in sets.items():
    for i,p in enumerate(hair.data.polygons): p.material_index=int(m[i])
    for nm,az in (('f',0),('q',45),('b',180)):
        a_=math.radians(az); pos=tgt+Vector((2*math.sin(a_),-2*math.cos(a_),0)); co.location=pos; co.rotation_euler=(tgt-pos).to_track_quat('-Z','Y').to_euler()
        sc.render.filepath=os.path.join(out,f'flag_{k}_{nm}.png'); bpy.ops.render.render(write_still=True)
np.savez(os.path.join(out,'flags.npz'),q=q,dih=dih,inward=inward,dist=dist)
print('FLAG_DONE')
