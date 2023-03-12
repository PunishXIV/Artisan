using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.QuestSync
{
    internal class QuestList
    {
        public static readonly Dictionary<uint, QuestObject> BeastTribeQuests = new()
        {
            {67828, new(){ QuestID = 67828 } }
        };
    }

    public class QuestObject
    {
        public uint QuestID;
        public uint Sequence;
        public uint RecipeID;
    }
}
