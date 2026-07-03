using Artisan.RawInformation;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.CraftingLists;

/// <summary>
///     Premade crafting lists for Disciple of the Hand relic tool collectable steps.
///     List IDs: 900_000 + stepOrdinal * 10 + craftType (CRP=0 … CUL=7). Regenerate definitions via
///     RelicTracker <c>data/gen_artisan_relic_lists.py</c> when <c>gen_tool_materials.py</c> changes.
/// </summary>
internal static partial class RelicToolPremadeLists
{
    internal const int IdBase = 900_000;

    private readonly record struct RelicToolPremadeEntry(int Id, string Name, int Quantity, string Collectable, int CraftType);

    private static readonly Dictionary<string, int> StepOrdinal = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Skysteel +1"] = 1,
        ["Dragonsung"] = 2,
        ["Augmented Dragonsung"] = 3,
        ["Skysung"] = 4,
        ["Skybuilders'"] = 5,
        ["Augmented"] = 6,
        ["Crystalline"] = 7,
        ["Chora-Zoi's"] = 8,
        ["Brilliant"] = 9,
        ["Vrandtic"] = 10,
        ["Lodestar"] = 11,
    };

    public static void EnsureBuilt(List<NewCraftingList> premadeCraftingLists)
    {
        bool added = false;
        foreach (RelicToolPremadeEntry def in Definitions)
        {
            if (premadeCraftingLists.Any(x => x.ID == def.Id))
            {
                continue;
            }

            if (!TryBuildList(def, out NewCraftingList? list) || list == null)
            {
                Svc.Log.Debug("[Artisan] Could not build relic premade list {Name} ({Collectable}).", def.Name, def.Collectable);
                continue;
            }

            premadeCraftingLists.Add(list);
            added = true;
            Svc.Log.Debug("[Artisan] Added relic premade list {Name} (ID {Id}).", list.Name ?? def.Name, list.ID);
        }

        if (added)
        {
            Svc.Log.Information("[Artisan] Built relic-tool premade crafting lists.");
        }
    }

    public static bool TryGetListId(string stepName, int craftTypeSlot, out int listId)
    {
        listId = 0;
        if (craftTypeSlot < 0 || craftTypeSlot > 7)
        {
            return false;
        }

        if (!StepOrdinal.TryGetValue(stepName, out int ordinal))
        {
            return false;
        }

        int id = IdBase + (ordinal * 10) + craftTypeSlot;
        listId = id;
        return Definitions.Any(d => d.Id == id);
    }

    private static bool TryBuildList(RelicToolPremadeEntry def, out NewCraftingList? list)
    {
        list = null;
        Item itemRow = LuminaSheets.ItemSheet.Values.FirstOrDefault(x =>
            string.Equals(x.Name.ToString(), def.Collectable, StringComparison.Ordinal));
        if (itemRow.RowId == 0)
        {
            return false;
        }

        Recipe recipe = LuminaSheets.RecipeSheet.Values.FirstOrDefault(x =>
            x.ItemResult.Value.RowId == itemRow.RowId && x.CraftType.RowId == def.CraftType);
        if (recipe.RowId == 0)
        {
            return false;
        }

        list = new NewCraftingList
        {
            ID = def.Id,
            Name = def.Name,
            IsPremade = true,
        };
        list.Locked = true;
        CraftingListUI.AddAllSubcrafts(recipe, list, def.Quantity);
        if (list.Recipes.Any(x => x.ID == recipe.RowId))
        {
            list.Recipes.First(x => x.ID == recipe.RowId).Quantity = def.Quantity;
        }
        else
        {
            list.Recipes.Add(new ListItem { ID = recipe.RowId, Quantity = def.Quantity });
        }

        CraftingListHelpers.TidyUpList(list);
        list.Locked = false;
        list.Save();
        return true;
    }
}
