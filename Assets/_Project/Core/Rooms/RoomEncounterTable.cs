// Core/Rooms/RoomEncounterTable.cs: which templates each of the 12 pool combat rooms may use (encounter templates plan, Task 2). Authored by room shape and affordance:
// OpenFloor templates (Pack, Mixed tier, Reinforced) only in rooms with a 3x3 free window; two-spawn rooms hold only templates of at most 3 solo (authored count + 1).
// Ambush only where the room is empty-looking (>= 3 free cells) and ONLY on the room the Ambush floor modifier picks. Authority: none (static data).
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    public static class RoomEncounterTable
    {
        const string P = EncounterTemplates.PackId, G = EncounterTemplates.GelSwarmId, W = EncounterTemplates.ShieldWallId, Rb = EncounterTemplates.RushBombardId,
            Gc = EncounterTemplates.GuardedCasterId, M = EncounterTemplates.MixedTierId, A = EncounterTemplates.AmbushId, Re = EncounterTemplates.ReinforcedId;

        static readonly Dictionary<string, string[]> map = new Dictionary<string, string[]>
        {
            ["larder_pantry"] = new[] { P, Gc, A },
            ["larder_cold_store"] = new[] { P, Gc, M },
            ["larder_hanging_hall"] = new[] { G, Rb, W },
            ["larder_smoke_gallery"] = new[] { P, W, Re },
            ["wine_cask_row"] = new[] { P, Rb, A },
            ["wine_vault_l"] = new[] { W, Gc, G },
            ["wine_press_hall"] = new[] { P, Gc, Re },
            ["wine_aisles"] = new[] { Rb, W, Gc },
            ["coal_bunker"] = new[] { P, M, A },
            ["coal_furnace_ring"] = new[] { G, Rb, W },
            ["coal_stoke_chicane"] = new[] { Gc, W },
            ["coal_boiler_hall"] = new[] { P, Re, M },
        };

        public static IReadOnlyCollection<string> RoomIds => map.Keys.ToList();
        public static IReadOnlyList<string> For(string roomId) => map.TryGetValue(roomId ?? "", out string[] t) ? t : new string[0];
    }
}
