using Artisan.CraftingLists;
using Artisan.RawInformation;
using Artisan.Tasks;
using ClickLib.Clicks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Ipc;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ExcelServices.TerritoryEnumeration;
using ECommons.Reflection;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
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

        public static TaskManager TM = new TaskManager();
        internal static bool GenericThrottle => EzThrottler.Throttle("RetainerInfoThrottler", 100);
        internal static void RethrottleGeneric(int num) => EzThrottler.Throttle("RetainerInfoThrottler", num, true);
        internal static void RethrottleGeneric() => EzThrottler.Throttle("RetainerInfoThrottler", 100, true);
        internal static Tasks.RetainerManager retainerManager = new(Svc.SigScanner);

        public static bool ATools
        {
            get
            {
                try
                {
                    return !P.Config.DisableAllaganTools && (DalamudReflector.TryGetDalamudPlugin("Allagan Tools", out var at, false, true) || DalamudReflector.TryGetDalamudPlugin("InventoryTools", out var it, false, true)) && _IsInitialized != null && _IsInitialized.InvokeFunc();
                }
                catch
                {
                    return false;
                }
            }
        }

        private static uint firstFoundQuantity = 0;

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

        private static void LogoutCacheClear()
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
                foreach (var recipe in CraftingListHelpers.FilteredList.Values)
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
                ClearCache(null);
            }
        }

        private static void OnItemRemoved((uint, InventoryItem.ItemFlags, ulong, uint) tuple)
        {
            if (Svc.Condition[ConditionFlag.OccupiedSummoningBell])
            {
                ClearCache(null);
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
            public uint ItemID { get; set; }

            public uint Quantity { get; set; }

            public uint HQQuantity { get; set; }

            public ItemInfo(uint itemId, uint quantity, uint hqQuantity)
            {
                ItemID = itemId;
                Quantity = quantity;
                HQQuantity = hqQuantity;
            }
        }

        public static void ClearCache(ulong? RetainerId)
        {
            RetainerData.Each(x => x.Value.Clear());
        }

        public static unsafe uint GetRetainerInventoryItem(uint itemID, ulong retainerId, bool hqonly = false)
        {
            if (ATools)
            {
                if (!hqonly)
                {
                    return _ItemCount.InvokeFunc(itemID, retainerId, 10000) +
                            _ItemCount.InvokeFunc(itemID, retainerId, 10001) +
                            _ItemCount.InvokeFunc(itemID, retainerId, 10002) +
                            _ItemCount.InvokeFunc(itemID, retainerId, 10003) +
                            _ItemCount.InvokeFunc(itemID, retainerId, 10004) +
                            _ItemCount.InvokeFunc(itemID, retainerId, 10005) +
                            _ItemCount.InvokeFunc(itemID, retainerId, 10006) +
                            _ItemCount.InvokeFunc(itemID, retainerId, (uint)InventoryType.RetainerCrystals);
                }
                else
                {
                    return _ItemCountHQ.InvokeFunc(itemID, retainerId, 10000) +
                            _ItemCountHQ.InvokeFunc(itemID, retainerId, 10001) +
                            _ItemCountHQ.InvokeFunc(itemID, retainerId, 10002) +
                            _ItemCountHQ.InvokeFunc(itemID, retainerId, 10003) +
                            _ItemCountHQ.InvokeFunc(itemID, retainerId, 10004) +
                            _ItemCountHQ.InvokeFunc(itemID, retainerId, 10005) +
                            _ItemCountHQ.InvokeFunc(itemID, retainerId, 10006);
                }
            }
            return 0;
        }
        public static unsafe int GetRetainerItemCount(uint itemId, bool tryCache = true, bool hqOnly = false)
        {

            if (ATools)
            {
                if (!Svc.ClientState.IsLoggedIn || Svc.Condition[ConditionFlag.OnFreeTrial]) return 0;

                try
                {
                    if (tryCache)
                    {
                        if (RetainerData.SelectMany(x => x.Value).Any(x => x.Key == itemId))
                        {
                            if (hqOnly)
                            {
                                return (int)RetainerData.Values.SelectMany(x => x.Values).Where(x => x.ItemID == itemId).Sum(x => x.HQQuantity);
                            }

                            return (int)RetainerData.Values.SelectMany(x => x.Values).Where(x => x.ItemID == itemId).Sum(x => x.Quantity);
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
                                retainerId = retainer->RetainerID;
                        }

                        if (retainer->RetainerID > 0 && !P.Config.RetainerIDs.Any(x => x.Key == retainer->RetainerID && x.Value == Svc.ClientState.LocalContentId))
                        {
                            if (retainer->Available)
                            {
                                P.Config.RetainerIDs.Add(retainer->RetainerID, Svc.ClientState.LocalContentId);
                                P.Config.Save();
                            }
                        }

                        if (!retainer->Available)
                        {
                            if (retainer->RetainerID > 0)
                                P.Config.UnavailableRetainerIDs.Add(retainer->RetainerID);
                            else
                                P.Config.UnavailableRetainerIDs.RemoveWhere(x => x == retainer->RetainerID);

                            P.Config.Save();
                        }

                        if (retainerId > 0 && !P.Config.UnavailableRetainerIDs.Any(x => x == retainerId))
                        {
                            if (RetainerData.ContainsKey(retainerId))
                            {
                                var ret = RetainerData[retainerId];
                                if (ret.ContainsKey(itemId))
                                {
                                    var item = ret[itemId];
                                    item.ItemID = itemId;
                                    item.Quantity = GetRetainerInventoryItem(itemId, retainerId);

                                }
                                else
                                {
                                    ret.TryAdd(itemId, new ItemInfo(itemId, GetRetainerInventoryItem(itemId, retainerId), GetRetainerInventoryItem(itemId, retainerId, true)));
                                }
                            }
                            else
                            {
                                RetainerData.TryAdd(retainerId, new Dictionary<uint, ItemInfo>());
                                var ret = RetainerData[retainerId];
                                if (ret.ContainsKey(itemId))
                                {
                                    var item = ret[itemId];
                                    item.ItemID = itemId;
                                    item.Quantity = GetRetainerInventoryItem(itemId, retainerId);

                                }
                                else
                                {
                                    ret.TryAdd(itemId, new ItemInfo(itemId, GetRetainerInventoryItem(itemId, retainerId), GetRetainerInventoryItem(itemId, retainerId, true)));

                                }
                            }
                        }
                    }

                    if (hqOnly)
                    {
                        return (int)RetainerData.Values.SelectMany(x => x.Values).Where(x => x.ItemID == itemId).Sum(x => x.HQQuantity);
                    }

                    return (int)RetainerData.SelectMany(x => x.Value).Where(x => x.Key == itemId).Sum(x => x.Value.Quantity);
                }
                catch (Exception ex)
                {
                    Svc.Log.Error(ex, "RetainerInfoItemCount");
                    return 0;
                }
            }

            return 0;
        }

        public static void RestockFromRetainers(uint itemId, int howManyToGet)
        {
            if (RetainerData.SelectMany(x => x.Value).Any(x => x.Value.ItemID == itemId && x.Value.Quantity > 0))
            {
                TM.Enqueue(() => Svc.Framework.Update += Tick);
                TM.Enqueue(() => AutoRetainer.Suppress());
                TM.EnqueueBell();
                TM.DelayNext("BellInteracted", 200);

                var retainerListSorted = RetainerData.Where(x => x.Value.Values.Any(y => y.ItemID == itemId && y.HQQuantity > 0)).ToDictionary(x => x.Key, x => x.Value);
                RetainerData.Where(x => x.Value.Values.Any(y => y.ItemID == itemId && y.Quantity > 0)).ToList().ForEach(x => retainerListSorted.TryAdd(x.Key, x.Value));

                foreach (var retainer in retainerListSorted)
                {
                    TM.Enqueue(() => RetainerListHandlers.SelectRetainerByID(retainer.Key), 5000, true, "SelectRetainer");
                    TM.DelayNext("WaitToSelectEntrust", 200);
                    TM.Enqueue(() => RetainerHandlers.SelectEntrustItems());
                    TM.DelayNext("EntrustSelected", 200);
                    TM.Enqueue(() =>
                    {
                        ExtractSingular(itemId, howManyToGet, retainer.Key);
                    }, "ExtractSingularEntry");

                    TM.DelayNext("CloseRetainer", 200);
                    TM.Enqueue(() => RetainerHandlers.CloseAgentRetainer());
                    TM.DelayNext("ClickQuit", 200);
                    TM.Enqueue(() => RetainerHandlers.SelectQuit());
                    TM.Enqueue(() =>
                    {
                        if (CraftingListUI.NumberOfIngredient(itemId) >= howManyToGet)
                        {
                            TM.DelayNextImmediate("CloseRetainerList", 200);
                            TM.EnqueueImmediate(() => RetainerListHandlers.CloseRetainerList());
                            TM.EnqueueImmediate(() => YesAlready.Unlock());
                            TM.EnqueueImmediate(() => AutoRetainer.Unsuppress());
                            TM.EnqueueImmediate(() => Svc.Framework.Update -= Tick);
                            TM.EnqueueImmediate(() => TM.Abort());
                        }
                    });
                }

                TM.DelayNext("CloseRetainerList", 200);
                TM.Enqueue(() => RetainerListHandlers.CloseRetainerList());
                TM.Enqueue(() => YesAlready.Unlock());
                TM.Enqueue(() => AutoRetainer.Unsuppress());
                TM.Enqueue(() => Svc.Framework.Update -= Tick);
            }
        }

        public static bool ExtractSingular(uint itemId, int howManyToGet, ulong retainerKey)
        {
            Svc.Log.Debug($"{howManyToGet}");
            if (howManyToGet != 0)
            {
                bool lookingForHQ = RetainerData[retainerKey].Values.Any(x => x.ItemID == itemId && x.HQQuantity > 0);
                TM.DelayNextImmediate("WaitOnRetainerInventory", 500);
                TM.EnqueueImmediate(() => RetainerHandlers.OpenItemContextMenu(itemId, lookingForHQ, out firstFoundQuantity), 300);
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
                            ExtractSingular(itemId, howManyToGet, retainerKey);
                        });
                        return true;
                    }
                    if (RetainerHandlers.InputNumericValue(value))
                    {
                        howManyToGet -= value;

                        TM.EnqueueImmediate(() =>
                        {
                            ExtractSingular(itemId, howManyToGet, retainerKey);
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

        public static void RestockFromRetainers(CraftingList list)
        {
            if (GetReachableRetainerBell() == null) return;

            Dictionary<int, int> requiredItems = new();
            Dictionary<uint, int> materialList = new();

            foreach (var item in list.Items)
            {
                var recipe = LuminaSheets.RecipeSheet[item];
                CraftingListHelpers.AddRecipeIngredientsToList(recipe, ref materialList, false);
            }

            foreach (var material in materialList.OrderByDescending(x => x.Key))
            {
                var invCount = CraftingListUI.NumberOfIngredient((uint)material.Key);
                if (invCount < material.Value)
                {
                    var diffcheck = material.Value - invCount;
                    requiredItems.Add((int)material.Key, diffcheck);
                }

                //Refresh retainer cache if empty
                GetRetainerItemCount((uint)material.Key);
            }

            if (RetainerData.SelectMany(x => x.Value).Any(x => requiredItems.Any(y => y.Key == x.Value.ItemID)))
            {
                TM.Enqueue(() => Svc.Framework.Update += Tick);
                TM.Enqueue(() => AutoRetainer.Suppress());
                TM.EnqueueBell();
                TM.DelayNext("BellInteracted", 200);

                foreach (var retainer in RetainerData)
                {
                    if (retainer.Value.Values.Any(x => requiredItems.Any(y => y.Value > 0 && y.Key == x.ItemID && x.Quantity > 0)))
                    {
                        TM.Enqueue(() => RetainerListHandlers.SelectRetainerByID(retainer.Key));
                        TM.DelayNext("WaitToSelectEntrust", 200);
                        TM.Enqueue(() => RetainerHandlers.SelectEntrustItems());
                        TM.DelayNext("EntrustSelected", 200);
                        foreach (var item in requiredItems)
                        {
                            if (retainer.Value.Values.Any(x => x.ItemID == item.Key && x.Quantity > 0))
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
                TM.Enqueue(() => AutoRetainer.Unsuppress());
                TM.Enqueue(() => Svc.Framework.Update -= Tick);
            }
        }

        private static unsafe void Tick(IFramework framework)
        {
            if (Svc.Condition[ConditionFlag.OccupiedSummoningBell])
            {
                if (TryGetAddonByName<AddonTalk>("Talk", out var addon) && addon->AtkUnitBase.IsVisible)
                {
                    ClickTalk.Using((IntPtr)addon).Click();
                }
            }
        }

        private static bool ExtractItem(Dictionary<int, int> requiredItems, KeyValuePair<int, int> item, ulong key)
        {
            if (requiredItems[item.Key] != 0)
            {
                bool lookingForHQ = RetainerData[key].Values.Any(x => x.ItemID == item.Key && x.HQQuantity > 0);
                TM.DelayNextImmediate("WaitOnRetainerInventory", 500);
                TM.EnqueueImmediate(() => RetainerHandlers.OpenItemContextMenu((uint)item.Key, lookingForHQ, out firstFoundQuantity), 300);
                TM.DelayNextImmediate("WaitOnNumericPopup", 200);
                TM.EnqueueImmediate(() =>
                {
                    var value = Math.Min(requiredItems[item.Key], (int)firstFoundQuantity);
                    if (value == 0) return true;
                    Svc.Log.Debug($"Min withdrawing: {value}, found {firstFoundQuantity}");
                    if (firstFoundQuantity == 1) { requiredItems[item.Key] -= (int)firstFoundQuantity; return true; }
                    if (RetainerHandlers.InputNumericValue(value))
                    {
                        requiredItems[item.Key] -= value;
                        Svc.Log.Debug($"{requiredItems[item.Key]}");

                        TM.EnqueueImmediate(() =>
                        {
                            ExtractItem(requiredItems, item, key);
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

        internal static GameObject? GetReachableRetainerBell()
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

        internal static float GetValidInteractionDistance(GameObject bell)
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

        public unsafe static bool IsTargetable(this GameObject o)
        {
            return o.Struct()->GetIsTargetable();
        }

        public unsafe static FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* Struct(this GameObject o)
        {
            return (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)o.Address;
        }
    }
}
