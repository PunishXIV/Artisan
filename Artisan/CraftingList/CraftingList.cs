using Artisan.Autocraft;
using Artisan.CraftingLogic;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
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
                if (Throttler.Throttle(1000))
                {
                    AgentRecipeNote.Instance()->OpenRecipeByRecipeIdInternal(recipeID);
                }
            }
        }

        private static bool HasItemsForRecipe(uint currentProcessedItem)
        {
            var recipe = CraftingListUI.FilteredList[currentProcessedItem];
            if (recipe.RowId == 0) return false;

            return CraftingListUI.CheckForIngredients(recipe);
        }

        internal static void ProcessList(CraftingList selectedList)
        {
            var isCrafting = Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Crafting];
            if (CurrentIndex < selectedList.Items.Count)
            {
                CraftingListUI.CurrentProcessedItem = selectedList.Items[CurrentIndex];
            }
            else
            {
                CraftingListUI.Processing = false;
            }

            var recipe = CraftingListUI.FilteredList[CraftingListUI.CurrentProcessedItem];
            if (!Throttler.Throttle(0))
            {
                return;
            }

            if (HasItemsForRecipe(CraftingListUI.CurrentProcessedItem))
            {
                if (Service.ClientState.LocalPlayer.ClassJob.Id != recipe.CraftType.Value.RowId + 8)
                {
                    if (isCrafting)
                    {
                        CloseCraftingMenu();
                    }

                    SwitchJobGearset(recipe.CraftType.Value.RowId + 8);
                }

                if (!Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Crafting])
                {
                    OpenRecipeByID(CraftingListUI.CurrentProcessedItem);

                    CurrentCraft.RepeatActualCraft();
                }
            }
            else
            {
                CurrentIndex++;
            }

            if (Artisan.CheckIfCraftFinished() && !Artisan.currentCraftFinished)
            { 
                CloseCraftingMenu();
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
