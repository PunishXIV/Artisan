using Artisan.CraftingLists;
using Artisan.RawInformation;
using Artisan.Tasks;
using ClickLib.Clicks;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
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
                    return !Service.Configuration.DisableAllaganTools && DalamudReflector.TryGetDalamudPlugin("Allagan Tools", out var it, false, true) && _IsInitialized != null && _IsInitialized.InvokeFunc();
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
            SetupIPC(true);
        }

        private static void SetupIPC(bool obj)
        {

            _OnRetainerChanged = Svc.PluginInterface.GetIpcSubscriber<ulong?, bool>("AllaganTools.RetainerChanged");
            _OnItemAdded = Svc.PluginInterface.GetIpcSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool>("AllaganTools.ItemAdded");
            _OnItemRemoved = Svc.PluginInterface.GetIpcSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool>("AllaganTools.ItemRemoved");

            _ItemCount = Svc.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCount");
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

            if (Service.Configuration.ShowOnlyCraftable || onLoad)
            {
                foreach (var recipe in CraftingListHelpers.FilteredList.Values)
                {
                    if (ATools && Service.Configuration.ShowOnlyCraftableRetainers || onLoad)
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
            _Initialized = null;
            _IsInitialized = null;
            _OnRetainerChanged = null;
            _OnItemAdded?.Unsubscribe(OnItemAdded);
            _OnItemRemoved?.Unsubscribe(OnItemRemoved);
            _OnItemAdded = null;
            _OnItemRemoved = null;
            _ItemCount = null;
        }

        public static Dictionary<ulong, Dictionary<uint, ItemInfo>> RetainerData = new Dictionary<ulong, Dictionary<uint, ItemInfo>>();
        public class ItemInfo
        {
            public uint ItemID { get; set; }

            public uint Quantity { get; set; }

            public ItemInfo(uint itemId, uint quantity)
            {
                ItemID = itemId;
                Quantity = quantity;
            }
        }

        public static void ClearCache(ulong? RetainerId)
        {
            RetainerData.Each(x => x.Value.Clear());
        }

        public static unsafe uint GetRetainerInventoryItem(uint itemID, ulong retainerId)
        {
            if (ATools)
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
            return 0;
        }
        public static unsafe int GetRetainerItemCount(uint itemId, bool tryCache = true)
        {

            if (ATools)
            {
                try
                {
                    if (tryCache)
                    {
                        if (RetainerData.SelectMany(x => x.Value).Any(x => x.Key == itemId))
                        {
                            return (int)RetainerData.Values.SelectMany(x => x.Values).Where(x => x.ItemID == itemId).Sum(x => x.Quantity);
                        }
                    }

                    for (int i = 0; i < 10; i++)
                    {
                        ulong retainerId = 0;
                        var retainer = RetainerManager.Instance()->GetRetainerBySortedIndex((uint)i);
                        if (Service.Configuration.RetainerIDs.Count(x => x.Value == Svc.ClientState.LocalContentId) > i)
                        {
                            retainerId = Service.Configuration.RetainerIDs.Where(x => x.Value == Svc.ClientState.LocalContentId).Select(x => x.Key).ToArray()[i];
                        }
                        else
                        {
                            retainerId = RetainerManager.Instance()->GetRetainerBySortedIndex((uint)i)->RetainerID;
                        }

                        if (retainer->RetainerID > 0 && !Service.Configuration.RetainerIDs.Any(x => x.Key == retainer->RetainerID && x.Value == Svc.ClientState.LocalContentId))
                        {
                            Service.Configuration.RetainerIDs.Add(retainer->RetainerID, Svc.ClientState.LocalContentId);
                            Service.Configuration.Save();
                        }

                        if (retainerId > 0)
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
                                    ret.TryAdd(itemId, new ItemInfo(itemId, GetRetainerInventoryItem(itemId, retainerId)));
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
                                    ret.TryAdd(itemId, new ItemInfo(itemId, GetRetainerInventoryItem(itemId, retainerId)));

                                }
                            }
                        }
                    }

                    return (int)RetainerData.SelectMany(x => x.Value).Where(x => x.Key == itemId).Sum(x => x.Value.Quantity);
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "RetainerInfoItemCount");
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
                foreach (var retainer in RetainerData)
                {
                    if (retainer.Value.Values.Any(x => x.ItemID == itemId && x.Quantity > 0))
                    {
                        TM.Enqueue(() => RetainerListHandlers.SelectRetainerByID(retainer.Key));
                        TM.DelayNext("WaitToSelectEntrust", 200);
                        TM.Enqueue(() => RetainerHandlers.SelectEntrustItems());
                        TM.DelayNext("EntrustSelected", 200);
                        TM.Enqueue(() =>
                        {
                            ExtractSingular(itemId, howManyToGet);
                        });

                        TM.DelayNext("CloseRetainer", 200);
                        TM.Enqueue(() => RetainerHandlers.CloseAgentRetainer());
                        TM.DelayNext("ClickQuit", 200);
                        TM.Enqueue(() => RetainerHandlers.SelectQuit());
                    }
                }
                TM.DelayNext("CloseRetainerList", 200);
                TM.Enqueue(() => RetainerListHandlers.CloseRetainerList());
                TM.Enqueue(() => YesAlready.EnableIfNeeded());
                TM.Enqueue(() => AutoRetainer.Unsuppress());
                TM.Enqueue(() => Svc.Framework.Update -= Tick);
            }
        }

        public static bool ExtractSingular(uint itemId, int howManyToGet)
        {
            if (howManyToGet != 0)
            {
                TM.DelayNextImmediate("WaitOnRetainerInventory", 500);
                TM.EnqueueImmediate(() => RetainerHandlers.OpenItemContextMenu(itemId, out firstFoundQuantity), 300);
                TM.DelayNextImmediate("WaitOnNumericPopup", 200);
                TM.EnqueueImmediate(() =>
                {
                    var value = Math.Min(howManyToGet, (int)firstFoundQuantity);
                    if (value == 0) return true;
                    PluginLog.Debug($"Min withdrawing: {value}, found {firstFoundQuantity}");
                    if (firstFoundQuantity == 1)
                    {
                        howManyToGet -= (int)firstFoundQuantity;
                        TM.EnqueueImmediate(() =>
                        {
                            ExtractSingular(itemId, howManyToGet);
                        }); 
                        return true;
                    }
                    if (RetainerHandlers.InputNumericValue(value))
                    {
                        howManyToGet -= value;

                        TM.EnqueueImmediate(() =>
                        {
                            ExtractSingular(itemId, howManyToGet);
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
                PluginLog.Debug($"Here");
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
                                    ExtractItem(requiredItems, item);
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
                TM.Enqueue(() => YesAlready.EnableIfNeeded());
                TM.Enqueue(() => AutoRetainer.Unsuppress());
                TM.Enqueue(() => Svc.Framework.Update -= Tick);
            }
        }

        private static unsafe void Tick(Framework framework)
        {
            if (Svc.Condition[ConditionFlag.OccupiedSummoningBell])
            {
                if (TryGetAddonByName<AddonTalk>("Talk", out var addon) && addon->AtkUnitBase.IsVisible)
                {
                    ClickTalk.Using((IntPtr)addon).Click();
                }
            }
        }

        private static bool ExtractItem(Dictionary<int, int> requiredItems, KeyValuePair<int, int> item)
        {
            if (requiredItems[item.Key] != 0)
            {
                PluginLog.Debug($"{requiredItems[item.Key]}");
                TM.DelayNextImmediate("WaitOnRetainerInventory", 500);
                TM.EnqueueImmediate(() => RetainerHandlers.OpenItemContextMenu((uint)item.Key, out firstFoundQuantity), 300);
                TM.DelayNextImmediate("WaitOnNumericPopup", 200);
                TM.EnqueueImmediate(() =>
                {
                    var value = Math.Min(requiredItems[item.Key], (int)firstFoundQuantity);
                    if (value == 0) return true;
                    PluginLog.Debug($"Min withdrawing: {value}, found {firstFoundQuantity}");
                    if (firstFoundQuantity == 1) { requiredItems[item.Key] -= (int)firstFoundQuantity; return true; }
                    if (RetainerHandlers.InputNumericValue(value))
                    {
                        requiredItems[item.Key] -= value;
                        PluginLog.Debug($"{requiredItems[item.Key]}");

                        TM.EnqueueImmediate(() =>
                        {
                            ExtractItem(requiredItems, item);
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

        internal static GameObject GetReachableRetainerBell()
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
