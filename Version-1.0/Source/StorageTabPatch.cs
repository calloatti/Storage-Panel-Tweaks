using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Timberborn.BatchControl;
using Timberborn.Buildings;
using Timberborn.CoreUI;
using Timberborn.DropdownSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.GoodsUI;
using Timberborn.InventorySystem;
using Timberborn.Localization;
using Timberborn.Stockpiles;
using Timberborn.StockpilesUI;
using Timberborn.StorageBatchControl;
using UnityEngine;
using UnityEngine.UIElements;

namespace StoragePanelTweaks
{
  [HarmonyPatch]
  public static class StoragePanelTweaksTabPatch
  {
    // Dependencies injected via ModConfigurator
    public static DropdownListDrawer DropdownListDrawer;
    public static DropdownItemsSetter DropdownItemsSetter;
    public static IGoodService GoodService;
    public static GoodDescriber GoodDescriber;
    public static ILoc Loc;

    // Native Game Registries
    public static EntityRegistry EntityRegistry;
    public static BatchControlDistrict BatchControlDistrict;

    private static readonly StorageRowComparer _comparer = new StorageRowComparer();
    private static StorageBatchControlRowFactory _rowFactory;
    private static List<ColumnLayout> _columnLayouts;
    private static StorageBatchControlTab _currentTab;
    private static VisualElement _currentHeader;

    // Filter variables
    private static Dropdown _filterDropdown;
    private static FilterDropdownProvider _filterProvider;
    public static string _selectedFilter = "All";
    public static List<string> _availableGoods;
    private static List<string> _allKnownGoods;
    private static HashSet<string> _goodsInUse = new HashSet<string>();

    private const string SORT_ID_NAME = "Name";
    private const string SORT_ID_STOCK = "Stock";
    private const string SORT_ID_ACTUAL_STOCK = "ActualStock";
    private const string SORT_ID_CAPACITY = "Capacity";
    private const string SORT_ID_GOOD = "Good";
    private const string SORT_ID_PRI = "Hauling";
    private const string SORT_ID_MODE = "Automatable";
    private const string SORT_ID_STATUS = "Status";

    [HarmonyPatch(typeof(BatchControlTab), "GetHeader")]
    [HarmonyPrefix]
    public static bool GetHeaderPrefix(BatchControlTab __instance, ref VisualElement __result)
    {
      if (!(__instance is StorageBatchControlTab storageTab))
      {
        return true;
      }

      _currentTab = storageTab;

      _rowFactory = Traverse.Create(storageTab).Field("_storageBatchControlRowFactory").GetValue<StorageBatchControlRowFactory>();

      var emptyLabel = Traverse.Create(__instance).Field("_emptyLabel").GetValue<Label>();
      if (emptyLabel != null)
      {
        emptyLabel.BringToFront();
        emptyLabel.style.marginTop = 16f;
      }

      if (_allKnownGoods == null && GoodService != null && GoodDescriber != null)
      {
        var goodsList = GoodService.Goods.ToList();
        goodsList.Sort((a, b) => string.CompareOrdinal(GoodDescriber.Describe(a), GoodDescriber.Describe(b)));

        _allKnownGoods = new List<string> { "All", "None" };
        _allKnownGoods.AddRange(goodsList);
      }

      VisualElement headerRoot = CreateDynamicHeader(storageTab);
      _currentHeader = headerRoot;
      __result = headerRoot;
      return false;
    }

    private static bool BelongsToDistrict(EntityComponent entity, DistrictCenter selectedDistrict)
    {
      if (selectedDistrict == null) return true;

      var districtBuilding = entity.GetComponent<DistrictBuilding>();
      if (districtBuilding != null)
      {
        var district = districtBuilding.GetInstantOrConstructionDistrict();
        if (district != null && district == selectedDistrict)
        {
          return true;
        }

        var accessible = entity.GetComponent<BuildingAccessible>();
        if (accessible != null)
        {
          return selectedDistrict.IsOnPreviewDistrictRoad(accessible.CalculateAccess());
        }
      }
      return false;
    }

