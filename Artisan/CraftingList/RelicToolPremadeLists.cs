using Artisan.RawInformation;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.CraftingLists;

internal static partial class RelicToolPremadeLists
{
    internal const int IdBase = 900_000;

    internal enum RelicToolStep
    {
        SkysteelPlus1 = 1,
        Dragonsung = 2,
        AugmentedDragonsung = 3,
        Skysung = 4,
        Skybuilders = 5,
        Augmented = 6,
        Crystalline = 7,
        ChoraZois = 8,
        Brilliant = 9,
        Vrandtic = 10,
        Lodestar = 11,
    }

    private readonly record struct RelicToolPremadeEntry(RelicToolStep Step, int Quantity, uint ItemId, int CraftType);

    public static void EnsureBuilt(List<NewCraftingList> premadeCraftingLists)
    {
        bool added = false;
        foreach (RelicToolPremadeEntry def in Definitions)
        {
            int id = ToListId(def.Step, def.CraftType);
            if (premadeCraftingLists.Any(x => x.ID == id))
                continue;

            if (!TryBuildList(def, id, out NewCraftingList? list) || list is null)
            {
                Svc.Log.Debug($"[Artisan] Could not build relic premade list {id} (item {def.ItemId}).");
                continue;
            }

            premadeCraftingLists.Add(list);
            added = true;
        }

        if (added)
            Svc.Log.Information("[Artisan] Built relic-tool premade crafting lists.");
    }

    public static bool TryGetListId(int stepOrdinal, int craftTypeSlot, out int listId)
    {
        listId = 0;
        if (craftTypeSlot is < 0 or > 7 || !Enum.IsDefined(typeof(RelicToolStep), stepOrdinal))
            return false;

        listId = ToListId((RelicToolStep)stepOrdinal, craftTypeSlot);
        return Definitions.Any(d => d.Step == (RelicToolStep)stepOrdinal && d.CraftType == craftTypeSlot);
    }

    private static int ToListId(RelicToolStep step, int craftType) => IdBase + ((int)step * 10) + craftType;

    private static string StepLabel(RelicToolStep step) => step switch
    {
        RelicToolStep.SkysteelPlus1 => "Skysteel +1",
        RelicToolStep.Dragonsung => "Dragonsung",
        RelicToolStep.AugmentedDragonsung => "Augmented Dragonsung",
        RelicToolStep.Skysung => "Skysung",
        RelicToolStep.Skybuilders => "Skybuilders'",
        RelicToolStep.Augmented => "Augmented",
        RelicToolStep.Crystalline => "Crystalline",
        RelicToolStep.ChoraZois => "Chora-Zoi's",
        RelicToolStep.Brilliant => "Brilliant",
        RelicToolStep.Vrandtic => "Vrandtic",
        RelicToolStep.Lodestar => "Lodestar",
        _ => step.ToString(),
    };

    private static bool TryBuildList(RelicToolPremadeEntry def, int id, out NewCraftingList? list)
    {
        list = null;
        if (LuminaSheets.ItemSheet is null || !LuminaSheets.ItemSheet.ContainsKey(def.ItemId))
            return false;

        Recipe recipe = LuminaSheets.RecipeSheet.Values.FirstOrDefault(x =>
            x.ItemResult.Value.RowId == def.ItemId && x.CraftType.RowId == def.CraftType);
        if (recipe.RowId == 0)
            return false;

        string jobName = LuminaSheets.ClassJobSheet[(uint)(def.CraftType + 8)].Name.ToString();
        list = new NewCraftingList
        {
            ID = id,
            Name = $"Relic Tool — {StepLabel(def.Step)} — {jobName}",
            IsPremade = true,
        };
        list.Locked = true;
        CraftingListUI.AddAllSubcrafts(recipe, list, def.Quantity);

        if (list.Recipes.FirstOrDefault(x => x.ID == recipe.RowId) is { } existing)
            existing.Quantity = def.Quantity;
        else
            list.Recipes.Add(new ListItem { ID = recipe.RowId, Quantity = def.Quantity });

        CraftingListHelpers.TidyUpList(list);
        list.Locked = false;
        list.Save();
        return true;
    }
}
