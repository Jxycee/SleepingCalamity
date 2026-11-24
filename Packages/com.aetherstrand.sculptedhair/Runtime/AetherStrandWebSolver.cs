using System;

namespace AetherStrand
{
    /// <summary>
    /// Faithful C# port of the AetherStrand WEB hair solver (src/engine/hairPhysics.ts, HairPhysicsSystem.step and helpers).
    ///
    /// Deliberate choices:
    ///  - Runs in WEB space (three.js: right-handed, Y up, +Z front, meters). Unity = web with X negated; the caller mirrors
    ///    at the boundary (AetherStrandWebHair does this). Quaternion mirror: (x,-y,-z,w).
    ///  - State is double precision (JS numbers are float64). Float32Array stores in the web code (entry buffers, node tables,
    ///    neighbour weights, velocity snapshot) are reproduced with explicit (float) rounding so contact branches match.
    ///  - Pure math, no UnityEngine, no allocation per step (all buffers are allocated once in Load).
    ///  - Deterministic: no random numbers.
    /// Authority: none (visual only); each client may run its own copy.
    ///
    /// Known gaps vs the web: live strandWidthScale changes (web re-bakes the inter-lock rest pose when the width moves by
    /// more than 0.03 from the baked value) are not supported; Load expects the rest pose baked at the width passed in cfg.
    /// Telemetry (instability, bounce stats, clearance) is not ported except the flip response that feeds back into physics.
    /// </summary>
    public sealed class AetherStrandWebSolver
    {
        public const int N = 12; // nodes per lock

        [Serializable]
        public class Config
        {
            public double stiffness = 0.65, gravity = 4.2, windStrength = 0.48, windTurbulence = 0.16, windAzimuth = 180;
            public double damping = 0.119, inertia = 0.85, bendingSmoothness = 0.55, collisionMarginMm = 9, rootAnchor = 0.64;
            public int solverSubsteps = 1;
            public double strandWidthScale = 1.0;
            public bool collisionEnabled = true;
        }

        /// <summary>Per-lock input in WEB space, head-pivot-local (headPivot rest = world (0,0.44,0)).</summary>
        public class LockData
        {
            public double[] rest = new double[N * 3]; // restLocal x,y,z per node
            public double[] stiffnessWeight;          // N, null = derive with stiffnessBias 1
            public double baseWidth = 0.03, baseThickness = 0.01, seed = 0.5, layerAO = 0.9, scalpRoot = 0.65;
            public double mass = -1;                  // <=0: derive width*thickness*1
            public int layerOrder = -1;               // <0: derive (base 0, else 2)
            public double restMidR = double.NaN;      // NaN: derive from rest[4]
            public double rootRawY = double.NaN;      // NaN: rest[0].y
            public bool isBaseShell, lockNormal, isFadeCap;
            public double[] rootNormal = { 0, 1, 0 }; // web head-local
        }

        // ---------------- constants (hairPhysics.ts) ----------------
        const int SolverIterations = 4;
        const double HeadCenterY = 0.425, HeadCenterZ = -0.01;
        static readonly double[] LatheY = { 0.0, 0.012, 0.055, 0.135, 0.245, 0.365, 0.48, 0.585, 0.672, 0.725, 0.742 };
        static readonly double[] LatheR = { 0.0, 0.052, 0.122, 0.192, 0.244, 0.272, 0.28, 0.264, 0.208, 0.115, 0.0 };
        const double HeadScaleX = 0.97, HeadScaleZ = 1.09;
        const int HashBuckets = 16384, HashMask = HashBuckets - 1, MaxHashEntries = 12288;
        static readonly int[] HalfDx = { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0 };
        static readonly int[] HalfDy = { 0, -1, -1, -1, 0, 0, 0, 1, 1, 1, 1, 1, 1, 0 };
        static readonly int[] HalfDz = { 0, -1, 0, 1, -1, 0, 1, -1, 0, 1, -1, 0, 1, 1 };
        const int MaxNeighbors = 4;

        // Float32 node tables (web: Float32Array(12))
        static readonly double[] RootSin = new double[N], TipTaper = new double[N], BaseTipTaper = new double[N],
            ThickFactor = new double[N], ThickPow = new double[N], TipFactor = new double[N], TipSq = new double[N],
            TipCube = new double[N], TipPow135 = new double[N], TipPow22 = new double[N], TipFalloff145 = new double[N];

        static AetherStrandWebSolver()
        {
            for (int k = 0; k < N; k++)
            {
                double t = k / 11.0;
                TipFactor[k] = (float)t;
                TipSq[k] = (float)(t * t);
                TipCube[k] = (float)(t * t * t);
                TipPow135[k] = (float)Math.Pow(t, 1.35);
                TipPow22[k] = (float)Math.Pow(t, 2.2);
                TipFalloff145[k] = (float)Math.Pow(1.0 - t, 1.45);
                double rSin = Math.Sin(Math.Min(1.0, t * 4.2) * Math.PI * 0.5);
                RootSin[k] = (float)rSin;
                double tipTaper = t < 0.44 ? 1.0 - t * 0.14 : Math.Pow((1.0 - t) / 0.56, 0.94) * 0.938;
                TipTaper[k] = (float)Math.Max(0.06, tipTaper);
                double baseTipTaper = t < 0.42 ? 1.0 - t * 0.16 : Math.Pow((1.0 - t) / 0.58, 1.12) * 0.933;
                BaseTipTaper[k] = (float)Math.Max(0.04, baseTipTaper);
                double thickPow = Math.Pow(Math.Max(0.04, 1.0 - t), 0.86);
                ThickPow[k] = (float)thickPow;
                double rootThickTuck = 0.2 + 0.8 * rSin;
                ThickFactor[k] = (float)(0.5 * 0.9 * rootThickTuck * thickPow);
            }
        }

        // ---------------- state ----------------
        int L;
        double[] px, py, pz, qx, qy, qz;      // pos, prevPos   [L*N]
        double[] rx, ry, rz;                  // restLocal
        double[] cx, cy, cz;                  // restCurvatureLocal
        double[] segLen, stiffW;
        double[] fpx, fpy, fpz, fvx, fvy, fvz; // framePrevPos / framePrevVel (head-local)
        double[] lockSeed, lockBaseW, lockBaseT, lockMass, lockRestMidR, lockRootRawY, lockScalpRoot, lockLayerAO;
        int[] lockLayer;
        bool[] lockBase, lockNormal;
        double[] rootNX, rootNY, rootNZ;
        double maxBaseW;

        int[] nbCount, nbLock, nbNode;
        float[] nbWeight, velBufX, velBufY, velBufZ;

        // spatial buffers (Float32 where the web uses Float32Array)
        int[] cellMapKey, cellMapId, dirtySlots, cellCx, cellCy, cellCz, cellCount, cellStart, cellWrite, entryCellId;
        int[] hashHead, hashNext, hashLock, hashNode, hashCellX, hashCellY, hashCellZ, sLock, sOrig;
        float[] entryX, entryY, entryZ, entryVelX, entryVelY, entryVelZ, entryRadX, entryRadY, entryRadZ, entryHalfW, entryHalfT,
            entryInvMass, entryRootY, entryRestMidR, entryWeight, dispX, dispY, dispZ, velDX, velDY, velDZ, weightSum, velWeightSum;
        short[] entryLayer;
        float[] sX, sY, sZ, sWeight, sHalfW;
        byte[] lockHit;

        // head state
        double prevHpx, prevHpy, prevHpz, prevHqx, prevHqy, prevHqz, prevHqw;
        bool hasPrevHead;
        double impX, impY, impZ;
        double simTime, lastWidthScale = 1.0;

        public int LockCount => L;
        public int ParticleCount => L * N;
        /// <summary>World-space (web) particle positions, index = lock*12+node.</summary>
        public double[] PosX => px;
        public double[] PosY => py;
        public double[] PosZ => pz;
        public double LastWidthScale => lastWidthScale;
        public bool IsBaseShell(int lockIndex) => lockBase[lockIndex];
        public double[] RestX => rx;
        public double[] RestY => ry;
        public double[] RestZ => rz;

        // ---------------- loading ----------------
        public void Load(LockData[] locks)
        {
            L = locks.Length;
            int P = L * N;
            px = new double[P]; py = new double[P]; pz = new double[P]; qx = new double[P]; qy = new double[P]; qz = new double[P];
            rx = new double[P]; ry = new double[P]; rz = new double[P]; cx = new double[P]; cy = new double[P]; cz = new double[P];
            segLen = new double[P]; stiffW = new double[P];
            fpx = new double[P]; fpy = new double[P]; fpz = new double[P]; fvx = new double[P]; fvy = new double[P]; fvz = new double[P];
            lockSeed = new double[L]; lockBaseW = new double[L]; lockBaseT = new double[L]; lockMass = new double[L];
            lockRestMidR = new double[L]; lockRootRawY = new double[L]; lockScalpRoot = new double[L]; lockLayerAO = new double[L];
            lockLayer = new int[L]; lockBase = new bool[L]; lockNormal = new bool[L];
            rootNX = new double[L]; rootNY = new double[L]; rootNZ = new double[L];
            maxBaseW = 0.052;
            for (int s = 0; s < L; s++)
            {
                LockData d = locks[s];
                int b = s * N;
                for (int k = 0; k < N; k++)
                {
                    rx[b + k] = d.rest[k * 3]; ry[b + k] = d.rest[k * 3 + 1]; rz[b + k] = d.rest[k * 3 + 2];
                    double t = k / (double)(N - 1);
                    stiffW[b + k] = d.stiffnessWeight != null
                        ? d.stiffnessWeight[k]
                        : Math.Min(1.0, Math.Max(0.12, 0.18 + 0.82 * Math.Pow(1.0 - t, 1.35)));
                }
                lockSeed[s] = d.seed; lockBaseW[s] = d.baseWidth; lockBaseT[s] = d.baseThickness;
                lockMass[s] = d.mass > 0 ? d.mass : Math.Max(0.00018, d.baseWidth * d.baseThickness);
                lockLayer[s] = d.layerOrder >= 0 ? d.layerOrder : (d.isBaseShell ? 0 : 2);
                lockBase[s] = d.isBaseShell; lockNormal[s] = d.lockNormal;
                lockScalpRoot[s] = d.scalpRoot; lockLayerAO[s] = d.layerAO;
                lockRootRawY[s] = double.IsNaN(d.rootRawY) ? ry[b] : d.rootRawY;
                if (double.IsNaN(d.restMidR))
                {
                    double ddx = rx[b + 4], ddy = ry[b + 4] - HeadCenterY, ddz = rz[b + 4] - HeadCenterZ;
                    lockRestMidR[s] = Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
                }
                else lockRestMidR[s] = d.restMidR;
                rootNX[s] = d.rootNormal[0]; rootNY[s] = d.rootNormal[1]; rootNZ[s] = d.rootNormal[2];
                if (!d.isBaseShell && d.baseWidth > maxBaseW) maxBaseW = d.baseWidth;
                for (int k = 1; k < N; k++)
                {
                    double dx = rx[b + k] - rx[b + k - 1], dy = ry[b + k] - ry[b + k - 1], dz = rz[b + k] - rz[b + k - 1];
                    segLen[b + k] = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                }
                for (int k = 1; k < N - 1; k++)
                {
                    cx[b + k] = rx[b + k] - (rx[b + k - 1] + rx[b + k + 1]) * 0.5;
                    cy[b + k] = ry[b + k] - (ry[b + k - 1] + ry[b + k + 1]) * 0.5;
                    cz[b + k] = rz[b + k] - (rz[b + k - 1] + rz[b + k + 1]) * 0.5;
                }
            }
            AllocSpatial();
            BuildCohesionGraph();
            hasPrevHead = false; simTime = 0; impX = impY = impZ = 0;
        }

