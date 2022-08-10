using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.Autocraft
{
    internal static class Throttler
    {
        static long NextCommandAt = 0;
        internal static bool Throttle(int ms)
        {
            if(Environment.TickCount64 > NextCommandAt)
            {
                NextCommandAt = Environment.TickCount64 + ms;
                return true;
            }
            return false;
        }
    }
}
