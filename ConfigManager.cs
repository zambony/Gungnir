using BepInEx.Configuration;
using System.Collections.Generic;
using UnityEngine;

namespace Consol
{
    internal static class ConfigManager
    {
        private static Dictionary<string, ConfigEntryBase> s_config;

        public static void Init(ConfigFile config)
        {
            s_config = new Dictionary<string, ConfigEntryBase>()
            {
                {"ConsoleEnabled", config.Bind<bool>("General", "ConsoleEnabled", true, "Whether the console should be enabled or not. Defaults to true.")}
            };
        }

        public static T get<T>(string key)
        {
            ConfigEntryBase obj;
            ConfigEntry<T> value;

            if (!s_config.TryGetValue(key, out obj))
                Debug.LogError($"[Consol] Attempt to access nonexistent config value '{key}'.");

            value = obj as ConfigEntry<T>;

            return value.Value;
        }
    }
}
