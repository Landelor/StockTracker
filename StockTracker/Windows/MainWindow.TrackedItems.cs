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
    private string newListName = string.Empty;

    private TrackedList CurrentList()
    {
        var list = plugin.Config.Lists.FirstOrDefault(l => l.Id == selectedListId)
                   ?? plugin.Config.Lists.FirstOrDefault();
        if (list is null)
        {
            list = new TrackedList { Name = "Default" };
            plugin.Config.Lists.Add(list);
            plugin.SaveConfig();
        }

        selectedListId = list.Id;
        return list;
    }

    private void DrawTrackedItemsTab()
    {
        var list = DrawListSelector();
        AdtStyling.VSpace(8f);
        ImGui.Separator();
        AdtStyling.VSpace(6f);

        DrawAddItemForm(list);
        AdtStyling.VSpace(10f);
        ImGui.Separator();
        AdtStyling.VSpace(6f);
        DrawTrackedItemsTable(list);
    }

    /// <summary>
    /// Tabs across the top for every saved list, plus a "+" to create a new one and a trash icon
    /// on the active tab to delete it (always leaves at least one list behind).
    /// </summary>
    private TrackedList DrawListSelector()
    {
        var current = CurrentList();

        foreach (var list in plugin.Config.Lists)
        {
            var active = list.Id == current.Id;
            if (active)
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(AdtStyling.WithAlpha(AdtStyling.AccentTeal, 0.35f)));
            if (ImGui.Button($"{list.Name}##st_list_{list.Id}"))
                selectedListId = list.Id;
            if (active)
                ImGui.PopStyleColor();
            ImGui.SameLine();
        }

        if (AdtIconButton.Draw(FontAwesomeIcon.Plus, "##st_list_add", 24f, tooltip: "New list"))
        {
            var created = new TrackedList { Name = $"List {plugin.Config.Lists.Count + 1}" };
            plugin.Config.Lists.Add(created);
            selectedListId = created.Id;
            plugin.SaveConfig();
        }
        ImGui.SameLine();

        ImGui.SetNextItemWidth(160f);
        var name = current.Name;
        if (ImGui.InputText("##st_list_rename", ref name, 64) && name.Length > 0)
        {
            current.Name = name;
            plugin.SaveConfig();
        }
        ImGui.SameLine();

        var canDelete = plugin.Config.Lists.Count > 1;
        ImGui.BeginDisabled(!canDelete);
        if (AdtIconButton.Draw(FontAwesomeIcon.Trash, "##st_list_delete", 24f, tooltip: canDelete ? "Delete this list" : "At least one list must remain"))
        {
            plugin.Config.Lists.Remove(current);
            selectedListId = plugin.Config.Lists[0].Id;
            plugin.SaveConfig();
            plugin.RefreshStatuses();
        }
        ImGui.EndDisabled();

        return CurrentList();
    }

    private void DrawAddItemForm(TrackedList list)
    {
        ImGui.Text($"Track a new item in \"{list.Name}\"");

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

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f);
        ImGui.InputInt("Target qty", ref addTargetQuantity);
        addTargetQuantity = Math.Max(1, addTargetQuantity);

        var canAdd = addItemSelected is not null;
        ImGui.BeginDisabled(!canAdd);
        if (ImGui.Button("Add"))
        {
            list.Items.Add(new TrackedItem
            {
                ItemId = addItemSelected!.Value.Id,
                ItemName = addItemSelected.Value.Name,
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

    private void DrawTrackedItemsTable(TrackedList list)
    {
        ImGui.Text("Tracked items");
        ImGui.TextDisabled("Target stock is the total across all of your retainers combined, not per retainer.");
        if (list.Items.Count == 0)
        {
            ImGui.TextDisabled("Nothing tracked yet in this list.");
            return;
        }

        if (!ImGui.BeginTable("##st_tracked_table", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
            return;

        ImGui.TableSetupColumn("Item");
        ImGui.TableSetupColumn("Target", ImGuiTableColumnFlags.WidthFixed, 96f);
        ImGui.TableSetupColumn("Current (all retainers)");
        ImGui.TableSetupColumn("");
        ImGui.TableHeadersRow();

        TrackedItem? toRemove = null;
        foreach (var item in list.Items.ToList())
        {
            var status = plugin.Statuses.FirstOrDefault(s => s.List.Id == list.Id && s.Item == item);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(item.ItemName);
            ImGui.TableNextColumn();
            DrawTargetStepper(list, item);
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
            if (AdtIconButton.Draw(FontAwesomeIcon.Trash, $"##st_remove_{list.Id}_{item.ItemId}", 24f, tooltip: "Stop tracking"))
                toRemove = item;
        }

        ImGui.EndTable();

        if (toRemove is not null)
        {
            list.Items.Remove(toRemove);
            plugin.SaveConfig();
            plugin.RefreshStatuses();
        }
    }

    /// <summary>Minus/text/plus stepper for a tracked item's target quantity - never below 1.</summary>
    private void DrawTargetStepper(TrackedList list, TrackedItem item)
    {
        var scale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
        var buttonSize = 20f * scale;

        ImGui.BeginDisabled(item.TargetQuantity <= 1);
        if (AdtIconButton.Draw(FontAwesomeIcon.Minus, $"##st_target_dec_{list.Id}_{item.ItemId}", buttonSize, tooltip: "Decrease target"))
            SetTargetQuantity(item, item.TargetQuantity - 1);
        ImGui.EndDisabled();

        ImGui.SameLine(0f, 6f * scale);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(item.TargetQuantity.ToString("N0"));

        ImGui.SameLine(0f, 6f * scale);
        if (AdtIconButton.Draw(FontAwesomeIcon.Plus, $"##st_target_inc_{list.Id}_{item.ItemId}", buttonSize, tooltip: "Increase target"))
            SetTargetQuantity(item, item.TargetQuantity + 1);
    }

    private void SetTargetQuantity(TrackedItem item, uint quantity)
    {
        item.TargetQuantity = Math.Max(1u, quantity);
        plugin.SaveConfig();
        plugin.RefreshStatuses();
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