        void AllocSpatial()
        {
            hashHead = new int[HashBuckets]; hashNext = new int[MaxHashEntries]; hashLock = new int[MaxHashEntries];
            hashNode = new int[MaxHashEntries]; hashCellX = new int[MaxHashEntries]; hashCellY = new int[MaxHashEntries]; hashCellZ = new int[MaxHashEntries];
            cellMapKey = new int[HashBuckets]; cellMapId = new int[HashBuckets];
            for (int i = 0; i < HashBuckets; i++) cellMapKey[i] = -1;
            dirtySlots = new int[MaxHashEntries]; cellCx = new int[MaxHashEntries]; cellCy = new int[MaxHashEntries]; cellCz = new int[MaxHashEntries];
            cellCount = new int[MaxHashEntries]; cellStart = new int[MaxHashEntries]; cellWrite = new int[MaxHashEntries]; entryCellId = new int[MaxHashEntries];
            sLock = new int[MaxHashEntries]; sOrig = new int[MaxHashEntries];
            entryX = new float[MaxHashEntries]; entryY = new float[MaxHashEntries]; entryZ = new float[MaxHashEntries];
            entryVelX = new float[MaxHashEntries]; entryVelY = new float[MaxHashEntries]; entryVelZ = new float[MaxHashEntries];
            entryRadX = new float[MaxHashEntries]; entryRadY = new float[MaxHashEntries]; entryRadZ = new float[MaxHashEntries];
            entryHalfW = new float[MaxHashEntries]; entryHalfT = new float[MaxHashEntries]; entryInvMass = new float[MaxHashEntries];
            entryRootY = new float[MaxHashEntries]; entryRestMidR = new float[MaxHashEntries]; entryWeight = new float[MaxHashEntries];
            dispX = new float[MaxHashEntries]; dispY = new float[MaxHashEntries]; dispZ = new float[MaxHashEntries];
            velDX = new float[MaxHashEntries]; velDY = new float[MaxHashEntries]; velDZ = new float[MaxHashEntries];
            weightSum = new float[MaxHashEntries]; velWeightSum = new float[MaxHashEntries];
            entryLayer = new short[MaxHashEntries];
            sX = new float[MaxHashEntries]; sY = new float[MaxHashEntries]; sZ = new float[MaxHashEntries];
            sWeight = new float[MaxHashEntries]; sHalfW = new float[MaxHashEntries];
            lockHit = new byte[Math.Max(L, 2048)];
        }

        static int HashCell(int x, int y, int z)
        {
            unchecked { return ((x * 73856093) ^ (y * 19349663) ^ (z * 83492791)) & HashMask; }
        }
        static int PackCell(int x, int y, int z) => ((x + 512) & 1023) | (((y + 512) & 1023) << 10) | (((z + 512) & 1023) << 20);

        // ---------------- cohesion graph (buildCohesionGraph) ----------------
        void BuildCohesionGraph()
        {
            int P = L * N;
            nbCount = new int[P]; nbLock = new int[P * MaxNeighbors]; nbNode = new int[P * MaxNeighbors];
            nbWeight = new float[P * MaxNeighbors]; velBufX = new float[P]; velBufY = new float[P]; velBufZ = new float[P];
            const double CELL = 0.074, MAX_DIST = 0.072;
            double invCell = 1.0 / CELL, maxDistSq = MAX_DIST * MAX_DIST;
            for (int i = 0; i < HashBuckets; i++) hashHead[i] = -1;
            int entryCountL = 0;
            for (int s = 0; s < L; s++)
            {
                if (lockBase[s]) continue;
                for (int k = 2; k < N; k++)
                {
                    if (entryCountL >= MaxHashEntries) break;
                    int i = s * N + k;
                    int ccx = (int)Math.Floor(rx[i] * invCell), ccy = (int)Math.Floor(ry[i] * invCell), ccz = (int)Math.Floor(rz[i] * invCell);
                    int h = HashCell(ccx, ccy, ccz);
                    hashLock[entryCountL] = s; hashNode[entryCountL] = k;
                    hashCellX[entryCountL] = ccx; hashCellY[entryCountL] = ccy; hashCellZ[entryCountL] = ccz;
                    hashNext[entryCountL] = hashHead[h]; hashHead[h] = entryCountL;
                    entryCountL++;
                }
            }
            var bestLock = new int[4]; var bestNode = new int[4]; var bestDistSq = new double[4];
            for (int e = 0; e < entryCountL; e++)
            {
                int sA = hashLock[e], kA = hashNode[e];
                int ia = sA * N + kA;
                int cx0 = hashCellX[e], cy0 = hashCellY[e], cz0 = hashCellZ[e];
                int found = 0;
                bestDistSq[0] = bestDistSq[1] = bestDistSq[2] = bestDistSq[3] = maxDistSq;
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int idx = hashHead[HashCell(cx0 + dx, cy0 + dy, cz0 + dz)];
                    while (idx != -1)
                    {
                        int sB = hashLock[idx];
                        if (sB != sA)
                        {
                            int kB = hashNode[idx];
                            int ib = sB * N + kB;
                            double ex = rx[ia] - rx[ib], ey = ry[ia] - ry[ib], ez = rz[ia] - rz[ib];
                            double dSq = ex * ex + ey * ey + ez * ez;
                            if (dSq < maxDistSq)
                            {
                                bool dup = false;
                                for (int m = 0; m < found; m++)
                                {
                                    if (bestLock[m] == sB)
                                    {
                                        dup = true;
                                        if (dSq < bestDistSq[m]) { bestDistSq[m] = dSq; bestNode[m] = kB; }
                                        break;
                                    }
                                }
                                if (!dup)
                                {
                                    if (found < MaxNeighbors)
                                    {
                                        bestLock[found] = sB; bestNode[found] = kB; bestDistSq[found] = dSq; found++;
                                    }
                                    else
                                    {
                                        int worst = 0;
                                        for (int m = 1; m < MaxNeighbors; m++) if (bestDistSq[m] > bestDistSq[worst]) worst = m;
                                        if (dSq < bestDistSq[worst]) { bestLock[worst] = sB; bestNode[worst] = kB; bestDistSq[worst] = dSq; }
                                    }
                                }
                            }
                        }
                        idx = hashNext[idx];
                    }
                }
                nbCount[ia] = found;
                if (found > 0)
                {
                    double wSum = 0;
                    int baseN = ia * MaxNeighbors;
                    double massA = lockMass[sA] + 1e-6;
                    for (int m = 0; m < found; m++)
                    {
                        double distW = Math.Max(0.05, 1.0 - Math.Sqrt(bestDistSq[m]) / MAX_DIST);
                        double massRatio = Math.Min(2.5, lockMass[bestLock[m]] / massA);
                        double w = distW * (0.4 + 0.6 * massRatio);
                        nbLock[baseN + m] = bestLock[m]; nbNode[baseN + m] = bestNode[m]; nbWeight[baseN + m] = (float)w;
                        wSum += w;
                    }
                    double invW = 1.0 / wSum;
                    for (int m = 0; m < found; m++) nbWeight[baseN + m] = (float)(nbWeight[baseN + m] * invW);
                }
            }
        }

        /// <summary>Test hook: overwrite the cohesion graph with the web's dumped one.</summary>
        public void OverrideCohesionGraph(int[] count, int[] lockIdx, int[] nodeIdx, float[] weight)
        {
            Array.Copy(count, nbCount, count.Length);
            Array.Copy(lockIdx, nbLock, lockIdx.Length);
            Array.Copy(nodeIdx, nbNode, nodeIdx.Length);
            Array.Copy(weight, nbWeight, weight.Length);
        }
        public int[] NeighborCount => nbCount;
        public int[] NeighborLock => nbLock;
        public int[] NeighborNode => nbNode;
        public float[] NeighborWeight => nbWeight;

        // ---------------- public control ----------------
        public void ApplyImpulse(double x, double y, double z)
        {
            impX += x; impY += y; impZ += z;
            double l2 = impX * impX + impY * impY + impZ * impZ;
            if (l2 > 27.04)
            {
                // three Vector3.setLength(5.2): normalize (divide by length || 1) then multiply
                double len = Math.Sqrt(l2);
                double inv = 1.0 / (len == 0 ? 1.0 : len);
                impX *= inv; impY *= inv; impZ *= inv;
                impX *= 5.2; impY *= 5.2; impZ *= 5.2;
            }
        }

