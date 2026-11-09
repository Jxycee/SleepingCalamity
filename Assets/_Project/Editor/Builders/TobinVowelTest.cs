// Capture-only (gate 2d): offline uLipSync analysis of spoken vowel WAVs (A I U E O, one file each, 16-bit mono PCM), no audio device needed.
// Same loop as uLipSync's own baker (60 analysis frames/s); each frame gives phoneme ratios and a normalised volume.
//   unity.ps1 exec -Method SleepingCalamity.EditorTools.TobinVowelTest.Run -OutDir <scratch>   Env: SC_VOWEL_DIR (WAVs, required), SC_VOWEL_OUT (result text file, required).
// Shape weight = phoneme ratio x normalised volume (what a driver would send to the blendshape), A=aa, I=ih, U=ou, E=E, O=oh.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class TobinVowelTest
    {
        static readonly Dictionary<string, string> Shape = new Dictionary<string, string> { { "A", "aa" }, { "I", "ih" }, { "U", "ou" }, { "E", "E" }, { "O", "oh" } };
        static readonly string[] Profiles =
        {
            "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample-Male.asset",
            "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample-Female.asset",
            "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample.asset",
        };

        public static void Run()
        {
            string dir = System.Environment.GetEnvironmentVariable("SC_VOWEL_DIR");
            string outFile = System.Environment.GetEnvironmentVariable("SC_VOWEL_OUT");
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(outFile)) throw new System.InvalidOperationException("Set SC_VOWEL_DIR and SC_VOWEL_OUT");
            var sb = new StringBuilder();
            foreach (var pp in Profiles)
            {
                var profile = AssetDatabase.LoadAssetAtPath<uLipSync.Profile>(pp);
                if (profile == null) { sb.AppendLine("MISSING PROFILE " + pp); continue; }
                sb.AppendLine("PROFILE " + Path.GetFileNameWithoutExtension(pp) + " phonemes " + string.Join(",", profile.GetPhonemeNames()));
                foreach (var v in new[] { "A", "I", "U", "E", "O" })
                {
                    float[] pcm = ReadWav(Path.Combine(dir, v + ".wav"), out int rate);
                    sb.AppendLine(Analyse(profile, v, pcm, rate));
                }
            }
            File.WriteAllText(outFile, sb.ToString());
            Debug.Log("SC VOWELS\n" + sb);
        }

        static string Analyse(uLipSync.Profile profile, string vowel, float[] pcm, int rate)
        {
            var go = new GameObject("vowel-bake");
            var ls = go.AddComponent<uLipSync.uLipSync>();
            ls.OnBakeStart(profile);
            int per = rate / 60;
            var buf = new float[per];
            var wins = new Dictionary<string, int>();
            double sumRatioTarget = 0, sumWeight = 0, peakWeight = 0; int voiced = 0;
            for (int off = 0; off + per < pcm.Length; off += per)
            {
                System.Array.Copy(pcm, off, buf, 0, per);
                ls.OnBakeUpdate(buf, 1);
                var r = ls.result;
                var frame = new uLipSync.BakedFrame { volume = r.rawVolume, phonemes = r.phonemeRatios.Select(kv => new uLipSync.BakedPhonemeRatio { phoneme = kv.Key, ratio = kv.Value }).ToList() };
                var info = uLipSync.BakedData.GetLipSyncInfo(frame);
                if (info.volume < 0.3f || info.phoneme == null) continue;   // silence / tails
                voiced++;
                wins[info.phoneme] = wins.TryGetValue(info.phoneme, out int c) ? c + 1 : 1;
                info.phonemeRatios.TryGetValue(vowel, out float ratio);
                double w = ratio * info.volume;
                sumRatioTarget += ratio; sumWeight += w; peakWeight = System.Math.Max(peakWeight, w);
            }
            ls.OnBakeEnd();
            Object.DestroyImmediate(go);
            string win = wins.Count == 0 ? "(none)" : wins.OrderByDescending(k => k.Value).First().Key;
            string hist = string.Join(" ", wins.OrderByDescending(k => k.Value).Select(k => k.Key + ":" + k.Value));
            return string.Format("  {0} target={1}: voiced frames {2}, winning phoneme {3} [{4}], mean ratio of {0} {5:F2}, shape {1} weight mean {6:F2} peak {7:F2}",
                vowel, Shape[vowel], voiced, win, hist, voiced > 0 ? sumRatioTarget / voiced : 0, voiced > 0 ? sumWeight / voiced : 0, peakWeight);
        }

        static float[] ReadWav(string path, out int rate)
        {
            var b = File.ReadAllBytes(path);
            int p = 12; rate = 16000; int ch = 1, bits = 16;
            while (p + 8 <= b.Length)
            {
                string id = Encoding.ASCII.GetString(b, p, 4); int len = System.BitConverter.ToInt32(b, p + 4);
                if (id == "fmt ") { ch = System.BitConverter.ToInt16(b, p + 10); rate = System.BitConverter.ToInt32(b, p + 12); bits = System.BitConverter.ToInt16(b, p + 22); }
                else if (id == "data")
                {
                    if (bits != 16) throw new System.NotSupportedException("16-bit only");
                    int n = Mathf.Min(len, b.Length - p - 8) / 2 / ch;
                    var f = new float[n];
                    for (int i = 0; i < n; i++) f[i] = System.BitConverter.ToInt16(b, p + 8 + i * 2 * ch) / 32768f;
                    return f;
                }
                p += 8 + len + (len & 1);
            }
            throw new InvalidDataException("no data chunk in " + path);
        }
    }
}
