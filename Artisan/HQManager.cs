using ClickLib.Clicks;
using Dalamud.Logging;
using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan
{
    internal unsafe static class HQManager
    {
        internal static List<int> Data = new();

        internal static bool TryGetCurrent([NotNullWhen(true)]out List<int>? data)
        {
            try
            {
                if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible)
                {
                    data = new();
                    for (var i = 23; i >= 18; i--)
                    {
                        var node = addon->UldManager.NodeList[i]->GetAsAtkComponentNode();
                        if (node->AtkResNode.IsVisible)
                        {
                            if (int.TryParse(node->Component->UldManager.NodeList[6]->GetAsAtkComponentNode()->Component->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText.ToString(), out var n))
                            {
                                data.Add(n);
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
                    return data.Count > 0;
                }
            }
            catch { }
            data = default;
            return false;
        }

        internal static long NextCheckAt = 0;
        internal static bool RestoreHQData(List<int> data, out bool dataFinalized)
        {
            if(Environment.TickCount64 < NextCheckAt)
            {
                dataFinalized = false;
                return false;
            }
            if(TryGetCurrent(out var currentData) && data.Count == currentData.Count && GenericHelpers.TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible)
            {
                if (currentData.SequenceEqual(data))
                {
                    dataFinalized = true;
                    return true;
                }
                else
                {
                    for(var i = 0; i < data.Count; i++)
                    {
                        if (data[i] > currentData[i])
                        {
                            var node = addon->UldManager.NodeList[23 - i]->GetAsAtkComponentNode();
                            if (node->AtkResNode.IsVisible)
                            {
                                NextCheckAt = Environment.TickCount64 + 100;
                                ClickRecipeNote.Using((IntPtr)addon).Material(i, true);
                                PluginLog.Debug($"Setting HQ {i}");
                                dataFinalized = false;
                                return true;
                            }
                        }
                        else if (data[i] < currentData[i])
                        {
                            var node = addon->UldManager.NodeList[23 - i]->GetAsAtkComponentNode();
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
                }
            }
            dataFinalized = false;
            return false;
        }
    }
}
