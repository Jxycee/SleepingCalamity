// Authority: none (EditMode). Where each roster slot stands around the spawn point.
using NUnit.Framework;
using SleepingCalamity.Gameplay;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class SpawnPointTests
    {
        // Catches: two players sharing a spot, slot 0 standing off the point, or a slot standing off the circle.
        [Test]
        public void FourSlots_StandOnDistinctSpots_AroundThePoint()
        {
            var go = new GameObject("SpawnPointTest");
            try
            {
                go.transform.position = new Vector3(10f, 0f, 5f);
                var point = go.AddComponent<SpawnPoint>();
                Assert.That(point.PositionForSlot(0), Is.EqualTo(go.transform.position));
                for (int a = 0; a < 4; a++)
                {
                    for (int b = a + 1; b < 4; b++)
                        Assert.That(Vector3.Distance(point.PositionForSlot(a), point.PositionForSlot(b)), Is.GreaterThan(1f), "slots " + a + " and " + b);
                    if (a > 0) Assert.That(Vector3.Distance(point.PositionForSlot(a), go.transform.position), Is.EqualTo(SpawnPoint.SlotRadius).Within(0.001f));
                }
                Assert.That(point.PositionForSlot(-3), Is.EqualTo(go.transform.position), "a bad slot stands on the point");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
