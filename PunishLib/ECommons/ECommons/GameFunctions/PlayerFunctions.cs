using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.GameFunctions
{
    public unsafe static class PlayerFunctions
    {
        public static bool TryGetPlaceholder(this GameObject pc, out int number)
        {
            for(var i = 1; i <= 8; i++)
            {
                var optr = Framework.Instance()->GetUiModule()->GetPronounModule()->ResolvePlaceholder($"<{i}>", 0, 0);
                PluginLog.Debug($"Placeholder {i} value {(optr == null ? "null" : optr->ObjectID)}");
                if (pc.Address == (IntPtr)optr)
                {
                    number = i;
                    return true;
                }
            }
            number = default;
            return false;
        }
    }
}
