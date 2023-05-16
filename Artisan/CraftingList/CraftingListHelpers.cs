using Artisan.CraftingLists;
using Artisan.RawInformation;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using PluginLog = Dalamud.Logging.PluginLog;

internal static class CraftingListHelpers
{
    public static Dictionary<uint, Recipe> FilteredList = LuminaSheets.RecipeSheet.Values
                .DistinctBy(x => x.RowId)
                .OrderBy(x => x.RecipeLevelTable.Value.ClassJobLevel)
                .ThenBy(x => x.ItemResult.Value.Name.RawString)
                .ToDictionary(x => x.RowId, x => x);
    internal static Dictionary<int, int> SelectedListMateralsNew = new();
    internal static Dictionary<int, bool> SelectedRecipesCraftable = new();

    public static void AddRecipeIngredientsToList(Recipe? recipe, ref Dictionary<int, int> ingredientList, bool addSublist = true, CraftingList selectedList = null)
    {
        try
        {
            if (recipe == null) return;

            if (selectedList != null)
            {
                foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0 && x.ItemIngredient != 0))
                {
                    if (ingredientList.ContainsKey(ing.ItemIngredient))
                    {
                        ingredientList[ing.ItemIngredient] += ing.AmountIngredient * selectedList.Items.Count(x => x == recipe.RowId);
                    }
                    else
                    {
                        ingredientList.TryAdd(ing.ItemIngredient, ing.AmountIngredient * selectedList.Items.Count(x => x == recipe.RowId));
                    }

                    var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                    SelectedRecipesCraftable[ing.ItemIngredient] = FilteredList.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                    if (GetIngredientRecipe(ing.ItemIngredient) != null && addSublist)
                    {
                        AddRecipeIngredientsToList(GetIngredientRecipe(ing.ItemIngredient), ref ingredientList);
                    }

                }
            }
            else
            {
                foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0 && x.ItemIngredient != 0))
                {
                    if (ingredientList.ContainsKey(ing.ItemIngredient))
                    {
                        ingredientList[ing.ItemIngredient] += ing.AmountIngredient;
                    }
                    else
                    {
                        ingredientList.TryAdd(ing.ItemIngredient, ing.AmountIngredient);
                    }

                    var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                    SelectedRecipesCraftable[ing.ItemIngredient] = FilteredList.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                    if (GetIngredientRecipe(ing.ItemIngredient) != null && addSublist)
                    {
                        AddRecipeIngredientsToList(GetIngredientRecipe(ing.ItemIngredient), ref ingredientList);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Dalamud.Logging.PluginLog.Error(ex, "ERROR");
        }
    }

    public static Recipe? GetIngredientRecipe(int ingredient)
    {
        if (FilteredList.Values.Any(x => x.ItemResult.Value.RowId == ingredient))
            return FilteredList.Values.First(x => x.ItemResult.Value.RowId == ingredient);

        return null;
    }

    public static void TidyUpList(CraftingList list)
    {
        var tempMaterialList = new Dictionary<int, int>();
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