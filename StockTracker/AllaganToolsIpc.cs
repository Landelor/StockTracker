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

    private readonly ICallGateSubscriber<uint, bool, uint[], bool, Dictionary<ulong, uint>> getItemCountsByCharacter;
    private readonly ICallGateSubscriber<bool, HashSet<ulong>> getCharactersOwnedByActive;

    public AllaganToolsIpc()
    {
        getItemCountsByCharacter = Plugin.PluginInterface
            .GetIpcSubscriber<uint, bool, uint[], bool, Dictionary<ulong, uint>>("AllaganTools.GetItemCountsByCharacter");
        getCharactersOwnedByActive = Plugin.PluginInterface
            .GetIpcSubscriber<bool, HashSet<ulong>>("AllaganTools.GetCharactersOwnedByActive");
    }

    public bool IsAvailable => Plugin.GetPluginState("InventoryTools").Loaded;

    /// <summary>
    /// How much of this item currently sits on each retainer's own market board, keyed by
    /// retainer id. Only retainers belonging to the active character are included. Returns an
    /// empty dictionary (no data, not "zero stock everywhere") if Allagan Tools is unavailable or
    /// the call fails, so callers must treat "no entry" as unknown rather than confirmed-empty.
    /// </summary>
    public Dictionary<ulong, uint> GetRetainerMarketQuantities(uint itemId)
    {
        if (!IsAvailable)
            return [];

        try
        {
            return getItemCountsByCharacter.InvokeFunc(itemId, true, [RetainerMarketCategory], false);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Allagan Tools GetItemCountsByCharacter IPC call failed");
            return [];
        }
    }

    public HashSet<ulong> GetOwnedRetainerIds()
    {
        if (!IsAvailable)
            return [];

        try
        {
            return getCharactersOwnedByActive.InvokeFunc(false);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Allagan Tools GetCharactersOwnedByActive IPC call failed");
            return [];
        }
    }
}
