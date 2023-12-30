using Artisan.Autocraft;
using Artisan.RawInformation;
using ClickLib.Clicks;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace Artisan.GameInterop;

public static unsafe class Operations
{
    public unsafe static void RepeatTrialCraft()
    {
        try
        {
            if (Throttler.Throttle(500))
            {
                if (GenericHelpers.TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var recipenote))
                {
                    Callback.Fire(&recipenote->AtkUnitBase, true, 10);
                    Endurance.Tasks.Clear();
                }
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "RepeatTrialCraft");
        }
    }

    public unsafe static void QuickSynthItem(int crafts)
    {
        try
        {
            var recipeWindow = Svc.GameGui.GetAddonByName("RecipeNote", 1);
            if (recipeWindow == nint.Zero)
                return;

            GenericHelpers.TryGetAddonByName<AddonRecipeNoteFixed>("RecipeNote", out var addon);

            if (!int.TryParse(addon->SelectedRecipeQuantityCraftableFromMaterialsInInventory->NodeText.ToString(), out int trueNumberCraftable) || trueNumberCraftable == 0)
            {
                return;
            }

            var addonPtr = (AddonRecipeNote*)recipeWindow;
            if (addonPtr == null)
                return;

            try
            {
                if (Throttler.Throttle(100))
                {
                    PreCrafting._clickQuickSynthesisButtonHook?.Disable();
                    ClickRecipeNote.Using(recipeWindow).QuickSynthesis();
                    PreCrafting._clickQuickSynthesisButtonHook?.Enable();
                  
                    var quickSynthPTR = Svc.GameGui.GetAddonByName("SynthesisSimpleDialog", 1);
                    if (quickSynthPTR == nint.Zero)
                        return;

                    var quickSynthWindow = (AtkUnitBase*)quickSynthPTR;
                    if (quickSynthWindow == null)
                        return;

                    var values = stackalloc AtkValue[2];
                    values[0] = new()
                    {
                        Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int,
                        Int = Math.Min(trueNumberCraftable, Math.Min(crafts, 99)),
                    };
                    values[1] = new()
                    {
                        Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Bool,
                        Byte = 1,
                    };

                    Callback.Fire(quickSynthWindow, true, values[0], values[1]);

                    Crafting.Update();
                }
            }
            catch (Exception e)
            {
                e.Log();
            }

        }
        catch (Exception ex)
        {
            ex.Log();
        }
    }

    public unsafe static void CloseQuickSynthWindow()
    {
        try
        {
            var quickSynthPTR = Svc.GameGui.GetAddonByName("SynthesisSimple", 1);
            if (quickSynthPTR == nint.Zero)
                return;

            var quickSynthWindow = (AtkUnitBase*)quickSynthPTR;
            if (quickSynthWindow == null)
                return;

            var qsynthButton = (AtkComponentButton*)quickSynthWindow->UldManager.NodeList[2];
            AtkResNodeFunctions.ClickButton(quickSynthWindow, qsynthButton, 0);
        }
        catch (Exception e)
        {
            e.Log();
        }
    }

    public unsafe static bool RepeatActualCraft()
    {
        var addon = (AddonRecipeNote*)Svc.GameGui.GetAddonByName("RecipeNote");
        if (addon == null)
            return false;

        Svc.Log.Debug($"Starting actual craft");
        Callback.Fire(&addon->AtkUnitBase, true, 8);
        Endurance.Tasks.Clear();
        return true;
    }
}
