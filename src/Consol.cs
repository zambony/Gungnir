using BepInEx;
using HarmonyLib;
using UnityEngine;

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
        private readonly CommandHandler m_handler;

        public Consol() : base()
        {
            m_harmony = new Harmony(ModGUID);
            m_handler = new CommandHandler();
        }

        void Awake() => m_harmony.PatchAll();

        void Start()
        {
            DontDestroyOnLoad(this.gameObject);
            this.transform.parent = null;
        }
    }
}
