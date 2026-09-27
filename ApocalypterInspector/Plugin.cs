using BepInEx;

namespace ApocalypterInspector
{
    [BepInPlugin(
        "com.connor.apocalypter.inspector",
        "Apocalypter Inspector",
        "2.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Logger.LogInfo("inspector 2.1.0 loaded. insert opens the menu, F9 dumps the scene, F8 is the old health/gas test.");
        }

        public static void Pump()
        {
            DebugMenu.Pump();
        }
    }
}
