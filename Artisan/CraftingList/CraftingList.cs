using Artisan.Autocraft;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using ClickLib.Clicks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using static ECommons.GenericHelpers;

namespace Artisan.CraftingLists
{
    public class CraftingList
    {
        public int ID { get; set; }

        public string? Name { get; set; }

        public List<uint> Items { get; set; } = new();

        public Dictionary<uint, ListItemOptions> ListItemOptions { get; set; } = new();

        public bool SkipIfEnough { get; set; }

        public bool Materia { get; set; }

        public bool Repair { get; set; }

        public int RepairPercent = 50;

        public bool AddAsQuickSynth;
    }

    public class ListItemOptions
    {
        public bool NQOnly { get; set; }
        // TODO: custom RecipeConfig?
    }

    public static class CraftingListFunctions
    {
        public static int CurrentIndex;

        public static bool Paused { get; set; } = false;

        public static Dictionary<uint, int>? Materials;

        private static TaskManager CLTM = new();

        public static void SetID(this CraftingList list)
        {
            var rng = new Random();
            var proposedRNG = rng.Next(1, 50000);
            while (P.Config.CraftingLists.Where(x => x.ID == proposedRNG).Any())
            {
                proposedRNG = rng.Next(1, 50000);
            }

            list.ID = proposedRNG;
        }

        public static Dictionary<uint, int> ListMaterials(this CraftingList list)
        {
            var output = new Dictionary<uint, int>();
            foreach (var item in list.Items.Distinct())
            {
                Recipe r = CraftingListHelpers.FilteredList[item];
                CraftingListHelpers.AddRecipeIngredientsToList(r, ref output, false, list);
            }

            return output;
        }

        public static bool Save(this CraftingList list, bool isNew = false)
        {
            if (list.Items.Count == 0 && !isNew) return false;

            list.SkipIfEnough = P.Config.DefaultListSkip;
            list.Materia = P.Config.DefaultListMateria;
            list.Repair = P.Config.DefaultListRepair;
            list.RepairPercent = P.Config.DefaultListRepairPercent;
            list.AddAsQuickSynth = P.Config.DefaultListQuickSynth;

            if (list.AddAsQuickSynth)
            {
                foreach (var item in list.ListItemOptions)
                {
                    item.Value.NQOnly = true;
                }
            }

            P.Config.CraftingLists.Add(list);
            P.Config.Save();
            return true;
        }

        public static unsafe void OpenCraftingMenu()
        {
            if (Crafting.CurState != Crafting.State.IdleNormal) return;

            if (!TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var addon))
            {
                if (Throttler.Throttle(1000))
                {
                    CommandProcessor.ExecuteThrottled("/clog");
                }
            }
        }

