using Artisan.CraftingLists;
using Artisan.RawInformation;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class IngredientHelpers
{
    // Progress over both passes (build each ingredient, then check its inventory/retainer counts), so the bar fills once.
    public int CurrentIngredient = 0;
    public int MaxIngredient = 0;

    public List<Ingredient> HelperList = new List<Ingredient>();

    public Dictionary<uint, Ingredient> HelperLookup = new();

    // Runs synchronously; callers are expected to invoke this from a background task.
    public List<Ingredient>? GenerateList(NewCraftingList originList, CancellationToken token)
    {
        var timer = Stopwatch.StartNew();
        CurrentIngredient = 0;
        MaxIngredient = 0;
        var materials = originList.ListMaterials();
        var materialsMs = timer.ElapsedMilliseconds;

        List<Ingredient> output = new();
        MaxIngredient = materials.Count * 2;
        foreach (var item in materials.OrderBy(x => x.Key))
        {
            if (token.IsCancellationRequested) return null;
            CurrentIngredient++;
            output.Add(new Ingredient(item.Key, item.Value, originList, materials, this));
        }
        var buildMs = timer.ElapsedMilliseconds - materialsMs;

        HelperLookup = output.ToDictionary(x => x.Data.RowId);
        HelperList = output;

        if (P.Config.UseUniversalis && !P.Config.UniversalisOnDemand)
            Task.Run(() => FetchMarketData(output, token));

        // Compute what the table needs on its first frame here, off the UI thread. The first lookup of each item's
        // retainer count goes through Allagan Tools IPC (one call per retainer inventory page) before it is cached,
        // which froze the game for seconds on big lists when the first table frame did it.
        foreach (var ingredient in output)
        {
            if (token.IsCancellationRequested) return null;
            CurrentIngredient++;
            ingredient.AmountUsedForSubcrafts = ingredient.GetSubCraftCount();
            _ = ingredient.Remaining;
        }
        var warmMs = timer.ElapsedMilliseconds - materialsMs - buildMs;

        Svc.Log.Debug($"[IngredientTable] {originList.Name}: {output.Count} ingredients in {timer.ElapsedMilliseconds} ms (materials {materialsMs} ms, build {buildMs} ms, inventory/retainers {warmMs} ms)");
        return output;
    }

    // One batched Universalis lookup for the whole table; a request per item trips the API's rate limit on big lists.
    private static void FetchMarketData(List<Ingredient> ingredients, CancellationToken token)
    {
        var timer = Stopwatch.StartNew();
        var results = P.UniversalsisClient.GetBulkData(ingredients.Select(x => (ulong)x.Data.RowId), P.Config.LimitUnversalisToDC, token);
        foreach (var ingredient in ingredients)
        {
            if (results.TryGetValue(ingredient.Data.RowId, out var data))
                ingredient.MarketboardData = data;
        }

        Svc.Log.Debug($"[IngredientTable] Universalis: prices for {results.Count}/{ingredients.Count} items in {timer.ElapsedMilliseconds} ms");
    }
}
