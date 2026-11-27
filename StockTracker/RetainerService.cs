using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace StockTracker;

/// <summary>
/// Thin wrapper around FFXIVClientStructs' RetainerManager - reads the active character's own
/// retainer roster (id, name) directly from game memory. This never needs Allagan Tools or any
/// other plugin: RetainerManager is populated by the client itself once you've interacted with at
/// least one retainer bell this session, the same precondition every retainer-aware plugin has.
/// </summary>
public static unsafe class RetainerService
{
    public readonly record struct RetainerInfo(ulong RetainerId, string Name);

    public static List<RetainerInfo> GetKnownRetainers()
    {
        var result = new List<RetainerInfo>();
        var manager = RetainerManager.Instance();
        if (manager is null || !manager->IsReady)
            return result;

        var count = manager->GetRetainerCount();
        for (byte i = 0; i < count; i++)
        {
            var retainer = manager->GetRetainerBySortedIndex(i);
            if (retainer is null || retainer->RetainerId == 0)
                continue;

            result.Add(new RetainerInfo(retainer->RetainerId, retainer->NameString));
        }

        return result;
    }

    public static string? GetRetainerName(ulong retainerId)
    {
        foreach (var retainer in GetKnownRetainers())
        {
            if (retainer.RetainerId == retainerId)
                return retainer.Name;
        }

        return null;
    }
}