    private static void UpdateAvailableGoods()
    {
      if (_allKnownGoods == null) return;

      _goodsInUse.Clear();

      if (EntityRegistry != null)
      {
        DistrictCenter selectedDistrict = BatchControlDistrict?.SelectedDistrict;

        foreach (var entity in EntityRegistry.Entities)
        {
          if (entity == null || entity.GetComponent<Stockpile>() == null) continue;
          if (selectedDistrict != null && !BelongsToDistrict(entity, selectedDistrict)) continue;

          var allower = entity.GetComponent<SingleGoodAllower>();
          if (allower != null && allower.HasAllowedGood)
          {
            _goodsInUse.Add(allower.AllowedGood);
          }
        }
      }

      _availableGoods = new List<string>();
      foreach (var good in _allKnownGoods)
      {
        if (good == "All" || good == "None" || _goodsInUse.Contains(good) || good == _selectedFilter)
        {
          _availableGoods.Add(good);
        }
      }
    }

    private static VisualElement CreateDynamicHeader(StorageBatchControlTab storageTab)
    {
      VisualElement headerRoot = new VisualElement();
      headerRoot.AddToClassList("batch-control-header-row");
      headerRoot.style.flexDirection = FlexDirection.Row;
      headerRoot.style.alignItems = Align.Center;
      headerRoot.style.justifyContent = Justify.FlexStart;
      headerRoot.style.height = 28;

      headerRoot.style.paddingLeft = 0;
      headerRoot.style.paddingRight = 8;

      if (_columnLayouts == null)
      {
        _columnLayouts = new List<ColumnLayout>
                {
                    new ColumnLayout { Label = "Name", SortId = SORT_ID_NAME, MarginRight = 4f },
                    new ColumnLayout { Label = "Stock", SortId = SORT_ID_ACTUAL_STOCK, MarginRight = 4f },
                    new ColumnLayout { Label = "Capacity", SortId = SORT_ID_CAPACITY, MarginRight = 4f },
                    new ColumnLayout { Label = "Stock/Capacity", SortId = SORT_ID_STOCK, MarginRight = 4f },
                    new ColumnLayout { Label = "Good", SortId = SORT_ID_GOOD, MarginRight = 4f },
                    new ColumnLayout { Label = "Priority", SortId = SORT_ID_PRI, MarginRight = 4f },
                    new ColumnLayout { Label = "Mode", SortId = SORT_ID_MODE, MarginRight = 4f },
                    new ColumnLayout { Label = "Status", SortId = SORT_ID_STATUS, MarginRight = 4f }
                };
      }

      foreach (var layout in _columnLayouts)
      {
        AddHeaderColumn(headerRoot, layout);
      }

      if (DropdownListDrawer != null && DropdownItemsSetter != null)
      {
        _filterDropdown = new Dropdown();
        _filterDropdown.Initialize(DropdownListDrawer);

        _filterDropdown.style.marginLeft = 0f;
        _filterDropdown.style.width = 200f;
        _filterDropdown.style.minHeight = 30f;
        _filterDropdown.style.top = -3f;

        var label = _filterDropdown.Q<Label>("Label");
        if (label != null) label.ToggleDisplayStyle(false);

        if (_filterProvider == null)
        {
          _filterProvider = new FilterDropdownProvider();
        }

        UpdateAvailableGoods();
        DropdownItemsSetter.SetItems(_filterDropdown, _filterProvider);
        headerRoot.Add(_filterDropdown);

        _filterDropdown.RegisterCallback<PointerEnterEvent>(evt =>
        {
          if (DropdownItemsSetter != null && _filterProvider != null)
          {
            DropdownItemsSetter.SetItems(_filterDropdown, _filterProvider);
          }
        });
      }

      headerRoot.RegisterCallback<AttachToPanelEvent>(evt =>
      {
        if (headerRoot.parent?.parent != null)
        {
          var fallbackEmptyLabel = headerRoot.parent.parent.Query<Label>().ToList()
              .FirstOrDefault(l => l.name != null && l.name.ToLower().Contains("empty"));

          if (fallbackEmptyLabel != null)
          {
            fallbackEmptyLabel.BringToFront();
            fallbackEmptyLabel.style.marginTop = 16f;
          }
        }
      });

      headerRoot.RegisterCallback<DetachFromPanelEvent>(evt =>
      {
        if (_currentHeader == headerRoot)
        {
          _currentHeader = null;
          _currentTab = null;
          _filterDropdown = null;
        }
      });

      return headerRoot;
    }

