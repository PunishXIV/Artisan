using ECommons;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan
{
    internal class BlockingTask
    {
        Action? action = null;
        long ExecuteAt = 0;

        internal void Schedule(Action a, long delayMs)
        {
            action = a;
            ExecuteAt = Environment.TickCount64 + delayMs;
        }

        internal bool TryBlockOrExecute()
        {
            if(action == null)
            {
                return false;
            }
            else
            {
                if(Environment.TickCount64 > ExecuteAt)
                {
                    GenericHelpers.Safe(action);
                    action = null;
                }
                return true;
            }
        }
    }
}
