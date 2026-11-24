import * as THREE from 'three';
import {
  HairBuilderArchetype,
  HairBuilderConfig,
  HairPresetId,
  PhysicsConfig,
  ShaderConfig,
} from '../types/hair';

export interface LockBuildSpec {
  pointsLocal: THREE.Vector3[];
  width: number;
  thickness: number;
  stiffnessBias: number;
  layerAO: number;
  layerOrder: number; // 0 = baseShell, 1 = inner nape/underlayer, 2 = mid sides/back, 3 = outer crown/bangs, 4 = accent spikes/flyaways
  customOutDir?: THREE.Vector3;
  isBaseShell?: boolean;
  lockNormal?: boolean;
  scalpRoot?: number;
}

export interface CuratedHairPalette {
  id: string;
  label: string;
  shader: ShaderConfig;
}

export interface ArchetypeMeta {
  id: HairBuilderArchetype;
  label: string;
  inspiration: string;
}

export interface RandomizedHairstyleResult {
  name: string;
  paletteLabel: string;
  basePreset: HairPresetId;
  builder: HairBuilderConfig;
  shader: ShaderConfig;
  physics: Omit<PhysicsConfig, 'showColliders' | 'showBones'>;
}

const HEAD_CENTER_LOCAL = new THREE.Vector3(0, 0.425, -0.01);
const SCALP_RX = 0.282;
const SCALP_RY = 0.318;
const SCALP_RZ = 0.31;
const NODES_PER_LOCK = 12;

export const ARCHETYPE_METADATA: ArchetypeMeta[] = [
  { id: 'layered-shag', label: 'Layered Shag', inspiration: 'Squall / Leon' },
  { id: 'spiky-hero', label: 'SOLDIER Spikes', inspiration: 'Cloud / Zack' },
  { id: 'long-cascade', label: 'Silky Cascade', inspiration: 'Sephiroth' },
  { id: 'wolf-cut', label: 'Crown Wolf-Cut', inspiration: 'Noctis / Vincent' },
  { id: 'sleek-bob', label: 'Sleek Blade Bob', inspiration: '2B / Yuna' },
  { id: 'asym-cascade', label: 'Valkyrie Asym', inspiration: 'Lightning' },
  { id: 'dolphin-long', label: 'Dolphin-Tail', inspiration: 'Tifa' },
  { id: 'ribbon-halfup', label: 'Half-Up Cascade', inspiration: 'Aerith / Jill' },
  { id: 'swept-back', label: 'Swept-Back', inspiration: 'Clive / Vergil' },
  { id: 'high-ponytail', label: 'Battle Ponytail', inspiration: 'Yuna X-2 / Fang' },
  { id: 'twin-tails', label: 'Dual Twin-Tails', inspiration: 'JRPG Twin Cascade' },
  { id: 'emo-sweep', label: 'Side-Swept Fringe', inspiration: 'Riku / Dante' },
  { id: 'short-crop', label: 'Short Crop', inspiration: 'Veteran / Soldier crop' },
  { id: 'undercut-fade', label: 'Undercut Fade', inspiration: 'Rogue / mercenary shaved sides' },
  { id: 'braided-crown', label: 'Braided Crown', inspiration: 'Fantasy healer / noble braids' },
  { id: 'samurai-topknot', label: 'Samurai Topknot', inspiration: 'Ronin / warrior high bun' },
];

export const ALL_ARCHETYPES: HairBuilderArchetype[] = ARCHETYPE_METADATA.map((a) => a.id);

function seededRand(seed: number): () => number {
  let s = seed >>> 0;
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0;
    return s / 4294967296;
  };
}

function skullSurfacePoint(theta: number, phi: number, lift = 0): THREE.Vector3 {
  const sinPhi = Math.sin(phi);
  const cosPhi = Math.cos(phi);
  return new THREE.Vector3(
    HEAD_CENTER_LOCAL.x + Math.sin(theta) * sinPhi * (SCALP_RX + lift),
    HEAD_CENTER_LOCAL.y + cosPhi * (SCALP_RY + lift),
    HEAD_CENTER_LOCAL.z + Math.cos(theta) * sinPhi * (SCALP_RZ + lift)
  );
}

function buildSculptedLock(params: {
  thetaStart: number;
  thetaEnd: number;
  phiStart: number;
  phiEnd: number;
  baseLift: number;
  midArch: number;
  tipFlickOut: number;
  tipDropExtra?: number;
  sCurveAmp?: number;
  napeHug?: number;
  rootOverride?: THREE.Vector3;
}): THREE.Vector3[] {
  const {
    thetaStart,
    thetaEnd,
    phiStart,
    phiEnd,
    baseLift,
    midArch,
    tipFlickOut,
    tipDropExtra = 0,
    sCurveAmp = 0,
    napeHug = 1.0,
    rootOverride,
  } = params;

  const eqPhi = 1.75;
  const pts: THREE.Vector3[] = [];

  // Anatomically seat rootOverride onto real skull dome without altering thetaStart (prevents cross-head wrap!)
  let seatedRoot: THREE.Vector3 | null = null;
  let effPhiStart = phiStart;
  if (rootOverride) {
    const dx = rootOverride.x - HEAD_CENTER_LOCAL.x;
    const dy = rootOverride.y - HEAD_CENTER_LOCAL.y;
    const dz = rootOverride.z - HEAD_CENTER_LOCAL.z;
    const nx = dx / SCALP_RX;
    const ny = dy / SCALP_RY;
    const nz = dz / SCALP_RZ;
    const nLen = Math.sqrt(nx * nx + ny * ny + nz * nz);
    if (nLen > 0.0001) {
      const ux = nx / nLen;
      const uy = THREE.MathUtils.clamp(ny / nLen, -1, 1);
      const uz = nz / nLen;
      const rPhi = Math.acos(uy);
      const rTheta = Math.atan2(ux, uz);
      const seatLift = THREE.MathUtils.clamp(baseLift * 0.72, 0.006, 0.022);
      seatedRoot = skullSurfacePoint(rTheta, rPhi, seatLift);
      effPhiStart = Math.min(phiEnd - 0.18, Math.max(phiStart, rPhi * 0.94));
    } else {
      seatedRoot = rootOverride.clone();
    }
  }

  for (let i = 0; i < NODES_PER_LOCK; i++) {
    const t = i / (NODES_PER_LOCK - 1);
    const smoothT = t * t * (3 - 2 * t);

    const theta =
      THREE.MathUtils.lerp(thetaStart, thetaEnd, smoothT) +
      Math.sin(t * Math.PI * 1.45) * sCurveAmp;
    const phi = THREE.MathUtils.lerp(effPhiStart, phiEnd, Math.pow(t, 0.94));

    const arch = Math.sin(t * Math.PI) * midArch;
    const rootSink = -0.004 * Math.pow(1.0 - Math.min(1.0, t * 4.5), 2.0);
    const flick = Math.pow(Math.max(0, (t - 0.62) / 0.38), 2.0) * tipFlickOut;
    const lift = baseLift + rootSink + arch + flick;

    let pt: THREE.Vector3;
    if (phi <= eqPhi) {
      pt = skullSurfacePoint(theta, phi, lift);
    } else {
      const eqPt = skullSurfacePoint(theta, eqPhi, baseLift + rootSink + arch);
      const extraPhi = phi - eqPhi;
      const dropDist = extraPhi * 0.325;
      const outX = Math.sin(theta);
      const outZ = Math.cos(theta);
      const isRearFactor = THREE.MathUtils.clamp((-outZ + 0.25) / 1.25, 0, 1);
      const inwardHug =
        napeHug *
        (isRearFactor * Math.min(0.115, extraPhi * 0.095) +
          (1 - isRearFactor) * Math.min(0.055, extraPhi * 0.042));

      pt = new THREE.Vector3(
        eqPt.x + outX * (flick - inwardHug),
        eqPt.y - dropDist,
        eqPt.z + outZ * (flick - inwardHug * (0.85 + 0.35 * isRearFactor))
      );
    }

    if (tipDropExtra !== 0) {
      pt.y -= Math.pow(t, 1.55) * tipDropExtra;
    }

    if (seatedRoot && i < 3) {
      const blend = Math.pow(1 - i / 3, 2.0);
      pt.lerp(seatedRoot, blend);
    }

    pts.push(pt);
  }
  return pts;
}

/**
 * Builds a graceful gathered tail lock (for Battle Ponytail, Dual Twin-Tails, or Half-Up Cascade)
 * rooted directly on the skull surface so rootClearM stays cleanly anchored with zero floating roots!
 */
function buildGatheredTailLock(params: {
  anchorTheta: number;
  anchorPhi: number;
  outDir: THREE.Vector3;
  spreadX: number;
  spreadZ: number;
  length: number;
  archOut: number;
  waveAmp: number;
  wavePhase: number;
}): THREE.Vector3[] {
  const { anchorTheta, anchorPhi, outDir, spreadX, spreadZ, length, archOut, waveAmp, wavePhase } = params;
  const root = skullSurfacePoint(anchorTheta, anchorPhi, 0.015);
  const pts: THREE.Vector3[] = [];
  const normOut = outDir.clone().normalize();

  for (let i = 0; i < NODES_PER_LOCK; i++) {
    const t = i / (NODES_PER_LOCK - 1);
    const bell = Math.sin(Math.min(1, t * 1.5) * Math.PI * 0.65);
    const drop = Math.pow(t, 1.28) * length;
    const outPush = bell * archOut + t * 0.05;
    const fan = Math.sin(t * Math.PI * 0.85);
    const wave = Math.sin(t * Math.PI * 2.1 + wavePhase) * waveAmp * t;

    pts.push(
      new THREE.Vector3(
        root.x + normOut.x * outPush + spreadX * fan + wave,
        root.y + Math.sin(t * Math.PI * 0.55) * (archOut * 0.34) - drop,
        root.z + normOut.z * outPush + spreadZ * fan
      )
    );
  }
  return pts;
}

