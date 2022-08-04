using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PunishLib
{
    public static class Motd
    {
        const string GUID = "/f4a0ec2c-620e-411c-a7b7-bb10972d417a";
        public static void PrintOnce()
        {
            if(!Svc.Commands.Commands.ContainsKey(GUID))
            {
                Svc.Commands.AddHandler(GUID, new(OnCommand) { ShowInHelp = false });
                Svc.Framework.Update += FrameworkUpdate;
            }
        }

        static void OnCommand(object a, object b)
        {
            Svc.Commands.RemoveHandler(GUID);
        }

        static void FrameworkUpdate(Framework f)
        {
            if(Svc.ClientState.LocalPlayer != null)
            {
                Svc.Framework.Update -= FrameworkUpdate;
                Svc.Chat.PrintChat(new()
                {
                    Message = new SeStringBuilder().AddUiForeground("Hello, you should see this message only once", 41).Build()
                });
            }
        }
    }
}
