using BepInEx.Configuration;
using System.Collections.Generic;

namespace Gungnir
{
    internal static class ConfigManager
    {
        private static Dictionary<string, ConfigEntryBase> s_config = new Dictionary<string, ConfigEntryBase>();

        /// <summary>
        /// Initialize the config file and values.
        /// </summary>
        /// <param name="config">Reference to the global <see cref="ConfigFile"/> created by BepInEx for this plugin.</param>
        public static void Init(ConfigFile config)
        {
            s_config.Add("ConsoleEnabled", config.Bind("General", "ConsoleEnabled", true, "Whether the console should be enabled or not. Defaults to true."));
        }

        /// <summary>
        /// Retrieve a specific config value by name.
        /// </summary>
        /// <typeparam name="T">Type of value stored at the given <paramref name="key"/></typeparam>
        /// <param name="key">Config value to lookup.</param>
        /// <returns>The value contained at that key, otherwise a default value is given.</returns>
        public static T Get<T>(string key)
        {
            ConfigEntryBase obj;
            ConfigEntry<T> value;

            if (!s_config.TryGetValue(key, out obj))
            {
                Logger.Error($"Attempt to access nonexistent config value '{key}'.");
                return default;
            }

            value = obj as ConfigEntry<T>;

            return value.Value;
        }
    }
}
