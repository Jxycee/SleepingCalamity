// T23 ruin-wall re-skin: puts the scanned wall sets (Content/Textures/T23_walls, Megascans "Mossy Stone Wall" via Fab) and WearLit's _SC_RUINSKIN mode on the ruin shells, the town wall,
// the lane kit and the square's cobbles. SC_SHELL_RESKIN_OFF=1 builds the old materials instead (the same-camera on/off pairs).
// Per-material tuning rides on properties the URP Lit cbuffer already has (see SC_WearForwardPass.hlsl): _DetailAlbedoMapScale = variation, _DetailNormalMapScale = second-scale normal,
// _SpecColor = (stone width m, moss amount, foot band m, foot darkness).
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static class T23Walls
    {
        public static bool Off => System.Environment.GetEnvironmentVariable("SC_SHELL_RESKIN_OFF") == "1";
        const string Root = "Assets/_Project/Content/Textures/T23_walls/";

        public struct Look
        {
            public string Set;            // "ashlar", "rubble" or null (keep the material's own textures: plaster, oak)
            public float Tile;            // texture repeats per metre
            public float Bump;
            public float StoneW, Moss, Foot, FootDark;
            public float Variation, DetailN, Parallax;
        }

        static Texture2D Tex(string set, string suffix) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + set + "/" + set + suffix + ".png");

        // Resets what Skin adds, so an Off build (or a rebuild) never keeps an earlier run's state on the shared .mat.
        public static void Clear(Material m)
        {
            m.DisableKeyword("_SC_RUINSKIN"); m.DisableKeyword("_PARALLAXMAP");
            m.SetVector("_SpecColor", new Vector4(0.19999996f, 0.19999996f, 0.19999996f, 1f));
            m.SetFloat("_DetailAlbedoMapScale", 1f); m.SetFloat("_DetailNormalMapScale", 1f);
        }

        // Returns false (material untouched except Clear) when the switch is off or the set is missing.
        public static bool Skin(Material m, Look l)
        {
            Clear(m);
            if (Off) return false;
            if (l.Set != null)
            {
                Texture2D alb = Tex(l.Set, "_albedo"), nrm = Tex(l.Set, "_normal"), ms = Tex(l.Set, "_ms"), occ = Tex(l.Set, "_occ"), hgt = Tex(l.Set, "_height");
                if (alb == null || nrm == null || ms == null || occ == null || hgt == null) { Debug.LogWarning("SC T23 wall set missing: " + l.Set); return false; }
                m.SetTexture("_BaseMap", alb); m.SetTexture("_MainTex", alb);
                m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", l.Bump); m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_MetallicGlossMap", ms); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_OcclusionMap", occ); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP");
                m.SetTexture("_ParallaxMap", hgt); m.SetFloat("_Parallax", Mathf.Clamp(l.Parallax, 0.005f, 0.08f)); m.EnableKeyword("_PARALLAXMAP");
                foreach (string p in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_ParallaxMap" }) { m.SetTextureScale(p, Vector2.one * l.Tile); m.SetTextureOffset(p, Vector2.zero); }
                m.SetTextureScale("_MainTex", Vector2.one * l.Tile);
            }
            if (l.Set != null)
            {
                float gain = l.Set == "ashlar" ? 1.9f : 2.3f;   // the scans are darker than the B1 sandstone (mean linear luminance 0.10 / 0.19 against 0.37): the tint brings them to about 0.19 / 0.43
                Color c = m.GetColor("_BaseColor").linear;
                m.SetColor("_BaseColor", new Color(Mathf.LinearToGammaSpace(c.r * gain), Mathf.LinearToGammaSpace(c.g * gain), Mathf.LinearToGammaSpace(c.b * gain), 1f));
                m.SetColor("_Color", m.GetColor("_BaseColor"));
            }
            m.SetFloat("_DetailAlbedoMapScale", l.Variation); m.SetFloat("_DetailNormalMapScale", l.DetailN);
            m.SetVector("_SpecColor", new Vector4(l.StoneW, l.Moss, l.Foot, l.FootDark));
            m.EnableKeyword("_SC_RUINSKIN");
            return true;
        }
    }
}
