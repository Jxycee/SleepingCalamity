#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AetherStrand.Editor
{
    /// <summary>
    /// Keeps the character's eyes visible: Bangs locks that hang in front of the face between the eyes' outer edges are shortened so their
    /// tips end just above the eyes. Works on the PresetGeometry.json (web pivot space) so the baked and the live-solver import both get it.
    /// Deviation from the web look is deliberate and opt-in (AetherStrandWebImport keepEyesClear).
    /// </summary>
    public static class AetherStrandFringeFit
    {
        public struct EyeLine { public float topY, centreZ, halfWidthX; }

        /// <summary>Eye landmarks in head-bone space from the character's eyeball renderers; false if none found.</summary>
        public static bool TryMeasureEyes(GameObject character, Transform head, out EyeLine eye)
        {
            eye = default(EyeLine);
            float top = float.MinValue, zSum = 0, xMax = 0; int n = 0;
            foreach (var r in character.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.ToLowerInvariant().Contains("eyeball")) continue;
                Bounds b = r.bounds;
                Vector3 c = head.InverseTransformPoint(b.center);
                float ey = b.extents.y; // world-aligned half height; fine for a sphere-like eyeball
                top = Mathf.Max(top, c.y + ey);
                zSum += c.z; n++;
                xMax = Mathf.Max(xMax, Mathf.Abs(c.x) + b.extents.x);
            }
            if (n == 0) return false;
            eye.topY = top; eye.centreZ = zSum / n; eye.halfWidthX = xMax;
            return true;
        }

        /// <summary>Writes a trimmed copy of the geometry to <paramref name="outPath"/>; returns the number of locks shortened (0 = unchanged copy).</summary>
        public static int TrimForEyes(string geometryJson, AetherStrandHeadFit.Fit fit, EyeLine eye, string outPath, float clearance = 0.004f)
        {
            var root = (Dictionary<string, object>)AetherStrandJson.Parse(File.ReadAllText(geometryJson));
            var locks = (List<object>)root["locks"];
            float capY = eye.topY + clearance, frontZ = eye.centreZ - 0.04f, halfX = eye.halfWidthX + 0.012f;
            int trimmed = 0;
            foreach (object o in locks)
            {
                var d = (Dictionary<string, object>)o;
                if (AetherStrandJson.B(d, "isBaseShell", false)) continue;
                if (!d.ContainsKey("zone") || !string.Equals((string)d["zone"], "Bangs", StringComparison.OrdinalIgnoreCase)) continue;
                var pts = (List<object>)d["points"]; int n = pts.Count; if (n < 3) continue;
                var web = new Vector3[n]; // blender [x,-z,y] -> web (x,y,z) -> unity-mirrored fitted head-local
                var fitted = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    double[] p = AetherStrandJson.Arr(pts[i]);
                    web[i] = new Vector3((float)p[0], (float)p[2], (float)-p[1]);
                    fitted[i] = AetherStrandHeadFit.Apply(fit, new Vector3(-web[i].x, web[i].y, web[i].z));
                }
                int hit = -1;
                for (int i = 1; i < n; i++)
                    if (fitted[i].y < capY && fitted[i].z > frontZ && Mathf.Abs(fitted[i].x) < halfX) { hit = i; break; }
                if (hit < 0) continue;
                float dy = fitted[hit - 1].y - fitted[hit].y;
                float t = dy > 1e-6f ? Mathf.Clamp01((fitted[hit - 1].y - capY) / dy) : 0f;
                float f = Mathf.Max(0.18f, ((hit - 1) + t) / (n - 1));
                var np = new List<object>(n);
                for (int i = 0; i < n; i++)
                {
                    float u = (float)i / (n - 1) * f * (n - 1);
                    int i0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, n - 1), i1 = Mathf.Min(n - 1, i0 + 1);
                    Vector3 w = Vector3.Lerp(web[i0], web[i1], u - i0);
                    np.Add(new List<object> { (double)w.x, (double)(-w.z), (double)w.y });
                }
                d["points"] = np; trimmed++;
            }
            File.WriteAllText(outPath, Write(root));
            return trimmed;
        }

        // minimal JSON writer for the plain Dictionary/List/double/string/bool tree AetherStrandJson.Parse produces
        static string Write(object o) { var sb = new StringBuilder(1 << 20); WriteValue(sb, o); return sb.ToString(); }
        static void WriteValue(StringBuilder sb, object o)
        {
            if (o == null) { sb.Append("null"); return; }
            if (o is string s) { sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"'); return; }
            if (o is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (o is double dd) { sb.Append(dd.ToString("R", CultureInfo.InvariantCulture)); return; }
            if (o is IDictionary<string, object> dict)
            {
                sb.Append('{'); bool first = true;
                foreach (var kv in dict) { if (!first) sb.Append(','); first = false; WriteValue(sb, kv.Key); sb.Append(':'); WriteValue(sb, kv.Value); }
                sb.Append('}'); return;
            }
            if (o is IList<object> list)
            {
                sb.Append('['); for (int i = 0; i < list.Count; i++) { if (i > 0) sb.Append(','); WriteValue(sb, list[i]); } sb.Append(']'); return;
            }
            sb.Append(Convert.ToDouble(o, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
#endif
