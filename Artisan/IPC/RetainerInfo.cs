using Artisan.CraftingLists;
using Artisan.RawInformation;
using Artisan.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Ipc;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.ExcelServices.TerritoryEnumeration;
using ECommons.Reflection;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using static ECommons.GenericHelpers;
using RetainerManager = FFXIVClientStructs.FFXIV.Client.Game.RetainerManager;

namespace Artisan.IPC
{
    public static class RetainerInfo
    {
        private static ICallGateSubscriber<ulong?, bool>? _OnRetainerChanged;
        private static ICallGateSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool>? _OnItemAdded;
        private static ICallGateSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool>? _OnItemRemoved;
        private static ICallGateSubscriber<uint, ulong, uint, uint>? _ItemCount;
        private static ICallGateSubscriber<uint, ulong, uint, uint>? _ItemCountHQ;
        private static ICallGateSubscriber<bool, bool>? _Initialized;
        private static ICallGateSubscriber<bool>? _IsInitialized;
        private static bool _InventoryChanged;

        public static TaskManager TM = new TaskManager();
        internal static bool GenericThrottle => EzThrottler.Throttle("RetainerInfoThrottler", 100);
        internal static void RethrottleGeneric(int num) => EzThrottler.Throttle("RetainerInfoThrottler", num, true);
        internal static void RethrottleGeneric() => EzThrottler.Throttle("RetainerInfoThrottler", 100, true);
        internal static Tasks.RetainerManager retainerManager = new(Svc.SigScanner);

        public static bool AToolsInstalled
        {
            get
            {
                return Svc.PluginInterface.InstalledPlugins.Any(x => x.InternalName is "Allagan Tools" or "InventoryTools");
            }
        }

        public static bool AToolsEnabled
        {
            get
            {
                return AToolsInstalled && (DalamudReflector.TryGetDalamudPlugin("Allagan Tools", out var at, false, true) || DalamudReflector.TryGetDalamudPlugin("InventoryTools", out var it, false, true)) && _IsInitialized != null && _IsInitialized.InvokeFunc();
            }
        }

