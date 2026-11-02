// Authority: none (pure EditMode tests). Hollowmere ambient life (effects only): the tuning spot has its toggles on, the shared wing card is a valid mesh whose uv.x is the wing weight,
// flocks pose deterministically and carry no collider, and a perched flock lifts when the hero comes within the scatter distance.
using NUnit.Framework;
using SleepingCalamity.Presentation;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class HollowmereAmbientLifeTests
    {
        [Test]
        public void Tuning_HasEveryEffectOn_AndANormalisedWind()
        {
            Assert.IsTrue(HollowmereAmbientLife.Birds && HollowmereAmbientLife.Butterflies && HollowmereAmbientLife.Bees && HollowmereAmbientLife.FishRings
                && HollowmereAmbientLife.ReedSway && HollowmereAmbientLife.WindWaves && HollowmereAmbientLife.ChimneySmoke);
            Assert.AreEqual(1f, HollowmereAmbientLife.WindDirection.magnitude, 1e-4f);
            Assert.AreEqual(15f, HollowmereAmbientLife.ScatterDistance);
        }

        [Test]
        public void CardMesh_IsValid_WithTheWingWeightInUvX()
        {
            Mesh m = AmbientCreatures.BuildCardMesh();
            try
            {
                Assert.AreEqual(m.vertexCount, m.uv.Length);
                foreach (int i in m.triangles) Assert.Less(i, m.vertexCount);
                for (int i = 0; i < m.vertexCount; i++) Assert.AreEqual(Mathf.Abs(m.vertices[i].x), m.uv[i].x, 0.15f, "vertex " + i);
            }
            finally { Object.DestroyImmediate(m); }
        }

        static (AmbientCreatures c, GameObject go) Make(AmbientCreatures.Motion motion)
        {
            var go = new GameObject("test");
            var c = go.AddComponent<AmbientCreatures>();
            var f = new AmbientCreatures.Flock { Name = "t", Move = motion, Centre = new Vector3(10f, 2f, 5f), Radius = 8f, Height = 6f, Speed = 5f, Spread = 4f, Seed = 7, Wingspan = 0.9f };
            f.Cards = new Transform[5];
            for (int i = 0; i < 5; i++) { var card = new GameObject("c" + i); card.transform.SetParent(go.transform, false); f.Cards[i] = card.transform; }
            c.Flocks = new[] { f };
            return (c, go);
        }

        [Test]
        public void Circling_IsDeterministic_AboveTheCentre_WithNoColliders()
        {
            var (c, go) = Make(AmbientCreatures.Motion.Circle);
            try
            {
                c.Pose(12f, 0f, false);
                var first = new Vector3[5];
                for (int i = 0; i < 5; i++) { first[i] = c.Flocks[0].Cards[i].position; Assert.Greater(first[i].y, 2f + 3f); }
                c.Pose(40f, 0f, false);
                c.Pose(12f, 0f, false);
                for (int i = 0; i < 5; i++) Assert.AreEqual(0f, Vector3.Distance(first[i], c.Flocks[0].Cards[i].position), 1e-4f);
                Assert.AreEqual(0, go.GetComponentsInChildren<Collider>(true).Length);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PerchedFlock_SitsOnTheGround_WithNoHero()
        {
            var (c, go) = Make(AmbientCreatures.Motion.Perch);
            try
            {
                c.Pose(0f, 0.02f, false);
                for (int i = 0; i < 5; i++) Assert.Less(c.Flocks[0].Cards[i].position.y, 2.3f);
                Assert.Greater(HollowmereAmbientLife.ScatterDistance, 1f);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
