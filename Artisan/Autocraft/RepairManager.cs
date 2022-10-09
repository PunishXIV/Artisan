using Artisan.RawInformation;
using ClickLib.Clicks;
using Dalamud.Logging;
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
            if(TryGetAddonByName<AddonRepair>("Repair", out var r) && 
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
                //PluginLog.Verbose("Condition good");
                if (TryGetAddonByName<AddonRepair>("Repair", out var r) && r->AtkUnitBase.IsVisible)
                {
                    //PluginLog.Verbose("Repair visible");
                    if (Throttler.Throttle(500))
                    {
                        //PluginLog.Verbose("Closing repair window");
                        Hotbars.actionManager->UseAction(ActionType.General, 6);
                    }
                    return false;
                }
                //PluginLog.Verbose("return true");
                return true;
            }
            else
            {
                //PluginLog.Verbose($"Condition bad, condition is {GetMinEquippedPercent()}, config is {Service.Configuration.RepairPercent}");
                if (use)
                {
                    //PluginLog.Verbose($"Doing repair");
                    if (TryGetAddonByName<AddonRepair>("Repair", out var r) && r->AtkUnitBase.IsVisible)
                    {
                        //PluginLog.Verbose($"Repair visible");
                        ConfirmYesNo();
                        Repair();
                    }
                    else
                    {
                        //PluginLog.Verbose($"Repair not visible");
                        if (Throttler.Throttle(500))
                        {
                            //PluginLog.Verbose($"Opening repair");
                            Hotbars.actionManager->UseAction(ActionType.General, 6);
                        }
                    }
                }
                //PluginLog.Verbose($"Returning false");
                return false;
            }
        }
    }
}
