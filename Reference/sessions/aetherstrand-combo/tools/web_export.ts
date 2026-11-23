// Headless web-side exporter. Run with cwd = AetherStrand clone:
//   npx tsx web_export.ts <recipe.json> <outDir>
// Writes PresetGeometry.json (plugin format) for ANY recipe (builder + presetId + physics).
import fs from 'node:fs';
import path from 'node:path';
import { buildExportLocksForBlender } from 'C:/Dev/scratch/AetherStrand/src/engine/hairPhysics';
const [recipePath, out = '.'] = process.argv.slice(2);
const r: any = JSON.parse(fs.readFileSync(recipePath, 'utf8'));
const locks = buildExportLocksForBlender(r.presetId, r.builder ?? null, r.physics?.strandWidthScale ?? 1);
fs.mkdirSync(out, { recursive: true });
const geo = { schemaVersion: '2.0', sourcePivot: [0, 0.44, 0], strandWidthScale: r.physics?.strandWidthScale ?? 1, presetId: r.presetId, presetName: r.customName ?? r.presetId,
  coordinateSystem: 'blender_z_up_neg_y_front', lockCount: locks.length, locks };
fs.writeFileSync(path.join(out, 'PresetGeometry.json'), JSON.stringify(geo));
console.log(geo.presetName, 'locks', locks.length);
