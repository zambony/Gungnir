using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using Gungnir.Patch;
using System;
using System.Text;
using System.IO;

namespace Gungnir
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInProcess("valheim.exe")]
    public class Gungnir : BaseUnityPlugin
    {
        public const string ModName    = "Gungnir";
        public const string ModOrg     = "zamboni";
        public const string ModGUID    = ModOrg + "." + ModName;
        public const string ModVersion = "1.2.0";

        private readonly Harmony m_harmony = new Harmony(ModGUID);
        private CommandHandler   m_handler = new CommandHandler();
        private CustomConsole    m_console;

        private Dictionary<KeyCode, string> m_binds = new Dictionary<KeyCode, string>();

        public bool BuildAnywhere       = false;
        public bool NoStructuralSupport = false;
        public bool NoStamina           = false;

        internal Dictionary<KeyCode, string> Binds { get => m_binds; set => m_binds = value; }
        internal CommandHandler Handler { get => m_handler; set => m_handler = value; }

        /// <summary>
        /// Save all of the user's console keybinds to a file in the BepInEx config folder.
        /// </summary>
        public void SaveBinds()
        {
            StringBuilder builder = new StringBuilder();

            foreach (KeyValuePair<KeyCode, string> pair in m_binds)
                builder.AppendLine($"{pair.Key}={pair.Value}");

            string output = builder.ToString();
            Debug.Log(output);

            string path = Path.Combine(Paths.ConfigPath, ModGUID + "_binds.txt");
            File.WriteAllText(path, output);
        }

        /// <summary>
        /// Load the user's console keybinds from the file in the BepInEx config folder.
        /// </summary>
        public void LoadBinds()
        {
            string path = Path.Combine(Paths.ConfigPath, ModGUID + "_binds.txt");

            if (!File.Exists(path))
                return;

            string[] lines = File.ReadAllLines(path);

            foreach (string line in lines)
            { 
                // Only split by the first instance of equals.
                string[] info = line.Trim().Split(new char[] {'='}, 1);

                if (info.Length != 2)
                    continue;

                if (!Enum.TryParse(info[0].Trim(), true, out KeyCode key))
                    continue;

                m_binds.Add(key, info[1].Trim());
            }
        }

        void Awake()
        {
            PatchManager.Plugin = this;
            ConfigManager.Init(Config);
            LoadBinds();
            m_harmony.PatchAll(typeof(PatchManager).Assembly);
        }

        void OnDestroy()
        {
            m_harmony.UnpatchSelf();
            // Destroy the handler so things re-register.
            m_handler = null;
        }

        void Start()
        {
            // Console object is a component so that it can think n stuff.
            m_console = gameObject.AddComponent<CustomConsole>();
            m_console.Handler = m_handler;
            m_handler.Console = m_console;
            m_handler.Plugin  = this;

            DontDestroyOnLoad(gameObject);
            transform.parent = null;
            // Attach the console manager to us.
            m_console.transform.parent = transform;
        }

        void Update()
        {
            if (Console.IsVisible())
                return;

            foreach (KeyValuePair<KeyCode, string> pair in m_binds)
            {
                if (Input.GetKeyDown(pair.Key) && Console.instance != null)
                    Console.instance.TryRunCommand(pair.Value);
            }
        }
    }
}
