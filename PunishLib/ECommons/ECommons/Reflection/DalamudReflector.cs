using Dalamud;
using Dalamud.Game.ClientState.Keys;
using ECommons.Logging;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using ECommons.Schedulers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.Reflection
{
    public static class DalamudReflector
    {
        delegate ref int GetRefValue(int vkCode);
        static GetRefValue getRefValue;
        static Dictionary<string, IDalamudPlugin> pluginCache;
        static List<Action> onPluginsChangedActions;

        internal static void Init()
        {
            onPluginsChangedActions = new();
            pluginCache = new();
            GenericHelpers.Safe(delegate
            {
                getRefValue = (GetRefValue)Delegate.CreateDelegate(typeof(GetRefValue), Svc.KeyState,
                            Svc.KeyState.GetType().GetMethod("GetRefValue",
                            BindingFlags.NonPublic | BindingFlags.Instance,
                            null, new Type[] { typeof(int) }, null));
            });
            GenericHelpers.Safe(delegate
            {
                var pm = GetPluginManager();
                pm.GetType().GetEvent("OnInstalledPluginsChanged").AddEventHandler(pm, OnInstalledPluginsChanged);
            });
        }

        internal static void Dispose()
        {
            if (pluginCache != null)
            {
                pluginCache = null;
                onPluginsChangedActions = null;
                GenericHelpers.Safe(delegate
                {
                    var pm = GetPluginManager();
                    pm.GetType().GetEvent("OnInstalledPluginsChanged").RemoveEventHandler(pm, OnInstalledPluginsChanged);
                });
            }
        }

        public static void RegisterOnInstalledPluginsChangedEvents(params Action[] actions)
        {
            foreach(var x in actions)
            {
                onPluginsChangedActions.Add(x);
            }
        }

        public static void SetKeyState(VirtualKey key, int state)
        {
            getRefValue((int)key) = state;
        }

        public static object GetPluginManager()
        {
            return Svc.PluginInterface.GetType().Assembly.
                    GetType("Dalamud.Service`1", true).MakeGenericType(Svc.PluginInterface.GetType().Assembly.GetType("Dalamud.Plugin.Internal.PluginManager", true)).
                    GetMethod("Get").Invoke(null, BindingFlags.Default, null, Array.Empty<object>(), null);
        }

        public static object GetService(string serviceFullName)
        {
            return Svc.PluginInterface.GetType().Assembly.
                    GetType("Dalamud.Service`1", true).MakeGenericType(Svc.PluginInterface.GetType().Assembly.GetType(serviceFullName, true)).
                    GetMethod("Get").Invoke(null, BindingFlags.Default, null, Array.Empty<object>(), null);
        }

        public static bool TryGetDalamudPlugin(string internalName, out IDalamudPlugin instance, bool suppressErrors = false, bool ignoreCache = false)
        {
            if(!ignoreCache && pluginCache.TryGetValue(internalName, out instance) && instance != null)
            {
                return true;
            }
            try
            {
                var pluginManager = GetPluginManager();
                var installedPlugins = (System.Collections.IList)pluginManager.GetType().GetProperty("InstalledPlugins").GetValue(pluginManager);

                foreach (var t in installedPlugins)
                {
                    if ((string)t.GetType().GetProperty("Name").GetValue(t) == internalName)
                    {
                        var type = t.GetType().Name == "LocalDevPlugin" ? t.GetType().BaseType : t.GetType();
                        var plugin = (IDalamudPlugin)type.GetField("instance", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(t);
                        if (plugin == null)
                        {
                            PluginLog.Warning($"Found requested plugin {internalName} but it was null");
                        }
                        else
                        {
                            instance = plugin;
                            pluginCache[internalName] = plugin;
                            return true;
                        }
                    }
                }
                instance = null;
                return false;
            }
            catch (Exception e)
            {
                if (!suppressErrors)
                {
                    PluginLog.Error($"Can't find {internalName} plugin: " + e.Message);
                    PluginLog.Error(e.StackTrace);
                }
                instance = null;
                return false;
            }
        }

        public static bool TryGetDalamudStartInfo(out DalamudStartInfo dalamudStartInfo, DalamudPluginInterface pluginInterface = null)
        {
            try
            {
                if (pluginInterface == null) pluginInterface = Svc.PluginInterface;
                var info = pluginInterface.GetType().Assembly.
                        GetType("Dalamud.Service`1", true).MakeGenericType(typeof(DalamudStartInfo)).
                        GetMethod("Get").Invoke(null, BindingFlags.Default, null, Array.Empty<object>(), null);
                dalamudStartInfo = (DalamudStartInfo)info;
                return true;
            }
            catch (Exception e)
            {
                PluginLog.Error($"{e.Message}\n{e.StackTrace ?? ""}");
                dalamudStartInfo = default;
                return false;
            }
        }

        public static string GetPluginName()
        {
            return ECommons.Instance?.Name ?? "Not initialized";
        }

        internal static void OnInstalledPluginsChanged()
        {
            PluginLog.Verbose("Installed plugins changed event fired");
            _ = new TickScheduler(delegate
            {
                pluginCache.Clear();
                foreach(var x in onPluginsChangedActions)
                {
                    x();
                }
            });
        }
    }
}
