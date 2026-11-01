// Authority: none (presentation, local peer). M6 S4 placement mode. A scene marker by the bakery lot (HollowmereHub builds it): near the build point the interact key asks the host for the builder's
// lock (TownSync, one builder at a time); once the host grants it this peer enters InputContext.Placement (the hero stands still, the input router passes only the placement buttons) and the
// placement camera takes over, a fixed top-down-ish view over the lot. The bakery (the only M6 building) is held as a ghost on the free foundations of its size class: the switch input moves to
// the neighbouring lot, interact cycles to the next free lot (Hollowmere has no building colliders: lots are chosen from the layout, never by a raycast), jump turns it a quarter, primary places
// the next step (frame, then oven/door/sign, then the baker), secondary picks up the last thing placed, pause leaves (which releases the lock; the host saves). The ghost is green when
// TownState.CanPlaceFrame is Ok on the replicated town, red otherwise. Every request is only a prompt: the host re-validates (TownSync). Watchers (everyone else) see the same ghost (NetGhost)
// and the same wish ticks, computed locally with WishEvaluator from that pose, plus the placed building's wishes after it stands.
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class PlacementMode : MonoBehaviour
    {
        public Transform BuildPoint;           // where the party stands to open placement (the bakery lot's doorstep / Wren's list wall)
        public float PromptRadius = 4f;
        public CinemachineCamera PlacementCam; // disabled until placing; a fixed pitch, positioned over the lot by this component
        public Transform Ghost;                // a box scaled to the lot, recoloured green/red
        public Renderer[] GhostRenderers = new Renderer[0];
        public string BuildingId = TownBuildings.Bakery;

        public const float CamHeight = 26f, CamBack = 17f, CamPitch = 57f, CamLerp = 6f;
        private static readonly Color Green = new Color(0.25f, 0.85f, 0.35f, 0.55f), Red = new Color(0.9f, 0.25f, 0.2f, 0.55f);

        public bool Active { get; private set; }
        public string AimLot => _lot;
        public int AimTurn => _turn;
        public string Message => _message;

        private string _lot = "";
        private int _turn;
        private bool _wantLock;
        private float _lockAskedAt, _messageUntil, _nextGhostSend, _flickReleased = 1f;
        private string _message = "", _sentSig = "";
        private TownSync _hooked;
        private MaterialPropertyBlock _block;

        private void OnDisable() { Leave(false); Unhook(); }

        private void Hook(TownSync s)
        {
            if (_hooked == s) return;
            Unhook();
            _hooked = s;
            if (s != null) s.ResultReceived += OnResult;
        }

        private void Unhook() { if (_hooked != null) _hooked.ResultReceived -= OnResult; _hooked = null; }

        private void OnResult(TownSync.Op op, byte result)
        {
            if (op == TownSync.Op.Lock && result != (byte)PlaceResult.Ok) { _wantLock = false; Say("Someone else is building"); return; }
            if (result != (byte)PlaceResult.Ok) Say(PlacementText.Refusal(result));
        }

        private void Say(string text) { _message = text; _messageUntil = Time.unscaledTime + 3f; }

        // ------------------------------------------------------------------ update
        private void Update()
        {
            TownSync sync = TownSync.Local;
            Hook(sync != null && sync.IsSpawned ? sync : null);
            if (sync == null || !sync.IsSpawned) { if (Active) Leave(false); return; }

            if (Active)
            {
                if (!sync.IsBuilder) { Leave(false); return; }   // the lock is gone (host released it, or this peer is no longer the holder)
                TickBuilder(sync);
            }
            else
            {
                if (_wantLock && sync.IsBuilder) Enter(sync);
                else if (_wantLock && Time.unscaledTime > _lockAskedAt + 5f) _wantLock = false;
                else if (!_wantLock) TickPrompt(sync);
            }
            UpdateGhost(sync);
            UpdateCamera();
        }

        private bool NearBuildPoint()
        {
            NetworkManager nm = NetworkManager.Singleton;
            NetworkObject player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            if (player == null || BuildPoint == null) return false;
            Vector3 d = player.transform.position - BuildPoint.position; d.y = 0f;
            return d.sqrMagnitude <= PromptRadius * PromptRadius;
        }

        private string _prompt = "";
        private void TickPrompt(TownSync sync)
        {
            _prompt = "";
            if (!NearBuildPoint()) return;
            IInputService input = HeroInput.ResolveDefault();
            if (!sync.BuilderIsFree) { _prompt = "Someone is building"; return; }
            string key = input != null ? input.GetBindingDisplayString(InputActionNames.Interact) : "";
            _prompt = "[" + (string.IsNullOrEmpty(key) ? "E" : key) + "]  Open placement mode";
            if (input != null && input.Context == InputContext.Free && input.Intent.InteractPressed)
            {
                _wantLock = true; _lockAskedAt = Time.unscaledTime;
                sync.RequestBuildLock();
            }
        }

        // ------------------------------------------------------------------ enter / leave
        private void Enter(TownSync sync)
        {
            Active = true; _wantLock = false; _prompt = "";
            TownState town = sync.View;
            PlacedBuilding placed = town.PlacedBuildingOf(BuildingId);
            _turn = placed != null ? placed.Turn : 0;
            _lot = placed != null ? placed.FoundationId : FirstFree(town, 0);
            IInputService input = HeroInput.ResolveDefault();
            if (input != null) input.GameplayContext = InputContext.Placement;
            if (PlacementCam != null) { PlacementCam.gameObject.SetActive(true); PlacementCam.Priority.Value = 100; SnapCamera(sync); }
            _sentSig = "";
            Say("Placement mode");
        }

        // Leaves placement locally; releaseLock also tells the host (leaving by the pause key).
        public void Leave(bool releaseLock)
        {
            TownSync sync = TownSync.Local;
            if (releaseLock && sync != null && sync.IsSpawned && sync.IsBuilder)
            {
                sync.SendGhost("", "", 0, false);
                sync.ReleaseBuildLock();
            }
            if (!Active) return;
            Active = false;
            IInputService input = HeroInput.ResolveDefault();
            if (input != null && input.GameplayContext == InputContext.Placement) input.GameplayContext = InputContext.Free;
            if (PlacementCam != null) { PlacementCam.Priority.Value = 0; PlacementCam.gameObject.SetActive(false); }
        }

        // ------------------------------------------------------------------ the builder's controls
        private void TickBuilder(TownSync sync)
        {
            IInputService input = HeroInput.ResolveDefault();
            if (input == null) return;
            if (input.MenuOpen) { Leave(true); return; }
            PlayerIntent i = input.Intent;
            TownState town = sync.View;
            BuildingDef def = TownBuildings.Get(BuildingId);
            PlacedBuilding placed = town.PlacedBuildingOf(BuildingId);

            if (i.PausePressed) { Leave(true); return; }

            if (placed == null)
            {
                if (_lot == "" || town.OccupantOf(_lot) != null) _lot = FirstFree(town, 0);
                float sx = i.SwitchX, sy = i.SwitchY;
                float mag = Mathf.Max(Mathf.Abs(sx), Mathf.Abs(sy));
                if (mag < 0.3f) _flickReleased = 1f;
                else if (mag > 0.6f && _flickReleased > 0f) { _flickReleased = 0f; _lot = Neighbour(town, def, _lot, sx, sy); }
                if (i.InteractPressed) _lot = NextFree(town, def, _lot);
                if (i.JumpPressed) _turn = WishEvaluator.NormalTurn(_turn + 1);
            }
            else
            {
                _lot = placed.FoundationId; _turn = placed.Turn;
                if (i.JumpPressed) sync.RequestSetTurn(BuildingId, placed.Turn + 1);
            }

            if (i.PrimaryPressed)
            {
                PlaceStep step = PlacementSteps.Next(town, BuildingId);
                if (step.Kind == PlaceStepKind.Frame) sync.RequestPlaceFrame(BuildingId, _lot, _turn);
                else if (step.Kind == PlaceStepKind.Part) sync.RequestPlacePart(BuildingId, step.Part);
                else if (step.Kind == PlaceStepKind.Resident) sync.RequestPlaceResident(BuildingId);
                else Say(placed == null ? "No frame in stock" : "Nothing left to place");
            }
            if (i.SecondaryPressed)
            {
                PlaceStep up = PlacementSteps.NextPickUp(town, BuildingId);
                if (up.Kind == PlaceStepKind.Resident) sync.RequestPickUpResident(BuildingId);
                else if (up.Kind == PlaceStepKind.Part) sync.RequestPickUpPart(BuildingId, up.Part);
                else if (up.Kind == PlaceStepKind.Frame) sync.RequestPickUpBuilding(BuildingId);
            }

            // The ghost pose goes to the host (and so to the watchers) when it changes, at most ten times a second.
            bool ghostOn = placed == null && _lot != "";
            string sig = ghostOn ? BuildingId + "@" + _lot + "/" + _turn : "-";
            if (sig != _sentSig && Time.unscaledTime >= _nextGhostSend)
            {
                _sentSig = sig; _nextGhostSend = Time.unscaledTime + 0.1f;
                sync.SendGhost(BuildingId, _lot, _turn, ghostOn);
            }
        }

        public static string FirstFree(TownState town, int skip)
        {
            BuildingDef def = TownBuildings.Get(TownBuildings.Bakery);
            List<string> all = PlacementSteps.CandidateFoundations(town.Layout, def);
            foreach (string id in all) if (town.OccupantOf(id) == null) { if (skip-- <= 0) return id; }
            return "";
        }

        public static string NextFree(TownState town, BuildingDef def, string current)
        {
            List<string> all = PlacementSteps.CandidateFoundations(town.Layout, def);
            if (all.Count == 0) return "";
            int start = all.IndexOf(current);
            for (int k = 1; k <= all.Count; k++)
            {
                string id = all[((start < 0 ? -1 : start) + k) % all.Count];
                if (town.OccupantOf(id) == null) return id;
            }
            return current;
        }

        // The free lot of the size class nearest to `current` in the pushed direction (x east, y north on the fixed placement view); stays when none lies that way.
        public static string Neighbour(TownState town, BuildingDef def, string current, float dx, float dz)
        {
            Foundation from = town.Layout.Foundation(current);
            if (from == null) return NextFree(town, def, current);
            float len = Mathf.Sqrt(dx * dx + dz * dz); if (len < 1e-4f) return current;
            dx /= len; dz /= len;
            string best = current; float bestScore = float.MaxValue;
            foreach (Foundation f in town.Layout.Foundations)
            {
                if (f.Id == current || f.Size != def.Size || town.OccupantOf(f.Id) != null) continue;
                float vx = f.Centre.X - from.Centre.X, vz = f.Centre.Z - from.Centre.Z, dist = Mathf.Sqrt(vx * vx + vz * vz);
                if (dist < 1e-3f) continue;
                float along = (vx * dx + vz * dz) / dist;   // cos of the angle to the pushed direction
                if (along < 0.5f) continue;                  // within 60 degrees of it
                float score = dist / along;
                if (score < bestScore) { bestScore = score; best = f.Id; }
            }
            return best;
        }

        // ------------------------------------------------------------------ ghost, camera
        // The pose to draw: the builder's own, or what the host mirrors for watchers.
        private bool CurrentPose(TownSync sync, out string lot, out int turn)
        {
            lot = ""; turn = 0;
            if (Active && sync.IsBuilder) { if (sync.View.PlacedBuildingOf(BuildingId) != null || _lot == "") return false; lot = _lot; turn = _turn; return true; }
            GhostNet g = sync.NetGhost.Value;
            if (!g.Active || g.Building.ToString() != BuildingId) return false;
            lot = g.Foundation.ToString(); turn = g.Turn; return true;
        }

        private void UpdateGhost(TownSync sync)
        {
            if (Ghost == null) return;
            bool show = CurrentPose(sync, out string lot, out int turn);
            Foundation f = show ? sync.Layout.Foundation(lot) : null;
            if (f == null) { if (Ghost.gameObject.activeSelf) Ghost.gameObject.SetActive(false); return; }
            if (!Ghost.gameObject.activeSelf) Ghost.gameObject.SetActive(true);
            float side = Foundation.MaxBuilding(f.Size);
            Ghost.position = new Vector3(f.Centre.X, f.Y + 0.05f, f.Centre.Z);
            LotSide door = WishEvaluator.DoorSide(f, turn);
            Ghost.rotation = Quaternion.Euler(0f, f.Yaw + (door == LotSide.North ? 180f : door == LotSide.East ? 270f : door == LotSide.South ? 0f : 90f), 0f);   // the same door yaw as TownView
            Ghost.localScale = new Vector3(side, 1f, side);   // the builder emits a slab (0.6 m tall) and a door nub under this root
            bool ok = sync.View.CanPlaceFrame(BuildingId, lot, turn) == PlaceResult.Ok;
            if (_block == null) _block = new MaterialPropertyBlock();
            _block.SetColor("_BaseColor", ok ? Green : Red); _block.SetColor("_Color", ok ? Green : Red);
            foreach (Renderer r in GhostRenderers) if (r != null) r.SetPropertyBlock(_block);
        }

        private Vector3 CamTarget(TownSync sync)
        {
            string lot = _lot;
            if (!Active || lot == "")
            {
                PlacedBuilding p = sync.View.PlacedBuildingOf(BuildingId);
                lot = p != null ? p.FoundationId : lot;
            }
            Foundation f = sync.Layout.Foundation(lot);
            if (f == null) return PlacementCam != null ? PlacementCam.transform.position : Vector3.zero;
            return new Vector3(f.Centre.X, f.Y + CamHeight, f.Centre.Z - CamBack);
        }

        private void SnapCamera(TownSync sync)
        {
            if (PlacementCam == null) return;
            PlacementCam.transform.SetPositionAndRotation(CamTarget(sync), Quaternion.Euler(CamPitch, 0f, 0f));
        }

        private void UpdateCamera()
        {
            TownSync sync = TownSync.Local;
            if (!Active || PlacementCam == null || sync == null) return;
            Vector3 target = CamTarget(sync);
            PlacementCam.transform.position = Vector3.Lerp(PlacementCam.transform.position, target, 1f - Mathf.Exp(-CamLerp * Time.unscaledDeltaTime));
            PlacementCam.transform.rotation = Quaternion.Euler(CamPitch, 0f, 0f);
        }

        // ------------------------------------------------------------------ the wish panel
        // The wishes shown for everyone: the placed building's, else the ghost's pose (builder or watcher). Null when nothing is being placed.
        public WishReport CurrentWishes(TownSync sync)
        {
            TownState town = sync.View;
            BuildingDef def = TownBuildings.Get(BuildingId);
            PlacedBuilding placed = town.PlacedBuildingOf(BuildingId);
            if (placed != null && (Active || !sync.BuilderIsFree)) return WishEvaluator.Evaluate(sync.Layout, def, placed.FoundationId, placed.Turn);
            if (CurrentPose(sync, out string lot, out int turn)) return WishEvaluator.Evaluate(sync.Layout, def, lot, turn);
            return null;
        }

        private void OnGUI()
        {
            TownSync sync = TownSync.Local;
            if (sync == null || !sync.IsSpawned) return;
            if (!Active && !string.IsNullOrEmpty(_prompt)) GUI.Box(new Rect(Screen.width * 0.5f - 190f, Screen.height - 150f, 380f, 30f), _prompt);
            WishReport wishes = CurrentWishes(sync);
            bool watching = !Active && !sync.BuilderIsFree;
            if (!Active && !watching) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Active ? "PLACEMENT MODE" : "Someone is building");
            if (wishes != null)
            {
                sb.AppendLine("The baker's wishes (" + wishes.MetCount + "/" + wishes.Count + "):");
                foreach (WishResult r in wishes.Results) sb.AppendLine((r.Met ? "[x] " : "[ ] ") + PlacementText.WishLine(r));
            }
            if (Active)
            {
                sb.AppendLine("Stock: " + PlacementText.StockLine(sync.View));
                PlaceStep next = PlacementSteps.Next(sync.View, BuildingId);
                sb.AppendLine("Place: " + (next.Kind == PlaceStepKind.None ? "-" : next.Kind == PlaceStepKind.Part ? next.Part : next.Kind.ToString().ToLowerInvariant()) + "   Lot: " + _lot + "  Turn: " + _turn);
                sb.AppendLine("Primary place | Secondary pick up | Interact next lot | Jump turn | Pause leave");
            }
            if (Time.unscaledTime < _messageUntil && _message != "") sb.AppendLine(_message);
            GUI.Box(new Rect(16f, 16f, 470f, 28f + 20f * (wishes != null ? wishes.Count + 5 : 4)), sb.ToString());
        }
    }
}
