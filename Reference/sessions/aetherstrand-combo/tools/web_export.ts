// Headless web-side exporter. Run with cwd = AetherStrand clone:
//   npx tsx web_export.ts <recipe.json> <outDir>
// Writes PresetGeometry.json (schema 2.1: plugin format + solver extras, full precision) for ANY recipe.
import fs from 'node:fs';
import path from 'node:path';
import { exportGeometry } from './web_geometry';

const [recipePath, out = '.'] = process.argv.slice(2);
const recipe: any = JSON.parse(fs.readFileSync(recipePath, 'utf8'));
fs.mkdirSync(out, { recursive: true });
const geo = exportGeometry(recipe);
fs.writeFileSync(path.join(out, 'PresetGeometry.json'), JSON.stringify(geo));
console.log(geo.presetName, 'locks', geo.lockCount);
