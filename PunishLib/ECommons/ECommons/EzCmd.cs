using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Dalamud.Game.Command.CommandInfo;

namespace ECommons
{
    public static class EzCmd
    {
        internal static List<string> RegisteredCommands = new();

        public static void Add(string command, HandlerDelegate action, string helpMessage = null)
        {
            RegisteredCommands.Add(command);
            Svc.Commands.AddHandler(command, new(action)
            {
                HelpMessage = helpMessage ?? ""
            });
        }
    }
}
