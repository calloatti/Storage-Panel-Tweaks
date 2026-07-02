using System.Collections.Generic;
using Timberborn.BatchControl;
using Timberborn.BlockSystem;
using Timberborn.ConstructionSites;
using Timberborn.EntityNaming;
using Timberborn.EntitySystem;
using Timberborn.InventorySystem;
using Timberborn.StockpilePrioritySystem;
using Timberborn.Stockpiles;
using Timberborn.Buildings;
using Timberborn.Localization;
using Timberborn.Goods;
using Timberborn.GoodsUI;
using Timberborn.BuilderPrioritySystem;
using Timberborn.Hauling;
using Timberborn.Automation;
using Timberborn.StatusSystem;
using HarmonyLib;

namespace StoragePanelTweaks
{
  public class StorageRowComparer : IComparer<BatchControlRow>
  {
    public string SortColumn = "Name";
    public bool Ascending = true;

    private GoodDescriber _goodDescriber;

    public void SetGoodDescriber(GoodDescriber goodDescriber)
    {
      _goodDescriber = goodDescriber;
    }

    public int Compare(BatchControlRow x, BatchControlRow y)
    {
      if (x == null || y == null) return 0;
      if (x.Entity == null || y.Entity == null) return 0;

      int result = CompareByColumn(x.Entity, y.Entity);
      if (result == 0)
      {
        result = CompareNames(x.Entity, y.Entity);
      }
      return Ascending ? result : -result;
    }

    private int CompareByColumn(EntityComponent x, EntityComponent y)
    {
      switch (SortColumn)
      {
        case "Name":
          return CompareNames(x, y);
        case "Stock": // Percentage Sort
          return CompareStock(x, y);
        case "ActualStock": // Absolute Stock Sort
          return CompareActualStock(x, y);
        case "Capacity": // Absolute Capacity Sort
          return CompareCapacity(x, y);
        case "Good":
          return CompareGood(x, y);
        case "Hauling":
          return CompareHauling(x, y);
        case "Automatable":
          return CompareAutomatable(x, y);
        case "Status":
          return CompareStatus(x, y);
        default:
          return CompareNames(x, y);
      }
    }

    private int CompareNames(EntityComponent x, EntityComponent y)
    {
      var namedX = x.GetComponent<NamedEntity>();
      var namedY = y.GetComponent<NamedEntity>();
      string nameX = namedX != null ? namedX.EntityName : x.GetComponent<LabeledEntity>().DisplayName;
      string nameY = namedY != null ? namedY.EntityName : y.GetComponent<LabeledEntity>().DisplayName;
      return string.CompareOrdinal(nameX, nameY);
    }

    private int CompareStock(EntityComponent x, EntityComponent y)
    {
      var stockX = x.GetComponent<Stockpile>();
      var stockY = y.GetComponent<Stockpile>();
      if (stockX == null || stockY == null) return 0;

      float fillX = (float)stockX.Inventory.TotalAmountInStock / stockX.Inventory.Capacity;
      float fillY = (float)stockY.Inventory.TotalAmountInStock / stockY.Inventory.Capacity;

      int result = fillX.CompareTo(fillY);
      if (result == 0) // Subsort by capacity
      {
        result = stockX.Inventory.Capacity.CompareTo(stockY.Inventory.Capacity);
      }
      return result;
    }

    private int CompareActualStock(EntityComponent x, EntityComponent y)
    {
      var stockX = x.GetComponent<Stockpile>();
      var stockY = y.GetComponent<Stockpile>();
      if (stockX == null || stockY == null) return 0;

      int result = stockX.Inventory.TotalAmountInStock.CompareTo(stockY.Inventory.TotalAmountInStock);
      if (result == 0) // Subsort by capacity
      {
        result = stockX.Inventory.Capacity.CompareTo(stockY.Inventory.Capacity);
      }
      return result;
    }

    private int CompareCapacity(EntityComponent x, EntityComponent y)
    {
      var stockX = x.GetComponent<Stockpile>();
      var stockY = y.GetComponent<Stockpile>();
      if (stockX == null || stockY == null) return 0;

      int result = stockX.Inventory.Capacity.CompareTo(stockY.Inventory.Capacity);
      if (result == 0) // Subsort by stock
      {
        result = stockX.Inventory.TotalAmountInStock.CompareTo(stockY.Inventory.TotalAmountInStock);
      }
      return result;
    }

    private int CompareGood(EntityComponent x, EntityComponent y)
    {
      var allowX = x.GetComponent<SingleGoodAllower>();
      var allowY = y.GetComponent<SingleGoodAllower>();
      if (allowX == null || allowY == null) return 0;

      if (_goodDescriber == null) return 0;

      string goodX = allowX.HasAllowedGood ? _goodDescriber.Describe(allowX.AllowedGood) : string.Empty;
      string goodY = allowY.HasAllowedGood ? _goodDescriber.Describe(allowY.AllowedGood) : string.Empty;
      return string.CompareOrdinal(goodX, goodY);
    }

    private int ComparePriority(EntityComponent x, EntityComponent y)
    {
      var prioX = x.GetComponent<StockpilePriority>();
      var prioY = y.GetComponent<StockpilePriority>();
      if (prioX == null || prioY == null) return 0;

      return GetPriorityValue(prioX).CompareTo(GetPriorityValue(prioY));
    }

    private int GetPriorityValue(StockpilePriority prio)
    {
      if (prio.IsObtainActive) return 3;
      if (prio.IsAcceptActive) return 2;
      if (prio.IsSupplyActive) return 1;
      if (prio.IsEmptyActive) return 0;
      return -1;
    }

    private int CompareConstructionPriority(EntityComponent x, EntityComponent y)
    {
      var siteX = x.GetComponent<BuilderPrioritizable>();
      var siteY = y.GetComponent<BuilderPrioritizable>();
      if (siteX == null || siteY == null) return 0;
      return siteX.Priority.CompareTo(siteY.Priority);
    }

    private int CompareHauling(EntityComponent x, EntityComponent y)
    {
      var haulX = x.GetComponent<HaulPrioritizable>();
      var haulY = y.GetComponent<HaulPrioritizable>();
      if (haulX == null && haulY == null) return 0;
      if (haulX == null) return -1;
      if (haulY == null) return 1;
      return haulX.Prioritized.CompareTo(haulY.Prioritized);
    }

    private int CompareAutomatable(EntityComponent x, EntityComponent y)
    {
      var priX = x.GetComponent<StockpilePriority>();
      var priY = y.GetComponent<StockpilePriority>();
      if (priX == null && priY == null) return 0;
      if (priX == null) return -1;
      if (priY == null) return 1;
      return GetModeValue(priX).CompareTo(GetModeValue(priY));
    }

    private int GetModeValue(StockpilePriority priority)
    {
      if (priority.IsAcceptActive) return 0;
      if (priority.IsObtainActive) return 1;
      if (priority.IsSupplyActive) return 2;
      if (priority.IsEmptyActive) return 3;
      return 4;
    }

    private int CompareStatus(EntityComponent x, EntityComponent y)
    {
      var statusX = x.GetComponent<StatusSubject>();
      var statusY = y.GetComponent<StatusSubject>();
      if (statusX == null && statusY == null) return 0;
      if (statusX == null) return -1;
      if (statusY == null) return 1;
      return statusX.ActiveStatuses.Count.CompareTo(statusY.ActiveStatuses.Count);
    }
  }
}