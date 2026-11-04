// Core/Rooms/EnemyHabitat.cs: which room affordances each enemy kind needs, so a room only hosts enemies that can fight in it (metrics gym plan, Task 3).
using System;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Core
{
    public static class EnemyHabitat
    {
        // The Brazen Ox rename is a later plan's; this uses the current enum.
        public static RoomAffordance Needs(EnemyKind kind) => kind == EnemyKind.DoughWarden ? RoomAffordance.OpenFloor : RoomAffordance.None;

        public static bool Fits(EnemyKind kind, RoomDataV2 room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            RoomAffordance need = Needs(kind);
            return (room.Affordances & need) == need;
        }
    }
}
