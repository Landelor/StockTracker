using System;

namespace StockTracker;

/// <summary>
/// One item the player crafts and sells through a specific retainer, with the stock level they
/// want kept listed. StockTracker compares this target against the retainer's live market
/// listing quantity (reported by Allagan Tools) to decide whether the item needs recrafting.
/// </summary>
public sealed class TrackedItem
{
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public ulong RetainerId { get; set; }
    public string RetainerName { get; set; } = string.Empty;
    public uint TargetQuantity { get; set; } = 1;
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// A snapshot of one tracked item's current state against its target, recomputed every poll.
/// Not persisted - rebuilt live from <see cref="TrackedItem"/> + Allagan Tools counts, so there is
/// nothing to desync between restarts.
/// </summary>
public sealed record StockStatus(
    TrackedItem Item,
    uint CurrentQuantity,
    bool NeedsRecraft,
    DateTimeOffset? FirstSeenBelowTarget);
