using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using PInvoke;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.GameFunctions
{
    public static unsafe class FakePronoun
    {
        public static GameObject* Resolve(string pronoun)
        {
            try
            {
                if (Svc.Condition[ConditionFlag.DutyRecorderPlayback])
                {
                    if (uint.TryParse(pronoun[1..2], out var pos))
                    {
                        var i = 0;
                        foreach (var x in Svc.Objects)
                        {
                            if (x is PlayerCharacter pc)
                            {
                                i++;
                                if (i == pos)
                                {
                                    return (GameObject*)pc.Address;
                                }
                            }
                        }
                        return null;
                    }
                    else
                    {
                        if (pronoun.StartsWith("<t") && int.TryParse(pronoun[2..3], out var n))
                        {
                            return GetRolePlaceholder(CombatRole.Tank, n);
                        }
                        else if (pronoun.StartsWith("<h") && int.TryParse(pronoun[2..3], out n))
                        {
                            return GetRolePlaceholder(CombatRole.Healer, n);
                        }
                        else if (pronoun.StartsWith("<d") && int.TryParse(pronoun[2..3], out n))
                        {
                            return GetRolePlaceholder(CombatRole.DPS, n);
                        }
                        return null;
                    }
                }
                else
                {
                    return FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->GetPronounModule()->ResolvePlaceholder($"{pronoun}", 0, 0);
                }
            }
            catch(Exception e)
            {
                e.Log();
                return null;
            }
        }

        static GameObject* GetRolePlaceholder(CombatRole role, int pos)
        {
            var i = 0;
            foreach (var x in Svc.Objects)
            {
                if (x is PlayerCharacter pc)
                {
                    if (pc.GetRole() == role)
                    {
                        i++;
                        if (i == pos)
                        {
                            return (GameObject*)pc.Address;
                        }
                    }
                }
            }
            return null;
        }
    }
}
