using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.SplatoonAPI
{

    /// 0: Game object with specific name |
    /// 1: Self |
    /// 2: Targeted enemy
    public enum RefActorType
    {
        GameObjectWithSpecifiedAttribute=0,
        Self=1,
        TargetedEnemy=2
    }
}
