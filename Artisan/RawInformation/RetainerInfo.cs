using Artisan.CraftingLists;
using Artisan.IPC;
using Artisan.Tasks;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Ipc;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ExcelServices.TerritoryEnumeration;
using ECommons.Reflection;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using static ECommons.GenericHelpers;
using RetainerManager = FFXIVClientStructs.FFXIV.Client.Game.RetainerManager;

namespace Artisan.RawInformation
{
    public static class RetainerInfo
    {
        private static ICallGateSubscriber<ulong?, bool>? _OnRetainerChanged;
        private static ICallGateSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool>? _OnItemAdded;
        private static ICallGateSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool>? _OnItemRemoved;
        public static TaskManager TM = new TaskManager();
        internal static bool GenericThrottle => EzThrottler.Throttle("RetainerInfoThrottler", 100);
        internal static void RethrottleGeneric(int num) => EzThrottler.Throttle("RetainerInfoThrottler", num, true);
        internal static void RethrottleGeneric() => EzThrottler.Throttle("RetainerInfoThrottler", 100, true);
        internal static Tasks.RetainerManager retainerManager = new(Svc.SigScanner);

        public static bool ATools => DalamudReflector.TryGetDalamudPlugin("Allagan Tools", out var it, false, true);
        private static uint firstFoundQuantity = 0;

        public static bool CacheBuilt = ATools ? false : true;
        public static CancellationTokenSource CTSource = new();
        public static readonly object _lockObj = new();

        internal static void Init()
        {
            if (ATools)
            {
                _OnRetainerChanged = Svc.PluginInterface.GetIpcSubscriber<ulong?, bool>("AllaganTools.RetainerChanged");
                //_OnRetainerChanged.Subscribe(ClearCache);
                _OnItemAdded = Svc.PluginInterface.GetIpcSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool>("AllaganTools.ItemAdded");
                _OnItemRemoved = Svc.PluginInterface.GetIpcSubscriber<(uint, InventoryItem.ItemFlags, ulong, uint), bool>("AllaganTools.ItemRemoved");

                _OnItemAdded.Subscribe(OnItemAdded);
                _OnItemRemoved.Subscribe(OnItemRemoved);
                TM.TimeoutSilently = true;
            }

            if (Svc.ClientState.IsLoggedIn)
                LoadCache(true);
            Svc.ClientState.Login += LoadCacheLogin;
        }

        private static void LoadCacheLogin(object? sender, EventArgs e)
        {
            LoadCache();
        }

        public async static Task<bool?> LoadCache(bool onLoad = false)
        {
            if (CraftingListUI.CraftableItems.Count > 0 && !CacheBuilt) return false;

            CacheBuilt = false;
            ClearCache(null);
            CraftingListUI.CraftableItems.Clear();

            if (Service.Configuration.ShowOnlyCraftable || onLoad)
            {
                foreach (var recipe in CraftingListUI.FilteredList.Values)
                {
                    if (ATools && Service.Configuration.ShowOnlyCraftableRetainers || onLoad)
                        await Task.Run(async () => await CraftingListUI.CheckForIngredients(recipe, false, true));
                    else
                        await Task.Run(async () => await CraftingListUI.CheckForIngredients(recipe, false, false));
                }
            }

            ClearCache(null);
            CacheBuilt = true;
            return true;
        }

