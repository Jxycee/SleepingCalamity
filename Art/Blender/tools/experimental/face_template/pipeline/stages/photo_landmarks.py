# facekeys env: FaceMesh 478 landmarks + head pose (yaw/pitch/roll from facial transformation matrix) on the cropped target source.
import sys, json, math, numpy as np, mediapipe as mp
from mediapipe.tasks import python as mpp
from mediapipe.tasks.python import vision
import os
D=os.environ['SCP_RUN'].rstrip('/')+'/target/'
BOX=None  # crop box is recorded by prep_image (cfg.prep.box); not needed downstream
opt=vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path=os.environ['SCP_FACE_MODEL']),
    num_faces=1,output_facial_transformation_matrixes=True,output_face_blendshapes=False)
lm=vision.FaceLandmarker.create_from_options(opt)
im=mp.Image.create_from_file(D+'crop_src.png'); r=lm.detect(im)
L=r.face_landmarks[0]; W,H=im.width,im.height
M=np.array(r.facial_transformation_matrixes[0]); R=M[:3,:3]; s=np.linalg.norm(R[:,0]); R=R/s
# canonical face: +X right of image?, +Y up, +Z toward camera; matrix maps canonical->camera(OpenGL-like)
yaw=math.degrees(math.atan2(R[0,2],R[2,2])); pitch=math.degrees(math.asin(-R[1,2])) ; roll=math.degrees(math.atan2(R[1,0],R[0,0]))
pitch2=math.degrees(math.atan2(-R[1,2],math.hypot(R[0,2],R[2,2])))
pts=[[L[i].x*W,L[i].y*H,L[i].z*W] for i in range(len(L))]
# simple 2D symmetry cue: nose tip x vs midpoint of outer eye corners and mouth corners, in units of inter-ocular distance
P=lambda i:np.array(pts[i][:2]); iod=np.linalg.norm(P(33)-P(263))
mid=(P(33)+P(263))/2; cues={'nose_off_eye_mid_iod':float((P(1)[0]-mid[0])/iod),'nose_off_mouth_mid_iod':float((P(1)[0]-(P(61)[0]+P(291)[0])/2)/iod),
 'chin_off_eye_mid_iod':float((P(152)[0]-mid[0])/iod),
 'left_face_w':float(abs(P(1)[0]-P(234)[0])),'right_face_w':float(abs(P(454)[0]-P(1)[0]))}
json.dump({'image':'crop_src.png','crop_box_in_cloud_src':BOX,'size':[W,H],'landmarks_px_xyz':pts,'yaw_deg':yaw,'pitch_deg':pitch2,'roll_deg':roll,
 'matrix':M.tolist(),'symmetry_cues':cues,'note':'MediaPipe image-left = subject right eye (idx 33 is the subject right eye outer corner, at image left). yaw sign: + = subject turned toward image right (verify)'},open(D+'landmarks.json','w'))
print(len(pts),'yaw',yaw,'pitch',pitch2,'roll',roll,cues)
# overlay
import cv2
img=cv2.imread(D+'crop_src.png')
for x,y,_ in pts: cv2.circle(img,(int(x),int(y)),1,(0,255,0),-1)
cv2.imwrite(D+'landmarks_overlay.png',img)
