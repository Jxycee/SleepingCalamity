"""Contact sheet: rows = labels, cols = images. usage: contact_sheet.py <out.png> <cell_px> <title> then pairs  label=path ... ; use '|' to start a new row.
Example: contact_sheet.py s.png 384 title "src=a.png" "k4=b.png" "|" "k9=c.png" """
import sys, cv2, numpy as np
out, px, title = sys.argv[1], int(sys.argv[2]), sys.argv[3]
rows, cur = [], []
for a in sys.argv[4:]:
    if a == '|': rows.append(cur); cur = []; continue
    lab, p = a.split('=', 1); im = cv2.imread(p)
    if im is None: im = np.full((px, px, 3), 60, np.uint8)
    h, w = im.shape[:2]; s = px / max(h, w); im = cv2.resize(im, None, fx=s, fy=s, interpolation=cv2.INTER_AREA)
    c = np.full((px + 22, px, 3), 30, np.uint8); c[22:22 + im.shape[0], :im.shape[1]] = im
    cv2.putText(c, lab, (4, 15), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 255), 1, cv2.LINE_AA); cur.append(c)
if cur: rows.append(cur)
W = max(len(r) for r in rows)
rows = [np.hstack(r + [np.full_like(r[0], 30)] * (W - len(r))) for r in rows]
sheet = np.vstack([np.full((24, rows[0].shape[1], 3), 20, np.uint8)] + rows)
cv2.putText(sheet, title, (6, 17), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1, cv2.LINE_AA)
cv2.imwrite(out, sheet); print(out, sheet.shape)
