using Artisan.CraftingLists;
using Artisan.RawInformation;
using ECommons.DalamudServices;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using System;
using System.Collections.Generic;
using System.Linq;

internal static class CraftingListHelpers
{    
    internal static Dictionary<uint, bool> SelectedRecipesCraftable = new();

    public static void AddRecipeIngredientsToList(Recipe? recipe, ref Dictionary<uint, int> ingredientList, bool addSublist = true, NewCraftingList? selectedList = null)
    {
        try
        {
            if (recipe == null) return;

            if (selectedList != null)
            {
                foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0 && x.ItemIngredient != 0))
                {
                    if (ingredientList.ContainsKey((uint)ing.ItemIngredient))
                    {
                        ingredientList[(uint)ing.ItemIngredient] += ing.AmountIngredient * selectedList.Recipes.First(x => x.ID == recipe.RowId).Quantity;
                    }
                    else
                    {
                        ingredientList.TryAdd((uint)ing.ItemIngredient, ing.AmountIngredient * selectedList.Recipes.First(x => x.ID == recipe.RowId).Quantity);
                    }

                    var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                    SelectedRecipesCraftable[(uint)ing.ItemIngredient] = LuminaSheets.RecipeSheet!.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                    if (GetIngredientRecipe((uint)ing.ItemIngredient) != null && addSublist)
                    {
                        AddRecipeIngredientsToList(GetIngredientRecipe((uint)ing.ItemIngredient), ref ingredientList);
                    }

                }
            }
            else
            {
                foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0 && x.ItemIngredient != 0))
                {
                    if (ingredientList.ContainsKey((uint)ing.ItemIngredient))
                    {
                        ingredientList[(uint)ing.ItemIngredient] += ing.AmountIngredient;
                    }
                    else
                    {
                        ingredientList.TryAdd((uint)ing.ItemIngredient, ing.AmountIngredient);
                    }

                    var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                    SelectedRecipesCraftable[(uint)ing.ItemIngredient] = LuminaSheets.RecipeSheet!.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                    if (GetIngredientRecipe((uint)ing.ItemIngredient) != null && addSublist)
                    {
                        AddRecipeIngredientsToList(GetIngredientRecipe((uint)ing.ItemIngredient), ref ingredientList);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "ERROR");
        }
    }

    public static Recipe? GetIngredientRecipe(uint ingredient)
    {
        if (LuminaSheets.RecipeSheet.Values.FindFirst(x => x.ItemResult.Value.RowId == ingredient, out var result))
            return result;

        return null;
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
                if (list.Recipes.Any(x => x.ID == recipe.RowId))
                {
                    var crafting = list.Recipes.First(x => x.ID == recipe.RowId).Quantity * recipe.AmountResult;
                    
                    if (crafting > requiredItem.Value)
                    {
                        double quant = Math.Ceiling((double)requiredItem.Value / recipe.AmountResult);
                        list.Recipes.First(x => x.ID == recipe.RowId).Quantity = (int)quant;
                    }
                }

            }
        }
    }
}