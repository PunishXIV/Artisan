using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.RawInformation
{
    public class LuminaSheets
    {
        
        public static Dictionary<uint, Lumina.Excel.GeneratedSheets.Recipe>? RecipeSheet = Service.DataManager?.GetExcelSheet<Lumina.Excel.GeneratedSheets.Recipe>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Lumina.Excel.GeneratedSheets.Action>? ActionSheet = Service.DataManager?.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Lumina.Excel.GeneratedSheets.CraftAction>? CraftActions = Service.DataManager?.GetExcelSheet<Lumina.Excel.GeneratedSheets.CraftAction>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Lumina.Excel.GeneratedSheets.CraftLevelDifference>? CraftLevelDifference = Service.DataManager?.GetExcelSheet<Lumina.Excel.GeneratedSheets.CraftLevelDifference>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Lumina.Excel.GeneratedSheets.RecipeLevelTable>? RecipeLevelTableSheet = Service.DataManager?.GetExcelSheet<Lumina.Excel.GeneratedSheets.RecipeLevelTable>()?
    .ToDictionary(i => i.RowId, i => i);
    }
}
