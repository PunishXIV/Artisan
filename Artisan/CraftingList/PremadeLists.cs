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
        private List<QuestRequiredItem>? _requiredItems = null;
        public List<QuestRequiredItem> RequiredItems
        {
            get
            {
                if (_requiredItems != null)
                {
                    return _requiredItems;
                }
                var list = CsvLoader.LoadResource<QuestRequiredItem>(CsvLoader.QuestRequiredItemResourceName, true, out var failed, out var exceptions, Svc.Data.GameData);
                static string questIdFix(string id) => $"{int.Parse(id) + 65536}";
                List<string[]> fromQst = [
                    // uint ItemId,uint QuestId,uint Quantity,bool IsHq
                    // Crystalline Mean
                    //["27237",questIdFix("3228"),"1","false"],
                    //["27245",questIdFix("3231"),"1","false"],
                    // Studium
                    ["35588",questIdFix("4133"),"6","false"],
                    ["35589",questIdFix("4134"),"6","false"],
                    ["35590",questIdFix("4135"),"6","false"],
                    //["35836",questIdFix("4136"),"1","false"],
                    ["35591",questIdFix("4137"),"6","false"],
                    ["35592",questIdFix("4140"),"6","false"],
                    ["35593",questIdFix("4141"),"6","false"],
                    ["35594",questIdFix("4142"),"6","false"],
                    ["35595",questIdFix("4144"),"6","false"],
                    ["35596",questIdFix("4147"),"6","false"],
                    ["35597",questIdFix("4148"),"6","false"],
                    ["35598",questIdFix("4149"),"6","false"],
                    ["35599",questIdFix("4151"),"6","false"],
                    // Wachumeqimeqi
                    ["43887",questIdFix("4969"),"6","false"],
                    ["43888",questIdFix("4970"),"6","false"],
                    ["43889",questIdFix("4971"),"6","false"],
                    ["43890",questIdFix("4973"),"6","false"],
                    ["43891",questIdFix("4976"),"6","false"],
                    ["43892",questIdFix("4977"),"6","false"],
                    ["43893",questIdFix("4978"),"6","false"],
                    ["43894",questIdFix("4980"),"6","false"],
                    ["43895",questIdFix("4983"),"6","false"],
                    ["43896",questIdFix("4984"),"6","false"],
                    ["43897",questIdFix("4985"),"6","false"],
                    ["43898",questIdFix("4987"),"6","false"],
                ];
                foreach (string[] data in fromQst)
                {
                    QuestRequiredItem questRequiredItem = new();
                    questRequiredItem.FromCsv(data);
                    if (list.Any(x => x.ItemId.Equals(questRequiredItem.ItemId)))
                    {
                        Svc.Log.Debug($"Item is in LuminaSupplemental data: {questRequiredItem.ItemId}");
                        continue;
                    }
                    list.Add(questRequiredItem);
                }
                _requiredItems = list;
                return list;
            }
        }

        public List<NewCraftingList> PremadeCraftingLists = [];

        public PremadeLists()
        {
            TryLoadFromFile();
            bool needToUpdate = false;
            foreach (var questCats in Svc.Data.GetExcelSheet<Quest>().Where(x =>
                x.JournalGenre.RowId is >= 165 and <= 172    // ARR-StB DoH quests
                || x.JournalGenre.RowId is >= 199 and <= 201 // Crystalline Mean DoH quests
                || x.JournalGenre.RowId is >= 205 and <= 207 // Studium DoH quests
                || x.JournalGenre.RowId is >= 211 and <= 213 // Wachumeqimeqi DoH quests
            ).GroupBy(x => x.JournalGenre.RowId).OrderBy(x => x.Key))
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
                        if (quest.JournalGenre.RowId <= 172)
                        {
                            var recipe = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Value.RowId == reqItem.ItemId && x.CraftType.RowId == quest.JournalGenre.RowId - 165);
                            int actualQuantity = (int)(quest.ClassJobLevel.First() == 5 && quest.RowId != 65791 ? 3 : reqItem.Quantity); //Adjust level 5 quests for all but CUL since source data is wrong.
                            CraftingListUI.AddAllSubcrafts(recipe, list, actualQuantity);
                            list.Recipes.Add(new ListItem()
                            {
                                ID = recipe.RowId,
                                Quantity = actualQuantity
                            });
                        }
                        else
                        {
                            foreach (Recipe recipe in LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.RowId == reqItem.ItemId))
                            {
                                list.Name = $"{list.Name} - {recipe.CraftType.ValueNullable?.Name ?? "Unknown"}";
                                if (int.TryParse($"{quest.RowId}{recipe.CraftType.RowId}", out int id))
                                {
                                    list.ID = id;
                                }
                                CraftingListUI.AddAllSubcrafts(recipe, list, (int)reqItem.Quantity);
                                list.Recipes.Add(new ListItem()
                                {
                                    ID = recipe.RowId,
                                    Quantity = (int)reqItem.Quantity
                                });

                                // Create separate lists for the different CraftTypes
                                list.Locked = false;
                                list.Save();
                                needToUpdate = true;
                                PremadeCraftingLists.Add(list);
                                list = new NewCraftingList();
                                list.ID = (int)quest.RowId;
                                list.Locked = true;
                                list.Name = $"{questCats.First().JournalGenre.Value.Name} - {quest.Name} - Lv.{quest.ClassJobLevel.First().ToString("00")}";
                                list.IsPremade = true;
                            }
                        }
                    }

                    if (list.Recipes.Count > 0)
                    {
                        list.Locked = false;
                        list.Save();
                        needToUpdate = true;
                        PremadeCraftingLists.Add(list);
                    }
                }
            }

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
