using BepInEx;
using HarmonyLib;
using UnityEngine;
using Consol.Patch;

namespace Consol
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInProcess("valheim.exe")]
    public class Consol : BaseUnityPlugin
    {
        public const string ModName    = "Consol";
        public const string ModOrg     = "zamboni";
        public const string ModGUID    = ModOrg + "." + ModName;
        public const string ModVersion = "1.0.0";

        private readonly Harmony m_harmony = new Harmony(ModGUID);
        private CommandHandler   m_handler = new CommandHandler();
        private CustomConsole    m_console;

        public bool BuildAnywhere = false;
        public bool NoStructuralSupport = false;

        void Awake()
        {
            PatchManager.Plugin = this;
            ConfigManager.Init(Config);
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
    }
}
