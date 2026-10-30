// The hero's animation slots: which clip plays for each state, in order of preference. A source is
//   Q1:<clip>  Quaternius UAL1      Q2:<clip>  Quaternius UAL2      KI:<Folder>/<Stem>  Kevin Iglesias (Folder = Basic, Archer, Spellcasting; Stem without "HumanM@")
//   MX:<hint>  a Mixamo FBX in Content/Animation/Mixamo whose file name, lower-cased with only letters and digits kept, CONTAINS the hint.
// A slot whose first source is an MX one and which resolved to a later source is a placeholder: the builder logs "SC ANIM placeholder" and the clip for
// the real Mixamo file is picked up on the next build once the file is dropped into the Mixamo folder. docs/animation/SOURCES.md lists the owed files.
using System.Collections.Generic;
using SleepingCalamity.Core;

namespace SleepingCalamity.EditorTools
{
    public sealed class ClipSlot
    {
        public readonly string Id;
        public readonly string[] Sources;
        public readonly bool Mirror;
        public ClipSlot(string id, bool mirror, params string[] sources) { Id = id; Mirror = mirror; Sources = sources; }
        public bool OwedFromMixamo => Sources.Length > 0 && Sources[0].StartsWith("MX:");
    }

    public static class HeroClipTable
    {
        static readonly string[] Dirs = { "Forward", "ForwardRight", "Right", "BackwardRight", "Backward", "BackwardLeft", "Left", "ForwardLeft" };

        public static readonly string[] WeaponNames = { "Axe", "Staff", "Dagger", "Bow" };
        public static readonly string[] SlotNames = { "Hit1", "Hit2", "Finisher", "Charge", "Special" };

        public static string AttackStateName(byte attackId) => "Atk_" + WeaponNames[HeroAnimMap.WeaponOf(attackId)] + "_" + SlotNames[HeroAnimMap.SlotOf(attackId)];

        public static List<ClipSlot> Slots()
        {
            var s = new List<ClipSlot>
            {
                new ClipSlot("Idle", false, "Q1:Idle_Loop"),
                new ClipSlot("LockIdle", false, "MX:warrioridle", "Q1:Sword_Idle"),
                new ClipSlot("Walk", false, "Q1:Walk_Loop"),
                new ClipSlot("Jog", false, "Q1:Jog_Fwd_Loop"),
                new ClipSlot("Run", false, "Q1:Sprint_Loop"),
                new ClipSlot("JumpStart", false, "Q1:Jump_Start"),
                new ClipSlot("JumpAir", false, "Q1:Jump_Loop"),
                new ClipSlot("JumpLand", false, "Q1:Jump_Land"),
                new ClipSlot("DodgeForward", false, "Q1:Roll"),
                new ClipSlot("DodgeBack", false, "MX:backstep", "MX:dodgeback", "Q1:Roll"),
                new ClipSlot("DodgeLeft", false, "MX:dodgeleft", "MX:sidedodgeleft", "Q1:Roll"),
                new ClipSlot("DodgeRight", false, "MX:dodgeright", "MX:sidedodgeright", "Q1:Roll"),
                new ClipSlot("HurtLight", false, "Q1:Hit_Chest"),
                new ClipSlot("HurtHeavy", false, "Q2:Hit_Knockback"),
                new ClipSlot("Downed", false, "MX:knockeddown", "MX:lying", "Q1:Death01"),
                new ClipSlot("GetUp", false, "Q2:LayToIdle"),
                new ClipSlot("Guard", false, "Q2:Idle_Shield_Loop"),
                new ClipSlot("AimHold", false, "MX:longbowaim", "KI:Archer/BowShot01 - Hold"),
                new ClipSlot("PickUpGround", false, "MX:pickingup", "Q1:PickUp_Table"),
            };
            foreach (string d in Dirs)
            {
                s.Add(new ClipSlot("Walk_" + d, false, "KI:Basic/Walk01_" + d));
                s.Add(new ClipSlot("Run_" + d, false, "KI:Basic/Run01_" + d));
            }
            foreach (string d in new[] { "ForwardRight", "Right", "BackwardRight", "BackwardLeft", "Left", "ForwardLeft" })
                s.Add(new ClipSlot("AimWalk_" + d, false, "MX:longbowaimwalk" + d.ToLowerInvariant(), "KI:Archer/StrafeRun01_" + d));

            // Attacks: one per (heirloom, slot). Air attacks reuse the ground clip until the Mixamo air attack arrives.
            Add(s, 0, 0, false, "MX:greatswordcombo1", "Q1:Sword_Attack");
            Add(s, 0, 1, true, "MX:greatswordcombo2", "Q1:Sword_Attack");
            Add(s, 0, 2, false, "MX:greatswordoverhead", "Q2:Sword_Heavy_Combo");
            Add(s, 0, 3, false, "MX:greatswordcharge", "Q2:Sword_Heavy_Combo");
            Add(s, 0, 4, false, "Q1:Sword_Attack");
            Add(s, 1, 0, false, "MX:staffstrike1", "Q1:Spell_Simple_Shoot");
            Add(s, 1, 1, true, "MX:staffstrike2", "Q1:Spell_Simple_Shoot");
            Add(s, 1, 2, false, "MX:staffstrike3", "KI:Spellcasting/MagicAttackOmni01");
            Add(s, 1, 3, false, "MX:staffcharge", "KI:Spellcasting/MagicAttackDirect2H01");
            Add(s, 1, 4, false, "KI:Spellcasting/MagicAttackDirect1H01_R");
            Add(s, 2, 0, false, "Q2:Sword_Regular_A");
            Add(s, 2, 1, false, "Q2:Sword_Regular_B");
            Add(s, 2, 2, false, "Q2:Sword_Regular_C");
            Add(s, 2, 3, false, "Q2:Sword_Dash");
            Add(s, 2, 4, false, "Q2:Shield_OneShot");
            Add(s, 3, 0, false, "Q2:Sword_Regular_A");
            Add(s, 3, 1, false, "Q2:Sword_Regular_B");
            Add(s, 3, 2, false, "Q2:Sword_Regular_C");
            Add(s, 3, 3, false, "Q2:Sword_Regular_Combo");
            Add(s, 3, 4, false, "MX:longbowshot", "KI:Archer/BowShot01 - Release");
            // Owed with no state yet: air attack, launcher (documented in SOURCES.md, wired when the clips and the combat moves exist).
            return s;
        }

        static void Add(List<ClipSlot> s, int weapon, int slot, bool mirror, params string[] sources)
        {
            s.Add(new ClipSlot(AttackStateName(HeroAnimMap.AttackId(weapon, slot)), mirror, sources));
        }
    }
}
