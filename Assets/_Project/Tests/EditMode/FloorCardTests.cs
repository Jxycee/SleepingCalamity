// Authority: none (pure EditMode tests). Plan 5 task 2: the floor card's name, objective lines and modifier text (FloorCard, FloorCardNames, FloorCardText).
// Catches: an authored floor with the wrong name, a repeated name within a dive, an objective line that changed, a modifier with no title / effect, a hazard with no printed reward.
// Does not catch: how the HUD lays the card out (Task 6).
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class FloorCardTests
    {
        [Test]
        public void AuthoredFloors_FixedNames()
        {
            for (uint s = 1; s <= 50; s++)
            {
                Assert.AreEqual("The Undercroft Steps", FloorCard.For(s, 1, FloorObjective.GateKey).Name);
                Assert.AreEqual("The Drowned Bakery", FloorCard.For(s, 3, FloorObjective.Rescue).Name);
                Assert.AreEqual("The Cistern Dome", FloorCard.For(s, 5, FloorObjective.Guardian).Name);
            }
        }

        private static void NoRepeatInDive(uint seeds)
        {
            var all = new HashSet<string>();
            for (uint s = 1; s <= seeds; s++)
            {
                var seen = new HashSet<string>();
                foreach (int f in new[] { 2, 4, 6, 7 })
                {
                    FloorCard c = FloorCard.For(s, f, FloorObjectivePick.Rolled(s, f));
                    Assert.IsTrue(seen.Add(c.Name), "seed " + s + " floor " + f + " repeats " + c.Name);
                    Assert.AreEqual(f, c.Floor);
                    Assert.AreEqual(FloorCardNames.NameFor(s, f, CellarFloorGenerator.Layout(s, f).Theme), c.Name);
                    all.Add(c.Name);
                }
            }
            Assert.GreaterOrEqual(all.Count, 15, "the names of every theme appear");
        }

        // Fast tier: 200 seeds. The 2000-seed sweep is Soak (1000+ seed sweeps run with `unity.ps1 soak`; it timed out the fast tier under machine load).
        [Test] public void GeneratedNames_NoRepeatInDive() => NoRepeatInDive(200);
        [Test, Category("Soak"), Timeout(600000)] public void GeneratedNames_NoRepeatInDive_Soak() => NoRepeatInDive(2000);

        [Test]
        public void ThemeTables_AreFiveNamesEach()
        {
            foreach (SubTheme t in new[] { SubTheme.Larder, SubTheme.WineVault, SubTheme.CoalCellar, SubTheme.Bakery, SubTheme.Any })
            {
                var names = new HashSet<string>();
                for (uint s = 1; s <= 400; s++) names.Add(FloorCardNames.NameFor(s, 2, t));
                Assert.AreEqual(5, names.Count, t.ToString());
            }
        }

        [Test]
        public void ObjectiveLines()
        {
            Assert.AreEqual("Find the gate key", FloorCardText.ObjectiveLine(FloorObjective.GateKey, 0, 0f, false, 0));
            Assert.AreEqual("Find the gate key", FloorCardText.ObjectiveLine(FloorObjective.GateKey, 0, 0f, false, 1));
            Assert.AreEqual("Gate key found: the way down is open", FloorCardText.ObjectiveLine(FloorObjective.GateKey, 0, 0f, false, 2));
            Assert.AreEqual("", FloorCardText.ObjectiveLine(FloorObjective.GateKey, 0, 0f, false, 0, true), "a key floor open at arm time (no carrier) has no key to find");
            Assert.AreEqual("Find the gate key", FloorCardText.ObjectiveLine(FloorObjective.GateKey, 0, 0f, false, 1, false));
            Assert.AreEqual("Open both valves (0/2)", FloorCardText.ObjectiveLine(FloorObjective.TwoValves, 0, 0f, false, 0));
            Assert.AreEqual("Open both valves (1/2)", FloorCardText.ObjectiveLine(FloorObjective.TwoValves, 1, 0f, false, 0));
            Assert.AreEqual("Start the pump", FloorCardText.ObjectiveLine(FloorObjective.HoldPump, 0, 0f, false, 0));
            Assert.AreEqual("Start the pump and hold it (0%)", FloorCardText.ObjectiveLine(FloorObjective.HoldPump, 0, 0f, true, 0));
            Assert.AreEqual("Start the pump and hold it (40%)", FloorCardText.ObjectiveLine(FloorObjective.HoldPump, 0, 0.4f, true, 0));
            Assert.AreEqual("Start the pump and hold it (100%)", FloorCardText.ObjectiveLine(FloorObjective.HoldPump, 0, 1.5f, true, 0));
            Assert.AreEqual("Wake the baker", FloorCardText.ObjectiveLine(FloorObjective.Rescue, 0, 0f, false, 0));
            Assert.AreEqual("Beat the guardian, then reach the bank", FloorCardText.ObjectiveLine(FloorObjective.Guardian, 0, 0f, false, 0));
            Assert.AreEqual("", FloorCardText.ObjectiveLine(FloorObjective.None, 0, 0f, false, 0));
            Assert.AreEqual("Open both valves (1/2) to open the way out", FloorCardText.WithWayOut(FloorCardText.ObjectiveLine(FloorObjective.TwoValves, 1, 0f, false, 0)));
        }

        [Test]
        public void EveryModifier_HasTitleEffect_AndHazardsHaveReward()
        {
            foreach (FloorModifier m in new[] { FloorModifier.Crowded, FloorModifier.Ambush, FloorModifier.Stirred, FloorModifier.Hurried, FloorModifier.Dim, FloorModifier.Brimming, FloorModifier.FatLarder })
            {
                Assert.IsNotEmpty(FloorCardText.Title(m), m.ToString()); Assert.IsNotEmpty(FloorCardText.EffectLine(m), m.ToString());
                StringAssert.StartsWith("Reward:", FloorCardText.RewardLine(m), m.ToString());
            }
            foreach (FloorModifier m in new[] { FloorModifier.Sparse, FloorModifier.WarmHands, FloorModifier.Hushed, FloorModifier.StillAir })
            {
                Assert.IsNotEmpty(FloorCardText.Title(m), m.ToString()); Assert.IsNotEmpty(FloorCardText.EffectLine(m), m.ToString());
                Assert.AreEqual("", FloorCardText.RewardLine(m), m.ToString());
            }
            Assert.AreEqual("", FloorCardText.Title(FloorModifier.None));
            Assert.AreEqual("Reward: +15 gold per room cleared, each", FloorCardText.RewardLine(FloorModifier.Crowded));
            Assert.AreEqual("The floor is dark", FloorCardText.EffectLine(FloorModifier.Dim));
            Assert.AreEqual("The hunter stays away this floor", FloorCardText.EffectLine(FloorModifier.Hushed));
        }

        [Test]
        public void Card_CarriesModifiersAndObjective()
        {
            FloorCard c = FloorCard.For(12345, 7, FloorObjective.HoldPump);
            Assert.AreEqual(FloorObjective.HoldPump, c.Objective);
            Assert.AreEqual(FloorModifierSet.For(12345, 7).Hash(), c.Modifiers.Hash());
        }
    }
}
