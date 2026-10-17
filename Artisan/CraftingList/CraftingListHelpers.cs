using Artisan.CraftingLists;
using Artisan.RawInformation;
using ECommons;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

internal static class CraftingListHelpers
{
    // Written by background ingredient-table builds and the UI thread at the same time.
    internal static ConcurrentDictionary<uint, bool> SelectedRecipesCraftable = new();

    public static void AddRecipeIngredientsToList(Recipe? recipe, ref Dictionary<uint, int> ingredientList, bool addSublist = true, NewCraftingList? selectedList = null)
    {
        try
        {
            if (recipe == null) return;

            if (selectedList != null)
            {
                var quantity = selectedList.Recipes.First(x => x.ID == recipe.Value.RowId).Quantity;
                foreach (var ing in recipe.Value.Ingredients().Where(x => x.Amount > 0 && x.Item.RowId != 0))
                {
                    if (ingredientList.ContainsKey(ing.Item.RowId))
                    {
                        ingredientList[ing.Item.RowId] += ing.Amount * quantity;
                    }
                    else
                    {
                        ingredientList.TryAdd(ing.Item.RowId, ing.Amount * quantity);
                    }

                    var name = LuminaSheets.ItemSheet[ing.Item.RowId].Name.ToString();
                    SelectedRecipesCraftable[ing.Item.RowId] = LuminaSheets.recipeLookup!.Contains(name);

                    if (addSublist && GetIngredientRecipe(ing.Item.RowId) is { } subRecipe)
                    {
                        AddRecipeIngredientsToList(subRecipe, ref ingredientList);
                    }

                }
            }
            else
            {
                foreach (var ing in recipe.Value.Ingredients().Where(x => x.Amount > 0 && x.Item.RowId != 0))
                {
                    if (ingredientList.ContainsKey(ing.Item.RowId))
                    {
                        ingredientList[ing.Item.RowId] += ing.Amount;
                    }
                    else
                    {
                        ingredientList.TryAdd(ing.Item.RowId, ing.Amount);
                    }

                    var name = LuminaSheets.ItemSheet[ing.Item.RowId].Name.ToString();
                    SelectedRecipesCraftable[ing.Item.RowId] = LuminaSheets.recipeLookup!.Contains(name);

                    if (addSublist && GetIngredientRecipe(ing.Item.RowId) is { } subRecipe)
                    {
                        AddRecipeIngredientsToList(subRecipe, ref ingredientList);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "ERROR");
        }
    }

    public static Recipe? GetIngredientRecipe(uint ingredient, int craftType = -1)
    {
        var recipes = LuminaSheets.RecipesByResultItem![ingredient];
        if (craftType != -1 && recipes.TryGetFirst(x => x.CraftType.RowId == craftType, out var byType))
            return byType;
        else
            return recipes.FirstOrNull(x => true);
    }

    public static void TidyUpList(NewCraftingList list)
    {
        var tempMaterialList = new Dictionary<uint, int>();
        foreach (var recipe in list.Recipes)
        {
            Recipe r = LuminaSheets.RecipeSheet[recipe.ID];
            AddRecipeIngredientsToList(r, ref tempMaterialList, false, list);
        }

        foreach (var requiredItem in tempMaterialList)
        {
            if (SelectedRecipesCraftable[requiredItem.Key])
            {
                var recipe = GetIngredientRecipe(requiredItem.Key);
                if (list.Recipes.Any(x => x.ID == recipe?.RowId))
                {
                    double? amountRes = recipe?.AmountResult;
                    var crafting = list.Recipes.First(x => x.ID == recipe?.RowId).Quantity * amountRes;

                    if (crafting > requiredItem.Value)
                    {
                        double quant = Math.Ceiling(requiredItem.Value / amountRes ?? 0);
                        list.Recipes.First(x => x.ID == recipe?.RowId).Quantity = (int)quant;
                    }
                }

            }
        }

    }
}