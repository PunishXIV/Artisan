using ECommons.Automation;
using System;

namespace Artisan.Autocraft
{
    internal static class CommandProcessor
    {
        static long nextCommandAt = 0;
        internal static bool ExecuteThrottled(string command)
        {
            if (Environment.TickCount64 > nextCommandAt)
            {
                nextCommandAt = Environment.TickCount64 + 500;
                Chat.SendMessage(command);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
