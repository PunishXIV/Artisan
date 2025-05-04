using Artisan.RawInformation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using System.Collections.Generic;
using System.Linq;
using ECommons.Logging;
using Artisan.GameInterop;
using FFXIVClientStructs.FFXIV.Client.Game;
using Artisan.CraftingLogic;
using Lumina.Excel.Sheets;

namespace Artisan.Autocraft
{
#pragma warning disable CS8604,CS8618,CS0649
    internal unsafe class ConsumableChecker
    {
        internal static (uint Id, string Name)[] Food;
        internal static (uint Id, string Name)[] Pots;
        internal static (uint Id, string Name)[] Manuals;
        internal static (uint Id, string Name)[] SquadronManuals;
        static Dictionary<uint, string> Usables;
        static AgentInterface* itemContextMenuAgent;

        internal static void Init()
        {
            itemContextMenuAgent = Framework.Instance()->UIModule->GetAgentModule()->GetAgentByInternalId(AgentId.InventoryContext);
            Usables = Svc.Data.GetExcelSheet<Item>().Where(i => i.ItemAction.RowId > 0).ToDictionary(i => i.RowId, i => i.Name.ToString().ToLower())
            .Concat(Svc.Data.GetExcelSheet<EventItem>().Where(i => i.Action.RowId > 0).ToDictionary(i => i.RowId, i => i.Name.ToString().ToLower()))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
            Food = Svc.Data.GetExcelSheet<Item>().Where(IsCraftersFood).Select(x => (x.RowId, x.Name.ToString())).ToArray();
            Pots = Svc.Data.GetExcelSheet<Item>().Where(IsCraftersPot).Select(x => (x.RowId, x.Name.ToString())).ToArray();
            Manuals = Svc.Data.GetExcelSheet<Item>().Where(IsManual).Select(x => (x.RowId, x.Name.ToString())).ToArray();
            SquadronManuals = Svc.Data.GetExcelSheet<Item>().Where(IsSquadronManual).Select(x => (x.RowId, x.Name.ToString())).ToArray();
        }

        internal static (uint Id, string Name)[] GetFood(bool inventoryOnly = false, bool hq = false)
        {
            if (inventoryOnly) return Food.Where(x => InventoryManager.Instance()->GetInventoryItemCount(x.Id, hq) > 0f).ToArray();
            return Food;
        }

        internal static (uint Id, string Name)[] GetPots(bool inventoryOnly = false, bool hq = false)
        {
            if (inventoryOnly) return Pots.Where(x => InventoryManager.Instance()->GetInventoryItemCount(x.Id, hq) > 0).ToArray();
            return Pots;
        }

        internal static (uint Id, string Name)[] GetManuals(bool inventoryOnly = false, bool hq = false)
        {
            if (inventoryOnly) return Manuals.Where(x => InventoryManager.Instance()->GetInventoryItemCount(x.Id, hq) > 0).ToArray();
            return Manuals;
        }

        internal static (uint Id, string Name)[] GetSquadronManuals(bool inventoryOnly = false, bool hq = false)
        {
            if (inventoryOnly) return SquadronManuals.Where(x => InventoryManager.Instance()->GetInventoryItemCount(x.Id, hq) > 0).ToArray();
            return SquadronManuals;
        }

        internal static ItemFood? GetItemConsumableProperties(Item item, bool hq)
        {
            if (!item.ItemAction.IsValid)
                return null;
            var action = item.ItemAction.Value;
            var actionParams = hq ? action.DataHQ : action.Data; // [0] = status, [1] = extra == ItemFood row, [2] = duration
            if (actionParams[0] is not 48 and not 49)
                return null; // not 'well fed' or 'medicated'
            return Svc.Data.GetExcelSheet<ItemFood>()?.GetRow(actionParams[1]);
        }

        internal static bool IsCraftersFood(Item item)
        {
            if (item.ItemUICategory.RowId != 46)
                return false; // not a 'meal'
            var consumable = GetItemConsumableProperties(item, false);
            return consumable != null && consumable.Value.Params.Any(p => p.BaseParam.RowId is 11 or 70 or 71); // cp/craftsmanship/control
        }

        internal static bool IsCraftersPot(Item item)
        {
            if (item.ItemUICategory.RowId != 44)
                return false; // not a 'medicine'
            var consumable = GetItemConsumableProperties(item, false);
            return consumable != null && consumable.Value.Params.Any(p => p.BaseParam.RowId is 11 or 70 or 71 or 69 or 68); // cp/craftsmanship/control/increased spiritbond/reduced durability loss
        }

        internal static bool IsManual(Item item)
        {
            if (item.ItemUICategory.RowId != 63)
                return false; // not 'other'
            if (!item.ItemAction.IsValid)
                return false;
            var action = item.ItemAction.Value;
            return action.Type == 816 && action.Data[0] is 300 or 301 or 1751 or 5329;
        }

        internal static bool IsSquadronManual(Item item)
        {
            if (item.ItemUICategory.RowId != 63)
                return false; // not 'other'
            if (!item.ItemAction.IsValid)
                return false;
            var action = item.ItemAction.Value;
            return action.Type == 816 && action.Data[0] is 2291 or 2292 or 2293 or 2294;
        }


