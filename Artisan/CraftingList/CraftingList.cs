using Artisan.Autocraft;
using Artisan.CraftingLogic;
using Artisan.RawInformation;
using ClickLib.Clicks;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using static ECommons.GenericHelpers;

namespace Artisan.CraftingLists
{
    public class CraftingList
    {
        public int ID { get; set; } = 0;

        public string Name { get; set; }

        public List<uint> Items { get; set; } = new();

        public Dictionary<uint, ListItemOptions> ListItemOptions { get; set; } = new();
    }

    public class ListItemOptions
    {
        public bool NQOnly { get; set; } = false;
    }
    public static class CraftingListFunctions
    {
        public static int CurrentIndex = 0;
        public static void SetID(this CraftingList list)
        {
            var rng = new Random();
            var proposedRNG = rng.Next(1, 50000);
            while (Service.Configuration.UserMacros.Where(x => x.ID == proposedRNG).Any())
            {
                proposedRNG = rng.Next(1, 50000);
            }
            list.ID = proposedRNG;
        }

        public static bool Save(this CraftingList list, bool isNew = false)
        {
            if (list.Items.Count() == 0 && !isNew) return false;

            Service.Configuration.CraftingLists.Add(list);
            Service.Configuration.Save();
            return true;
        }

        public unsafe static void OpenCraftingMenu()
        {
            Dalamud.Logging.PluginLog.Debug($"{TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var test)}");

            if (!TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var addon))
            {
                if (Throttler.Throttle(1000))
                {
                    CommandProcessor.ExecuteThrottled("/clog");
                }
            }
        }

