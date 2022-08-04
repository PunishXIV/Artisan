using Dalamud.Logging;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.ObjectLifeTracker;
using ECommons.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons
{
    public static class ECommons
    {
        public static void Init(DalamudPluginInterface pluginInterface, params Module[] modules)
        {
            GenericHelpers.Safe(() => Svc.Init(pluginInterface));
            if (modules.ContainsAny(Module.All, Module.ObjectFunctions))
            {
                PluginLog.Information("Object functions module has been requested");
                GenericHelpers.Safe(ObjectFunctions.Init);
            }
            if (modules.ContainsAny(Module.All, Module.DalamudReflector))
            {
                PluginLog.Information("Advanced Dalamud reflection module has been requested");
                GenericHelpers.Safe(DalamudReflector.Init);
            }
            if (modules.ContainsAny(Module.All, Module.ObjectLife))
            {
                PluginLog.Information("Object life module has been requested");
                GenericHelpers.Safe(ObjectLife.Init);
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
        }
    }
}
