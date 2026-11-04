// Tests/PlayMode/HeroClimbMetricsTests.cs: with a real CharacterController (the hero's numbers) and a copy of HeroLocomotion's run-and-jump step, measures the highest ledge a run-jump can reach, that one height level is a barrier, and that the one-cell stair climbs one level (metrics gym plan, Task 8; room-authoring spec, height rule). Authority: test-only.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.Tests.PlayMode
{
    public class HeroClimbMetricsTests
    {
        const float Dt = 1f / 60f;
        const float FaceZ = 8f;               // the ledge or wall face, metres in front of the start
        const float TakeoffGap = 0.3f;        // the run-jump starts when the capsule's front edge is this far from the face
        const int MaxSteps = 240;             // every loop below stops here (4 s at 60 Hz)
        static readonly float LevelHeight = GameMetrics.LevelHeightCm / 100f;

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            Physics.SyncTransforms();
        }

        // A minimal copy of the HeroLocomotion step (Gameplay/Player/HeroLocomotion.cs:118-196): ground and air acceleration, grounded push,
        // jump velocity, exact vertical integration, CharacterController.Move, the CollisionFlags.Above rule.
        sealed class SimHero
        {
            public readonly CharacterController Cc;
            float _vz, _vy;
            public SimHero(GameObject go)
            {
                Cc = go.AddComponent<CharacterController>();
                Cc.height = MovementMath.CapsuleHeight;
                Cc.radius = MovementMath.CapsuleRadius;
                Cc.center = new Vector3(0f, MovementMath.CapsuleHeight * 0.5f, 0f);
                Cc.stepOffset = MovementMath.StepOffset;
                Cc.slopeLimit = MovementMath.SlopeLimit;
                Cc.skinWidth = MovementMath.SkinWidth;
            }
            public Vector3 Position => Cc.transform.position;
            public bool Grounded => Cc.isGrounded;

            public void Step(float stick, bool sprint, bool jump)
            {
                bool grounded = Cc.isGrounded;
                float vx = 0f;
                float speed = MovementMath.TargetSpeed(stick, sprint && grounded);
                MovementMath.Accelerate(ref vx, ref _vz, 0f, speed, grounded ? MovementMath.GroundAccel : MovementMath.AirAccel, Dt);
                if (grounded && _vy < 0f) _vy = MovementMath.GroundedPush(Dt);
                if (jump && grounded) _vy = MovementMath.JumpVelocity(MovementMath.JumpHeight, MovementMath.Gravity);
                float dy = MovementMath.VerticalStep(ref _vy, MovementMath.Gravity, MovementMath.TerminalVelocity, Dt);
                CollisionFlags flags = Cc.Move(new Vector3(0f, dy, _vz * Dt));
                if ((flags & CollisionFlags.Above) != 0 && _vy > 0f) _vy = 0f;
            }
        }

        GameObject Box(string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.position = centre;
            go.AddComponent<BoxCollider>().size = size;
            _made.Add(go);
            return go;
        }

        // A 40 m floor whose top is y = 0, running from z = -10 to z = 30.
        void Floor() => Box("Floor", new Vector3(0f, -0.5f, 10f), new Vector3(20f, 1f, 40f));

        SimHero Hero()
        {
            var go = new GameObject("SimHero");
            go.transform.position = new Vector3(0f, 0.05f, 0f);
            _made.Add(go);
            var hero = new SimHero(go);
            Physics.SyncTransforms();
            for (int i = 0; i < 10; i++) hero.Step(0f, false, false);   // settle onto the floor
            return hero;
        }

        // Runs at RunSpeed toward the face at FaceZ and jumps once, TakeoffGap before it; stops moving after landing. Returns the end position.
        Vector3 RunJump(SimHero hero)
        {
            bool jumped = false;
            int sinceJump = 0;
            for (int i = 0; i < MaxSteps; i++)
            {
                if (jumped && sinceJump > 5 && hero.Grounded) break;   // landed again (on the ledge, or back on the floor)
                bool jump = !jumped && hero.Grounded && hero.Position.z + MovementMath.CapsuleRadius >= FaceZ - TakeoffGap;
                if (jump) jumped = true;
                if (jumped) sinceJump++;
                hero.Step(1f, true, jump);
            }
            return hero.Position;
        }

        // The highest ledge (top at the given height, deep enough to stand on) the run-jump ends up standing on; -1 if none.
        float EndHeightOnLedge(float height)
        {
            Box("Ledge", new Vector3(0f, height * 0.5f, FaceZ + 7f), new Vector3(20f, height, 14f));
            SimHero hero = Hero();
            return RunJump(hero).y;
        }

        [Test]
        public void MaxClimbableLedge_IsBelowOneLevel()
        {
            float best = 0f;
            var log = new System.Text.StringBuilder();
            for (int tenth = 4; tenth <= 20; tenth++)
            {
                float h = tenth / 10f;
                foreach (GameObject go in _made) if (go != null) Object.DestroyImmediate(go);
                _made.Clear();
                Physics.SyncTransforms();
                Floor();
                float endY = EndHeightOnLedge(h);
                bool stood = endY >= h - 0.05f;
                log.Append(h.ToString("0.0")).Append(stood ? ":up " : ":no ");
                if (stood && h > best) best = h;
            }
            Debug.Log("SC METRIC max_ledge=" + best.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            Debug.Log("SC METRIC ledge sweep " + log);
            Assert.That(best, Is.GreaterThanOrEqualTo(MovementMath.JumpHeight), "the hero reaches at least a jump-height ledge; sweep " + log);
            Assert.That(best, Is.LessThan(LevelHeight - 0.2f), "one level (" + LevelHeight + " m) stays a barrier; sweep " + log);
        }

        [Test]
        public void Stair_ClimbsOneLevel()
        {
            Floor();
            const float z0 = 3f;
            float rise = GameMetrics.StairRiseCm / 100f, run = GameMetrics.StairRunCm / 100f;
            int steps = GameMetrics.StepsPerLevel;
            for (int i = 0; i < steps; i++)
            {
                float top = (i + 1) * rise;
                Box("Step" + i, new Vector3(0f, top * 0.5f, z0 + i * run + run * 0.5f), new Vector3(4f, top, run));
            }
            float deckStart = z0 + steps * run;
            Box("Deck", new Vector3(0f, LevelHeight * 0.5f, deckStart + 4f), new Vector3(4f, LevelHeight, 8f));
            SimHero hero = Hero();

            int frames = 0;
            for (; frames < 180; frames++)   // 3 s
            {
                hero.Step(1f, false, false);   // a full stick, no sprint, no jump
                if (hero.Position.y >= LevelHeight - 0.05f && hero.Grounded) break;
            }
            Debug.Log("SC METRIC stair_climb_seconds=" + (frames * Dt).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " end_y=" + hero.Position.y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            Assert.That(frames, Is.LessThan(180), "reached the deck within 3 s; end y " + hero.Position.y + " z " + hero.Position.z);
            Assert.That(hero.Position.y, Is.EqualTo(LevelHeight).Within(0.1f));
        }

        [Test]
        public void OneLevelWall_BlocksARunJump()
        {
            Floor();
            Box("Wall", new Vector3(0f, LevelHeight * 0.5f, FaceZ + 0.25f), new Vector3(20f, LevelHeight, 0.5f));
            SimHero hero = Hero();
            Vector3 end = RunJump(hero);
            Debug.Log("SC METRIC wall_end=" + end);
            Assert.That(end.y, Is.LessThan(0.5f), "a 2 m wall stops a run-jump; end " + end);
            Assert.That(end.z + MovementMath.CapsuleRadius, Is.LessThanOrEqualTo(FaceZ + 0.1f), "the hero did not get past the wall; end " + end);
        }
    }
}
