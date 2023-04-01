using Dalamud.Logging;
using ECommons.DalamudServices;
using ECommons.Reflection;

namespace Artisan.IPC;

internal static class AutoRetainer
{
    internal static bool IsEnabled()
    {
        if (DalamudReflector.TryGetDalamudPlugin("AutoRetainer", out var pl, false, true))
        {
            return Svc.PluginInterface.GetIpcSubscriber<bool>("AutoRetainer.GetSuppressed").InvokeFunc();
        }
        return false;
    }

    internal static void Suppress()
    {
        if (DalamudReflector.TryGetDalamudPlugin("AutoRetainer", out var pl, false, true))
        {
            Svc.PluginInterface.GetIpcSubscriber<bool, object>("AutoRetainer.SetSuppressed").InvokeAction(true);
        }
    }

    internal static void Unsuppress()
    {
        if (DalamudReflector.TryGetDalamudPlugin("AutoRetainer", out var pl, false, true))
        {
            Svc.PluginInterface.GetIpcSubscriber<bool, object>("AutoRetainer.SetSuppressed").InvokeAction(false);
        }
    }
}
