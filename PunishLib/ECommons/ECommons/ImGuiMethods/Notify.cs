using Dalamud.Interface.Internal.Notifications;
using ECommons.DalamudServices;
using ECommons.Reflection;
using ECommons.Schedulers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.ImGuiMethods
{
    public static class Notify
    {
        public static void Success(string s)
        {
            _ = new TickScheduler(delegate
            {
                Svc.PluginInterface.UiBuilder.AddNotification(s, DalamudReflector.GetPluginName(), NotificationType.Success);
            });
        }

        public static void Info(string s)
        {
            _ = new TickScheduler(delegate
            {
                Svc.PluginInterface.UiBuilder.AddNotification(s, DalamudReflector.GetPluginName(), NotificationType.Info);
            });
        }

        public static void Error(string s)
        {
            _ = new TickScheduler(delegate
            {
                Svc.PluginInterface.UiBuilder.AddNotification(s, DalamudReflector.GetPluginName(), NotificationType.Error);
            });
        }

        public static void Warning(string s)
        {
            _ = new TickScheduler(delegate
            {
                Svc.PluginInterface.UiBuilder.AddNotification(s, DalamudReflector.GetPluginName(), NotificationType.Warning);
            });
        }

        public static void Plain(string s)
        {
            _ = new TickScheduler(delegate
            {
                Svc.PluginInterface.UiBuilder.AddNotification(s, DalamudReflector.GetPluginName(), NotificationType.None);
            });
        }
    }
}
