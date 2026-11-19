// Evidence for traversal issues at given spots: every collider a vertical ray meets over (x,z), top to bottom, plus the terrain height there.
// SC_TRAVERSAL_POINTS="x,z;x,z;..." and SC_TRAVERSAL_PRESET=Ruined|Rebuilt; run with
//   unity.ps1 exec -Method SleepingCalamity.EditorTools.TraversalPointProbe.Run -OutDir <scratch>
using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class TraversalPointProbe
    {
        public static void Run()
        {
            var preset = Environment.GetEnvironmentVariable("SC_TRAVERSAL_PRESET") == "Rebuilt" ? HollowmereBuilder.Preset.Rebuilt : HollowmereBuilder.Preset.Ruined;
            HollowmereSplit.OpenAll(HollowmereBuilder.ScenePath);
            HollowmereBuilder.ApplyPreset(preset);
            Physics.SyncTransforms();
            string spec = Environment.GetEnvironmentVariable("SC_TRAVERSAL_POINTS") ?? "";
            SleepingCalamity.Core.TownLayout layout = null;
            foreach (string pt in spec.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] xz = pt.Split(',');
                float x = float.Parse(xz[0], System.Globalization.CultureInfo.InvariantCulture), z = float.Parse(xz[1], System.Globalization.CultureInfo.InvariantCulture);
                var sb = new StringBuilder($"SC PPROBE ({x:0.00},{z:0.00}): ");
                RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, 30f, z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore).OrderByDescending(h => h.point.y).ToArray();
                foreach (RaycastHit h in hits) sb.Append($"[y{h.point.y:0.00} {TraversalSources.HierarchyPath(h.collider.transform)}] ");
                var tp = new SleepingCalamity.Core.TownPoint(x, z);
                SleepingCalamity.Core.TownLayout lay = layout ??= SleepingCalamity.Core.HollowmereLayout.Create();
                sb.Append($"| Ground()={SleepingCalamity.Core.HollowmereLayout.Ground(lay, x, z):0.00} natural={SleepingCalamity.Core.HollowmereLayout.NaturalGround(x, z):0.00} lots containing(1m): ");
                foreach (SleepingCalamity.Core.Foundation f in lay.Foundations) if (f.Footprint.Contains(tp, 1.0f)) sb.Append($"{f.Id}(Y{f.Y:0.00}) ");
                sb.Append("lots within 4 m: ");
                foreach (SleepingCalamity.Core.Foundation f in lay.Foundations) if (SleepingCalamity.Core.TownPoint.Distance(f.Centre, tp) < 8f) sb.Append($"{f.Id}(Y{f.Y:0.00} yaw{f.Yaw:0} c{f.Centre.X:0.0},{f.Centre.Z:0.0}) ");
                Debug.Log(sb.ToString());
            }
        }
    }
}
