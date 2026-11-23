// Headless AetherStrand web-solver trace writer (ground truth for the C# port).
//
//   cd C:/Dev/scratch/AetherStrand
//   npx tsx <this file> <recipe.json> <outDir> <mode> [frames=300] [--set key=value,key=value] [--nocollide]
//
// mode: idle | walk | turn | gust | rest   (rest = head fixed at the neutral pose, no motion)
// --set  overrides PhysicsConfig fields (e.g. gravity=0,windStrength=0,windTurbulence=0) for staged tests.
//
// Reproduces HairViewport capture mode exactly:
//   snapToHead(headPivot) at the neutral pose (0,0.44,0), then for f = 0..N-1:
//   applyMotionPose(t = f/60)  ->  step(1/60, headPivot, rootGroup, physics).
// rootGroup is the identity at the origin (as in createStylizedBust). applyMotionPose is copied verbatim from
// src/components/HairViewport.tsx (that file imports DOM/React, so it cannot be loaded headless).
//
// Determinism: HairPhysicsSystem and loadCustomBuilder contain no Math.random (locksFromSpecs uses a seeded LCG);
// the trace is bit-reproducible run to run.
//
// Outputs (outDir):
//   trace.bin   Float32 LE, layout [frame][lock][node][xyz], frame 0 = state right after snapToHead, frame f = after step f-1.
//               Coordinates: three.js WORLD space (right-handed, Y up, +Z front, meters), headPivot rest at (0,0.44,0).
//   trace.json  metadata: lockCount, nodesPerLock, frames, dt, physics, per-frame head pose (decomposed matrixWorld,
//               float64: headPos[f], headQuat[f] = x,y,z,w) and per-frame impulse applied before that step.
//   init.json   full-precision solver state after loadCustomBuilder (per-lock + per-particle + cohesion graph) used by the
//               strict C# test, and PresetGeometry.json (live-system rest pose, plugin format + optional solver extras).
import fs from 'node:fs';
import path from 'node:path';
import * as THREE from 'C:/Dev/scratch/AetherStrand/node_modules/three/build/three.module.js';
import { HairPhysicsSystem } from 'C:/Dev/scratch/AetherStrand/src/engine/hairPhysics';

const argv = process.argv.slice(2);
const flags: Record<string, string> = {};
const pos: string[] = [];
for (let i = 0; i < argv.length; i++) {
  if (argv[i] === '--set') flags.set = argv[++i];
  else if (argv[i] === '--nocollide') flags.nocollide = '1';
  else pos.push(argv[i]);
}
const [recipePath, outDir, mode = 'idle', framesArg = '300'] = pos;
const frames = parseInt(framesArg, 10);
const recipe: any = JSON.parse(fs.readFileSync(recipePath, 'utf8'));

const physics: any = { ...recipe.physics, showColliders: false, showBones: false };
if (flags.set) {
  for (const kv of flags.set.split(',')) {
    const [k, v] = kv.split('=');
    physics[k] = v === 'true' ? true : v === 'false' ? false : parseFloat(v);
  }
}
if (flags.nocollide) physics.collisionEnabled = false;

const rootGroup = new THREE.Group();
const headPivot = new THREE.Group();
headPivot.position.set(0, 0.44, 0);
rootGroup.add(headPivot);

const hair = new HairPhysicsSystem(new THREE.ShaderMaterial(), recipe.presetId);
hair.loadCustomBuilder(recipe.builder, recipe.presetId);
hair.snapToHead(headPivot, physics.strandWidthScale);

// ---- verbatim from HairViewport.tsx applyMotionPose (impulse call is logged instead of only applied) ----
let lastImpulse: [number, number, number] = [0, 0, 0];
function applyMotionPose(mode: string, t: number): void {
  if (mode === 'idle') {
    headPivot.position.set(Math.sin(t * 0.68) * 0.005, 0.44 + Math.sin(t * 1.45) * 0.006, Math.cos(t * 0.6) * 0.003);
    headPivot.rotation.set(Math.sin(t * 0.95) * 0.028, Math.sin(t * 0.65) * 0.125, Math.sin(t * 0.52) * 0.024);
  } else if (mode === 'walk') {
    headPivot.position.set(Math.sin(t * 3.2) * 0.022, 0.44 + Math.abs(Math.sin(t * 6.4)) * 0.032, Math.cos(t * 3.2) * 0.012);
    headPivot.rotation.set(0.03 + Math.sin(t * 6.4) * 0.048, Math.sin(t * 3.2) * 0.18, Math.cos(t * 3.2) * 0.052);
  } else if (mode === 'turn') {
    const swing = Math.sin(t * 2.15);
    headPivot.position.set(swing * 0.022, 0.44 + Math.cos(t * 4.3) * 0.012, 0);
    headPivot.rotation.set(Math.sin(t * 1.4) * 0.055, swing * 0.62, -swing * 0.085);
  } else if (mode === 'gust') {
    headPivot.position.set(Math.sin(t * 1.8) * 0.008, 0.44 + Math.sin(t * 2.1) * 0.007, 0);
    headPivot.rotation.set(-0.05 + Math.sin(t * 1.7) * 0.038, 0.14 + Math.sin(t * 0.9) * 0.11, -0.045 + Math.cos(t * 1.5) * 0.025);
    if (Math.sin(t * 1.8) > 0.72) {
      hair.applyImpulse(0.28, 0.05, -0.14);
      lastImpulse = [0.28, 0.05, -0.14];
    }
  } else if (mode === 'rest') {
    headPivot.position.set(0, 0.44, 0);
    headPivot.rotation.set(0, 0, 0);
  } else throw new Error('unknown mode ' + mode);
}

