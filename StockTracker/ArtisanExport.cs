using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Lumina.Excel.Sheets;

namespace StockTracker;

/// <summary>
/// Builds a JSON payload shaped exactly like Artisan's own <c>NewCraftingList</c>/<c>ListItem</c>/
/// <c>ListItemOptions</c> classes (PunishXIV/Artisan, <c>CraftingList/CraftingList.cs</c>), so it
/// can be pasted straight into Artisan's own "Import List From Clipboard (Artisan Export)" button
/// - that button just JSON-deserializes whatever is on the clipboard directly into a
/// <c>NewCraftingList</c>. Property names/casing below are copied verbatim from that class, not
/// guessed: Artisan's import has no tolerance for a differently-shaped or differently-cased
/// payload.
///
/// Artisan's <c>ListItem.ID</c> is a RECIPE id, not an item id, and its <c>Quantity</c> is a craft
/// COUNT (how many times to run the recipe), not a target item count - <c>StartList()</c> expands
/// it via <c>Enumerable.Repeat(r.ID, r.Quantity)</c>. So each tracked item's stock deficit is
/// converted here: look up the recipe that produces that item, then divide the deficit by the
/// recipe's own yield (<c>AmountResult</c>, usually 1 but not always) and round up.
/// </summary>
public static class ArtisanExport
{
    public sealed record ExportResult(string Json, int ExportedCount, int SkippedCount);

    public static ExportResult Build(string listName, IEnumerable<(uint ItemId, uint Deficit)> itemDeficits)
    {
        var recipeSheet = Plugin.DataManager.GetExcelSheet<Recipe>();
        var recipes = new List<object>();
        var skipped = 0;

        foreach (var (itemId, deficit) in itemDeficits)
        {
            var recipe = recipeSheet.FirstOrDefault(row => row.ItemResult.RowId == itemId);
            if (recipe.RowId == 0)
            {
                skipped++;
                continue;
            }

            var amountResult = Math.Max((uint)1, recipe.AmountResult);
            var crafts = (int)Math.Ceiling(Math.Max(1u, deficit) / (double)amountResult);
            recipes.Add(new
            {
                ID = recipe.RowId,
                Quantity = Math.Max(1, crafts),
                ListItemOptions = new { NQOnly = false, Skipping = false },
            });
        }

        var payload = new
        {
            ID = 0,
            Name = listName,
            Recipes = recipes,
            ExpandedList = Array.Empty<uint>(),
            SkipIfEnough = false,
            Materia = false,
            Repair = false,
            RepairPercent = 50,
            AddAsQuickSynth = false,
            TidyAfter = true,
            OnlyRestockNonCrafted = false,
            IsPremade = false,
        };

        return new ExportResult(JsonSerializer.Serialize(payload), recipes.Count, skipped);
    }
}
