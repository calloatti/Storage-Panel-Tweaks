using HarmonyLib;
using Timberborn.BatchControl;
using Timberborn.StockpilesUI;
using UnityEngine.UIElements;

namespace StoragePanelTweaks
{
    [HarmonyPatch]
    public static class StockCapacityRemovalPatch
    {
        [HarmonyPatch(typeof(StockpileBatchControlRowItemFactory), nameof(StockpileBatchControlRowItemFactory.Create))]
        [HarmonyPostfix]
        public static void RemoveCapacityLabel(ref IBatchControlRowItem __result)
        {
            if (__result == null) return;

            var root = __result.Root;
            if (root == null) return;

            // Find the CapacityWrapper element
            var capacityWrapper = root.Q<VisualElement>("CapacityWrapper");
            if (capacityWrapper != null)
            {
                // Find and remove the "Capacity:" label
                var capacityHeader = capacityWrapper.Q<Label>("CapacityHeader");
                if (capacityHeader != null)
                {
                    capacityHeader.style.display = DisplayStyle.None;
                }
            }
        }
    }
}
