// Authority: host. A hero's life and downed state (docs/PHASE1.md 6.5, P24). The host holds the Core PlayerVitals and writes the NetworkVariables;
// everyone reads them (HP bars, the downed overlay). HeroNetState is owner-write only, which is why this is its own component. The host also runs the
// downed timer and the revive: an ally's client reports "I am holding revive on X" every 0.2 s (HeroCombat), and the host checks the reach and the 3 s.
using System.Collections.Generic;
using SleepingCalamity.Core.Combat;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    public sealed class HeroVitals : NetworkBehaviour
    {
        public const float ReviveReach = 3.0f;
        private const float HoldTimeout = 0.45f;   // a hold report older than this no longer counts (reports come every 0.2 s)

        private static readonly List<HeroVitals> s_all = new List<HeroVitals>();
        public static IReadOnlyList<HeroVitals> All => s_all;

        public readonly NetworkVariable<int> NetHp = new NetworkVariable<int>(PlayerVitals.BaseMaxHp, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<byte> NetState = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<float> NetDownedLeft = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<float> NetReviveProgress = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public PlayerVitals Model { get; private set; } = new PlayerVitals();   // meaningful on the host only
        public int MaxHp => PlayerVitals.BaseMaxHp;
        public int Hp => NetHp.Value;
        public VitalState State => (VitalState)NetState.Value;
        public bool IsUp => State == VitalState.Up;
        public bool IsDowned => State == VitalState.Downed;
        public bool IsOut => State == VitalState.Out;
        public float DownedLeft => NetDownedLeft.Value;
        public float ReviveProgress => NetReviveProgress.Value;
        public System.Action<HeroVitals> ServerWentDown;

        private double _holdUntil;          // host: a reviver's hold report is valid until this time
        private float _syncTimer;

        private void OnEnable() { if (!s_all.Contains(this)) s_all.Add(this); }
        private void OnDisable() { s_all.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_all.Clear(); }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Model = new PlayerVitals();
                Push();
            }
        }

        private void Push()
        {
            NetHp.Value = Model.Hp;
            NetState.Value = (byte)Model.State;
            NetDownedLeft.Value = Model.DownedLeft;
            NetReviveProgress.Value = Model.ReviveProgress;
        }

        // Host: returns the HP lost.
        public int ServerDamage(int amount)
        {
            if (!IsServer) return 0;
            int lost = Model.Damage(amount, out bool downed);
            if (lost > 0)
            {
                Push();
                if (downed) ServerWentDown?.Invoke(this);
            }
            return lost;
        }

        public int ServerHeal(int amount)
        {
            if (!IsServer) return 0;
            int gain = Model.Heal(amount);
            if (gain > 0) Push();
            return gain;
        }

        // Host: an out hero stands up with the revive HP (the arena only; a dive keeps its out rule).
        public bool ServerReviveFromOut()
        {
            if (!IsServer || Model.State != VitalState.Out) return false;
            Model.Revive();
            Push();
            return true;
        }

        public void ServerFullReset()
        {
            if (!IsServer) return;
            Model.FullReset();
            Push();
        }

        // Host: a reviver's client says it is holding the revive on this player.
        public void ServerReviveHold(HeroVitals reviver)
        {
            if (!IsServer || !IsDowned || reviver == null || reviver == this || !reviver.IsUp) return;
            // The reviver's own client checked ReviveReach against positions that are a round trip apart from the host's: a margin on the host's re-check.
            const float hostReach = ReviveReach + 0.5f;
            if ((reviver.transform.position - transform.position).sqrMagnitude > hostReach * hostReach) return;
            _holdUntil = Time.timeAsDouble + HoldTimeout;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || Model.State != VitalState.Downed) return;
            bool holding = Time.timeAsDouble < _holdUntil;
            bool revived = Model.Tick(Time.deltaTime, holding);
            _syncTimer -= Time.deltaTime;
            if (revived || Model.State != VitalState.Downed || _syncTimer <= 0f)
            {
                _syncTimer = 0.2f;
                Push();
            }
        }
    }
}
