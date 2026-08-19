using HarmonyLib;
using Timberborn.ModManagerScene;

namespace StoragePanelTweaks
{
    public class ModStarter : IModStarter
    {
        public void StartMod(IModEnvironment modEnvironment)
        {
new Harmony("Calloatti.StoragePanelTweaks").PatchAll();
        }
    }
}
