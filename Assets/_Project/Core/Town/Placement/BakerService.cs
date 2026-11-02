// Core/Town/Placement/BakerService.cs: the baker's service rule (M6 Q4 C). At dive start every player gets LoavesPerPlayer loaves iff the bakery is placed AND complete, the baker has moved
// in AND all the bakery's wishes are met at that moment; otherwise nobody gets any and Reason says why (Notify: the baker is housed but his wishes are not met, so the dive-start prompt
// tells the party). The first-meet beat latches the first time the baker stands in his complete bakery and never un-latches (picking him up or breaking a wish does not undo it).
// Authority: none (pure); the host evaluates on its own TownState.
namespace SleepingCalamity.Core
{
    public readonly struct BakerLoaves
    {
        public readonly int PerPlayer;     // loaves each player is granted at dive start
        public readonly string Reason;     // why there are none (null when granted)
        public readonly bool Notify;       // show the "wishes aren't met" prompt: the baker is housed in his complete bakery but a wish is unmet
        public BakerLoaves(int perPlayer, string reason, bool notify) { PerPlayer = perPlayer; Reason = reason; Notify = notify; }
        public bool Granted => PerPlayer > 0;
    }

    // The host's memory of the loaf decision made at dive start (loaves per player, 0 when not granted), so a hero that spawns mid-dive (late joiner, reconnect) gets the same number.
    // Cleared when the dive ends. Authority: host only.
    public sealed class DiveLoafLedger
    {
        public bool DiveActive { get; private set; }
        public int PerPlayer { get; private set; }
        public void Begin(int perPlayer) { DiveActive = true; PerPlayer = perPlayer < 0 ? 0 : perPlayer; }
        public void End() { DiveActive = false; PerPlayer = 0; }
        // Loaves for a hero spawning now: the remembered number during a dive, null (leave the count alone) otherwise.
        public int? ForSpawn() { return DiveActive ? PerPlayer : (int?)null; }
    }

    public static class BakerService
    {
        public const int LoavesPerPlayer = 3;
        public const string WishesUnmetNotice = "The baker's wishes aren't met: no loaves this dive.";

        // True while the baker is in his complete bakery (the first-meet condition and the service condition before wishes).
        public static bool BakerHoused(TownState town)
        {
            if (town == null) return false;
            PlacedBuilding b = town.PlacedBuildingOf(TownBuildings.Bakery);
            return b != null && town.IsComplete(TownBuildings.Bakery) && b.Resident == TownBuildings.BakerResident;
        }

        public static BakerLoaves Evaluate(TownState town)
        {
            if (town == null || town.PlacedBuildingOf(TownBuildings.Bakery) == null) return new BakerLoaves(0, "The bakery is not built.", false);
            if (!town.IsComplete(TownBuildings.Bakery)) return new BakerLoaves(0, "The bakery is not finished.", false);
            if (town.PlacedBuildingOf(TownBuildings.Bakery).Resident != TownBuildings.BakerResident) return new BakerLoaves(0, "The baker has not moved in.", false);
            WishReport wishes = town.Wishes(TownBuildings.Bakery);
            if (wishes == null || !wishes.AllMet) return new BakerLoaves(0, WishesUnmetNotice, true);
            return new BakerLoaves(LoavesPerPlayer, null, false);
        }

        // The latch after this look at the town: once true it stays true whatever the town now shows.
        public static bool NextFirstMeet(bool latched, TownState town) => latched || BakerHoused(town);
    }
}
