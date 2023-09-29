using ECommons.Automation;
using System;

namespace Artisan.Autocraft
{
    internal static class CommandProcessor
    {
        static long nextCommandAt = 0;
        static Chat? chat = null;
        internal static bool ExecuteThrottled(string command)
        {
            if (Environment.TickCount64 > nextCommandAt)
            {
                nextCommandAt = Environment.TickCount64 + 500;
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
