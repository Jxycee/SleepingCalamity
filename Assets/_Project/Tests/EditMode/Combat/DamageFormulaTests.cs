// Catches: the 6.2 ratio formula drifting (subtraction creeping back, a hit rounding to 0, the ceil lost), the finisher bonus applied to the wrong hits,
// the crit and element/tag multipliers not reaching the number.
using NUnit.Framework;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class DamageFormulaTests
    {
        static DamageInput In(float atk, float move, float def) => DamageInput.Basic(atk, move, def);

        [Test]
        public void Ratio_AxeHitOnSlime_IsCeilOfAtkTimesRatio()
        {
            // 22 x 1.0 x 100/105 = 20.95 -> 21
            Assert.That(DamageFormula.Compute(In(22f, 1f, 5f)), Is.EqualTo(21));
            // 22 x 100/110 = 20 exactly: float noise must not make it 21
            Assert.That(DamageFormula.Compute(In(22f, 1f, 10f)), Is.EqualTo(20));
        }

        [Test]
        public void Ratio_HighDefence_NeverBelowOne()
        {
            Assert.That(DamageFormula.Compute(In(1f, 0.1f, 5000f)), Is.EqualTo(1));
            Assert.That(DamageFormula.Compute(In(0f, 1f, 0f)), Is.EqualTo(1), "a zero attack still chips for the minimum");
            Assert.That(DamageFormula.Compute(In(float.NaN, 1f, 0f)), Is.EqualTo(1));
        }

        [Test]
        public void Ratio_TierGap_StaysReadable()
        {
            // Subtraction (22 - 33) would be negative against a tier 2 broom (def 15 x 3.3 = 49.5): the ratio still pays.
            int tier1 = DamageFormula.Compute(In(22f, 1f, 15f));
            int tier2 = DamageFormula.Compute(In(22f, 1f, 49.5f));
            Assert.That(tier1, Is.EqualTo(20));
            Assert.That(tier2, Is.EqualTo(15));
            Assert.That(tier2, Is.LessThan(tier1));
        }

        [Test]
        public void FinisherBonus_IsTenPercentPerPriorHit_AndOnlyForFinishers()
        {
            Assert.That(DamageFormula.MoveMultiplier(1.6f, true, 0), Is.EqualTo(1.6f).Within(1e-5f));
            Assert.That(DamageFormula.MoveMultiplier(1.6f, true, 2), Is.EqualTo(1.92f).Within(1e-5f), "1.6 x (1 + 0.1 x 2)");
            Assert.That(DamageFormula.MoveMultiplier(1.1f, false, 2), Is.EqualTo(1.1f), "a normal hit has no combo bonus");
            Assert.That(DamageFormula.MoveMultiplier(1.6f, true, -3), Is.EqualTo(1.6f), "negative hits count as none");
        }

        [Test]
        public void Crit_RollBelowFivePercent_IsOnePointFive()
        {
            Assert.That(DamageFormula.RollCrit(0.049f), Is.True);
            Assert.That(DamageFormula.RollCrit(0.05f), Is.False);
            Assert.That(DamageFormula.RollCrit(0.9f), Is.False);
            var plain = In(22f, 1f, 10f);
            var crit = plain;
            crit.Crit = true;
            Assert.That(DamageFormula.Compute(crit), Is.EqualTo(30), "22 x 100/110 x 1.5 = 30");
        }

        [Test]
        public void ElementAndTag_BecomeNamedMultipliers()
        {
            Assert.That(DamageFormula.ElementBonus(10f, true), Is.EqualTo(0.2f).Within(1e-5f), "Fire 10 = +20% on a fire-weak enemy");
            Assert.That(DamageFormula.ElementBonus(10f, false), Is.EqualTo(0f));
            Assert.That(DamageFormula.TagMultiplier(10f, true), Is.EqualTo(1.3f).Within(1e-5f), "Pest 10 = x1.3 on a pest");
            Assert.That(DamageFormula.TagMultiplier(10f, false), Is.EqualTo(1f));
            var i = In(30f, 1f, 15f);
            i.ElementBonus = DamageFormula.ElementBonus(10f, true);
            Assert.That(DamageFormula.Compute(i), Is.EqualTo(32), "30 x 100/115 x 1.2 = 31.3");
            i.Part = 1.5f;
            Assert.That(DamageFormula.Compute(i), Is.EqualTo(47), "x1.5 weak part = 46.96");
        }

        [Test]
        public void Other_GuardOrBristles_ScalesButKeepsTheMinimum()
        {
            var i = In(12f, 1f, 10f);   // 10.9 -> 11 unguarded
            Assert.That(DamageFormula.Compute(i), Is.EqualTo(11));
            i.Other = 0.2f;
            Assert.That(DamageFormula.Compute(i), Is.EqualTo(3), "10.9 x 0.2 = 2.18 -> 3");
            i.Atk = 1f;
            Assert.That(DamageFormula.Compute(i), Is.EqualTo(1));
        }
    }
}
