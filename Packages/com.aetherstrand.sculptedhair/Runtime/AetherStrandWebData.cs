using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AetherStrand
{
    /// <summary>Tiny dependency-free JSON reader (objects -> Dictionary, arrays -> List, numbers -> double). Load-time only.</summary>
    public static class AetherStrandJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            object v = ParseValue(s, ref i);
            return v;
        }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object ParseValue(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++; Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    string key = ParseString(s, ref i);
                    Ws(s, ref i); i++; // :
                    d[key] = ParseValue(s, ref i);
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return d; // }
                }
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++; Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(ParseValue(s, ref i));
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return l; // ]
                }
            }
            if (c == '"') return ParseString(s, ref i);
            if (c == 't') { i += 4; return true; }
            if (c == 'f') { i += 5; return false; }
            if (c == 'n') { i += 4; return null; }
            int st = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
        }

        static string ParseString(string s, ref int i)
        {
            i++; // opening quote
            var sb = new StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char e = s[i];
                    if (e == 'n') sb.Append('\n'); else if (e == 't') sb.Append('\t');
                    else if (e == 'u') { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else sb.Append(e);
                }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }

        public static double D(object o) => o is double d ? d : Convert.ToDouble(o, CultureInfo.InvariantCulture);
        public static bool Has(Dictionary<string, object> d, string k) => d.ContainsKey(k) && d[k] != null;
        public static double D(Dictionary<string, object> d, string k, double def) => Has(d, k) ? D(d[k]) : def;
        public static bool B(Dictionary<string, object> d, string k, bool def) => Has(d, k) && d[k] is bool b ? b : def;
        public static double[] Arr(object o)
        {
            var l = (List<object>)o;
            var r = new double[l.Count];
            for (int i = 0; i < r.Length; i++) r[i] = D(l[i]);
            return r;
        }
    }

    /// <summary>Loads PresetGeometry.json (plugin format, Blender Z-up coordinates) and recipe physics blocks for the web solver.</summary>
    public static class AetherStrandWebData
    {
        /// <summary>
        /// PresetGeometry.json -> solver locks (WEB space, head-pivot-local). Blender (bx,by,bz) = web (x, y, z) -> (bx, bz, -by).
        /// Optional solver extras per lock (written by web_trace.ts): layerOrder, mass, restMidR, rootRawY, stiffnessWeight.
        /// Without them: layerOrder = 0 base / 2 otherwise, mass = width*thickness, restMidR from node 4, stiffnessBias 1.
        /// </summary>
        public static AetherStrandWebSolver.LockData[] LoadGeometry(string json, out string[] zones)
        {
            var root = (Dictionary<string, object>)AetherStrandJson.Parse(json);
            double wsc = AetherStrandJson.D(root, "strandWidthScale", 1.0);
            double safeW = Math.Min(1.45, Math.Max(0.65, wsc == 0 ? 1.0 : wsc));
            double thickScale = 0.65 + 0.35 * safeW;
            var list = (List<object>)root["locks"];
            var res = new AetherStrandWebSolver.LockData[list.Count];
            zones = new string[list.Count];
            for (int s = 0; s < res.Length; s++)
            {
                var d = (Dictionary<string, object>)list[s];
                var pts = (List<object>)d["points"];
                var ld = new AetherStrandWebSolver.LockData();
                for (int k = 0; k < AetherStrandWebSolver.N; k++)
                {
                    double[] p = AetherStrandJson.Arr(pts[k]);
                    ld.rest[k * 3] = p[0]; ld.rest[k * 3 + 1] = p[2]; ld.rest[k * 3 + 2] = -p[1];
                }
                zones[s] = d.ContainsKey("zone") ? (string)d["zone"] : "Sides";
                ld.baseWidth = AetherStrandJson.D(d, "width", 0.03) / safeW;
                ld.baseThickness = AetherStrandJson.D(d, "thickness", 0.01) / thickScale;
                ld.seed = AetherStrandJson.D(d, "seed", 0.5);
                ld.layerAO = AetherStrandJson.D(d, "layerAO", 0.9);
                ld.scalpRoot = AetherStrandJson.D(d, "scalpRoot", 0.65);
                ld.isBaseShell = AetherStrandJson.B(d, "isBaseShell", false);
                ld.lockNormal = AetherStrandJson.B(d, "lockNormal", false);
                ld.isFadeCap = AetherStrandJson.B(d, "isFadeCap", false);
                ld.fadeStart = AetherStrandJson.D(d, "fadeStart", double.NaN);
                ld.smooth = System.Math.Max(1, System.Math.Min(4, (int)System.Math.Round(AetherStrandJson.D(d, "smooth", 1.0))));
                ld.roundness = System.Math.Max(0.0, System.Math.Min(1.0, AetherStrandJson.D(d, "roundness", 0.0)));
                if (AetherStrandJson.Has(d, "rootNormal"))
                {
                    double[] rn = AetherStrandJson.Arr(d["rootNormal"]);
                    ld.rootNormal = new[] { rn[0], rn[2], -rn[1] };
                }
                if (AetherStrandJson.Has(d, "layerOrder")) ld.layerOrder = (int)AetherStrandJson.D(d["layerOrder"]);
                if (AetherStrandJson.Has(d, "mass")) ld.mass = AetherStrandJson.D(d["mass"]);
                if (AetherStrandJson.Has(d, "restMidR")) ld.restMidR = AetherStrandJson.D(d["restMidR"]);
                if (AetherStrandJson.Has(d, "rootRawY")) ld.rootRawY = AetherStrandJson.D(d["rootRawY"]);
                if (AetherStrandJson.Has(d, "stiffnessWeight")) ld.stiffnessWeight = AetherStrandJson.Arr(d["stiffnessWeight"]);
                res[s] = ld;
            }
            return res;
        }

        /// <summary>Reads the "physics" block of a recipe.json (PhysicsConfig).</summary>
        public static AetherStrandWebSolver.Config LoadConfig(object physicsBlock)
        {
            var d = (Dictionary<string, object>)physicsBlock;
            var c = new AetherStrandWebSolver.Config();
            c.stiffness = AetherStrandJson.D(d, "stiffness", c.stiffness);
            c.gravity = AetherStrandJson.D(d, "gravity", c.gravity);
            c.windStrength = AetherStrandJson.D(d, "windStrength", c.windStrength);
            c.windTurbulence = AetherStrandJson.D(d, "windTurbulence", c.windTurbulence);
            c.windAzimuth = AetherStrandJson.D(d, "windAzimuth", c.windAzimuth);
            c.damping = AetherStrandJson.D(d, "damping", c.damping);
            c.inertia = AetherStrandJson.D(d, "inertia", c.inertia);
            c.bendingSmoothness = AetherStrandJson.D(d, "bendingSmoothness", c.bendingSmoothness);
            c.collisionMarginMm = AetherStrandJson.D(d, "collisionMarginMm", c.collisionMarginMm);
            c.rootAnchor = AetherStrandJson.D(d, "rootAnchor", c.rootAnchor);
            c.solverSubsteps = (int)AetherStrandJson.D(d, "solverSubsteps", c.solverSubsteps);
            c.strandWidthScale = AetherStrandJson.D(d, "strandWidthScale", c.strandWidthScale);
            c.collisionEnabled = AetherStrandJson.B(d, "collisionEnabled", c.collisionEnabled);
            return c;
        }
    }
}
