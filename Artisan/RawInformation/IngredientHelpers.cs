using Artisan.CraftingLists;
using Artisan.RawInformation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class IngredientHelpers
{
    public int CurrentIngredient = 0;
    public int MaxIngredient = 0;

    public List<Ingredient> HelperList = new List<Ingredient>();

    public async Task<List<Ingredient>> GenerateList(NewCraftingList originList, System.Threading.CancellationTokenSource source)
    {
        var materials = originList.ListMaterials();
        List<Ingredient> output = new();
        var taskList = new List<Task>();
        MaxIngredient = materials.Count();
        foreach (var item in materials.OrderBy(x => x.Key))
        {
            if (source.IsCancellationRequested) return null;
            CurrentIngredient++;
            await Task.Run(() => output.Add(new Ingredient(item.Key, item.Value, originList, materials, this)));
        }

        HelperList = output;
        return output;
    }
}