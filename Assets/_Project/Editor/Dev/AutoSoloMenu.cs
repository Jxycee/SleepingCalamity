// SleepingCalamity > Dev > Auto Solo (checkable, EditorPrefs). The main-editor-only gate lives in DevStartup (MPPM clones share EditorPrefs).
using SleepingCalamity.Runtime;
using UnityEditor;

namespace SleepingCalamity.EditorTools
{
    public static class AutoSoloMenu
    {
        const string MenuPath = "SleepingCalamity/Dev/Auto Solo";
        const string CombatMenuPath = "SleepingCalamity/Dev/Combat Arena";   // M3: sessions start in Arena_Combat
        const string DiveMenuPath = "SleepingCalamity/Dev/Dive Hub";   // M5: sessions start in Arena_Dive

        [MenuItem(MenuPath)]
        public static void Toggle()
        {
            bool next = !EditorPrefs.GetBool(DevPrefKeys.AutoSolo, false);
            EditorPrefs.SetBool(DevPrefKeys.AutoSolo, next);
            Menu.SetChecked(MenuPath, next);
        }

        [MenuItem(MenuPath, true)]
        public static bool ValidateToggle()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(DevPrefKeys.AutoSolo, false));
            return true;
        }

        [MenuItem(CombatMenuPath)]
        public static void ToggleCombat()
        {
            bool next = !EditorPrefs.GetBool(DevPrefKeys.CombatArena, false);
            EditorPrefs.SetBool(DevPrefKeys.CombatArena, next);
            Menu.SetChecked(CombatMenuPath, next);
        }

        [MenuItem(DiveMenuPath)]
        public static void ToggleDive()
        {
            bool next = !EditorPrefs.GetBool(DevPrefKeys.DiveArena, false);
            EditorPrefs.SetBool(DevPrefKeys.DiveArena, next);
            Menu.SetChecked(DiveMenuPath, next);
        }

        [MenuItem(DiveMenuPath, true)]
        public static bool ValidateToggleDive()
        {
            Menu.SetChecked(DiveMenuPath, EditorPrefs.GetBool(DevPrefKeys.DiveArena, false));
            return true;
        }

        [MenuItem(CombatMenuPath, true)]
        public static bool ValidateToggleCombat()
        {
            Menu.SetChecked(CombatMenuPath, EditorPrefs.GetBool(DevPrefKeys.CombatArena, false));
            return true;
        }
    }
}
