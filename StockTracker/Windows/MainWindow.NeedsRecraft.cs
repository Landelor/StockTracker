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
        ImGui.TextDisabled("Tracked items whose total retainer stock has sold below their target quantity, across every list.");
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
        ImGui.TableSetupColumn("List");
        ImGui.TableSetupColumn("Target");
        ImGui.TableSetupColumn("Current (all retainers)");
        ImGui.TableSetupColumn("");
        ImGui.TableHeadersRow();

        (System.Guid ListId, uint ItemId)? dismiss = null;
        foreach (var status in needsRecraft)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(status.Item.ItemName);
            ImGui.TableNextColumn();
            ImGui.Text(status.List.Name);
            ImGui.TableNextColumn();
            ImGui.Text(status.Item.TargetQuantity.ToString("N0"));
            ImGui.TableNextColumn();
            ImGui.TextColored(AdtStyling.AccentAmber, status.CurrentQuantity.ToString("N0"));
            ImGui.TableNextColumn();
            if (AdtIconButton.Draw(FontAwesomeIcon.Check, $"##st_dismiss_{status.List.Id}_{status.Item.ItemId}", 24f,
                    tooltip: "Dismiss until it sells again (doesn't change target stock)"))
                dismiss = (status.List.Id, status.Item.ItemId);
        }

        ImGui.EndTable();

        if (dismiss is { } key)
            plugin.DismissRecraft(key.ListId, key.ItemId);
    }
}