        public unsafe static void CloseCraftingMenu()
        {
            if (TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var addon) && addon->AtkUnitBase.IsVisible)
            {
                if (Throttler.Throttle(1000))
                {
                    CommandProcessor.ExecuteThrottled("/clog");
                }
            }
        }

        public unsafe static void OpenRecipeByID(uint recipeID)
        {
            if (!TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var addon))
            {
                if (Throttler.Throttle(500))
                {
                    AgentRecipeNote.Instance()->OpenRecipeByRecipeIdInternal(recipeID);
                }
            }
        }

        private static bool HasItemsForRecipe(uint currentProcessedItem)
        {
            var recipe = CraftingListUI.FilteredList[currentProcessedItem];
            if (recipe.RowId == 0) return false;

            return CraftingListUI.CheckForIngredients(recipe, false);
        }

        internal static void ProcessList(CraftingList selectedList)
        {
            var isCrafting = Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Crafting];
            var preparing = Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.PreparingToCraft];

            if (CurrentIndex < selectedList.Items.Count)
            {
                Dalamud.Logging.PluginLog.Verbose($"Current Item: {selectedList.Items[CurrentIndex]}");
                CraftingListUI.CurrentProcessedItem = selectedList.Items[CurrentIndex];
            }
            else
            {
                Dalamud.Logging.PluginLog.Verbose($"End of Index");
                CurrentIndex = 0;
                CraftingListUI.Processing = false;
            }

            var recipe = CraftingListUI.FilteredList[CraftingListUI.CurrentProcessedItem];
            if (!Throttler.Throttle(0))
            {
                return;
            }


            if (!HasItemsForRecipe(CraftingListUI.CurrentProcessedItem) && !isCrafting)
            {
                if (Throttler.Throttle(500))
                {
                    Service.ChatGui.PrintError("Insufficient materials for recipe. Moving on.");
                    var currentRecipe = selectedList.Items[CurrentIndex];
                    while (currentRecipe == selectedList.Items[CurrentIndex])
                    {
                        CurrentIndex++;
                    }
                }
            }

            if (Service.ClientState.LocalPlayer.ClassJob.Id != recipe.CraftType.Value.RowId + 8 && !isCrafting && !preparing)
            {
                if (!SwitchJobGearset(recipe.CraftType.Value.RowId + 8))
                {
                    Service.ChatGui.PrintError($"Gearset not found for {LuminaSheets.ClassJobSheet[recipe.CraftType.Value.RowId + 8].Name.RawString}. Moving on.");
                    CurrentIndex++;
                    return;
                }

            }

            if (Service.ClientState.LocalPlayer.Level < recipe.RecipeLevelTable.Value.ClassJobLevel && Service.ClientState.LocalPlayer.ClassJob.Id == recipe.CraftType.Value.RowId + 8 && !isCrafting && !preparing)
            {
                Service.ChatGui.PrintError("Insufficient level to craft this item. Moving on.");
                CurrentIndex++;
                return;
            }

            if (!isCrafting)
            {
                if (CurrentIndex == 0 || CraftingListUI.CurrentProcessedItem != selectedList.Items[CurrentIndex - 1])
                OpenRecipeByID(CraftingListUI.CurrentProcessedItem);
                SetIngredients(CraftingListUI.CurrentProcessedItem);

                if (selectedList.ListItemOptions.TryGetValue(CraftingListUI.CurrentProcessedItem, out var options) && options.NQOnly)
                {
                    var lastIndex = selectedList.Items.LastIndexOf(CraftingListUI.CurrentProcessedItem);
                    var count = lastIndex - CurrentIndex + 1;
                    if (count >= 99)
                    {
                        CurrentCraft.QuickSynthItem(99);
                    }
                    else
                    {
                        CurrentCraft.QuickSynthItem(count);
                    }

                }
                else
                {
                    CurrentCraft.RepeatActualCraft();
                }
            }

            if (CurrentIndex == 0 || CraftingListUI.CurrentProcessedItem != selectedList.Items[CurrentIndex - 1] || (CurrentCraft.QuickSynthCurrent == CurrentCraft.QuickSynthMax && CurrentCraft.QuickSynthMax > 0))
            {
                if (isCrafting)
                {
                    CloseCraftingMenu();
                }

                if (CurrentCraft.QuickSynthCurrent == CurrentCraft.QuickSynthMax && CurrentCraft.QuickSynthMax > 0)
                {
                    CurrentCraft.CloseQuickSynthWindow();
                }

                if (Artisan.CheckIfCraftFinished())
                {
                    CloseCraftingMenu();
                }
            }

            if (isCrafting)
            {
                if (selectedList.ListItemOptions.TryGetValue(CraftingListUI.CurrentProcessedItem, out var options) && options.NQOnly)
                {
                    var lastIndex = selectedList.Items.LastIndexOf(CraftingListUI.CurrentProcessedItem);
                    var count = lastIndex - CurrentIndex + 1;
                    if (count >= 99)
                    {
                        CurrentCraft.QuickSynthItem(99);
                    }
                    else
                    {
                        CurrentCraft.QuickSynthItem(count);
                    }
                }
                else
                {
                    SetIngredients(CraftingListUI.CurrentProcessedItem);
                    CurrentCraft.RepeatActualCraft();
                }
            }
        }

        private unsafe static void SetIngredients(uint currentProcessedItem)
        {
            if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible)
            {
                for (var i = 0; i <= 5; i++)
                {
                    try
                    {
                        var node = addon->UldManager.NodeList[23 - i]->GetAsAtkComponentNode();
                        if (node is null || !node->AtkResNode.IsVisible)
                        {
                            return;
                        }

                        var setNQ = node->Component->UldManager.NodeList[9]->GetAsAtkComponentNode()->Component->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText.ToString();
                        var setHQ = node->Component->UldManager.NodeList[6]->GetAsAtkComponentNode()->Component->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText.ToString();
                        var setNQint = Convert.ToInt32(setNQ);
                        var setHQint = Convert.ToInt32(setHQ);

                        var nqNodeText = node->Component->UldManager.NodeList[8]->GetAsAtkTextNode();
                        var hqNodeText = node->Component->UldManager.NodeList[5]->GetAsAtkTextNode();
                        var required = node->Component->UldManager.NodeList[15]->GetAsAtkTextNode();

                        int nqMaterials = Convert.ToInt32(nqNodeText->NodeText.ToString().GetNumbers());
                        int hqMaterials = Convert.ToInt32(hqNodeText->NodeText.ToString().GetNumbers());
                        int requiredMaterials = Convert.ToInt32(required->NodeText.ToString().GetNumbers());

                        if ((setHQint + setNQint) == requiredMaterials) continue;

                        for (int m = 0; m <= requiredMaterials && m <= nqMaterials; m++)
                        {
                            ClickRecipeNote.Using((IntPtr)addon).Material(i, false);
                        }

                        for (int m = 0; m <= requiredMaterials && m <= hqMaterials; m++)
                        {
                            ClickRecipeNote.Using((IntPtr)addon).Material(i, true);
                        }
                    }
                    catch
                    {
                        return;
                    }
                }
            }
        }

        private unsafe static bool SwitchJobGearset(uint cjID)
        {
            var gs = GetGearsetForClassJob(cjID);
            if (gs is null) return false;
            
            if (Throttler.Throttle(1000))
            {
                CommandProcessor.ExecuteThrottled($"/gearset change {gs.Value + 1}");
            }
            return true;
        }

        private unsafe static byte? GetGearsetForClassJob(uint cjId)
        {
            var gearsetModule = RaptureGearsetModule.Instance();
            for (var i = 0; i < 100; i++)
            {
                var gearset = gearsetModule->Gearset[i];
                if (gearset == null) continue;
                if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
                if (gearset->ID != i) continue;
                if (gearset->ClassJob == cjId) return gearset->ID;
            }
            return null;
        }
    }
}
