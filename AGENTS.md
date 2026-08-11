Include ..\AGENTS.md

# Storage Panel Tweaks — Mod-Specific Agent Instructions

## Identity
- **Assembly:** `storagepaneltweaks`
- **Namespace:** `StoragePanelTweaks`
- **ModId:** `Calloatti.StoragePanelTweaks`
- **Framework:** Harmony, Bindito DI
- **Min Game Version:** 1.0.13.0 — uses `timberborn-decompiled-1.0.*`

## What This Mod Does
Tweaks the storage panel UI: removes unnecessary stock capacity indicators, adds storage row sorting, and modifies storage tab behavior.

## Source Architecture (`Version-1.0/Source/`)

| File | Role |
|---|---|
| `ModStarter.cs` | Entry point — `IModStarter` |
| `ModConfigurator.cs` | DI configurator |
| `StorageTabPatch.cs` | Storage tab UI patches — `_rows`, `_headerRow`, and `VisibleChildrenCount` on `BatchControlRowGroup` are accessed **directly** (BatchControl is publicized via `CommonModSettings.props`; was `Traverse` before publicization) |
| `StorageRowComparer.cs` | Row sorting logic |
| `StockCapacityRemovalPatch.cs` | Stock capacity visual removal |
