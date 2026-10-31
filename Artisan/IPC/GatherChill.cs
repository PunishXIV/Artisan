using ECommons.DalamudServices;
using ECommons.Reflection;

namespace Artisan.IPC
{
    internal static class GatherChill
    {
        internal static int ApiVersion()
        {
            if (DalamudReflector.TryGetDalamudPlugin("GatherChill", out _, false, true))
            {
                try
                {
                    return Svc.PluginInterface.GetIpcSubscriber<int>("GatherChill.ApiVersion").InvokeFunc();
                }
                catch
                {
                    return 0;
                }
            }

            return 0;
        }

        internal static bool IsEnabled() => ApiVersion() >= 1;

        // GatherItem and Stop were added in v2
        internal static bool CanGatherDirectly() => ApiVersion() >= 2;

        internal static bool IsBusy()
        {
            if (!IsEnabled()) return false;
            return Svc.PluginInterface.GetIpcSubscriber<bool>("GatherChill.IsBusy").InvokeFunc();
        }

        internal static bool CanGather(uint itemId)
        {
            if (!IsEnabled()) return false;
            return Svc.PluginInterface.GetIpcSubscriber<uint, bool>("GatherChill.CanGather").InvokeFunc(itemId);
        }

        // Amount is how many more to gather; items already on the list keep the larger amount
        internal static int AddToGatherList((uint ItemId, int Amount)[] items)
        {
            if (!IsEnabled()) return 0;
            return Svc.PluginInterface.GetIpcSubscriber<(uint ItemId, int Amount)[], int>("GatherChill.AddToGatherList").InvokeFunc(items);
        }

        internal static bool GatherItem(uint itemId, int amount)
        {
            if (!CanGatherDirectly()) return false;
            return Svc.PluginInterface.GetIpcSubscriber<uint, int, bool>("GatherChill.GatherItem").InvokeFunc(itemId, amount);
        }

        internal static void Stop()
        {
            if (!CanGatherDirectly()) return;
            Svc.PluginInterface.GetIpcSubscriber<object>("GatherChill.Stop").InvokeAction();
        }
    }
}
