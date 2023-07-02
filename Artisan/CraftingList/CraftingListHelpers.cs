using Artisan.CraftingLists;
using Artisan.RawInformation;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using System;
using System.Collections.Generic;
using System.Linq;
using PluginLog = Dalamud.Logging.PluginLog;

internal static class CraftingListHelpers
{
    public static Dictionary<uint, Recipe> FilteredList = LuminaSheets.RecipeSheet.Values
                .Where(x => x.ItemResult.Row > 0)
                .DistinctBy(x => x.RowId)
                .OrderBy(x => x.RecipeLevelTable.Value.ClassJobLevel)
                .ThenBy(x => x.ItemResult.Value.Name.RawString)
                .ToDictionary(x => x.RowId, x => x);
    
    internal static Dictionary<uint, bool> SelectedRecipesCraftable = new();

    public static void AddRecipeIngredientsToList(Recipe? recipe, ref Dictionary<uint, int> ingredientList, bool addSublist = true, CraftingList selectedList = null)
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
                        ingredientList[(uint)ing.ItemIngredient] += ing.AmountIngredient * selectedList.Items.Count(x => x == recipe.RowId);
                    }
                    else
                    {
                        ingredientList.TryAdd((uint)ing.ItemIngredient, ing.AmountIngredient * selectedList.Items.Count(x => x == recipe.RowId));
                    }

                    var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                    SelectedRecipesCraftable[(uint)ing.ItemIngredient] = FilteredList.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

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
                    SelectedRecipesCraftable[(uint)ing.ItemIngredient] = FilteredList.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                    if (GetIngredientRecipe((uint)ing.ItemIngredient) != null && addSublist)
                    {
                        AddRecipeIngredientsToList(GetIngredientRecipe((uint)ing.ItemIngredient), ref ingredientList);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, "ERROR");
        }
    }

    public static Recipe? GetIngredientRecipe(uint ingredient)
    {
        if (LuminaSheets.RecipeSheet.Values.FindFirst(x => x.ItemResult.Value.RowId == ingredient, out var result))
            return result;

        return null;
    }

    public static void TidyUpList(CraftingList list)
    {
        var tempMaterialList = new Dictionary<uint, int>();
        foreach (var recipe in list.Items.Distinct())
        {
            Recipe r = FilteredList[recipe];
            AddRecipeIngredientsToList(r, ref tempMaterialList, false, list);
        }

        foreach (var requiredItem in tempMaterialList)
        {
            if (SelectedRecipesCraftable[requiredItem.Key])
            {
                var recipe = GetIngredientRecipe(requiredItem.Key);
                if (list.Items.Any(x => x == recipe.RowId))
                {
                    
                    var crafting = list.Items.Count(x => x == recipe.RowId) * recipe.AmountResult;
                    
                    if (crafting > requiredItem.Value)
                    {
                        double diff = crafting - requiredItem.Value;
                        
                        var numberOfCrafts = Math.Floor(diff / recipe.AmountResult);
                        
                        for (int i = 0; i < numberOfCrafts; i++)
                        {
                            var index = list.Items.IndexOf(recipe.RowId);
                            list.Items.RemoveAt(index);
                        }

                        
                    }
                }

            }
        }

        tempMaterialList.Clear();
        foreach (var recipe in list.Items.Distinct())
        {
            Recipe r = FilteredList[recipe];
            AddRecipeIngredientsToList(r, ref tempMaterialList, false, list);
        }

        foreach (var requiredItem in tempMaterialList)
        {
            if (SelectedRecipesCraftable[requiredItem.Key])
            {
                var recipe = GetIngredientRecipe(requiredItem.Key);
                if (list.Items.Any(x => x == recipe.RowId))
                {
                   
                    var crafting = list.Items.Count(x => x == recipe.RowId) * recipe.AmountResult;
                   
                    if (crafting > requiredItem.Value)
                    {
                        double diff = crafting - requiredItem.Value;
                      
                        var numberOfCrafts = Math.Floor(diff / recipe.AmountResult);

                        for (int i = 0; i < numberOfCrafts; i++)
                        {
                            var index = list.Items.IndexOf(recipe.RowId);
                            list.Items.RemoveAt(index);
                        }

                    }
                }

            }
        }

    }
}