        /// <summary>Web snapToHead: place all particles at the rest pose in the given head pose, reset trackers.</summary>
        public void SnapToHead(double hpx, double hpy, double hpz, double hqx, double hqy, double hqz, double hqw, double widthScale)
        {
            lastWidthScale = widthScale;
            Mat3 m = Mat3.FromQuat(hqx, hqy, hqz, hqw);
            for (int i = 0; i < L * N; i++)
            {
                double x = m.m00 * rx[i] + m.m01 * ry[i] + m.m02 * rz[i] + hpx;
                double y = m.m10 * rx[i] + m.m11 * ry[i] + m.m12 * rz[i] + hpy;
                double z = m.m20 * rx[i] + m.m21 * ry[i] + m.m22 * rz[i] + hpz;
                px[i] = x; py[i] = y; pz[i] = z; qx[i] = x; qy[i] = y; qz[i] = z;
                fpx[i] = rx[i]; fpy[i] = ry[i]; fpz[i] = rz[i]; fvx[i] = fvy[i] = fvz[i] = 0;
            }
            prevHpx = hpx; prevHpy = hpy; prevHpz = hpz; prevHqx = hqx; prevHqy = hqy; prevHqz = hqz; prevHqw = hqw;
            hasPrevHead = true;
        }

        // ---------------- math helpers ----------------
        struct Mat3
        {
            public double m00, m01, m02, m10, m11, m12, m20, m21, m22;
            public static Mat3 FromQuat(double x, double y, double z, double w)
            {
                double x2 = x + x, y2 = y + y, z2 = z + z;
                double xx = x * x2, xy = x * y2, xz = x * z2, yy = y * y2, yz = y * z2, zz = z * z2, wx = w * x2, wy = w * y2, wz = w * z2;
                Mat3 r;
                r.m00 = 1 - (yy + zz); r.m01 = xy - wz; r.m02 = xz + wy;
                r.m10 = xy + wz; r.m11 = 1 - (xx + zz); r.m12 = yz - wx;
                r.m20 = xz - wy; r.m21 = yz + wx; r.m22 = 1 - (xx + yy);
                return r;
            }
        }

        static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

        static double Smoothstep01(double e0, double e1, double x)
        {
            double t = Clamp((x - e0) / (e1 - e0), 0, 1);
            return t * t * (3 - 2 * t);
        }

        static double SampleLathe(double y)
        {
            if (y <= LatheY[0] || y >= LatheY[LatheY.Length - 1]) return 0;
            for (int i = 0; i < LatheY.Length - 1; i++)
            {
                double y0 = LatheY[i], y1 = LatheY[i + 1];
                if (y >= y0 && y <= y1)
                {
                    double f = (y - y0) / (y1 - y0);
                    return LatheR[i] + (LatheR[i + 1] - LatheR[i]) * f;
                }
            }
            return 0;
        }

        /// <summary>resolveExactHeadCollision. inverse quat is passed explicitly (web passes the conjugate of the possibly non-unit slerp result).</summary>
        static bool ResolveHead(ref double wx, ref double wy, ref double wz, double hpx, double hpy, double hpz,
            double hqx, double hqy, double hqz, double hqw, double iqx, double iqy, double iqz, double iqw, double margin)
        {
            double rx_ = wx - hpx, ry_ = wy - hpy, rz_ = wz - hpz;
            double tx0 = 2.0 * (iqy * rz_ - iqz * ry_);
            double ty0 = 2.0 * (iqz * rx_ - iqx * rz_);
            double tz0 = 2.0 * (iqx * ry_ - iqy * rx_);
            double lx = rx_ + iqw * tx0 + (iqy * tz0 - iqz * ty0);
            double ly = ry_ + iqw * ty0 + (iqz * tx0 - iqx * tz0);
            double lz = rz_ + iqw * tz0 + (iqx * ty0 - iqy * tx0);
            bool pushed = false;

            double sRx = 0.277 + margin, sRy = 0.314 + margin, sRz = 0.309 + margin;
            double sx = lx / sRx, sy = (ly - 0.425) / sRy, sz = (lz + 0.012) / sRz;
            double sDistSq = sx * sx + sy * sy + sz * sz;
            if (sDistSq < 1.0 && sDistSq > 0.00001)
            {
                double invS = 1.0 / Math.Sqrt(sDistSq);
                lx = sx * invS * sRx;
                ly = 0.425 + sy * invS * sRy;
                lz = -0.012 + sz * invS * sRz;
                pushed = true;
            }

            if (ly > 0.008 && ly < 0.67)
            {
                double rSkin = SampleLathe(ly);
                double ex = rSkin * HeadScaleX + margin;
                double ez = rSkin * HeadScaleZ + margin;
                if (ex > 0.008 && ez > 0.008)
                {
                    double nx = lx / ex, nz = lz / ez;
                    double eDistSq = nx * nx + nz * nz;
                    if (eDistSq < 1.0)
                    {
                        double fade = Smoothstep01(0.67, 0.56, ly);
                        if (fade > 0.001)
                        {
                            double targetX = lx, targetZ = lz;
                            if (eDistSq > 0.00001)
                            {
                                double invLen = 1.0 / Math.Sqrt(eDistSq);
                                targetX = nx * invLen * ex;
                                targetZ = nz * invLen * ez;
                            }
                            else targetZ = ez;
                            lx += (targetX - lx) * fade;
                            lz += (targetZ - lz) * fade;
                            pushed = true;
                        }
                    }
                }
            }

            if (pushed)
            {
                double ux = 2.0 * (hqy * lz - hqz * ly);
                double uy = 2.0 * (hqz * lx - hqx * lz);
                double uz = 2.0 * (hqx * ly - hqy * lx);
                wx = hpx + lx + hqw * ux + (hqy * uz - hqz * uy);
                wy = hpy + ly + hqw * uy + (hqz * ux - hqx * uz);
                wz = hpz + lz + hqw * uz + (hqx * uy - hqy * ux);
            }
            return pushed;
        }

        /// <summary>resolveExactBustCollision (neck ellipsoid in head space + torso ellipsoid in root space).</summary>
        static bool ResolveBust(ref double wx, ref double wy, ref double wz, double hpx, double hpy, double hpz,
            double hqx, double hqy, double hqz, double hqw, double iqx, double iqy, double iqz, double iqw,
            double rpx, double rpy, double rpz, double rqx, double rqy, double rqz, double rqw,
            double irx, double iry, double irz, double irw, double margin)
        {
            if (wy > hpy + 0.16 && wy > rpy + 0.38) return false;
            bool pushed = false;
            {
                double rx_ = wx - hpx, ry_ = wy - hpy, rz_ = wz - hpz;
                double tx0 = 2.0 * (iqy * rz_ - iqz * ry_);
                double ty0 = 2.0 * (iqz * rx_ - iqx * rz_);
                double tz0 = 2.0 * (iqx * ry_ - iqy * rx_);
                double lx = rx_ + iqw * tx0 + (iqy * tz0 - iqz * ty0);
                double ly = ry_ + iqw * ty0 + (iqz * tx0 - iqx * tz0);
                double lz = rz_ + iqw * tz0 + (iqx * ty0 - iqy * tx0);
                double nRx = 0.114 + margin, nRy = 0.135 + margin, nRz = 0.118 + margin;
                double nx = lx / nRx, ny = (ly + 0.055) / nRy, nz = (lz + 0.032) / nRz;
                double nDistSq = nx * nx + ny * ny + nz * nz;
                if (nDistSq < 1.0 && nDistSq > 0.00001)
                {
                    double invN = 1.0 / Math.Sqrt(nDistSq);
                    double plx = nx * invN * nRx, ply = -0.055 + ny * invN * nRy, plz = -0.032 + nz * invN * nRz;
                    double ux = 2.0 * (hqy * plz - hqz * ply);
                    double uy = 2.0 * (hqz * plx - hqx * plz);
                    double uz = 2.0 * (hqx * ply - hqy * plx);
                    wx = hpx + plx + hqw * ux + (hqy * uz - hqz * uy);
                    wy = hpy + ply + hqw * uy + (hqz * ux - hqx * uz);
                    wz = hpz + plz + hqw * uz + (hqx * uy - hqy * ux);
                    pushed = true;
                }
            }
            {
                double rx_ = wx - rpx, ry_ = wy - rpy, rz_ = wz - rpz;
                double tx0 = 2.0 * (iry * rz_ - irz * ry_);
                double ty0 = 2.0 * (irz * rx_ - irx * rz_);
                double tz0 = 2.0 * (irx * ry_ - iry * rx_);
                double lx = rx_ + irw * tx0 + (iry * tz0 - irz * ty0);
                double ly = ry_ + irw * ty0 + (irz * tx0 - irx * tz0);
                double lz = rz_ + irw * tz0 + (irx * ty0 - iry * tx0);
                double tRx = 0.39 + margin, tRy = 0.175 + margin, tRz = 0.138 + margin;
                double tx = lx / tRx, ty = (ly - 0.145) / tRy, tz = (lz + 0.032) / tRz;
                double tDistSq = tx * tx + ty * ty + tz * tz;
                if (tDistSq < 1.0 && tDistSq > 0.00001)
                {
                    double invT = 1.0 / Math.Sqrt(tDistSq);
                    double plx = tx * invT * tRx, ply = 0.145 + ty * invT * tRy, plz = -0.032 + tz * invT * tRz;
                    double ux = 2.0 * (rqy * plz - rqz * ply);
                    double uy = 2.0 * (rqz * plx - rqx * plz);
                    double uz = 2.0 * (rqx * ply - rqy * plx);
                    wx = rpx + plx + rqw * ux + (rqy * uz - rqz * uy);
                    wy = rpy + ply + rqw * uy + (rqz * ux - rqx * uz);
                    wz = rpz + plz + rqw * uz + (rqx * uy - rqy * ux);
                    pushed = true;
                }
            }
            return pushed;
        }

