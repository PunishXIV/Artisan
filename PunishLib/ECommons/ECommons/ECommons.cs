using ECommons.Logging;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.ImGuiMethods;
using ECommons.ObjectLifeTracker;
using ECommons.Reflection;
using ECommons.SimpleGui;
using ECommons.SplatoonAPI;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons
{
    public static class ECommons
    {
        internal static IDalamudPlugin Instance = null;
        //test
        public static void Init(DalamudPluginInterface pluginInterface, IDalamudPlugin instance, params Module[] modules)
        {
            Instance = instance;
            GenericHelpers.Safe(() => Svc.Init(pluginInterface));
            if (modules.ContainsAny(Module.All, Module.ObjectFunctions))
            {
                PluginLog.Information("Object functions module has been requested");
                GenericHelpers.Safe(ObjectFunctions.Init);
            }
            if (modules.ContainsAny(Module.All, Module.DalamudReflector, Module.SplatoonAPI))
            {
                PluginLog.Information("Advanced Dalamud reflection module has been requested");
                GenericHelpers.Safe(DalamudReflector.Init);
            }
            if (modules.ContainsAny(Module.All, Module.ObjectLife))
            {
                PluginLog.Information("Object life module has been requested");
                GenericHelpers.Safe(ObjectLife.Init);
            }
            if(modules.ContainsAny(Module.All, Module.SplatoonAPI))
            {
                PluginLog.Information("Splatoon API module has been requested");
                GenericHelpers.Safe(Splatoon.Init);
            }
        }

        public static void Dispose()
        {
            foreach(var x in ImGuiMethods.ThreadLoadImageHandler.CachedTextures)
            {
                GenericHelpers.Safe(x.Value.texture.Dispose);
            }
            GenericHelpers.Safe(ImGuiMethods.ThreadLoadImageHandler.CachedTextures.Clear);
            GenericHelpers.Safe(ObjectLife.Dispose);
            GenericHelpers.Safe(DalamudReflector.Dispose);
            if(EzConfigGui.windowSystem != null)
            {
                Svc.PluginInterface.UiBuilder.OpenConfigUi -= EzConfigGui.Open;
                Svc.PluginInterface.UiBuilder.Draw -= EzConfigGui.Draw;
                if (EzConfigGui.Config != null)
                {
                    Svc.PluginInterface.SavePluginConfig(EzConfigGui.Config);
                    Notify.Info("Configuration saved");
                }
                EzConfigGui.windowSystem.RemoveAllWindows();
                EzConfigGui.windowSystem = null;
            }
            foreach(var x in EzCmd.RegisteredCommands)
            {
                Svc.Commands.RemoveHandler(x);
            }
            if(Splatoon.Instance != null)
            {
                GenericHelpers.Safe(Splatoon.Reset);
            }
            GenericHelpers.Safe(Splatoon.Shutdown);
            Instance = null;
        }
    }
}