        private static void OnItemAdded((uint, InventoryItem.ItemFlags, ulong, uint) tuple)
        {
            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedSummoningBell])
            {
                ClearCache(null);
                //if (RetainerData.ContainsKey(tuple.Item3))
                //{
                //    if (RetainerData[tuple.Item3].ContainsKey(tuple.Item1))
                //        RetainerData[tuple.Item3][tuple.Item1].Quantity += tuple.Item4;
                //    else
                //        RetainerData[tuple.Item3].TryAdd(tuple.Item1, new ItemInfo(tuple.Item1, tuple.Item4));
                //}
            }
        }

        private static void OnItemRemoved((uint, InventoryItem.ItemFlags, ulong, uint) tuple)
        {
            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedSummoningBell])
            {
                ClearCache(null);
                //if (RetainerData[tuple.Item3].ContainsKey(tuple.Item1))
                //    RetainerData[tuple.Item3][tuple.Item1].Quantity -= tuple.Item4;
                //else
                //    RetainerData[tuple.Item3].TryAdd(tuple.Item1, new ItemInfo(tuple.Item1, tuple.Item4));

            }
        }

        internal static void Dispose()
        {
            if (ATools)
            {
                //_OnRetainerChanged?.Unsubscribe(ClearCache);
                _OnRetainerChanged = null;
                _OnItemAdded?.Unsubscribe(OnItemAdded);
                _OnItemRemoved?.Unsubscribe(OnItemRemoved);
                _OnItemAdded = null;
                _OnItemRemoved = null;
                Svc.ClientState.Login -= LoadCacheLogin;
            }
        }

        public static Dictionary<ulong, Dictionary<uint, ItemInfo>> RetainerData = new Dictionary<ulong, Dictionary<uint, ItemInfo>>();
        public class ItemInfo
        {
            public uint ItemID { get; set; }

            public uint Quantity { get; set; }

            public ItemInfo(uint itemId, uint quantity)
            {
                this.ItemID = itemId;
                this.Quantity = quantity;
            }
        }

        public static void ClearCache(ulong? RetainerId)
        {
            RetainerData.Each(x => x.Value.Clear());
        }

        private static unsafe uint GetRetainerInventoryItem(uint itemID, ulong retainerId)
        {
            if (ATools)
            {
                return Svc.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCount").InvokeFunc(itemID, retainerId, 10000) +
                        Svc.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCount").InvokeFunc(itemID, retainerId, 10001) +
                        Svc.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCount").InvokeFunc(itemID, retainerId, 10002) +
                        Svc.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCount").InvokeFunc(itemID, retainerId, 10003) +
                        Svc.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCount").InvokeFunc(itemID, retainerId, 10004) +
                        Svc.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCount").InvokeFunc(itemID, retainerId, 10005) +
                        Svc.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCount").InvokeFunc(itemID, retainerId, 10006) +
                        Svc.PluginInterface.GetIpcSubscriber<uint, ulong, uint, uint>("AllaganTools.ItemCount").InvokeFunc(itemID, retainerId, (uint)InventoryType.RetainerCrystals);
            }
            return 0;
        }
        public static unsafe uint GetRetainerItemCount(uint itemId, bool tryCache = true)
        {
            if (tryCache)
            {
                if (RetainerData.SelectMany(x => x.Value).Any(x => x.Key == itemId))
                {
                    return (uint)RetainerData.Values.SelectMany(x => x.Values).Where(x => x.ItemID == itemId).Sum(x => x.Quantity);
                }
            }

            for (int i = 0; i < 10; i++)
            {
                var retainer = RetainerManager.Instance()->GetRetainerBySortedIndex((uint)i);

                if (retainer != null)
                {
                    if (RetainerData.ContainsKey(retainer->RetainerID))
                    {
                        var ret = RetainerData[retainer->RetainerID];
                        if (ret.ContainsKey(itemId))
                        {
                            var item = ret[itemId];
                            item.ItemID = itemId;
                            item.Quantity = GetRetainerInventoryItem(itemId, retainer->RetainerID);

                        }
                        else
                        {
                            ret.TryAdd(itemId, new ItemInfo(itemId, GetRetainerInventoryItem(itemId, retainer->RetainerID)));
                        }
                    }
                    else
                    {
                        RetainerData.TryAdd(retainer->RetainerID, new Dictionary<uint, ItemInfo>());
                        var ret = RetainerData[retainer->RetainerID];
                        if (ret.ContainsKey(itemId))
                        {
                            var item = ret[itemId];
                            item.ItemID = itemId;
                            item.Quantity = GetRetainerInventoryItem(itemId, retainer->RetainerID);

                        }
                        else
                        {
                            ret.TryAdd(itemId, new ItemInfo(itemId, GetRetainerInventoryItem(itemId, retainer->RetainerID)));

                        }
                    }
                }
            }


            return (uint)RetainerData.SelectMany(x => x.Value).Where(x => x.Key == itemId).Sum(x => x.Value.Quantity);
        }

        public static void RestockFromRetainers(CraftingList list)
        {
            if (GetReachableRetainerBell() == null) return;

            Dictionary<int, int> requiredItems = new();
            Dictionary<int, int> materialList = new();

            foreach (var item in list.Items)
            {
                var recipe = LuminaSheets.RecipeSheet[item];
                CraftingListUI.AddRecipeIngredientsToList(recipe, ref materialList, false);
            }

            foreach (var material in materialList.OrderByDescending(x => x.Key))
            {
                var invCount = CraftingListUI.NumberOfIngredient((uint)material.Key);
                if (invCount < material.Value)
                {
                    var diffcheck = material.Value - invCount;
                    requiredItems.Add(material.Key, diffcheck);
                }
            }

            if (RetainerData.SelectMany(x => x.Value).Any(x => requiredItems.Any(y => y.Key == x.Value.ItemID)))
            {
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
                                    if (requiredItems[item.Key] != 0)
                                    {
                                        TM.EnqueueImmediate(() => RetainerHandlers.OpenItemContextMenu((uint)item.Key, out firstFoundQuantity), 1500);
                                        TM.DelayNext("WaitOnNumericPopup", 300, true);
                                        TM.EnqueueImmediate(() =>
                                        {
                                            var value = Math.Min(requiredItems[item.Key], (int)firstFoundQuantity);
                                            if (value == 1) return true;
                                            if (RetainerHandlers.InputNumericValue(value))
                                            {
                                                requiredItems[item.Key] -= value;

                                                return true;
                                            }
                                            else
                                            {
                                                return false;
                                            }
                                        }, 300);
                                    }
                                });

                                TM.Enqueue(() =>
                                {
                                    if (requiredItems[item.Key] != 0)
                                    {
                                        TM.DelayNext("TryForExtraMat", 200, true);
                                        TM.EnqueueImmediate(() => RetainerHandlers.OpenItemContextMenu((uint)item.Key, out firstFoundQuantity), 1500);
                                        TM.DelayNext("WaitOnNumericPopup", 300, true);
                                        TM.EnqueueImmediate(() =>
                                        {
                                            var value = Math.Min(requiredItems[item.Key], (int)firstFoundQuantity);
                                            if (value == 1) return true;
                                            if (RetainerHandlers.InputNumericValue(value))
                                            {
                                                requiredItems[item.Key] -= value;

                                                return true;
                                            }
                                            else
                                            {
                                                return false;
                                            }
                                        }, 300);
                                    }
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
            }
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
