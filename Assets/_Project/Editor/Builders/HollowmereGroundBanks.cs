// T13a rule 3 (earth banks are broken up): the keep terrace's flanking banks and the crag face behind the bell yard and the churchyard get outcrops of the T20 scanned rocks in
// clusters of two or three (a big one, a small one, a skirt of scree), ferns and grass at their feet on the downslope side, fern-and-grass bands across the face on three contours,
// and roots lying down the fall line. The earth between them is broken by the terrain paint (HollowmereTerrain: gullies down the fall line, grass bands). A partial of
// HollowmereOrchard because it shares the bell yard's placement helpers (PutSlopeDim, PutNat, WeedT, OkBank, TY); called from HollowmereBuilder after BuildRoadside, before Combine.
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static partial class HollowmereOrchard
    {
        public static void BuildBanksT13(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            T = t; counts.Clear();
            Physics.SyncTransforms();
            // T22 (controller, 2026-11-18): the soil shelves are off. The T13 milestone judges rejected them (blocky dark slab risers, green strip blocks slicing into the bank); flip BankStepsOn to bring them back.
            int stepN = BankStepsOn ? BuildBankSteps(set, t) : 0;
            Transform root = Sub(Sub(ruined, "BellYard"), "T13Banks");
            Transform props = Sub(root, "Props"), flora = Sub(root, "Flora");
            string[] rocks = { "T20a", "T20b", "T20c", "T20d", "T20e", "T20f", "T20g", "T20h" };
            string[] plants = { "T21l", "T12b", "T21e", "T21f", "T21j" };
            int rockN = 0, plantN = 0, rootN = 0, bandN = 0;
            // outcrop clusters: (centre x, z, spread) per bank / contour; each cluster = one big rock, one or two mediums, scree, a fern ring on the downslope side
            var sites = new System.Collections.Generic.List<(Vector2 c, float down)>();   // down = yaw of the downslope direction
            foreach (int side in new[] { -1, 1 })
                for (int i = 0; i < 9; i++)
                    sites.Add((new Vector2(side * Mathf.Lerp(23.6f, 30.6f, H(i, 1, 1700 + side)), Mathf.Lerp(20.4f, 31.4f, (i + H(i, 2, 1700 + side)) / 9f)), side > 0 ? 90f : 270f));
            for (int i = 0; i < 30; i++)
                sites.Add((new Vector2(Mathf.Lerp(-27f, 27f, (i + H(i, 3, 1710)) / 30f), Mathf.Lerp(34.6f, 44f, H(i, 4, 1710))), 180f));
            int si = 0;
            foreach (var (c, down) in sites)
            {
                float yaw0 = H(si, 5, 1720) * 360f;
                Vector2 pBig = c;
                if (OkBank(pBig, 0.9f) && PutSlopeDim(props, rocks[(int)(H(si, 6, 1720) * rocks.Length) % rocks.Length], "T13Outcrop_" + si, pBig, Mathf.Lerp(2.4f, 4.4f, H(si, 7, 1720)), yaw0, (H(si, 8, 1720) - 0.5f) * 10f, (H(si, 9, 1720) - 0.5f) * 10f, Mathf.Lerp(0.35f, 0.5f, H(si, 10, 1720))) != null) rockN++;
                for (int k = 0; k < 2; k++)
                {
                    float a = (yaw0 + 140f * k + 60f * H(si, 11 + k, 1720)) * Mathf.Deg2Rad;
                    Vector2 q = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Lerp(1.0f, 1.7f, H(si, 13 + k, 1720));
                    if (OkBank(q, 0.5f) && PutSlopeDim(props, rocks[(int)(H(si, 15 + k, 1720) * rocks.Length) % rocks.Length], "T13OutcropB_" + si + "_" + k, q, Mathf.Lerp(1.0f, 1.9f, H(si, 17 + k, 1720)), H(si, 19 + k, 1720) * 360f, 0f, 0f, 0.4f) != null) rockN++;
                }
                for (int k = 0; k < 5; k++)
                {
                    float a = (down - 90f + 36f * (k - 2) + (H(si, 21 + k, 1720) - 0.5f) * 30f) * Mathf.Deg2Rad;   // the downslope half of the rock
                    Vector2 q = c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * Mathf.Lerp(1.2f, 2.2f, H(si, 26 + k, 1720));
                    if (!OkBank(q, 0.1f) || k == 1 || k == 3 || k == 4) continue;
                    if (k == 2) PutNat(flora, plants[(int)(H(si, 31 + k, 1720) * plants.Length) % plants.Length], "T13RockFern_" + si + "_" + k, q, TY(q), Mathf.Lerp(0.8f, 1.3f, H(si, 36 + k, 1720)), H(si, 41 + k, 1720) * 360f, 0f, 0f, 0.04f);
                    else WeedT(flora, q, 1730, si * 8 + k, 1.2f, 2.2f);
                    plantN++;
                }
                si++;
            }
            // fern-and-grass bands across the face on contours (broken by hashed gaps), so the earth never reads as one ramp
            for (int band = 0; band < 5; band++)
            {
                float z0 = 35.2f + band * 3.4f;
                for (float x = -26f; x < 26f; x += 2.2f)
                {
                    float hx = H((int)(x * 10f), band, 1740);
                    if (hx < 0.35f) continue;
                    Vector2 q = new Vector2(x + (hx - 0.5f), z0 + (H((int)(x * 10f), band, 1741) - 0.5f) * 1.6f);
                    if (!OkBank(q, 0.1f)) continue;
                    if (hx > 0.94f) PutNat(flora, plants[(int)(hx * 50f) % plants.Length], "T13BandFern_" + band + "_" + (int)(x * 10f), q, TY(q), Mathf.Lerp(0.9f, 1.4f, H((int)(x * 10f), band, 1742)), hx * 3600f, 0f, 0f, 0.04f);
                    else WeedT(flora, q, 1743, band * 100 + (int)(x + 30f), 1.4f, 2.6f);
                    bandN++;
                }
            }
            // roots and fallen branches lying down the fall line on the crag, and across the keep banks
            for (int i = 0; i < 12; i++)
            {
                Vector2 p = i < 8 ? new Vector2(Mathf.Lerp(-25f, 25f, (i + H(i, 50, 1750)) / 8f), Mathf.Lerp(35f, 43f, H(i, 51, 1750))) : new Vector2(((i & 1) == 0 ? -1 : 1) * Mathf.Lerp(24f, 30f, H(i, 52, 1750)), Mathf.Lerp(21f, 30f, H(i, 53, 1750)));
                if (OkBank(p, 0.5f) && PutNat(props, i % 3 == 0 ? "T20p" : i % 3 == 1 ? "T20o" : "T20q", "T13Root_" + i, p, TY(p), Mathf.Lerp(0.9f, 1.3f, H(i, 54, 1750)), i < 8 ? 90f + (H(i, 55, 1750) - 0.5f) * 40f : 0f + (H(i, 55, 1750) - 0.5f) * 40f, 0f, 0f, 0.12f) != null) rootN++;
            }
            Debug.Log("SC HOLLOWMERE banks (T13a/b): soil steps " + stepN + ", outcrop rocks " + rockN + ", plants at the rocks " + plantN + ", band plants " + bandN + ", roots " + rootN);
        }
    }
}
