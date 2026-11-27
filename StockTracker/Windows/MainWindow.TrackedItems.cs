using System;
using System.Linq;
using System.Numerics;
using StockTracker.Windows.Adt;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace StockTracker.Windows;

public sealed partial class MainWindow
{
    private string addItemSearch = string.Empty;
    private ItemChoice? addItemSelected;
    private int addTargetQuantity = 1;
    private ulong addRetainerId;

    private void DrawTrackedItemsTab()
    {
        DrawAddItemForm();
        AdtStyling.VSpace(10f);
        ImGui.Separator();
        AdtStyling.VSpace(6f);
        DrawTrackedItemsTable();
    }

    private void DrawAddItemForm()
    {
        ImGui.Text("Track a new item");

        ImGui.SetNextItemWidth(260f);
        if (ImGui.InputTextWithHint("##st_item_search", "Item name...", ref addItemSearch, 64))
            addItemSelected = null;

        if (addItemSearch.Length >= 2 && addItemSelected is null)
        {
            var matches = items
                .Where(item => item.Name.Contains(addItemSearch, StringComparison.OrdinalIgnoreCase))
                .Take(12)
                .ToList();

            if (matches.Count > 0)
            {
                using var _ = ImRaiiListBox("##st_item_matches");
                foreach (var match in matches)
                {
                    if (ImGui.Selectable(match.Name))
                    {
                        addItemSelected = match;
                        addItemSearch = match.Name;
                    }
                }
            }
        }

        var retainers = RetainerService.GetKnownRetainers();
        ImGui.SetNextItemWidth(200f);
        var retainerLabel = retainers.FirstOrDefault(r => r.RetainerId == addRetainerId).Name ?? "Select a retainer...";
        if (ImGui.BeginCombo("##st_retainer_pick", retainerLabel))
        {
            if (retainers.Count == 0)
                ImGui.TextDisabled("No retainers found - visit a retainer bell at least once this session.");

            foreach (var retainer in retainers)
            {
                if (ImGui.Selectable(retainer.Name, retainer.RetainerId == addRetainerId))
                    addRetainerId = retainer.RetainerId;
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f);
        ImGui.InputInt("Target qty", ref addTargetQuantity);
        addTargetQuantity = Math.Max(1, addTargetQuantity);

        var canAdd = addItemSelected is not null && addRetainerId != 0;
        ImGui.BeginDisabled(!canAdd);
        if (ImGui.Button("Add"))
        {
            var retainerName = retainers.FirstOrDefault(r => r.RetainerId == addRetainerId).Name ?? string.Empty;
            plugin.Config.TrackedItems.Add(new TrackedItem
            {
                ItemId = addItemSelected!.Value.Id,
                ItemName = addItemSelected.Value.Name,
                RetainerId = addRetainerId,
                RetainerName = retainerName,
                TargetQuantity = (uint)addTargetQuantity,
            });
            plugin.SaveConfig();
            plugin.RefreshStatuses();
            addItemSearch = string.Empty;
            addItemSelected = null;
            addTargetQuantity = 1;
        }
        ImGui.EndDisabled();
    }

    private void DrawTrackedItemsTable()
    {
        ImGui.Text("Tracked items");
        if (plugin.Config.TrackedItems.Count == 0)
        {
            ImGui.TextDisabled("Nothing tracked yet.");
            return;
        }

        if (!ImGui.BeginTable("##st_tracked_table", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
            return;

        ImGui.TableSetupColumn("Item");
        ImGui.TableSetupColumn("Retainer");
        ImGui.TableSetupColumn("Target");
        ImGui.TableSetupColumn("Current");
        ImGui.TableSetupColumn("");
        ImGui.TableHeadersRow();

        TrackedItem? toRemove = null;
        foreach (var item in plugin.Config.TrackedItems.ToList())
        {
            var status = plugin.Statuses.FirstOrDefault(s => s.Item == item);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(item.ItemName);
            ImGui.TableNextColumn();
            ImGui.Text(item.RetainerName);
            ImGui.TableNextColumn();
            ImGui.Text(item.TargetQuantity.ToString("N0"));
            ImGui.TableNextColumn();
            if (status is not null)
            {
                var color = status.NeedsRecraft ? AdtStyling.AccentAmber : AdtStyling.AccentMint;
                ImGui.TextColored(color, status.CurrentQuantity.ToString("N0"));
            }
            else
            {
                ImGui.TextDisabled("-");
            }
            ImGui.TableNextColumn();
            if (AdtIconButton.Draw(FontAwesomeIcon.Trash, $"##st_remove_{item.ItemId}_{item.RetainerId}", 24f, tooltip: "Stop tracking"))
                toRemove = item;
        }

        ImGui.EndTable();

        if (toRemove is not null)
        {
            plugin.Config.TrackedItems.Remove(toRemove);
            plugin.SaveConfig();
            plugin.RefreshStatuses();
        }
    }

    /// <summary>Minimal IDisposable shim so a plain ImGui.BeginListBox/EndListBox pair reads like the Raii style used elsewhere.</summary>
    private static IDisposable ImRaiiListBox(string id)
    {
        ImGui.BeginListBox(id, new Vector2(ImGui.GetContentRegionAvail().X, 140f));
        return new ListBoxScope();
    }

    private sealed class ListBoxScope : IDisposable
    {
        public void Dispose() => ImGui.EndListBox();
    }
}
