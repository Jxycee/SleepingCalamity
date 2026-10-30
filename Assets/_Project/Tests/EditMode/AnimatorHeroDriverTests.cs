// Authority: none (EditMode). AnimatorHeroDriver's hurt trigger rules on a real Humanoid Animator (the Y Bot with the committed hero controller), and the
// Mixamo hint matching that keeps "greatswordcombo1" off "greatswordcombo10".
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.EditorTools;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class AnimatorHeroDriverTests
    {
        private GameObject _go;
        private AnimatorHeroDriver _driver;
        private Animator _animator;

        [SetUp]
        public void SetUp()
        {
            var ybot = AssetDatabase.LoadAssetAtPath<GameObject>(AnimationImportBuilder.YBot);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HeroAnimatorBuilder.ControllerPath);
            Assert.That(ybot, Is.Not.Null);
            Assert.That(controller, Is.Not.Null, "run the build first: the hero controller is missing");
            _go = Object.Instantiate(ybot);
            _animator = _go.GetComponent<Animator>();
            if (_animator == null) _animator = _go.AddComponent<Animator>();
            _animator.runtimeAnimatorController = controller;
            _driver = _go.AddComponent<AnimatorHeroDriver>();
            _driver.Configure(_animator);
            Assert.That(_driver.IsWorking, Is.True, "Y Bot with the controller should be a working Humanoid Animator");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        private void Apply(int serial, bool heavy = false, bool downed = false)
        {
            var s = new HeroAnimState { Grounded = true, HurtSerial = serial, HurtHeavy = heavy, Downed = downed };
            _driver.Apply(in s, 0.016f);
        }

        [Test]
        public void FirstFrame_RecordsTheSerialWithoutFiring()
        {
            Apply(7);   // a late joiner meets a hero that was already hurt seven times
            Assert.That(_animator.GetBool(HeroAnimParam.Hurt), Is.False);
            Assert.That(_animator.GetBool(HeroAnimParam.HurtHeavy), Is.False);
            Apply(7);
            Assert.That(_animator.GetBool(HeroAnimParam.Hurt), Is.False, "an unchanged serial never fires");
        }

        [Test]
        public void BumpedSerial_FiresHurtOnce()
        {
            Apply(3);
            Apply(4);
            Assert.That(_animator.GetBool(HeroAnimParam.Hurt), Is.True);
            Assert.That(_animator.GetBool(HeroAnimParam.HurtHeavy), Is.False);
            _animator.ResetTrigger(HeroAnimParam.Hurt);   // what the controller does when it consumes the trigger
            Apply(4);
            Assert.That(_animator.GetBool(HeroAnimParam.Hurt), Is.False, "the same serial does not fire again");
        }

        [Test]
        public void HeavyBump_FiresHurtHeavy()
        {
            Apply(0);
            Apply(1, heavy: true);
            Assert.That(_animator.GetBool(HeroAnimParam.HurtHeavy), Is.True);
            Assert.That(_animator.GetBool(HeroAnimParam.Hurt), Is.False);
        }

        [Test]
        public void Downed_SuppressesHurt_AndDoesNotFireLateOnGettingUp()
        {
            Apply(0);
            Apply(1, downed: true);
            Assert.That(_animator.GetBool(HeroAnimParam.Hurt) || _animator.GetBool(HeroAnimParam.HurtHeavy), Is.False, "lying down: no flinch");
            Apply(1);
            Assert.That(_animator.GetBool(HeroAnimParam.Hurt), Is.False, "the hit taken while downed was swallowed, not queued");
        }
    }

    public class MixamoHintMatchTests
    {
        [Test]
        public void ExactNameBeatsContains()
        {
            Assert.That(HeroClipTable.MatchRank("Great Sword Combo 1", "greatswordcombo1"), Is.EqualTo(0));
            Assert.That(HeroClipTable.MatchRank("Great Sword Combo 1 In Place", "greatswordcombo1"), Is.EqualTo(1));
            Assert.That(HeroClipTable.MatchRank("Warrior Idle", "warrioridle"), Is.EqualTo(0));
        }

        [Test]
        public void TrailingDigitHint_DoesNotMatchALongerNumber()
        {
            Assert.That(HeroClipTable.MatchRank("Great Sword Combo 10", "greatswordcombo1"), Is.EqualTo(-1));
            Assert.That(HeroClipTable.MatchRank("great_sword_combo_11_inplace", "greatswordcombo1"), Is.EqualTo(-1));
            Assert.That(HeroClipTable.MatchRank("Great Sword Combo 1 Slash", "greatswordcombo1"), Is.EqualTo(1));
            Assert.That(HeroClipTable.MatchRank("Dodge Left", "dodgeright"), Is.EqualTo(-1));
        }
    }
}
