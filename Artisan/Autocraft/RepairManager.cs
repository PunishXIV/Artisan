using Artisan.CraftingLists;
using Artisan.RawInformation;
using Artisan.TemporaryFixes;
using Artisan.UI;
using ClickLib.Clicks;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using static ECommons.GenericHelpers;

namespace Artisan.Autocraft
{
    internal unsafe class RepairManager
    {
        internal static void Repair()
        {
            if (TryGetAddonByName<AddonRepairFixed>("Repair", out var addon) && addon->AtkUnitBase.IsVisible && addon->RepairAllButton->IsEnabled && Throttler.Throttle(500))
            {
                new ClickRepairFixed((IntPtr)addon).RepairAll();
            }
        }

        internal static void ConfirmYesNo()
        {
            if(TryGetAddonByName<AddonRepairFixed>("Repair", out var r) && 
                r->AtkUnitBase.IsVisible && TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) && 
                addon->AtkUnitBase.IsVisible && 
                addon->YesButton->IsEnabled && 
                addon->AtkUnitBase.UldManager.NodeList[15]->IsVisible && 
                Throttler.Throttle(500))
            {
                new ClickSelectYesNo((IntPtr)addon).Yes();
            }
        }

        internal static int GetMinEquippedPercent()
        {
            ushort ret = ushort.MaxValue;
            var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            for(var i  = 0; i < equipment->Size; i++)
            {
                var item = equipment->GetInventorySlot(i);
                if (item != null && item->ItemID > 0)
                {
                    if (item->Condition < ret) ret = item->Condition;
                }
            }
            return (int)Math.Ceiling((double)ret / 300);
        }

        internal static bool ProcessRepair(bool use = true, CraftingList? CraftingList = null)
        {
            int repairPercent = CraftingList != null ? CraftingList.RepairPercent : P.Config.RepairPercent;
            if (GetMinEquippedPercent() >= repairPercent)
            {
                if (DebugTab.Debug) Svc.Log.Verbose("Condition good");
                if (TryGetAddonByName<AddonRepairFixed>("Repair", out var r) && r->AtkUnitBase.IsVisible)
                {
                    if (DebugTab.Debug) Svc.Log.Verbose("Repair visible");
                    if (Throttler.Throttle(500))
                    {
                        if (DebugTab.Debug) Svc.Log.Verbose("Closing repair window");
                        Hotbars.actionManager->UseAction(ActionType.GeneralAction, 6);
                    }
                    return false;
                }
                if (DebugTab.Debug) Svc.Log.Verbose("return true");
                return true;
            }
            else
            {
                if (DebugTab.Debug) Svc.Log.Verbose($"Condition bad, condition is {GetMinEquippedPercent()}, config is {P.Config.RepairPercent}");
                if (use)
                {
                    if (DebugTab.Debug) Svc.Log.Verbose($"Doing repair");
                    if (TryGetAddonByName<AddonRepairFixed>("Repair", out var r) && r->AtkUnitBase.IsVisible)
                    {
                        //Svc.Log.Verbose($"Repair visible");
                        ConfirmYesNo();
                        Repair();
                    }
                    else
                    {
                        if (DebugTab.Debug) Svc.Log.Verbose($"Repair not visible");
                        if (Throttler.Throttle(500))
                        {
                            if (DebugTab.Debug) Svc.Log.Verbose($"Opening repair");
                            Hotbars.actionManager->UseAction(ActionType.GeneralAction, 6);
                        }
                    }
                }
                if (DebugTab.Debug) Svc.Log.Verbose($"Returning false");
                return false;
            }
        }
    }
}