        public static bool ATools
        {
            get
            {
                try
                {
                    return !P.Config.DisableAllaganTools && AToolsEnabled;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static int firstFoundQuantity = 0;

        public static bool CacheBuilt = ATools ? false : true;
        public static CancellationTokenSource CTSource = new();
        public static readonly object _lockObj = new();

        internal static void Init()
        {
            _Initialized = Svc.PluginInterface.GetIpcSubscriber<bool, bool>("AllaganTools.Initialized");
            _IsInitialized = Svc.PluginInterface.GetIpcSubscriber<bool>("AllaganTools.IsInitialized");
            _Initialized.Subscribe(SetupIPC);
            Svc.ClientState.Logout += LogoutCacheClear;
            SetupIPC(true);
        }

        private static void LogoutCacheClear(int t, int c)
        {
            RetainerData.Clear();
        }

        private static void SetupIPC(bool obj)
        {

            _OnRetainerChanged = Svc.PluginInterface.GetIpcSubscriber<ulong?, bool>("AllaganTools.RetainerChanged");
            _OnItemAdded = Svc.PluginInterface.GetIpcSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool>("AllaganTools.ItemAdded");
            _OnItemRemoved = Svc.PluginInterface.GetIpcSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool>("AllaganTools.ItemRemoved");

            _ItemCount = Svc.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCount");
            _ItemCountHQ = Svc.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCountHQ");
            _OnItemAdded.Subscribe(OnItemAdded);
            _OnItemRemoved.Subscribe(OnItemRemoved);
            TM.TimeoutSilently = true;
        }

        public async static Task<bool?> LoadCache(bool onLoad = false)
        {
            if (onLoad)
            {
                CraftingListUI.CraftableItems.Clear();
                RetainerData.Clear();
            }

            CacheBuilt = false;
            CraftingListUI.CraftableItems.Clear();

            if (P.Config.ShowOnlyCraftable || onLoad)
            {
                foreach (var recipe in LuminaSheets.RecipeSheet.Values)
                {
                    if (ATools && P.Config.ShowOnlyCraftableRetainers || onLoad)
                        await Task.Run(() => Safe(() => CraftingListUI.CheckForIngredients(recipe, false, true)));
                    else
                        await Task.Run(() => Safe(() => CraftingListUI.CheckForIngredients(recipe, false, false)));
                }
            }

            ClearCache(null);
            CacheBuilt = true;
            return true;
        }

        private static void OnItemAdded((uint, InventoryItem.ItemFlags, ulong, uint) tuple)
        {
            if (Svc.Condition[ConditionFlag.OccupiedSummoningBell])
            {
                Svc.Log.Debug($"Item Added: Clearing cache");
                ClearCache(null);
                _InventoryChanged = true;
            }
        }

        private static void OnItemRemoved((uint, InventoryItem.ItemFlags, ulong, uint) tuple)
        {
            if (Svc.Condition[ConditionFlag.OccupiedSummoningBell])
            {
                Svc.Log.Debug($"Item Removed: Clearing cache");
                ClearCache(null);
                _InventoryChanged = true;
            }
        }

        internal static void Dispose()
        {
            _Initialized?.Unsubscribe(SetupIPC);
            _OnItemAdded?.Unsubscribe(OnItemAdded);
            _OnItemRemoved?.Unsubscribe(OnItemRemoved);
            Svc.ClientState.Logout -= LogoutCacheClear;
            _Initialized = null;
            _IsInitialized = null;
            _OnRetainerChanged = null;
            _OnItemAdded = null;
            _OnItemRemoved = null;
            _ItemCount = null;
        }

        public static Dictionary<ulong, Dictionary<uint, ItemInfo>> RetainerData = new Dictionary<ulong, Dictionary<uint, ItemInfo>>();
        public class ItemInfo
        {
            public uint ItemId { get; set; }

            public uint Quantity { get; set; }

            public uint HQQuantity { get; set; }

            public ItemInfo(uint itemId, uint quantity, uint hqQuantity)
            {
                ItemId = itemId;
                Quantity = quantity;
                HQQuantity = hqQuantity;
            }
        }

        public static void ClearCache(ulong? RetainerId)
        {
            RetainerData.Each(x => x.Value.Clear());
        }

        public static unsafe uint GetRetainerInventoryItem(uint ItemId, ulong retainerId, bool hqonly = false)
        {
            if (ATools)
            {
                if (!hqonly)
                {
                    return _ItemCount.InvokeFunc(ItemId, retainerId, 10000) +
                            _ItemCount.InvokeFunc(ItemId, retainerId, 10001) +
                            _ItemCount.InvokeFunc(ItemId, retainerId, 10002) +
                            _ItemCount.InvokeFunc(ItemId, retainerId, 10003) +
                            _ItemCount.InvokeFunc(ItemId, retainerId, 10004) +
                            _ItemCount.InvokeFunc(ItemId, retainerId, 10005) +
                            _ItemCount.InvokeFunc(ItemId, retainerId, 10006) +
                            _ItemCount.InvokeFunc(ItemId, retainerId, (uint)InventoryType.RetainerCrystals);
                }
                else
                {
                    return _ItemCountHQ.InvokeFunc(ItemId, retainerId, 10000) +
                            _ItemCountHQ.InvokeFunc(ItemId, retainerId, 10001) +
                            _ItemCountHQ.InvokeFunc(ItemId, retainerId, 10002) +
                            _ItemCountHQ.InvokeFunc(ItemId, retainerId, 10003) +
                            _ItemCountHQ.InvokeFunc(ItemId, retainerId, 10004) +
                            _ItemCountHQ.InvokeFunc(ItemId, retainerId, 10005) +
                            _ItemCountHQ.InvokeFunc(ItemId, retainerId, 10006);
                }
            }
            return 0;
        }
        public static unsafe int GetRetainerItemCount(uint ItemId, bool tryCache = true, bool hqOnly = false)
        {

            if (ATools)
            {
                if (!Svc.ClientState.IsLoggedIn || Svc.Condition[ConditionFlag.OnFreeTrial]) return 0;

                try
                {
                    if (tryCache)
                    {
                        if (RetainerData.SelectMany(x => x.Value).Any(x => x.Key == ItemId))
                        {
                            if (hqOnly)
                            {
                                return (int)RetainerData.Values.SelectMany(x => x.Values).Where(x => x.ItemId == ItemId).Sum(x => x.HQQuantity);
                            }

                            return (int)RetainerData.Values.SelectMany(x => x.Values).Where(x => x.ItemId == ItemId).Sum(x => x.Quantity);
                        }
                    }

                    for (int i = 0; i < 10; i++)
                    {
                        ulong retainerId = 0;
                        var retainer = RetainerManager.Instance()->GetRetainerBySortedIndex((uint)i);

                        if (P.Config.RetainerIDs.Count(x => x.Value == Svc.ClientState.LocalContentId) > i)
                        {
                            retainerId = P.Config.RetainerIDs.Where(x => x.Value == Svc.ClientState.LocalContentId).Select(x => x.Key).ToArray()[i];
                        }
                        else
                        {
                            if (retainer->Available)
                                retainerId = retainer->RetainerId;
                        }

                        if (retainer->RetainerId > 0 && !P.Config.RetainerIDs.Any(x => x.Key == retainer->RetainerId && x.Value == Svc.ClientState.LocalContentId))
                        {
                            if (retainer->Available)
                            {
                                P.Config.RetainerIDs.Add(retainer->RetainerId, Svc.ClientState.LocalContentId);
                                P.Config.Save();
                            }
                        }

                        if (!retainer->Available)
                        {
                            if (retainer->RetainerId > 0 && !P.Config.UnavailableRetainerIDs.Contains(retainer->RetainerId))
                            {
                                P.Config.UnavailableRetainerIDs.Add(retainer->RetainerId);
                                P.Config.Save();
                            }
                        }
                        else
                        {
                            if (P.Config.UnavailableRetainerIDs.Contains(retainer->RetainerId))
                            {
                                P.Config.UnavailableRetainerIDs.RemoveWhere(x => x == retainer->RetainerId);
                                P.Config.Save();
                            }
                        }

                        if (retainerId > 0 && !P.Config.UnavailableRetainerIDs.Any(x => x == retainerId))
                        {
                            if (RetainerData.ContainsKey(retainerId))
                            {
                                var ret = RetainerData[retainerId];
                                if (ret.ContainsKey(ItemId))
                                {
                                    var item = ret[ItemId];
                                    item.ItemId = ItemId;
                                    item.Quantity = GetRetainerInventoryItem(ItemId, retainerId);

                                }
                                else
                                {
                                    ret.TryAdd(ItemId, new ItemInfo(ItemId, GetRetainerInventoryItem(ItemId, retainerId), GetRetainerInventoryItem(ItemId, retainerId, true)));
                                }
                            }
                            else
                            {
                                RetainerData.TryAdd(retainerId, new Dictionary<uint, ItemInfo>());
                                var ret = RetainerData[retainerId];
                                if (ret.ContainsKey(ItemId))
                                {
                                    var item = ret[ItemId];
                                    item.ItemId = ItemId;
                                    item.Quantity = GetRetainerInventoryItem(ItemId, retainerId);

                                }
                                else
                                {
                                    ret.TryAdd(ItemId, new ItemInfo(ItemId, GetRetainerInventoryItem(ItemId, retainerId), GetRetainerInventoryItem(ItemId, retainerId, true)));
                                }
                            }
                        }
                    }

                    if (hqOnly)
                    {
                        return (int)RetainerData.Values.SelectMany(x => x.Values).Where(x => x.ItemId == ItemId).Sum(x => x.HQQuantity);
                    }

                    return (int)RetainerData.SelectMany(x => x.Value).Where(x => x.Key == ItemId).Sum(x => x.Value.Quantity);
                }
                catch (Exception ex)
                {
                    //Svc.Log.Error(ex, "RetainerInfoItemCount");
                    return 0;
                }
            }

            return 0;
        }

        public static void RestockFromRetainers(uint ItemId, int howManyToGet)
        {
            if (RetainerData.SelectMany(x => x.Value).Any(x => x.Value.ItemId == ItemId && x.Value.Quantity > 0))
            {
                TM.Enqueue(() => Svc.Framework.Update += Tick);
                TM.Enqueue(() => AutoRetainerIPC.Suppress());
                TM.EnqueueBell();
                TM.DelayNext("BellInteracted", 200);

                var retainerListSorted = RetainerData.Where(x => x.Value.Values.Any(y => y.ItemId == ItemId && y.HQQuantity > 0)).ToDictionary(x => x.Key, x => x.Value);
                RetainerData.Where(x => x.Value.Values.Any(y => y.ItemId == ItemId && y.Quantity > 0)).ToList().ForEach(x => retainerListSorted.TryAdd(x.Key, x.Value));

                foreach (var retainer in retainerListSorted)
                {
                    TM.Enqueue(() => RetainerListHandlers.SelectRetainerByID(retainer.Key), 5000, true, "SelectRetainer");
                    TM.DelayNext("WaitToSelectEntrust", 200);
                    TM.Enqueue(() => RetainerHandlers.SelectEntrustItems());
                    TM.DelayNext("EntrustSelected", 200);
                    TM.Enqueue(() =>
                    {
                        ExtractSingular(ItemId, howManyToGet, retainer.Key);
                    }, "ExtractSingularEntry");

                    TM.DelayNext("CloseRetainer", 200);
                    TM.Enqueue(() => RetainerHandlers.CloseAgentRetainer());
                    TM.DelayNext("ClickQuit", 200);
                    TM.Enqueue(() => RetainerHandlers.SelectQuit());
                    TM.Enqueue(() =>
                    {
                        if (CraftingListUI.NumberOfIngredient(ItemId) >= howManyToGet)
                        {
                            TM.DelayNextImmediate("CloseRetainerList", 200);
                            TM.EnqueueImmediate(() => RetainerListHandlers.CloseRetainerList());
                            TM.EnqueueImmediate(() => YesAlready.Unlock());
                            TM.EnqueueImmediate(() => AutoRetainerIPC.Unsuppress());
                            TM.EnqueueImmediate(() => Svc.Framework.Update -= Tick);
                            TM.EnqueueImmediate(() => TM.Abort());
                        }
                    });
                }

                TM.DelayNext("CloseRetainerList", 200);
                TM.Enqueue(() => RetainerListHandlers.CloseRetainerList());
                TM.Enqueue(() => YesAlready.Unlock());
                TM.Enqueue(() => AutoRetainerIPC.Unsuppress());
                TM.Enqueue(() => Svc.Framework.Update -= Tick);
            }
        }

        public static bool ExtractSingular(uint ItemId, int howManyToGet, ulong retainerKey)
        {
            Svc.Log.Debug($"{howManyToGet}");
            if (howManyToGet != 0)
            {
                bool lookingForHQ = RetainerData[retainerKey].Values.Any(x => x.ItemId == ItemId && x.HQQuantity > 0);
                TM.DelayNextImmediate("WaitOnRetainerInventory", 500);
                TM.EnqueueImmediate(() => RetainerHandlers.OpenItemContextMenu(ItemId, lookingForHQ, out firstFoundQuantity), 300);
                TM.DelayNextImmediate("WaitOnNumericPopup", 200);
                TM.EnqueueImmediate(() =>
                {
                    var value = Math.Min(howManyToGet, (int)firstFoundQuantity);
                    if (value == 0) return true;
                    Svc.Log.Debug($"Min withdrawing: {value}, found {firstFoundQuantity}");
                    if (firstFoundQuantity == 1)
                    {
                        howManyToGet -= (int)firstFoundQuantity;
                        TM.EnqueueImmediate(() =>
                        {
                            ExtractSingular(ItemId, howManyToGet, retainerKey);
                        });
                        return true;
                    }
                    if (RetainerHandlers.InputNumericValue(value))
                    {
                        howManyToGet -= value;

                        TM.EnqueueImmediate(() =>
                        {
                            ExtractSingular(ItemId, howManyToGet, retainerKey);
                        });
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }, 1000);
            }

            return true;
        }

        public static void RestockFromRetainers(NewCraftingList list)
        {
            if (GetReachableRetainerBell() == null) return;

            Dictionary<int, int> requiredItems = new();
            Dictionary<uint, int> materialList = new();

            Svc.Log.Debug($"Making material list");

            materialList = list.ListMaterials();

            Svc.Log.Debug($"Creating Fetch List");

            foreach (var material in materialList.OrderByDescending(x => x.Key))
            {
                Svc.Log.Debug($"{material}");
                var invCount = CraftingListUI.NumberOfIngredient(material.Key);
                if (invCount < material.Value)
                {
                    var diffcheck = material.Value - invCount;
                    Svc.Log.Debug($"{material.Key} {diffcheck}");
                    requiredItems.Add((int)material.Key, diffcheck);
                }

                //Refresh retainer cache if empty
                GetRetainerItemCount(material.Key);
            }

            if (RetainerData.SelectMany(x => x.Value).Any(x => requiredItems.Any(y => y.Key == x.Value.ItemId)))
            {
                Svc.Log.Debug($"Processing Retainer Data");
                TM.Enqueue(() => Svc.Framework.Update += Tick);
                TM.Enqueue(() => AutoRetainerIPC.Suppress());
                TM.EnqueueBell();
                TM.DelayNext("BellInteracted", 200);

                foreach (var retainer in RetainerData)
                {
                    if (retainer.Value.Values.Any(x => requiredItems.Any(y => y.Value > 0 && y.Key == x.ItemId && x.Quantity > 0)))
                    {
                        TM.Enqueue(() => RetainerListHandlers.SelectRetainerByID(retainer.Key));
                        TM.DelayNext("WaitToSelectEntrust", 200);
                        TM.Enqueue(() => RetainerHandlers.SelectEntrustItems());
                        TM.DelayNext("EntrustSelected", 200);
                        foreach (var item in requiredItems)
                        {
                            if (retainer.Value.Values.Any(x => x.ItemId == item.Key && x.Quantity > 0))
                            {
                                TM.DelayNext("SwitchItems", 200);
                                TM.Enqueue(() =>
                                {
                                    ExtractItem(requiredItems, item, retainer.Key);
                                });
                            }
                        }
                        TM.DelayNext("CloseRetainer", 200);
                        TM.Enqueue(() => RetainerHandlers.CloseAgentRetainer());
                        TM.DelayNext("ClickQuit", 200);
                        TM.Enqueue(() => RetainerHandlers.SelectQuit());
                    }
                }
                TM.DelayNext("CloseRetainerList", 200);
                TM.Enqueue(() => RetainerListHandlers.CloseRetainerList());
                TM.Enqueue(() => YesAlready.Unlock());
                TM.Enqueue(() => AutoRetainerIPC.Unsuppress());
                TM.Enqueue(() => Svc.Framework.Update -= Tick);
            }
        }

        private static unsafe void Tick(IFramework framework)
        {
            if (Svc.Condition[ConditionFlag.OccupiedSummoningBell])
            {
                if (TryGetAddonByName<AddonTalk>("Talk", out var addon) && addon->AtkUnitBase.IsVisible)
                {
                    new AddonMaster.Talk((IntPtr)addon).Click();
                }
            }
        }

        private static bool ExtractItem(Dictionary<int, int> requiredItems, KeyValuePair<int, int> item, ulong key)
        {
            if (requiredItems[item.Key] != 0)
            {
                _InventoryChanged = false;
                TM.EnqueueImmediate(() => GetRetainerItemCount((uint)item.Key));
                bool lookingForHQ = RetainerData[key].Values.Any(x => x.ItemId == item.Key && x.HQQuantity > 0);
                Svc.Log.Debug($"HQ?: {lookingForHQ}");
                TM.DelayNextImmediate("WaitOnRetainerInventory", 500);
                TM.EnqueueImmediate(() => RetainerHandlers.OpenItemContextMenu((uint)item.Key, lookingForHQ, out firstFoundQuantity), 300);
                TM.DelayNextImmediate("WaitOnNumericPopup", 200);
                TM.EnqueueImmediate(() =>
                {
                    var value = Math.Min(requiredItems[item.Key], (int)firstFoundQuantity);
                    if (value == 0) return true;
                    Svc.Log.Debug($"Min withdrawing: {value}, found {firstFoundQuantity}/{requiredItems[item.Key]}");
                    if (firstFoundQuantity == 1) { requiredItems[item.Key] -= (int)firstFoundQuantity; return true; }
                    if (RetainerHandlers.InputNumericValue(value))
                    {
                        requiredItems[item.Key] -= value;
                        TM.EnqueueImmediate(() => _InventoryChanged);
                        TM.EnqueueImmediate(() =>
                        {
                            ExtractItem(requiredItems, item, key);
                        }, "RecursiveExtract");
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }, 1000);
            }

            return true;
        }

        internal static IGameObject? GetReachableRetainerBell()
        {
            foreach (var x in Svc.Objects)
            {
                if ((x.ObjectKind == ObjectKind.Housing || x.ObjectKind == ObjectKind.EventObj) && x.Name.ToString().EqualsIgnoreCaseAny(BellName, "リテイナーベル"))
                {
                    if (Vector3.Distance(x.Position, Svc.ClientState.LocalPlayer.Position) < GetValidInteractionDistance(x) && x.IsTargetable())
                    {
                        return x;
                    }
                }
            }
            return null;
        }

        internal static float GetValidInteractionDistance(IGameObject bell)
        {
            if (bell.ObjectKind == ObjectKind.Housing)
            {
                return 6.5f;
            }
            else if (Inns.List.Contains(Svc.ClientState.TerritoryType))
            {
                return 4.75f;
            }
            else
            {
                return 4.6f;
            }
        }

        internal static string BellName
        {
            get => Svc.Data.GetExcelSheet<EObjName>().GetRow(2000401).Singular.ToString();
        }

        public unsafe static bool IsTargetable(this IGameObject o)
        {
            return o.Struct()->GetIsTargetable();
        }

        public unsafe static FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* Struct(this IGameObject o)
        {
            return (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)o.Address;
        }
    }
}