export const CURATED_HAIR_PALETTES: CuratedHairPalette[] = [
  {
    id: 'obsidian',
    label: 'Smoky Obsidian',
    shader: {
      rootColor: '#090a0c',
      baseColor: '#25272b',
      tipColor: '#4c5057',
      primarySpecColor: '#c8ccd2',
      secondarySpecColor: '#5c6068',
      primaryShift: 0.05,
      primaryPower: 68,
      primaryIntensity: 0.36,
      secondaryShift: -0.14,
      secondaryPower: 26,
      secondaryIntensity: 0.22,
      rimIntensity: 0.44,
      rimPower: 2.8,
      rootAO: 0.68,
      grooveDepth: 0.76,
      strandVariation: 0.25,
    },
  },
  {
    id: 'chestnut',
    label: 'Warm Chestnut',
    shader: {
      rootColor: '#26180f',
      baseColor: '#4c3322',
      tipColor: '#674730',
      primarySpecColor: '#d9ba9c',
      secondarySpecColor: '#8c5d3a',
      primaryShift: 0.08,
      primaryPower: 84,
      primaryIntensity: 0.34,
      secondaryShift: -0.15,
      secondaryPower: 32,
      secondaryIntensity: 0.2,
      rimIntensity: 0.28,
      rimPower: 3.4,
      rootAO: 0.46,
      grooveDepth: 0.68,
      strandVariation: 0.22,
    },
  },
  {
    id: 'gold',
    label: 'SOLDIER Gold',
    shader: {
      rootColor: '#5c401a',
      baseColor: '#b3893b',
      tipColor: '#d4b05d',
      primarySpecColor: '#fff0c7',
      secondarySpecColor: '#c48f2e',
      primaryShift: 0.09,
      primaryPower: 92,
      primaryIntensity: 0.36,
      secondaryShift: -0.14,
      secondaryPower: 34,
      secondaryIntensity: 0.22,
      rimIntensity: 0.3,
      rimPower: 3.3,
      rootAO: 0.44,
      grooveDepth: 0.66,
      strandVariation: 0.19,
    },
  },
  {
    id: 'platinum',
    label: 'Platinum Silver',
    shader: {
      rootColor: '#414957',
      baseColor: '#919cab',
      tipColor: '#c2ccd9',
      primarySpecColor: '#f2f6ff',
      secondarySpecColor: '#92a6c4',
      primaryShift: 0.06,
      primaryPower: 102,
      primaryIntensity: 0.36,
      secondaryShift: -0.16,
      secondaryPower: 38,
      secondaryIntensity: 0.2,
      rimIntensity: 0.32,
      rimPower: 3.2,
      rootAO: 0.42,
      grooveDepth: 0.64,
      strandVariation: 0.17,
    },
  },
  {
    id: 'crimson',
    label: 'Crimson Auburn',
    shader: {
      rootColor: '#2e100c',
      baseColor: '#69251c',
      tipColor: '#8f392a',
      primarySpecColor: '#ebbba0',
      secondarySpecColor: '#a33c24',
      primaryShift: 0.08,
      primaryPower: 84,
      primaryIntensity: 0.34,
      secondaryShift: -0.15,
      secondaryPower: 32,
      secondaryIntensity: 0.22,
      rimIntensity: 0.3,
      rimPower: 3.3,
      rootAO: 0.44,
      grooveDepth: 0.66,
      strandVariation: 0.2,
    },
  },
  {
    id: 'valkyrie-rose',
    label: 'Valkyrie Rose',
    shader: {
      rootColor: '#54313a',
      baseColor: '#b87b88',
      tipColor: '#dfabb5',
      primarySpecColor: '#fff0f3',
      secondarySpecColor: '#c77d90',
      primaryShift: 0.07,
      primaryPower: 94,
      primaryIntensity: 0.36,
      secondaryShift: -0.15,
      secondaryPower: 34,
      secondaryIntensity: 0.2,
      rimIntensity: 0.32,
      rimPower: 3.2,
      rootAO: 0.42,
      grooveDepth: 0.64,
      strandVariation: 0.18,
    },
  },
  {
    id: 'clive-charcoal',
    label: 'Rosaria Charcoal',
    shader: {
      rootColor: '#121010',
      baseColor: '#262120',
      tipColor: '#403633',
      primarySpecColor: '#cfc2b8',
      secondarySpecColor: '#594842',
      primaryShift: 0.06,
      primaryPower: 74,
      primaryIntensity: 0.35,
      secondaryShift: -0.14,
      secondaryPower: 28,
      secondaryIntensity: 0.22,
      rimIntensity: 0.38,
      rimPower: 3.0,
      rootAO: 0.62,
      grooveDepth: 0.74,
      strandVariation: 0.24,
    },
  },
  {
    id: 'tifa-jet',
    label: 'Seventh Jet Silk',
    shader: {
      rootColor: '#0b090a',
      baseColor: '#1c181a',
      tipColor: '#382d30',
      primarySpecColor: '#d9ccd0',
      secondarySpecColor: '#594148',
      primaryShift: 0.06,
      primaryPower: 96,
      primaryIntensity: 0.38,
      secondaryShift: -0.15,
      secondaryPower: 36,
      secondaryIntensity: 0.22,
      rimIntensity: 0.4,
      rimPower: 2.9,
      rootAO: 0.64,
      grooveDepth: 0.72,
      strandVariation: 0.2,
    },
  },
  {
    id: 'aerith-amber',
    label: 'Ancient Amber',
    shader: {
      rootColor: '#362112',
      baseColor: '#6e4528',
      tipColor: '#99643d',
      primarySpecColor: '#ffe0bd',
      secondarySpecColor: '#a66838',
      primaryShift: 0.08,
      primaryPower: 88,
      primaryIntensity: 0.36,
      secondaryShift: -0.15,
      secondaryPower: 32,
      secondaryIntensity: 0.21,
      rimIntensity: 0.3,
      rimPower: 3.3,
      rootAO: 0.44,
      grooveDepth: 0.66,
      strandVariation: 0.21,
    },
  },
  {
    id: 'tidus-sun',
    label: 'Zanarkand Blonde',
    shader: {
      rootColor: '#634d28',
      baseColor: '#bfa060',
      tipColor: '#e3ca8a',
      primarySpecColor: '#fff9e0',
      secondarySpecColor: '#cfb069',
      primaryShift: 0.08,
      primaryPower: 94,
      primaryIntensity: 0.36,
      secondaryShift: -0.14,
      secondaryPower: 36,
      secondaryIntensity: 0.2,
      rimIntensity: 0.3,
      rimPower: 3.3,
      rootAO: 0.4,
      grooveDepth: 0.64,
      strandVariation: 0.19,
    },
  },
  {
    id: 'dante-frost',
    label: 'Frost Ash-White',
    shader: {
      rootColor: '#656c78',
      baseColor: '#bcc4d0',
      tipColor: '#e0e6f0',
      primarySpecColor: '#ffffff',
      secondarySpecColor: '#b3c0d4',
      primaryShift: 0.05,
      primaryPower: 108,
      primaryIntensity: 0.36,
      secondaryShift: -0.16,
      secondaryPower: 40,
      secondaryIntensity: 0.18,
      rimIntensity: 0.3,
      rimPower: 3.3,
      rootAO: 0.38,
      grooveDepth: 0.62,
      strandVariation: 0.16,
    },
  },
  {
    id: 'riku-periwinkle',
    label: 'Dawn Periwinkle',
    shader: {
      rootColor: '#3d455e',
      baseColor: '#818ead',
      tipColor: '#b2bed9',
      primarySpecColor: '#edf2ff',
      secondarySpecColor: '#7f92bf',
      primaryShift: 0.06,
      primaryPower: 96,
      primaryIntensity: 0.36,
      secondaryShift: -0.15,
      secondaryPower: 36,
      secondaryIntensity: 0.2,
      rimIntensity: 0.34,
      rimPower: 3.1,
      rootAO: 0.44,
      grooveDepth: 0.65,
      strandVariation: 0.18,
    },
  },
  {
    id: 'yuna-espresso',
    label: 'Besaid Espresso',
    shader: {
      rootColor: '#1f1410',
      baseColor: '#3f2920',
      tipColor: '#5c3e30',
      primarySpecColor: '#d9bfb0',
      secondarySpecColor: '#734d3b',
      primaryShift: 0.07,
      primaryPower: 86,
      primaryIntensity: 0.34,
      secondaryShift: -0.15,
      secondaryPower: 32,
      secondaryIntensity: 0.2,
      rimIntensity: 0.3,
      rimPower: 3.3,
      rootAO: 0.5,
      grooveDepth: 0.68,
      strandVariation: 0.21,
    },
  },
  {
    id: 'cobalt',
    label: 'Midnight Cobalt',
    shader: {
      rootColor: '#0e1629',
      baseColor: '#22355c',
      tipColor: '#3d5c8f',
      primarySpecColor: '#b8d4ff',
      secondarySpecColor: '#496da8',
      primaryShift: 0.07,
      primaryPower: 88,
      primaryIntensity: 0.36,
      secondaryShift: -0.15,
      secondaryPower: 34,
      secondaryIntensity: 0.22,
      rimIntensity: 0.38,
      rimPower: 3.0,
      rootAO: 0.56,
      grooveDepth: 0.7,
      strandVariation: 0.22,
    },
  },
  {
    id: 'ash-violet',
    label: 'Ash Lavender',
    shader: {
      rootColor: '#241e33',
      baseColor: '#544a6e',
      tipColor: '#8378a3',
      primarySpecColor: '#e4dcf7',
      secondarySpecColor: '#75669c',
      primaryShift: 0.07,
      primaryPower: 94,
      primaryIntensity: 0.35,
      secondaryShift: -0.15,
      secondaryPower: 34,
      secondaryIntensity: 0.2,
      rimIntensity: 0.34,
      rimPower: 3.1,
      rootAO: 0.46,
      grooveDepth: 0.65,
      strandVariation: 0.2,
    },
  },
  {
    id: 'copper',
    label: 'Copper Ember',
    shader: {
      rootColor: '#38190d',
      baseColor: '#874122',
      tipColor: '#b86437',
      primarySpecColor: '#ffd7b5',
      secondarySpecColor: '#ba5a2c',
      primaryShift: 0.09,
      primaryPower: 86,
      primaryIntensity: 0.36,
      secondaryShift: -0.14,
      secondaryPower: 32,
      secondaryIntensity: 0.22,
      rimIntensity: 0.32,
      rimPower: 3.2,
      rootAO: 0.44,
      grooveDepth: 0.66,
      strandVariation: 0.21,
    },
  },
  {
    id: 'champagne',
    label: 'Champagne Ash',
    shader: {
      rootColor: '#4a4033',
      baseColor: '#9c8b75',
      tipColor: '#c7b8a3',
      primarySpecColor: '#fff7eb',
      secondarySpecColor: '#a89378',
      primaryShift: 0.07,
      primaryPower: 96,
      primaryIntensity: 0.35,
      secondaryShift: -0.15,
      secondaryPower: 36,
      secondaryIntensity: 0.2,
      rimIntensity: 0.3,
      rimPower: 3.3,
      rootAO: 0.42,
      grooveDepth: 0.64,
      strandVariation: 0.18,
    },
  },
  {
    id: 'raven-teal',
    label: 'Mako Emerald',
    shader: {
      rootColor: '#0c1c1c',
      baseColor: '#1d403e',
      tipColor: '#366b66',
      primarySpecColor: '#b5ede6',
      secondarySpecColor: '#3a756f',
      primaryShift: 0.06,
      primaryPower: 86,
      primaryIntensity: 0.36,
      secondaryShift: -0.14,
      secondaryPower: 32,
      secondaryIntensity: 0.22,
      rimIntensity: 0.36,
      rimPower: 3.0,
      rootAO: 0.56,
      grooveDepth: 0.72,
      strandVariation: 0.22,
    },
  },
  {
    id: 'burgundy-wine',
    label: 'Garlean Burgundy',
    shader: {
      rootColor: '#260b14',
      baseColor: '#591a2e',
      tipColor: '#822c47',
      primarySpecColor: '#f2c4d2',
      secondarySpecColor: '#8c2b4a',
      primaryShift: 0.08,
      primaryPower: 88,
      primaryIntensity: 0.35,
      secondaryShift: -0.15,
      secondaryPower: 32,
      secondaryIntensity: 0.21,
      rimIntensity: 0.34,
      rimPower: 3.1,
      rootAO: 0.5,
      grooveDepth: 0.68,
      strandVariation: 0.2,
    },
  },
  {
    id: 'sakura-silver',
    label: 'Sakura Mist',
    shader: {
      rootColor: '#5c4b54',
      baseColor: '#bfa4b1',
      tipColor: '#e6cfd9',
      primarySpecColor: '#fff5fa',
      secondarySpecColor: '#c99eb3',
      primaryShift: 0.06,
      primaryPower: 100,
      primaryIntensity: 0.35,
      secondaryShift: -0.16,
      secondaryPower: 36,
      secondaryIntensity: 0.19,
      rimIntensity: 0.32,
      rimPower: 3.2,
      rootAO: 0.4,
      grooveDepth: 0.63,
      strandVariation: 0.17,
    },
  },
];

