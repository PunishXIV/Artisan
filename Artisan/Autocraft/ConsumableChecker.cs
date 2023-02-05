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

namespace Artisan.Autocraft
{
#pragma warning disable CS8604,CS8618,CS0649
    internal unsafe class ConsumableChecker
    {
        internal static (uint Id, string Name)[] Food;
        internal static (uint Id, string Name)[] Pots;
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
            Food = Service.DataManager.GetExcelSheet<Item>().Where(x => x.ItemUICategory.Value.RowId == 46 && IsCraftersAttribute(x)).Select(x => (x.RowId, x.Name.ToString())).ToArray();
            Pots = Service.DataManager.GetExcelSheet<Item>().Where(x => !x.RowId.EqualsAny<uint>(4570) && x.ItemUICategory.Value.RowId == 44 && IsCraftersAttribute(x)).Select(x => (x.RowId, x.Name.ToString())).ToArray();
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


        internal static bool IsFooded()
        {
            return Svc.ClientState.LocalPlayer?.StatusList.Any(x => x.StatusId == 48 && x.RemainingTime > 0f) == true;
        }

        internal static bool IsPotted()
        {
            return Svc.ClientState.LocalPlayer?.StatusList.Any(x => x.StatusId == 49 && x.RemainingTime > 0f) == true;
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
                //var ret = UseItemInternal(id, hq);
                //return ret;
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
        internal static bool CheckConsumables(bool use = true)
        {
            var fooded = IsFooded() || Service.Configuration.Food == 0;
            if (!fooded)
            {
                if (GetFood(true, Service.Configuration.FoodHQ).Any())
                {
                    if(use) UseItem(Service.Configuration.Food, Service.Configuration.FoodHQ);
                    return false;
                }
                else
                {
                    fooded = !Service.Configuration.AbortIfNoFoodPot;
                }
            }
            var potted = IsPotted() || Service.Configuration.Potion == 0;
            if (!potted)
            {
                if (GetPots(true, Service.Configuration.PotHQ).Any())
                {
                    if (use) UseItem(Service.Configuration.Potion, Service.Configuration.PotHQ);
                    return false;
                }
                else
                {
                    potted = !Service.Configuration.AbortIfNoFoodPot;
                }
            }
            var ret = potted && fooded;
            return ret;
        }

    }
}