    private static void AddHeaderColumn(VisualElement headerRoot, ColumnLayout layout)
    {
      var container = new VisualElement();
      container.style.flexDirection = FlexDirection.Row;
      container.style.alignItems = Align.Center;
      container.style.minHeight = 28;

      container.style.top = -4f;

      container.style.backgroundColor = new Color(21f / 255f, 39f / 255f, 34f / 255f);

      container.style.borderTopColor = new Color(186f / 255f, 160f / 255f, 107f / 255f);
      container.style.borderBottomColor = new Color(186f / 255f, 160f / 255f, 107f / 255f);
      container.style.borderLeftColor = new Color(186f / 255f, 160f / 255f, 107f / 255f);
      container.style.borderRightColor = new Color(186f / 255f, 160f / 255f, 107f / 255f);

      container.style.borderTopWidth = 1f;
      container.style.borderBottomWidth = 1f;
      container.style.borderLeftWidth = 1f;
      container.style.borderRightWidth = 1f;

      container.style.borderTopLeftRadius = 2f;
      container.style.borderTopRightRadius = 2f;
      container.style.borderBottomLeftRadius = 2f;
      container.style.borderBottomRightRadius = 2f;

      container.style.paddingLeft = 8f;
      container.style.paddingRight = 8f;
      container.style.marginRight = layout.MarginRight;

      var label = new Label(layout.Label);

      // Changed to use the game's native game-text-small class (12px, Muted Gray)
      label.AddToClassList("game-text-small");
      label.style.whiteSpace = WhiteSpace.NoWrap;
      label.style.top = -0f;

      if (_comparer.SortColumn == layout.SortId)
      {
        label.text += _comparer.Ascending ? " ▲" : " ▼";

        // Changed to use the exact Timberborn yellowish-gold highlight color
        label.style.color = new Color(188f / 255f, 162f / 255f, 108f / 255f);
      }

      container.AddToClassList("clickable");
      container.RegisterCallback<ClickEvent>(evt => HandleHeaderClick(layout.SortId));

      container.Add(label);
      headerRoot.Add(container);
    }

    private static void HandleHeaderClick(string columnId)
    {
      if (_comparer.SortColumn == columnId)
      {
        _comparer.Ascending = !_comparer.Ascending;
      }
      else
      {
        _comparer.SortColumn = columnId;
        _comparer.Ascending = true;
      }

      if (_currentTab != null && _currentHeader != null)
      {
        _currentHeader.Clear();
        foreach (var layout in _columnLayouts)
        {
          AddHeaderColumn(_currentHeader, layout);
        }

        if (_filterDropdown != null)
        {
          _currentHeader.Add(_filterDropdown);
        }

        _currentTab.IsDirty = true;
      }
    }