const ARCHETYPE_LABELS: Record<HairBuilderArchetype, string> = ARCHETYPE_METADATA.reduce(
  (acc, item) => {
    acc[item.id] = item.label;
    return acc;
  },
  {} as Record<HairBuilderArchetype, string>
);

/**
 * Generates a cohesive, anatomically sealed, layered Final Fantasy / JRPG hairstyle directly
 * from parametric HairBuilderConfig controls across all 12 distinct archetypes.
 */
export function buildCustomHairSpecs(config: HairBuilderConfig): LockBuildSpec[] {
  const rand = seededRand(config.seed || 1337);
  const specs: LockBuildSpec[] = [];

  const arch = config.archetype;
  const density = THREE.MathUtils.clamp(config.density, 0.65, 1.25);
  const wMul = THREE.MathUtils.clamp(config.strandWidth, 0.75, 1.25);
  const partX = THREE.MathUtils.clamp(config.partPosition, -1, 1) * 0.095;
  const crownLift = 0.014 + config.crownVolume * 0.014;
  const shag = THREE.MathUtils.clamp(config.layerShag, 0, 1);
  const spikeAmt = THREE.MathUtils.clamp(config.spikeLift, 0, 1);
  const bangsLenPhi = THREE.MathUtils.lerp(1.48, 2.34, config.bangsLength);
  const isCrop = arch === 'short-crop';
  const sideLenPhi = isCrop ? THREE.MathUtils.lerp(1.45, 1.85, config.sideLength) : THREE.MathUtils.lerp(1.94, 3.04, config.sideLength);
  const backLenPhi = isCrop ? THREE.MathUtils.lerp(2.1, 2.7, config.backLength) : THREE.MathUtils.lerp(2.24, 3.48, config.backLength);

  const isBob = arch === 'sleek-bob';
  const isAsym = arch === 'asym-cascade';
  const isDolphin = arch === 'dolphin-long';
  const isHalfUp = arch === 'ribbon-halfup';
  const isSweptBack = arch === 'swept-back' || isCrop; // short-crop reuses the brushed-back crown with short length caps
  const isPonytail = arch === 'high-ponytail';
  const isTwinTails = arch === 'twin-tails';
  const isEmoSweep = arch === 'emo-sweep';
  const isLong = arch === 'long-cascade' || isDolphin || isHalfUp || config.backLength > 0.78;
  const isSpiky = arch === 'spiky-hero' || spikeAmt > 0.45;
  const isPulledScalp = isPonytail || isTwinTails;
  const napeHugBase = isBob || isPulledScalp ? 1.26 : isLong ? 0.84 : 1.06;

  // NEON-SEED samurai-topknot
  if (arch === 'samurai-topknot') {
    const knotTheta = Math.PI;
    const knotPhi = 0.3;
    const knotAnchor = skullSurfacePoint(knotTheta, knotPhi, 0.016);

    // Zone 1: Tight sleek base shell (ears exposed, clean nape)
    for (let tier = 0; tier < 3; tier++) {
      const count = 28;
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + tier * 0.21;
        const cosT = Math.cos(theta);
        const phiStart = tier === 0 ? 0.02 : 0.24 + tier * 0.36;
        const maxPhi = cosT > 0.35 ? 1.3 : cosT < -0.25 ? 1.95 : 1.6;
        if (phiStart >= maxPhi - 0.05) continue;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart, phiEnd: Math.min(maxPhi, phiStart + 0.72),
          baseLift: 0.003, midArch: 0.003, tipFlickOut: 0.0, napeHug: 1.35,
        });
        specs.push({
          pointsLocal: pts, width: 0.1 * wMul, thickness: 0.01, stiffnessBias: 0.98, layerAO: 0.8,
          layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4,
        });
      }
    }

    // Zone 2: pulled-back scalp strands converging into the knot
    const pullCount = Math.round(46 * density);
    for (let i = 0; i < pullCount; i++) {
      const theta = (i / pullCount) * Math.PI * 2 + (rand() - 0.5) * 0.05;
      const cosT = Math.cos(theta);
      const phiStart = cosT > 0.35 ? 1.28 : cosT < -0.25 ? 1.9 : 1.56;
      const targetTheta = theta > 0 ? Math.PI : -Math.PI;
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: THREE.MathUtils.lerp(theta, targetTheta, 0.72), phiStart, phiEnd: knotPhi,
        baseLift: 0.007 + config.crownVolume * 0.006, midArch: 0.004, tipFlickOut: 0.0, napeHug: 1.3,
      });
      pts[NODES_PER_LOCK - 1].lerp(knotAnchor, 0.85);
      pts[NODES_PER_LOCK - 2].lerp(knotAnchor, 0.45);
      specs.push({
        pointsLocal: pts, width: (0.03 + rand() * 0.01) * wMul, thickness: 0.01, stiffnessBias: 0.97,
        layerAO: 0.88, layerOrder: 2, lockNormal: false, scalpRoot: 0.35,
      });
    }

    // Zone 3A: compact topknot bun core
    const bunCount = Math.round(34 * density);
    for (let i = 0; i < bunCount; i++) {
      const ang = (i / bunCount) * Math.PI * 2;
      const pts = buildGatheredTailLock({
        anchorTheta: knotTheta + Math.cos(ang) * 0.08, anchorPhi: knotPhi + Math.sin(ang) * 0.06,
        outDir: new THREE.Vector3(Math.cos(ang) * 0.3, 0.9, -0.4),
        spreadX: Math.cos(ang) * 0.06, spreadZ: Math.sin(ang) * 0.06,
        length: 0.22 + config.crownVolume * 0.06, archOut: 0.105, waveAmp: 0.004, wavePhase: ang,
      });
      specs.push({
        pointsLocal: pts, width: 0.05 * wMul, thickness: 0.014, stiffnessBias: 0.94, layerAO: 0.86,
        layerOrder: 3, lockNormal: false, scalpRoot: 0.5,
      });
    }

    // Zone 3B: short flared warrior tail behind the tie
    const tailCount = Math.round(16 * density);
    for (let i = 0; i < tailCount; i++) {
      const ang = (i / tailCount) * Math.PI * 2;
      const pts = buildGatheredTailLock({
        anchorTheta: knotTheta + Math.cos(ang) * 0.05, anchorPhi: knotPhi + 0.03,
        outDir: new THREE.Vector3(Math.cos(ang) * 0.22, 0.28 + spikeAmt * 0.24, -0.92),
        spreadX: Math.cos(ang) * 0.055, spreadZ: Math.sin(ang) * 0.035,
        length: 0.14 + config.backLength * 0.14, archOut: 0.095, waveAmp: 0.008, wavePhase: ang * 1.5,
      });
      specs.push({
        pointsLocal: pts, width: (0.026 + rand() * 0.01) * wMul, thickness: 0.011, stiffnessBias: 0.76,
        layerAO: 0.92, layerOrder: 4, lockNormal: false, scalpRoot: 0.4,
      });
    }

    return specs;
  }

  // NEON-SEED braided-crown
  if (arch === 'braided-crown') {
    const backEndPhi = THREE.MathUtils.lerp(2.15, 2.85, config.backLength);
    const sideEndPhi = THREE.MathUtils.lerp(1.92, 2.45, config.sideLength);

    // Zone 1: Smooth base shell
    for (let tier = 0; tier < 3; tier++) {
      const count = 18;
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + tier * 0.22;
        const cosT = Math.cos(theta);
        if (tier >= 1 && cosT > 0.52) continue;
        const isBack = cosT < -0.15;
        const phiStart = tier === 0 ? 0.01 : 0.2 + tier * 0.38;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart,
          phiEnd: Math.min(isBack ? backEndPhi - 0.1 : sideEndPhi - 0.12, phiStart + 1.25),
          baseLift: 0.004, midArch: 0.006, tipFlickOut: 0.002, napeHug: 0.96,
        });
        specs.push({
          pointsLocal: pts, width: 0.084 * wMul, thickness: 0.012, stiffnessBias: 0.88, layerAO: 0.82,
          layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: tier === 0 ? 0.48 : 0.22,
        });
      }
    }

    // Zone 2: Center-parted smooth crown + under-hair
    const canopyCount = Math.round(38 * density);
    for (let i = 0; i < canopyCount; i++) {
      const theta = (i / canopyCount) * Math.PI * 2 + (rand() - 0.5) * 0.05;
      const cosT = Math.cos(theta);
      if (cosT > 0.65) continue;
      const isBack = cosT < -0.18;
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: theta + (Math.sin(theta) > 0 ? 0.05 : -0.05),
        phiStart: 0.06 + rand() * 0.18, phiEnd: isBack ? backEndPhi : sideEndPhi,
        baseLift: 0.012 + config.crownVolume * 0.01, midArch: 0.01, tipFlickOut: 0.006, napeHug: 0.92,
      });
      specs.push({
        pointsLocal: pts, width: (0.044 + rand() * 0.012) * wMul, thickness: 0.012, stiffnessBias: 0.74,
        layerAO: 0.88, layerOrder: 2, lockNormal: false, scalpRoot: 0.35,
      });
    }

    // Soft temple framing wisps
    for (const side of [-1, 1]) {
      for (let k = 0; k < 3; k++) {
        const pts = buildSculptedLock({
          thetaStart: side * (0.48 + k * 0.12), thetaEnd: side * (0.76 + k * 0.1),
          phiStart: 0.84 + k * 0.05, phiEnd: 1.65 + config.bangsLength * 0.36 + k * 0.08,
          baseLift: 0.01, midArch: 0.012, tipFlickOut: 0.008, napeHug: 0.85,
        });
        specs.push({
          pointsLocal: pts, width: 0.024 * wMul, thickness: 0.009, stiffnessBias: 0.68, layerAO: 0.94,
          layerOrder: 3, lockNormal: false, scalpRoot: 0.25,
        });
      }
    }

    // Zone 3: Twin woven side braids above the ears from temples to occipital join
    for (const side of [-1, 1]) {
      for (let s = 0; s < 6; s++) {
        const phase = s % 2 === 0 ? 1 : -1;
        const offsetPhi = (s - 2.5) * 0.024;
        const pts = buildSculptedLock({
          thetaStart: side * (0.44 + (s % 3) * 0.05), thetaEnd: side * 3.04,
          phiStart: 0.88 + offsetPhi, phiEnd: 1.42 + offsetPhi * 0.6,
          baseLift: 0.024 + (s % 3) * 0.004, midArch: 0.009, tipFlickOut: 0.0, sCurveAmp: phase * 0.048, napeHug: 1.25,
        });
        specs.push({
          pointsLocal: pts, width: 0.034 * wMul, thickness: 0.016, stiffnessBias: 0.95,
          layerAO: 0.9 - (s % 2) * 0.08, layerOrder: 3, lockNormal: false, scalpRoot: 0.45,
        });
      }
    }

    // Zone 4: Central woven back braid from the occipital join
    const braidCount = Math.round(18 * density);
    for (let i = 0; i < braidCount; i++) {
      const phase = (i / braidCount) * Math.PI * 2;
      const pts = buildGatheredTailLock({
        anchorTheta: Math.PI + Math.cos(phase) * 0.06, anchorPhi: 1.42 + Math.sin(phase) * 0.03,
        outDir: new THREE.Vector3(Math.cos(phase) * 0.14, -0.18, -0.96),
        spreadX: Math.cos(phase) * 0.028, spreadZ: Math.sin(phase) * 0.022,
        length: 0.36 + config.backLength * 0.28, archOut: 0.042, waveAmp: 0.018, wavePhase: phase * 2.0,
      });
      specs.push({
        pointsLocal: pts, width: 0.032 * wMul, thickness: 0.015, stiffnessBias: 0.64, layerAO: 0.9,
        layerOrder: 4, lockNormal: false, scalpRoot: 0.4,
      });
    }

    return specs;
  }

  // NEON-SEED undercut-fade
  if (arch === 'undercut-fade') {
    const sweepSign = config.bangsSweep >= 0 ? 1 : -1;
    const fadeSideEnd = THREE.MathUtils.lerp(1.5, 1.66, config.sideLength); // fade ends at the ear top
    const fadeNapeEnd = THREE.MathUtils.lerp(1.9, 2.1, config.backLength);
    const topSweepAmt = 0.48 + Math.abs(config.bangsSweep) * 0.36;
    const frontHairline = 1.28;

    // Zone 1: skin-tight faded scalp shell (dense: no scalp showing)
    for (let tier = 0; tier < 3; tier++) {
      const count = 28;
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + tier * 0.2;
        const cosT = Math.cos(theta);
        const maxPhi = cosT > 0.35 ? frontHairline - 0.04 : cosT < -0.25 ? fadeNapeEnd - 0.04 : fadeSideEnd - 0.04;
        const phiStart = tier === 0 ? 0.01 : 0.22 + tier * 0.34;
        if (phiStart >= maxPhi - 0.05) continue;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart, phiEnd: Math.min(maxPhi, phiStart + 0.7),
          baseLift: 0.002, midArch: 0.001, tipFlickOut: 0.0, napeHug: 1.4,
        });
        specs.push({
          pointsLocal: pts, width: 0.095 * wMul, thickness: 0.008, stiffnessBias: 0.99, layerAO: 0.76,
          layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: 0.4,
        });
      }
    }

    // Zone 2: ultra-short close-shaved sides & nape below the disconnect
    const fadeCount = Math.round(60 * density);
    for (let i = 0; i < fadeCount; i++) {
      const theta = (i / fadeCount) * Math.PI * 2 + (rand() - 0.5) * 0.05;
      const cosT = Math.cos(theta);
      if (cosT > 0.46) continue;
      const isBack = cosT < -0.25;
      const maxPhi = isBack ? fadeNapeEnd : fadeSideEnd;
      const phiStart = isBack ? 1.1 + rand() * 0.3 : 1.0 + rand() * 0.2;
      const pts = buildSculptedLock({
        thetaStart: theta, thetaEnd: theta, phiStart, phiEnd: Math.min(maxPhi, phiStart + 0.3),
        baseLift: 0.003, midArch: 0.001, tipFlickOut: 0.0, napeHug: 1.45,
      });
      specs.push({
        pointsLocal: pts, width: 0.028 * wMul, thickness: 0.007, stiffnessBias: 0.99, layerAO: 0.74,
        layerOrder: 1, lockNormal: false, scalpRoot: 0.35,
      });
    }

    // Zone 3: disconnected textured top locks swept diagonally across the crown
    const topRings = 4;
    for (let r = 0; r < topRings; r++) {
      const rf = r / (topRings - 1);
      const count = Math.round((16 + r * 6) * density);
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + r * 0.25 + (rand() - 0.5) * 0.08;
        const cosT = Math.cos(theta);
        const phiStart = 0.05 + rf * 0.64;
        const isFront = cosT > 0.2;
        const phiEnd = isFront
          ? Math.min(frontHairline + config.bangsLength * 0.12, phiStart + 0.5)
          : Math.min(1.2, phiStart + 0.46);
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta + sweepSign * topSweepAmt * (0.55 + rf * 0.45), phiStart, phiEnd,
          baseLift: 0.018 + config.crownVolume * 0.022, midArch: 0.012 + config.crownVolume * 0.014,
          tipFlickOut: 0.007 + spikeAmt * 0.014, napeHug: 1.15,
        });
        specs.push({
          pointsLocal: pts, width: (0.03 + rand() * 0.014) * wMul, thickness: 0.013, stiffnessBias: 0.86,
          layerAO: 0.9 + rf * 0.06, layerOrder: 3, lockNormal: false, scalpRoot: r === 0 ? 0.5 : 0.3,
        });
      }
    }

    return specs;
  }

  // NEON-SEED short-crop (standalone, early return)
  if (arch === 'short-crop') {
    const topLift = 0.011 + config.crownVolume * 0.015;
    const sideEndBase = THREE.MathUtils.lerp(1.55, 1.72, config.sideLength); // ends at the ear top (real crew cut taper)
    const napeEndBase = THREE.MathUtils.lerp(1.95, 2.15, config.backLength); // tapered nape
    const frontHairlinePhi = THREE.MathUtils.lerp(1.22, 1.36, config.bangsLength); // mid-forehead hairline (measured against the real photo)
    const sweepDir = THREE.MathUtils.clamp(config.bangsSweep, -1, 1);

    // Zone 1: Full dome near-skin foundation shell
    for (let tier = 0; tier < 4; tier++) {
      const count = tier === 0 ? 20 : 30;
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + tier * 0.19;
        const cosT = Math.cos(theta);
        const isFront = cosT > 0.38;
        const isBack = cosT < -0.22;
        const phiStart = tier === 0 ? 0.01 : 0.16 + tier * 0.28;
        const maxPhi = isFront ? frontHairlinePhi - 0.04 : isBack ? napeEndBase - 0.06 : sideEndBase - 0.05;
        if (phiStart >= maxPhi - 0.05) continue;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta, phiStart,
          phiEnd: Math.min(maxPhi, Math.max(phiStart + 0.3, phiStart + 0.72)),
          baseLift: 0.002 + tier * 0.001, midArch: 0.002, tipFlickOut: 0.001, napeHug: 1.35,
        });
        specs.push({
          pointsLocal: pts, width: 0.095 * wMul, thickness: 0.009, stiffnessBias: 0.99, layerAO: 0.84,
          layerOrder: 0, isBaseShell: true, lockNormal: false, scalpRoot: tier === 0 ? 0.5 : 0.25,
        });
      }
    }

    // Zone 2: Tapered near-skin sides & nape above the ears
    const sideRows = 4;
    for (let r = 0; r < sideRows; r++) {
      const rowFrac = r / (sideRows - 1);
      const count = Math.round(56 * density);
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + r * 0.11 + (rand() - 0.5) * 0.05;
        const cosT = Math.cos(theta);
        if (cosT > 0.48) continue;
        const isBack = cosT < -0.25;
        const maxPhi = isBack ? napeEndBase : sideEndBase;
        const phiStart = THREE.MathUtils.lerp(0.76, maxPhi - 0.22, rowFrac);
        const phiEnd = Math.min(maxPhi, phiStart + 0.24 + (1 - rowFrac) * 0.1);
        const taperLift = THREE.MathUtils.lerp(0.009, 0.003, rowFrac);
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta + (rand() - 0.5) * 0.04, phiStart, phiEnd,
          baseLift: taperLift, midArch: 0.002, tipFlickOut: 0.001, napeHug: 1.4,
        });
        specs.push({
          pointsLocal: pts, width: THREE.MathUtils.lerp(0.024, 0.018, rowFrac) * wMul, thickness: 0.008,
          stiffnessBias: 0.98, layerAO: 0.78 + rowFrac * 0.12, layerOrder: 1, lockNormal: false, scalpRoot: 0.35,
        });
      }
    }

    // Zone 3: Short brushed forward-up crown & hairline quiff
    const crownRings = 5;
    for (let ring = 0; ring < crownRings; ring++) {
      const rf = ring / (crownRings - 1);
      const count = Math.round((20 + ring * 8) * density);
      for (let i = 0; i < count; i++) {
        const theta = (i / count) * Math.PI * 2 + ring * 0.27 + (rand() - 0.5) * 0.08;
        const cosT = Math.cos(theta);
        const isFrontZone = cosT > 0.35;
        const phiStart = 0.04 + rf * (isFrontZone ? frontHairlinePhi - 0.34 : 0.72);
        const segLen = isFrontZone ? 0.34 + config.bangsLength * 0.1 : 0.3 + shag * 0.06;
        const phiEnd = Math.min(isFrontZone ? frontHairlinePhi : 1.2, phiStart + segLen);
        const forwardBias = isFrontZone ? sweepDir * 0.16 : -Math.sin(theta) * 0.08 + sweepDir * 0.08;
        const frontUpFlick = isFrontZone ? 0.008 + config.crownVolume * 0.01 : 0.003 + spikeAmt * 0.004;
        const pts = buildSculptedLock({
          thetaStart: theta, thetaEnd: theta + forwardBias, phiStart, phiEnd,
          baseLift: topLift * (1.0 - rf * 0.22), midArch: 0.004 + config.crownVolume * 0.003,
          tipFlickOut: frontUpFlick, tipDropExtra: isFrontZone ? -0.008 : 0, napeHug: 1.2,
        });
        specs.push({
          pointsLocal: pts, width: (0.02 + rand() * 0.009) * wMul, thickness: 0.01, stiffnessBias: 0.96,
          layerAO: 0.88 + rf * 0.08, layerOrder: 3, lockNormal: false, scalpRoot: ring === 0 ? 0.55 : 0.35,
        });
      }
    }

    return specs;
  }

  // =========================================================================
  // ZONE A: Seamless 360° Inner Foundation Shell (phiStart = 0.01 seals crown apex 100%)
  // =========================================================================
  for (let tier = 0; tier < 4; tier++) {
    const count = tier === 0 ? 18 : 15;
    for (let i = 0; i < count; i++) {
      const theta = (i / count) * Math.PI * 2 + tier * 0.24 + (rand() - 0.5) * 0.04;
      const cosT = Math.cos(theta);
      const phiStart = tier === 0 ? 0.01 : 0.14 + tier * 0.36;
      // Anatomical facial hairline cutout: keep bare forehead, brow corners, and cheeks 100% clear of base-shell slabs
      if (tier >= 1) {
        const maxCosT = phiStart < 0.72 ? 0.62 : phiStart < 1.12 ? 0.42 : 0.20;
        if (cosT > maxCosT) continue;
      }
      const isBack = cosT < -0.12;
      const shellCap = isPulledScalp ? 2.18 : isBack ? backLenPhi - 0.14 : sideLenPhi - 0.14;
      const targetEnd = Math.min(shellCap, phiStart + (isBack ? 1.18 : 0.96) + tier * 0.16);

      const pts = buildSculptedLock({
        thetaStart: theta,
        thetaEnd: theta + (isBack ? (Math.sin(theta) > 0 ? -0.08 : 0.08) : 0),
        phiStart,
        phiEnd: Math.max(phiStart + 0.65, targetEnd),
        baseLift: 0.004 + tier * 0.0015,
        midArch: 0.008,
        tipFlickOut: 0.003,
        napeHug: isBack ? napeHugBase : 0.92,
      });

      specs.push({
        pointsLocal: pts,
        width: 0.092 * wMul,
        thickness: 0.013,
        stiffnessBias: isLong && isBack ? 0.72 : 0.94,
        layerAO: 0.82,
        layerOrder: 0,
        isBaseShell: true,
        lockNormal: false,
        scalpRoot: tier === 0 ? 0.48 : 0.2,
      });
    }
  }

  // =========================================================================
  // ZONE B: Crown Canopy (Archetype-Aware: Pulled-Back, Swept-Back, Asym, or Parted)
  // =========================================================================
  if (isPonytail || isTwinTails) {
    // Sleek pulled-back scalp strands converging neatly toward rear ponytail or dual side-rear twin-tail ties
    const wrapCount = Math.round(44 * density);
    for (let i = 0; i < wrapCount; i++) {
      const frac = i / (wrapCount - 1);
      const thetaStart = (frac - 0.5) * Math.PI * 1.92;
      const side = thetaStart < 0 ? -1 : 1;
      const targetTheta = isPonytail ? (side < 0 ? -Math.PI : Math.PI) : side * 1.98;
      const thetaEnd = THREE.MathUtils.lerp(thetaStart, targetTheta, 0.72);
      const phiStart = 0.03 + (i % 3) * 0.08;
      const phiEnd = isPonytail ? 1.56 + (i % 2) * 0.22 : 1.72 + (i % 2) * 0.22;

      const pts = buildSculptedLock({
        thetaStart,
        thetaEnd,
        phiStart,
        phiEnd,
        baseLift: 0.014 + (i % 2) * 0.004,
        midArch: 0.011 + config.crownVolume * 0.006,
        tipFlickOut: 0.003,
        napeHug: 1.18,
      });
      specs.push({
        pointsLocal: pts,
        width: 0.062 * wMul,
        thickness: 0.012,
        stiffnessBias: 0.94,
        layerAO: 0.86 + (i % 2) * 0.06,
        layerOrder: 2,
        scalpRoot: 0.78,
      });
    }
  } else if (isSweptBack) {
    // Clive / Vergil windswept brushed-back forehead & crown waves flowing seamlessly over the occiput
    const waveCount = Math.round(52 * density * (isCrop ? 1.25 : 1));
    for (let i = 0; i < waveCount; i++) {
      const u = i / (waveCount - 1);
      const side = u < 0.5 ? -1 : 1;
      const distCenter = Math.abs(u - 0.5) * 2.0; // 0 = center forehead, 1 = temples
      const subRow = i % 2;
      const thetaStart = (u - 0.5) * Math.PI * 1.18 + (rand() - 0.5) * 0.04;
      // Sweep decisively backward toward the occiput!
      const thetaEnd = side * THREE.MathUtils.lerp(Math.PI * 0.76, Math.PI * 0.98, 1 - distCenter * 0.45) * (isCrop ? 0.65 : 1); // crop: 35% less side-to-rear wrap
      const phiStart = 0.04 + subRow * 0.1 + distCenter * 0.14;
      const phiEnd = Math.min(3.05, backLenPhi - 0.18 + (i % 3) * 0.14);

      const pts = buildSculptedLock({
        thetaStart,
        thetaEnd,
        phiStart,
        phiEnd,
        baseLift: isCrop ? Math.min(0.008, crownLift + 0.004 + subRow * 0.005) : crownLift + 0.004 + subRow * 0.005,
        midArch: isCrop ? 0.008 : 0.02 + config.crownVolume * 0.012,
        tipFlickOut: isCrop ? 0.008 : 0.018 + shag * 0.026,
        sCurveAmp: side * 0.045 * (isCrop ? 0.65 : 1),
        napeHug: 1.06,
      });
      specs.push({
        pointsLocal: pts,
        width: (0.054 + rand() * 0.008) * wMul * (isCrop ? 1.15 : 1),
        thickness: 0.013,
        stiffnessBias: 0.88,
        layerAO: 0.88 + subRow * 0.08,
        layerOrder: 3,
        scalpRoot: 0.84,
      });
    }
  } else {
    // Parted Left & Right Crown Canopy Cascades (adapts cleanly for Asym, Dolphin, Half-Up, Emo-Sweep, Shag, Wolf, Bob, Long)
    const leftCount = Math.round(30 * density);
    const rightCount = Math.round(30 * density);
    const effPartX = isEmoSweep ? (partX >= 0 ? Math.max(0.068, partX) : Math.min(-0.068, partX)) : partX;

    for (const side of [-1, 1] as const) {
      const count = side === -1 ? leftCount : rightCount;
      for (let i = 0; i < count; i++) {
        const u = count > 1 ? i / (count - 1) : 0.5;
        const subRow = i % 2;
        const zPart = THREE.MathUtils.lerp(0.142, -0.17, u);
        const partRoot = new THREE.Vector3(
          effPartX + side * (0.004 + subRow * 0.005),
          0.744 - Math.pow(u - 0.22, 2) * 0.1,
          zPart
        );

        const thetaStart = side * THREE.MathUtils.lerp(0.14, Math.PI * 0.72, u);
        const sweepOffset = side * THREE.MathUtils.lerp(0.24, 0.28, u) + (rand() - 0.5) * 0.06;
        const thetaEnd = THREE.MathUtils.clamp(thetaStart + sweepOffset, -Math.PI * 0.98, Math.PI * 0.98);
        const isRear = u > 0.52;

        // Valkyrie Asym (Lightning): viewer's left (-1) cascades long past shoulder, viewer's right (+1) is cropped & feathered!
        let sideTargetPhi = sideLenPhi;
        if (isAsym) {
          sideTargetPhi = side === -1 ? Math.max(3.12, sideLenPhi + 0.45) : Math.min(2.12, sideLenPhi * 0.82);
        }

        const basePhiEnd = isRear
          ? THREE.MathUtils.lerp(sideTargetPhi, backLenPhi, (u - 0.52) / 0.48)
          : THREE.MathUtils.lerp(bangsLenPhi + 0.08, sideTargetPhi, u / 0.52);

        const isFrontCrown = u < 0.28;
        const pts = buildSculptedLock({
          thetaStart,
          thetaEnd,
          phiStart: 0.06 + u * 0.06,
          phiEnd: Math.min(3.52, basePhiEnd + (i % 3) * 0.1 * shag + rand() * 0.08),
          baseLift: (isFrontCrown ? crownLift * 0.82 : crownLift) + subRow * 0.004,
          midArch: (isFrontCrown ? 0.014 : 0.018) + config.crownVolume * 0.01,
          tipFlickOut: isBob ? -0.008 : isAsym && side === 1 ? 0.036 : 0.01 + shag * 0.028 + subRow * 0.006,
          sCurveAmp: side * (0.032 + shag * 0.032),
          napeHug: isRear ? napeHugBase : 0.86,
          rootOverride: partRoot,
        });

        specs.push({
          pointsLocal: pts,
          width: (isFrontCrown ? 0.048 : 0.054 + rand() * 0.008) * wMul,
          thickness: isFrontCrown ? 0.012 : 0.014,
          stiffnessBias: isLong ? 0.62 : 0.86,
          layerAO: 0.88 + subRow * 0.07,
          layerOrder: subRow === 1 ? 3 : 2,
          scalpRoot: 0.78,
        });
      }
    }
  }

  // =========================================================================
  // ZONE C: Sculpted Forehead Bangs & Temple Seal (Archetype-Specialized)
  // =========================================================================
  const bangCount = Math.round((isSweptBack ? 34 : isEmoSweep ? 42 : 36) * density);
  const sweepDir = isEmoSweep
    ? config.bangsSweep >= 0
      ? Math.max(0.75, config.bangsSweep)
      : Math.min(-0.75, config.bangsSweep)
    : isAsym
      ? -0.68
      : config.bangsSweep;

  for (let i = 0; i < bangCount; i++) {
    const frac = bangCount > 1 ? i / (bangCount - 1) : 0.5;
    const isOuter = i % 2 === 1;
    const isRightOfPart = (frac - 0.5) * 0.34 > partX;

    const rootX = THREE.MathUtils.lerp(-0.15, 0.15, frac) + partX * 0.22;
    const rootY = 0.734 - Math.abs(frac - 0.5) * 0.044;
    const rootZ = 0.144 + (1 - Math.abs(frac - 0.5) * 1.3) * 0.022;
    const root = new THREE.Vector3(THREE.MathUtils.clamp(rootX, -0.17, 0.17), rootY, rootZ);

    const thetaStart = THREE.MathUtils.lerp(-0.88, 0.88, frac);
    let thetaEnd: number;
    let phiEnd: number;

    if (isSweptBack) {
      // Clive / Vergil sleek swept-back hairline: full widow's-peak coverage parting smoothly toward temples without bare forehead triangles
      const sideSign = frac < 0.5 ? -1 : 1;
      const distMid = Math.abs(frac - 0.5) * 2.0;
      const centerVeil = distMid < 0.28;
      thetaEnd = centerVeil
        ? thetaStart + sideSign * (0.14 + distMid * 0.42)
        : thetaStart + sideSign * (0.34 + distMid * 0.42);
      phiEnd = centerVeil
        ? 1.72 + (i % 3) * 0.08
        : 1.82 + distMid * 0.48 + (i % 3) * 0.08;
      if (isCrop) phiEnd = Math.min(phiEnd, 1.18 + config.bangsLength * 0.2 + (i % 3) * 0.04); // and fall nearly straight (no centre V) cropped hairline: bangs stop at the forehead edge
      if (isCrop) thetaEnd = thetaStart + sideSign * (0.22 + distMid * 0.18); // brushed up-and-back, lateral sweep
    } else if (isEmoSweep) {
      // Dramatic Riku / Dante long angled fringe sweeping across one eye
      const longSideFactor = sweepDir < 0 ? frac : 1 - frac;
      thetaEnd = thetaStart + sweepDir * (0.48 + longSideFactor * 0.28);
      phiEnd = Math.min(2.58, bangsLenPhi + longSideFactor * 0.48 + (i % 3) * 0.07);
    } else if (isHalfUp || isDolphin) {
      // Aerith / Tifa delicate center-parted curtain tendrils framing brow & cheeks
      const sideSign = frac < 0.5 ? -1 : 1;
      const distMid = Math.abs(frac - 0.5) * 2.0;
      thetaEnd = thetaStart + sideSign * (0.28 + (1 - distMid) * 0.16);
      phiEnd = Math.min(2.74, bangsLenPhi + distMid * 0.65 + (i % 2) * 0.1);
    } else {
      const curtainBias = isRightOfPart ? 0.22 : -0.22;
      const sweepTerm = sweepDir * 0.36 + curtainBias * (1.0 - Math.min(1.0, Math.abs(sweepDir) * 1.15));
      thetaEnd = thetaStart + sweepTerm + (rand() - 0.5) * 0.05;
      const edgeDrop = Math.abs(frac - 0.5) * 0.42;
      const stagger = (i % 3) * (0.06 + shag * 0.07);
      phiEnd = Math.min(2.42, bangsLenPhi + edgeDrop + stagger - (isOuter ? 0.06 : 0));
    }

    const pts = buildSculptedLock({
      thetaStart,
      thetaEnd,
      phiStart: 0.14 + (isOuter ? 0.03 : 0),
      phiEnd,
      baseLift: crownLift * 0.85 + (isOuter ? 0.005 : 0.001),
      midArch: 0.014 + config.crownVolume * 0.008,
      tipFlickOut: isOuter ? 0.004 + shag * 0.008 : -0.006,
      sCurveAmp: (thetaEnd - thetaStart) * 0.16,
      napeHug: 0.58,
      rootOverride: root,
    });

    specs.push({
      pointsLocal: pts,
      width: (isOuter ? 0.044 : 0.05) * wMul + rand() * 0.005,
      thickness: isOuter ? 0.012 : 0.013,
      stiffnessBias: 0.84,
      layerAO: isOuter ? 0.94 : 0.88,
      layerOrder: isOuter ? 3 : 2,
      customOutDir: new THREE.Vector3(Math.sin(thetaEnd) * 0.35, 0.2, 0.94),
      scalpRoot: 0.88,
    });
  }

  // =========================================================================
  // ZONE D: Shingled Side Tiers & Face-Framing Sideburns
  // =========================================================================
  const sideTiers = isPulledScalp ? 3 : Math.max(3, Math.round(3.5 * density));
  for (const side of [-1, 1] as const) {
    // Valkyrie Asym: left side (-1) flows long over the shoulder, right side (+1) stays cropped & edgy!
    const effSideLenPhi = isAsym
      ? side === -1
        ? Math.max(3.16, sideLenPhi + 0.5)
        : Math.min(2.04, sideLenPhi * 0.78)
      : isPulledScalp
        ? 2.28
        : sideLenPhi;

    for (let tier = 0; tier < sideTiers; tier++) {
      const locksInTier = Math.max(5, Math.round((isPulledScalp ? 7 : 6) * density));
      for (let j = 0; j < locksInTier; j++) {
        const v = locksInTier > 1 ? j / (locksInTier - 1) : 0.5;
        const thetaStart = side * THREE.MathUtils.lerp(0.84, 2.04, v) + (rand() - 0.5) * 0.05;
        const backSweep = side * (0.18 + (1 - v) * 0.18) * (isBob ? 0.45 : isPulledScalp ? 1.45 : 1.0);
        const thetaEnd = thetaStart + backSweep;
        const phiStart = 0.3 + tier * 0.26 + (rand() - 0.5) * 0.03;
        const phiEnd = isPulledScalp
          ? THREE.MathUtils.lerp(2.14, 2.34, v) + tier * 0.04
          : Math.min(
              Math.max(effSideLenPhi, backLenPhi * 0.88),
              phiStart + 0.9 + tier * 0.14 + Math.max(0, effSideLenPhi - 2.05) * 0.65 + rand() * 0.08
            );
        const isOuterTier = tier >= Math.floor(sideTiers / 2);

        const pts = buildSculptedLock({
          thetaStart,
          thetaEnd,
          phiStart,
          phiEnd,
          baseLift: isPulledScalp ? 0.011 + tier * 0.003 : 0.014 + tier * 0.004,
          midArch: isPulledScalp ? 0.009 : 0.013 + config.crownVolume * 0.005,
          tipFlickOut: isBob || isPulledScalp ? -0.006 : isOuterTier ? 0.014 + shag * 0.03 : 0.008,
          sCurveAmp: side * (isPulledScalp ? 0.014 : 0.028 + shag * 0.026),
          napeHug: isPulledScalp ? 1.24 : v > 0.5 ? napeHugBase : 0.86,
        });

        specs.push({
          pointsLocal: pts,
          width: (isPulledScalp ? 0.062 : 0.05 + rand() * 0.008) * wMul,
          thickness: 0.013,
          stiffnessBias: isPulledScalp ? 0.9 : isLong ? 0.64 : 0.84,
          layerAO: 0.84 + (tier % 2) * 0.07,
          layerOrder: isOuterTier ? 2 : 1,
          scalpRoot: tier === 0 ? 0.48 : 0.18,
        });
      }
    }

    // Cheek-Framing Tendrils / Sideburns (extra lush on Valkyrie Left, Aerith Half-Up & Tifa Dolphin!)
    const tendrilCount = isAsym && side === -1 ? 7 : isHalfUp || isDolphin ? 5 : 4;
    for (let k = 0; k < tendrilCount; k++) {
      const frac = tendrilCount > 1 ? k / (tendrilCount - 1) : 0;
      const thetaS = side * (0.84 + frac * 0.32);
      const thetaE = side * (isAsym && side === -1 ? 0.56 + frac * 0.24 : 0.66 + frac * 0.28);
      const phiS = 0.56 + frac * 0.18;
      const phiE = Math.min(
        3.35,
        isAsym && side === -1
          ? 3.24 - frac * 0.28
          : isHalfUp
            ? 2.86 - frac * 0.22
            : isPonytail || isTwinTails
              ? 2.48 - frac * 0.25
              : effSideLenPhi + (k < 2 ? 0.1 : -0.12)
      );

      const pts = buildSculptedLock({
        thetaStart: thetaS,
        thetaEnd: thetaE,
        phiStart: phiS,
        phiEnd: phiE,
        baseLift: 0.014 + (k % 2) * 0.004,
        midArch: 0.014,
        tipFlickOut: isBob ? -0.014 : k < 2 ? -0.006 : 0.01 * shag,
        tipDropExtra: isAsym && side === -1 ? 0.045 : config.sideLength > 0.65 ? 0.018 : 0.006,
        sCurveAmp: side * (isHalfUp ? -0.065 : -0.032),
        napeHug: 0.78,
      });

      specs.push({
        pointsLocal: pts,
        width: (0.044 - frac * 0.008) * wMul,
        thickness: 0.012,
        stiffnessBias: isAsym && side === -1 ? 0.58 : 0.74,
        layerAO: 0.92 + (k % 2) * 0.04,
        layerOrder: 2,
        scalpRoot: 0.24,
      });
    }
  }

  // =========================================================================
  // ZONE E: Multi-Tier Back Canopy + Special Archetype Sculptures
  // =========================================================================
  if (isPonytail) {
    // Sleek overlapping rear cap & tucked nape smoothly gathering into the ponytail tie
    for (let tier = 0; tier < 2; tier++) {
      for (let i = 0; i < 18; i++) {
        const u = i / 17;
        const theta = Math.PI * 0.58 + u * Math.PI * 0.84;
        const pts = buildSculptedLock({
          thetaStart: theta,
          thetaEnd: Math.PI + (theta - Math.PI) * 0.38,
          phiStart: tier === 0 ? 0.68 : 1.16,
          phiEnd: tier === 0 ? 2.18 : 2.38,
          baseLift: 0.012 + tier * 0.002,
          midArch: 0.009,
          tipFlickOut: -0.004,
          napeHug: 1.28,
        });
        specs.push({
          pointsLocal: pts,
          width: 0.062 * wMul,
          thickness: 0.012,
          stiffnessBias: 0.92,
          layerAO: 0.84 + tier * 0.04,
          layerOrder: 1,
          scalpRoot: tier === 0 ? 0.42 : 0.2,
        });
      }
    }

    // Dynamic High Battle Ponytail Fountain (28 lush flowing strands rooted on upper-rear skull!)
    const tailCount = Math.round(28 * density);
    const tailLen = THREE.MathUtils.lerp(0.46, 0.74, config.backLength);
    for (let i = 0; i < tailCount; i++) {
      const u = i / (tailCount - 1);
      const ring = i % 3;
      const angle = (u - 0.5) * Math.PI * 1.1;
      const anchorTheta = Math.PI + Math.sin(angle) * 0.14;
      const anchorPhi = 0.84 + ring * 0.07;
      const spreadX = Math.sin(angle) * (0.08 + ring * 0.032);
      const spreadZ = -Math.cos(angle) * (0.045 + ring * 0.02);

      const pts = buildGatheredTailLock({
        anchorTheta,
        anchorPhi,
        outDir: new THREE.Vector3(Math.sin(angle) * 0.28, 0.12 - ring * 0.08, -0.96),
        spreadX,
        spreadZ,
        length: tailLen + (ring === 0 ? 0.06 : -ring * 0.04) + rand() * 0.04,
        archOut: 0.13 + ring * 0.025,
        waveAmp: 0.02 + shag * 0.025,
        wavePhase: i * 0.7,
      });

      specs.push({
        pointsLocal: pts,
        width: (0.052 + (ring === 0 ? 0.008 : 0)) * wMul,
        thickness: 0.013,
        stiffnessBias: 0.48,
        layerAO: 0.88 + ring * 0.04,
        layerOrder: ring === 2 ? 3 : 2,
        scalpRoot: 0.35,
      });
    }
  } else if (isTwinTails) {
    // Sleek overlapping rear cap & tucked nape parting cleanly toward both twin-tail ties
    for (let tier = 0; tier < 2; tier++) {
      for (let i = 0; i < 18; i++) {
        const u = i / 17;
        const theta = Math.PI * 0.58 + u * Math.PI * 0.84;
        const pts = buildSculptedLock({
          thetaStart: theta,
          thetaEnd: theta,
          phiStart: tier === 0 ? 0.64 : 1.12,
          phiEnd: tier === 0 ? 2.16 : 2.38,
          baseLift: 0.012 + tier * 0.002,
          midArch: 0.009,
          tipFlickOut: -0.004,
          napeHug: 1.28,
        });
        specs.push({
          pointsLocal: pts,
          width: 0.062 * wMul,
          thickness: 0.012,
          stiffnessBias: 0.92,
          layerAO: 0.84 + tier * 0.04,
          layerOrder: 1,
          scalpRoot: tier === 0 ? 0.42 : 0.2,
        });
      }
    }

    // Dual Sculpted Twin-Tail Cascades (16 lush flowing ribbons per side = 32 strands!)
    const perSide = Math.round(16 * density);
    const tailLen = THREE.MathUtils.lerp(0.44, 0.72, config.backLength);
    for (const side of [-1, 1] as const) {
      for (let i = 0; i < perSide; i++) {
        const u = i / (perSide - 1);
        const ring = i % 3;
        const spreadAngle = (u - 0.5) * Math.PI * 0.9;
        // tie roots +30 mm up and ~25 mm further back, root bundle radius -30% (Neon round 4)
        const anchorTheta = side * (2.08 + (u - 0.5) * 0.16 * 0.7);
        const anchorPhi = 0.71 + ring * 0.07 * 0.7;

        const pts = buildGatheredTailLock({
          anchorTheta,
          anchorPhi,
          outDir: new THREE.Vector3(side * 0.84, 0.08 - ring * 0.06, -0.52),
          spreadX: (side * (0.07 + ring * 0.025) + Math.sin(spreadAngle) * 0.04) * 0.7,
          spreadZ: -0.05 + Math.cos(spreadAngle) * 0.045,
          length: tailLen + (ring === 0 ? 0.05 : -ring * 0.03) + rand() * 0.04,
          archOut: 0.11 + ring * 0.02,
          waveAmp: 0.022 + shag * 0.024,
          wavePhase: i * 0.8 + (side > 0 ? 1.5 : 0),
        });

        specs.push({
          pointsLocal: pts,
          width: (0.048 + (ring === 0 ? 0.006 : 0)) * wMul,
          thickness: 0.013,
          stiffnessBias: 0.5,
          layerAO: 0.88 + ring * 0.04,
          layerOrder: ring === 2 ? 3 : 2,
          scalpRoot: 0.32,
        });
      }
    }
  } else {
    // Full Multi-Tier Shingled Back Cascade (adapts for Dolphin-Tail, Half-Up, Wolf-Cut, Bob, Swept-Back, Asym, Long, Shag)
    const backTiers = 4;
    const backPerTier = Math.round(16 * density);
    for (let tier = 0; tier < backTiers; tier++) {
      for (let i = 0; i < backPerTier; i++) {
        const u = backPerTier > 1 ? i / (backPerTier - 1) : 0.5;
        const thetaStart = Math.PI * 0.56 + u * Math.PI * 0.88 + (rand() - 0.5) * 0.05;
        const sideSign = u < 0.5 ? 1 : -1;
        const crossBias = tier % 2 === 0 ? 1 : -0.55;

        // Tifa Dolphin-Tail: lower back locks converge slightly then split outward into twin curved dolphin-tail wings!
        let thetaEnd = thetaStart + sideSign * crossBias * (0.14 + shag * 0.12);
        let tipFlick = isBob ? -0.01 : tier >= 2 ? 0.014 + shag * 0.03 : 0.008;
        let centerV =
          arch === 'wolf-cut'
            ? Math.exp(-Math.pow((u - 0.5) * 2.0, 2)) * 0.32
            : isBob
              ? -0.06
              : Math.exp(-Math.pow((u - 0.5) * 2.2, 2)) * 0.14;

        if (isDolphin) {
          const distFromCenter = Math.abs(u - 0.5);
          // Dolphin-tail twin-wing flare near tips + central V-notch
          thetaEnd = thetaStart - sideSign * (0.08 + (0.5 - distFromCenter) * 0.16);
          centerV = -0.08 + Math.sin(distFromCenter * Math.PI * 2.0) * 0.24;
          tipFlick = tier >= 2 ? 0.036 + distFromCenter * 0.025 : 0.012;
        } else if (isAsym) {
          // Asymmetric back bias: longer on left (u > 0.5 is viewer's left rear when theta > PI)
          centerV = (u - 0.5) * 0.42;
        }

        const phiStart = 0.24 + tier * 0.36 + (i % 3) * 0.06;
        const phiEnd = Math.min(
          3.56,
          Math.max(phiStart + 0.92, backLenPhi - (3 - tier) * 0.14 + centerV + rand() * 0.09)
        );

        const pts = buildSculptedLock({
          thetaStart,
          thetaEnd,
          phiStart,
          phiEnd,
          baseLift: 0.013 + tier * 0.005,
          midArch: 0.014 + config.crownVolume * 0.007,
          tipFlickOut: tipFlick,
          sCurveAmp: sideSign * (isDolphin ? -0.048 : 0.024 + shag * 0.022),
          napeHug: tier < 2 ? napeHugBase + 0.08 : napeHugBase,
        });

        specs.push({
          pointsLocal: pts,
          width: (0.058 + rand() * 0.008) * wMul,
          thickness: 0.014,
          stiffnessBias: isLong ? 0.52 : 0.78,
          layerAO: 0.84 + tier * 0.04,
          layerOrder: tier < 2 ? 1 : 2,
          scalpRoot: tier === 0 ? 0.42 : 0.14,
        });
      }
    }

    // Half-Up Cascade (Aerith / Jill): add signature gathered upper-rear half-up waterfall plume!
    if (isHalfUp) {
      const halfTailCount = Math.round(18 * density);
      const halfLen = THREE.MathUtils.lerp(0.42, 0.66, config.backLength);
      for (let i = 0; i < halfTailCount; i++) {
        const u = i / (halfTailCount - 1);
        const angle = (u - 0.5) * Math.PI * 0.85;
        const pts = buildGatheredTailLock({
          anchorTheta: Math.PI + Math.sin(angle) * 0.16,
          anchorPhi: 0.96 + (i % 2) * 0.06,
          outDir: new THREE.Vector3(Math.sin(angle) * 0.22, 0.04, -0.98),
          spreadX: Math.sin(angle) * 0.11,
          spreadZ: -Math.cos(angle) * 0.05,
          length: halfLen + (i % 3) * 0.03,
          archOut: 0.1,
          waveAmp: 0.024,
          wavePhase: i * 0.65,
        });
        specs.push({
          pointsLocal: pts,
          width: 0.052 * wMul,
          thickness: 0.013,
          stiffnessBias: 0.54,
          layerAO: 0.92 + (i % 2) * 0.05,
          layerOrder: 3,
          scalpRoot: 0.34,
        });
      }
    }
  }

  // =========================================================================
  // ZONE F: Sculpted Hero Spikes / Crown Crest (Driven by spikeLift)
  // =========================================================================
  if (spikeAmt > 0.12 && !isPulledScalp) {
    const spikeCount = Math.round((10 + spikeAmt * 30) * density);
    for (let i = 0; i < spikeCount; i++) {
      const ring = i % 3;
      const u = Math.floor(i / 3) / Math.max(1, Math.ceil(spikeCount / 3));
      const theta = isAsym
        ? THREE.MathUtils.lerp(0.15, Math.PI * 1.15, u) + ring * 0.18
        : u * Math.PI * 2 + ring * 0.34 + (rand() - 0.5) * 0.08;
      const phi = 0.24 + ring * 0.24 + (i % 2) * 0.04;
      const root = skullSurfacePoint(theta, phi, 0.012);
      const radial = root.clone().sub(HEAD_CENTER_LOCAL).normalize();

      const horizX = Math.sin(theta);
      const horizZ = Math.cos(theta);
      const fanOut = new THREE.Vector3(
        horizX * (0.78 + ring * 0.14) + config.bangsSweep * 0.22,
        0.38 + spikeAmt * 0.34 - ring * 0.22,
        horizZ * 0.72 - (horizZ < -0.1 || isSweptBack ? 0.22 : 0)
      ).normalize();

      const dir = radial.clone().multiplyScalar(0.32).addScaledVector(fanOut, 0.68).normalize();
      if (dir.dot(radial) < 0.55) {
        dir.addScaledVector(radial, 0.55 - dir.dot(radial)).normalize();
      }

      const len = 0.11 + spikeAmt * 0.095 + (i % 3 === 0 ? 0.024 : 0) + rand() * 0.014;
      const pts: THREE.Vector3[] = [];
      for (let k = 0; k < NODES_PER_LOCK; k++) {
        const t = k / (NODES_PER_LOCK - 1);
        const arc = Math.sin(t * Math.PI) * 0.014;
        const sweep = t * t * 0.022;
        pts.push(
          root
            .clone()
            .addScaledVector(dir, t * len)
            .add(new THREE.Vector3(horizX * sweep, arc - t * t * (0.022 - spikeAmt * 0.012), horizZ * sweep * 0.5))
        );
      }

      const bladeN = new THREE.Vector3(horizX * 0.5, 0.45, horizZ >= 0 ? 0.74 : -0.74).normalize();
      specs.push({
        pointsLocal: pts,
        width: (0.046 + (i % 3 === 0 ? 0.01 : 0)) * wMul,
        thickness: 0.011,
        stiffnessBias: isSpiky ? 0.96 : 0.9,
        layerAO: 0.92,
        layerOrder: 4,
        customOutDir: bladeN,
        lockNormal: true,
        scalpRoot: 0.36,
      });
    }
  }

  // =========================================================================
  // ZONE G: Tasteful Canopy-Tucked Silhouette Flyaways (Driven by layerShag)
  // =========================================================================
  if (shag > 0.22 && !isPulledScalp) {
    const wispCount = Math.round(14 * shag * density);
    for (let i = 0; i < wispCount; i++) {
      const u = wispCount > 1 ? i / (wispCount - 1) : 0.5;
      const theta = Math.PI * 0.34 + u * Math.PI * 1.32 + (rand() - 0.5) * 0.1;
      const rawPhiStart = 0.34 + (i % 4) * 0.28;
      // Keep lower wisps strictly behind the temple hairline so none root on bare temple/cheek skin
      const phiStart = Math.cos(theta) > 0.32 ? Math.min(rawPhiStart, 0.88) : rawPhiStart;
      const phiEnd = Math.min(backLenPhi, phiStart + 0.72 + rand() * 0.26);

      const pts = buildSculptedLock({
        thetaStart: theta,
        thetaEnd: theta + (rand() - 0.5) * 0.2,
        phiStart,
        phiEnd,
        baseLift: 0.018 + (i % 2) * 0.004,
        midArch: 0.015,
        tipFlickOut: 0.036 + shag * 0.024 + rand() * 0.014,
        sCurveAmp: (i % 2 === 0 ? 1 : -1) * 0.036,
        napeHug: 0.9,
      });

      specs.push({
        pointsLocal: pts,
        width: (0.025 + rand() * 0.007) * wMul,
        thickness: 0.009,
        stiffnessBias: 0.74,
        layerAO: 0.96,
        layerOrder: 4,
        scalpRoot: 0.18,
      });
    }
  }

  return specs;
}

