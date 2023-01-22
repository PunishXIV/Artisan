using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.RawInformation
{
    public class LuminaSheets
    {

        public static Dictionary<uint, Recipe>? RecipeSheet = Service.DataManager?.GetExcelSheet<Recipe>()?
            .Where(x => !string.IsNullOrEmpty(x.ItemResult.Value.Name.RawString))
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, GatheringItem>? GatheringItemSheet = Service.DataManager?.GetExcelSheet<GatheringItem>()?
            .Where(x => x.GatheringItemLevel.Value.GatheringItemLevel > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, SpearfishingItem>? SpearfishingItemSheet = Service.DataManager?.GetExcelSheet<SpearfishingItem>()?
            .Where(x => x.GatheringItemLevel.Value.GatheringItemLevel > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, GatheringPointBase>? GatheringPointBaseSheet = Service.DataManager?.GetExcelSheet<GatheringPointBase>()?
            .Where(x => x.GatheringLevel > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, ClassJob>? ClassJobSheet = Service.DataManager?.GetExcelSheet<ClassJob>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Item>? ItemSheet = Service.DataManager?.GetExcelSheet<Item>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Lumina.Excel.GeneratedSheets.Action>? ActionSheet = Service.DataManager?.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, CraftAction>? CraftActions = Service.DataManager?.GetExcelSheet<CraftAction>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, CraftLevelDifference>? CraftLevelDifference = Service.DataManager?.GetExcelSheet<CraftLevelDifference>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, RecipeLevelTable>? RecipeLevelTableSheet = Service.DataManager?.GetExcelSheet<RecipeLevelTable>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Addon>? AddonSheet = Service.DataManager?.GetExcelSheet<Addon>()?
            .ToDictionary(i => i.RowId, i => i);
    }

    public static class SheetExtensions
    {
        public static string NameOfAction(this uint id)
        {
            if (id < 100000)
            {
                return LuminaSheets.ActionSheet[id].Name.RawString;
            }
            else
            {
                return LuminaSheets.CraftActions[id].Name.RawString;
            }
        }
    }
}
