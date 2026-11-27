using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Gui.ContextMenu;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace StockTracker;

/// <summary>
/// Adds a "StockTracker" submenu to the Crafting Log's (RecipeNote addon) right-click context
/// menu, letting the player add the currently selected recipe's result item straight to one of
/// their tracked lists without leaving the crafting window.
///
/// The RecipeNote context menu does not go through Dalamud's typed <see cref="MenuTargetDefault"/>
/// (that type only carries character-target fields - name, object id, home world - there is no
/// item id on it at all). Instead, the item being right-clicked is read directly off the
/// RecipeNote agent's own <c>ContextMenuResultItemId</c> field (offset 0x398), confirmed against
/// both the real FFXIVClientStructs source (aers/FFXIVClientStructs, AgentRecipeNote.cs) and
/// PunishXIV/Artisan's own shipped ContextMenus/CraftingListContextMenu.cs, which reads the exact
/// same offset off the same agent for the same addon to implement its own "add to crafting list"
/// context menu entry - real-world confirmation this is the right field for this exact UI.
/// </summary>
public sealed unsafe class ContextMenuService : IDisposable
{
    private readonly Plugin plugin;

    public ContextMenuService(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.ContextMenu.OnMenuOpened += OnMenuOpened;
    }

    public void Dispose()
    {
        Plugin.ContextMenu.OnMenuOpened -= OnMenuOpened;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (!plugin.Config.ContextMenuEnabled)
            return;

        if (args.AddonName != "RecipeNote")
            return;

        var agent = AgentRecipeNote.Instance();
        if (agent is null)
            return;

        var itemId = agent->ContextMenuResultItemId;
        if (itemId == 0)
            return;

        var itemName = Plugin.DataManager.GetExcelSheet<Item>().GetRowOrDefault(itemId)?.Name.ToString();
        if (string.IsNullOrWhiteSpace(itemName))
            return;

        var submenu = new MenuItem
        {
            Name = "StockTracker",
            PrefixChar = 'S',
            IsSubmenu = true,
        };
        submenu.OnClicked += clickArgs => clickArgs.OpenSubmenu(BuildSubmenu(itemId, itemName));

        args.AddMenuItem(submenu);
    }

    private List<MenuItem> BuildSubmenu(uint itemId, string itemName)
    {
        var menuItems = new List<MenuItem>();

        foreach (var list in plugin.Config.Lists)
        {
            var alreadyTracked = list.Items.Any(item => item.ItemId == itemId);
            var entry = new MenuItem
            {
                Name = alreadyTracked ? $"Already in \"{list.Name}\"" : $"Add to \"{list.Name}\"",
                PrefixChar = 'S',
                IsEnabled = !alreadyTracked,
            };
            var capturedList = list;
            entry.OnClicked += _ => AddToList(capturedList, itemId, itemName);
            menuItems.Add(entry);
        }

        var newListEntry = new MenuItem
        {
            Name = "Add to New List",
            PrefixChar = 'S',
        };
        newListEntry.OnClicked += _ => AddToNewList(itemId, itemName);
        menuItems.Add(newListEntry);

        return menuItems;
    }

    private void AddToList(TrackedList list, uint itemId, string itemName)
    {
        if (list.Items.Any(item => item.ItemId == itemId))
            return;

        list.Items.Add(new TrackedItem { ItemId = itemId, ItemName = itemName, TargetQuantity = 1 });
        plugin.SaveConfig();
        plugin.RefreshStatuses();
        Plugin.Log.Information($"StockTracker: added {itemName} to list \"{list.Name}\" from the Crafting Log context menu");
    }

    private void AddToNewList(uint itemId, string itemName)
    {
        var list = new TrackedList { Name = itemName };
        list.Items.Add(new TrackedItem { ItemId = itemId, ItemName = itemName, TargetQuantity = 1 });
        plugin.Config.Lists.Add(list);
        plugin.SaveConfig();
        plugin.RefreshStatuses();
        Plugin.Log.Information($"StockTracker: created list \"{list.Name}\" with {itemName} from the Crafting Log context menu");
    }
}