const ARCHETYPE_PRESET_MAP: Record<HairBuilderArchetype, HairPresetId> = {
  'spiky-hero': 'soldier',
  'long-cascade': 'silver',
  'wolf-cut': 'noctis',
  'sleek-bob': 'crimson',
  'layered-shag': 'leon',
  'asym-cascade': 'crimson',
  'dolphin-long': 'silver',
  'ribbon-halfup': 'silver',
  'swept-back': 'noctis',
  'high-ponytail': 'silver',
  'twin-tails': 'crimson',
  'emo-sweep': 'leon',
  'short-crop': 'silver',
  'undercut-fade': 'silver',
  'braided-crown': 'crimson',
  'samurai-topknot': 'noctis',
};

function buildArchetypeParameters(archetype: HairBuilderArchetype, rand: () => number, seed: number): HairBuilderConfig {
  const rRange = (lo: number, hi: number) => Number((lo + rand() * (hi - lo)).toFixed(2));

  switch (archetype) {
    case 'spiky-hero':
      return {
        archetype,
        partPosition: rRange(-0.45, 0.45),
        bangsLength: rRange(0.48, 0.74),
        bangsSweep: rRange(-0.65, 0.65),
        crownVolume: rRange(0.55, 0.88),
        spikeLift: rRange(0.58, 0.96),
        sideLength: rRange(0.44, 0.72),
        backLength: rRange(0.46, 0.74),
        layerShag: rRange(0.62, 0.92),
        density: rRange(0.95, 1.12),
        strandWidth: rRange(0.92, 1.08),
        seed,
      };
    case 'long-cascade':
      return {
        archetype,
        partPosition: rand() < 0.45 ? 0 : rRange(-0.55, 0.55),
        bangsLength: rRange(0.62, 0.9),
        bangsSweep: rRange(-0.4, 0.4),
        crownVolume: rRange(0.32, 0.62),
        spikeLift: rRange(0.0, 0.14),
        sideLength: rRange(0.76, 0.98),
        backLength: rRange(0.82, 1.0),
        layerShag: rRange(0.24, 0.54),
        density: rRange(0.96, 1.12),
        strandWidth: rRange(0.96, 1.12),
        seed,
      };
    case 'wolf-cut':
      return {
        archetype,
        partPosition: rRange(-0.58, 0.58),
        bangsLength: rRange(0.52, 0.78),
        bangsSweep: rRange(-0.6, 0.6),
        crownVolume: rRange(0.42, 0.72),
        spikeLift: rRange(0.14, 0.42),
        sideLength: rRange(0.56, 0.8),
        backLength: rRange(0.74, 0.95),
        layerShag: rRange(0.65, 0.95),
        density: rRange(0.95, 1.12),
        strandWidth: rRange(0.9, 1.06),
        seed,
      };
    case 'sleek-bob':
      return {
        archetype,
        partPosition: rRange(-0.65, 0.65),
        bangsLength: rRange(0.46, 0.76),
        bangsSweep: rRange(-0.7, 0.7),
        crownVolume: rRange(0.22, 0.48),
        spikeLift: rRange(0.0, 0.1),
        sideLength: rRange(0.42, 0.66),
        backLength: rRange(0.36, 0.56),
        layerShag: rRange(0.08, 0.3),
        density: rRange(0.98, 1.14),
        strandWidth: rRange(0.96, 1.14),
        seed,
      };
    case 'asym-cascade':
      return {
        archetype,
        partPosition: rRange(0.32, 0.72),
        bangsLength: rRange(0.58, 0.82),
        bangsSweep: rRange(-0.85, -0.45),
        crownVolume: rRange(0.44, 0.74),
        spikeLift: rRange(0.18, 0.48),
        sideLength: rRange(0.66, 0.92),
        backLength: rRange(0.46, 0.74),
        layerShag: rRange(0.54, 0.84),
        density: rRange(0.96, 1.12),
        strandWidth: rRange(0.92, 1.08),
        seed,
      };
    case 'dolphin-long':
      return {
        archetype,
        partPosition: rRange(-0.18, 0.18),
        bangsLength: rRange(0.52, 0.78),
        bangsSweep: rRange(-0.25, 0.25),
        crownVolume: rRange(0.3, 0.54),
        spikeLift: rRange(0.0, 0.08),
        sideLength: rRange(0.68, 0.9),
        backLength: rRange(0.84, 1.0),
        layerShag: rRange(0.26, 0.52),
        density: rRange(0.98, 1.14),
        strandWidth: rRange(0.96, 1.12),
        seed,
      };
    case 'ribbon-halfup':
      return {
        archetype,
        partPosition: rRange(-0.2, 0.2),
        bangsLength: rRange(0.56, 0.8),
        bangsSweep: rRange(-0.35, 0.35),
        crownVolume: rRange(0.44, 0.74),
        spikeLift: rRange(0.0, 0.1),
        sideLength: rRange(0.62, 0.86),
        backLength: rRange(0.76, 0.98),
        layerShag: rRange(0.32, 0.62),
        density: rRange(0.96, 1.12),
        strandWidth: rRange(0.94, 1.1),
        seed,
      };
    case 'samurai-topknot':
      return {
        archetype,
        partPosition: rRange(-0.1, 0.1),
        bangsLength: rRange(0.05, 0.25),
        bangsSweep: rRange(-0.15, 0.15),
        crownVolume: rRange(0.25, 0.5),
        spikeLift: rRange(0.05, 0.25),
        sideLength: rRange(0.15, 0.35),
        backLength: rRange(0.25, 0.55),
        layerShag: rRange(0.15, 0.4),
        density: rRange(1.0, 1.16),
        strandWidth: rRange(0.9, 1.08),
        seed,
      };
    case 'braided-crown':
      return {
        archetype,
        partPosition: rRange(-0.12, 0.12),
        bangsLength: rRange(0.28, 0.52),
        bangsSweep: rRange(-0.25, 0.25),
        crownVolume: rRange(0.35, 0.6),
        spikeLift: rRange(0, 0.06),
        sideLength: rRange(0.45, 0.7),
        backLength: rRange(0.62, 0.88),
        layerShag: rRange(0.18, 0.42),
        density: rRange(1.0, 1.16),
        strandWidth: rRange(0.92, 1.1),
        seed,
      };
    case 'undercut-fade':
      return {
        archetype,
        partPosition: rRange(-0.65, -0.25),
        bangsLength: rRange(0.28, 0.55),
        bangsSweep: rRange(0.45, 0.9),
        crownVolume: rRange(0.48, 0.8),
        spikeLift: rRange(0.12, 0.42),
        sideLength: rRange(0.05, 0.22),
        backLength: rRange(0.08, 0.26),
        layerShag: rRange(0.45, 0.78),
        density: rRange(0.98, 1.16),
        strandWidth: rRange(0.86, 1.04),
        seed,
      };
    case 'short-crop':
      return {
        archetype,
        partPosition: rRange(-0.2, 0.2),
        bangsLength: rRange(0.1, 0.3),
        bangsSweep: rRange(-0.4, 0.1),
        crownVolume: rRange(0.2, 0.4),
        spikeLift: rRange(0, 0.1),
        sideLength: rRange(0.05, 0.25),
        backLength: rRange(0.05, 0.25),
        layerShag: rRange(0.2, 0.45),
        density: rRange(1.0, 1.15),
        strandWidth: rRange(0.8, 1.0),
        seed,
      };
    case 'swept-back':
      return {
        archetype,
        partPosition: rRange(-0.25, 0.25),
        bangsLength: rRange(0.34, 0.58),
        bangsSweep: rRange(-0.3, 0.3),
        crownVolume: rRange(0.54, 0.86),
        spikeLift: rRange(0.16, 0.46),
        sideLength: rRange(0.44, 0.72),
        backLength: rRange(0.54, 0.82),
        layerShag: rRange(0.56, 0.88),
        density: rRange(0.96, 1.12),
        strandWidth: rRange(0.92, 1.08),
        seed,
      };
    case 'high-ponytail':
      return {
        archetype,
        partPosition: rRange(-0.35, 0.35),
        bangsLength: rRange(0.48, 0.74),
        bangsSweep: rRange(-0.55, 0.55),
        crownVolume: rRange(0.24, 0.52),
        spikeLift: 0.0,
        sideLength: rRange(0.42, 0.68),
        backLength: rRange(0.62, 0.98),
        layerShag: rRange(0.22, 0.56),
        density: rRange(0.96, 1.12),
        strandWidth: rRange(0.94, 1.1),
        seed,
      };
    case 'twin-tails':
      return {
        archetype,
        partPosition: rRange(-0.25, 0.25),
        bangsLength: rRange(0.48, 0.74),
        bangsSweep: rRange(-0.4, 0.4),
        crownVolume: rRange(0.24, 0.5),
        spikeLift: 0.0,
        sideLength: rRange(0.44, 0.68),
        backLength: rRange(0.58, 0.96),
        layerShag: rRange(0.2, 0.52),
        density: rRange(0.96, 1.12),
        strandWidth: rRange(0.94, 1.1),
        seed,
      };
    case 'emo-sweep':
      return {
        archetype,
        partPosition: rand() < 0.5 ? rRange(0.55, 0.8) : rRange(-0.8, -0.55),
        bangsLength: rRange(0.66, 0.94),
        bangsSweep: rand() < 0.5 ? rRange(-0.95, -0.68) : rRange(0.68, 0.95),
        crownVolume: rRange(0.36, 0.66),
        spikeLift: rRange(0.08, 0.34),
        sideLength: rRange(0.56, 0.84),
        backLength: rRange(0.52, 0.82),
        layerShag: rRange(0.48, 0.82),
        density: rRange(0.96, 1.12),
        strandWidth: rRange(0.92, 1.08),
        seed,
      };
    default:
      // layered-shag
      return {
        archetype: 'layered-shag',
        partPosition: rRange(-0.6, 0.6),
        bangsLength: rRange(0.54, 0.78),
        bangsSweep: rRange(-0.65, 0.65),
        crownVolume: rRange(0.36, 0.64),
        spikeLift: rRange(0.08, 0.32),
        sideLength: rRange(0.54, 0.78),
        backLength: rRange(0.52, 0.78),
        layerShag: rRange(0.52, 0.84),
        density: rRange(0.95, 1.12),
        strandWidth: rRange(0.9, 1.06),
        seed,
      };
  }
}

