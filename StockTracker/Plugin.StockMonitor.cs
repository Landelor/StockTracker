using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;

namespace StockTracker;

public sealed partial class Plugin
{
    /// <summary>How often the retainer stock levels are recomputed against Allagan Tools' cache.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    private DateTime lastPoll = DateTime.MinValue;

    /// <summary>
    /// First-seen-below-target timestamp per item id, purely in-memory (resets on plugin reload).
    /// Used only to sort/age the "needs recraft" list - the underlying truth (is it below target
    /// right now) is always recomputed live from Allagan Tools, never trusted from this cache.
    /// </summary>
    private readonly Dictionary<uint, DateTimeOffset> firstSeenBelowTarget = [];

    internal List<StockStatus> Statuses { get; private set; } = [];

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (DateTime.UtcNow - lastPoll < PollInterval)
            return;
        lastPoll = DateTime.UtcNow;

        RefreshStatuses();
    }

    /// <summary>
    /// Recomputes every tracked item's current retainer-market quantity and whether it is below
    /// its target. An item automatically drops off the "needs recraft" list the moment Allagan
    /// Tools reports it back at or above target (i.e. the player re-listed enough), and any
    /// manual dismissal for that item is cleared at the same time so a future sale re-flags it.
    /// </summary>
    internal void RefreshStatuses()
    {
        var now = DateTimeOffset.UtcNow;
        var statuses = new List<StockStatus>(Config.TrackedItems.Count);

        foreach (var item in Config.TrackedItems)
        {
            if (!item.Enabled)
                continue;

            var quantities = AllaganTools.GetRetainerMarketQuantities(item.ItemId);
            var current = quantities.GetValueOrDefault(item.RetainerId, 0u);
            var below = AllaganTools.IsAvailable && current < item.TargetQuantity;

            if (below)
            {
                if (!firstSeenBelowTarget.ContainsKey(item.ItemId))
                    firstSeenBelowTarget[item.ItemId] = now;
            }
            else
            {
                firstSeenBelowTarget.Remove(item.ItemId);
                if (Config.DismissedItemIds.Remove(item.ItemId))
                    SaveConfig();
            }

            statuses.Add(new StockStatus(item, current, below, firstSeenBelowTarget.GetValueOrDefault(item.ItemId)));
        }

        Statuses = statuses;
    }

    /// <summary>Items below target and not manually dismissed - what the "Needs Recraft" tab shows.</summary>
    internal IEnumerable<StockStatus> NeedsRecraft => Statuses
        .Where(status => status.NeedsRecraft && !Config.DismissedItemIds.Contains(status.Item.ItemId))
        .OrderBy(status => status.FirstSeenBelowTarget);

    internal void DismissRecraft(uint itemId)
    {
        if (Config.DismissedItemIds.Add(itemId))
            SaveConfig();
    }
}