        /// <summary>three.js Quaternion.slerp (this = a, toward b).</summary>
        static void Slerp(double ax, double ay, double az, double aw, double bx, double by, double bz, double bw, double t,
            out double ox, out double oy, out double oz, out double ow)
        {
            double x = bx, y = by, z = bz, w = bw;
            double dot = ax * bx + ay * by + az * bz + aw * bw;
            if (dot < 0) { x = -x; y = -y; z = -z; w = -w; dot = -dot; }
            double s = 1 - t;
            if (dot < 0.9995)
            {
                double theta = Math.Acos(dot);
                double sin = Math.Sin(theta);
                s = Math.Sin(s * theta) / sin;
                t = Math.Sin(t * theta) / sin;
                ox = ax * s + x * t; oy = ay * s + y * t; oz = az * s + z * t; ow = aw * s + w * t;
            }
            else
            {
                ox = ax * s + x * t; oy = ay * s + y * t; oz = az * s + z * t; ow = aw * s + w * t;
                double l = Math.Sqrt(ox * ox + oy * oy + oz * oz + ow * ow);
                if (l == 0) { ox = 0; oy = 0; oz = 0; ow = 1; }
                else { l = 1 / l; ox *= l; oy *= l; oz *= l; ow *= l; }
            }
        }

        // ---------------- step ----------------
        // root (bust group) pose of the current step, read by the collision helpers
        double rootPx, rootPy, rootPz, rootQx, rootQy, rootQz, rootQw, rootIx, rootIy, rootIz, rootIw;