export function calibratePhysicsForBuilder(
  builder: HairBuilderConfig,
  windAzimuth = 30
): Omit<PhysicsConfig, 'showColliders' | 'showBones'> {
  const longFactor = (builder.backLength + builder.sideLength) * 0.5;
  const stiffness = Number(
    THREE.MathUtils.clamp(0.76 - longFactor * 0.24 + builder.spikeLift * 0.18, 0.52, 0.86).toFixed(2)
  );
  const gravity = Number(
    THREE.MathUtils.clamp(3.0 + longFactor * 2.4 - builder.spikeLift * 1.1, 2.4, 5.4).toFixed(1)
  );
  const damping = Number(THREE.MathUtils.clamp(0.105 + longFactor * 0.022, 0.1, 0.13).toFixed(3));

  return {
    stiffness,
    gravity,
    windStrength: 0.48,
    windTurbulence: 0.16,
    windAzimuth,
    damping,
    inertia: Number(THREE.MathUtils.clamp(0.76 + longFactor * 0.14, 0.74, 0.9).toFixed(2)),
    bendingSmoothness: Number(THREE.MathUtils.clamp(0.52 + builder.spikeLift * 0.14, 0.5, 0.68).toFixed(2)),
    collisionMarginMm: 9,
    rootAnchor: 0.64,
    solverSubsteps: 1,
    strandWidthScale: 1.0,
    collisionEnabled: true,
  };
}

