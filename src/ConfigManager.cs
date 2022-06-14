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
            s_config.Add("BindList", config.Bind("General", "BindList", "{}", "A JSON list of commands bound to key codes. Edit manually at your own peril."));
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

        /// <summary>
        /// Set a specific config value by name.
        /// </summary>
        /// <typeparam name="T">Type of value stored at the given <paramref name="key"/></typeparam>
        /// <param name="key">Config value to set.</param>
        /// <param name="value">Value to set the key to.</param>
        /// <returns></returns>
        public static void Set<T>(string key, T value)
        {
            if (!s_config.TryGetValue(key, out ConfigEntryBase obj))
            {
                Logger.Error($"Attempt to access nonexistent config value '{key}'.");
                return;
            }

            var converted = obj as ConfigEntry<T>;
            converted.Value = value;
        }
    }
}