const sys: any = hair as any; // private fields are plain JS properties at runtime
const locks: any[] = sys.locks;
const L = locks.length;
const N = 12;
for (const lk of locks) if (lk.particles.length !== N) throw new Error('lock with ' + lk.particles.length + ' nodes');

fs.mkdirSync(outDir, { recursive: true });
const frameFloats = L * N * 3;
const buf = new Float32Array(frameFloats * (frames + 1));
const headPos: number[][] = [];
const headQuat: number[][] = [];
const impulses: number[][] = [];
const wp = new THREE.Vector3();
const wq = new THREE.Quaternion();
const ws = new THREE.Vector3();
function dumpFrame(f: number) {
  let o = f * frameFloats;
  for (let s = 0; s < L; s++) {
    const ps = locks[s].particles;
    for (let k = 0; k < N; k++) {
      buf[o++] = ps[k].pos.x;
      buf[o++] = ps[k].pos.y;
      buf[o++] = ps[k].pos.z;
    }
  }
  headPivot.updateMatrixWorld(true);
  headPivot.matrixWorld.decompose(wp, wq, ws);
  headPos.push([wp.x, wp.y, wp.z]);
  headQuat.push([wq.x, wq.y, wq.z, wq.w]);
}

// ---- init dump (before any step; snapToHead already ran, positions are the rest pose in world space) ----
const initLocks = locks.map((lk: any) => ({
  seed: lk.seed,
  baseWidth: lk.baseWidth,
  baseThickness: lk.baseThickness,
  mass: lk.mass,
  layerAO: lk.layerAO,
  layerOrder: lk.layerOrder,
  isBaseShell: lk.isBaseShell,
  lockNormal: lk.lockNormal,
  scalpRoot: lk.scalpRoot,
  restMidR: lk.restMidR,
  rootRawY: lk.particles[0].rawRestLocal.y,
  rootNormalLocal: [lk.rootNormalLocal.x, lk.rootNormalLocal.y, lk.rootNormalLocal.z],
  restLocal: lk.particles.map((p: any) => [p.restLocal.x, p.restLocal.y, p.restLocal.z]),
  stiffnessWeight: lk.particles.map((p: any) => p.stiffnessWeight),
  segLen: lk.particles.map((p: any) => p.segLen),
  restCurvature: lk.particles.map((p: any) => [p.restCurvatureLocal.x, p.restCurvatureLocal.y, p.restCurvatureLocal.z]),
}));
const MAXN = 4;
fs.writeFileSync(
  path.join(outDir, 'init.json'),
  JSON.stringify({
    lockCount: L,
    nodesPerLock: N,
    locks: initLocks,
    neighborCount: Array.from(sys.neighborCount as Uint8Array),
    neighborLockIdx: Array.from(sys.neighborLockIdx as Int32Array),
    neighborNodeIdx: Array.from(sys.neighborNodeIdx as Int32Array),
    neighborWeight: Array.from(sys.neighborWeight as Float32Array),
    maxNeighbors: MAXN,
  })
);

// ---- PresetGeometry.json from the LIVE system (post bakeCollisionFreeRestPose), plugin format + optional extras ----
const exported: any[] = hair.exportLockGeometry(physics.strandWidthScale);
exported.forEach((e: any, i: number) => {
  const lk = locks[i];
  e.layerOrder = lk.layerOrder;
  e.mass = lk.mass;
  e.restMidR = Math.round(lk.restMidR * 1e6) / 1e6;
  e.rootRawY = Math.round(lk.particles[0].rawRestLocal.y * 1e6) / 1e6;
  e.stiffnessWeight = lk.particles.map((p: any) => Math.round(p.stiffnessWeight * 1e6) / 1e6);
});
fs.writeFileSync(
  path.join(outDir, 'PresetGeometry.json'),
  JSON.stringify({
    schemaVersion: '2.1',
    sourcePivot: [0, 0.44, 0],
    strandWidthScale: physics.strandWidthScale,
    presetId: recipe.presetId,
    presetName: recipe.customName ?? recipe.presetId,
    coordinateSystem: 'blender_z_up_neg_y_front',
    solverExtras: 'layerOrder,mass,restMidR,rootRawY,stiffnessWeight per lock (live web system, widthScale=baked)',
    lockCount: L,
    locks: exported,
  })
);

// ---- run ----
dumpFrame(0);
impulses.push([0, 0, 0]);
const dt = 1 / 60;
for (let f = 0; f < frames; f++) {
  lastImpulse = [0, 0, 0];
  applyMotionPose(mode, f * dt);
  hair.step(dt, headPivot, rootGroup, physics);
  dumpFrame(f + 1);
  impulses.push(lastImpulse);
}
// headPos[0]/headQuat[0] describe the snapToHead pose; headPos[f+1] is the pose used by step f.
fs.writeFileSync(path.join(outDir, 'trace.bin'), Buffer.from(buf.buffer));
fs.writeFileSync(
  path.join(outDir, 'trace.json'),
  JSON.stringify({
    recipe: path.basename(path.dirname(path.resolve(recipePath))),
    mode,
    frames,
    dt,
    lockCount: L,
    nodesPerLock: N,
    space: 'three.js world, RH Y-up +Z front, meters; headPivot rest (0,0.44,0); rootGroup identity at origin',
    layout: 'Float32 LE [frame 0..frames][lock][node][xyz]; frame0 = after snapToHead',
    physics,
    headPos,
    headQuat,
    impulse: impulses,
  })
);
console.log(`${recipe.customName ?? recipe.presetId} locks=${L} mode=${mode} frames=${frames} -> ${outDir}`);
