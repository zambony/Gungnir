using BepInEx.Configuration;
using System.Collections.Generic;
using UnityEngine;

namespace Consol
{
    internal static class ConfigManager
    {
        private static Dictionary<string, ConfigEntryBase> s_config = new Dictionary<string, ConfigEntryBase>();

        public static void Init(ConfigFile config)
        {
            s_config.Add("ConsoleEnabled", config.Bind<bool>("General", "ConsoleEnabled", true, "Whether the console should be enabled or not. Defaults to true."));
        }

        public static T get<T>(string key)
        {
            ConfigEntryBase obj;
            ConfigEntry<T> value;

            if (!s_config.TryGetValue(key, out obj))
                Logger.Error($"Attempt to access nonexistent config value '{key}'.");

            value = obj as ConfigEntry<T>;

            return value.Value;
        }
    }
}