    [HarmonyPatch(typeof(StorageBatchControlTab), "GetRowGroups")]
    [HarmonyPrefix]
    public static bool GetRowGroupsPrefix(
        StorageBatchControlTab __instance,
        IEnumerable<EntityComponent> entities,
        ref IEnumerable<BatchControlRowGroup> __result,
        StorageBatchControlRowFactory ____storageBatchControlRowFactory,
        BatchControlRowGroupFactory ____batchControlRowGroupFactory)
    {
      if (GoodDescriber != null)
      {
        _comparer.SetGoodDescriber(GoodDescriber);
      }

      var storageEntities = entities.Where(e => e.GetComponent<Stockpile>() != null);

      VisualElement emptyHeaderRoot = new VisualElement();
      emptyHeaderRoot.style.height = 0;
      BatchControlRow dummyHeader = new BatchControlRow(emptyHeaderRoot);

      BatchControlRowGroup group = ____batchControlRowGroupFactory.CreateUnsorted(dummyHeader);
      Traverse.Create(group).Field("_comparer").SetValue(_comparer);

      foreach (var entity in storageEntities)
      {
        group.AddRow(____storageBatchControlRowFactory.Create(entity));
      }

      __result = new List<BatchControlRowGroup> { group };
      return false;
    }

    [HarmonyPatch(typeof(BatchControlRowGroup), "UpdateVisibleRows")]
    [HarmonyPostfix]
    public static void UpdateVisibleRowsPostfix(BatchControlRowGroup __instance, DistrictCenter selectedDistrict, ref bool __result)
    {
      if (_currentTab == null) return;

      var rows = Traverse.Create(__instance).Field("_rows").GetValue<List<BatchControlRow>>();
      if (rows == null) return;

      int visibleCount = 0;
      foreach (var row in rows)
      {
        if (row.Root.style.display == DisplayStyle.None) continue;

        var entity = row.Entity;
        if (entity != null && entity.GetComponent<Stockpile>() != null)
        {
          bool matchesFilter = true;
          if (_selectedFilter != "All")
          {
            var allower = entity.GetComponent<SingleGoodAllower>();
            bool hasGood = allower != null && allower.HasAllowedGood;

            if (_selectedFilter == "None")
            {
              matchesFilter = !hasGood;
            }
            else
            {
              matchesFilter = hasGood && allower.AllowedGood == _selectedFilter;
            }
          }

          if (!matchesFilter)
          {
            row.Root.ToggleDisplayStyle(false);
          }
          else
          {
            visibleCount++;
          }
        }
        else
        {
          visibleCount++;
        }
      }

      var headerRow = Traverse.Create(__instance).Field("_headerRow").GetValue<BatchControlRow>();
      if (headerRow != null)
      {
        bool showHeader = visibleCount > 0;
        headerRow.Root.ToggleDisplayStyle(showHeader);
        __result = showHeader;
      }

      Traverse.Create(__instance).Property("VisibleChildrenCount").SetValue(visibleCount);
    }

    private class ColumnLayout
    {
      public string Label { get; set; }
      public string SortId { get; set; }
      public float MarginRight { get; set; }
    }

    private class FilterDropdownProvider : IExtendedDropdownProvider
    {
      public IReadOnlyList<string> Items
      {
        get
        {
          UpdateAvailableGoods();
          return _availableGoods ?? new List<string> { "All", "None" };
        }
      }

      public string GetValue() => _selectedFilter;

      public void SetValue(string value)
      {
        _selectedFilter = value;
        if (_currentTab != null)
        {
          _currentTab.IsDirty = true;
        }

        if (_filterDropdown != null && DropdownItemsSetter != null)
        {
          DropdownItemsSetter.SetItems(_filterDropdown, this);
        }
      }

      public string FormatDisplayText(string value, bool selected)
      {
        if (value == "All") return "All";
        if (value == "None") return Loc != null ? Loc.T("Automation.AutomationNone") : "None";
        return GoodDescriber != null ? GoodDescriber.Describe(value) : value;
      }

      public Sprite GetIcon(string value)
      {
        if (value == "All" || value == "None" || GoodDescriber == null) return null;
        return GoodDescriber.GetIcon(value);
      }

      public ImmutableArray<string> GetItemClasses(string value) => ImmutableArray<string>.Empty;
    }
  }
}