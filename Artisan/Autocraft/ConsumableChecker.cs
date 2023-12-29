using Artisan.RawInformation;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using ECommons.Logging;
using Artisan.GameInterop;
using FFXIVClientStructs.FFXIV.Client.Game;
using Artisan.CraftingLogic;

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
            Usables = Svc.Data.GetExcelSheet<Item>().Where(i => i.ItemAction.Row > 0).ToDictionary(i => i.RowId, i => i.Name.ToString().ToLower())
            .Concat(Svc.Data.GetExcelSheet<EventItem>().Where(i => i.Action.Row > 0).ToDictionary(i => i.RowId, i => i.Name.ToString().ToLower()))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
            Food = Svc.Data.GetExcelSheet<Item>().Where(x => (x.ItemUICategory.Value.RowId == 46 && IsCraftersAttribute(x)) || x.RowId == 10146).Select(x => (x.RowId, x.Name.ToString())).ToArray();
            Pots = Svc.Data.GetExcelSheet<Item>().Where(x => x.RowId > 5000 && !x.RowId.EqualsAny<uint>(4570) && x.ItemUICategory.Value.RowId == 44 && (IsCraftersAttribute(x) || IsSpiritBondAttribute(x)) || x.RowId == 7060).Select(x => (x.RowId, x.Name.ToString())).ToArray();
            Manuals = Svc.Data.GetExcelSheet<Item>().Where(x => !x.RowId.EqualsAny<uint>(4570) && x.ItemUICategory.Value.RowId == 63 && IsManualAttribute(x)).Select(x => (x.RowId, x.Name.ToString())).ToArray();
            SquadronManuals = Svc.Data.GetExcelSheet<Item>().Where(x => !x.RowId.EqualsAny<uint>(4570) && x.ItemUICategory.Value.RowId == 63 && IsSquadronManualAttribute(x)).Select(x => (x.RowId, x.Name.ToString())).ToArray();
        }

        internal static (uint Id, string Name)[] GetFood(bool inventoryOnly = false, bool hq = false)
        {
            if (inventoryOnly) return Food.Where(x => InventoryManager.Instance()->GetInventoryItemCount(x.Id, hq) > 0).ToArray();
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

        internal static bool IsManualAttribute(Item x)
        {
            try
            {
                ushort[] engineeringManuals = { 301, 1751, 5329 };
                if (x.ItemAction.Value.Type.Equals(816) && x.ItemAction.Value.Data[0].EqualsAny(engineeringManuals))
                {
                    return true;
                }
            }
            catch { }
            return false;
        }

        internal static bool IsSquadronManualAttribute(Item x)
        {
            try
            {
                ushort[] squadrantManuals = { 2291, 2292, 2293, 2294 };
                if (x.ItemAction.Value.Type.Equals(816) && x.ItemAction.Value.Data[0].EqualsAny(squadrantManuals))
                {
                   return true;
                }
            }
            catch { }
            return false;
        }

        internal static bool IsSpiritBondAttribute(Item x)
        {
            try
            {
                foreach (var z in x.ItemAction.Value?.Data)
                {
                    if (Svc.Data.GetExcelSheet<ItemFood>().GetRow(z).UnkData1[0].BaseParam.EqualsAny<byte>(69))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        internal static bool IsCraftersAttribute(Item x)
        {
            try
            {
                foreach (var z in x.ItemAction.Value?.Data)
                {
                    if (Svc.Data.GetExcelSheet<ItemFood>().GetRow(z).UnkData1[0].BaseParam.EqualsAny<byte>(11, 70, 71))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }


        internal static bool IsFooded(RecipeConfig? config)
        {
            if (config == null || config.RequiredFood == 0)
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
            if (config == null || config.RequiredPotion == 0)
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
            if (config == null || config.RequiredManual == 0)
                return true; // don't need a manual
            return Svc.ClientState.LocalPlayer?.StatusList.Any(x => x.StatusId == 45) == true;
        }

        internal static bool IsSquadronManualled(RecipeConfig? config)
        {
            if (config == null || config.RequiredSquadronManual == 0)
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

        internal static unsafe bool UseItem2(uint itemID) => ActionManagerEx.UseItem(itemID);

        internal static bool CheckConsumables(RecipeConfig config, bool use = true)
        {
            if (Endurance.SkipBuffs) return false;

            var fooded = IsFooded(config) || (config.RequiredFood == 0 && Endurance.Enable);
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
                        Endurance.Enable = false;
                    }
                    fooded = !P.Config.AbortIfNoFoodPot;
                }
            }
            var potted = IsPotted(config) || (config.RequiredPotion == 0 && Endurance.Enable);
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
                        Endurance.Enable = false;
                    }
                    potted = !P.Config.AbortIfNoFoodPot;
                }
            }
            var manualed = IsManualled(config) || (config.RequiredManual == 0 && Endurance.Enable);
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
                        Endurance.Enable = false;
                    }
                    manualed = !P.Config.AbortIfNoFoodPot;
                }
            }
            var squadronManualed = IsSquadronManualled(config) || (config.RequiredSquadronManual == 0 && Endurance.Enable);
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
                        Endurance.Enable = false;
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
