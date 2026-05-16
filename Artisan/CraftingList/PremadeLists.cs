using Artisan.CraftingLists;
using Artisan.CraftingLogic.Solvers;
using Artisan.RawInformation;
using Artisan.UI;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Artisan.CraftingLists
{
    internal class PremadeLists
    {
        public ListFolders PremadesUI;
        public List<QuestRequiredItem> RequiredItems
        {
            get
            {
                return CsvLoader.LoadResource<QuestRequiredItem>(CsvLoader.QuestRequiredItemResourceName, true, out var failed, out var exceptions, Svc.Data.GameData);
            }
        }

        public List<NewCraftingList> PremadeCraftingLists = [];

        public PremadeLists()
        {
            TryLoadFromFile();
            bool needToUpdate = false;
            foreach (var questCats in Svc.Data.GetExcelSheet<Quest>().Where(x => x.JournalGenre.RowId is >= 165 and <= 172).GroupBy(x => x.JournalGenre.RowId).OrderBy(x => x.Key))
            {
                foreach (var quest in questCats.OrderBy(x => x.ClassJobLevel.First()))
                {
                    var reqItems = RequiredItems.Where(x => x.QuestId == quest.RowId);
                    if (!reqItems.Any())
                    {
                        Svc.Log.Debug($"No required items found for {questCats.First().JournalGenre.Value.Name}, skipping.");
                        continue;
                    }

                    if (PremadeCraftingLists.Any(x => x.ID == (int)quest.RowId))
                    {
                        Svc.Log.Debug($"Premade list for {questCats.First().JournalGenre.Value.Name} already exists, skipping.");
                        continue;
                    }

                    var list = new NewCraftingList();
                    list.ID = (int)quest.RowId;
                    list.Locked = true;
                    list.Name = $"{questCats.First().JournalGenre.Value.Name} - {quest.Name} - Lv.{quest.ClassJobLevel.First().ToString("00")}";
                    list.IsPremade = true;

                    foreach (var reqItem in reqItems)
                    {
                        Svc.Log.Debug($"Adding {reqItem.ItemId} to {list.Name}");
                        var recipe = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Value.RowId == reqItem.ItemId && x.CraftType.RowId == quest.JournalGenre.RowId - 165);
                        int actualQuantity = (int)(quest.ClassJobLevel.First() == 5 && quest.RowId != 65791 ? 3 : reqItem.Quantity); //Adjust level 5 quests for all but CUL since source data is wrong.
                        CraftingListUI.AddAllSubcrafts(recipe, list, actualQuantity);
                        list.Recipes.Add(new ListItem()
                        {
                            ID = recipe.RowId,
                            Quantity = actualQuantity
                        });
                    }

                    list.Locked = false;
                    list.Save();
                    needToUpdate = true;
                    PremadeCraftingLists.Add(list);
                }
            }

            if (needToUpdate)
            {
                TryWriteToFile();
            }
            Svc.Log.Debug($"Adding {PremadeCraftingLists.Count()} premade lists.");
            PremadesUI = new(PremadeCraftingLists, true);
        }

        private void TryWriteToFile()
        {
            var file = new FileInfo(Path.Combine(P.Config.ConfigDirectory.FullName, "PremadeCrafts.dat"));
            try
            {
                var json = JsonSerializer.Serialize(PremadeCraftingLists);
                File.WriteAllText(file.FullName, json);
            }
            catch (Exception e)
            {
                Svc.Log.Error($"Error saving premade list cache file \"{file.FullName}\":\n{e}");
            }
        }

        private void TryLoadFromFile()
        {
            var file = new FileInfo(Path.Combine(P.Config.ConfigDirectory.FullName, "PremadeCrafts.dat"));
            if (!file.Exists)
                return;

            try
            {
                Svc.Log.Information("Loading premade list cache from file...");
                try
                {
                    var raw = File.ReadAllText(file.FullName);
                    var json = JsonSerializer.Deserialize<List<NewCraftingList>>(raw) ?? null;
                    PremadeCraftingLists = json ?? new List<NewCraftingList>();
                    if (PremadeCraftingLists.Count == 0)
                    {
                        Svc.Log.Information("No premade lists found in cache file.");
                        return;
                    }

                }
                catch (Exception e)
                {
                    Svc.Log.Error($"Error reading premade list cache file \"{file.FullName}\":\n{e}");
                    return;
                }
            }
            catch (Exception e)
            {
                Svc.Log.Error($"Error reading raphael cache file \"{file.FullName}\":\n{e}");
            }
        }
    }
}
