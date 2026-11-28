using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;

namespace StockTracker;

public sealed partial class Plugin
{
    /// <summary>How often retainer stock levels are recomputed against Allagan Tools' cache.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    private DateTime lastPoll = DateTime.MinValue;

    /// <summary>
    /// First-seen-below-target timestamp per "{ListId}:{ItemId}" key, purely in-memory (resets on
    /// plugin reload). Used only to sort/age the "needs recraft" list - the underlying truth (is
    /// it below target right now) is always recomputed live from Allagan Tools, never trusted
    /// from this cache.
    /// </summary>
    private readonly Dictionary<string, DateTimeOffset> firstSeenBelowTarget = [];

    internal List<StockStatus> Statuses { get; private set; } = [];

    private static string Key(Guid listId, uint itemId) => $"{listId}:{itemId}";

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (DateTime.UtcNow - lastPoll < PollInterval)
            return;
        lastPoll = DateTime.UtcNow;

        RefreshStatuses();
    }

    /// <summary>
    /// Recomputes every tracked item's current total retainer-market quantity (summed across all
    /// retainers) and whether it is below its target, in every list. An item automatically drops
    /// off the "needs recraft" list the moment Allagan Tools reports it back at or above target
    /// (i.e. the player re-listed enough), and any manual dismissal for that (list, item) pair is
    /// cleared at the same time so a future sale re-flags it. The same item id is evaluated
    /// independently per list, since each list can set its own target for it.
    /// </summary>
    internal void RefreshStatuses()
    {
        var now = DateTimeOffset.UtcNow;
        var statuses = new List<StockStatus>();

        // Cache per-item lookups within a single poll, since the same item id can appear in
        // multiple lists and each lookup is an IPC round-trip.
        var quantityCache = new Dictionary<uint, uint>();

        foreach (var list in Config.Lists)
        {
            if (!list.Enabled)
                continue;

            foreach (var item in list.Items)
            {
                if (!item.Enabled)
                    continue;

                if (!quantityCache.TryGetValue(item.ItemId, out var current))
                {
                    current = AllaganTools.GetTotalRetainerMarketQuantity(item.ItemId);
                    quantityCache[item.ItemId] = current;
                }

                var key = Key(list.Id, item.ItemId);
                var below = AllaganTools.IsAvailable && current < item.TargetQuantity;

                if (below)
                {
                    if (!firstSeenBelowTarget.ContainsKey(key))
                        firstSeenBelowTarget[key] = now;
                }
                else
                {
                    firstSeenBelowTarget.Remove(key);
                    if (Config.DismissedKeys.Remove(key))
                        SaveConfig();
                }

                statuses.Add(new StockStatus(list, item, current, below, firstSeenBelowTarget.GetValueOrDefault(key)));
            }
        }

        Statuses = statuses;
    }

    /// <summary>Items below target and not manually dismissed, across every list - what the "Needs Recraft" tab shows.</summary>
    internal IEnumerable<StockStatus> NeedsRecraft => Statuses
        .Where(status => status.NeedsRecraft && !Config.DismissedKeys.Contains(Key(status.List.Id, status.Item.ItemId)))
        .OrderBy(status => status.FirstSeenBelowTarget);

    internal void DismissRecraft(Guid listId, uint itemId)
    {
        if (Config.DismissedKeys.Add(Key(listId, itemId)))
            SaveConfig();
    }
}