        internal static bool IsFooded(RecipeConfig? config)
        {
            if (config == null || !config.FoodEnabled)
                return true; // don't need a food
            var foodBuff = Svc.ClientState.LocalPlayer.StatusList.FirstOrDefault(x => x.StatusId == 48 & x.RemainingTime > 10f);
            if (foodBuff == null)
                return false; // don't have any well-fed buff
            var desiredFood = LuminaSheets.ItemSheet[config.RequiredFood].ItemAction.Value;
            if (foodBuff.Param == desiredFood.DataHQ[1] + 10000)
                return true; // we have HQ food, assume it's ok even if recipe requires NQ
            if (foodBuff.Param == desiredFood.Data[1] && !config.RequiredFoodHQ)
                return true; // we have NQ food and don't require HQ
            return false;
        }

        internal static bool IsPotted(RecipeConfig? config)
        {
            if (config == null || !config.PotionEnabled)
                return true; // don't need a pot
            var potBuff = Svc.ClientState.LocalPlayer.StatusList.FirstOrDefault(x => x.StatusId == 49 & x.RemainingTime > 10f);
            if (potBuff == null)
                return false; // don't have any well-fed buff
            var desiredPot = LuminaSheets.ItemSheet[config.RequiredPotion].ItemAction.Value;
            if (potBuff.Param == desiredPot.DataHQ[1] + 10000)
                return true; // we have HQ pot, assume it's ok even if recipe requires NQ
            if (potBuff.Param == desiredPot.Data[1] && !config.RequiredPotionHQ)
                return true; // we have NQ pot and don't require HQ
            return false;
        }

        internal static bool IsManualled(RecipeConfig? config)
        {
            if (config == null || !config.ManualEnabled)
                return true; // don't need a manual
            return Svc.ClientState.LocalPlayer?.StatusList.Any(x => x.StatusId == 45) == true;
        }

        internal static bool IsSquadronManualled(RecipeConfig? config)
        {
            if (config == null || !config.SquadronManualEnabled)
                return true; // don't need a squadron manual
            // Squadron engineering/spiritbonding/rationing/gear manual.
            uint[] SquadronManualBuffss = { 1082, 1083, 1084, 1085 };
            return Svc.ClientState.LocalPlayer?.StatusList.Any(x => SquadronManualBuffss.Contains(x.StatusId)) == true;
        }

        internal static bool UseItem(uint id, bool hq = false)
        {
            if (Throttler.Throttle(2000))
            {
                if (hq)
                {
                    return UseItem2(id + 1_000_000);
                }
                else
                {
                    return UseItem2(id);
                }
            }
            return false;
        }

        internal static unsafe bool UseItem2(uint ItemId) => ActionManagerEx.UseItem(ItemId);

        internal static bool CheckConsumables(RecipeConfig config, bool use = true)
        {
            if (Endurance.SkipBuffs) return false;

            var fooded = IsFooded(config) || (!config.FoodEnabled && Endurance.Enable);
            if (!fooded)
            {
                if (GetFood(true, config.RequiredFoodHQ).Any(x => x.Id == config.RequiredFood))
                {
                    if (use) UseItem(config.RequiredFood, config.RequiredFoodHQ);
                    return false;
                }
                else
                {
                    if (Endurance.Enable)
                    {
                        Svc.Toasts.ShowError("Food not found. Disabling Endurance.");
                        DuoLog.Error("Food not found. Disabling Endurance.");
                        Endurance.ToggleEndurance(false);
                    }
                    fooded = !P.Config.AbortIfNoFoodPot;
                }
            }
            var potted = IsPotted(config) || (!config.PotionEnabled && Endurance.Enable);
            if (!potted)
            {
                if (GetPots(true, config.RequiredPotionHQ).Any(x => x.Id == config.RequiredPotion))
                {
                    if (use) UseItem(config.RequiredPotion, config.RequiredPotionHQ);
                    return false;
                }
                else
                {
                    if (Endurance.Enable)
                    {
                        Svc.Toasts.ShowError("Potion not found. Disabling Endurance.");
                        DuoLog.Error("Potion not found. Disabling Endurance.");
                        Endurance.ToggleEndurance(false);
                    }
                    potted = !P.Config.AbortIfNoFoodPot;
                }
            }
            var manualed = IsManualled(config) || (!config.ManualEnabled && Endurance.Enable);
            if (!manualed)
            {
                if (GetManuals(true).Any(x => x.Id == config.RequiredManual))
                {
                    if (use) UseItem(config.RequiredManual);
                    return false;
                }
                else
                {
                    if (Endurance.Enable)
                    {
                        Svc.Toasts.ShowError("Manual not found. Disabling Endurance.");
                        DuoLog.Error("Manual not found. Disabling Endurance.");
                        Endurance.ToggleEndurance(false);
                    }
                    manualed = !P.Config.AbortIfNoFoodPot;
                }
            }
            var squadronManualed = IsSquadronManualled(config) || (!config.SquadronManualEnabled && Endurance.Enable);
            if (!squadronManualed)
            {
                if (GetSquadronManuals(true).Any(x => x.Id == config.RequiredSquadronManual))
                {
                    if (use) UseItem(config.RequiredSquadronManual);
                    return false;
                }
                else
                {
                    if (Endurance.Enable)
                    {
                        Svc.Toasts.ShowError("Squadron Manual not found. Disabling Endurance.");
                        DuoLog.Error("Squadron Manual not found. Disabling Endurance.");
                        Endurance.ToggleEndurance(false);
                    }
                    squadronManualed = !P.Config.AbortIfNoFoodPot;
                }
            }
            return potted && fooded && manualed && squadronManualed;
        }

        internal static bool HasItem(uint requiredItem, bool requiredItemHQ)
        {
            if (requiredItem == 0) return true;
            return InventoryManager.Instance()->GetInventoryItemCount(requiredItem, requiredItemHQ) > 0;
        }
    }
}
