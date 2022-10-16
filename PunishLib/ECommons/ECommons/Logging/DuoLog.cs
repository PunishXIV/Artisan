using Dalamud.Game.Text.SeStringHandling;
using ECommons.Logging;
using ECommons.DalamudServices;
using ECommons.Reflection;
using ECommons.Schedulers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.Logging
{
    public static class DuoLog
    {
        public static void Information(string s)
        {
            var str = $"[{DalamudReflector.GetPluginName()}] {s}";
            PluginLog.Information(str);
            _ = new TickScheduler(delegate
            {
                Svc.Chat.PrintChat(new()
                {
                    Message = new SeStringBuilder().AddUiForeground(str, 3).Build()
                });
            });
        }

        public static void Debug(string s)
        {
            var str = $"[{DalamudReflector.GetPluginName()}] {s}";
            PluginLog.Debug(str);
            _ = new TickScheduler(delegate
            {
                Svc.Chat.PrintChat(new()
                {
                    Message = new SeStringBuilder().AddUiForeground(str, 4).Build()
                });
            });
        }

        public static void Verbose(string s)
        {
            var str = $"[{DalamudReflector.GetPluginName()}] {s}";
            PluginLog.Verbose(str);
            _ = new TickScheduler(delegate
            {
                Svc.Chat.PrintChat(new()
                {
                    Message = new SeStringBuilder().AddUiForeground(str, 5).Build()
                });
            });
        }

        public static void Warning(string s)
        {
            var str = $"[{DalamudReflector.GetPluginName()}] {s}";
            PluginLog.Warning(str);
            _ = new TickScheduler(delegate
            {
                Svc.Chat.PrintChat(new()
                {
                    Message = new SeStringBuilder().AddUiForeground(str, 540).Build()
                });
            });
        }

        public static void Error(string s)
        {
            var str = $"[{DalamudReflector.GetPluginName()}] {s}";
            PluginLog.Error(str);
            _ = new TickScheduler(delegate
            {
                Svc.Chat.PrintChat(new()
                {
                    Message = new SeStringBuilder().AddUiForeground(str, 17).Build()
                });
            });
        }

        public static void Fatal(string s)
        {
            var str = $"[{DalamudReflector.GetPluginName()}] {s}";
            PluginLog.Fatal(str);
            _ = new TickScheduler(delegate
            {
                Svc.Chat.PrintChat(new()
                {
                    Message = new SeStringBuilder().AddUiForeground(str, 19).Build()
                });
            });
        }
    }
}
