using Artisan.CraftingLists;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.Tasks;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.Autocraft
{
    public static unsafe class AutoDepositManager
    {
        internal static bool DepositFailed;
        private static bool depositRunning;
        private static int foundQuantity;

        public static int GetFreeInventorySlots()
        {
            var inventories = new[]
            {
                InventoryType.Inventory1,
                InventoryType.Inventory2,
                InventoryType.Inventory3,
                InventoryType.Inventory4,
            };

            int free = 0;
            foreach (var inv in inventories)
            {
                var container = InventoryManager.Instance()->GetInventoryContainer(inv);
                if (container == null) continue;
                for (int i = 0; i < container->Size; i++)
                {
                    if (container->GetInventorySlot(i)->ItemId == 0)
                        free++;
                }
            }
            return free;
        }

        public static List<(ulong Id, string Name)> GetCharacterRetainers()
        {
            List<(ulong Id, string Name)> result = new();
            if (!Svc.ClientState.IsLoggedIn) return result;
            var rm = RetainerManager.Instance();
            if (rm == null) return result;
            for (uint i = 0; i < 10; i++)
            {
                var retainer = rm->GetRetainerBySortedIndex(i);
                if (retainer == null || retainer->RetainerId == 0 || !retainer->Available) continue;
                result.Add((retainer->RetainerId, retainer->NameString));
            }
            return result;
        }

        internal static void ResetBackoff()
        {
            RecoverIfAborted();
            DepositFailed = false;
        }

        internal static void RecoverIfAborted()
        {
            if (!depositRunning || RetainerInfo.TM.IsBusy) return;

            // The task chain aborted on a timeout before FinishDeposit could run.
            Svc.Framework.Update -= RetainerInfo.Tick;
            AutoRetainerIPC.Unsuppress();
            YesAlready.Unlock();
            depositRunning = false;
            Fail("Auto-deposit did not complete (the retainer interaction timed out). Auto-deposit is paused until the next craft session.");
        }

        internal static bool ProcessDeposit(NewCraftingList? list = null)
        {
            RecoverIfAborted();

            if (!P.Config.AutoDepositCrafts || DepositFailed) return true;
            if (depositRunning || RetainerInfo.TM.IsBusy) return !depositRunning;
            if (GetFreeInventorySlots() > P.Config.AutoDepositFreeSlotThreshold) return true;

            if (!P.Config.AutoDepositRetainers.TryGetValue(Svc.PlayerState.ContentId, out var retainerId) || retainerId == 0)
            {
                Fail("Auto-deposit is enabled but no retainer is selected in the Artisan settings. Continuing to craft.");
                return true;
            }

            if (!GetCharacterRetainers().Any(x => x.Id == retainerId))
            {
                Fail("Auto-deposit: the selected retainer no longer exists on this character. Pick a new retainer in the Artisan settings. Continuing to craft.");
                return true;
            }

            var items = GetDepositItems(list);
            if (items.Count == 0)
            {
                Fail("Auto-deposit: inventory is nearly full but there are no depositable crafted items. Continuing to craft.");
                return true;
            }

            if (RetainerInfo.GetReachableRetainerBell() == null)
            {
                Fail("Auto-deposit: no retainer bell within interaction range. Continuing to craft.");
                return true;
            }

            EnqueueDeposit(retainerId, items);
            return false;
        }

        private static List<uint> GetDepositItems(NewCraftingList? list)
        {
            HashSet<uint> outputs = new();

            if (list != null)
            {
                foreach (var recipeItem in list.Recipes)
                    outputs.Add(LuminaSheets.RecipeSheet[recipeItem.ID].ItemResult.RowId);

                HashSet<uint> remainingIngredients = new();
                for (int i = CraftingListFunctions.CurrentIndex; i < list.ExpandedList.Count; i++)
                {
                    foreach (var ing in LuminaSheets.RecipeSheet[list.ExpandedList[i]].Ingredients().Where(x => x.Amount > 0))
                        remainingIngredients.Add(ing.Item.RowId);
                }

                outputs.ExceptWith(remainingIngredients);
            }
            else if (Endurance.RecipeID > 0)
            {
                outputs.Add(LuminaSheets.RecipeSheet[Endurance.RecipeID].ItemResult.RowId);
            }

            outputs.RemoveWhere(x => x <= 19 || LuminaSheets.ItemSheet[x].IsCollectable);
            outputs.RemoveWhere(x => CraftingListUI.NumberOfIngredient(x) == 0);
            return outputs.ToList();
        }

        private static void EnqueueDeposit(ulong retainerId, List<uint> items)
        {
            depositRunning = true;
            var TM = RetainerInfo.TM;

            TM.Enqueue(() => Svc.Framework.Update += RetainerInfo.Tick);
            TM.Enqueue(() => AutoRetainerIPC.Suppress());
            TM.EnqueueBell();
            TM.DelayNext("DepositBellInteracted", 1000);
            TM.Enqueue(() => Svc.Condition[ConditionFlag.OccupiedSummoningBell]);
            TM.Enqueue(() => RetainerListHandlers.SelectRetainerByID(retainerId), 5000, true, "SelectDepositRetainer");
            TM.DelayNext("DepositWaitToSelectEntrust", 200);
            TM.Enqueue(() => RetainerHandlers.SelectEntrustItems());
            TM.DelayNext("DepositEntrustSelected", 200);

            foreach (var item in items)
            {
                TM.Enqueue(() => DepositSingular(item), $"DepositSingular{item}");
            }

            TM.DelayNext("DepositCloseRetainer", 200);
            TM.Enqueue(() => RetainerHandlers.CloseAgentRetainer());
            TM.DelayNext("DepositClickQuit", 200);
            TM.Enqueue(() => RetainerHandlers.SelectQuit());
            TM.DelayNext("DepositCloseRetainerList", 200);
            TM.Enqueue(() => RetainerListHandlers.CloseRetainerList());
            TM.Enqueue(() => YesAlready.Unlock());
            TM.Enqueue(() => AutoRetainerIPC.Unsuppress());
            TM.Enqueue(() => Svc.Framework.Update -= RetainerInfo.Tick);
            TM.Enqueue(() => FinishDeposit());
        }

        private static bool DepositSingular(uint itemId, int previousCount = -1)
        {
            var TM = RetainerInfo.TM;
            int current = 0;
            TM.DelayNextImmediate("DepositWaitOnInventory", 500);
            TM.EnqueueImmediate(() =>
            {
                current = CraftingListUI.NumberOfIngredient(itemId);
                if (previousCount >= 0 && current >= previousCount)
                {
                    // Nothing left the player inventory since the previous pass (retainer full or
                    // item not entrustable) — stop recursing so the chain can finish and back off.
                    foundQuantity = 0;
                    return true;
                }
                return RetainerHandlers.EntrustItem(itemId, out foundQuantity);
            }, 300);
            TM.DelayNextImmediate("DepositWaitOnNumericPopup", 200);
            TM.EnqueueImmediate(() =>
            {
                if (foundQuantity == 0) return true;
                if (foundQuantity == 1)
                {
                    TM.EnqueueImmediate(() => DepositSingular(itemId, current));
                    return true;
                }
                if (RetainerHandlers.InputNumericValue(foundQuantity))
                {
                    TM.EnqueueImmediate(() => DepositSingular(itemId, current));
                    return true;
                }
                return false;
            }, 1000);
            return true;
        }

        private static bool FinishDeposit()
        {
            depositRunning = false;
            if (GetFreeInventorySlots() <= P.Config.AutoDepositFreeSlotThreshold)
            {
                Fail("Auto-deposit finished but inventory is still nearly full (the retainer may be full). Auto-deposit is paused until the next craft session.");
            }
            return true;
        }

        private static void Fail(string message)
        {
            DepositFailed = true;
            DuoLog.Warning(message);
        }
    }
}