        /// <summary>
        /// One web step. Head and root poses are WEB-space world poses (quaternions x,y,z,w). Root = bust group
        /// (torso collider frame); pass identity at the origin to match the web test rig.
        /// </summary>
        public void Step(double dt, double hpx, double hpy, double hpz, double hqx, double hqy, double hqz, double hqw,
            double rpx, double rpy, double rpz, double rqx, double rqy, double rqz, double rqw, Config cfg)
        {
            double clampedDt = Math.Min(dt, 0.033);
            if (clampedDt <= 0.0001) return;
            rootPx = rpx; rootPy = rpy; rootPz = rpz; rootQx = rqx; rootQy = rqy; rootQz = rqz; rootQw = rqw;
            rootIx = -rqx; rootIy = -rqy; rootIz = -rqz; rootIw = rqw;

            if (!hasPrevHead)
            {
                SnapToHead(hpx, hpy, hpz, hqx, hqy, hqz, hqw, lastWidthScale);
                return;
            }

            double azRad = cfg.windAzimuth * (Math.PI / 180.0);
            double windDirX = Math.Cos(azRad), windDirZ = Math.Sin(azRad);

            int SUBSTEPS = Math.Max(1, Math.Min(4, (int)Math.Floor(cfg.solverSubsteps + 0.5)));
            double subDt = clampedDt / SUBSTEPS;
            double subDtSq = subDt * subDt;
            double subDt60 = subDt * 60.0;
            double dampingMul = Math.Pow(1.0 - Clamp(cfg.damping, 0.01, 0.35), subDt60);
            double impulseDecay = Math.Exp(-4.2 * subDt);
            double baseStiff = Clamp(cfg.stiffness, 0.05, 0.98);
            double inertiaScale = Clamp(cfg.inertia, 0, 1.5);
            double bendSmooth = Clamp(cfg.bendingSmoothness, 0, 0.95);
            double marginMeters = Clamp(cfg.collisionMarginMm * 0.001, 0.002, 0.02);
            double rootAnchorScale = Clamp(cfg.rootAnchor, 0.1, 1.0) / 0.6;
            double widthScale = Clamp(cfg.strandWidthScale, 0.65, 1.45);
            double windStrength = cfg.windStrength, windTurb = cfg.windTurbulence, gravity = cfg.gravity;
            bool collisionEnabled = cfg.collisionEnabled;
            const double crownCapExtra = 0.004;
            lastWidthScale = widthScale;

            for (int sub = 0; sub < SUBSTEPS; sub++)
            {
                simTime += subDt;
                impX *= impulseDecay; impY *= impulseDecay; impZ *= impulseDecay;
                double iX = impX, iY = impY, iZ = impZ;

                double alpha0 = sub / (double)SUBSTEPS, alpha1 = (sub + 1) / (double)SUBSTEPS;
                double ppx = prevHpx + (hpx - prevHpx) * alpha0, ppy = prevHpy + (hpy - prevHpy) * alpha0, ppz = prevHpz + (hpz - prevHpz) * alpha0;
                double sx_ = prevHpx + (hpx - prevHpx) * alpha1, sy_ = prevHpy + (hpy - prevHpy) * alpha1, sz_ = prevHpz + (hpz - prevHpz) * alpha1;
                double pqx, pqy, pqz, pqw;
                Slerp(prevHqx, prevHqy, prevHqz, prevHqw, hqx, hqy, hqz, hqw, alpha0, out pqx, out pqy, out pqz, out pqw);
                double sqx, sqy, sqz, sqw;
                Slerp(prevHqx, prevHqy, prevHqz, prevHqw, hqx, hqy, hqz, hqw, alpha1, out sqx, out sqy, out sqz, out sqw);
                double iqx = -sqx, iqy = -sqy, iqz = -sqz, iqw = sqw;
                double hpx_ = sx_, hpy_ = sy_, hpz_ = sz_;
                Mat3 M = Mat3.FromQuat(sqx, sqy, sqz, sqw);
                Mat3 PM = Mat3.FromQuat(pqx, pqy, pqz, pqw);

                double tNow = simTime;
                double t115 = tNow * 1.15, t19 = tNow * 1.9, t20 = tNow * 2.0, t22 = tNow * 2.2, t24 = tNow * 2.4, t34 = tNow * 3.4;

                for (int s = 0; s < L; s++)
                {
                    int b = s * N;
                    bool isBase = lockBase[s];
                    double seedPhase = lockSeed[s] * 23.17;
                    double shellFirmness = isBase ? 3.2 : 1.0;
                    double outerMargin = isBase ? Math.Min(0.003, marginMeters * 0.4) : marginMeters;
                    double baseFollowScale = isBase ? 0.25 : 1.0;
                    double baseInertiaScale = -0.2 * inertiaScale * (isBase ? 0.2 : 1.0);
                    double baseWindScale = isBase ? 0.08 : 0.62;
                    double baseImpScale = isBase ? 0.2 : 1.35;

                    double cosFlutterBase = 0.3 * Math.Cos(t34 + seedPhase * 1.6);
                    double sinTurbX = Math.Sin(t20 + seedPhase) * windTurb * 0.3;
                    double cosTurbZ = Math.Cos(t22 + seedPhase) * windTurb * 0.3;
                    double turbYScale = 0.1 + windTurb * 0.26;
                    double crossScale = windStrength * 0.22 + 0.12;
                    double maxDispFactor = 0.16 * (1.25 - baseStiff * 0.45);

                    {
                        double r0x = rx[b], r0y = ry[b], r0z = rz[b];
                        double p0x = M.m00 * r0x + M.m01 * r0y + M.m02 * r0z + hpx_;
                        double p0y = M.m10 * r0x + M.m11 * r0y + M.m12 * r0z + hpy_;
                        double p0z = M.m20 * r0x + M.m21 * r0y + M.m22 * r0z + hpz_;
                        px[b] = p0x; py[b] = p0y; pz[b] = p0z; qx[b] = p0x; qy[b] = p0y; qz[b] = p0z;
                    }

                    for (int k = 1; k < N; k++)
                    {
                        int i = b + k;
                        double rlx = rx[i], rly = ry[i], rlz = rz[i];
                        double tipSq = TipSq[k], tipCube = TipCube[k];

                        double tx = M.m00 * rlx + M.m01 * rly + M.m02 * rlz + hpx_;
                        double ty = M.m10 * rlx + M.m11 * rly + M.m12 * rlz + hpy_;
                        double tz = M.m20 * rlx + M.m21 * rly + M.m22 * rlz + hpz_;
                        double ptx = PM.m00 * rlx + PM.m01 * rly + PM.m02 * rlz + ppx;
                        double pty = PM.m10 * rlx + PM.m11 * rly + PM.m12 * rlz + ppy;
                        double ptz = PM.m20 * rlx + PM.m21 * rly + PM.m22 * rlz + ppz;
                        double tdx = tx - ptx, tdy = ty - pty, tdz = tz - ptz;

                        if (collisionEnabled && k >= 2 && (ty <= hpy_ + 0.16 || ty <= rootPy + 0.38))
                        {
                            double vtx = tx, vty = ty, vtz = tz;
                            if (ResolveBust(ref vtx, ref vty, ref vtz, hpx_, hpy_, hpz_, sqx, sqy, sqz, sqw, iqx, iqy, iqz, iqw,
                                    rootPx, rootPy, rootPz, rootQx, rootQy, rootQz, rootQw, rootIx, rootIy, rootIz, rootIw, outerMargin))
                            { tx = vtx; ty = vty; tz = vtz; }
                        }

                        double curX = px[i], curY = py[i], curZ = pz[i];
                        double vx = (curX - qx[i]) * dampingMul;
                        double vy = (curY - qy[i]) * dampingMul;
                        double vz = (curZ - qz[i]) * dampingMul;

                        double rawFollow = 1.0 - inertiaScale * 0.56 * TipPow135[k] * baseFollowScale;
                        double followRigid = rawFollow < 0.22 ? 0.22 : rawFollow > 1.0 ? 1.0 : rawFollow;
                        double fdx = tdx * followRigid, fdy = tdy * followRigid, fdz = tdz * followRigid;
                        double nx0 = curX + fdx, ny0 = curY + fdy, nz0 = curZ + fdz;
                        qx[i] = nx0; qy[i] = ny0; qz[i] = nz0;

                        double inertMul = baseInertiaScale * tipSq;
                        vx += tdx * inertMul; vy += tdy * inertMul; vz += tdz * inertMul;

                        double spatialPhase = rlx * 4.2 + rlz * 3.8 + seedPhase;
                        double breathWave = 0.5 + 0.5 * Math.Sin(t115 + spatialPhase * 0.4);
                        double rippleWave = 0.22 * Math.Sin(t19 + seedPhase + k * 0.38);
                        double flutterWave = windTurb * (0.45 * Math.Sin(t22 + spatialPhase * 0.9 + k * 0.45) + cosFlutterBase);

                        double windGain = baseWindScale * (0.04 + 0.96 * TipPow22[k]);
                        double windMag = windStrength * (breathWave + rippleWave) * windGain;
                        double crossSwirl = crossScale * flutterWave * windGain;

                        bool isUprightSpike = lockNormal[s] && rly > 0.62;
                        double gravitySag = -gravity * (isBase ? 0.04 : isUprightSpike ? 0.03 : 0.12) * tipCube;
                        double impW = baseImpScale * (0.1 + 0.9 * tipSq);

                        double spikeBoost = isUprightSpike ? 1.55 : 1.0;
                        double springK = baseStiff * stiffW[i] * shellFirmness * spikeBoost * (40.0 + 240.0 * TipFalloff145[k]);

                        double ax = windDirX * windMag - windDirZ * crossSwirl + sinTurbX * windGain + iX * impW + (tx - nx0) * springK;
                        double ay = gravitySag + Math.Cos(t24 + seedPhase + k * 0.35) * turbYScale * windGain + iY * impW + (ty - ny0) * springK;
                        double az = windDirZ * windMag + windDirX * crossSwirl + cosTurbZ * windGain + iZ * impW + (tz - nz0) * springK;

                        double ppx2 = nx0 + vx + ax * subDtSq;
                        double ppy2 = ny0 + vy + ay * subDtSq;
                        double ppz2 = nz0 + vz + az * subDtSq;

                        if (k <= 3)
                        {
                            double kFade = k == 1 ? 0.42 : k == 2 ? 0.22 : 0.09;
                            double rawPin = kFade * baseStiff * rootAnchorScale * subDt60;
                            double rootPin = rawPin > 0.78 ? 0.78 : rawPin < 0 ? 0 : rawPin;
                            ppx2 += (tx - ppx2) * rootPin;
                            ppy2 += (ty - ppy2) * rootPin;
                            ppz2 += (tz - ppz2) * rootPin;
                        }
                        else
                        {
                            double dfx = ppx2 - tx, dfy = ppy2 - ty, dfz = ppz2 - tz;
                            double maxDisp = isBase ? 0.016 : 0.036 + maxDispFactor * tipSq;
                            double dispSq = dfx * dfx + dfy * dfy + dfz * dfz;
                            if (dispSq > maxDisp * maxDisp)
                            {
                                double dist = Math.Sqrt(dispSq);
                                double pullScale = ((maxDisp - dist) * 0.45) / dist;
                                double cdx = dfx * pullScale, cdy = dfy * pullScale, cdz = dfz * pullScale;
                                ppx2 += cdx; ppy2 += cdy; ppz2 += cdz;
                                qx[i] += cdx * 0.88; qy[i] += cdy * 0.88; qz[i] += cdz * 0.88;
                            }
                        }
                        px[i] = ppx2; py[i] = ppy2; pz[i] = ppz2;
                    }

                    // 2. anti-kink + distance + collisions
                    int iterCount = isBase ? 1 : SolverIterations;
                    double bendAlpha = isBase ? 0 : bendSmooth * 0.3;
                    bool isCrownRoot = !isBase && !lockNormal[s] && ry[b] > 0.65;
                    double maxCrownY = ry[b] + crownCapExtra;
                    for (int iter = 0; iter < iterCount; iter++)
                    {
                        bool isFinalIter = iter == iterCount - 1;
                        if (!isBase && (bendAlpha > 0.005 || !isFinalIter))
                        {
                            for (int k = 2; k < N - 1; k++)
                            {
                                int i = b + k, ip = i - 1, inx = i + 1;
                                double ax = px[i] - px[ip], ay = py[i] - py[ip], az = pz[i] - pz[ip];
                                double bx = px[inx] - px[i], by = py[inx] - py[i], bz = pz[inx] - pz[i];
                                double lenA = Math.Sqrt(ax * ax + ay * ay + az * az);
                                double lenB = Math.Sqrt(bx * bx + by * by + bz * bz);
                                double alpha = !isFinalIter ? bendAlpha : 0;
                                double curvScale = 1.0;
                                if (lenA > 0.0001 && lenB > 0.0001)
                                {
                                    double cosAng = (ax * bx + ay * by + az * bz) / (lenA * lenB);
                                    if (cosAng < 0.935)
                                    {
                                        double uRaw = (0.935 - cosAng) / 0.17;
                                        double u = uRaw < 1.0 ? uRaw : 1.0;
                                        double smoothU = u * u * (3.0 - 2.0 * u);
                                        double aKink = 0.78 * smoothU;
                                        if (aKink > alpha) alpha = aKink;
                                        curvScale = 1.0 - smoothU;
                                    }
                                }
                                if (alpha > 0.005)
                                {
                                    double cwx = (M.m00 * cx[i] + M.m01 * cy[i] + M.m02 * cz[i]) * curvScale;
                                    double cwy = (M.m10 * cx[i] + M.m11 * cy[i] + M.m12 * cz[i]) * curvScale;
                                    double cwz = (M.m20 * cx[i] + M.m21 * cy[i] + M.m22 * cz[i]) * curvScale;
                                    px[i] += (0.5 * (px[ip] + px[inx]) + cwx - px[i]) * alpha;
                                    py[i] += (0.5 * (py[ip] + py[inx]) + cwy - py[i]) * alpha;
                                    pz[i] += (0.5 * (pz[ip] + pz[inx]) + cwz - pz[i]) * alpha;
                                }
                            }
                        }

                        for (int k = 1; k < N; k++)
                        {
                            int i = b + k, ip = i - 1;
                            double dx = px[i] - px[ip], dy = py[i] - py[ip], dz = pz[i] - pz[ip];
                            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                            if (dist > 0.00001)
                            {
                                double err = (dist - segLen[i]) / dist;
                                if (k == 1)
                                {
                                    px[i] -= dx * err; py[i] -= dy * err; pz[i] -= dz * err;
                                }
                                else
                                {
                                    double prevShare = k == 2 ? 0.03 : 0.2;
                                    double pS = err * prevShare, cS = err * (1.0 - prevShare);
                                    px[ip] += dx * pS; py[ip] += dy * pS; pz[ip] += dz * pS;
                                    px[i] -= dx * cS; py[i] -= dy * cS; pz[i] -= dz * cS;
                                }
                            }

                            if (isFinalIter && k == 1 && isCrownRoot)
                            {
                                double ex = px[i] - hpx_, ey = py[i] - hpy_, ez = pz[i] - hpz_;
                                double ly = M.m01 * ex + M.m11 * ey + M.m21 * ez;
                                if (ly > maxCrownY)
                                {
                                    double lx = M.m00 * ex + M.m10 * ey + M.m20 * ez;
                                    double lz = M.m02 * ex + M.m12 * ey + M.m22 * ez;
                                    px[i] = hpx_ + M.m00 * lx + M.m01 * maxCrownY + M.m02 * lz;
                                    py[i] = hpy_ + M.m10 * lx + M.m11 * maxCrownY + M.m12 * lz;
                                    pz[i] = hpz_ + M.m20 * lx + M.m21 * maxCrownY + M.m22 * lz;
                                }
                            }

                            if (collisionEnabled)
                            {
                                double nodeMargin = k == 1 ? Math.Min(outerMargin, 0.002)
                                    : k == 2 ? Math.Min(outerMargin, outerMargin * 0.5)
                                    : k == 3 ? Math.Min(outerMargin, outerMargin * 0.82) : outerMargin;
                                double wx = px[i], wy = py[i], wz = pz[i];
                                bool hitHead = ResolveHead(ref wx, ref wy, ref wz, hpx_, hpy_, hpz_, sqx, sqy, sqz, sqw, iqx, iqy, iqz, iqw, nodeMargin);
                                px[i] = wx; py[i] = wy; pz[i] = wz;
                                if (hitHead && isFinalIter)
                                {
                                    qx[i] += (px[i] - qx[i]) * 0.86; qy[i] += (py[i] - qy[i]) * 0.86; qz[i] += (pz[i] - qz[i]) * 0.86;
                                }
                                if (k >= 2 && !isBase)
                                {
                                    wx = px[i]; wy = py[i]; wz = pz[i];
                                    bool hitBust = ResolveBust(ref wx, ref wy, ref wz, hpx_, hpy_, hpz_, sqx, sqy, sqz, sqw, iqx, iqy, iqz, iqw,
                                        rootPx, rootPy, rootPz, rootQx, rootQy, rootQz, rootQw, rootIx, rootIy, rootIz, rootIw, outerMargin);
                                    px[i] = wx; py[i] = wy; pz[i] = wz;
                                    if (hitBust && isFinalIter)
                                    {
                                        qx[i] += (px[i] - qx[i]) * 0.94; qy[i] += (py[i] - qy[i]) * 0.94; qz[i] += (pz[i] - qz[i]) * 0.94;
                                    }
                                }
                            }
                        }
                    }
                }

                ApplyCohesiveNeighborWeight();
                ResolveInterLock(hpx_, hpy_, hpz_, sqx, sqy, sqz, sqw, iqx, iqy, iqz, iqw, marginMeters, collisionEnabled, widthScale);
            }

            // 4. flip telemetry (the prevPos lerp on a detected flip feeds back into the simulation)
            {
                double iqx = -hqx, iqy = -hqy, iqz = -hqz, iqw = hqw;
                Mat3 IM = Mat3.FromQuat(iqx, iqy, iqz, iqw);
                for (int s = 0; s < L; s++)
                {
                    int b = s * N;
                    bool isBase = lockBase[s];
                    for (int k = 0; k < N; k++)
                    {
                        int i = b + k;
                        double ex = px[i] - hpx, ey = py[i] - hpy, ez = pz[i] - hpz;
                        double lx = IM.m00 * ex + IM.m01 * ey + IM.m02 * ez;
                        double ly = IM.m10 * ex + IM.m11 * ey + IM.m12 * ez;
                        double lz = IM.m20 * ex + IM.m21 * ey + IM.m22 * ez;
                        if (k == 0 || isBase)
                        {
                            fpx[i] = lx; fpy[i] = ly; fpz[i] = lz;
                            continue;
                        }
                        double vcx = lx - fpx[i], vcy = ly - fpy[i], vcz = lz - fpz[i];
                        double vLen = Math.Sqrt(vcx * vcx + vcy * vcy + vcz * vcz);
                        double pvLen = Math.Sqrt(fvx[i] * fvx[i] + fvy[i] * fvy[i] + fvz[i] * fvz[i]);
                        if (vLen > 0.00125 && pvLen > 0.00125)
                        {
                            double dirDot = (vcx * fvx[i] + vcy * fvy[i] + vcz * fvz[i]) / (vLen * pvLen);
                            if (dirDot < -0.38)
                            {
                                qx[i] += (px[i] - qx[i]) * 0.84; qy[i] += (py[i] - qy[i]) * 0.84; qz[i] += (pz[i] - qz[i]) * 0.84;
                            }
                        }
                        fvx[i] = vcx; fvy[i] = vcy; fvz[i] = vcz;
                        fpx[i] = lx; fpy[i] = ly; fpz[i] = lz;
                    }
                }
            }
            prevHpx = hpx; prevHpy = hpy; prevHpz = hpz; prevHqx = hqx; prevHqy = hqy; prevHqz = hqz; prevHqw = hqw;
        }

