using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.GameFunctions
{
    public static class FakeParty
    {
        public static IEnumerator<PlayerCharacter> Get()
        {
            if (Svc.Condition[ConditionFlag.DutyRecorderPlayback])
            {
                foreach (var x in Svc.Objects)
                {
                    if (x is PlayerCharacter pc)
                    {
                        yield return pc;
                    }
                }
            }
            else
            {
                foreach(var x in Svc.Party)
                {
                    if(x.GameObject is PlayerCharacter pc)
                    {
                        yield return pc;
                    }
                }
            }
        }
    }
}
