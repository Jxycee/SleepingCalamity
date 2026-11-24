// Generates a recipe + PresetGeometry.json for every web archetype (deterministic seeds).
// Run with cwd = AetherStrand clone:  npx tsx gen_recipes.ts <outDir>
import fs from 'node:fs';
import path from 'node:path';
import { ALL_ARCHETYPES, generateRandomHairstyle } from 'C:/Dev/scratch/AetherStrand/src/engine/hairBuilder';
import { exportGeometry } from './web_geometry';
import { HAIR_PRESETS, DEFAULT_BUILDER_BY_PRESET } from 'C:/Dev/scratch/AetherStrand/src/types/hair';

const out = process.argv[2] ?? 'C:/Dev/scratch/batch';
fs.mkdirSync(out, { recursive: true });
const index: any[] = [];
ALL_ARCHETYPES.forEach((arch: string, i: number) => {
  const seed = 7001 + i;
  const r: any = generateRandomHairstyle(seed, arch as any);
  const recipe = {
    schemaVersion: '2.0',
    presetId: r.basePreset,
    customName: r.name,
    builder: r.builder,
    shader: r.shader,
    physics: { ...r.physics, solverSubsteps: 1, collisionEnabled: true },
  };
  const dir = path.join(out, arch);
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(dir, 'recipe.json'), JSON.stringify(recipe));
  const geo = exportGeometry(recipe);
  fs.writeFileSync(path.join(dir, 'PresetGeometry.json'), JSON.stringify(geo));
  const locks = geo.locks;
  index.push({ arch, name: r.name, seed, locks: locks.length });
  console.log(arch, r.name, locks.length);
});
fs.writeFileSync(path.join(out, 'index.json'), JSON.stringify(index));

// The web app's named presets (the five web presets) with their default builder configs.
for (const id of Object.keys(HAIR_PRESETS)) {
  const def: any = (HAIR_PRESETS as any)[id];
  const recipe = {
    schemaVersion: '2.0',
    presetId: id,
    customName: def.name,
    builder: (DEFAULT_BUILDER_BY_PRESET as any)[id],
    shader: def.shader,
    physics: { ...def.physics, solverSubsteps: 1, collisionEnabled: true },
  };
  const dir = path.join(out, 'preset-' + id);
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(dir, 'recipe.json'), JSON.stringify(recipe));
  const geo = exportGeometry(recipe);
  fs.writeFileSync(path.join(dir, 'PresetGeometry.json'), JSON.stringify(geo));
  console.log('preset-' + id, def.name, geo.lockCount);
}
