// Authority: none (pure EditMode tests). Plan 5 task 2: the two modifier slots (FloorModifierSet), the twists folded into them (CellarFloorRoster.TwistOf), the Stirred bonus on DiveStir.
// Catches: a modifier on a calm floor, two slots on floors 2/4, two modifiers of one family, a modifier twice in a dive, rates that drift from the tuning, a Thin twist without Sparse,
// Dim off the dark floor, a ModifierBonus applied before the sqrt, saved, or kept after push-on. Does not catch: what a modifier does in the live dive (Task 5).
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class FloorModifierTests
    {
        [Test]
        public void CalmFloors_HaveNone()
        {
            for (uint s = 1; s <= 500; s++)
                foreach (int f in new[] { 1, 3, 5 })
                {
                    FloorModifierSet m = FloorModifierSet.For(s, f);
                    Assert.AreEqual(FloorModifier.None, m.A, "seed " + s + " floor " + f); Assert.AreEqual(FloorModifier.None, m.B);
                }
            // floor 6 is calm too: nothing but the dark floor's Dim
            for (uint s = 1; s <= 500; s++)
            {
                FloorModifierSet m = FloorModifierSet.For(s, 6);
                Assert.AreEqual(CellarAtmosphere.IsDarkFloor(s, 6) ? FloorModifier.Dim : FloorModifier.None, m.A, "seed " + s);
                Assert.AreEqual(FloorModifier.None, m.B);
            }
        }

        [Test]
        public void EarlyFloors_AtMostOneSlot()
        {
            for (uint s = 1; s <= 2000; s++)
                foreach (int f in new[] { 2, 4 })
                {
                    FloorModifierSet m = FloorModifierSet.For(s, f);
                    Assert.IsFalse(m.A != FloorModifier.None && m.B != FloorModifier.None, "seed " + s + " floor " + f);
                }
        }

        [Test]
        public void NeverTwoOfOneFamily()
        {
            for (uint s = 1; s <= 2000; s++)
                foreach (int f in new[] { 2, 4, 6, 7 })
                {
                    FloorModifierSet m = FloorModifierSet.For(s, f);
                    if (m.A != FloorModifier.None && m.B != FloorModifier.None)
                        Assert.AreNotEqual(FloorModifierSet.FamilyOf(m.A), FloorModifierSet.FamilyOf(m.B), "seed " + s + " floor " + f);
                }
        }

        [Test]
        public void NoModifierTwiceInADive()
        {
            for (uint s = 1; s <= 2000; s++)
            {
                var seen = new System.Collections.Generic.HashSet<FloorModifier>();
                foreach (int f in new[] { 2, 4, 6, 7 })
                {
                    FloorModifierSet m = FloorModifierSet.For(s, f);
                    foreach (FloorModifier x in new[] { m.A, m.B })
                    {
                        if (x == FloorModifier.None || x == FloorModifier.Dim) continue;   // Dim: IsDarkFloor already allows one per dive
                        Assert.IsTrue(seen.Add(x), "seed " + s + " floor " + f + " repeats " + x);
                    }
                }
            }
        }

        [Test]
        public void Rates_RoughlyMatchTuning()
        {
            int a2 = 0, b2 = 0, a7 = 0, b7 = 0, n = 0, n7 = 0, nb7 = 0;
            for (uint s = 1; s <= 5000; s++)
            {
                if (CellarAtmosphere.IsDarkFloor(s, 2) || CellarAtmosphere.IsDarkFloor(s, 7)) continue;   // a dark floor overrides slot A
                FloorModifierSet m2 = FloorModifierSet.For(s, 2), m7 = FloorModifierSet.For(s, 7);
                n++;
                if (m2.A != FloorModifier.None) a2++;
                if (m2.B != FloorModifier.None) b2++;
                // floor 7 "before the repeat rule": only dives where floors 4 and 6 left nothing used (floor 2 may have; its pick is independent of floor 7's roll)
                FloorModifierSet m4 = FloorModifierSet.For(s, 4), m6 = FloorModifierSet.For(s, 6);
                if (m2.A != FloorModifier.None || m2.B != FloorModifier.None || m4.A != FloorModifier.None || m4.B != FloorModifier.None || m6.A != FloorModifier.None || m6.B != FloorModifier.None) continue;
                n7++;
                if (m7.A != FloorModifier.None) a7++;
                if (m7.A == FloorModifier.None) { nb7++; if (m7.B != FloorModifier.None) b7++; }
            }
            Assert.AreEqual(FloorModifierTuning.EarlyAPercent, a2 * 100f / n, 4f, "floor 2 A");
            Assert.AreEqual(FloorModifierTuning.EarlyBPercent, b2 * 100f / n, 4f, "floor 2 B");
            Assert.AreEqual(FloorModifierTuning.LateAPercent, a7 * 100f / n7, 4f, "floor 7 A");
            Assert.AreEqual(FloorModifierTuning.LateBPercent, b7 * 100f / nb7, 5f, "floor 7 B with no hazard (a hazard of the same family would empty it)");
        }

        [Test]
        public void TwistOf_FollowsModifiers()
        {
            for (uint s = 1; s <= 2000; s++)
                foreach (int f in new[] { 1, 2, 3, 4, 5, 6, 7 })
                {
                    FloorModifierSet m = FloorModifierSet.For(s, f);
                    FloorTwist want = m.A == FloorModifier.Crowded ? FloorTwist.Crowded : m.A == FloorModifier.Ambush ? FloorTwist.Ambush : m.B == FloorModifier.Sparse ? FloorTwist.Thin : FloorTwist.None;
                    Assert.AreEqual(want, CellarFloorRoster.TwistOf(s, f), "seed " + s + " floor " + f);
                    if (CellarFloorRoster.TwistOf(s, f) == FloorTwist.Thin) Assert.IsTrue(m.Has(FloorModifier.Sparse));
                }
        }

        [Test]
        public void StirredBonus_OnlyCurrentFloor()
        {
            int stirred = 0;
            for (uint s = 1; s <= 2000; s++)
                for (int f = 1; f <= 7; f++)
                {
                    bool has = FloorModifierSet.For(s, f).Has(FloorModifier.Stirred);
                    Assert.AreEqual(has ? FloorModifierTuning.StirredBonus : 0f, FloorModifierSet.StirredBonusFor(s, f), "seed " + s + " floor " + f);
                    if (has) stirred++;
                }
            Assert.Greater(stirred, 100, "the sweep must reach Stirred floors");
        }

        [Test]
        public void Dim_ExactlyOnDarkGeneratedFloors()
        {
            int dim = 0;
            for (uint s = 1; s <= 2000; s++)
                for (int f = 1; f <= 7; f++)
                {
                    bool want = CellarAtmosphere.IsDarkFloor(s, f) && CellarFloorGenerator.IsGeneratedFloor(f);
                    Assert.AreEqual(want, FloorModifierSet.For(s, f).Has(FloorModifier.Dim), "seed " + s + " floor " + f);
                    if (want) dim++;
                }
            Assert.Greater(dim, 20);
        }

        [Test]
        public void IsDeterministic_AndHashDiffers()
        {
            for (uint s = 1; s <= 200; s++) Assert.AreEqual(FloorModifierSet.For(s, 7).Hash(), FloorModifierSet.For(s, 7).Hash());
            Assert.AreNotEqual(new FloorModifierSet(FloorModifier.Crowded, FloorModifier.None).Hash(), new FloorModifierSet(FloorModifier.None, FloorModifier.Sparse).Hash());
        }

        [Test]
        public void DiveStir_ModifierBonus_AddedAfterSqrt_ClampedAndClearedOnPushOn()
        {
            var st = new DiveStir { PlayersPresent = 4 };
            DiveStir.State before = st.Capture();   // a fresh meter, before any bonus is set
            st.OnFloorCleared(1); st.OnFloorCleared(2);   // base 20
            Assert.AreEqual(40f, st.Value, 0.001f);        // 20 * sqrt(4)
            st.ModifierBonus = FloorModifierTuning.StirredBonus;
            Assert.AreEqual(50f, st.Value, 0.001f);        // 20 * 2 + 10: the bonus is not scaled again
            st.ModifierBonus = 500f;
            Assert.AreEqual(DiveStir.Max, st.Value, 0.001f);
            st.ModifierBonus = -5f;
            Assert.AreEqual(0f, st.ModifierBonus);
            Assert.AreEqual(40f, st.Value, 0.001f);
            DiveStir.State withoutBonus = st.Capture();
            st.ModifierBonus = 10f;
            Assert.AreEqual(withoutBonus, st.Capture(), "the bonus is not part of State (only the floors cleared differ from the fresh meter)");
            Assert.AreNotEqual(before, withoutBonus, "the captured state does move with the floors cleared");
            var other = new DiveStir(); other.Restore(withoutBonus);
            Assert.AreEqual(0f, other.ModifierBonus);
            var fresh = new DiveStir { PlayersPresent = 4 };
            fresh.ModifierBonus = 10f;
            Assert.AreEqual(before, fresh.Capture(), "a bonus on a fresh meter leaves State at its fresh value (nothing latched below the thresholds)");
            st.OnPushOn();
            Assert.AreEqual(0f, st.ModifierBonus);
            st.ModifierBonus = 10f; st.ResetForNewDive();
            Assert.AreEqual(0f, st.ModifierBonus);
        }

        // Documented hazard (DiveStir.cs header, Stirred effect line): the flat +10 counts like any stir, so it can latch the extra enemy or the hunter for the rest of the half.
        [Test]
        public void DiveStir_ModifierBonus_CanLatchExtraEnemyAndHunter_ForTheHalf()
        {
            var calm = new DiveStir { PlayersPresent = 1 };
            calm.OnFloorCleared(1); calm.OnFloorCleared(2); calm.OnFloorCleared(3);   // 30: below the extra-enemy threshold (40)
            Assert.IsFalse(calm.ExtraEnemyLatched);
            var stirred = new DiveStir { PlayersPresent = 1 };
            stirred.OnFloorCleared(1); stirred.OnFloorCleared(2); stirred.OnFloorCleared(3);
            stirred.ModifierBonus = FloorModifierTuning.StirredBonus;   // 30 + 10 = 40
            Assert.IsTrue(stirred.ExtraEnemyLatched, "the bonus brought the extra enemy");
            stirred.ModifierBonus = 0f;   // the party leaves the Stirred floor
            Assert.AreEqual(30f, stirred.Value, 0.001f);
            Assert.IsTrue(stirred.ExtraEnemyLatched, "the latch holds for the half after the bonus is gone");
            Assert.IsTrue(stirred.Capture().Extra, "and it is part of the saved State");

            var hunted = new DiveStir { PlayersPresent = 4 };
            hunted.OnFloorCleared(1); hunted.OnFloorCleared(2); hunted.OnFloorCleared(3);   // 30 * 2 = 60: extra latched, hunter (70) not
            Assert.IsTrue(hunted.ExtraEnemyLatched); Assert.IsFalse(hunted.HunterLatched);
            hunted.ModifierBonus = FloorModifierTuning.StirredBonus;   // 70
            Assert.IsTrue(hunted.HunterLatched, "the bonus brought the hunter");
            hunted.ModifierBonus = 0f;
            Assert.IsTrue(hunted.HunterLatched, "the hunter latch holds");
            hunted.OnPushOn();
            Assert.IsFalse(hunted.HunterLatched, "the second half starts without the latches");
        }

        [Test]
        public void EffectHelpers_FollowTheSet_AndTheCollapseYieldsTheHurriedMultiplier()
        {
            var none = default(FloorModifierSet);
            Assert.AreEqual(0, none.GoldPerRoomClear); Assert.AreEqual(0, none.AmbushRoomGold); Assert.AreEqual(0, none.LeaveGold); Assert.AreEqual(0, none.ExtraChestRolls);
            Assert.AreEqual(2, none.ChestRankBonus(2)); Assert.AreEqual(30f, none.HealRechargeSeconds(30f)); Assert.AreEqual(100f, none.CollapsePathSeconds(100f));
            Assert.AreEqual(FloorModifierTuning.CrowdedGoldPerClear, new FloorModifierSet(FloorModifier.Crowded, FloorModifier.None).GoldPerRoomClear);
            Assert.AreEqual(FloorModifierTuning.AmbushGold, new FloorModifierSet(FloorModifier.Ambush, FloorModifier.None).AmbushRoomGold);
            Assert.AreEqual(FloorModifierTuning.HurriedGold, new FloorModifierSet(FloorModifier.Hurried, FloorModifier.None).LeaveGold);
            Assert.AreEqual(FloorModifierTuning.StirredExtraRolls, new FloorModifierSet(FloorModifier.Stirred, FloorModifier.None).ExtraChestRolls);
            Assert.AreEqual(2 + FloorModifierTuning.DimRankBonus, new FloorModifierSet(FloorModifier.Dim, FloorModifier.None).ChestRankBonus(2));
            var warm = new FloorModifierSet(FloorModifier.None, FloorModifier.WarmHands);
            Assert.AreEqual(FloorModifierTuning.WarmHandsRechargeSeconds, warm.HealRechargeSeconds(30f));
            Assert.AreEqual(0.2f, warm.HealRechargeSeconds(0.2f), "a shorter test recharge is kept");
            var hurried = new FloorModifierSet(FloorModifier.Hurried, FloorModifier.None);
            Assert.AreEqual(FloorModifierTuning.HurriedCollapseMultiplier * 200f, DiveStir.CollapseSeconds(hurried.CollapsePathSeconds(200f)), 0.01f, "1.25x the path, not 1.5x");
            Assert.AreEqual(DiveStir.CollapseMultiplier * 200f, DiveStir.CollapseSeconds(none.CollapsePathSeconds(200f)), 0.01f);
        }
    }
}
