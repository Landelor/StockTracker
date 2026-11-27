using System.Linq;
using StockTracker.Windows.Adt;
using Dalamud.Bindings.ImGui;

namespace StockTracker.Windows;

public sealed partial class MainWindow
{
    private void DrawDashboardTab()
    {
        ImGui.Text("Dependencies");
        DrawPluginStatus("Allagan Tools", Plugin.GetPluginState("InventoryTools"), required: true);
        if (!plugin.AllaganTools.IsAvailable)
        {
            ImGui.TextWrapped(
                "Allagan Tools (InventoryTools) is required: StockTracker reads each item's total " +
                "retainer market listing quantity (summed across all your retainers) from it to know " +
                "whether it has sold below target. Install or enable it, then return here - StockTracker " +
                "detects it automatically.");
        }
        ImGui.Separator();
        AdtStyling.VSpace(8f);

        var lists = plugin.Config.Lists.Count;
        var tracked = plugin.Config.Lists.Sum(list => list.Items.Count);
        var needsRecraft = plugin.NeedsRecraft.Count();

        const float tileWidth = 150f;
        AdtStatTile.Draw("Lists", lists.ToString("N0"), "saved", AdtStyling.AccentSlate, tileWidth);
        ImGui.SameLine();
        AdtStatTile.Draw("Tracked Items", tracked.ToString("N0"), "configured", AdtStyling.AccentTeal, tileWidth);
        ImGui.SameLine();
        AdtStatTile.Draw("Needs Recraft", needsRecraft.ToString("N0"), "below target",
            needsRecraft > 0 ? AdtStyling.AccentAmber : AdtStyling.AccentMint, tileWidth);

        AdtStyling.VSpace(12f);
        if (needsRecraft > 0)
        {
            ImGui.TextWrapped($"{needsRecraft} tracked item(s) have sold below their target stock and are waiting to be recrafted. See the Needs Recraft tab.");
        }
        else if (tracked == 0)
        {
            ImGui.TextWrapped("Add items you craft and sell on the Tracked Items tab to start monitoring them.");
        }
        else
        {
            ImGui.TextWrapped("Every tracked item currently meets its target stock across your retainers.");
        }
    }
}