        // ---------------- cohesion ----------------
        void ApplyCohesiveNeighborWeight()
        {
            for (int s = 0; s < L; s++)
            {
                if (lockBase[s]) continue;
                int b = s * N;
                for (int k = 2; k < N; k++)
                {
                    int i = b + k;
                    velBufX[i] = (float)(px[i] - qx[i]); velBufY[i] = (float)(py[i] - qy[i]); velBufZ[i] = (float)(pz[i] - qz[i]);
                }
            }
            for (int s = 0; s < L; s++)
            {
                if (lockBase[s]) continue;
                int b = s * N;
                for (int k = 2; k < N; k++)
                {
                    int i = b + k;
                    int count = nbCount[i];
                    if (count == 0) continue;
                    double avgX = 0, avgY = 0, avgZ = 0;
                    int nBase = i * MaxNeighbors;
                    for (int m = 0; m < count; m++)
                    {
                        int nFlat = nbLock[nBase + m] * N + nbNode[nBase + m];
                        double w = nbWeight[nBase + m];
                        avgX += velBufX[nFlat] * w; avgY += velBufY[nFlat] * w; avgZ += velBufZ[nFlat] * w;
                    }
                    double dvx = avgX - velBufX[i], dvy = avgY - velBufY[i], dvz = avgZ - velBufZ[i];
                    double tipFactor = k / (double)(N - 1);
                    double coh = lockNormal[s] ? 0.16 : 0.38 - 0.06 * tipFactor;
                    qx[i] -= dvx * coh; qy[i] -= dvy * coh; qz[i] -= dvz * coh;
                }
            }
        }

