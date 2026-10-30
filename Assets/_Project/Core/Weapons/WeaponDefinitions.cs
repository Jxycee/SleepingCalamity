// Core/Weapons/WeaponDefinitions.cs: weapon stats, attachments and the weapon definitions as data (PHASE1 6.3, 6.5). Pure C#, no engine.
// Every number here is a P15 Placeholder: tune in M3 and M7. The four heirlooms use the 6.5 ATK starts (axe 22, staff 14, dagger+shield 10,
// bow-and-sword 12); the four build-up targets (names and stats) are Placeholder too.
// Authority: none (static data; the host reads it when it applies growth).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core.Weapons
{
    // What a weapon is worth: attack plus an element (fire) and a species tag (pest, "anti-pest") value. Equality is exact on purpose: the
    // re-route tests demand stats "exactly as they were".
    [Serializable]
    public readonly struct WeaponStats : IEquatable<WeaponStats>
    {
        public readonly float Atk, Fire, Pest;

        public WeaponStats(float atk, float fire = 0f, float pest = 0f) { Atk = atk; Fire = fire; Pest = pest; }

        public static WeaponStats operator +(WeaponStats a, WeaponStats b) => new WeaponStats(a.Atk + b.Atk, a.Fire + b.Fire, a.Pest + b.Pest);
        public static WeaponStats operator *(WeaponStats a, float k) => new WeaponStats(a.Atk * k, a.Fire * k, a.Pest * k);

        public bool Equals(WeaponStats o) => Atk == o.Atk && Fire == o.Fire && Pest == o.Pest;
        public override bool Equals(object obj) => obj is WeaponStats o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(Atk, Fire, Pest);
        public override string ToString() => $"ATK {Atk} Fire {Fire} Pest {Pest}";
    }

    // The three Cellars drops. The numbers are the value each adds to the weapon once absorbed at a level-up.
    public enum AttachmentKind { Ember = 0, PestBane = 1, Heartwood = 2 }

    public static class Attachments
    {
        public static bool IsValid(AttachmentKind k) => k == AttachmentKind.Ember || k == AttachmentKind.PestBane || k == AttachmentKind.Heartwood;

        // Placeholder: Ember +5 fire, Pest-bane +5 pest tag, Heartwood +2 ATK.
        public static WeaponStats Bonus(AttachmentKind k)
        {
            switch (k)
            {
                case AttachmentKind.Ember: return new WeaponStats(0f, 5f, 0f);
                case AttachmentKind.PestBane: return new WeaponStats(0f, 0f, 5f);
                case AttachmentKind.Heartwood: return new WeaponStats(2f, 0f, 0f);
                default: return default;
            }
        }
    }

    // One weapon as data. BuildUpTargetId is null when nothing builds up from it (a target, or a weapon with no tree yet).
    public sealed class WeaponDefinition
    {
        public readonly string Id, DisplayName, BuildUpTargetId;
        public readonly WeaponStats Base;
        public readonly int SlotCount;   // 2..3

        public WeaponDefinition(string id, string displayName, WeaponStats baseStats, int slotCount, string buildUpTargetId)
        {
            Id = id; DisplayName = displayName; Base = baseStats; SlotCount = slotCount; BuildUpTargetId = buildUpTargetId;
        }
    }

    public sealed class WeaponCatalog
    {
        public const string Axe = "axe", Staff = "staff", DaggerShield = "dagger_shield", BowSword = "bow_sword";
        public const string CinderAxe = "cinder_axe", WardenStaff = "warden_staff", BaneDagger = "bane_dagger", EmberBow = "ember_bow";

        readonly Dictionary<string, WeaponDefinition> _byId = new Dictionary<string, WeaponDefinition>();

        public WeaponCatalog(IEnumerable<WeaponDefinition> defs)
        {
            foreach (var d in defs) _byId[d.Id] = d;
        }

        public IEnumerable<WeaponDefinition> All => _byId.Values;
        public bool TryGet(string id, out WeaponDefinition def)
        {
            def = null;
            return id != null && _byId.TryGetValue(id, out def);
        }

        // Placeholder names and stats for the four heirlooms and one build-up target each. Targets want ATK and one element/tag at 90%
        // of their base: the axe needs 3 levels (22 -> 28 of 27) and two Embers (10 of 9).
        public static readonly WeaponCatalog Default = new WeaponCatalog(new[]
        {
            new WeaponDefinition(Axe, "Heirloom Axe", new WeaponStats(22f), 3, CinderAxe),
            new WeaponDefinition(Staff, "Heirloom Staff", new WeaponStats(14f), 3, WardenStaff),
            new WeaponDefinition(DaggerShield, "Heirloom Dagger and Shield", new WeaponStats(10f), 2, BaneDagger),
            new WeaponDefinition(BowSword, "Heirloom Bow and Sword", new WeaponStats(12f), 2, EmberBow),

            new WeaponDefinition(CinderAxe, "Cinder Axe", new WeaponStats(30f, fire: 10f), 3, null),
            new WeaponDefinition(WardenStaff, "Warden Staff", new WeaponStats(20f, pest: 10f), 3, null),
            new WeaponDefinition(BaneDagger, "Bane Dagger", new WeaponStats(15f, pest: 10f), 3, null),
            new WeaponDefinition(EmberBow, "Ember Bow", new WeaponStats(18f, fire: 10f), 3, null),
        });
    }
}
