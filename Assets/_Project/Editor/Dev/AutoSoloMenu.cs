// SleepingCalamity > Dev > Auto Solo (checkable, EditorPrefs). The main-editor-only gate lives in DevStartup (MPPM clones share EditorPrefs).
using SleepingCalamity.Runtime;
using UnityEditor;

namespace SleepingCalamity.EditorTools
{
    public static class AutoSoloMenu
    {
        const string MenuPath = "SleepingCalamity/Dev/Auto Solo";

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
    }
}
