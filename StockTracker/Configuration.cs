using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StockTracker;

/// <summary>
/// Persisted plugin state: the player's hand-maintained lists of tracked items. Plain JSON file
/// in the plugin config directory (same pattern as AutoMarket's marketCache.json) rather than
/// Dalamud's IPluginConfiguration, since there is nothing version-sensitive here yet.
/// </summary>
public sealed class Configuration
{
    public List<TrackedList> Lists { get; set; } = [];

    /// <summary>
    /// "{ListId}:{ItemId}" keys for (list, item) pairs the player has manually dismissed from the
    /// "needs recraft" view without actually restocking. Cleared automatically once that item's
    /// total stock reaches its target again.
    /// </summary>
    public HashSet<string> DismissedKeys { get; set; } = [];

    public static Configuration Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new Configuration { Lists = [new TrackedList { Name = "Default" }] };

            var config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(path)) ?? new Configuration();
            if (config.Lists.Count == 0)
                config.Lists.Add(new TrackedList { Name = "Default" });
            return config;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "StockTracker configuration could not be loaded; starting fresh");
            return new Configuration { Lists = [new TrackedList { Name = "Default" }] };
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
