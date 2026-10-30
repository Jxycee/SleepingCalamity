// Authority: none (EditMode, reads the imported animation FBX). The builder makes a controller with every expected parameter, state and layer, every
// attack id has a state, and every clip slot resolves. Builds to a temp path so the project's own controller is never touched.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class HeroAnimatorBuilderTests
    {
        private const string TempFolder = "Assets/_Project/Content/Animation/_TestTmp";
        private AnimatorController _ctrl;
        private readonly List<string> _placeholders = new List<string>();

        [OneTimeSetUp]
        public void Build()
        {
            _ctrl = HeroAnimatorBuilder.Build(TempFolder + "/Test.controller", _placeholders);
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            AssetDatabase.DeleteAsset(TempFolder);
        }

        [Test]
        public void Controller_HasEveryParameter_WithGroundedDefaultTrue()
        {
            var have = _ctrl.parameters.ToDictionary(p => p.name);
            foreach (string n in HeroAnimatorBuilder.FloatParams) Assert.That(have[n].type, Is.EqualTo(AnimatorControllerParameterType.Float), n);
            foreach (string n in HeroAnimatorBuilder.BoolParams) Assert.That(have[n].type, Is.EqualTo(AnimatorControllerParameterType.Bool), n);
            foreach (string n in HeroAnimatorBuilder.IntParams) Assert.That(have[n].type, Is.EqualTo(AnimatorControllerParameterType.Int), n);
            foreach (string n in HeroAnimatorBuilder.TriggerParams) Assert.That(have[n].type, Is.EqualTo(AnimatorControllerParameterType.Trigger), n);
            Assert.That(have[HeroAnimParam.Grounded].defaultBool, Is.True, "a hero must not start in the jump state");
        }

        [Test]
        public void Controller_HasBaseStates_AttackStatesAndHurtStates()
        {
            Assert.That(_ctrl.layers.Length, Is.EqualTo(3));
            var baseStates = _ctrl.layers[0].stateMachine.states.Select(s => s.state.name).ToList();
            foreach (string n in HeroAnimatorBuilder.BaseStates) Assert.That(baseStates, Contains.Item(n));
            Assert.That(_ctrl.layers[0].stateMachine.defaultState.name, Is.EqualTo("Free"));

            var attackStates = _ctrl.layers[1].stateMachine.states.Select(s => s.state.name).ToList();
            Assert.That(attackStates, Contains.Item("Empty"));
            for (int id = 1; id < HeroAnimMap.AttackIdCount; id++)
                Assert.That(attackStates, Contains.Item(HeroClipTable.AttackStateName((byte)id)), "attack id " + id);
            foreach (var st in _ctrl.layers[1].stateMachine.states.Where(s => s.state.name != "Empty"))
            {
                Assert.That(st.state.motion, Is.Not.Null, st.state.name);
                Assert.That(st.state.timeParameterActive && st.state.timeParameter == HeroAnimParam.AttackTime, Is.True, st.state.name + " plays by the combat clock");
            }

            var hurtStates = _ctrl.layers[2].stateMachine.states.Select(s => s.state.name).ToList();
            Assert.That(hurtStates, Is.SupersetOf(new[] { "Empty", "HurtLight", "HurtHeavy" }));
        }

        [Test]
        public void Controller_EveryBaseStateHasAMotion_AndEveryConditionNamesARealParameter()
        {
            var names = new HashSet<string>(_ctrl.parameters.Select(p => p.name));
            foreach (var layer in _ctrl.layers)
            {
                var transitions = new List<AnimatorStateTransition>(layer.stateMachine.anyStateTransitions);
                foreach (var st in layer.stateMachine.states)
                {
                    if (layer.name == "Base") Assert.That(st.state.motion, Is.Not.Null, st.state.name);
                    transitions.AddRange(st.state.transitions);
                }
                foreach (var t in transitions)
                    foreach (var c in t.conditions) Assert.That(names, Contains.Item(c.parameter), layer.name + " transition to " + (t.destinationState != null ? t.destinationState.name : "?"));
            }
        }

        [Test]
        public void DodgeState_PlaysByProgress()
        {
            AnimatorState dodge = _ctrl.layers[0].stateMachine.states.First(s => s.state.name == "Dodge").state;
            Assert.That(dodge.timeParameterActive && dodge.timeParameter == HeroAnimParam.DodgeTime, Is.True);
        }

        [Test]
        public void OwedMixamoSlots_AreLogged_AndEachHasAFallbackClip()
        {
            Assert.That(_placeholders.Any(l => l.Contains("DodgeBack")), Is.True, "the owed back step is reported as a placeholder");
            foreach (ClipSlot slot in HeroClipTable.Slots())
                if (slot.OwedFromMixamo) Assert.That(slot.Sources.Length, Is.GreaterThan(1), slot.Id + " has no fallback clip");
        }
    }
}
