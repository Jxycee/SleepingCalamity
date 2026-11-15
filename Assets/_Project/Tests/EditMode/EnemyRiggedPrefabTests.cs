// Authority: none (EditMode, reads the built enemy prefabs and the imported FBX). Each rigged enemy (Slime, RatKing, Broom) has an Animator whose controller
// holds Idle, Move, Hit, Death and a state for every attack id of its EnemyDefinition, every Attack_* clip is as long as the attack (60 fps), the model stands
// on the feet at real size, and EnemyAnimMap maps the replicated Enemy state onto those names and times.
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.EditorTools;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class EnemyRiggedPrefabTests
    {
        static readonly EnemyKind[] Rigged = { EnemyKind.Slime, EnemyKind.RatKing, EnemyKind.Broom };

        static string PrefabPath(EnemyKind k) => k == EnemyKind.Slime ? AssetPaths.SlimePrefab : k == EnemyKind.RatKing ? AssetPaths.RatPrefab : AssetPaths.BroomPrefab;

        static AnimatorController ControllerOf(EnemyKind kind)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(kind));
            Assert.That(prefab, Is.Not.Null, kind + " prefab");
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, kind + " has an Animator under Visual");
            var ctrl = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(ctrl, Is.Not.Null, kind + " Animator has a controller");
            return ctrl;
        }

        [TestCaseSource(nameof(Rigged))]
        public void Controller_HasBaseStates_AndAStatePerAttack(EnemyKind kind)
        {
            AnimatorController ctrl = ControllerOf(kind);
            var states = ctrl.layers[0].stateMachine.states.Select(s => s.state).ToDictionary(s => s.name);
            foreach (string n in new[] { EnemyAnimMap.Idle, EnemyAnimMap.Move, EnemyAnimMap.Hit, EnemyAnimMap.Death }) Assert.That(states.ContainsKey(n), n + " state");
            if (kind == EnemyKind.Broom) Assert.That(states.ContainsKey(EnemyAnimMap.Brace), "the broom braces");
            Assert.That(ctrl.layers[0].stateMachine.defaultState.name, Is.EqualTo(EnemyAnimMap.Idle));
            foreach (AttackData attack in EnemyCatalog.Get(kind).Attacks)
            {
                string name = EnemyAnimMap.AttackState(attack);
                Assert.That(states.ContainsKey(name), name + " state");
                Assert.That(states[name].speed, Is.EqualTo(0f), name + " is placed by the attack clock, not played");
                Assert.That(states[name].motion, Is.Not.Null, name + " has a clip");
            }
        }

        [TestCaseSource(nameof(Rigged))]
        public void AttackClips_AreAsLongAsTheAttack_AndExistInTheFbx(EnemyKind kind)
        {
            EnemyArtBuilder.Spec spec = EnemyArtBuilder.SpecOf(kind);
            AnimationClip[] clips = EnemyArtBuilder.Clips(spec);
            foreach (AttackData attack in EnemyCatalog.Get(kind).Attacks)
            {
                AnimationClip clip = clips.FirstOrDefault(c => c.name == EnemyAnimMap.AttackState(attack));
                Assert.That(clip, Is.Not.Null, EnemyAnimMap.AttackState(attack) + " exists in " + spec.Fbx);
                Assert.That(clip.length * AttackData.FrameRate, Is.EqualTo(attack.TotalFrames).Within(1.0), clip.name + " length in frames");
                Assert.That(clip.isLooping, Is.False, clip.name);
            }
            foreach (string n in new[] { EnemyAnimMap.Idle, EnemyAnimMap.Move })
            {
                AnimationClip clip = clips.FirstOrDefault(c => c.name == n);
                Assert.That(clip, Is.Not.Null, n);
                Assert.That(clip.isLooping, Is.True, n + " loops");
            }
            Assert.That(clips.Any(c => c.name.Contains("|")), Is.False, "clip names are stripped to the suffix");
        }

        [TestCaseSource(nameof(Rigged))]
        public void Prefab_ModelStandsOnTheFeet_AtTheDefinitionsSize_AndIsOnTheEnemyLayer(EnemyKind kind)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(kind));
            EnemyDefinition def = EnemyCatalog.Get(kind);
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                root.transform.position = Vector3.zero;
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length, Is.GreaterThan(0));
                Bounds b = renderers[0].bounds;
                foreach (Renderer r in renderers) { b.Encapsulate(r.bounds); Assert.That(r.gameObject.layer, Is.EqualTo(Layers.Enemy), r.name + " layer"); }
                Assert.That(b.min.y, Is.InRange(-0.25f, 0.12f), kind + " stands on its feet (skinned bounds run a little past the mesh)");
                Assert.That(b.max.y, Is.InRange(def.Height * 0.7f, def.Height * 1.5f), kind + " about as tall as its capsule");
                Assert.That(b.size.x, Is.LessThan(def.Radius * 2f * 2.2f), kind + " no wider than twice its capsule");
                foreach (TellPart part in new[] { TellPart.Lid, TellPart.Bristles, TellPart.Handle, TellPart.Head, TellPart.Tail, TellPart.Crown })
                {
                    if (!def.Attacks.Any(a => a.Tell != null && a.Tell.Part == part)) continue;
                    Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == part.ToString()), part + " transform exists for the tell");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void GreyboxEnemies_KeepNoAnimator()
        {
            var warden = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPaths.WardenPrefab);
            Assert.That(warden.GetComponentInChildren<Animator>(true), Is.Null);
        }

        [Test]
        public void Map_AttackStateName_IsAttackPrefixPlusId()
        {
            Assert.That(EnemyAnimMap.AttackState("slime_hop"), Is.EqualTo("Attack_slime_hop"));
            foreach (EnemyKind kind in Rigged)
                foreach (AttackData a in EnemyCatalog.Get(kind).Attacks) Assert.That(EnemyAnimMap.AttackState(a), Is.EqualTo("Attack_" + a.Id));
        }

        [Test]
        public void Map_AttackNormalizedTime_IsFrameOverTotal_Clamped()
        {
            Assert.That(EnemyAnimMap.AttackNormalizedTime(0f, 60), Is.EqualTo(0f));
            Assert.That(EnemyAnimMap.AttackNormalizedTime(30f, 60), Is.EqualTo(0.5f).Within(1e-6));
            Assert.That(EnemyAnimMap.AttackNormalizedTime(75f, 60), Is.EqualTo(1f));
            Assert.That(EnemyAnimMap.AttackNormalizedTime(-3f, 60), Is.EqualTo(0f));
            Assert.That(EnemyAnimMap.AttackNormalizedTime(5f, 0), Is.EqualTo(0f));
            Assert.That(EnemyAnimMap.ClipSeconds(66), Is.EqualTo(1.1f).Within(1e-5));
        }

        [Test]
        public void Map_Locomotion_FollowsThePhase()
        {
            Assert.That(EnemyAnimMap.Locomotion(EnemyPhase.Idle, false), Is.EqualTo("Idle"));
            Assert.That(EnemyAnimMap.Locomotion(EnemyPhase.Recover, false), Is.EqualTo("Idle"));
            Assert.That(EnemyAnimMap.Locomotion(EnemyPhase.Chase, false), Is.EqualTo("Move"));
            Assert.That(EnemyAnimMap.Locomotion(EnemyPhase.Stagger, false), Is.EqualTo("Hit"));
            Assert.That(EnemyAnimMap.Locomotion(EnemyPhase.Dead, true), Is.EqualTo("Death"));
            Assert.That(EnemyAnimMap.Locomotion(EnemyPhase.Idle, true), Is.EqualTo("Brace"));
            Assert.That(EnemyAnimMap.Locomotion(EnemyPhase.Chase, true), Is.EqualTo("Brace"));
        }
    }
}
