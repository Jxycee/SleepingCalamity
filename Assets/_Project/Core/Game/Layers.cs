// Core/Game/Layers.cs: physics layer indices. Layer 4 is Unity's built-in "Water".
// ProjectSettingsBuilder writes UserLayerNames into UserLayerIndices; the two arrays are index-aligned.
// Ported from FishGame with the fishing layers removed; combat adds Enemy, Hitbox and the like with its milestone.
namespace SleepingCalamity.Core
{
    public static class Layers
    {
        public const int Default = 0;
        public const int Water = 4;          // built-in name "Water" (already present)
        public const int Player = 6;         // local owner's root
        public const int RemotePlayer = 7;   // remote roots
        public const int Ground = 8;         // floors and terrain
        public const int Structure = 9;      // walls, props, buildings
        public const int Interactable = 10;  // things the interact key can use
        public const int Enemy = 11;         // M2: target dummies and (from M3) enemies; line-of-sight and camera rays ignore it
        public static readonly int[] UserLayerIndices = { 6, 7, 8, 9, 10, 11 };
        public static readonly string[] UserLayerNames = { "Player", "RemotePlayer", "Ground", "Structure", "Interactable", "Enemy" };
        public static int Mask(int layer) => 1 << layer;
    }
}
