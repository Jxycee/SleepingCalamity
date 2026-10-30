# Tobin Marsh: concept options (M10, 2026-10-29)

Pick one direction. Sheets: `docs/design/tobin/option_{a,b,c}.jpg` (front, 3/4, side, back, plus a small figure at gameplay-camera size). Blockouts are primitives with flat colours, made by `Art/Blender/concepts/tobin_blockout.py`. They show mass and silhouette only, not final art, face or hair.

## Shared brief
- **Read in silhouette (30 m):** broad, heavy shoulders, big hands and boots (PHASE1 3.6: 1.90 m, about 6.5 heads, hands and feet 1.15x). Gentle and tired: head slightly forward, arms hang loose, the axe is carried rather than brandished.
- **Heirloom:** his father's beefy logging axe. Long oak haft (about 0.95 m), broad iron head, worn but loved. It is the one shape that must read at the 5-6.5 m boom.
- **Palette (PHASE1 3.1 day bands):** warm mid-values, no pure white or black; +10-15% saturation only on cloth and accents; shadows slate-teal. Shared: skin `#c98f6b`, oak haft `#6b4a2b`, iron `#6e7378`, boots `#3f2c20`, timber-dark `#2e2520`.
- **Materials:** wool (rough, sheen-free), linen (soft, creased), leather (matte, worn edges), iron (dark, tidy wear), oak haft (adzed, hand-polished grip).
- **Outfit layers (all separate meshes on one skeleton, per STORY 3 customisation):** boots, trousers, undershirt/shirt, outer layer, belt and pouches, neck piece, hair, axe.

## Option A: timber-yard worker
- **Silhouette:** a wide rectangle of shoulders and apron; axe slanted across the back, head rising over the left shoulder. Most honest to "his father's axe from the timber yard".
- **Layers:** linen shirt `#d8cdb0` with rolled sleeves (bare forearms), leather apron `#8a5a30`, belt `#2e2520`, trousers `#5a4a38`, boots `#3f2c20`, short dark hair `#3a2a1e`.
- **Axe at distance:** the iron head breaks the head and shoulder line against the sky, and the diagonal haft is visible from the camera behind him (always the view in play). Sheathed on the back, so attack animations show it drawn.

## Option B: mountain warden
- **Silhouette:** a tall bell shape: fur collar and long cloak form one heavy triangle, the axe hangs low in the right hand with its head near the ground. Most "tired guardian", least mobile-looking.
- **Layers:** green wool tunic `#5f6b4a`, cloak `#7c3f2e` (madder), fur collar `#cbbd9c`, belt `#2e2520`, trousers `#4e4844`, leather gloves, accent toggle `#b6ab28`.
- **Axe at distance:** a low, vertical iron block beside the leg. Weakest axe read from behind (cloak hides the haft side), so the cloak would need a split or a shorter hem.

## Option C: town carpenter
- **Silhouette:** big torso with a bright vest panel and a pouch line at the hip; the axe rests over the right shoulder, head high above the head line. Reads most as a builder, who is his story role.
- **Layers:** cream linen shirt `#cbbd9c`, blue waistcoat `#3d5a6e`, tool belt `#4e3420` with ochre pouches `#c8802a` and a hammer, bandaged forearms `#e6dcc0`, trousers `#6b5a44`, boots `#3f2c20`.
- **Axe at distance:** the highest and clearest read: the head stands above his head, with a hand holding the haft. The blue vest gives a second readable mass against the warm town.

## Notes
- Blockouts use Workbench flat shading, so tones appear lighter and flatter than PBR. Hex values are the intended albedos.
- Hair, face, beard and fine detail are not part of these sheets; they go to the Tripo stage after the pick.
