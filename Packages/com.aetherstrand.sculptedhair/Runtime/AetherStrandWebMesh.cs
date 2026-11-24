using UnityEngine;
using UnityEngine.Rendering;

namespace AetherStrand
{
    /// <summary>
    /// Web hair mesh (hairPhysics.ts buildVolumetricBuffers + updateVolumetricBuffers) driven by live solver particles.
    /// Same ring/frame math as AetherStrandMeshGenerator.BuildVolumetricMesh (frames are built in WEB space so cross
    /// products match, then X is mirrored to Unity), but reading live particle positions instead of rest points.
    /// Vertex order, UVs, uv2, colours, tangent.w and triangle winding are identical to the static mesh.
    /// Output vertices are in "hair space": (-webX, webY, webZ) * unitScale, i.e. Unity world axes relative to the hair origin.
    /// </summary>
    public sealed class AetherStrandWebMesh
    {
        public const int Rings = AetherStrandWebSolver.N - 1; // 11
        public const int RingVerts = 7;
        public const int VertsPerLock = Rings * RingVerts + 1;

        static readonly float[] PU = { 0.0f, 0.17f, 0.34f, 0.5f, 0.66f, 0.83f, 1.0f };
        static readonly double[] PX = { 0.0, 0.76, 1.0, 0.0, -1.0, -0.76, 0.0 };
        static readonly double[] PY = { 1.0, 0.42, -0.32, -0.45, -0.32, 0.42, 1.0 };
        static readonly double[] PNX = { 0.0, 0.64409, 0.94451, 0.0, -0.94451, -0.64409, 0.0 };
        static readonly double[] PNY = { 1.0, 0.76495, -0.32849, -1.0, -0.32849, 0.76495, 1.0 };

        readonly AetherStrandWebSolver.LockData[] locks;
        readonly AetherStrandWebSolver solver;
        public readonly Mesh mesh;
        readonly Vector3[] verts, norms;
        readonly Vector4[] tans;
        // per-ring tables (identical to the web's Float32Array tables)
        static readonly double[] RootSin = new double[AetherStrandWebSolver.N], TipTaper = new double[AetherStrandWebSolver.N],
            BaseTipTaper = new double[AetherStrandWebSolver.N], TipFactor = new double[AetherStrandWebSolver.N], ThickPow = new double[AetherStrandWebSolver.N];

        static AetherStrandWebMesh()
        {
            for (int k = 0; k < AetherStrandWebSolver.N; k++)
            {
                double t = k / 11.0;
                RootSin[k] = (float)System.Math.Sin(System.Math.Min(1.0, t * 4.2) * System.Math.PI * 0.5);
                TipTaper[k] = (float)System.Math.Max(0.06, t < 0.44 ? 1.0 - t * 0.14 : System.Math.Pow((1.0 - t) / 0.56, 0.94) * 0.938);
                BaseTipTaper[k] = (float)System.Math.Max(0.04, t < 0.42 ? 1.0 - t * 0.16 : System.Math.Pow((1.0 - t) / 0.58, 1.12) * 0.933);
                TipFactor[k] = (float)t;
                ThickPow[k] = (float)System.Math.Pow(System.Math.Max(0.04, 1.0 - t), 0.86);
            }
        }

