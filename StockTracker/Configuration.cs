using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StockTracker;

/// <summary>
/// Persisted plugin state: the player's hand-maintained list of tracked items. Plain JSON file in
/// the plugin config directory (same pattern as AutoMarket's marketCache.json) rather than
/// Dalamud's IPluginConfiguration, since there is nothing version-sensitive here yet.
/// </summary>
public sealed class Configuration
{
    public List<TrackedItem> TrackedItems { get; set; } = [];

    /// <summary>
    /// Item ids currently below target that the player has manually dismissed from the "needs
    /// recraft" list without actually restocking (e.g. they don't plan to make more right now).
    /// Cleared automatically once that item's stock reaches its target again.
    /// </summary>
    public HashSet<uint> DismissedItemIds { get; set; } = [];

    public static Configuration Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new Configuration();

            return JsonSerializer.Deserialize<Configuration>(File.ReadAllText(path)) ?? new Configuration();
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "StockTracker configuration could not be loaded; starting fresh");
            return new Configuration();
        }
    }

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "StockTracker configuration could not be saved");
        }
    }
}
