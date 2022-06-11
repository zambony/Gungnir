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
        private static Consol s_plugin = null;
        public static Consol Plugin { get => s_plugin; set => s_plugin = value; }

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

        // Same as above. Private method, can't target with nameof.
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

        [HarmonyPatch(typeof(Location), nameof(Location.IsInsideNoBuildLocation))]
        private static class BuildRestrictionPatch
        {
            private static bool Prefix(ref bool __result)
            {
                if (Plugin.BuildAnywhere)
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }

        // Private method, no nameof.
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static class BuildPlacementPatch
        {
            private static void Postfix(ref int ___m_placementStatus)
            {
                // 4 = Player.PlacementStatus.PrivateZone
                if (Plugin.BuildAnywhere && Player.m_localPlayer && ___m_placementStatus != 4)
                {
                    ___m_placementStatus = 0;
                }
            }
        }

        // Private method, no nameof.
        [HarmonyPatch(typeof(WearNTear), "UpdateSupport")]
        private static class StructuralSupportPatch
        {
            private static bool Prefix(ref float ___m_support, ref ZNetView ___m_nview)
            {
                if (Plugin.NoStructuralSupport)
                {
                    ___m_support += ___m_support;
                    ___m_nview.GetZDO().Set("support", ___m_support);
                    return false;
                }

                return true;
            }
        }
    }
}
