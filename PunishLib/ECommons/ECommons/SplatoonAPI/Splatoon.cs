using ECommons.Logging;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using ECommons.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.SplatoonAPI
{
    public static class Splatoon
    {
        internal static IDalamudPlugin Instance;
        internal static int Version;
        public static Action OnConnect = null;

        internal static void Init()
        {
            try
            {
                if (Svc.PluginInterface.GetIpcSubscriber<bool>("Splatoon.IsLoaded").InvokeFunc())
                {
                    Connect();
                }
            }
            catch (Exception e)
            {
                //e.Log();
            }
            Svc.PluginInterface.GetIpcSubscriber<bool>("Splatoon.Loaded").Subscribe(Connect);
            Svc.PluginInterface.GetIpcSubscriber<bool>("Splatoon.Unloaded").Subscribe(Reset);
        }

        internal static void Shutdown()
        {
            Svc.PluginInterface.GetIpcSubscriber<bool>("Splatoon.Loaded").Unsubscribe(Connect);
            Svc.PluginInterface.GetIpcSubscriber<bool>("Splatoon.Unloaded").Unsubscribe(Reset);
        }

        internal static void Reset()
        {
            Instance = null;
            PluginLog.Information("Disconnected from Splatoon");
        }

        static void Connect()
        {
            try
            {
                if (DalamudReflector.TryGetDalamudPlugin("Splatoon", out var plugin, false, true) && (bool)plugin.GetType().GetField("Init").GetValue(plugin))
                {
                    Instance = plugin;
                    Version++;
                    OnConnect?.Invoke();
                    PluginLog.Information("Successfully connected to Splatoon.");
                }
                else
                {
                    throw new Exception("Splatoon is not initialized");
                }
            }
            catch (Exception e)
            {
                PluginLog.Error("Can't find Splatoon plugin: " + e.Message);
                PluginLog.Error(e.StackTrace);
            }
        }

        public static bool IsConnected()
        {
            return Instance != null;
        }

        public static bool AddDynamicElement(string name, Element e, long[] DestroyCondition)
        {
            return AddDynamicElements(name, new Element[] { e }, DestroyCondition);
        }

        public static bool AddDynamicElement(string name, Element e, long DestroyCondition)
        {
            return AddDynamicElements(name, new Element[] { e }, new long[] { DestroyCondition });
        }

        public static bool AddDynamicElements(string name, Element[] e, long DestroyCondition)
        {
            return AddDynamicElements(name, e, new long[] { DestroyCondition });
        }

        public static bool AddDynamicElements(string name, Element[] e, long[] DestroyCondition)
        {
            if (!IsConnected())
            {
                PluginLog.Warning("Not connected to Splatoon");
                return false;
            }
            if (!e.All(x => x.IsValid()))
            {
                PluginLog.Warning("Elements are no longer valid");
                return false;
            }
            if (e.Length == 0)
            {
                PluginLog.Warning("There are no elements");
                return false;
            }
            try
            {
                var array = Array.CreateInstance(e[0].Instance.GetType(), e.Length);
                for (var i = 0; i < e.Length; i++)
                {
                    array.SetValue(e[i].Instance, i);
                }
                Instance.GetType().GetMethod("AddDynamicElements").Invoke(Instance, new object[] { name, array, DestroyCondition });
                return true;
            }
            catch (Exception ex)
            {
                ex.Log();
                return false;
            }
        }

        public static bool DisplayOnce(Element e)
        {
            if (!IsConnected())
            {
                PluginLog.Warning("Not connected to Splatoon");
                return false;
            }
            if (!e.IsValid())
            {
                PluginLog.Warning("Elements are no longer valid");
                return false;
            }
            try
            {
                Instance.GetType().GetMethod("InjectElement").Invoke(Instance, new object[] { e.Instance });
                return true;
            }
            catch (Exception ex)
            {
                ex.Log();
                return false;
            }
        }

        public static bool RemoveDynamicElements(string name)
        {
            if (!IsConnected())
            {
                PluginLog.Warning("Not connected to Splatoon");
                return false;
            }
            try
            {
                Instance.GetType().GetMethod("RemoveDynamicElements").Invoke(Instance, new object[] { name });
                return true;
            }
            catch (Exception ex)
            {
                ex.Log();
                return false;
            }
        }
    }
}