        // ---------------- inter-lock ribbon collision (resolveInterLockVolumeAndWeight) ----------------
        void ResolveInterLock(double hpx, double hpy, double hpz, double hqx, double hqy, double hqz, double hqw,
            double iqx, double iqy, double iqz, double iqw, double marginMeters, bool collisionEnabled, double widthScale)
        {
            const double WIDTH_FRAC = 0.8, THICK_SCALE = 1.48;
            double thickScaleW = 0.65 + 0.35 * widthScale;
            double RAD_PAD = Math.Min(0.026, 0.02 * Math.Max(1.0, thickScaleW));
            double RAD_PAD_SQ = RAD_PAD * RAD_PAD;
            double massScale = Math.Max(0.35, widthScale * thickScaleW);
            double MAX_COMB_W = Math.Min(0.056 * Math.Max(1.0, widthScale), maxBaseW * widthScale * WIDTH_FRAC);
            double CELL_SIZE = Clamp(Math.Sqrt(MAX_COMB_W * MAX_COMB_W + RAD_PAD_SQ) + 0.001, 0.046, 0.084);
            double INV_CELL = 1.0 / CELL_SIZE;

            // head centre in world: HEAD_CENTER_LOCAL rotated by head quat (applyQuaternion) + head pos
            double hcx, hcy, hcz;
            {
                double lx = 0, ly = HeadCenterY, lz = HeadCenterZ;
                double ix = hqw * lx + hqy * lz - hqz * ly;
                double iy = hqw * ly + hqz * lx - hqx * lz;
                double iz = hqw * lz + hqx * ly - hqy * lx;
                double iw = -hqx * lx - hqy * ly - hqz * lz;
                hcx = ix * hqw + iw * -hqx + iy * -hqz - iz * -hqy + hpx;
                hcy = iy * hqw + iw * -hqy + iz * -hqx - ix * -hqz + hpy;
                hcz = iz * hqw + iw * -hqz + ix * -hqy - iy * -hqx + hpz;
            }

            int cellCountN = 0, entryCount = 0;
            for (int s = 0; s < L; s++)
            {
                if (lockBase[s]) continue;
                int b = s * N;
                double rootMinW = lockScalpRoot[s] >= 0.5 ? 0.36 : 0.08;
                double invMass = 1.0 / (lockMass[s] * massScale);
                int layer = lockLayer[s];
                double rootY = lockRootRawY[s];
                double midR = lockRestMidR[s];
                double baseHalfW = lockBaseW[s] * 0.5 * widthScale;
                double baseThick = lockBaseT[s] * thickScaleW;

                for (int k = 1; k < N; k++)
                {
                    if (entryCount >= MaxHashEntries) break;
                    int i = b + k;
                    double x = px[i], y = py[i], z = pz[i];
                    int ccx = (int)Math.Floor(x * INV_CELL), ccy = (int)Math.Floor(y * INV_CELL), ccz = (int)Math.Floor(z * INV_CELL);
                    int key = PackCell(ccx, ccy, ccz);
                    int slot = HashCell(ccx, ccy, ccz);
                    int cellId;
                    while (true)
                    {
                        int existing = cellMapKey[slot];
                        if (existing == key) { cellId = cellMapId[slot]; break; }
                        if (existing == -1)
                        {
                            cellId = cellCountN++;
                            cellMapKey[slot] = key; cellMapId[slot] = cellId; dirtySlots[cellId] = slot;
                            cellCx[cellId] = ccx; cellCy[cellId] = ccy; cellCz[cellId] = ccz; cellCount[cellId] = 0;
                            break;
                        }
                        slot = (slot + 1) & HashMask;
                    }
                    cellCount[cellId]++;
                    entryCellId[entryCount] = cellId;
                    int e = entryCount;
                    hashLock[e] = s; hashNode[e] = k;
                    entryX[e] = (float)x; entryY[e] = (float)y; entryZ[e] = (float)z;
                    entryVelX[e] = (float)(x - qx[i]); entryVelY[e] = (float)(y - qy[i]); entryVelZ[e] = (float)(z - qz[i]);
                    entryLayer[e] = (short)layer; entryRootY[e] = (float)rootY; entryRestMidR[e] = (float)midR;
                    entryWeight[e] = k == 1 ? 0.0f : k == 2 ? 0.62f : k == 3 ? 0.88f : 1.0f;
                    double ddx = x - hcx, ddy = y - hcy, ddz = z - hcz;
                    double rl = Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
                    double invR = 1.0 / (rl == 0 || double.IsNaN(rl) ? 1.0 : rl);
                    entryRadX[e] = (float)(ddx * invR); entryRadY[e] = (float)(ddy * invR); entryRadZ[e] = (float)(ddz * invR);
                    double rootTuck = rootMinW + (1.0 - rootMinW) * RootSin[k];
                    double rootScale = k == 1 ? 0.9 : k == 2 ? 0.96 : 1.0;
                    entryHalfW[e] = (float)(baseHalfW * rootTuck * TipTaper[k]);
                    entryHalfT[e] = (float)(baseThick * ThickFactor[k] * rootScale);
                    entryInvMass[e] = (float)invMass;
                    dispX[e] = 0; dispY[e] = 0; dispZ[e] = 0; velDX[e] = 0; velDY[e] = 0; velDZ[e] = 0; weightSum[e] = 0; velWeightSum[e] = 0;
                    entryCount++;
                }
            }

            int offset = 0;
            for (int c = 0; c < cellCountN; c++) { cellStart[c] = offset; cellWrite[c] = offset; offset += cellCount[c]; }
            for (int e = 0; e < entryCount; e++)
            {
                int pos = cellWrite[entryCellId[e]]++;
                sX[pos] = entryX[e]; sY[pos] = entryY[e]; sZ[pos] = entryZ[e];
                sLock[pos] = hashLock[e]; sWeight[pos] = entryWeight[e]; sHalfW[pos] = entryHalfW[e]; sOrig[pos] = e;
            }

            double maxPairBox = MAX_COMB_W + 0.012, negMaxPairBox = -maxPairBox;
            bool anyHit = false;

            for (int cA = 0; cA < cellCountN; cA++)
            {
                int startA = cellStart[cA], endA = startA + cellCount[cA];
                int cx0 = cellCx[cA], cy0 = cellCy[cA], cz0 = cellCz[cA];
                for (int o = 0; o < 14; o++)
                {
                    int startB, endB;
                    bool isSelf = o == 0;
                    if (isSelf)
                    {
                        if (endA - startA < 2) continue;
                        startB = startA; endB = endA;
                    }
                    else
                    {
                        int nx = cx0 + HalfDx[o], ny = cy0 + HalfDy[o], nz = cz0 + HalfDz[o];
                        int nKey = PackCell(nx, ny, nz);
                        int slot = HashCell(nx, ny, nz);
                        int cB = -1;
                        while (true)
                        {
                            int existing = cellMapKey[slot];
                            if (existing == nKey) { cB = cellMapId[slot]; break; }
                            if (existing == -1) break;
                            slot = (slot + 1) & HashMask;
                        }
                        if (cB == -1) continue;
                        startB = cellStart[cB]; endB = startB + cellCount[cB];
                    }

                    int iEnd = isSelf ? endA - 1 : endA;
                    for (int ii = startA; ii < iEnd; ii++)
                    {
                        double pAx = sX[ii], pAy = sY[ii], pAz = sZ[ii];
                        int sA = sLock[ii];
                        double wA = sWeight[ii];
                        int e = sOrig[ii];
                        double radAX = entryRadX[e], radAY = entryRadY[e], radAZ = entryRadZ[e];
                        double halfWA = entryHalfW[e], halfTA = entryHalfT[e], invMA = entryInvMass[e];
                        int layerA = entryLayer[e];
                        double rootYA = entryRootY[e], midRA = entryRestMidR[e];
                        double velAX = entryVelX[e], velAY = entryVelY[e], velAZ = entryVelZ[e];

                        int jStart = isSelf ? ii + 1 : startB;
                        for (int j = jStart; j < endB; j++)
                        {
                            double vx = sX[j] - pAx;
                            if (vx >= maxPairBox || vx <= negMaxPairBox) continue;
                            double vy = sY[j] - pAy;
                            if (vy >= maxPairBox || vy <= negMaxPairBox) continue;
                            double vz = sZ[j] - pAz;
                            if (vz >= maxPairBox || vz <= negMaxPairBox) continue;
                            int sB = sLock[j];
                            if (sB == sA) continue;
                            double wB = sWeight[j];
                            if (wA == 0 && wB == 0) continue;

                            double rawCombW = (halfWA + sHalfW[j]) * WIDTH_FRAC;
                            double combHalfW = rawCombW < MAX_COMB_W ? rawCombW : MAX_COMB_W;
                            double combWSq = combHalfW * combHalfW;
                            double dSq = vx * vx + vy * vy + vz * vz;
                            if (dSq >= combWSq + RAD_PAD_SQ) continue;

                            int idx = sOrig[j];
                            double radBX = entryRadX[idx], radBY = entryRadY[idx], radBZ = entryRadZ[idx];
                            double mrx = radAX + radBX, mry = radAY + radBY, mrz = radAZ + radBZ;
                            double mrSq = mrx * mrx + mry * mry + mrz * mrz;
                            double invMr;
                            if (mrSq > 0.25) invMr = 0.5 * (1.5 - 0.125 * mrSq);
                            else { double sq = Math.Sqrt(mrSq); invMr = 1.0 / (sq == 0 || double.IsNaN(sq) ? 1.0 : sq); }
                            mrx *= invMr; mry *= invMr; mrz *= invMr;
                            double dRad = vx * mrx + vy * mry + vz * mrz;
                            double tx = vx - dRad * mrx, ty = vy - dRad * mry, tz = vz - dRad * mrz;
                            double dTanSq = tx * tx + ty * ty + tz * tz;
                            if (dTanSq >= combWSq) continue;

                            double uTanSq = dTanSq / combWSq;
                            int layerB = entryLayer[idx];
                            double rootYB = entryRootY[idx];
                            bool aIsOuter;
                            if (layerA != layerB) aIsOuter = layerA > layerB;
                            else
                            {
                                double dyRoot = rootYA - rootYB;
                                if (dyRoot > 0.01) aIsOuter = true;
                                else if (dyRoot < -0.01) aIsOuter = false;
                                else
                                {
                                    double drMid = midRA - entryRestMidR[idx];
                                    if (drMid > 0.003) aIsOuter = true;
                                    else if (drMid < -0.003) aIsOuter = false;
                                    else
                                    {
                                        int cA3 = sA % 3, cB3 = sB % 3;
                                        if (cA3 != cB3) aIsOuter = cA3 > cB3;
                                        else
                                        {
                                            int lo = sA < sB ? sA : sB, hi = sA < sB ? sB : sA;
                                            uint hh = unchecked((uint)((lo * 73856093) ^ (hi * 19349663))) & 1u;
                                            aIsOuter = hh == 0 ? sA > sB : sA < sB;
                                        }
                                    }
                                }
                            }

                            double halfTB = entryHalfT[idx];
                            double innerT = aIsOuter ? halfTB : halfTA;
                            double outerT = aIsOuter ? halfTA : halfTB;
                            double edgeProfile = 1.0 - 0.46 * uTanSq * uTanSq;
                            double reqRad = (edgeProfile * (innerT + 0.42 * outerT) + 0.0018 * (1.0 - uTanSq)) * THICK_SCALE;
                            double backDepth = (edgeProfile * (outerT + 0.28 * innerT) + 0.0018) * THICK_SCALE;
                            double signedClear = aIsOuter ? -dRad : dRad;
                            double penRad = reqRad - signedClear;
                            if (penRad <= 0 || signedClear <= -backDepth) continue;

                            double efRaw = (1.0 - uTanSq) * 2.4;
                            double ef = efRaw < 1.0 ? efRaw : 1.0;
                            double edgeFade = ef * ef * (3.0 - 2.0 * ef);
                            double bfRaw = (signedClear + backDepth) / (backDepth * 0.78 + 0.0015);
                            double bf = bfRaw < 1.0 ? bfRaw : 1.0;
                            double backFade = bf * bf * (3.0 - 2.0 * bf);
                            double pfRaw = penRad / (reqRad * 0.16 + 0.0005);
                            double pf = pfRaw < 1.0 ? pfRaw : 1.0;
                            double penFade = pf * pf * (3.0 - 2.0 * pf);
                            double invMB = entryInvMass[idx];
                            double invSum = invMA + invMB;
                            double shareA = invMA / invSum, shareB = invMB / invSum;
                            double maxW = wA > wB ? wA : wB;
                            double cw = edgeFade * backFade * penFade;
                            double effPenRad = penRad > reqRad ? reqRad + (penRad - reqRad) * 0.2 : penRad;
                            double puRaw = effPenRad / (reqRad * 0.8 + 0.0008);
                            double pu = puRaw < 1.0 ? puRaw : 1.0;
                            double prio = 0.1 + 0.9 * (pu * pu * (3.0 - 2.0 * pu));
                            double cwPrio = cw * prio;
                            anyHit = true;

                            double rawRad = effPenRad * 0.66;
                            double radBase = (rawRad < 0.0095 ? rawRad : 0.0095) * cwPrio;
                            double rawOuterA = (aIsOuter ? 0.78 + 0.14 * shareA : 0.08 + 0.14 * shareA) * wA;
                            double rawOuterB = (aIsOuter ? 0.22 - 0.14 * shareA : 0.92 - 0.14 * shareA) * wB;
                            double outerSum = rawOuterA + rawOuterB;
                            if (outerSum == 0 || double.IsNaN(outerSum)) outerSum = 1.0;
                            double pushA = maxW * (rawOuterA / outerSum);
                            double pushB = maxW * (rawOuterB / outerSum);
                            if (pushA > wA)
                            {
                                double ex = pushA - wA; pushA = wA;
                                double pb = pushB + ex; pushB = pb < wB ? pb : wB;
                            }
                            else if (pushB > wB)
                            {
                                double ex = pushB - wB; pushB = wB;
                                double pa = pushA + ex; pushA = pa < wA ? pa : wA;
                            }
                            double signRadA = (aIsOuter ? 1.0 : -1.0) * radBase * pushA;
                            double signRadB = (aIsOuter ? 1.0 : -1.0) * radBase * pushB;

                            dispX[e] = (float)(dispX[e] + radAX * signRadA);
                            dispY[e] = (float)(dispY[e] + radAY * signRadA);
                            dispZ[e] = (float)(dispZ[e] + radAZ * signRadA);
                            dispX[idx] = (float)(dispX[idx] - radBX * signRadB);
                            dispY[idx] = (float)(dispY[idx] - radBY * signRadB);
                            dispZ[idx] = (float)(dispZ[idx] - radBZ * signRadB);

                            double maxWeightA = pushA, maxWeightB = pushB;

                            if (wA > 0 && wB > 0)
                            {
                                double dTan = Math.Sqrt(dTanSq);
                                if (dTan > 0.0002)
                                {
                                    bool sameTier = layerA == layerB && Math.Abs(rootYA - rootYB) <= 0.025;
                                    double latGain = sameTier ? 0.26 : 0.09;
                                    double penTan = combHalfW - dTan;
                                    double cfRaw = dTan / 0.011;
                                    double cf = cfRaw < 1.0 ? cfRaw : 1.0;
                                    double centerFade = cf * cf * (3.0 - 2.0 * cf);
                                    double rawLat = penTan * latGain;
                                    double latBase = (rawLat < 0.005 ? rawLat : 0.005) * cwPrio * centerFade;
                                    double latA = shareA * wA, latB = shareB * wB;
                                    double latSum = latA + latB;
                                    if (latSum == 0 || double.IsNaN(latSum)) latSum = 1.0;
                                    double lpa = maxW * (latA / latSum), lpb = maxW * (latB / latSum);
                                    double latPushA = lpa < wA ? lpa : wA;
                                    double latPushB = lpb < wB ? lpb : wB;
                                    if (latPushA > maxWeightA) maxWeightA = latPushA;
                                    if (latPushB > maxWeightB) maxWeightB = latPushB;
                                    double invTan = 1.0 / dTan;
                                    double lScaleA = latBase * latPushA * invTan, lScaleB = latBase * latPushB * invTan;
                                    dispX[e] = (float)(dispX[e] - tx * lScaleA);
                                    dispY[e] = (float)(dispY[e] - ty * lScaleA);
                                    dispZ[e] = (float)(dispZ[e] - tz * lScaleA);
                                    dispX[idx] = (float)(dispX[idx] + tx * lScaleB);
                                    dispY[idx] = (float)(dispY[idx] + ty * lScaleB);
                                    dispZ[idx] = (float)(dispZ[idx] + tz * lScaleB);
                                }
                            }

                            weightSum[e] = (float)(weightSum[e] + cwPrio * maxWeightA);
                            weightSum[idx] = (float)(weightSum[idx] + cwPrio * maxWeightB);

                            double dvx = entryVelX[idx] - velAX, dvy = entryVelY[idx] - velAY, dvz = entryVelZ[idx] - velAZ;
                            double dvRad = dvx * mrx + dvy * mry + dvz * mrz;
                            double momW = 0.42 * maxW * cw;
                            dvx = (dvx - dvRad * mrx) * momW;
                            dvy = (dvy - dvRad * mry) * momW;
                            dvz = (dvz - dvRad * mrz) * momW;
                            double vwA = cw * shareA * wA, vwB = cw * shareB * wB;
                            velWeightSum[e] = (float)(velWeightSum[e] + vwA);
                            velWeightSum[idx] = (float)(velWeightSum[idx] + vwB);
                            velDX[e] = (float)(velDX[e] + dvx * shareA * wA);
                            velDY[e] = (float)(velDY[e] + dvy * shareA * wA);
                            velDZ[e] = (float)(velDZ[e] + dvz * shareA * wA);
                            velDX[idx] = (float)(velDX[idx] - dvx * shareB * wB);
                            velDY[idx] = (float)(velDY[idx] - dvy * shareB * wB);
                            velDZ[idx] = (float)(velDZ[idx] - dvz * shareB * wB);
                        }
                    }
                }
            }

            for (int c = 0; c < cellCountN; c++) cellMapKey[dirtySlots[c]] = -1;
            if (!anyHit) return;

            for (int s = 0; s < L; s++) lockHit[s] = 0;

            for (int e = 0; e < entryCount; e++)
            {
                double wNode = entryWeight[e];
                if (wNode <= 0) continue;
                double wSum = weightSum[e];
                if (wSum <= 1e-6) continue;
                int lockIdx = hashLock[e];
                lockHit[lockIdx] = 1;
                int i = lockIdx * N + hashNode[e];
                double invD = wNode / (wSum > wNode ? wSum : wNode);
                double dx = dispX[e] * invD, dy = dispY[e] * invD, dz = dispZ[e] * invD;
                double magSq = dx * dx + dy * dy + dz * dz;
                const double maxDisp = 0.0095;
                if (magSq > maxDisp * maxDisp)
                {
                    double sc = maxDisp / Math.Sqrt(magSq);
                    dx *= sc; dy *= sc; dz *= sc;
                }
                px[i] += dx; py[i] += dy; pz[i] += dz;
                double vwSum = velWeightSum[e];
                double invV = vwSum > wNode ? wNode / vwSum : 1.0;
                double vdx = velDX[e] * invV, vdy = velDY[e] * invV, vdz = velDZ[e] * invV;
                double vMagSq = vdx * vdx + vdy * vdy + vdz * vdz;
                const double maxVelD = 0.004;
                if (vMagSq > maxVelD * maxVelD)
                {
                    double vSc = maxVelD / Math.Sqrt(vMagSq);
                    vdx *= vSc; vdy *= vSc; vdz *= vSc;
                }
                qx[i] += dx - vdx; qy[i] += dy - vdy; qz[i] += dz - vdz;
                double fricRamp = wSum * 3.5 < 1.0 ? wSum * 3.5 : 1.0;
                double rawFric = 0.2 + wSum * 0.3;
                double contactFriction = fricRamp * (rawFric < 0.62 ? rawFric : 0.62);
                qx[i] += (px[i] - qx[i]) * contactFriction;
                qy[i] += (py[i] - qy[i]) * contactFriction;
                qz[i] += (pz[i] - qz[i]) * contactFriction;
            }

            Mat3 M = Mat3.FromQuat(hqx, hqy, hqz, hqw);
            const double crownCapExtra = 0.004;

            for (int s = 0; s < L; s++)
            {
                if (lockHit[s] == 0) continue;
                int b = s * N;
                bool isCrownRoot = !lockNormal[s] && ry[b] > 0.65;
                double maxCrownY = ry[b] + crownCapExtra;

                for (int pass = 0; pass < 2; pass++)
                {
                    double passGain = pass == 0 ? 0.68 : 0.28;
                    for (int k = 2; k < N - 1; k++)
                    {
                        int i = b + k, ip = i - 1, inx = i + 1;
                        double ax = px[i] - px[ip], ay = py[i] - py[ip], az = pz[i] - pz[ip];
                        double bx = px[inx] - px[i], by = py[inx] - py[i], bz = pz[inx] - pz[i];
                        double lenA = Math.Sqrt(ax * ax + ay * ay + az * az);
                        double lenB = Math.Sqrt(bx * bx + by * by + bz * bz);
                        if (lenA > 0.0001 && lenB > 0.0001)
                        {
                            double cosAng = (ax * bx + ay * by + az * bz) / (lenA * lenB);
                            if (cosAng < 0.935)
                            {
                                double uRaw = (0.935 - cosAng) / 0.15;
                                double u = uRaw < 1.0 ? uRaw : 1.0;
                                double smoothU = u * u * (3.0 - 2.0 * u);
                                double blend = smoothU * passGain;
                                double curvScale = 1.0 - smoothU;
                                double cwx = (M.m00 * cx[i] + M.m01 * cy[i] + M.m02 * cz[i]) * curvScale;
                                double cwy = (M.m10 * cx[i] + M.m11 * cy[i] + M.m12 * cz[i]) * curvScale;
                                double cwz = (M.m20 * cx[i] + M.m21 * cy[i] + M.m22 * cz[i]) * curvScale;
                                double dfx = (0.5 * (px[ip] + px[inx]) + cwx - px[i]) * blend;
                                double dfy = (0.5 * (py[ip] + py[inx]) + cwy - py[i]) * blend;
                                double dfz = (0.5 * (pz[ip] + pz[inx]) + cwz - pz[i]) * blend;
                                if (k >= 3)
                                {
                                    px[i] += dfx * 0.62; py[i] += dfy * 0.62; pz[i] += dfz * 0.62;
                                    qx[i] += dfx * 0.62; qy[i] += dfy * 0.62; qz[i] += dfz * 0.62;
                                    px[ip] -= dfx * 0.19; py[ip] -= dfy * 0.19; pz[ip] -= dfz * 0.19;
                                    qx[ip] -= dfx * 0.19; qy[ip] -= dfy * 0.19; qz[ip] -= dfz * 0.19;
                                    px[inx] -= dfx * 0.19; py[inx] -= dfy * 0.19; pz[inx] -= dfz * 0.19;
                                    qx[inx] -= dfx * 0.19; qy[inx] -= dfy * 0.19; qz[inx] -= dfz * 0.19;
                                }
                                else
                                {
                                    px[i] += dfx * 0.72; py[i] += dfy * 0.72; pz[i] += dfz * 0.72;
                                    qx[i] += dfx * 0.72; qy[i] += dfy * 0.72; qz[i] += dfz * 0.72;
                                    px[inx] -= dfx * 0.28; py[inx] -= dfy * 0.28; pz[inx] -= dfz * 0.28;
                                    qx[inx] -= dfx * 0.28; qy[inx] -= dfy * 0.28; qz[inx] -= dfz * 0.28;
                                }
                            }
                        }
                    }

                    for (int k = 1; k < N; k++)
                    {
                        int i = b + k, ip = i - 1;
                        double dx = px[i] - px[ip], dy = py[i] - py[ip], dz = pz[i] - pz[ip];
                        double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (dist > 0.00001)
                        {
                            double err = (dist - segLen[i]) / dist;
                            if (k == 1)
                            {
                                px[i] -= dx * err; py[i] -= dy * err; pz[i] -= dz * err;
                                qx[i] -= dx * err; qy[i] -= dy * err; qz[i] -= dz * err;
                            }
                            else
                            {
                                double prevShare = k == 2 ? 0.04 : 0.2;
                                double pS = err * prevShare, cS = err * (1.0 - prevShare);
                                px[ip] += dx * pS; py[ip] += dy * pS; pz[ip] += dz * pS;
                                qx[ip] += dx * pS; qy[ip] += dy * pS; qz[ip] += dz * pS;
                                px[i] -= dx * cS; py[i] -= dy * cS; pz[i] -= dz * cS;
                                qx[i] -= dx * cS; qy[i] -= dy * cS; qz[i] -= dz * cS;
                            }
                        }
                        if (k == 1 && isCrownRoot)
                        {
                            double ex = px[i] - hpx, ey = py[i] - hpy, ez = pz[i] - hpz;
                            double ly = M.m01 * ex + M.m11 * ey + M.m21 * ez;
                            if (ly > maxCrownY)
                            {
                                double lx = M.m00 * ex + M.m10 * ey + M.m20 * ez;
                                double lz = M.m02 * ex + M.m12 * ey + M.m22 * ez;
                                px[i] = hpx + M.m00 * lx + M.m01 * maxCrownY + M.m02 * lz;
                                py[i] = hpy + M.m10 * lx + M.m11 * maxCrownY + M.m12 * lz;
                                pz[i] = hpz + M.m20 * lx + M.m21 * maxCrownY + M.m22 * lz;
                            }
                        }
                        if (collisionEnabled)
                        {
                            double nodeMargin = k == 1 ? Math.Min(marginMeters, 0.002)
                                : k == 2 ? Math.Min(marginMeters, marginMeters * 0.5)
                                : k == 3 ? Math.Min(marginMeters, marginMeters * 0.82) : marginMeters;
                            double wx = px[i], wy = py[i], wz = pz[i];
                            bool hitH = ResolveHead(ref wx, ref wy, ref wz, hpx, hpy, hpz, hqx, hqy, hqz, hqw, iqx, iqy, iqz, iqw, nodeMargin);
                            px[i] = wx; py[i] = wy; pz[i] = wz;
                            bool hitB = false;
                            if (k >= 2)
                            {
                                wx = px[i]; wy = py[i]; wz = pz[i];
                                hitB = ResolveBust(ref wx, ref wy, ref wz, hpx, hpy, hpz, hqx, hqy, hqz, hqw, iqx, iqy, iqz, iqw,
                                    rootPx, rootPy, rootPz, rootQx, rootQy, rootQz, rootQw, rootIx, rootIy, rootIz, rootIw, marginMeters);
                                px[i] = wx; py[i] = wy; pz[i] = wz;
                            }
                            if (hitB)
                            {
                                qx[i] += (px[i] - qx[i]) * 0.94; qy[i] += (py[i] - qy[i]) * 0.94; qz[i] += (pz[i] - qz[i]) * 0.94;
                            }
                            else if (hitH)
                            {
                                qx[i] += (px[i] - qx[i]) * 0.88; qy[i] += (py[i] - qy[i]) * 0.88; qz[i] += (pz[i] - qz[i]) * 0.88;
                            }
                        }
                    }
                }
            }
        }
    }
}
