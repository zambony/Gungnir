using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Gungnir.Patch
{
    /// <summary>
    /// Organizational section to store any patches that need to be made. Most patches should go here, but
    /// if there's things that need access to something from the console/command handler, maybe they should go there.
    /// I don't want to make any global accessors for either.
    /// </summary>
    internal static class PatchManager
    {
        private static Gungnir s_plugin = null;
        public static Gungnir Plugin { get => s_plugin; set => s_plugin = value; }

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
        public static class BuildRestrictionPatch
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
        public static class BuildPlacementPatch
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
        public static class StructuralSupportPatch
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

        [HarmonyPatch(typeof(Player), "UseStamina")]
        public static class UseStaminaPatch
        {
            private static bool Prefix(ref ZNetView ___m_nview, float ___m_maxStamina)
            {
                if (Plugin.NoStamina && ___m_nview.IsValid() && ___m_nview.IsOwner())
                {
                    ___m_nview.GetZDO().Set("stamina", ___m_maxStamina);
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "HaveStamina")]
        public static class HaveStaminaPatch
        {
            private static bool Prefix(ref bool __result, ref ZNetView ___m_nview)
            {
                if (Plugin.NoStamina && ___m_nview.IsValid() && ___m_nview.IsOwner())
                {
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "UseEitr")]
        public static class UseEitrPatch
        {
            private static bool Prefix(ref ZNetView ___m_nview, float ___m_maxEitr)
            {
                if (Plugin.NoMana && ___m_nview.IsValid() && ___m_nview.IsOwner())
                {
                    ___m_nview.GetZDO().Set("eitr", 1000f);
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "HaveEitr")]
        public static class HaveEitrPatch
        {
            private static bool Prefix(ref bool __result, ref ZNetView ___m_nview)
            {
                if (Plugin.NoMana && ___m_nview.IsValid() && ___m_nview.IsOwner())
                {
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "GetMaxEitr")]
        public static class GetMaxEitrPatch
        {
            private static bool Prefix(ref float __result, ref ZNetView ___m_nview)
            {
                if (Plugin.NoMana && ___m_nview.IsValid() && ___m_nview.IsOwner())
                {
                    __result = 1000f;
                    return false;
                }

                return true;
            }
        }
        
        [HarmonyPatch(typeof(Player), "UpdateStats")]
        public static class UpdateStatsEitrPatch
        {
            private static void Postfix(ref ZNetView ___m_nview, ref float ___m_eitr)
            {
                if (Plugin.NoMana && ___m_nview.IsValid() && ___m_nview.IsOwner())
                {
                    ___m_eitr = 1000f;
                }
            }
        }

        [HarmonyPatch(typeof(Terminal), "TryRunCommand")]
        public static class CommandChainingPatch
        {
            private static bool Prefix(ref string text)
            {
                text = Plugin.Handler.ReplaceAlias(text);

                List<string> commands = text.SplitEscaped(';');

                // Avoid recursion. Don't want to call this prefix infinitely...
                if (commands.Count > 1)
                {
                    foreach (string command in commands)
                        Console.instance.TryRunCommand(command);

                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Character), "GetSlideAngle")]
        public static class SpidermanSlidePatch
        {
            private static void Postfix(ref float __result, ref ZNetView ___m_nview)
            {
                if (Plugin.NoSlide && ___m_nview.IsOwner())
                    __result = 90f;
            }
        }

        [HarmonyPatch(typeof(Character), "UpdateBodyFriction")]
        public static class SpidermanFrictionPatch
        {
            private static void Postfix(ref Collider ___m_collider, ref Vector3 ___m_moveDir, ref ZNetView ___m_nview)
            {
                if (Plugin.NoSlide && ___m_nview.IsOwner())
                {
                    if (___m_moveDir.magnitude < 0.1f)
                    {
                        ___m_collider.material.frictionCombine = PhysicMaterialCombine.Maximum;
                        ___m_collider.material.staticFriction = 1f;
                        ___m_collider.material.dynamicFriction = 1f;
                    }
                    else
                    {
                        ___m_collider.material.frictionCombine = PhysicMaterialCombine.Minimum;
                        ___m_collider.material.staticFriction = 0f;
                        ___m_collider.material.dynamicFriction = 0f;
                    }
                }
            }
        }
    }
}
