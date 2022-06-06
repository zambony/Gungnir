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

        private readonly Harmony m_harmony;
        private CommandHandler m_handler;

        public Consol() : base()
        {
            m_harmony = new Harmony(ModGUID);
            m_handler = new CommandHandler();
        }

        void Awake() => m_harmony.PatchAll(typeof(PatchManager).Assembly);
        void OnDestroy()
        {
            m_harmony.UnpatchSelf();
            // Destroy the handler so things re-register.
            m_handler = null;
        }

        void Start()
        {
            DontDestroyOnLoad(this.gameObject);
            this.transform.parent = null;

            ConfigManager.Init(Config);
        }
    }
}
