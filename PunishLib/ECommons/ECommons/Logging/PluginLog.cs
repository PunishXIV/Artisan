using ECommons.DalamudServices;
using ECommons.Reflection;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.Logging
{
    public static class PluginLog
    {

        public static void Information(string s)
        {
            Dalamud.Logging.PluginLog.Information($"[{DalamudReflector.GetPluginName()}] {s}");
            Svc.Framework?.RunOnFrameworkThread(delegate
            {
                InternalLog.Messages.PushBack(new(s, LogEventLevel.Information));
            });
        }
        public static void Error(string s)
        {
            Dalamud.Logging.PluginLog.Error($"[{DalamudReflector.GetPluginName()}] {s}");
            Svc.Framework?.RunOnFrameworkThread(delegate
            {
                InternalLog.Messages.PushBack(new(s, LogEventLevel.Error));
            });
        }
        public static void Fatal(string s)
        {
            Dalamud.Logging.PluginLog.Fatal($"[{DalamudReflector.GetPluginName()}] {s}");
            Svc.Framework?.RunOnFrameworkThread(delegate
            {
                InternalLog.Messages.PushBack(new(s, LogEventLevel.Fatal));
            });
        }
        public static void Debug(string s)
        {
            Dalamud.Logging.PluginLog.Debug($"[{DalamudReflector.GetPluginName()}] {s}");
            Svc.Framework?.RunOnFrameworkThread(delegate
            {
                InternalLog.Messages.PushBack(new(s, LogEventLevel.Debug));
            });
        }
        public static void Verbose(string s)
        {
            Dalamud.Logging.PluginLog.Verbose($"[{DalamudReflector.GetPluginName()}] {s}");
            Svc.Framework?.RunOnFrameworkThread(delegate
            {
                InternalLog.Messages.PushBack(new(s, LogEventLevel.Verbose));
            });
        }
        public static void Warning(string s)
        {
            Dalamud.Logging.PluginLog.Warning($"[{DalamudReflector.GetPluginName()}] {s}");
            Svc.Framework?.RunOnFrameworkThread(delegate
            {
                InternalLog.Messages.PushBack(new(s, LogEventLevel.Warning));
            });
        }
        public static void LogInformation(string s)
        {
            Information(s);
        }
        public static void LogError(string s)
        {
            Error(s);
        }
        public static void LogFatal(string s)
        {
            Fatal(s);
        }
        public static void LogDebug(string s)
        {
            Debug(s);
        }
        public static void LogVerbose(string s)
        {
            Verbose(s);
        }
        public static void LogWarning(string s)
        {
            Warning(s);
        }
        public static void Log(string s)
        {
            Information(s);
        }
    }
}