export function pickRandomPalette(excludeId?: string): CuratedHairPalette {
  const pool = excludeId ? CURATED_HAIR_PALETTES.filter((p) => p.id !== excludeId) : CURATED_HAIR_PALETTES;
  return pool[Math.floor(Math.random() * pool.length)] ?? CURATED_HAIR_PALETTES[0];
}

export function generateRandomShape(
  currentArchetype?: HairBuilderArchetype,
  customSeed?: number
): { name: string; basePreset: HairPresetId; builder: HairBuilderConfig; physics: Omit<PhysicsConfig, 'showColliders' | 'showBones'> } {
  const seed =
    customSeed !== undefined && Number.isFinite(customSeed)
      ? Math.abs(Math.floor(customSeed)) % 999999 || 42
      : Math.floor(Math.random() * 899999) + 100000;
  const rand = seededRand(seed);
  const pool = currentArchetype ? ALL_ARCHETYPES.filter((a) => a !== currentArchetype) : ALL_ARCHETYPES;
  const archetype = pool[Math.floor(rand() * pool.length)] ?? ALL_ARCHETYPES[0];
  const builder = buildArchetypeParameters(archetype, rand, seed);
  const physics = calibratePhysicsForBuilder(builder, Math.round(rand() * 12) * 30);
  const shortId = String(seed % 1000).padStart(3, '0');
  return {
    name: `${ARCHETYPE_LABELS[archetype]} #${shortId}`,
    basePreset: ARCHETYPE_PRESET_MAP[archetype],
    builder,
    physics,
  };
}

