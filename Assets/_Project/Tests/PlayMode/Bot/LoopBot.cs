// Authority: test-only (local owner of the host hero; solo host, or the host peer of a host-plus-client session). PL-1 task 1.2: plays the playable loop by button presses only, from the Hollowmere spawn:
// Interact at the trapdoor (a floor-1 start: with a checkpoint held the pick defaults to the bank floor after PL-4 4.2, so the bot presses Lock until the pick is floor 1), DiveBot plays the dive through the same FakeInputService, home by any ending (a death, or the bank room's
// go-home button on floor 5), Interact at the build point, place what is in stock (Primary: frame, parts, resident), Pause out (the world saves), leave, start the same world again, check the town.
// It may TELEPORT the hero between town points (the town walk is not what is under test); it never calls a Server* method or an RPC for the action under test. The only test-side cheats are the
// two the dive bots already have: DiveBot.Tough (the host tops the hero up below 50% HP, so a run tests wiring and not difficulty) and the optional ExtraTick of a caller (pair test).
// Measures per dive: game seconds, the end reason, the floor it ended on, and the floor each piece was first found on (read from the town stock, so a real pickup is what counts).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.Tests.PlayMode
{
    public sealed class LoopBot
    {
        public const float Step = 1f / 60f;

        public sealed class DiveRecord
        {
            public int Number;
            public float Seconds;                       // game seconds the DiveBot played
            public DiveEndReason? End;                  // null: stopped early (StopWhenFrameHome) or timed out
            public int EndFloor, MaxFloor;
            public bool StoppedEarly, TimedOut, FrameHomeAtEnd;
            public readonly List<string> Found = new List<string>();
        }

        public TownInputRig Rig;
        public bool Tough;
        public bool StopWhenFrameHome;                  // Measure: stop the dive as soon as the frame is home and the bot has left floor 2
        public float GameLimit = 3300f;                 // game seconds per dive (the bank room is floor 5)
        public double RealLimit = 2400.0;               // wall seconds per dive
        public Action<float> ExtraTick;                 // per frame during a dive, after the DiveBot (a FollowerBot, a party top-up)
        public Func<IEnumerator> BeforeDive;            // runs before the trapdoor press (the pair test puts the second hero there)
        public readonly List<DiveRecord> Dives = new List<DiveRecord>();
        public readonly Dictionary<int, int> PieceFloor = new Dictionary<int, int>();        // piece id -> floor it was first seen arriving on
        public readonly Dictionary<int, int> DeathsByFloor = new Dictionary<int, int>();     // failed dives by the floor they ended on
        public readonly List<string> Log = new List<string>();
        public int BakerFloor = -1;
        readonly HashSet<int> _known = new HashSet<int>();
        bool _bakerKnown;

        public LoopBot(TownInputRig rig)
        {
            Rig = rig;
            foreach (int p in rig.Director.Town.State.StockPieces) _known.Add(p);
            _bakerKnown = rig.Director.Town.State.ResidentInStock(TownBuildings.BakerResident);
        }

        public static string Sig(TownState s) => TownWire.From(s).Signature();
        public bool FrameHome => Rig.Director.Town.State.InStock(PieceCatalog.FrameId);
        public bool BakeryStands => Rig.Director.Town.State.PlacedBuildingOf(TownBuildings.Bakery) != null;
        void Say(string s) { Log.Add(s); Debug.Log("[LOOPBOT] " + s); }

        // ------------------------------------------------------------------------------------------------ one dive
        public IEnumerator PlayDive()
        {
            DiveDirector d = Rig.Director;
            var rec = new DiveRecord { Number = Dives.Count + 1 };
            Dives.Add(rec);
            if (BeforeDive != null) yield return BeforeDive();
            Rig.StandAtTrapdoor();
            yield return PlayModeHarness.WaitUntil(() => d.Prompt == DiveDirector.PromptKind.Descend, TownInputRig.Timeout, "[loop] dive " + rec.Number + ": no trapdoor prompt at the trapdoor");
            for (int i = 0; i < 12 && d.NetCheckpointHeld.Value && d.PickFloor != UndercroftPlan.FirstFloor; i++)
                yield return Rig.Press(f => f.Frame.LockPressed = true);   // a held checkpoint defaults the pick to the bank floor; this bot always starts on floor 1
            Assert.AreEqual(UndercroftPlan.FirstFloor, d.PickFloor, "[loop] dive " + rec.Number + ": Lock never brought the pick back to floor 1");
            yield return Rig.PressInteract();
            yield return PlayModeHarness.WaitUntil(() => d.NetActive.Value && d.FloorSync != null && d.FloorSync.Built != null && d.FloorSync.BuiltFloor == UndercroftPlan.FirstFloor, TownInputRig.Timeout,
                "[loop] dive " + rec.Number + ": Interact at the trapdoor never built floor 1");
            yield return PlayModeHarness.WaitUntil(() => Rig.Hero.transform.position.y < AreaLook.Current.CellarBelowY, TownInputRig.Timeout, "[loop] the hero never arrived under the town");

            bool ended = false;
            DiveEndReason? reason = null; int failedOn = 0;
            Action<DiveEndReason> onEnd = r => { ended = true; reason = r; };
            Action<int> onFail = f => failedOn = f;
            d.DiveEnded += onEnd; d.DiveFailedOn += onFail;
            var bot = new DiveBot(Rig.Input, Rig.Hero.transform) { Tough = Tough, GoHomeAtBank = true, TownExternal = true };
            double deadline = Time.realtimeSinceStartupAsDouble + RealLimit;
            int lastFloor = 0, frame = 0, tail = 0;
            Time.captureFramerate = 60;
            try
            {
                while (true)
                {
                    if (bot.Seconds > GameLimit || Time.realtimeSinceStartupAsDouble > deadline) { rec.TimedOut = true; Say($"dive {rec.Number}: time limit at {bot.Seconds:0} s of play, floor {bot.Floor}"); break; }
                    bot.Tick(Step);
                    ExtraTick?.Invoke(Step);
                    if (bot.Floor > 0) lastFloor = bot.Floor;
                    rec.MaxFloor = Mathf.Max(rec.MaxFloor, bot.Floor);
                    if (++frame % 6 == 0) TrackStock(bot.Floor > 0 ? bot.Floor : lastFloor, rec, bot.Seconds);
                    if (ended) { if (++tail >= 3) break; }   // a few more ticks: the bot's own bookkeeping sees the dive end
                    else if (StopWhenFrameHome && FrameHome && bot.Floor > 0 && bot.Floor != 2) { rec.StoppedEarly = true; Say($"dive {rec.Number}: frame home, stopped on entering floor {bot.Floor} after {bot.Seconds:0} s"); break; }
                    yield return null;
                }
            }
            finally
            {
                d.DiveEnded -= onEnd; d.DiveFailedOn -= onFail;
                Rig.Input.Frame = default;
                Time.captureFramerate = 0;
            }
            rec.Seconds = bot.Seconds;
            rec.End = ended ? reason : null;
            rec.EndFloor = failedOn > 0 ? failedOn : lastFloor;
            if (ended && reason == DiveEndReason.Failed) DeathsByFloor[rec.EndFloor] = (DeathsByFloor.TryGetValue(rec.EndFloor, out int n) ? n : 0) + 1;
            TrackStock(lastFloor, rec, bot.Seconds);
            bot.Dispose();
            rec.FrameHomeAtEnd = FrameHome;
            Say($"dive {rec.Number}: {(ended ? reason.ToString() : rec.StoppedEarly ? "stopped early" : "timed out")} on floor {rec.EndFloor} after {rec.Seconds:0} s, deepest {rec.MaxFloor}, found [{string.Join(", ", rec.Found)}], frame home {rec.FrameHomeAtEnd}");
            if (ended)
            {
                HeroVitals v = Rig.Hero.GetComponent<HeroVitals>();
                yield return PlayModeHarness.WaitUntil(() => !d.NetActive.Value && Rig.Hero.transform.position.y > AreaLook.Current.CellarBelowY && !d.HasPendingTeleport, TownInputRig.Timeout, "[loop] the hero never landed back in the town");
                yield return PlayModeHarness.WaitUntil(() => v.IsUp, TownInputRig.Timeout, "[loop] the hero is still down in the town after the dive");
                yield return TownInputRig.Seconds(0.5f);
            }
        }

        void TrackStock(int floor, DiveRecord rec, float t)
        {
            TownState s = Rig.Director.Town.State;
            foreach (int p in s.StockPieces)
                if (_known.Add(p)) { PieceFloor[p] = floor; rec.Found.Add($"piece {p} on F{floor} at {t:0} s"); }
            if (!_bakerKnown && s.ResidentInStock(TownBuildings.BakerResident)) { _bakerKnown = true; BakerFloor = floor; rec.Found.Add($"baker on F{floor} at {t:0} s"); }
        }

        // ------------------------------------------------------------------------------------------------ the build point
        // Interact at the build point, aim at F02 (the lot the QA walk uses), then one Primary per step the placement steps name, until nothing is left (or only the frame with frameOnly). Pause leaves and saves.
        public IEnumerator PlaceStock(bool frameOnly = false)
        {
            Assert.That(PlacementSteps.Next(Rig.Town.View, TownBuildings.Bakery).Kind, Is.Not.EqualTo(PlaceStepKind.None), "[loop] nothing in stock can be placed");
            yield return Rig.EnterPlacement();
            yield return Rig.AimAt("F02");
            for (int guard = 0; guard < 8; guard++)
            {
                PlaceStep step = PlacementSteps.Next(Rig.Town.View, TownBuildings.Bakery);
                if (step.Kind == PlaceStepKind.None) break;
                string before = Sig(Rig.Town.View);
                yield return Rig.PressPrimary();
                yield return PlayModeHarness.WaitUntil(() => Sig(Rig.Town.View) != before, TownInputRig.Timeout, "[loop] Primary never placed the " + step.Kind + " " + step.Part + " (message '" + Rig.Mode.Message + "')");
                Say($"placed {step.Kind} {step.Part}");
                if (frameOnly && step.Kind == PlaceStepKind.Frame) break;
            }
            yield return Rig.PressPause();
            yield return PlayModeHarness.WaitUntil(() => !Rig.Mode.Active && Rig.Town.BuilderIsFree, TownInputRig.Timeout, "[loop] Pause never left placement");
        }

        // ------------------------------------------------------------------------------------------------ leave and come back (solo)
        public IEnumerator LeaveAndReopen(Action<TownInputRig> done)
        {
            string worldId = Rig.App.WorldSaveFlow.SelectedWorldId;
            string saveDir = Rig.SaveDir;
            Rig.App.WorldSaveFlow.SaveNow();
            yield return PlayModeHarness.LeaveAndWaitForLobby();
            TownInputRig again = null;
            yield return TownInputRig.Reopen(r => again = r, saveDir);
            Assert.That(again.App.WorldSaveFlow.SelectedWorldId, Is.EqualTo(worldId), "[loop] the same world reopened");
            done(again);
        }
    }
}
