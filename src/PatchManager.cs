using HarmonyLib;

namespace Consol.Patch
{
    internal static class PatchManager
    {
        [HarmonyPatch(typeof(Console), nameof(Console.IsConsoleEnabled))]
        public static class ConsoleEnablePatch
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ref bool __result)
            {
                __result = ConfigManager.get<bool>("ConsoleEnabled");
            }
        }
    }
}
