using Artisan.Autocraft;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            if (TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var addon) && addon->AtkUnitBase.IsVisible)
            {
                return;
            }
            CommandProcessor.ExecuteThrottled("/clog");
        }

        public unsafe static void OpenRecipeByID(uint recipeID)
        {
            if (TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var addon) && addon->AtkUnitBase.IsVisible) 
            {
                AgentRecipeNote.Instance()->OpenRecipeByRecipeIdInternal(recipeID);
            }
        }
    }
}
