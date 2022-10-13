using Dalamud.Configuration;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using ECommons.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.SimpleGui
{
    public static class EzConfigGui
    {
        internal static WindowSystem windowSystem;
        internal static Action Draw = null;
        internal static Action OnClose = null;
        internal static Action OnOpen = null;
        internal static IPluginConfiguration Config;
        static ConfigWindow configWindow;
        static string Ver = string.Empty;
        public static Window Window { get { return configWindow; } }

        public static void Init(string name, Action draw, IPluginConfiguration config = null)
        {
            if(windowSystem != null)
            {
                throw new Exception("ConfigGui already initialized");
            }
            windowSystem = new($"ECommons@{DalamudReflector.GetPluginName()}");
            Draw = draw;
            Config = config;
            Ver = ECommons.Instance.GetType().Assembly.GetName().Version.ToString();
            configWindow = new($"{name} v{Ver}###{name}");
            windowSystem.AddWindow(configWindow);
            Svc.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
            Svc.PluginInterface.UiBuilder.OpenConfigUi += Open;
        }

        public static void Open()
        {
            configWindow.IsOpen = true;
        }
        
        public static void Open(string cmd = null, string args = null)
        {
            Open();
        }
    }
}
