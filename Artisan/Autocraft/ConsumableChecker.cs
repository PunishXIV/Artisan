using Artisan.RawInformation;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Utility.Signatures;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Schedulers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Artisan.CraftingLists;
using ECommons.Logging;

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
        //[Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 41 B0 01 BA 13 00 00 00", Fallibility = Fallibility.Infallible)]
        //static delegate* unmanaged<AgentInterface*, uint, uint, uint, short, void> useItem;

        internal static void Init()
        {
            SignatureHelper.Initialise(new ConsumableChecker());
            itemContextMenuAgent = Framework.Instance()->UIModule->GetAgentModule()->GetAgentByInternalId(AgentId.InventoryContext);
            Usables = Service.DataManager.GetExcelSheet<Item>().Where(i => i.ItemAction.Row > 0).ToDictionary(i => i.RowId, i => i.Name.ToString().ToLower())
            .Concat(Service.DataManager.GetExcelSheet<EventItem>().Where(i => i.Action.Row > 0).ToDictionary(i => i.RowId, i => i.Name.ToString().ToLower()))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
            Food = Service.DataManager.GetExcelSheet<Item>().Where(x => (x.ItemUICategory.Value.RowId == 46 && IsCraftersAttribute(x)) || x.RowId == 10146).Select(x => (x.RowId, x.Name.ToString())).ToArray();
            Pots = Service.DataManager.GetExcelSheet<Item>().Where(x => !x.RowId.EqualsAny<uint>(4570) && x.ItemUICategory.Value.RowId == 44 && (IsCraftersAttribute(x) || IsSpiritBondAttribute(x))).Select(x => (x.RowId, x.Name.ToString())).ToArray();
            Manuals = Service.DataManager.GetExcelSheet<Item>().Where(x => !x.RowId.EqualsAny<uint>(4570) && x.ItemUICategory.Value.RowId == 63 && IsManualAttribute(x)).Select(x => (x.RowId, x.Name.ToString())).ToArray();
            SquadronManuals = Service.DataManager.GetExcelSheet<Item>().Where(x => !x.RowId.EqualsAny<uint>(4570) && x.ItemUICategory.Value.RowId == 63 && IsSquadronManualAttribute(x)).Select(x => (x.RowId, x.Name.ToString())).ToArray();
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
                    if (Service.DataManager.GetExcelSheet<ItemFood>().GetRow(z).UnkData1[0].BaseParam.EqualsAny<byte>(69))
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
                    if (Service.DataManager.GetExcelSheet<ItemFood>().GetRow(z).UnkData1[0].BaseParam.EqualsAny<byte>(11, 70, 71))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }


        internal static bool IsFooded(ListItemOptions? listItemOptions = null)
        {
            if (listItemOptions != null && listItemOptions.Food == 0) return true;
            if (Service.ClientState.LocalPlayer.StatusList.Any(x => x.StatusId == 48 & x.RemainingTime > 10f))
            {
                var configFood = listItemOptions != null ? listItemOptions.Food : Service.Configuration.Food;
                var configFoodHQ = listItemOptions != null ? listItemOptions.FoodHQ : Service.Configuration.FoodHQ;

                var foodBuff = Service.ClientState.LocalPlayer.StatusList.First(x => x.StatusId == 48);
                var desiredFood = LuminaSheets.ItemSheet[configFood].ItemAction.Value;
                var itemFood = LuminaSheets.ItemFoodSheet[configFoodHQ ? desiredFood.DataHQ[1] : desiredFood.Data[1]];
                if (foodBuff.Param != (itemFood.RowId + (configFoodHQ ? 10000 : 0)))
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        internal static bool IsPotted(ListItemOptions? listItemOptions = null)
        {
            if (listItemOptions != null && listItemOptions.Potion == 0) return true;
            if (Service.ClientState.LocalPlayer.StatusList.Any(x => x.StatusId == 49 && x.RemainingTime > 10f))
            {
                var configPot = listItemOptions != null ? listItemOptions.Potion : Service.Configuration.Potion ;
                var configPotHQ = listItemOptions != null ? listItemOptions.PotHQ : Service.Configuration.PotHQ;

                var potBuff = Service.ClientState.LocalPlayer.StatusList.First(x => x.StatusId == 49);
                var desiredPot = LuminaSheets.ItemSheet[configPot].ItemAction.Value;
                var itemFood = LuminaSheets.ItemFoodSheet[configPotHQ ? desiredPot.DataHQ[1] : desiredPot.Data[1]];
                if (potBuff.Param != (itemFood.RowId + (configPotHQ ? 10000 : 0)))
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        internal static bool IsManualled()
        {
            return Svc.ClientState.LocalPlayer?.StatusList.Any(x => x.StatusId == 45) == true;
        }

        internal static bool IsSquadronManualled()
        {
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

        //internal static bool UseItemInternal(uint id, bool hq = false)
        //{
        //    if (id == 0) return false;
        //    if (hq) id += 1_000_000;
        //    if (!Usables.ContainsKey(id is >= 1_000_000 and < 2_000_000 ? id - 1_000_000 : id)) return false;
        //    useItem(itemContextMenuAgent, id, 9999, 0, 0);
        //    return true;
        //}

        internal static unsafe bool UseItem2(uint itemID) =>
            ActionManager.Instance() is not null && ActionManager.Instance()->UseAction(ActionType.Item, itemID, a4: 65535);

        internal static unsafe uint GetItemStatus(uint itemID) => ActionManager.Instance() is null ? uint.MaxValue : ActionManager.Instance()->GetActionStatus(ActionType.Item, itemID);
        internal static bool CheckConsumables(bool use = true, ListItemOptions? listItemOptions = null)
        {
            if (Endurance.SkipBuffs) return false;

            uint desiredFood = listItemOptions != null ? listItemOptions.Food : Service.Configuration.Food;
            bool desiredFoodHQ = listItemOptions != null ? listItemOptions.FoodHQ : Service.Configuration.FoodHQ;
            uint desiredPot = listItemOptions != null ? listItemOptions.Potion : Service.Configuration.Potion;
            bool desiredPotHQ = listItemOptions != null ? listItemOptions.PotHQ : Service.Configuration.PotHQ;

            var fooded = IsFooded(listItemOptions) || (desiredFood == 0 && Endurance.Enable);
            if (!fooded)
            {
                if (GetFood(true, desiredFoodHQ).Any(x => x.Id == desiredFood))
                {
                    if (use) UseItem(desiredFood, desiredFoodHQ);
                    return false;
                }
                else
                {
                    if (Endurance.Enable)
                    {
                        DuoLog.Error("Food not found. Disabling Endurance.");
                        Endurance.Enable = false;
                    }
                    fooded = !Service.Configuration.AbortIfNoFoodPot;
                }
            }
            var potted = IsPotted(listItemOptions) || (Service.Configuration.Potion == 0 && Endurance.Enable);
            if (!potted)
            {
                if (GetPots(true, desiredPotHQ).Any(x => x.Id == desiredPot))
                {
                    if (use) UseItem(desiredPot, desiredPotHQ);
                    return false;
                }
                else
                {
                    if (Endurance.Enable)
                    {
                        DuoLog.Error("Potion not found. Disabling Endurance.");
                        Endurance.Enable = false;
                    }
                    potted = !Service.Configuration.AbortIfNoFoodPot;
                }
            }
            if (listItemOptions == null)
            {
                var manualed = IsManualled() || (Service.Configuration.Manual == 0 && Endurance.Enable);
                if (!manualed)
                {
                    if (GetManuals(true).Any(x => x.Id == Service.Configuration.Manual))
                    {
                        if (use) UseItem(Service.Configuration.Manual);
                        return false;
                    }
                    else
                    {
                        if (Endurance.Enable)
                        {
                            DuoLog.Error("Manual not found. Disabling Endurance.");
                            Endurance.Enable = false;
                        }
                        manualed = !Service.Configuration.AbortIfNoFoodPot;
                    }
                }
                var squadronManualed = IsSquadronManualled() || (Service.Configuration.SquadronManual == 0 && Endurance.Enable);
                if (!squadronManualed)
                {
                    if (GetSquadronManuals(true).Any(x => x.Id == Service.Configuration.SquadronManual))
                    {
                        if (use) UseItem(Service.Configuration.SquadronManual);
                        return false;
                    }
                    else
                    {
                        if (Endurance.Enable)
                        {
                            DuoLog.Error("Squadron Manual not found. Disabling Endurance.");
                            Endurance.Enable = false;
                        }
                        squadronManualed = !Service.Configuration.AbortIfNoFoodPot;
                    }
                }
                var ret = potted && fooded && manualed && squadronManualed;
                return ret;
            }

            return potted && fooded;
        }

    }
}
