using HarmonyLib;
using Timberborn.ModManagerScene;

namespace StoragePanelTweaks
{
    public class ModStarter : IModStarter
    {
        public void StartMod(IModEnvironment modEnvironment)
        {
            var harmony = new Harmony("StoragePanelTweaks.Mod");
            harmony.PatchAll(typeof(ModStarter).Assembly);
        }
    }
}
