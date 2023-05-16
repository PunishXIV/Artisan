using Artisan.RawInformation;
using ClickLib.Clicks;
using Dalamud.Logging;
using ECommons;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using PluginLog = Dalamud.Logging.PluginLog;

namespace Artisan.Autocraft
{
    internal unsafe static class HQManager
    {
        public static bool InsufficientMaterials = false;
        internal static bool TryGetCurrent([NotNullWhen(true)] out List<int>? hqIngredients)
        {
            try
            {
                if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible)
                {
                    hqIngredients = new();
                    for (var i = 23; i >= 18; i--)
                    {
                        var node = addon->UldManager.NodeList[i]->GetAsAtkComponentNode();
                        if (node->AtkResNode.IsVisible)
                        {
                            if (int.TryParse(node->Component->UldManager.NodeList[6]->GetAsAtkComponentNode()->Component->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText.ToString(), out var n))
                            {
                                hqIngredients.Add(n);
                            }
                            else
                            {
                                return false;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    return hqIngredients.Count > 0;
                }
            }
            catch { }
            hqIngredients = default;
            return false;
        }

        internal static long NextCheckAt = 0;
        internal static bool RestoreHQData(List<int> ingredients, out bool dataFinalized)
        {
            if (Environment.TickCount64 < NextCheckAt)
            {
                InsufficientMaterials = false;
                dataFinalized = false;
                return false;
            }
            if (TryGetCurrent(out var currentData) && ingredients.Count == currentData.Count && GenericHelpers.TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible)
            {
                for (var i = 0; i < ingredients.Count; i++)
                {
                    var node = addon->UldManager.NodeList[23 - i]->GetAsAtkComponentNode();
                    var nqNodeText = node->Component->UldManager.NodeList[8]->GetAsAtkTextNode();
                    var hqNodeText = node->Component->UldManager.NodeList[5]->GetAsAtkTextNode();
                    var required = node->Component->UldManager.NodeList[15]->GetAsAtkTextNode();

                    int nqMaterials = Convert.ToInt32(nqNodeText->NodeText.ToString().GetNumbers());
                    int hqMaterials = Convert.ToInt32(hqNodeText->NodeText.ToString().GetNumbers());
                    int requiredMaterials = Convert.ToInt32(required->NodeText.ToString().GetNumbers());

                    if (nqMaterials + hqMaterials < requiredMaterials)
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Insufficient Materials");
                        Handler.Enable = false;
                        InsufficientMaterials = true;
                        dataFinalized = true;
                        DuoLog.Error("Insufficient materials set to craft. Disabling endurance mode.");
                        return false;
                    }


                    if (ingredients[i] > currentData[i])
                    {
                        if (node->AtkResNode.IsVisible)
                        {
                            NextCheckAt = Environment.TickCount64 + 100;
                            ClickRecipeNote.Using((IntPtr)addon).Material(i, true);
                            PluginLog.Debug($"Setting HQ {i}");
                            dataFinalized = false;
                            return true;
                        }
                    }
                    else if (ingredients[i] < currentData[i])
                    {
                        if (node->AtkResNode.IsVisible)
                        {
                            NextCheckAt = Environment.TickCount64 + 100;
                            ClickRecipeNote.Using((IntPtr)addon).Material(i, false);
                            PluginLog.Debug($"Setting NQ {i}");
                            dataFinalized = false;
                            return true;
                        }
                    }
                }

                if (currentData.SequenceEqual(ingredients))
                {
                    dataFinalized = true;
                    return true;
                }

            }
            dataFinalized = false;
            return false;
        }
    }
}
