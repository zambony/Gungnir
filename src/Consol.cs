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
        private GameObject       m_console;

        void Awake()
        {
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
            m_console = new GameObject("Consol");
            m_console.AddComponent<CustomConsole>();
            CustomConsole guiConsole = m_console.GetComponent<CustomConsole>();
            guiConsole.Handler = m_handler;
            m_handler.Console = guiConsole;

            DontDestroyOnLoad(gameObject);
            transform.parent = null;
            // Attach the console manager to us.
            m_console.transform.parent = transform;
        }
    }
}
