using Artisan.RawInformation;
using System.Linq;

namespace Artisan.CustomDeliveries
{
    internal class SatisfactionDataCache
    {
    }

    public class SatisfactionNPCDetail
    {
        public string Name;

        public uint CraftItemID;

        public uint RecipeID
        {
            get
            {
                if (CharacterInfo.JobID() < 8 || CharacterInfo.JobID() > 15)
                    return 0;

                return LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.RowId == CraftItemID && x.CraftType.Value.RowId == CharacterInfo.JobID() - 8).FirstOrDefault().RowId;
            }
        }

        public bool IsBonus;
    }
}
