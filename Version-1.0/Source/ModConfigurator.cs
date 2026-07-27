using System;
using Bindito.Core;
using Timberborn.BatchControl;
using Timberborn.DropdownSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.GoodsUI;
using Timberborn.Localization;

namespace StoragePanelTweaks
{
  [Context("Game")]
  public class ModConfigurator : Configurator
  {
    protected override void Configure()
    {
      // Bind our custom injector so Bindito instantiates it when the game starts
      Bind<DependencyInjector>().AsSingleton();
    }
  }

  // Implementing IDisposable ensures Bindito calls Dispose() when the scene unloads
  public class DependencyInjector : IDisposable
  {
    public DependencyInjector(
        DropdownListDrawer dropdownListDrawer,
        DropdownItemsSetter dropdownItemsSetter,
        IGoodService goodService,
        GoodDescriber goodDescriber,
        ILoc loc,
        EntityRegistry entityRegistry,
        BatchControlDistrict batchControlDistrict)
    {
      StoragePanelTweaksTabPatch.DropdownListDrawer = dropdownListDrawer;
      StoragePanelTweaksTabPatch.DropdownItemsSetter = dropdownItemsSetter;
      StoragePanelTweaksTabPatch.GoodService = goodService;
      StoragePanelTweaksTabPatch.GoodDescriber = goodDescriber;
      StoragePanelTweaksTabPatch.Loc = loc;

      // Injecting the native game registries
      StoragePanelTweaksTabPatch.EntityRegistry = entityRegistry;
      StoragePanelTweaksTabPatch.BatchControlDistrict = batchControlDistrict;
    }

    public void Dispose()
    {
      // Prevent dangling references when returning to the Main Menu
      StoragePanelTweaksTabPatch.DropdownListDrawer = null;
      StoragePanelTweaksTabPatch.DropdownItemsSetter = null;
      StoragePanelTweaksTabPatch.GoodService = null;
      StoragePanelTweaksTabPatch.GoodDescriber = null;
      StoragePanelTweaksTabPatch.Loc = null;
      StoragePanelTweaksTabPatch.EntityRegistry = null;
      StoragePanelTweaksTabPatch.BatchControlDistrict = null;
    }
  }
}