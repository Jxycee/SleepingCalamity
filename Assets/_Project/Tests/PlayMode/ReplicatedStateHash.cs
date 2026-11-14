// Authority: test-only (reads state, never writes). M7 S1: one peer's view of the replicated dive / town / hero state as canonical text and a hash of it.
// Every peer (host or client) must produce the same text once replication has caught up; AssertAllEqual polls until they agree, else fails with the differing lines.
// Covers (by reflection over each component's public NetworkVariable fields, so a new field is covered without editing this file):
//   DiveDirector (NetActive, NetHeartBanked, NetCheckpointHeld, NetPickableMaxFloor, NetStairOpen, NetHome/HubStair/Origin), DiveHaulSync (Net = the Summary, NetHealSecondsLeft),
//   CellarFloorSync (Net seed/floor + the peer's BUILT seed, floor and layout hash), CellarContentsSync (Net = ContentsState), TownSync (NetTown signature, NetBuilder, NetGhost, NetBakerMet),
//   HeroVitals (Hp, state, downed/revive, loaves) and HeroCombat.NetWeapon per player object, the player-object roster, and the set of spawned NetworkObject ids.
// ALSO (separate calls, because peers legitimately differ; the public API above is unchanged and source-compatible): BagMismatch (each peer's own owner-only PlayerBagSync view must equal BagView.From the HOST's
//   authoritative PlayerBag of that client), StirMismatch (the host's published Summary stir fields must equal a fresh DiveStir recomputed from the host's bags, the session's cleared floors across the dive's
//   halves, the connected players and the floor's Stirred bonus), AssertBagsAndStirMatchHost (both, polled), DescribeLocal (own bag view + the host's authoritative bags and stir, for messages).
// NOT covered: enemy state (moves every frame), transforms, owner-written vars (stance, swing), host-only state other than the bags and the stir (momentum, retry, objectives), visuals. A bag view only reaches a peer
//   whose hero exists (PlayerBagSync lives on the player object), so a peer with no hero is skipped by BagMismatch.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Tests.PlayMode
{
    public static class ReplicatedStateHash
    {
        private static readonly Type[] Covered = { typeof(DiveDirector), typeof(DiveHaulSync), typeof(CellarFloorSync), typeof(CellarContentsSync), typeof(TownSync), typeof(HeroVitals), typeof(HeroCombat) };
        private static readonly HashSet<string> OwnerWritten = new HashSet<string> { "NetSwing" };   // written by the owner every swing; not part of the shared world state

        // Canonical text, one fact per line, sorted, so two peers' texts diff line by line.
        public static string Describe(NetworkManager nm)
        {
            var lines = new List<string>();
            if (nm == null || !nm.IsListening) return "(not listening)";
            var players = new List<string>();
            var ids = new List<ulong>();
            foreach (KeyValuePair<ulong, NetworkObject> kv in nm.SpawnManager.SpawnedObjects)
            {
                NetworkObject o = kv.Value;
                if (o == null) continue;
                ids.Add(kv.Key);
                if (o.IsPlayerObject) players.Add("client " + o.OwnerClientId + " obj " + kv.Key);
                foreach (Type t in Covered)
                {
                    Component c = o.GetComponent(t);
                    if (c == null) continue;
                    string key = t.Name + "#" + kv.Key + (t == typeof(HeroVitals) || t == typeof(HeroCombat) ? "(owner " + o.OwnerClientId + ")" : "");
                    foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (!typeof(NetworkVariableBase).IsAssignableFrom(f.FieldType) || OwnerWritten.Contains(f.Name)) continue;
                        lines.Add(key + "." + f.Name + " = " + Canon(ValueOf(f.GetValue(c))));
                    }
                    if (c is CellarFloorSync fs) lines.Add(key + ".Built = seed " + fs.BuiltSeed + " floor " + fs.BuiltFloor + " layout " + fs.BuiltLayoutHash);
                }
            }
            players.Sort(StringComparer.Ordinal);
            ids.Sort();
            lines.Add("players = [" + string.Join("; ", players) + "]");
            lines.Add("spawned ids = [" + string.Join(",", ids) + "]");
            lines.Sort(StringComparer.Ordinal);
            return string.Join("\n", lines);
        }

        public static string Hash(NetworkManager nm)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(Describe(nm)));
                var sb = new StringBuilder();
                for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }

        // ---- per-peer views that legitimately differ (bags, the host's stir internals) ----
        // Describe/Hash/AssertAllEqual compare what EVERY peer should see identically, so they cannot hold a player's bag: PlayerBagSync.Net is owner-only (a peer sees only its own bag, the host sees all).
        // The bag and stir checks below compare each peer against the HOST instead: a peer's own BagView must equal BagView.From(the host's PlayerBag for that owner), and the published stir
        // (DiveHaulSync.Summary) must equal a DiveStir recomputed from the host's bags, the dive's cleared floors and the connected players. Hash/Describe are unchanged (cross-peer equal).

        // This peer's own bag view, and for the host also the authoritative bag of every connected player and the stir inputs, as canonical lines (for failure messages and hand inspection).
        public static string DescribeLocal(NetworkManager nm)
        {
            var lines = new List<string>();
            if (nm == null || !nm.IsListening) return "(not listening)";
            PlayerBagSync own = PlayerBagSync.ForLocalPlayer(nm);
            lines.Add("own bag view (client " + nm.LocalClientId + ") = " + (own == null ? "none" : Canon(own.View)));
            DiveHaulSync haul = HostHaul(nm);
            if (haul != null)
            {
                foreach (ulong cid in nm.ConnectedClientsIds)
                {
                    PlayerBag bag = haul.ServerBagOf(cid);
                    lines.Add("host bag of client " + cid + " = " + (bag == null ? "none" : Canon(BagView.From(bag))));
                }
                DiveStir.State st = haul.Session.Stir.Capture();
                lines.Add("host stir state = floorMask " + st.FloorMask + " valuables " + st.Valuables + " baseline " + st.ValuablesBaseline + " secondHalf " + st.SecondHalf + " extra " + st.Extra + " hunter " + st.Hunter
                    + " collapseUsed " + st.CollapseUsed + " running " + st.Running + " secondsLeft " + st.SecondsLeft + " rewardCarry " + st.RewardCarry + "; carried valuables " + haul.Session.Bags.CarriedValuables() + " cleared mask " + haul.Session.Snapshot().ClearedMask + " published " + Canon(haul.Current));
            }
            return string.Join("\n", lines);
        }

        private static DiveHaulSync HostHaul(NetworkManager nm)
        {
            if (nm == null || !nm.IsServer || !nm.IsListening) return null;
            foreach (NetworkObject o in nm.SpawnManager.SpawnedObjects.Values)
            {
                DiveHaulSync h = o != null ? o.GetComponent<DiveHaulSync>() : null;
                if (h != null && h.Session != null) return h;
            }
            return null;
        }

        // Null when every peer's own bag view equals the host's authoritative bag for that peer's client id; else the differing lines. The host peer must be in the list (without it: null, nothing to compare).
        public static string BagMismatch(IEnumerable<NetworkManager> peers)
        {
            var list = new List<NetworkManager>(peers);
            DiveHaulSync haul = null;
            foreach (NetworkManager nm in list) { haul = HostHaul(nm); if (haul != null) break; }
            if (haul == null) return null;
            var sb = new StringBuilder();
            foreach (NetworkManager nm in list)
            {
                PlayerBagSync s = PlayerBagSync.ForLocalPlayer(nm);
                if (s == null) continue;
                PlayerBag bag = haul.ServerBagOf(nm.LocalClientId);
                BagView expected = BagView.From(bag);
                if (!s.View.Equals(expected))
                    sb.AppendLine("bag of client " + nm.LocalClientId + ": " + Label(nm) + " sees " + Canon(s.View) + " but the host's bag is " + Canon(expected));
            }
            return sb.Length == 0 ? null : sb.ToString();
        }

        // Null when the host's published stir (Summary) equals a fresh DiveStir recomputed from the host's bags (valuables), the session's cleared floors, the connected players and the floor's Stirred
        // modifier (the same inputs DiveHaulSync.FeedStir uses); the baseline, half, reward carry, latches and collapse clock are taken from the live stir (nothing else holds them). Catches a bag
        // and the stir drifting apart, or a floor the stir counted that the session did not (or the reverse).
        public static string StirMismatch(NetworkManager hostNm)
        {
            DiveHaulSync haul = HostHaul(hostNm);
            if (haul == null) return null;
            DiveSession ses = haul.Session;
            DiveStir.State st = ses.Stir.Capture();
            var fresh = new DiveStir();
            fresh.Restore(new DiveStir.State(0, 0, st.ValuablesBaseline, st.SecondHalf, st.Extra, st.Hunter, st.CollapseUsed, st.Running, st.SecondsLeft, st.RewardCarry));
            int mask = ses.Snapshot().ClearedMask;
            for (int f = UndercroftPlan.FirstFloor; f <= UndercroftPlan.BossFloor; f++) if ((mask & (1 << (f - 1))) != 0) fresh.OnFloorCleared(f);
            bool inDive = ses.Phase == DivePhase.Floor || ses.Phase == DivePhase.BankRoom;
            fresh.PlayersPresent = Mathf.Max(1, hostNm.ConnectedClientsIds.Count);
            fresh.UnbankedValuables = inDive ? ses.Bags.CarriedValuables() : st.Valuables;
            float bonus = 0f;
            if (ses.Phase == DivePhase.Floor)
            {
                int floor = haul.Current.CurrentFloor;
                CellarFloorSync fs = null;
                foreach (NetworkObject o in hostNm.SpawnManager.SpawnedObjects.Values) { fs = o != null ? o.GetComponent<CellarFloorSync>() : null; if (fs != null) break; }
                uint seed = fs != null && fs.Built != null && fs.BuiltFloor == floor ? fs.BuiltSeed : haul.DiveSeed;
                if (FloorModifierSet.For(seed, floor).Has(FloorModifier.Stirred)) bonus = FloorModifierTuning.StirredBonus;
            }
            fresh.ModifierBonus = bonus;

            DiveHaulSync.Summary sm = haul.Current;
            var sb = new StringBuilder();
            if (sm.StirValue != Mathf.RoundToInt(fresh.Value)) sb.AppendLine("StirValue published " + sm.StirValue + " but recomputed " + Mathf.RoundToInt(fresh.Value) + " (floors cleared mask " + mask + ", valuables " + (inDive ? ses.Bags.CarriedValuables() : st.Valuables) + ", players " + fresh.PlayersPresent + ")");
            if ((sm.ExtraEnemiesPerRoom > 0) != fresh.ExtraEnemyLatched) sb.AppendLine("ExtraEnemiesPerRoom published " + sm.ExtraEnemiesPerRoom + " but the recomputed latch is " + fresh.ExtraEnemyLatched);
            if (sm.HunterLatched != fresh.HunterLatched) sb.AppendLine("HunterLatched published " + sm.HunterLatched + " but recomputed " + fresh.HunterLatched);
            if (sm.RewardRankBonus != fresh.RewardRankBonus) sb.AppendLine("RewardRankBonus published " + sm.RewardRankBonus + " but recomputed " + fresh.RewardRankBonus);
            if (sm.DeepClosesLatched != fresh.CollapseRunning) sb.AppendLine("DeepClosesLatched published " + sm.DeepClosesLatched + " but recomputed " + fresh.CollapseRunning);
            if (inDive && st.Valuables != ses.Bags.CarriedValuables()) sb.AppendLine("the stir holds " + st.Valuables + " valuables but the bags carry " + ses.Bags.CarriedValuables());
            return sb.Length == 0 ? null : sb.ToString();
        }

        // Polls (paced, bounded) until BagMismatch and StirMismatch are both clean (host publish lag), else fails with what differs. peers must include the host.
        public static IEnumerator AssertBagsAndStirMatchHost(IEnumerable<NetworkManager> peers, string step, float timeoutSeconds = 20f)
        {
            var list = new List<NetworkManager>(peers);
            NetworkManager host = list.Find(p => p.IsServer);
            Assert.That(host, Is.Not.Null, step + ": the host must be one of the peers");
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            string failure;
            while ((failure = BagMismatch(list) + StirMismatch(host)) != "")
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail("[" + step + "] bags / stir differ from the host after " + timeoutSeconds + " s:\n" + failure + "\n" + DescribeLocal(host));
                PlayModeHarness.Pace();
                yield return null;
            }
        }

        // The differing lines between two described states ("" when equal).
        public static string Diff(string label, string a, string b, string labelB)
        {
            var sa = new HashSet<string>(a.Split('\n')); var sb = new HashSet<string>(b.Split('\n'));
            var sbd = new StringBuilder();
            foreach (string l in a.Split('\n')) if (!sb.Contains(l)) sbd.Append("  only ").Append(label).Append(": ").AppendLine(l);
            foreach (string l in b.Split('\n')) if (!sa.Contains(l)) sbd.Append("  only ").Append(labelB).Append(": ").AppendLine(l);
            return sbd.ToString();
        }

        // Polls (paced, bounded) until every peer's description equals the first peer's (replication lag), then fails with the diff if they never do.
        public static IEnumerator AssertAllEqual(IEnumerable<NetworkManager> peers, string step, float timeoutSeconds = 20f)
        {
            var list = new List<NetworkManager>(peers);
            Assert.That(list.Count, Is.GreaterThanOrEqualTo(2), step + ": need at least two peers to compare");
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            string failure;
            while ((failure = Mismatch(list)) != null)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail("[" + step + "] replicated state differs between peers after " + timeoutSeconds + " s:\n" + failure);
                PlayModeHarness.Pace();
                yield return null;
            }
        }

        private static string Mismatch(List<NetworkManager> list)
        {
            string first = Describe(list[0]);
            var sb = new StringBuilder();
            for (int i = 1; i < list.Count; i++)
            {
                string other = Describe(list[i]);
                if (other != first) sb.AppendLine("peer 0 (" + Label(list[0]) + ") vs peer " + i + " (" + Label(list[i]) + "):").Append(Diff("peer0", first, other, "peer" + i));
            }
            return sb.Length == 0 ? null : sb.ToString();
        }

        private static string Label(NetworkManager nm) => nm.IsServer ? "host" : "client " + nm.LocalClientId;

        private static object ValueOf(object networkVariable)
        {
            if (networkVariable == null) return null;
            PropertyInfo p = networkVariable.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            return p == null ? null : p.GetValue(networkVariable);
        }

        private static string Canon(object v)
        {
            var sb = new StringBuilder();
            Write(sb, v, 0);
            return sb.ToString().Replace("\r", "").Replace("\n", "\\n");
        }

        private static void Write(StringBuilder sb, object v, int depth)
        {
            if (v == null) { sb.Append("null"); return; }
            Type t = v.GetType();
            if (depth > 6) { sb.Append("..."); return; }
            if (v is float f) { sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); return; }
            if (v is double d) { sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return; }
            if (v is Vector3 vec) { sb.Append('(').Append(vec.x.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(vec.y.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(vec.z.ToString("R", CultureInfo.InvariantCulture)).Append(')'); return; }
            if (t.IsPrimitive || t.IsEnum || v is string) { sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); return; }
            if (t.Name.StartsWith("FixedString", StringComparison.Ordinal)) { sb.Append(v.ToString()); return; }
            MethodInfo sig = t.GetMethod("Signature", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (sig != null && sig.ReturnType == typeof(string)) { sb.Append("sig:").Append((string)sig.Invoke(v, null)); return; }
            if (v is System.Collections.IEnumerable en)
            {
                sb.Append('[');
                bool first = true;
                foreach (object item in en) { if (!first) sb.Append(','); first = false; Write(sb, item, depth + 1); }
                sb.Append(']');
                return;
            }
            // TownNet-style wrappers expose their payload through a public field; plain structs list their public fields in declaration order.
            FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
            if (fields.Length == 0) { sb.Append(v.ToString()); return; }
            sb.Append('{');
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(fields[i].Name).Append('=');
                Write(sb, fields[i].GetValue(v), depth + 1);
            }
            sb.Append('}');
        }

        // Test hook for the corruption case: overwrites THIS peer's local copy of a replicated NetworkVariable without marking it dirty, so nothing replicates the change back.
        public static void CorruptLocalCopy(NetworkVariableBase variable, object newValue)
        {
            for (Type t = variable.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo fi = t.GetField("m_InternalValue", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi == null) continue;
                fi.SetValue(variable, newValue);
                return;
            }
            Assert.Fail("CorruptLocalCopy: no m_InternalValue on " + variable.GetType().Name + " (NGO changed its layout; update the hook)");
        }
    }
}
