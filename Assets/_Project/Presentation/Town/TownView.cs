// Authority: none (presentation). M6 S3 minimal runtime town view: the builder emits the bakery's frame and parts as greybox groups hidden on lot F02 (HollowmereBuilder/HollowmereHub),
// and this component shows them from a TownState: the frame when a building stands on the lot (turned to its door side), each part when its piece is slotted, the baker when he has moved
// in, and it hides the lot's ruin while a building stands there. The state source is the host's town stock (DiveDirector.Instance.Town); a client has none until S4's TownSync replicates
// placements, so a client shows the default town (nothing placed), which is the town every peer starts from. Nothing can be placed before S4, so the views agree.
using System;
using SleepingCalamity.Core;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class TownView : MonoBehaviour
    {
        [Serializable]
        public sealed class Lot
        {
            public string FoundationId;
            public GameObject Ruin;         // the lot's ruin group (hidden while a building stands here)
            public Transform Root;          // turned to the door side
            public GameObject[] Parts;      // indexed like BuildingDef.Parts (0 = frame)
            public GameObject Resident;
            public float LotYaw;            // Foundation.Yaw
            public LotSide LotFacing;       // Foundation.Facing
        }

        public Lot[] Lots = new Lot[0];
        public Func<TownState> Source;      // tests; null = the host's DiveDirector town stock
        private string _shown = "";
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.25f;
            TownState state = null;
            if (Source != null) state = Source();
            else
            {
                NetworkManager nm = NetworkManager.Singleton;
                DiveDirector director = DiveDirector.Instance;
                if (nm != null && nm.IsServer && director != null && director.IsSpawned) state = director.Town.State;
            }
            Show(state);
        }

        // Shows `state` (null = the default town: nothing placed). Cheap when nothing changed.
        public void Show(TownState state)
        {
            string sig = Signature(state);
            if (sig == _shown) return;
            _shown = sig;
            foreach (Lot lot in Lots)
            {
                PlacedBuilding b = state != null ? state.OccupantOf(lot.FoundationId) : null;
                BuildingDef def = b != null ? TownBuildings.Get(b.BuildingId) : null;
                if (lot.Ruin != null) lot.Ruin.SetActive(b == null);
                if (lot.Root != null)
                {
                    lot.Root.gameObject.SetActive(b != null);
                    if (b != null) lot.Root.localRotation = Quaternion.Euler(0f, YawOf(lot, b.Turn), 0f);
                }
                for (int i = 0; i < lot.Parts.Length; i++)
                    if (lot.Parts[i] != null) lot.Parts[i].SetActive(b != null && def != null && i < b.PiecesByPart.Length && b.PiecesByPart[i] >= 0);
                if (lot.Resident != null) lot.Resident.SetActive(b != null && b.Resident != null);
            }
        }

        // The yaw of the lot frame with the door on the side `turn` quarter turns from the lot's default (the same formula as Foundation.FacingYaw).
        public static float YawOf(Lot lot, int turn)
        {
            LotSide side = (LotSide)(((int)lot.LotFacing + WishEvaluator.NormalTurn(turn)) & 3);
            return lot.LotYaw + (side == LotSide.North ? 180f : side == LotSide.East ? 270f : side == LotSide.South ? 0f : 90f);
        }

        private static string Signature(TownState state)
        {
            if (state == null) return "-";
            var sb = new System.Text.StringBuilder();
            foreach (PlacedBuilding b in state.Placed)
            {
                sb.Append(b.BuildingId).Append('@').Append(b.FoundationId).Append('/').Append(b.Turn).Append('/');
                foreach (int p in b.PiecesByPart) sb.Append(p >= 0 ? '1' : '0');
                sb.Append('/').Append(b.Resident ?? "").Append(';');
            }
            return sb.ToString();
        }
    }
}
