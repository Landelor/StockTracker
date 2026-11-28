using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using StockTracker.Windows.Adt;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace StockTracker.Windows;

public sealed partial class MainWindow
{
    /// <summary>Per-list "add item" form state, so two lists expanded at once don't share one search box.</summary>
    private sealed class AddItemFormState
    {
        public string Search = string.Empty;
        public ItemChoice? Selected;
        public int TargetQuantity = 1;
    }

    private readonly Dictionary<Guid, AddItemFormState> addItemForms = new();

    private AddItemFormState FormFor(Guid listId) =>
        addItemForms.TryGetValue(listId, out var state) ? state : addItemForms[listId] = new AddItemFormState();

    /// <summary>
    /// Every saved list, each in its own collapsible section (click the header to expand/collapse
    /// it) rather than one tab-selector showing a single list at a time - makes it easy to glance
    /// over several lists, or fold away ones you aren't working on right now. ImGui remembers each
    /// section's open/closed state by itself for the rest of the game session.
    /// </summary>
    private void DrawTrackedItemsTab()
    {
        ImGui.Text("Tracked Lists");
        ImGui.TextDisabled("Target stock is the total across all of your retainers combined, not per retainer.");
        AdtStyling.VSpace(8f);

        if (AdtIconButton.Draw(FontAwesomeIcon.Plus, "##st_list_add", 24f, tooltip: "New list"))
        {
            var created = new TrackedList { Name = $"List {plugin.Config.Lists.Count + 1}" };
            plugin.Config.Lists.Add(created);
            plugin.SaveConfig();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("Add a new list");

        AdtStyling.VSpace(8f);
        ImGui.Separator();

        TrackedList? toDelete = null;
        foreach (var list in plugin.Config.Lists.ToList())
        {
            AdtStyling.VSpace(6f);
            ImGui.PushID(list.Id.ToString());

            var headerLabel = $"{list.Name} ({list.Items.Count})###st_list_header_{list.Id}";
            var expanded = ImGui.CollapsingHeader(headerLabel, ImGuiTreeNodeFlags.DefaultOpen);
            if (expanded)
            {
                ImGui.Indent();
                DrawListControls(list, plugin.Config.Lists.Count > 1, () => toDelete = list);
                AdtStyling.VSpace(6f);
                DrawAddItemForm(list);
                AdtStyling.VSpace(10f);
                DrawTrackedItemsTable(list);
                ImGui.Unindent();
            }

            ImGui.PopID();
        }

        if (toDelete is not null)
        {
            plugin.Config.Lists.Remove(toDelete);
            plugin.SaveConfig();
            plugin.RefreshStatuses();
        }
    }

    private void DrawListControls(TrackedList list, bool canDelete, Action requestDelete)
    {
        ImGui.SetNextItemWidth(220f);
        var name = list.Name;
        if (ImGui.InputText("##st_list_rename", ref name, 64) && name.Length > 0)
        {
            list.Name = name;
            plugin.SaveConfig();
        }
        ImGui.SameLine();

        ImGui.BeginDisabled(!canDelete);
        if (AdtIconButton.Draw(FontAwesomeIcon.Trash, "##st_list_delete", 24f, tooltip: canDelete ? "Delete this list" : "At least one list must remain"))
            requestDelete();
        ImGui.EndDisabled();
    }

    private void DrawAddItemForm(TrackedList list)
    {
        var form = FormFor(list.Id);

        ImGui.Text($"Track a new item in \"{list.Name}\"");

        ImGui.SetNextItemWidth(260f);
        if (ImGui.InputTextWithHint("##st_item_search", "Item name...", ref form.Search, 64))
            form.Selected = null;

        if (form.Search.Length >= 2 && form.Selected is null)
        {
            var matches = items
                .Where(item => item.Name.Contains(form.Search, StringComparison.OrdinalIgnoreCase))
                .Take(12)
                .ToList();

            if (matches.Count > 0)
            {
                using var _ = ImRaiiListBox("##st_item_matches");
                foreach (var match in matches)
                {
                    if (ImGui.Selectable(match.Name))
                    {
                        form.Selected = match;
                        form.Search = match.Name;
                    }
                }
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f);
        ImGui.InputInt("Target qty", ref form.TargetQuantity);
        form.TargetQuantity = Math.Max(1, form.TargetQuantity);

        var canAdd = form.Selected is not null;
        ImGui.BeginDisabled(!canAdd);
        if (ImGui.Button("Add"))
        {
            list.Items.Add(new TrackedItem
            {
                ItemId = form.Selected!.Value.Id,
                ItemName = form.Selected.Value.Name,
                TargetQuantity = (uint)form.TargetQuantity,
            });
            plugin.SaveConfig();
            plugin.RefreshStatuses();
            form.Search = string.Empty;
            form.Selected = null;
            form.TargetQuantity = 1;
        }
        ImGui.EndDisabled();
    }

    private void DrawTrackedItemsTable(TrackedList list)
    {
        if (list.Items.Count == 0)
        {
            ImGui.TextDisabled("Nothing tracked yet in this list.");
            return;
        }

        if (!ImGui.BeginTable($"##st_tracked_table_{list.Id}", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
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