        public static unsafe bool RecipeWindowOpen()
        {
            return TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var addon) && addon->AtkUnitBase.IsVisible && RecipeNote.Instance()->RecipeList is var rl && rl != null && rl->Recipes != null;
        }

        public static unsafe void CloseCraftingMenu()
        {
            if (TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var addon) && addon->AtkUnitBase.IsVisible)
            {
                CommandProcessor.ExecuteThrottled("/clog");
            }
        }

        public static unsafe void OpenRecipeByID(uint recipeID, bool skipThrottle = false)
        {
            if (Crafting.CurState != Crafting.State.IdleNormal) return;

            if (!TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var addon))
            {
                if (Throttler.Throttle(500) || skipThrottle)
                {
                    AgentRecipeNote.Instance()->OpenRecipeByRecipeIdInternal(recipeID);
                }
            }
        }

        public static bool HasItemsForRecipe(uint currentProcessedItem)
        {
            if (currentProcessedItem == 0) return false;
            var recipe = CraftingListHelpers.FilteredList[currentProcessedItem];
            if (recipe.RowId == 0) return false;

            return CraftingListUI.CheckForIngredients(recipe, false);
        }

        internal static unsafe void ProcessList(CraftingList selectedList)
        {
            var isCrafting = Svc.Condition[ConditionFlag.Crafting];
            var preparing = Svc.Condition[ConditionFlag.PreparingToCraft];
            Materials ??= selectedList.ListMaterials();

            if (Paused)
            {
                return;
            }

            if (CurrentIndex < selectedList.Items.Count)
            {
                CraftingListUI.CurrentProcessedItem = selectedList.Items[CurrentIndex];
            }
            else
            {
                Svc.Log.Verbose("End of Index");
                CurrentIndex = 0;
                CraftingListUI.Processing = false;
                Operations.CloseQuickSynthWindow();
                CLTM.Enqueue(() => CloseCraftingMenu(), "EndOfListCloseMenu");

                if (P.Config.PlaySoundFinishList)
                Sounds.SoundPlayer.PlaySound();
                return;
            }

            if (QuickCrafting.Max > 0 && (QuickCrafting.Completed || selectedList.Materia && Spiritbond.IsSpiritbondReadyAny() && CharacterInfo.MateriaExtractionUnlocked()))
            {
                Operations.CloseQuickSynthWindow();
            }

            var recipe = CraftingListHelpers.FilteredList[CraftingListUI.CurrentProcessedItem];

            if (!Throttler.Throttle(0))
            {
                return;
            }

            if (recipe.SecretRecipeBook.Row != 0)
            {
                if (!PlayerState.Instance()->IsSecretRecipeBookUnlocked(recipe.SecretRecipeBook.Row))
                {
                    SeString error = new SeString(
                        new TextPayload("You haven't unlocked the recipe book "),
                        new ItemPayload(recipe.SecretRecipeBook.Value.Item.Row),
                        new UIForegroundPayload(1),
                        new TextPayload(recipe.SecretRecipeBook.Value.Name.RawString),
                        RawPayload.LinkTerminator,
                        UIForegroundPayload.UIForegroundOff,
                        new TextPayload(" for this recipe. Moving on."));
                    Svc.Chat.PrintError(error);

                    var currentRecipe = selectedList.Items[CurrentIndex];
                    while (currentRecipe == selectedList.Items[CurrentIndex])
                    {
                        CurrentIndex++;
                        if (CurrentIndex == selectedList.Items.Count)
                            return;
                    }
                }
            }

            if (selectedList.SkipIfEnough &&
                CraftingListUI.NumberOfIngredient(recipe.ItemResult.Value.RowId) >= Materials.FirstOrDefault(x => x.Key == recipe.ItemResult.Row).Value &&
                (preparing || !isCrafting))
            {
                // Probably a final craft, treat like before
                if (Materials!.Count(x => x.Key == recipe.ItemResult.Row) == 0)
                {
                    if (CraftingListUI.NumberOfIngredient(recipe.ItemResult.Value.RowId) >= selectedList.Items.Count(x => CraftingListHelpers.FilteredList[x].ItemResult.Value.Name.RawString == recipe.ItemResult.Value.Name.RawString) * recipe.AmountResult)
                    {
                        if (Throttler.Throttle(500))
                        {
                            var currentRecipe = selectedList.Items[CurrentIndex];
                            while (currentRecipe == selectedList.Items[CurrentIndex])
                            {
                                CurrentIndex++;
                                if (CurrentIndex == selectedList.Items.Count)
                                    return;
                            }

                            return;
                        }

                    }
                }
                else
                {
                    Svc.Log.Debug($"{recipe.RowId.NameOfRecipe()} {CraftingListUI.NumberOfIngredient(recipe.ItemResult.Value.RowId)} {Materials.First(x => x.Key == recipe.ItemResult.Row).Value}");
                    if (Throttler.Throttle(500))
                    {
                        var currentRecipe = selectedList.Items[CurrentIndex];
                        while (currentRecipe == selectedList.Items[CurrentIndex])
                        {
                            CurrentIndex++;
                            if (CurrentIndex == selectedList.Items.Count)
                                return;
                        }

                        return;
                    }
                }
            }

            if (!HasItemsForRecipe(CraftingListUI.CurrentProcessedItem) && (preparing || !isCrafting))
            {
                if (Throttler.Throttle(500))
                {
                    Svc.Chat.PrintError($"Insufficient materials for {recipe.ItemResult.Value.Name.ExtractText()}. Moving on.");
                    var currentRecipe = selectedList.Items[CurrentIndex];

                    while (currentRecipe == selectedList.Items[CurrentIndex])
                    {
                        CurrentIndex++;
                        if (CurrentIndex == selectedList.Items.Count)
                            return;
                    }

                    return;
                }

                return;
            }

            if (Svc.ClientState.LocalPlayer.ClassJob.Id != recipe.CraftType.Value.RowId + 8)
            {
                if (isCrafting && !preparing)
                    return;

                if (preparing)
                {
                    if (Throttler.Throttle(1000))
                    {
                        CloseCraftingMenu();
                    }
                }

                if (!isCrafting)
                {
                    if (!SwitchJobGearset(recipe.CraftType.Value.RowId + 8))
                    {
                        Svc.Chat.PrintError($"Gearset not found for {LuminaSheets.ClassJobSheet[recipe.CraftType.Value.RowId + 8].Name.RawString}. Moving on.");
                        var currentRecipe = selectedList.Items[CurrentIndex];

                        while (currentRecipe == selectedList.Items[CurrentIndex])
                        {
                            CurrentIndex++;
                            if (CurrentIndex == selectedList.Items.Count)
                                return;
                        }

                        return;
                    }
                }

                return;
            }

            if (Svc.ClientState.LocalPlayer.Level < recipe.RecipeLevelTable.Value.ClassJobLevel - 5 && Svc.ClientState.LocalPlayer.ClassJob.Id == recipe.CraftType.Value.RowId + 8 && !isCrafting && !preparing)
            {
                Svc.Chat.PrintError("Insufficient level to craft this item. Moving on.");
                var currentRecipe = selectedList.Items[CurrentIndex];

                while (currentRecipe == selectedList.Items[CurrentIndex])
                {
                    CurrentIndex++;
                    if (CurrentIndex == selectedList.Items.Count)
                        return;
                }

                return;
            }

            if (Svc.Condition[ConditionFlag.Occupied39])
            {
                Throttler.Rethrottle(1000);
            }

            if (!Spiritbond.ExtractMateriaTask(selectedList.Materia, isCrafting, preparing))
                return;

            if (selectedList.Repair && !RepairManager.ProcessRepair(false, selectedList) && ((selectedList.Materia && !Spiritbond.IsSpiritbondReadyAny()) || (!selectedList.Materia) || (!CharacterInfo.MateriaExtractionUnlocked())))
            {
                if (RecipeWindowOpen() && Svc.Condition[ConditionFlag.Crafting])
                {
                    if (Throttler.Throttle(1000))
                    {
                        CloseCraftingMenu();
                    }
                }
                else
                {
                    if (!Svc.Condition[ConditionFlag.Crafting]) RepairManager.ProcessRepair(true, selectedList);
                }

                return;
            }

            selectedList.ListItemOptions.TryAdd(CraftingListUI.CurrentProcessedItem, new ListItemOptions());

            var options = selectedList.ListItemOptions.GetValueOrDefault(CraftingListUI.CurrentProcessedItem);
            var config = /* options?.CustomConfig ?? */ P.Config.RecipeConfigs.GetValueOrDefault(CraftingListUI.CurrentProcessedItem) ?? new();
            if (!ConsumableChecker.CheckConsumables(config, false))
            {
                if (RecipeWindowOpen() && Svc.Condition[ConditionFlag.Crafting])
                {
                    if (Throttler.Throttle(1000))
                    {
                        CloseCraftingMenu();
                    }
                }
                else
                {
                    if (!isCrafting)
                        ConsumableChecker.CheckConsumables(config, true);
                }

                return;
            }

            if (isCrafting && !preparing)
                return;

            if (!isCrafting)
            {
                if (!RecipeWindowOpen())
                {
                    if (Endurance.RecipeID != CraftingListUI.CurrentProcessedItem)
                    {
                        OpenRecipeByID(CraftingListUI.CurrentProcessedItem);
                        return;
                    }
                    else
                    {
                        OpenCraftingMenu();
                        return;
                    }
                }

                if (RecipeWindowOpen())
                {
                    if ((options?.NQOnly ?? false) && recipe.CanQuickSynth && P.ri.HasRecipeCrafted(recipe.RowId))
                    {
                        var lastIndex = selectedList.Items.LastIndexOf(CraftingListUI.CurrentProcessedItem);
                        var count = lastIndex - CurrentIndex + 1;
                        count = CheckWhatExpected(selectedList, recipe, count);

                        if (count >= 99)
                        {
                            Operations.QuickSynthItem(99);
                            return;
                        }
                        else
                        {
                            Operations.QuickSynthItem(count);
                            return;
                        }

                    }
                    else
                    {
                        if (!CLTM.IsBusy)
                        {
                            CLTM.Enqueue(() => SetIngredients(), "SettingIngredients");
                            CLTM.Enqueue(() => Operations.RepeatActualCraft(), "ListCraft");
                            return;
                        }
                    }
                }

            }

            if ((CurrentIndex == 0 && CraftingListUI.CurrentProcessedItem != Endurance.RecipeID)
                || (CurrentIndex > 0 && CraftingListUI.CurrentProcessedItem != selectedList.Items[CurrentIndex - 1] && Endurance.RecipeID != CraftingListUI.CurrentProcessedItem)
                || QuickCrafting.Completed)
            {
                if (RecipeWindowOpen())
                {
                    if (isCrafting)
                    {
                        CloseCraftingMenu();
                        return;
                    }

                    if (Crafting.CurState is Crafting.State.IdleNormal or Crafting.State.IdleBetween)
                    {
                        CloseCraftingMenu();
                        return;
                    }
                }
            }

            if (isCrafting)
            {
                if (RecipeWindowOpen())
                {
                    if ((options?.NQOnly ?? false) && recipe.CanQuickSynth && P.ri.HasRecipeCrafted(recipe.RowId))
                    {
                        var lastIndex = selectedList.Items.LastIndexOf(CraftingListUI.CurrentProcessedItem);
                        var count = lastIndex - CurrentIndex + 1;
                        count = CheckWhatExpected(selectedList, recipe, count);

                        if (count >= 99)
                        {
                            Operations.QuickSynthItem(99);
                            return;
                        }
                        else
                        {
                            Operations.QuickSynthItem(count);
                            return;
                        }
                    }
                    else
                    {
                        if (!CLTM.IsBusy)
                        {
                            if (P.Config.ListCraftThrottle < 0.2f)
                            {
                                CraftingListUI.Processing = false;
                                return;
                            }

                            if (P.Config.ListCraftThrottle > 2)
                            {
                                P.Config.ListCraftThrottle = 2;
                                P.Config.Save();
                            }

                            CLTM.Enqueue(() => SetIngredients(), "SettingIngredients");
                            CLTM.DelayNext("CraftListDelay", (int)(P.Config.ListCraftThrottle * 1000));
                            CLTM.Enqueue(() => { Operations.RepeatActualCraft(); }, "ListCraft");

                            return;
                        }
                    }
                }
            }
        }

        private static int CheckWhatExpected(CraftingList selectedList, Recipe recipe, int count)
        {
            if (selectedList.SkipIfEnough)
            {
                var inventoryitems = CraftingListUI.NumberOfIngredient(recipe.ItemResult.Value.RowId);
                var expectedNumber = 0;
                var stillToCraft = 0;
                var totalToCraft = selectedList.Items.Count(x => CraftingListHelpers.FilteredList[x].ItemResult.Value.Name.RawString == recipe.ItemResult.Value.Name.RawString) * recipe.AmountResult;
                if (Materials!.Count(x => x.Key == recipe.ItemResult.Row) == 0)
                {
                    // var previousCrafted = selectedList.Items.Count(x => CraftingListHelpers.FilteredList[x].ItemResult.Value.Name.RawString == recipe.ItemResult.Value.Name.RawString && selectedList.Items.IndexOf(x) < CurrentIndex) * recipe.AmountResult;
                    stillToCraft = selectedList.Items.Count(x => CraftingListHelpers.FilteredList[x].ItemResult.Value.Name.RawString == recipe.ItemResult.Value.Name.RawString && selectedList.Items.IndexOf(x) >= CurrentIndex) * recipe.AmountResult - inventoryitems;
                    expectedNumber = stillToCraft > 0 ? Math.Min(selectedList.Items.Count(x => x == CraftingListUI.CurrentProcessedItem) * recipe.AmountResult, stillToCraft) : selectedList.Items.Count(x => x == CraftingListUI.CurrentProcessedItem);

                }
                else
                {
                    expectedNumber = Materials!.First(x => x.Key == recipe.ItemResult.Row).Value;
                }

                var difference = Math.Min(totalToCraft - inventoryitems, expectedNumber);
                double numberToCraft = Math.Ceiling((double)difference / recipe.AmountResult);

                count = (int)numberToCraft;
            }

            return count;
        }

        public static unsafe bool SetIngredients(EnduranceIngredients[]? setIngredients = null)
        {
            try
            {
                if (TryGetAddonByName<AddonRecipeNoteFixed>("RecipeNote", out var addon) && 
                    addon->AtkUnitBase.IsVisible && 
                    AgentRecipeNote.Instance() != null && 
                    RaptureAtkModule.Instance()->AtkModule.IsAddonReady(AgentRecipeNote.Instance()->AgentInterface.AddonId))
                {
                    if (setIngredients == null)
                    {
                        for (var i = 0; i <= 5; i++)
                        {
                            try
                            {
                                var node = addon->AtkUnitBase.UldManager.NodeList[23 - i]->GetAsAtkComponentNode();
                                if (node->Component->UldManager.NodeListCount < 16)
                                    return false;

                                if (node is null || !node->AtkResNode.IsVisible)
                                {
                                    return true;
                                }

                                if (node->Component->UldManager.NodeList[11]->IsVisible)
                                {
                                    var ingredient = LuminaSheets.RecipeSheet.Values.Where(x => x.RowId == Endurance.RecipeID).FirstOrDefault().UnkData5[i].ItemIngredient;
                                    Svc.Log.Debug($"{ingredient}");

                                    var btn = node->Component->UldManager.NodeList[14]->GetAsAtkComponentButton();
                                    try
                                    {
                                        btn->ClickAddonButton((AtkComponentBase*)addon, 4);
                                    }
                                    catch(Exception ex)
                                    {
                                        ex.Log();
                                    }
                                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextIconMenu");
                                    if (contextMenu != null)
                                    {
                                        Callback.Fire(contextMenu, true, 0, 0, 0, ingredient, 0);
                                    }
                                }
                                else
                                {
                                    var nqNodeText = node->Component->UldManager.NodeList[8]->GetAsAtkTextNode();
                                    var hqNodeText = node->Component->UldManager.NodeList[5]->GetAsAtkTextNode();
                                    var required = node->Component->UldManager.NodeList[15]->GetAsAtkTextNode();

                                    int nqMaterials = Convert.ToInt32(nqNodeText->NodeText.ToString().GetNumbers());
                                    int hqMaterials = Convert.ToInt32(hqNodeText->NodeText.ToString().GetNumbers());
                                    int requiredMaterials = Convert.ToInt32(required->NodeText.ToString().GetNumbers());

                                    // if ((setHQint + setNQint) == requiredMaterials) continue;
                                    for (int m = 0; m <= requiredMaterials && m <= nqMaterials; m++)
                                    {
                                        ClickRecipeNote.Using((IntPtr)addon).Material(i, false);
                                    }

                                    for (int m = 0; m <= requiredMaterials && m <= hqMaterials; m++)
                                    {
                                        ClickRecipeNote.Using((IntPtr)addon).Material(i, true);
                                    }
                                }

                            }
                            catch
                            {
                                return false;
                            }
                        }

                        return true;
                    }
                    else
                    {
                        for (var i = 0; i <= 5; i++)
                        {
                            try
                            {
                                var node = addon->AtkUnitBase.UldManager.NodeList[23 - i]->GetAsAtkComponentNode();
                                if (node->Component->UldManager.NodeListCount < 16)
                                    return false;

                                if (node is null || !node->AtkResNode.IsVisible)
                                {
                                    return true;
                                }

                                var hqSetButton = node->Component->UldManager.NodeList[6]->GetAsAtkComponentNode();
                                var nqSetButton = node->Component->UldManager.NodeList[9]->GetAsAtkComponentNode();

                                var hqSetText = hqSetButton->Component->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText;
                                var nqSetText = nqSetButton->Component->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText;

                                int hqSet = Convert.ToInt32(hqSetText.ToString().GetNumbers());
                                int nqSet = Convert.ToInt32(nqSetText.ToString().GetNumbers());

                                if (setIngredients.Any(y => y.IngredientSlot == i))
                                {
                                    for (int h = hqSet; h < setIngredients.First(x => x.IngredientSlot == i).HQSet; h++)
                                    {
                                        ClickRecipeNote.Using((IntPtr)addon).Material(i, true);
                                    }

                                    for (int h = nqSet; h < setIngredients.First(x => x.IngredientSlot == i).NQSet; h++)
                                    {
                                        ClickRecipeNote.Using((IntPtr)addon).Material(i, false);
                                    }
                                }
                            }
                            catch
                            {
                                return false;
                            }
                        }

                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool SwitchJobGearset(uint cjID)
        {
            var gs = GetGearsetForClassJob(cjID);
            if (gs is null) return false;

            if (Throttler.Throttle(1000))
            {
                CommandProcessor.ExecuteThrottled($"/gearset change {gs.Value + 1}");
            }

            return true;
        }

        private static unsafe byte? GetGearsetForClassJob(uint cjId)
        {
            var gearsetModule = RaptureGearsetModule.Instance();
            for (var i = 0; i < 100; i++)
            {
                var gearset = gearsetModule->GetGearset(i);
                if (gearset == null) continue;
                if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
                if (gearset->ID != i) continue;
                if (gearset->ClassJob == cjId) return gearset->ID;
            }

            return null;
        }
    }
}
