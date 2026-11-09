import trimesh,sys
s=trimesh.load(sys.argv[1]);g=list(s.geometry.values())[0] if hasattr(s,'geometry') else s
print('tris',len(g.faces),'bounds',g.bounds.tolist(),'size',(g.bounds[1]-g.bounds[0]).tolist(),'uv',hasattr(g.visual,'uv') and g.visual.uv is not None,'tex',type(getattr(g.visual.material,'baseColorTexture',None)))
