using System.Linq;
using StockTracker.Windows.Adt;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace StockTracker.Windows;

public sealed partial class MainWindow
{
    private void DrawNeedsRecraftTab()
    {
        ImGui.Text("Needs Recraft");
        ImGui.TextDisabled("Tracked items whose retainer stock has sold below its target quantity.");
        AdtStyling.VSpace(8f);

        if (!plugin.AllaganTools.IsAvailable)
        {
            ImGui.TextWrapped("Allagan Tools is not loaded, so stock levels can't be checked. See the Dashboard tab.");
            return;
        }

        var needsRecraft = plugin.NeedsRecraft.ToList();
        if (needsRecraft.Count == 0)
        {
            ImGui.TextWrapped("Nothing needs recrafting right now.");
            return;
        }

        if (!ImGui.BeginTable("##st_recraft_table", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
            return;

        ImGui.TableSetupColumn("Item");
        ImGui.TableSetupColumn("Retainer");
        ImGui.TableSetupColumn("Target");
        ImGui.TableSetupColumn("Current");
        ImGui.TableSetupColumn("");
        ImGui.TableHeadersRow();

        uint? dismiss = null;
        foreach (var status in needsRecraft)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(status.Item.ItemName);
            ImGui.TableNextColumn();
            ImGui.Text(status.Item.RetainerName);
            ImGui.TableNextColumn();
            ImGui.Text(status.Item.TargetQuantity.ToString("N0"));
            ImGui.TableNextColumn();
            ImGui.TextColored(AdtStyling.AccentAmber, status.CurrentQuantity.ToString("N0"));
            ImGui.TableNextColumn();
            if (AdtIconButton.Draw(FontAwesomeIcon.Check, $"##st_dismiss_{status.Item.ItemId}_{status.Item.RetainerId}", 24f,
                    tooltip: "Dismiss until it sells again (doesn't change target stock)"))
                dismiss = status.Item.ItemId;
        }

        ImGui.EndTable();

        if (dismiss is { } itemId)
            plugin.DismissRecraft(itemId);
    }
}
