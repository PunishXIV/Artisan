using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.SplatoonAPI
{
    /// <summary>
    /// 0: Object at fixed coordinates |
    /// 1: Object relative to actor position | 
    /// 2: Line between two fixed coordinates | 
    /// 3: Line relative to object pos | 
    /// 4: Cone relative to object position |
    /// 5: Cone at fixed coordinates
    /// </summary>
    public enum ElementType : int
    {
        CircleAtFixedCoordinates=0,
        CircleRelativeToActorPosition = 1,
        LineBetweenTwoFixedCoordinates=2,
        LineRelativeToObjectPosition=3,
        ConeRelativeToObjectPosition=4,
        ConeAtFixedCoordinates=5
    }
}
