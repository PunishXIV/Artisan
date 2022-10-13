using Dalamud.Game;
using ECommons.Logging;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.Schedulers
{
    public class TickScheduler : IScheduler
    {
        long executeAt;
        Action function;
        bool disposed = false;

        public TickScheduler(Action function, long delayMS = 0)
        {
            executeAt = Environment.TickCount64 + delayMS;
            this.function = function;
            Svc.Framework.Update += Execute;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                Svc.Framework.Update -= Execute;
            }
            disposed = true;
        }

        void Execute(object _)
        {
            if (Environment.TickCount64 < executeAt) return;
            try
            {
                function();
            }
            catch (Exception e)
            {
                PluginLog.Error(e.Message + "\n" + e.StackTrace ?? "");
            }
            Dispose();
        }
    }
}