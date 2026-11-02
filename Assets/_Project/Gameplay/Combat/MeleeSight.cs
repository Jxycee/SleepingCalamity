// Authority: none of its own; a stateless physics query. Called by the OWNER (HeroCombat: swing detection, judging of incoming enemy hits) and by the HOST
// (CombatHost.ServerPlayerHitsEnemy re-checks an intent against the attacker's reported position), so a melee hit never passes through a wall.
// HitGeometry (Core) is range + cone + height only and leaves walls to the caller; the bow raycasts for itself. This is the melee caller's wall test (alpha
// playtest item 2): a line between the two chests that a Structure collider (walls, chunk solids) interrupts.
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    public static class MeleeSight
    {
        public const float ChestHeight = 1.0f;

        // True when no Structure collider lies between the two ground positions at chest height.
        public static bool Clear(Vector3 attackerFeet, Vector3 targetFeet)
        {
            Vector3 a = attackerFeet + Vector3.up * ChestHeight;
            Vector3 b = targetFeet + Vector3.up * ChestHeight;
            return !Physics.Linecast(a, b, Layers.Mask(Layers.Structure), QueryTriggerInteraction.Ignore);
        }
    }
}
