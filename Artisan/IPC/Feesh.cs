using ECommons.DalamudServices;
using ECommons.Reflection;

namespace Artisan.IPC
{
    // Feesh is the fishing module of WahTools
    internal static class Feesh
    {
        internal static bool IsEnabled() => DalamudReflector.TryGetDalamudPlugin("WahTools", out _, false, true);

        internal static bool IsBusy()
        {
            if (!IsEnabled()) return false;
            return Svc.PluginInterface.GetIpcSubscriber<bool>("WahTools.IsBusy").InvokeFunc();
        }

        // Feesh has no "can fish" call; TaggedStand is empty when it knows no fishing hole for the fish
        internal static bool CanFish(uint itemId)
        {
            if (!IsEnabled()) return false;

            try
            {
                return !string.IsNullOrEmpty(Svc.PluginInterface.GetIpcSubscriber<uint, string>("WahTools.Feesh.TaggedStand").InvokeFunc(itemId));
            }
            catch
            {
                return false;
            }
        }

        internal static bool FishFor(uint itemId)
        {
            if (!IsEnabled()) return false;
            return Svc.PluginInterface.GetIpcSubscriber<uint, bool>("WahTools.Feesh.FishFor").InvokeFunc(itemId);
        }

        internal static void Stop()
        {
            if (!IsEnabled()) return;
            Svc.PluginInterface.GetIpcSubscriber<bool>("WahTools.Feesh.Stop").InvokeFunc();
        }
    }
}
