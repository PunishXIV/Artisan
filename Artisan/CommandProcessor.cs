using ECommons.Automation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan
{
    internal static class CommandProcessor
    {
        static long nextCommandAt = 0;
        static Chat? chat = null;
        internal static bool ExecuteThrottled(string command)
        {
            if(Environment.TickCount64 > nextCommandAt)
            {
                nextCommandAt = Environment.TickCount64 + 1500;
                chat ??= new();
                chat.SendMessage(command);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
