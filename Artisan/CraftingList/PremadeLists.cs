using Artisan.RawInformation;
using Artisan.UI;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RecipeNotebookList = Lumina.Excel.Sheets.RecipeNotebookList;

namespace Artisan.CraftingLists
{
    internal class PremadeLists
    {
        public ListFolders? PremadesUI;
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
            Task.Run(() =>
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
                        list.Name = $"{questCats.First().JournalGenre.Value.Name} — {quest.Name}  — Lv.{quest.ClassJobLevel.First().ToString("00")}";
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

                needToUpdate |= BuildOtherClassLists(PremadeCraftingLists, 1321, 1343, "Studium Deliveries");
                needToUpdate |= BuildOtherClassLists(PremadeCraftingLists, 1113, 1135, "Crystarium Deliveries");
                needToUpdate |= BuildOtherClassLists(PremadeCraftingLists, 1465, 1487, "Wachu Deliveries");

                int premadeCountBefore = PremadeCraftingLists.Count;
                RelicToolPremadeLists.EnsureBuilt(PremadeCraftingLists);
                if (PremadeCraftingLists.Count > premadeCountBefore)
                {
                    needToUpdate = true;
                }

                if (needToUpdate)
                {
                    TryWriteToFile();
                }
                Svc.Log.Debug($"Adding {PremadeCraftingLists.Count()} premade lists.");
                var classQuests = PremadeCraftingLists.Where(x => x.ID < 800_000).ToList();
                var others = PremadeCraftingLists.Where(x => x.ID >= 800_000).OrderBy(x => x.ID).ToList();
                PremadeCraftingLists.Clear();
                PremadeCraftingLists.AddRange(classQuests);
                PremadeCraftingLists.AddRange(others);
                PremadesUI = new(PremadeCraftingLists, true);
            });
        }

        private bool BuildOtherClassLists(List<NewCraftingList> premadeCraftingLists, uint minRow, uint maxRow, string label)
        {
            uint baseId = 800_000;
            bool added = false; 
            for (uint i = minRow; i <= maxRow; i++)
            {
                if (premadeCraftingLists.Any(x => x.ID == (int)(baseId + i)))
                {
                    Svc.Log.Debug($"Premade list for Studium Quest {i} already exists, skipping.");
                    continue;
                }

                var rowFound = Svc.Data.GetExcelSheet<RecipeNotebookList>().TryGetRow(i, out var recipes);

                if (rowFound)
                {
                    var crafter = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(LuminaSheets.ClassJobSheet[recipes.Recipe.First().Value.CraftType.RowId + 8].Name.ToString());
                    var list = new NewCraftingList
                    {
                        ID = (int)(baseId + i),
                        Locked = true,
                        Name = $"{label} — {crafter}",
                        IsPremade = true
                    };

                    var recipesToAdd = new List<ListItem>();
                    foreach (var recipe in recipes.Recipe.Where(x => x.IsValid && x.RowId != 0))
                    {
                        Svc.Log.Debug($"Adding {recipe.Value.ItemResult.Value.Name} to {list.Name}");
                        CraftingListUI.AddAllSubcrafts(recipe.Value, list, recipe.Value.ItemResult.Value.IsCollectable ? 6 : 1);
                        recipesToAdd.Add(new ListItem()
                        {
                            ID = recipe.RowId,
                            Quantity = recipe.Value.ItemResult.Value.IsCollectable ? 6 : 1
                        });
                    }

                    foreach (var item in recipesToAdd)
                    {
                        list.Recipes.Add(item);
                    }

                    list.Locked = false;
                    PremadeCraftingLists.Add(list);
                }

                added = true;
            }
            return added;
        }

        private void TryWriteToFile()
        {
            var file = new FileInfo(Path.Combine(P.Config.ConfigDirectory.FullName, "PremadeCraftsV2.dat"));
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
            var file = new FileInfo(Path.Combine(P.Config.ConfigDirectory.FullName, "PremadeCraftsV2.dat"));
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
