// Authority: none (EditMode). Ground-blend step: the CPU ground bake is deterministic and decodes to the terrain's heights, and the placement helper sinks a prop into a flat host.
using NUnit.Framework;
using SleepingCalamity.EditorTools;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class GroundBlendTests
    {
        GameObject terrainGo;

        [TearDown]
        public void TearDown()
        {
            if (terrainGo != null) Object.DestroyImmediate(terrainGo);
        }

        Terrain MakeTerrain(float height01)
        {
            var data = new TerrainData { heightmapResolution = 33 };
            data.size = new Vector3(40f, 20f, 40f);
            var h = new float[33, 33];
            for (int j = 0; j < 33; j++) for (int i = 0; i < 33; i++) h[j, i] = height01;
            data.SetHeights(0, 0, h);
            terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.transform.position = new Vector3(-20f, 1f, -20f);
            return terrainGo.GetComponent<Terrain>();
        }

        [Test]
        public void Compute_TwiceOnTheSameTerrain_GivesIdenticalBytes_AndDecodesTheHeight()
        {
            Terrain t = MakeTerrain(0.25f);   // 5 m above the terrain origin, origin y 1 => world y 6
            SCGroundBaker.Result a = SCGroundBaker.Compute(t, null, new Vector2(-10f, -10f), new Vector2(20f, 20f), 16);
            SCGroundBaker.Result b = SCGroundBaker.Compute(t, null, new Vector2(-10f, -10f), new Vector2(20f, 20f), 16);
            Assert.That(b.HeightBytes, Is.EqualTo(a.HeightBytes));
            Assert.That(b.AlbedoBytes, Is.EqualTo(a.AlbedoBytes));
            Assert.That(a.HeightBytes.Length, Is.EqualTo(16 * 16 * 2));
            Assert.That(a.AlbedoBytes.Length, Is.EqualTo(16 * 16 * 4));
            Assert.That(a.GroundHeightParams.x, Is.EqualTo(6f).Within(0.01f), "minY of a flat terrain is its height");
            Assert.That(a.GroundBounds.z, Is.EqualTo(1f / 20f).Within(1e-6f));
        }

        [Test]
        public void Compute_StreetColliderAboveTerrain_RaisesTheHeightAndCountsTexels()
        {
            Terrain t = MakeTerrain(0.25f);
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                slab.transform.position = new Vector3(0f, 6.5f, 0f); slab.transform.localScale = new Vector3(4f, 1f, 4f);   // top at y 7
                Physics.SyncTransforms();
                SCGroundBaker.Result r = SCGroundBaker.Compute(t, new Collider[] { slab.GetComponent<Collider>() }, new Vector2(-10f, -10f), new Vector2(20f, 20f), 16);
                Assert.That(r.StreetTexels, Is.GreaterThan(0));
                Assert.That(r.GroundHeightParams.x + r.GroundHeightParams.y, Is.EqualTo(7f).Within(0.01f), "maxY is the slab top");
            }
            finally { Object.DestroyImmediate(slab); }
        }

        [Test]
        public void TryGroundAndSink_FloatingBlockOnFlatTerrain_ReachesTheSinkDepth_AndNoSupportFails()
        {
            Terrain t = MakeTerrain(0.25f);   // ground y 6
            Physics.SyncTransforms();
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                block.transform.localScale = new Vector3(1f, 1f, 1f);
                block.transform.position = new Vector3(0f, 9f, 0f);   // hovering
                Assert.That(SCPlacementGrounding.TryGroundAndSink(block.transform, null, 0.2f, 45f, false, 4), Is.True);
                Assert.That(block.GetComponent<Renderer>().bounds.min.y, Is.EqualTo(6f - 0.2f).Within(0.02f), "bottom sits 20% of the height under the ground");

                block.transform.position = new Vector3(500f, 9f, 500f);   // off the terrain: no support
                Vector3 before = block.transform.position;
                Assert.That(SCPlacementGrounding.TryGroundAndSink(block.transform), Is.False);
                Assert.That(block.transform.position, Is.EqualTo(before), "a failed grounding leaves the target alone");
            }
            finally { Object.DestroyImmediate(block); }
        }
    }
}
