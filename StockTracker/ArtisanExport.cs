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

    /// <summary>Caps recipe-tree recursion so a data error (or a genuine cycle) can't hang the export.</summary>
    private const int MaxDepth = 8;

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

            var crafts = CraftCountFor(recipe, deficit);
            recipes.Add(RecipeEntry(recipe.RowId, crafts));
        }

        return new ExportResult(Serialize(listName, recipes), recipes.Count, skipped);
    }

    /// <summary>
    /// Same as <see cref="Build"/>, but also walks each recipe's own ingredients looking for
    /// ingredients that are themselves craftable (sub-crafts - e.g. a weaver recipe that consumes
    /// a dyed fabric you also craft), and includes those recipes too, with a craft count derived
    /// from how much of that ingredient the parent recipe actually needs. This recurses, so a
    /// sub-craft's own sub-crafts are included as well. The same recipe needed by more than one
    /// parent (or needed both directly and as an ingredient) has its craft counts summed, not
    /// duplicated. Output order lists every ingredient recipe before the recipe(s) that consume
    /// it, so importing into Artisan and running the list top-to-bottom crafts materials first.
    /// </summary>
    public static ExportResult BuildWithSubcrafts(string listName, IEnumerable<(uint ItemId, uint Deficit)> itemDeficits)
    {
        var recipeSheet = Plugin.DataManager.GetExcelSheet<Recipe>();
        var itemToRecipe = BuildItemToRecipeIndex(recipeSheet);

        var totalCrafts = new Dictionary<uint, long>();
        var skipped = 0;
        var requestedTopLevel = 0;

        foreach (var (itemId, deficit) in itemDeficits)
        {
            requestedTopLevel++;
            if (!itemToRecipe.TryGetValue(itemId, out var recipe))
            {
                skipped++;
                continue;
            }

            ExpandCrafts(recipe, CraftCountFor(recipe, deficit), itemToRecipe, totalCrafts, depth: 0);
        }

        var order = TopologicalOrder(totalCrafts, itemToRecipe, recipeSheet);
        var recipes = order
            .Select(recipeId => RecipeEntry(recipeId, (int)Math.Max(1, totalCrafts[recipeId])))
            .ToList<object>();

        return new ExportResult(Serialize(listName, recipes), requestedTopLevel - skipped, skipped);
    }

    private static int CraftCountFor(Recipe recipe, uint itemDeficit)
    {
        var amountResult = Math.Max((uint)1, recipe.AmountResult);
        return Math.Max(1, (int)Math.Ceiling(Math.Max(1u, itemDeficit) / (double)amountResult));
    }

    /// <summary>First recipe that produces each item id, keyed by item id for O(1) ingredient lookups.</summary>
    private static Dictionary<uint, Recipe> BuildItemToRecipeIndex(Lumina.Excel.ExcelSheet<Recipe> recipeSheet)
    {
        var index = new Dictionary<uint, Recipe>();
        foreach (var recipe in recipeSheet)
        {
            var itemId = recipe.ItemResult.RowId;
            if (itemId != 0 && !index.ContainsKey(itemId))
                index[itemId] = recipe;
        }

        return index;
    }

    /// <summary>
    /// Adds <paramref name="craftCount"/> runs of <paramref name="recipe"/> to the running totals,
    /// then recurses into every ingredient slot that is itself a craftable item, converting the
    /// total item quantity that ingredient needs (per-craft amount times how many times this
    /// recipe is being run) into a further craft count via that ingredient recipe's own yield.
    /// </summary>
    private static void ExpandCrafts(Recipe recipe, long craftCount, Dictionary<uint, Recipe> itemToRecipe,
        Dictionary<uint, long> totalCrafts, int depth)
    {
        if (craftCount <= 0 || depth > MaxDepth)
            return;

        totalCrafts[recipe.RowId] = totalCrafts.GetValueOrDefault(recipe.RowId) + craftCount;

        var slots = Math.Min(recipe.Ingredient.Count, recipe.AmountIngredient.Count);
        for (var i = 0; i < slots; i++)
        {
            var ingredientItemId = recipe.Ingredient[i].RowId;
            var perCraft = recipe.AmountIngredient[i];
            if (ingredientItemId == 0 || perCraft == 0)
                continue;

            if (!itemToRecipe.TryGetValue(ingredientItemId, out var subRecipe))
                continue; // not craftable (a gathered/vendor/market-only material) - nothing to add

            var totalNeeded = (long)perCraft * craftCount;
            var subAmountResult = Math.Max((uint)1, subRecipe.AmountResult);
            var subCraftCount = (long)Math.Ceiling(totalNeeded / (double)subAmountResult);
            ExpandCrafts(subRecipe, subCraftCount, itemToRecipe, totalCrafts, depth + 1);
        }
    }

    /// <summary>
    /// Dependency-first ordering (post-order DFS) over whichever recipes ended up in
    /// <paramref name="totalCrafts"/>, so every ingredient recipe is listed before whatever
    /// recipe(s) consume it. Each recipe id appears exactly once even if several parents need it.
    /// </summary>
    private static List<uint> TopologicalOrder(Dictionary<uint, long> totalCrafts, Dictionary<uint, Recipe> itemToRecipe,
        Lumina.Excel.ExcelSheet<Recipe> recipeSheet)
    {
        var order = new List<uint>();
        var visited = new HashSet<uint>();

        void Visit(uint recipeId, int depth)
        {
            if (depth > MaxDepth || !totalCrafts.ContainsKey(recipeId) || !visited.Add(recipeId))
                return;

            var recipe = recipeSheet.GetRowOrDefault(recipeId);
            if (recipe is { } row)
            {
                var slots = Math.Min(row.Ingredient.Count, row.AmountIngredient.Count);
                for (var i = 0; i < slots; i++)
                {
                    var ingredientItemId = row.Ingredient[i].RowId;
                    if (ingredientItemId != 0 && itemToRecipe.TryGetValue(ingredientItemId, out var subRecipe))
                        Visit(subRecipe.RowId, depth + 1);
                }
            }

            order.Add(recipeId);
        }

        foreach (var recipeId in totalCrafts.Keys)
            Visit(recipeId, 0);

        return order;
    }

    private static object RecipeEntry(uint recipeId, int quantity) => new
    {
        ID = recipeId,
        Quantity = Math.Max(1, quantity),
        ListItemOptions = new { NQOnly = false, Skipping = false },
    };

    private static string Serialize(string listName, List<object> recipes)
    {
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

        return JsonSerializer.Serialize(payload);
    }
}
