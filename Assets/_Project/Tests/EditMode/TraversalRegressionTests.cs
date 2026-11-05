// Traversal regression: reachability and intended-path fit on Hollowmere (Ruined and Rebuilt) and Cellars floors 1-4 for 10 fixed seeds, against the hero's
// real capsule (TraversalScan.RegressionRun). Fails when a target that was reachable no longer is, or when a space gets more blocked or narrow spots than the
// baseline (Reference/qa/traversal/baseline.json, written by TraversalScan.WriteBaseline). Fixing issues lowers the counts; rewrite the baseline then.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.EditorTools;
using UnityEditor.SceneManagement;

namespace SleepingCalamity.Tests.EditMode
{
    [Category("Traversal"), Timeout(1200000)]
    public class TraversalRegressionTests
    {
        static List<TraversalScan.QuickEntry> s_run;

        [OneTimeSetUp]
        public void Run()
        {
            s_run = TraversalScan.RegressionRun();
        }

        [OneTimeTearDown]
        public void Close()
        {
            UnityEngine.AI.NavMesh.RemoveAllNavMeshData();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void HeroCapsule_ComesFromThePrefab()
        {
            HeroCapsule h = HeroCapsule.FromPrefab();
            Assert.Greater(h.Radius, 0.1f); Assert.Greater(h.Height, 1f); Assert.Greater(h.StepOffset, 0f); Assert.Greater(h.SlopeLimit, 0f);
        }

        [Test]
        public void RegressionSet_CoversTownAndTenSeeds()
        {
            Assert.AreEqual(2 + TraversalScan.RegressionSeeds.Length * DiveFloorTable.LastBuiltFloor, s_run.Count);
        }

        [Test]
        public void NoNewUnreachableTargets_AndNoMoreBlockedOrNarrowSpots()
        {
            TraversalScan.Baseline b = TraversalScan.ReadBaseline();
            Assert.IsNotNull(b, "no baseline at " + TraversalScan.BaselineFile + ": run TraversalScan.WriteBaseline");
            var known = b.entries.ToDictionary(e => e.space);
            var failures = new List<string>();
            foreach (TraversalScan.QuickEntry e in s_run)
            {
                if (!known.TryGetValue(e.Space, out TraversalScan.BaselineEntry k)) { failures.Add(e.Space + ": not in the baseline"); continue; }
                var allowed = new HashSet<string>(k.unreachable ?? new string[0]);
                foreach (string u in e.Unreachable.Where(u => !allowed.Contains(u))) failures.Add($"{e.Space}: '{u}' became unreachable");
                if (e.Blocked > k.blocked) failures.Add($"{e.Space}: {e.Blocked} blocked spots on intended paths (baseline {k.blocked}): " + string.Join("; ", e.Spots.Where(s => s.StartsWith("Blocked")).Take(5)));
                if (e.Narrow > k.narrow) failures.Add($"{e.Space}: {e.Narrow} narrow spots on intended paths (baseline {k.narrow}): " + string.Join("; ", e.Spots.Where(s => s.StartsWith("Narrow")).Take(5)));
            }
            Assert.IsEmpty(failures, string.Join("\n", failures) + "\n\nA failure here means: run the traversal scan (TraversalScan.RunAll) and look at REPORT.md. If the change is intended or fixes issues elsewhere, "
                + "rewrite the baseline with SleepingCalamity.EditorTools.TraversalScan.WriteBaseline (unity.sh exec) and commit " + TraversalScan.BaselineFile + ".");
        }
    }
}
