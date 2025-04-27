using Artisan.Autocraft;
using Artisan.CraftingLogic;
using Artisan.GameInterop;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.Sheets;
using OtterGui;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Artisan.CraftingLists
{
    internal class CraftingListUI
    {
        internal static string Search = string.Empty;
        public static unsafe InventoryManager* invManager = InventoryManager.Instance();
        public static ConcurrentDictionary<Recipe, bool> CraftableItems = new();
        internal static ConcurrentDictionary<int, int> SelectedRecipeRawIngredients = new();
        internal static bool keyboardFocus = true;
        internal static string newListName = string.Empty;
        internal static NewCraftingList selectedList = new();
        internal static List<uint> jobs = new();
        internal static List<int> rawIngredientsList = new();
        internal static ConcurrentDictionary<int, int> subtableList = new();
        internal static List<int> listMaterials = new();
        internal static ConcurrentDictionary<int, int> listMaterialsNew = new();
        public static bool Processing;
        public static uint CurrentProcessedItem;
        public static int CurrentProcessedItemIndex;
        public static int CurrentProcessedItemCount;
        public static int CurrentProcessedItemListCount;
        private static readonly ListFolders ListsUI = new();

        private static bool GatherBuddy => DalamudReflector.TryGetDalamudPlugin("GatherBuddy", out var gb, false, true);
        private static bool ItemVendor => DalamudReflector.TryGetDalamudPlugin("Item Vendor Location", out var ivl, false, true);

        private static bool MonsterLookup => DalamudReflector.TryGetDalamudPlugin("Monster Loot Hunter", out var mlh, false, true);

        internal static void Draw()
        {
            ImGui.TextWrapped($"Crafting lists are a fantastic way to queue up different crafts and have them craft one-by-one. Create a list by importing from Teamcraft using the button at the bottom, or click the '+' icon and give your list a name." +
                              $" You can also right click an item from the game's recipe menu to either add it to a new list if one is not selected, or to create a new list with it as the first item if a list is not selected.");

            ImGui.Dummy(new Vector2(0, 14f));
            ImGui.TextWrapped("Left click a list to open the editor. Right click a list to select it without opening the editor.");

            ImGui.Separator();

            DrawListOptions();
            ImGui.Spacing();
        }

        private static void DrawListOptions()
        {
            ImGui.BeginChild("ListsSelector", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 200f));
            ListsUI.Draw(ImGui.GetContentRegionAvail().X);
            ImGui.EndChild();

            ImGui.BeginChild("ListButtons", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 95f));
            if (selectedList.ID != 0)
            {
                if (Endurance.Enable || Processing)
                    ImGui.BeginDisabled();

                if (ImGui.Button("Start Crafting List", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                {
                    StartList();
                }

                if (RetainerInfo.ATools)
                {
                    if (RetainerInfo.TM.IsBusy)
                    {
                        if (ImGui.Button("Abort Collecting From Retainer", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                        {
                            RetainerInfo.TM.Abort();
                        }
                    }
                    else
                    {
                        bool disable = !Player.Available ? false : RetainerInfo.GetReachableRetainerBell() == null;
                        using (ImRaii.Disabled(disable))
                        {
                            if (ImGui.Button("Restock Inventory From Retainers", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                            {
                                Task.Run(() => RetainerInfo.RestockFromRetainers(selectedList));
                            }
                        }
                    }
                }
                else
                {
                    if (!RetainerInfo.AToolsInstalled)
                        ImGuiEx.TextCentered(ImGuiColors.DalamudYellow, $"Please install Allagan Tools for retainer features.");

                    if (RetainerInfo.AToolsInstalled && !RetainerInfo.AToolsEnabled)
                        ImGuiEx.TextCentered(ImGuiColors.DalamudYellow, $"Please enable Allagan Tools for retainer features.");
                }


                if (Endurance.Enable || Processing)
                    ImGui.EndDisabled();
            }

            if (ImGui.Button("Import List From Clipboard (Artisan Export)", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
            {
                try
                {
                    var clipboard = ImGui.GetClipboardText();
                    if (clipboard != string.Empty)
                    {
                        if (clipboard.TryParseJson<NewCraftingList>(out var import))
                        {
                            import.SetID();
                            import.Save(true);
                        }
                        else
                        {
                            Notify.Error("Invalid import string.");
                        }
                    }
                    else
                    {
                        Notify.Error("Clipboard is empty.");
                    }
                }
                catch (Exception ex)
                {
                    ex.Log();
                }
            }


            ImGui.EndChild();

            ImGui.BeginChild("TeamCraftSection", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 5f), false);
            Teamcraft.DrawTeamCraftListButtons();
            ImGui.EndChild();
        }

        public static void StartList()
        {
            CraftingListFunctions.Materials = null;
            CraftingListFunctions.CurrentIndex = 0;
            selectedList.ExpandedList.Clear();
            foreach (var r in selectedList.Recipes)
            {
                if (r.ListItemOptions == null)
                {
                    r.ListItemOptions = new();
                    P.Config.Save();
                }
                if (r.ListItemOptions.Skipping) continue;
                selectedList.ExpandedList.AddRange(Enumerable.Repeat(r.ID, r.Quantity));
            }

            if (P.ws.Windows.FindFirst(x => x.WindowName.Contains(selectedList.ID.ToString(), StringComparison.CurrentCultureIgnoreCase), out var window))
                window.IsOpen = false;


            CraftingListFunctions.ListEndTime = GetListTimer(selectedList);
            Crafting.CraftFinished += UpdateListTimer;
            Processing = true;
            Endurance.ToggleEndurance(false);
        }

        public static void UpdateListTimer(Recipe recipe, CraftState craft, StepState finalStep, bool cancelled)
        {
            Task.Run(() =>
            {
                TimeSpan output = new();
                for (int i = CraftingListFunctions.CurrentIndex; i < selectedList.ExpandedList.Count; i++)
                {
                    var item = selectedList.ExpandedList[i];
                    var options = selectedList.Recipes.First(x => x.ID == item).ListItemOptions;
                    output = output.Add(GetCraftDuration(item, (options?.NQOnly ?? false))).Add(TimeSpan.FromSeconds(1));
                }

                CraftingListFunctions.ListEndTime = output;
            });
        }

        public static TimeSpan GetListTimer(NewCraftingList selectedList)
        {
            TimeSpan output = new();
            try
            {
                if (selectedList is null) return output;
                foreach (var item in selectedList.Recipes.Distinct())
                {
                    if (item.ListItemOptions is null)
                    {
                        item.ListItemOptions = new();
                        P.Config.Save();
                    }
                    if (item.ListItemOptions.Skipping) continue;
                    var count = item.Quantity;
                    var options = item.ListItemOptions;
                    if (GetCraftDuration(item.ID, (options?.NQOnly ?? false)) == TimeSpan.Zero)
                        output = TimeSpan.Zero;
                    else
                        output = output.Add(GetCraftDuration(item.ID, (options?.NQOnly ?? false)) * count).Add(TimeSpan.FromSeconds(1 * count));
                }

                return output;
            }
            catch (Exception ex)
            {
                return output;
            }
        }

        public static TimeSpan GetCraftDuration(uint recipeId, bool qs)
        {
            if (qs)
                return TimeSpan.FromSeconds(3);

            var recipe = LuminaSheets.RecipeSheet[recipeId];
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipe.RowId) ?? new();
            var stats = CharacterStats.GetBaseStatsForClassHeuristic(Job.CRP + recipe.CraftType.RowId);
            stats.AddConsumables(new(config.RequiredFood, config.RequiredFoodHQ), new(config.RequiredPotion, config.RequiredPotionHQ), CharacterInfo.FCCraftsmanshipbuff);
            var craft = Crafting.BuildCraftStateForRecipe(stats, Job.CRP + recipe.CraftType.RowId, recipe);
            var solver = CraftingProcessor.GetSolverForRecipe(config, craft).CreateSolver(craft);
            if (solver != null)
            {
                var time = SolverUtils.EstimateCraftTime(solver, craft, 0);

                return time;
            }
            return TimeSpan.Zero;
        }

        private static void DrawNewListPopup()
        {
            if (ImGui.BeginPopup("NewCraftingList"))
            {
                if (keyboardFocus)
                {
                    ImGui.SetKeyboardFocusHere();
                    keyboardFocus = false;
                }

                if (ImGui.InputText("List Name###listName", ref newListName, 100, ImGuiInputTextFlags.EnterReturnsTrue) && newListName.Any())
                {
                    NewCraftingList newList = new();
                    newList.Name = newListName;
                    newList.SetID();
                    newList.Save(true);

                    newListName = string.Empty;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        public static void AddAllSubcrafts(Recipe selectedRecipe, NewCraftingList selectedList, int amounts = 1, int loops = 1)
        {
            foreach (var subItem in selectedRecipe.Ingredients().Where(x => x.Amount > 0))
            {
                var subRecipe = CraftingListHelpers.GetIngredientRecipe(subItem.Item.RowId);
                if (subRecipe != null)
                {
                    AddAllSubcrafts(subRecipe.Value, selectedList, subItem.Amount * amounts, loops);

                    var quant = Math.Ceiling(subItem.Amount / (double)subRecipe.Value.AmountResult * loops * amounts);
                    if (selectedList.Recipes.Any(x => x.ID == subRecipe.Value.RowId))
                    {
                        selectedList.Recipes.First(x => x.ID == subRecipe.Value.RowId).Quantity += (int)quant;
                    }
                    else
                    {
                        Svc.Log.Debug($"Adding as new {subRecipe.Value.RowId.NameOfRecipe()}");
                        selectedList.Recipes.Add(new() { ID = subRecipe.Value.RowId, Quantity = (int)quant });
                    }
                }
            }
        }


        private static void AddRecipeIngredientsToList(Recipe? recipe, ref List<int> ingredientList, bool addSubList = true, NewCraftingList? selectedList = null)
        {
            try
            {
                if (recipe == null) return;

                foreach (var ing in recipe.Value.Ingredients().Where(x => x.Amount > 0 && x.Item.RowId != 0))
                {
                    var name = LuminaSheets.ItemSheet[ing.Item.RowId].Name.ToString();
                    CraftingListHelpers.SelectedRecipesCraftable[ing.Item.RowId] = LuminaSheets.RecipeSheet!.Any(x => x.Value.ItemResult.Value.Name.ToDalamudString().ToString() == name);

                    for (int i = 1; i <= ing.Amount; i++)
                    {
                        ingredientList.Add((int)ing.Item.RowId);
                        if (CraftingListHelpers.GetIngredientRecipe(ing.Item.RowId).Value.RowId != 0 && addSubList)
                        {
                            AddRecipeIngredientsToList(CraftingListHelpers.GetIngredientRecipe(ing.Item.RowId), ref ingredientList);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "ERROR");
            }
        }


        public static unsafe bool CheckForIngredients(Recipe recipe, bool fetchFromCache = true, bool checkRetainer = false)
        {
            if (fetchFromCache)
                if (CraftableItems.TryGetValue(recipe, out bool canCraft)) return canCraft;

            foreach (var value in recipe.Ingredients().Where(x => x.Item.RowId != 0 && x.Amount > 0))
            {
                try
                {
                    int? invNumberNQ = invManager->GetInventoryItemCount(value.Item.RowId);
                    int? invNumberHQ = invManager->GetInventoryItemCount(value.Item.RowId, true);

                    if (!checkRetainer)
                    {
                        if (value.Amount > (invNumberNQ + invNumberHQ))
                        {
                            invNumberHQ = null;
                            invNumberNQ = null;

                            CraftableItems[recipe] = false;
                            return false;
                        }
                    }
                    else
                    {
                        int retainerCount = RetainerInfo.GetRetainerItemCount(value.Item.RowId);
                        if (value.Amount > (invNumberNQ + invNumberHQ + retainerCount))
                        {
                            invNumberHQ = null;
                            invNumberNQ = null;

                            CraftableItems[recipe] = false;
                            return false;
                        }
                    }

                    invNumberHQ = null;
                    invNumberNQ = null;
                }
                catch
                {

                }

            }

            CraftableItems[recipe] = true;
            return true;
        }

        public static unsafe int NumberOfIngredient(uint ingredient)
        {
            try
            {
                var invNumberNQ = invManager->GetInventoryItemCount(ingredient, false, false, false);
                var invNumberHQ = invManager->GetInventoryItemCount(ingredient, true, false, false);

                if (LuminaSheets.ItemSheet[ingredient].AlwaysCollectable)
                {
                    var inventories = new List<InventoryType>
                                          {
                                              InventoryType.Inventory1,
                                              InventoryType.Inventory2,
                                              InventoryType.Inventory3,
                                              InventoryType.Inventory4,
                                          };

                    foreach (var inv in inventories)
                    {
                        var container = invManager->GetInventoryContainer(inv);
                        for (int i = 0; i < container->Size; i++)
                        {
                            var item = container->GetInventorySlot(i);

                            if (item->ItemId == ingredient)
                                invNumberNQ++;
                        }
                    }
                }

                return invNumberHQ + invNumberNQ;
            }
            catch
            {
                return 0;
            }
        }




        public static Recipe? GetIngredientRecipe(string ingredient)
        {
            return LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Value.Name.ToDalamudString().ToString() == ingredient) ? LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Value.Name.ToDalamudString().ToString() == ingredient) : null;
        }
    }
}
