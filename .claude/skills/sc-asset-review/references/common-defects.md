<!-- Covers: table of defects seen in review renders, with cause and fix. Moved from SKILL.md section 5. -->

## 5. Common defects and their fix
| Seen in the render | Cause | Fix |
|---|---|---|
| "sandpaper" or "foam": one scale of noise | scan tiled too fine, fine normal too strong, low value contrast | coarser scan tile, weaker fine normal, more contrast (the mortar: tile 0.12 -> 0.25, normal strength 1.8 -> 0.8, contrast 0.45 -> 0.9) |
| micro-detail invisible | bump too weak for review scale | detail normals 0.5-0.8 plus a fine noise layer |
| every rim melted | smoothing the whole mesh | fillet only the concave seams where parts meet |
| chip "islands" all over smooth faces, moon craters | ungated noise chips, dome bites | gate by curvature (convex rims), cut planar flakes with a square arris |
| stripes along a cylinder | planar facets on a turned part | clean source low, Catmull-resampled profile |
| metal renders black | nothing to reflect | environment plus a probe in the review scene; reflection probe in Unity |
| glaze reads as modern plastic | full glossy glaze | mostly bare clay, glaze over the shoulder ending in runs |
| streaks look airbrushed or like teeth | 1D-Voronoi runs on metal | noise stretched along gravity |
| liquid reads neon | bright liquid colour | dark saturated colour (linear 0.1-0.15 on the main channel) |
| pillow-puffy stones with black gaps | big bevel, no mortar | smaller bevel plus a recessed lime-mortar bed |
| photo-dull colour | scan albedo used as is | grade toward the PHASE1 3.1 key hues (sc-asset-craft palette summary); +10-15% chroma only on foliage, painted wood, cloth and accents, none on stone, plaster or roofs |
| a material change has no effect | a light is doing it (the brazier's light inside its wall) | switch lights off one at a time first |
| contour bands in the sheet | big PNG posterised by the viewer | JPEG, 2000 px or less |
