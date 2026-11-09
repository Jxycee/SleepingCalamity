"""debug: mean landmark displacement (mm, render -> photo, eye-aligned) per facial group. usage: r4_pairs.py <tag>"""
import json,sys,numpy as np
from r4_common import *
tag=sys.argv[1]; cam=json.load(open(FIT+f'_cam_{tag}.json')); R_,S_,cx,cz=cam['R'],cam['S'],cam['cx'],cam['cz']
rl=json.load(open(FIT+f'lm_{tag}.json')); rp=np.array([rl[str(i)] for i in range(468)]); ph=np.array(json.load(open(TGT+'landmarks.json'))['landmarks_px_xyz'])[:468,:2]
px2mm=lambda p: np.stack([cx+(p[:,0]/R_-0.5)*S_,cz+(0.5-p[:,1]/R_)*S_],1)
EC=[33,263,133,362]; s2,Rr,tt=umeyama(np.c_[rp[EC],np.zeros(4)],np.c_[ph[EC],np.zeros(4)],True); R2=Rr[:2,:2]; inv=lambda q:((q-tt[:2])@R2)/s2
d=(px2mm(inv(ph))-px2mm(rp))*1e3
G={'browL':[70,63,105,66,107],'browR':[300,293,334,296,336],'nose':[1,2,4,5,6,19,94,98,327,129,358],'noseline':[168,195,197],'upperlip':[0,267,269,37,39,40],'mouthc':[13,14,17],'corners':[61,291],'chin':[152,148,176,377,400],'lowerlip':[17,84,181,314,405],'cheek':[234,454,172,397,132,361],'eyeUp':[159,386],'eyeLo':[145,374],'lid':[33,133,263,362]}
for k,v in G.items(): print('%-9s mean dx,dz mm %s | x-std %.2f'%(k,d[v].mean(0).round(2),d[v][:,0].std()))
print('sim scale',round(s2,3),'rot deg',round(np.degrees(np.arctan2(Rr[1,0],Rr[0,0])),2))
