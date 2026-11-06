// Core/Rooms/EncounterValidator.cs: the rules a RoomEncounter must satisfy (encounter templates plan, Task 4). Pure C#. Each rule returns a message; empty list = clean.
// Authority: none (static data).
using System;
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Core
{
    public static class EncounterValidator
    {
        public static List<string> Validate(RoomDataV2 room, RoomEncounter e, int rosterMask, bool isAmbushRoom = false)
        {
            var errs = new List<string>(); string who = room.Base.Id + "/" + e.TemplateId + " floor " + e.Floor + " players " + e.Players;
            EncounterTemplate t = EncounterTemplates.Find(e.TemplateId);
            if (t == null) { errs.Add(who + ": unknown template"); return errs; }
            if (e.RoomId != room.Base.Id) errs.Add(who + ": encounter belongs to " + e.RoomId);
            var state = new int[room.Regions.Count];
            foreach (var g in e.Spawns.GroupBy(s => s.Wave))
            {
                if (g.Count() > GameMetrics.MaxLiveEnemies) errs.Add(who + ": " + g.Count() + " live in wave " + g.Key + " (max " + GameMetrics.MaxLiveEnemies + ")");
                if (g.Select(s => (s.Cell.X, s.Cell.Y)).Distinct().Count() != g.Count()) errs.Add(who + ": two spawns on one cell in wave " + g.Key);
            }
            foreach (PlannedSpawn s in e.Spawns)
            {
                if (!room.Base.InBounds(s.Cell.X, s.Cell.Y) || !room.Standable(s.Cell.X, s.Cell.Y, state)) errs.Add(who + ": spawn at " + s.Cell + " is not a standable floor cell");
                else if (room.Base.Doors.Any(d => d.X == s.Cell.X && d.Y == s.Cell.Y)) errs.Add(who + ": spawn at " + s.Cell + " is on a door cell");
                if (s.Kind == EnemyKind.DoughWarden || s.Tier != SpawnTier.Tier1) errs.Add(who + ": " + s.Kind + " is above tier 1");
                if ((CellarFloorRoster.BitOf(s.Kind) & rosterMask) == 0) errs.Add(who + ": " + s.Kind + " is off the floor roster");
                if (!EnemyHabitat.Fits(s.Kind, room)) errs.Add(who + ": " + s.Kind + " does not fit the room");
                if (s.Wave < 0 || s.Wave >= EncounterTemplate.MaxWaves) errs.Add(who + ": wave " + s.Wave + " is out of range");
            }
            if (e.PeakLive > GameMetrics.MaxLiveEnemies) errs.Add(who + ": peak live " + e.PeakLive + " is over " + GameMetrics.MaxLiveEnemies);
            if (e.WaveCount > EncounterTemplate.MaxWaves) errs.Add(who + ": " + e.WaveCount + " waves (max " + EncounterTemplate.MaxWaves + ")");
            if (e.LiveIn(0) == 0) errs.Add(who + ": wave 0 is empty");
            if (e.PeakLive > 0 && !FightSpace.Fits(room.Base, e.HeldHeroes, e.PeakLive)) errs.Add(who + ": " + e.PeakLive + " enemies and " + e.HeldHeroes + " heroes do not fit the room's FightSpace");
            int maxKinds = e.Floor <= 2 ? 2 : 3;
            if (e.Spawns.Select(s => s.Kind).Distinct().Count() > maxKinds) errs.Add(who + ": more than " + maxKinds + " kinds on floor " + e.Floor);
            if (e.Floor <= 2 && e.WaveCount > 1) errs.Add(who + ": floors 1-2 allow one wave only");
            if ((room.Affordances & t.Wants) != t.Wants) errs.Add(who + ": the room lacks " + t.Wants + " the template wants");
            if (t.IsAmbush && !isAmbushRoom) errs.Add(who + ": Ambush template outside the Ambush room");
            if (t.IsAmbush && room.Regions.Count > 0) errs.Add(who + ": Ambush template in a room with a hazard region");
            // plan guardrails: solo group <= today's plan count (authored spawns) + 1; extra players add at most CoopScaling.ExtraEnemiesPerRoom
            if (t.SoloPeak > room.Base.Spawns.Count + 1) errs.Add(who + ": solo group of " + t.SoloPeak + " is over the authored spawn count " + room.Base.Spawns.Count + " + 1");
            if (e.LiveIn(0) > t.BaseInWave(0) + CoopScaling.ExtraEnemiesPerRoom(e.HeldHeroes)) errs.Add(who + ": more adds than CoopScaling.ExtraEnemiesPerRoom allows");
            return errs;
        }
    }
}
