// Generates a recipe + PresetGeometry.json for every web archetype (deterministic seeds).
// Run with cwd = AetherStrand clone:  npx tsx gen_recipes.ts <outDir>
import fs from 'node:fs';
import path from 'node:path';
import { ALL_ARCHETYPES, generateRandomHairstyle } from 'C:/Dev/scratch/AetherStrand/src/engine/hairBuilder';
import { buildExportLocksForBlender } from 'C:/Dev/scratch/AetherStrand/src/engine/hairPhysics';

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
  const locks = buildExportLocksForBlender(r.basePreset, r.builder, r.physics.strandWidthScale ?? 1);
  const dir = path.join(out, arch);
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(dir, 'recipe.json'), JSON.stringify(recipe));
  fs.writeFileSync(
    path.join(dir, 'PresetGeometry.json'),
    JSON.stringify({
      schemaVersion: '2.0',
      sourcePivot: [0, 0.44, 0],
      strandWidthScale: recipe.physics.strandWidthScale ?? 1,
      presetId: r.basePreset,
      presetName: r.name,
      coordinateSystem: 'blender_z_up_neg_y_front',
      lockCount: locks.length,
      locks,
    })
  );
  index.push({ arch, name: r.name, seed, locks: locks.length });
  console.log(arch, r.name, locks.length);
});
fs.writeFileSync(path.join(out, 'index.json'), JSON.stringify(index));
