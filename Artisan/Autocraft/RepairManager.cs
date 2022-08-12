using ClickLib.Clicks;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ECommons.GenericHelpers;

namespace Artisan.Autocraft
{
    internal unsafe class RepairManager
    {
        internal static void Repair()
        {
            if (TryGetAddonByName<AddonRepair>("Repair", out var addon) && addon->AtkUnitBase.IsVisible && addon->RepairAllButton->IsEnabled && Throttler.Throttle(500))
            {
                new ClickRepair((IntPtr)addon).RepairAll();
            }
        }

        internal static void ConfirmYesNo()
        {
            if(TryGetAddonByName<AddonRepair>("Repair", out var r) && r->AtkUnitBase.IsVisible && TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) && addon->AtkUnitBase.IsVisible && addon->YesButton->IsEnabled && addon->AtkUnitBase.UldManager.NodeList[15]->GetAsAtkTextNode()->NodeText.ToString().StartsWith("Repair as many of the displayed items as") && Throttler.Throttle(500))
            {
                new ClickSelectYesNo((IntPtr)addon).Yes();
            }
        }

        internal static int GetMinEquippedPercent()
        {
            var ret = int.MaxValue;
            var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
            for(var i  = 0; i < equipment->Size; i++)
            {
                var item = equipment->GetInventorySlot(i);
                if (item->Condition < ret) ret = item->Condition;
            }
            return (ret / 300);
        }

        internal static bool ProcessRepair(bool use = true)
        {
            if(GetMinEquippedPercent() >= Service.Configuration.RepairPercent)
            {
                if (TryGetAddonByName<AddonRepair>("Repair", out var r) && r->AtkUnitBase.IsVisible)
                {
                    if (Throttler.Throttle(500))
                    {
                        CommandProcessor.ExecuteThrottled("/generalaction repair");
                    }
                    return false;
                }
                return true;
            }
            else
            {
                if (use)
                {
                    if (TryGetAddonByName<AddonRepair>("Repair", out var r) && r->AtkUnitBase.IsVisible)
                    {
                        ConfirmYesNo();
                        Repair();
                    }
                    else
                    {
                        if (Throttler.Throttle(500))
                        {
                            CommandProcessor.ExecuteThrottled("/generalaction repair");
                        }
                    }
                }
                return false;
            }
        }
    }
}
