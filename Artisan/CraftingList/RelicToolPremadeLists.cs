using Artisan.RawInformation;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Artisan.CraftingLists;

internal static partial class RelicToolPremadeLists
{
    internal const uint IdBase = 900_000;

    internal const uint AllClassesSlot = 8;

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
        Supra = 12,
        Lucis = 13,
        ResplendentA = 14,
        ResplendentB = 15,
        ResplendentC = 16,
        Zodiac = 17,
        Umbrae = 18,
        Obscurum = 19,
        Eclipticum = 20,
        Anima = 21,
        Relic = 22,
    }

    private readonly record struct RelicToolPremadeEntry(RelicToolStep Step, int Quantity, uint RecipeId);

    private readonly record struct RelicWeaponPremadeEntry(RelicToolStep Step, (uint RecipeId, int Quantity)[] Recipes);
    
    private readonly record struct RelicJobPremadeEntry(RelicToolStep Step, uint JobSlot, string Job, int Quantity, uint RecipeId);

    public static void EnsureBuilt(List<NewCraftingList> premadeCraftingLists)
    {
        bool added = false;
        foreach (RelicToolPremadeEntry def in Definitions)
        {
            var subStep = LuminaSheets.RecipeSheet[def.RecipeId].CraftType.RowId;
            uint id = ToListId(def.Step, subStep);
            if (premadeCraftingLists.Any(x => x.ID == id))
                continue;

            if (!TryBuildList(def, id, out NewCraftingList? list) || list is null)
            {
                Svc.Log.Debug($"[Artisan] Could not build relic premade list {id} (item {def.RecipeId}).");
                continue;
            }

            premadeCraftingLists.Add(list);
            added = true;
        }

        foreach (RelicWeaponPremadeEntry def in WeaponDefinitions)
        {
            uint id = ToListId(def.Step, AllClassesSlot);
            if (premadeCraftingLists.Any(x => x.ID == id))
                continue;

            premadeCraftingLists.Add(BuildList(id, $"Relic Weapon — {StepLabel(def.Step)}", def.Recipes));
            added = true;
        }

        foreach (RelicJobPremadeEntry def in JobDefinitions)
        {
            uint id = ToListId(def.Step, def.JobSlot);
            if (premadeCraftingLists.Any(x => x.ID == id))
                continue;

            premadeCraftingLists.Add(BuildList(id, $"Relic Weapon — {StepLabel(def.Step)} — {def.Job}", [(def.RecipeId, def.Quantity)]));
            added = true;
        }

        if (added)
            Svc.Log.Information("[Artisan] Built relic-tool premade crafting lists.");
    }

    public static bool TryGetListId(int stepOrdinal, uint craftTypeSlot, out uint listId)
    {
        listId = 0;
        if (!Enum.IsDefined(typeof(RelicToolStep), stepOrdinal))
            return false;

        var step = (RelicToolStep)stepOrdinal;
        if (JobDefinitions.Any(d => d.Step == step))
        {
            listId = ToListId(step, craftTypeSlot);
            return JobDefinitions.Any(d => d.Step == step && d.JobSlot == craftTypeSlot);
        }

        if (craftTypeSlot > AllClassesSlot)
            return false;

        listId = ToListId(step, craftTypeSlot);
        return craftTypeSlot == AllClassesSlot
            ? WeaponDefinitions.Any(d => d.Step == step)
            : Definitions.Any(d => d.Step == step && LuminaSheets.RecipeSheet[d.RecipeId].CraftType.RowId == craftTypeSlot);
    }

    private static uint ToListId(RelicToolStep step, uint craftType) => IdBase + ((uint)step * 10) + craftType;

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
        RelicToolStep.Supra => "Supra",
        RelicToolStep.Lucis => "Lucis",
        RelicToolStep.ResplendentA => "Resplendent Component A",
        RelicToolStep.ResplendentB => "Resplendent Component B",
        RelicToolStep.ResplendentC => "Resplendent Component C",
        RelicToolStep.Zodiac => "Zodiac",
        RelicToolStep.Umbrae => "Umbrae (one-time step)",
        RelicToolStep.Obscurum => "Obscurum (one-time step)",
        RelicToolStep.Eclipticum => "Eclipticum (one-time step)",
        RelicToolStep.Anima => "Anima",
        RelicToolStep.Relic => "A Relic Reborn",
        _ => step.ToString(),
    };

    private static bool TryBuildList(RelicToolPremadeEntry def, uint id, out NewCraftingList? list)
    {
        list = null;

        var recipe = LuminaSheets.RecipeSheet[def.RecipeId];

        string jobName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(LuminaSheets.ClassJobSheet[(uint)(recipe.CraftType.RowId + 8)].Name.ToString());
        list = new NewCraftingList
        {
            ID = Convert.ToInt32(id),
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
    
    private static NewCraftingList BuildList(uint id, string name, (uint RecipeId, int Quantity)[] recipes)
    {
        var list = new NewCraftingList
        {
            ID = Convert.ToInt32(id),
            Name = name,
            IsPremade = true,
        };
        list.Locked = true;
        foreach ((uint recipeId, int quantity) in recipes)
        {
            var recipe = LuminaSheets.RecipeSheet[recipeId];
            CraftingListUI.AddAllSubcrafts(recipe, list, quantity);

            if (list.Recipes.FirstOrDefault(x => x.ID == recipe.RowId) is { } existing)
                existing.Quantity = quantity;
            else
                list.Recipes.Add(new ListItem { ID = recipe.RowId, Quantity = quantity });
        }

        CraftingListHelpers.TidyUpList(list);
        list.Locked = false;
        list.Save();
        return list;
    }
}
