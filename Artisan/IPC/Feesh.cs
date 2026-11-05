using Artisan.RawInformation;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.IPC
{
    internal static class Feesh
    {
        private const string Owner = "Artisan";

        private static ICallGateSubscriber<string, bool, object>? _catchDone;

        internal static void Init()
        {
            _catchDone = Svc.PluginInterface.GetIpcSubscriber<string, bool, object>("WahTools.Feesh.CatchDone");
            _catchDone.Subscribe(OnCatchDone);
        }

        internal static void Dispose()
        {
            _catchDone?.Unsubscribe(OnCatchDone);
        }

        internal static bool IsEnabled() => DalamudReflector.TryGetDalamudPlugin("WahTools", out _, false, true);

        private static HashSet<uint>? _spearfishItemIds;
        
        internal static bool IsFish(uint itemId)
        {
            _spearfishItemIds ??= LuminaSheets.SpearfishingItemSheet?.Values.Select(x => x.Item.RowId).ToHashSet() ?? [];
            return LuminaSheets.FishingSpotItemIds?.Contains(itemId) == true || _spearfishItemIds.Contains(itemId);
        }

        internal static bool IsBusy()
        {
            if (!IsEnabled()) return false;
            return Svc.PluginInterface.GetIpcSubscriber<bool>("WahTools.IsBusy").InvokeFunc();
        }
        
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
        
        internal static bool Catch(uint itemId, int count)
        {
            if (!IsEnabled()) return false;
            return Svc.PluginInterface.GetIpcSubscriber<string, uint, int, bool, bool>("WahTools.Feesh.Catch").InvokeFunc(Owner, itemId, count, true);
        }

        internal static bool CatchList((uint ItemId, int Count)[] items)
        {
            if (!IsEnabled() || items.Length == 0) return false;
            return Svc.PluginInterface.GetIpcSubscriber<string, uint[], int[], bool, bool>("WahTools.Feesh.CatchList")
                .InvokeFunc(Owner, items.Select(x => x.ItemId).ToArray(), items.Select(x => x.Count).ToArray(), true);
        }

        internal static bool CancelCatch()
        {
            if (!IsEnabled()) return false;
            return Svc.PluginInterface.GetIpcSubscriber<string, bool>("WahTools.Feesh.CancelCatch").InvokeFunc(Owner);
        }

        internal static void ReleaseAll()
        {
            if (!IsEnabled()) return;

            try
            {
                Svc.PluginInterface.GetIpcSubscriber<string, bool>("WahTools.Feesh.ReleaseAll").InvokeFunc(Owner);
            }
            catch
            {
            }
        }

        internal static void Stop()
        {
            if (!IsEnabled()) return;
            Svc.PluginInterface.GetIpcSubscriber<bool>("WahTools.Feesh.Stop").InvokeFunc();
        }

        private static void OnCatchDone(string owner, bool done)
        {
            if (owner != Owner) return;

            if (done)
                Notify.Success("Feesh caught all the fish.");
            else
                Notify.Error("Feesh stopped before catching all the fish.");
        }
    }
}