/**
 * Generates a cohesive, artistically balanced Final Fantasy / JRPG hairstyle with matched
 * silhouette archetype (12 archetypes), curated character palette (20 palettes), and calibrated physics!
 */
export function generateRandomHairstyle(
  customSeed?: number,
  forcedArchetype?: HairBuilderArchetype
): RandomizedHairstyleResult {
  const seed =
    customSeed !== undefined && Number.isFinite(customSeed)
      ? Math.abs(Math.floor(customSeed)) % 999999 || 42
      : Math.floor(Math.random() * 899999) + 100000;
  const rand = seededRand(seed);

  const archetype = forcedArchetype ?? ALL_ARCHETYPES[Math.floor(rand() * ALL_ARCHETYPES.length)];
  const palette = CURATED_HAIR_PALETTES[Math.floor(rand() * CURATED_HAIR_PALETTES.length)];
  const builder = buildArchetypeParameters(archetype, rand, seed);
  const physics = calibratePhysicsForBuilder(builder, Math.round(rand() * 12) * 30);

  const shortId = String(seed % 1000).padStart(3, '0');
  const name = `${palette.label} ${ARCHETYPE_LABELS[archetype]} #${shortId}`;

  return {
    name,
    paletteLabel: palette.label,
    basePreset: ARCHETYPE_PRESET_MAP[archetype],
    builder,
    shader: { ...palette.shader },
    physics,
  };
}
