using System;
using System.Collections.Generic;
using Dalamud.Plugin.Ipc;

namespace StockTracker;

/// <summary>
/// Thin wrapper around Allagan Tools' (InventoryTools) published IPC - confirmed against its
/// real IPC/IPCService.cs source on GitHub (Critical-Impact/InventoryTools), not guessed channel
/// names. Allagan Tools is an optional dependency: every call here degrades to a safe default
/// (no crash, no false "needs recraft") when it isn't installed, isn't loaded, or the call throws.
///
/// AutoRetainer's own public IPC was considered first but only exposes aggregate per-retainer
/// counts (MBItems, Gil) - no per-item id/quantity - so it cannot tell which item sold. Allagan
/// Tools tracks full per-item, per-location inventory (including each retainer's own market
/// listings, InventoryCategory.RetainerMarket = 9) and exposes it per-item via IPC, which is
/// exactly what's needed here.
/// </summary>
public sealed class AllaganToolsIpc
{
    /// <summary>Allagan Tools' own CriticalCommonLib.Models.InventoryCategory.RetainerMarket value.</summary>
    private const uint RetainerMarketCategory = 9;

    private readonly ICallGateSubscriber<uint, bool, uint[], bool, uint> itemCountOwnedByCategory;

    public AllaganToolsIpc()
    {
        itemCountOwnedByCategory = Plugin.PluginInterface
            .GetIpcSubscriber<uint, bool, uint[], bool, uint>("AllaganTools.ItemCountOwnedByCategory");
    }

    public bool IsAvailable => Plugin.GetPluginState("InventoryTools").Loaded;

    /// <summary>
    /// Total quantity of this item currently sitting across all of the active character's
    /// retainers' own market boards combined (not bags, not the free company chest). Returns 0
    /// if Allagan Tools is unavailable or the call fails - callers must check
    /// <see cref="IsAvailable"/> separately to distinguish "confirmed zero" from "unknown".
    /// </summary>
    public uint GetTotalRetainerMarketQuantity(uint itemId)
    {
        if (!IsAvailable)
            return 0;

        try
        {
            return itemCountOwnedByCategory.InvokeFunc(itemId, true, [RetainerMarketCategory], false);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Allagan Tools ItemCountOwnedByCategory IPC call failed");
            return 0;
        }
    }
}
