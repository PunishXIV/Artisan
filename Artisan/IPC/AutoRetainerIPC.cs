using ECommons.DalamudServices;
using ECommons.Reflection;
using System;

namespace Artisan.IPC;

internal class AutoRetainerIPC : IDisposable
{
    internal static bool ReEnable = false;

    public AutoRetainerIPC()
    {
       
    }

    internal static bool IsEnabled()
    {
        if (DalamudReflector.TryGetDalamudPlugin("AutoRetainer", out var pl, false, true))
        {
            ReEnable = Svc.PluginInterface.GetIpcSubscriber<bool>("AutoRetainer.GetSuppressed").InvokeFunc();
            return ReEnable;
        }
        return false;
    }

    internal static void Suppress()
    {
        if (IsEnabled() && DalamudReflector.TryGetDalamudPlugin("AutoRetainer", out var pl, false, true))
        {
            Svc.PluginInterface.GetIpcSubscriber<bool, object>("AutoRetainer.SetSuppressed").InvokeAction(true);
        }
    }

    internal static void Unsuppress()
    {
        if (ReEnable && DalamudReflector.TryGetDalamudPlugin("AutoRetainer", out var pl, false, true))
        {
            Svc.PluginInterface.GetIpcSubscriber<bool, object>("AutoRetainer.SetSuppressed").InvokeAction(false);
            ReEnable = false;
        }
    }

    public void Dispose()
    {
        
    }
}
