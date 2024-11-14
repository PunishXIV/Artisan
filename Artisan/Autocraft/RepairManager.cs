using Artisan.CraftingLists;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.Logging;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using System;
using System.Linq;
using System.Numerics;
using static ECommons.GenericHelpers;

namespace Artisan.Autocraft
{
    internal unsafe class RepairManager
    {
        internal static void Repair()
        {
            if (TryGetAddonByName<AddonRepair>("Repair", out var addon) && addon->AtkUnitBase.IsVisible && addon->RepairAllButton->IsEnabled && Throttler.Throttle(500))
            {
                new AddonMaster.Repair((IntPtr)addon).RepairAll();
            }
        }

        internal static void ConfirmYesNo()
        {
            if (TryGetAddonByName<AddonRepair>("Repair", out var r) &&
                r->AtkUnitBase.IsVisible && TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) &&
                addon->AtkUnitBase.IsVisible &&
                addon->YesButton is not null &&
                addon->YesButton->IsEnabled &&
                addon->AtkUnitBase.UldManager.NodeList[15]->IsVisible())
            {
                new AddonMaster.SelectYesno((IntPtr)addon).Yes();
            }
        }

        internal static bool HasDarkMatterOrBetter(uint darkMatterID)
        {
            var repairResources = Svc.Data.Excel.GetSheet<ItemRepairResource>();
            foreach (var dm in repairResources)
            {
                if (dm.Item.RowId < darkMatterID)
                    continue;

                if (InventoryManager.Instance()->GetInventoryItemCount(dm.Item.RowId) > 0)
                    return true;
            }
            return false;
        }

        internal static int GetNPCRepairPrice()
        {
            var output = 0;
            var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            for (var i = 0; i < equipment->Size; i++)
            {
                var item = equipment->GetInventorySlot(i);
                if (item != null && item->ItemId > 0)
                {
                    double actualCond = Math.Round(item->Condition / (float)300, 2);
                    if (actualCond < 100)
                    {
                        var lvl = LuminaSheets.ItemSheet[item->ItemId].LevelEquip;
                        var condDif = (100 - actualCond) / 100;
                        var price = Math.Round(Svc.Data.GetExcelSheet<ItemRepairPrice>().GetRow(lvl).Unknown0 * condDif, 0, MidpointRounding.ToPositiveInfinity);
                        output += (int)price;
                    }
                }
            }

            return output;
        }

        internal static int GetMinEquippedPercent()
        {
            ushort ret = ushort.MaxValue;
            var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            for (var i = 0; i < equipment->Size; i++)
            {
                var item = equipment->GetInventorySlot(i);
                if (item != null && item->ItemId > 0)
                {
                    if (item->Condition < ret) ret = item->Condition;
                }
            }
            return (int)Math.Ceiling((double)ret / 300);
        }

        internal static bool CanRepairAny(int repairPercent = 0)
        {
            var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            for (var i = 0; i < equipment->Size; i++)
            {
                var item = equipment->GetInventorySlot(i);
                if (item != null && item->ItemId > 0)
                {
                    if (CanRepairItem(item->ItemId) && item->Condition / 300 < (repairPercent > 0 ? repairPercent : 100))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static bool CanRepairItem(uint ItemId)
        {
            var item = LuminaSheets.ItemSheet[ItemId];

            if (item.ClassJobRepair.RowId > 0)
            {
                var actualJob = (Job)(item.ClassJobRepair.RowId);
                var repairItem = item.ItemRepair.Value.Item;

                if (!HasDarkMatterOrBetter(repairItem.RowId))
                    return false;

                var jobLevel = CharacterInfo.JobLevel(actualJob);
                if (Math.Max(item.LevelEquip - 10, 1) <= jobLevel)
                    return true;
            }

            return false;
        }

        internal static bool RepairNPCNearby(out IGameObject npc)
        {
            npc = null;
            if (Svc.ClientState.LocalPlayer != null)
            {
                foreach (var obj in Svc.Objects.Where(x => x.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventNpc))
                {
                    if (Svc.Data.Excel.GetSheet<ENpcBase>().TryGetRow(obj.DataId, out var enpcsheet))
                    {
                        if (enpcsheet.ENpcData.Any(x => x.RowId == 720915))
                        {
                            var npcDistance = Vector3.Distance(obj.Position, Svc.ClientState.LocalPlayer.Position);
                            if (npcDistance > 7)
                                continue;

                            npc = obj;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        internal static bool RepairWindowOpen()
        {
            if (TryGetAddonByName<AddonRepair>("Repair", out var repairAddon))
                return true;

            return false;
        }
        internal static bool InteractWithRepairNPC()
        {
            if (RepairNPCNearby(out IGameObject npc))
            {
                TargetSystem.Instance()->OpenObjectInteraction(npc.Struct());
                if (TryGetAddonByName<AddonSelectIconString>("SelectIconString", out var addonSelectIconString))
                {
                    var index = GenericHelpers.IndexOf(Svc.Data.Excel.GetSheet<ENpcBase>().GetRow(npc.DataId).ENpcData, x => x.RowId == 720915);
                    Callback.Fire(&addonSelectIconString->AtkUnitBase, true, index);
                }

                if (TryGetAddonByName<AddonRepair>("AddonRepair", out var addonRepair))
                {
                    return true;
                }

            }
            return false;
        }

        private static DateTime _nextRetry;

        internal static bool ProcessRepair(NewCraftingList? CraftingList = null)
        {
            int repairPercent = CraftingList != null ? CraftingList.RepairPercent : P.Config.RepairPercent;
            if (GetMinEquippedPercent() >= repairPercent)
            {
                if (TryGetAddonByName<AddonRepair>("Repair", out var r) && r->AtkUnitBase.IsVisible)
                {
                    if (DateTime.Now < _nextRetry) return false;
                    if (!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied39])
                    {
                        if (DebugTab.Debug) Svc.Log.Verbose("Repair visible");
                        if (DebugTab.Debug) Svc.Log.Verbose("Closing repair window");
                        ActionManagerEx.UseRepair();
                    }
                    _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
                    return false;
                }
                return true;
            }

            if (DateTime.Now < _nextRetry) return false;

            if (TryGetAddonByName<AddonRepair>("Repair", out var repairAddon) && repairAddon->AtkUnitBase.IsVisible && repairAddon->RepairAllButton != null)
            {
                if (!repairAddon->RepairAllButton->IsEnabled)
                {
                    ActionManagerEx.UseRepair();
                    _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
                    return false;
                }

                if (!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied39])
                {
                    ConfirmYesNo();
                    Repair();
                }
                _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
                return false;
            }

            if (P.Config.PrioritizeRepairNPC || !CanRepairAny())
            {
                if (RepairNPCNearby(out var npc) && InventoryManager.Instance()->GetInventoryItemCount(1) >= GetNPCRepairPrice() && !RepairWindowOpen())
                {
                    InteractWithRepairNPC();
                    _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
                    return false;
                }
            }

            if (CanRepairAny())
            {
                if (!PreCrafting.Occupied() && !RepairWindowOpen())
                {
                    ActionManagerEx.UseRepair();
                }
                _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
                return false;
            }

            if (Endurance.Enable && P.Config.DisableEnduranceNoRepair)
            {
                Endurance.ToggleEndurance(false);
                DuoLog.Warning($"Endurance has stopped due to being unable to repair.");
                _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
                return false;
            }

            if (CraftingListUI.Processing && P.Config.DisableListsNoRepair)
            {
                CraftingListFunctions.Paused = true;
                DuoLog.Warning($"List has been paused due to being unable to repair.");
                _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
                return false;
            }

            return true;
        }
    }
}
