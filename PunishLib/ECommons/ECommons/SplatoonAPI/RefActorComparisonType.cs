using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.SplatoonAPI
{

    /// <summary>
    /// 0: Name |
    /// 1: Model ID |
    /// 2: Object ID |
    /// 3: Data ID | 
    /// 4: NPC ID |
    /// 5: Placeholder |
    /// 6: Name ID | 
    /// 7: VFX Path
    /// </summary>
    public enum RefActorComparisonType : int
    {
        Name=0,
        ModelID=1,
        ObjectID=2,
        DataID=3,
        NpcID=4,
        Placeholder=5,
        NameID=6,
        VfxPath=7
    }
}
