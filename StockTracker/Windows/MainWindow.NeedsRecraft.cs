using System;
using System.Collections.Generic;
using System.Linq;
using StockTracker.Windows.Adt;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace StockTracker.Windows;

public sealed partial class MainWindow
{
    private string? artisanExportStatus;

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

        DrawArtisanExportButtons(needsRecraft);
        AdtStyling.VSpace(8f);

        if (!ImGui.BeginTable("##st_recraft_table", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
            return;

        ImGui.TableSetupColumn("Item");
        ImGui.TableSetupColumn("List");
        ImGui.TableSetupColumn("Target", ImGuiTableColumnFlags.WidthFixed, 72f);
        ImGui.TableSetupColumn("Current (all retainers)");
        ImGui.TableSetupColumn("");
        ImGui.TableHeadersRow();

        (Guid ListId, uint ItemId)? dismiss = null;
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

    /// <summary>
    /// Copies every currently-visible "needs recraft" item to the clipboard as an Artisan-shaped
    /// crafting list (see ArtisanExport) - paste it straight into Artisan's own "Import List From
    /// Clipboard (Artisan Export)" button. Each item's craft count is its stock deficit
    /// (target - current), converted from item count to recipe-run count by ArtisanExport. The
    /// second button does the same thing but also walks each recipe's ingredients for any that
    /// are themselves craftable, adding those sub-craft recipes (and their own sub-crafts) too,
    /// in dependency order, so the imported list can be run top-to-bottom from base materials up.
    /// </summary>
    private void DrawArtisanExportButtons(List<StockStatus> needsRecraft)
    {
        IEnumerable<(uint ItemId, uint Deficit)> Deficits() => needsRecraft.Select(status => (
            status.Item.ItemId,
            Deficit: status.Item.TargetQuantity > status.CurrentQuantity
                ? status.Item.TargetQuantity - status.CurrentQuantity
                : 1u));

        if (ImGui.Button("Export to Artisan (Copy to Clipboard)"))
            CopyArtisanExport(ArtisanExport.Build($"StockTracker Recraft {DateTime.Now:yyyy-MM-dd HH:mm}", Deficits()));

        ImGui.SameLine();
        if (ImGui.Button("Export to Artisan with Subcrafts (Copy to Clipboard)"))
            CopyArtisanExport(ArtisanExport.BuildWithSubcrafts($"StockTracker Recraft (Subcrafts) {DateTime.Now:yyyy-MM-dd HH:mm}", Deficits()));

        if (artisanExportStatus is not null)
        {
            AdtStyling.VSpace(4f);
            ImGui.TextColored(AdtStyling.AccentMint, artisanExportStatus);
        }
    }

    private void CopyArtisanExport(ArtisanExport.ExportResult result)
    {
        ImGui.SetClipboardText(result.Json);
        artisanExportStatus = result.SkippedCount > 0
            ? $"Copied {result.ExportedCount} item(s) \u2014 {result.SkippedCount} skipped (no known recipe). Paste into Artisan's \"Import List From Clipboard\" button."
            : $"Copied {result.ExportedCount} item(s) to the clipboard. Paste into Artisan's \"Import List From Clipboard (Artisan Export)\" button.";
    }
}
