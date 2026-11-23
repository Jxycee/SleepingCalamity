// Shared: full-precision PresetGeometry (schema 2.1, plugin format + solver extras) from the LIVE web HairPhysicsSystem.
// Mirrors HairPhysicsSystem.loadCustomBuilder (collision-free bake included), so it equals what the web viewport renders and simulates.
import * as THREE from 'C:/Dev/scratch/AetherStrand/node_modules/three/build/three.module.js';
import { HairPhysicsSystem } from 'C:/Dev/scratch/AetherStrand/src/engine/hairPhysics';

export function exportGeometry(recipe: any): any {
  const hair: any = new HairPhysicsSystem(new THREE.ShaderMaterial(), recipe.presetId);
  const headPivot = new THREE.Group();
  headPivot.position.set(0, 0.44, 0);
  const root = new THREE.Group();
  root.add(headPivot);
  hair.loadCustomBuilder(recipe.builder, recipe.presetId);
  const w = recipe.physics?.strandWidthScale ?? 1;
  hair.snapToHead(headPivot, w);
  const locks: any[] = hair.locks;
  const exported: any[] = hair.exportLockGeometry(w);
  exported.forEach((e: any, i: number) => {
    const lk = locks[i];
    e.points = lk.particles.map((p: any) => [p.restLocal.x, -p.restLocal.z, p.restLocal.y]);
    e.rootNormal = [lk.rootNormalLocal.x, -lk.rootNormalLocal.z, lk.rootNormalLocal.y];
    e.width = lk.baseWidth * w;
    e.thickness = lk.baseThickness * (0.65 + 0.35 * w);
    e.seed = lk.seed;
    e.scalpRoot = lk.scalpRoot;
    e.lockNormal = lk.lockNormal;
    e.layerOrder = lk.layerOrder;
    e.mass = lk.mass;
    e.restMidR = lk.restMidR;
    e.rootRawY = lk.particles[0].rawRestLocal.y;
    e.stiffnessWeight = lk.particles.map((p: any) => p.stiffnessWeight);
  });
  return {
    schemaVersion: '2.1',
    sourcePivot: [0, 0.44, 0],
    strandWidthScale: w,
    presetId: recipe.presetId,
    presetName: recipe.customName ?? recipe.presetId,
    coordinateSystem: 'blender_z_up_neg_y_front',
    solverExtras: 'layerOrder,mass,restMidR,rootRawY,stiffnessWeight per lock',
    lockCount: exported.length,
    locks: exported,
  };
}
