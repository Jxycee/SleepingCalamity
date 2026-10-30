// Authority: host. A healing pickup dropped by a broken boss part (or a healing point later): the host heals the first living, hurt hero that walks into it
// and despawns it. Clients only see it (a bobbing greybox sphere). Spawned by CombatHost.
using SleepingCalamity.Core.Combat;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    public sealed class HealingDrop : NetworkBehaviour
    {
        public const float PickupRadius = 1.4f, Lifetime = 45f;

        public readonly NetworkVariable<int> NetAmount = new NetworkVariable<int>(25, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float _age;
        private Vector3 _basePos;
        private Transform _visual;   // the prefab's "Visual" child: the bob and spin move this, never the networked root (the server's NetworkTransform owns that)

        private void Awake()
        {
            _visual = transform.Find("Visual");
        }

        public override void OnNetworkSpawn()
        {
            _basePos = transform.position;
        }

        public void ServerInit(int amount) { NetAmount.Value = amount; }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_visual != null)
            {
                _visual.localPosition = Vector3.up * (0.12f * Mathf.Sin(_age * 3f));
                _visual.localRotation = Quaternion.Euler(0f, _age * 90f, 0f);
            }
            if (!IsServer || !IsSpawned) return;
            if (_age > Lifetime) { NetworkObject.Despawn(true); return; }
            var all = HeroVitals.All;
            for (int i = 0; i < all.Count; i++)
            {
                HeroVitals v = all[i];
                if (v == null || !v.IsSpawned || v.NetworkManager != NetworkManager || !v.IsUp || v.Hp >= v.MaxHp) continue;
                Vector3 d = v.transform.position - _basePos;
                d.y = 0f;
                if (d.sqrMagnitude > PickupRadius * PickupRadius) continue;
                int healed = v.ServerHeal(NetAmount.Value);
                if (healed > 0)
                {
                    HeroCombat c = v.GetComponent<HeroCombat>();
                    if (c != null) c.ServerBroadcastHeal(healed);
                    NetworkObject.Despawn(true);
                    return;
                }
            }
        }
    }
}
