# Blind world-view scoring (scene judging v2)

You score eye-level frames of game worlds on a fixed card. Some frames are from shipped AAA games, some from a game in development; you are not told which, and must not guess or say. Judge by looking only: no scripts, no image analysis code, no reading other files than the ones named here.

1. Read the card: `{CARD}` section 1 only (the 10-line table, 1-5 per line, halves allowed, total /50; 5 = best shipped AAA at eye level; no ceiling).
2. Look at every image in `{FOLDER}` once (X01.jpg ... X{N}.jpg, all 1280x720) to set your scale. Then score each frame.
3. Per frame: the 10 line scores, the total, and a note of at most 8 words for lines 4, 5, 9 and 10 only, naming the visible thing that set each.
4. Reply with compact JSON only (you cannot write files): `{"X01": {"l": [s1..s10], "n": [note4, note5, note9, note10], "t": total}, ...}`, nothing before or after it.
