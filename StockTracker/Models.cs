using System;
using System.Collections.Generic;

namespace StockTracker;

/// <summary>
/// One item the player crafts and sells, with the total stock they want kept listed across all
/// of their retainers combined (not tied to a single retainer - a crafted item is commonly spread
/// across whichever retainer currently has room). StockTracker compares this target against the
/// item's live total retainer-market quantity (summed across retainers by Allagan Tools) to
/// decide whether the item needs recrafting.
/// </summary>
public sealed class TrackedItem
{
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public uint TargetQuantity { get; set; } = 1;
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// A named collection of tracked items - e.g. separate lists per crafting job, per market
/// category, or whatever grouping the player finds useful. Lists are independent: the same item
/// id can appear in more than one list with a different target in each.
/// </summary>
public sealed class TrackedList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New List";
    public List<TrackedItem> Items { get; set; } = [];
}

/// <summary>
/// A snapshot of one tracked item's current state against its target, recomputed every poll.
/// Not persisted - rebuilt live from <see cref="TrackedItem"/> + Allagan Tools counts, so there is
/// nothing to desync between restarts.
/// </summary>
public sealed record StockStatus(
    TrackedList List,
    TrackedItem Item,
    uint CurrentQuantity,
    bool NeedsRecraft,
    DateTimeOffset? FirstSeenBelowTarget);