        public AetherStrandWebMesh(AetherStrandWebSolver solver, AetherStrandWebSolver.LockData[] locks)
        {
            this.solver = solver; this.locks = locks;
            int L = locks.Length, V = L * VertsPerLock;
            verts = new Vector3[V]; norms = new Vector3[V]; tans = new Vector4[V];
            var uv = new Vector2[V]; var uv2 = new Vector2[V]; var col = new Color[V];
            var tris = new int[L * ((Rings - 2) * (RingVerts - 1) * 6 + (RingVerts - 1) * 3 + (RingVerts - 1) * 6)];
            int ti = 0;
            for (int s = 0; s < L; s++)
            {
                var d = locks[s];
                int bv = s * VertsPerLock;
                float mask = d.isBaseShell ? 0f : d.isFadeCap ? 0.25f : 1f;
                int vi = bv;
                for (int k = 0; k < Rings; k++)
                {
                    float t = k / (float)(AetherStrandWebSolver.N - 1);
                    for (int r = 0; r < RingVerts; r++)
                    {
                        uv[vi] = new Vector2(PU[r], t);
                        uv2[vi] = new Vector2((float)d.scalpRoot, 0f);
                        float under = PY[r] < -0.1 ? 0.85f : 1.0f;
                        { float sd = Mathf.Clamp01((float)d.seed) * 255f, sh = Mathf.Floor(sd); col[vi] = new Color(sd - sh, (float)d.layerAO * under, sh / 255f, mask); }
                        vi++;
                    }
                }
                int tip = vi;
                uv[tip] = new Vector2(0.5f, 1f); uv2[tip] = new Vector2((float)d.scalpRoot, 0f);
                { float sd = Mathf.Clamp01((float)d.seed) * 255f, sh = Mathf.Floor(sd); col[tip] = new Color(sd - sh, (float)d.layerAO, sh / 255f, mask); }
                for (int k = 0; k < Rings - 1; k++)
                {
                    int r0 = bv + k * RingVerts, r1 = bv + (k + 1) * RingVerts;
                    for (int r = 0; r < RingVerts - 1; r++)
                    {
                        int a = r0 + r, b = r0 + r + 1, c = r1 + r, dd = r1 + r + 1;
                        tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                        tris[ti++] = b; tris[ti++] = c; tris[ti++] = dd;
                    }
                }
                int last = bv + (Rings - 1) * RingVerts;
                for (int r = 0; r < RingVerts - 1; r++) { tris[ti++] = last + r; tris[ti++] = tip; tris[ti++] = last + r + 1; }
            }
            mesh = new Mesh { name = "AetherStrandWebHair", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            mesh.vertices = verts; // zeros, replaced by Update
            mesh.uv = uv; mesh.uv2 = uv2; mesh.colors = col;
            mesh.SetTriangles(tris, 0, false);
            mesh.bounds = new Bounds(new Vector3(0, 0.5f, 0), new Vector3(2, 2, 2));
        }

        /// <summary>
        /// Rebuild all ring vertices from the solver's current particles (web updateVolumetricBuffers) and upload them.
        /// head* = web-space head pose of the last step (the web recomputes the head centre from it).
        /// </summary>
        /// <summary>
        /// Head-fit output mode (character rigs): vertices are written in HEAD-BONE-LOCAL space after mapping web space through
        /// the per-axis head fit (see Editor/AetherStrandHeadFit). The renderer must then be a child of the head bone with identity local pose.
        /// </summary>
        public bool useFit;
        public Vector3 fitScale = Vector3.one;
        public Vector3 fitOffset = Vector3.zero;

        public void Update(double hpx, double hpy, double hpz, double hqx, double hqy, double hqz, double hqw, double widthScale, float unitScale)
        {
            // rotate by the conjugate of the head quaternion: web world offset -> rest-head-local (web space)
            double cqx = -hqx, cqy = -hqy, cqz = -hqz, cqw = hqw;
            bool fit = useFit;
            double fsx = fitScale.x, fsy = fitScale.y, fsz = fitScale.z;
            Vector3 MapPoint(double x, double y, double z)
            {
                if (!fit) return new Vector3((float)(-x * unitScale), (float)(y * unitScale), (float)(z * unitScale));
                double ax = x - hpx, ay = y - hpy, az = z - hpz;
                double tx2 = 2 * (cqy * az - cqz * ay), ty2 = 2 * (cqz * ax - cqx * az), tz2 = 2 * (cqx * ay - cqy * ax);
                double lx = ax + cqw * tx2 + (cqy * tz2 - cqz * ty2), ly = ay + cqw * ty2 + (cqz * tx2 - cqx * tz2), lz = az + cqw * tz2 + (cqx * ty2 - cqy * tx2);
                return new Vector3((float)(-lx * fsx + fitOffset.x), (float)(ly * fsy + fitOffset.y), (float)(lz * fsz + fitOffset.z));
            }
            // kind: 0 = normal (inverse-transpose of the fit), 1 = tangent/direction
            Vector3 MapDir(double x, double y, double z, int kind)
            {
                if (!fit) return new Vector3((float)-x, (float)y, (float)z);
                double tx2 = 2 * (cqy * z - cqz * y), ty2 = 2 * (cqz * x - cqx * z), tz2 = 2 * (cqx * y - cqy * x);
                double lx = x + cqw * tx2 + (cqy * tz2 - cqz * ty2), ly = y + cqw * ty2 + (cqz * tx2 - cqx * tz2), lz = z + cqw * tz2 + (cqx * ty2 - cqy * tx2);
                double dx = -lx, dy = ly, dz = lz;
                if (kind == 0) { dx /= fsx; dy /= fsy; dz /= fsz; } else { dx *= fsx; dy *= fsy; dz *= fsz; }
                double l = System.Math.Sqrt(dx * dx + dy * dy + dz * dz); if (l < 1e-12) l = 1;
                return new Vector3((float)(dx / l), (float)(dy / l), (float)(dz / l));
            }
            double[] X = solver.PosX, Y = solver.PosY, Z = solver.PosZ;
            // head centre in world = HEAD_CENTER_LOCAL (0,0.425,-0.01) rotated + head pos (three applyQuaternion)
            double hcx, hcy, hcz;
            {
                double lx = 0, ly = 0.425, lz = -0.01;
                double ix = hqw * lx + hqy * lz - hqz * ly;
                double iy = hqw * ly + hqz * lx - hqx * lz;
                double iz = hqw * lz + hqx * ly - hqy * lx;
                double iw = -hqx * lx - hqy * ly - hqz * lz;
                hcx = ix * hqw + iw * -hqx + iy * -hqz - iz * -hqy + hpx;
                hcy = iy * hqw + iw * -hqy + iz * -hqx - ix * -hqz + hpy;
                hcz = iz * hqw + iw * -hqz + ix * -hqy - iy * -hqx + hpz;
            }
            double x2 = hqx + hqx, y2 = hqy + hqy, z2 = hqz + hqz;
            double xx = hqx * x2, xy = hqx * y2, xz = hqx * z2, yy = hqy * y2, yz = hqy * z2, zz = hqz * z2, wx = hqw * x2, wy = hqw * y2, wz = hqw * z2;
            double m00 = 1 - (yy + zz), m01 = xy - wz, m02 = xz + wy;
            double m10 = xy + wz, m11 = 1 - (xx + zz), m12 = yz - wx;
            double m20 = xz - wy, m21 = yz + wx, m22 = 1 - (xx + yy);
            double us = unitScale;
            int vIdx = 0;
            for (int s = 0; s < locks.Length; s++)
            {
                var lk = locks[s];
                int b = s * AetherStrandWebSolver.N;
                bool isBase = lk.isBaseShell, lockNormal = lk.lockNormal;
                double rootMinW = lk.scalpRoot >= 0.5 ? 0.36 : 0.08;
                double effW = isBase ? 1.0 : widthScale;
                double effT = isBase ? 1.0 : 0.9 * (0.65 + 0.35 * effW);
                double baseHalfW = lk.baseWidth * 0.5 * effW;
                double baseHalfT = lk.baseThickness * 0.5 * effT;

                double odx = m00 * lk.rootNormal[0] + m01 * lk.rootNormal[1] + m02 * lk.rootNormal[2];
                double ody = m10 * lk.rootNormal[0] + m11 * lk.rootNormal[1] + m12 * lk.rootNormal[2];
                double odz = m20 * lk.rootNormal[0] + m21 * lk.rootNormal[1] + m22 * lk.rootNormal[2];
                double odl = System.Math.Sqrt(odx * odx + ody * ody + odz * odz);
                double odInv = 1.0 / (odl == 0 ? 1.0 : odl);
                odx *= odInv; ody *= odInv; odz *= odInv;
                double ox = 0, oy = 1, oz = 0;

                for (int k = 0; k < Rings; k++)
                {
                    int i = b + k;
                    double px0 = X[i], py0 = Y[i], pz0 = Z[i];
                    double tx, ty, tz;
                    if (k == 0) { tx = X[i + 1] - px0; ty = Y[i + 1] - py0; tz = Z[i + 1] - pz0; }
                    else { tx = X[i + 1] - X[i - 1]; ty = Y[i + 1] - Y[i - 1]; tz = Z[i + 1] - Z[i - 1]; }
                    double tl = System.Math.Sqrt(tx * tx + ty * ty + tz * tz);
                    double tInv = 1.0 / (tl == 0 ? 1.0 : tl);
                    tx *= tInv; ty *= tInv; tz *= tInv;

                    double hnx = px0 - hcx, hny = py0 - hcy, hnz = pz0 - hcz;
                    double hl = System.Math.Sqrt(hnx * hnx + hny * hny + hnz * hnz);
                    double hnInv = 1.0 / (hl == 0 ? 1.0 : hl);
                    hnx *= hnInv; hny *= hnInv; hnz *= hnInv;

                    double sx, sy, sz;
                    if (!lockNormal)
                    {
                        sx = ty * hnz - tz * hny; sy = tz * hnx - tx * hnz; sz = tx * hny - ty * hnx;
                        if (sx * sx + sy * sy + sz * sz >= 0.0025) { odx = hnx; ody = hny; odz = hnz; }
                        else
                        {
                            odx += (hnx - odx) * 0.5; ody += (hny - ody) * 0.5; odz += (hnz - odz) * 0.5;
                            double ol = System.Math.Sqrt(odx * odx + ody * ody + odz * odz);
                            double oInv = 1.0 / (ol == 0 ? 1.0 : ol);
                            odx *= oInv; ody *= oInv; odz *= oInv;
                            sx = ty * odz - tz * ody; sy = tz * odx - tx * odz; sz = tx * ody - ty * odx;
                        }
                    }
                    else { sx = ty * odz - tz * ody; sy = tz * odx - tx * odz; sz = tx * ody - ty * odx; }

                    double sLenSq = sx * sx + sy * sy + sz * sz;
                    if (sLenSq < 0.0001) { sx = 1; sy = 0; sz = 0; }
                    else { double sInv = 1.0 / System.Math.Sqrt(sLenSq); sx *= sInv; sy *= sInv; sz *= sInv; }

                    ox = sy * tz - sz * ty; oy = sz * tx - sx * tz; oz = sx * ty - sy * tx;

                    double rSin = RootSin[k];
                    double rootTuck = isBase ? 0.88 + 0.12 * rSin : rootMinW + (1.0 - rootMinW) * rSin;
                    double rootThickTuck = isBase ? 1.0 : 0.2 + 0.8 * rSin;
                    bool isFade = lk.isFadeCap;
                    double tipTaper = isFade ? 1.0 : (isBase ? BaseTipTaper[k] : TipTaper[k]);
                    double halfW = baseHalfW * rootTuck * tipTaper;
                    double halfT = isFade ? baseHalfT * rootThickTuck * 0.55 * (1.0 - TipFactor[k] * 0.65) : baseHalfT * rootThickTuck * ThickPow[k];

                    for (int r = 0; r < RingVerts; r++)
                    {
                        double a = PX[r] * halfW, c = PY[r] * halfT;
                        double vx = px0 + sx * a + ox * c, vy = py0 + sy * a + oy * c, vz = pz0 + sz * a + oz * c;
                        verts[vIdx] = MapPoint(vx, vy, vz);
                        double nx = sx * PNX[r] + ox * PNY[r], ny = sy * PNX[r] + oy * PNY[r], nz = sz * PNX[r] + oz * PNY[r];
                        norms[vIdx] = MapDir(nx, ny, nz, 0);
                        { Vector3 tv = MapDir(tx, ty, tz, 1); tans[vIdx] = new Vector4(tv.x, tv.y, tv.z, 1f); }
                        vIdx++;
                    }
                }

                int ti = b + AetherStrandWebSolver.N - 1;
                double ttx = X[ti] - X[ti - 1], tty = Y[ti] - Y[ti - 1], ttz = Z[ti] - Z[ti - 1];
                double tl2 = System.Math.Sqrt(ttx * ttx + tty * tty + ttz * ttz);
                double ttInv = 1.0 / (tl2 == 0 ? 1.0 : tl2);
                ttx *= ttInv; tty *= ttInv; ttz *= ttInv;
                verts[vIdx] = MapPoint(X[ti], Y[ti], Z[ti]);
                norms[vIdx] = MapDir(ox, oy, oz, 0);
                { Vector3 tv2 = MapDir(ttx, tty, ttz, 1); tans[vIdx] = new Vector4(tv2.x, tv2.y, tv2.z, 1f); }
                vIdx++;
            }
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTangents(tans);
            mesh.RecalculateBounds();
        }

        /// <summary>Copy of the last vertex positions (hair space), for tests.</summary>
        public Vector3[] Vertices => verts;
    }
}
