using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Consol.Patch
{
    /// <summary>
    /// Organizational section to store any patches that need to be made. Most patches should go here, but
    /// if there's things that need access to something from the console/command handler, maybe they should go there.
    /// I don't want to make any global accessors for either.
    /// </summary>
    internal static class PatchManager
    {
        [HarmonyPatch(typeof(Console), nameof(Console.IsConsoleEnabled))]
        public static class ConsoleEnablePatch
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ref bool __result)
            {
                __result = ConfigManager.Get<bool>("ConsoleEnabled");
            }
        }

        // Patch to stop the mouse from not showing while the console is open.
        [HarmonyPatch(typeof(ZInput), nameof(ZInput.IsMouseActive))]
        public static class MouseInputDetectionPatch
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(ref bool __result)
            {
                if (Console.IsVisible())
                {
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        // Same as above.
        [HarmonyPatch(typeof(GameCamera), "UpdateMouseCapture")]
        public static class MouseVisibilityPatch
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix()
            {
                if (Console.IsVisible())
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    return false;
                }

                return true;
            }
        }
    }
}
