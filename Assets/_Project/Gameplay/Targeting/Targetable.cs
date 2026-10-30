// Not networked (TargetDummy, a NetworkBehaviour, feeds IsElite from the host). Anything the soft target and the hard lock can pick.
// A static registry of live targets; owner-side lock code reads it every frame. Reset on SubsystemRegistration for domain-reload-off play.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    public sealed class Targetable : MonoBehaviour
    {
        private static readonly List<Targetable> s_all = new List<Targetable>();
        public static IReadOnlyList<Targetable> All => s_all;

        [SerializeField] private float aimHeight = 1.1f;

        private static int s_nextId = 1;

        private bool _isElite;

        // The point rays and the camera aim at (above the feet).
        public Vector3 AimPoint => transform.position + Vector3.up * aimHeight;
        public Transform AimTransform => transform;   // the camera's target group uses the root; AimPoint is for line of sight
        public int Id { get; private set; }   // unique per live target in this process (GetInstanceID is obsolete in Unity 6.6)
        public bool Alive { get; set; } = true;

        // Elites and bosses never lose a lock to range or sight (TargetLock). Set by the host-owned TargetDummy (and, from M3, by enemy tier).
        public bool IsElite
        {
            get => _isElite;
            set
            {
                if (_isElite == value) return;
                _isElite = value;
                EliteChanged?.Invoke(value);
            }
        }

        public event Action<bool> EliteChanged;

        private void Awake()
        {
            Id = s_nextId++;
        }

        private void OnEnable()
        {
            if (!s_all.Contains(this)) s_all.Add(this);
        }

        private void OnDisable()
        {
            s_all.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_all.Clear();
            s_nextId = 1;
        }
    }
}
