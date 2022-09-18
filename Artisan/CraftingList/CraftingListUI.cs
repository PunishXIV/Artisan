using Artisan.RawInformation;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Artisan.CraftingLists
{
    internal class CraftingListUI
    {
        internal static Recipe? SelectedRecipe = null;
        internal static string Search = "";
        internal unsafe static InventoryManager* invManager = InventoryManager.Instance();
        internal static Dictionary<Recipe, bool> CraftableItems = new();
        internal static List<int> SelectedRecipeRawIngredients = new();
        internal static Dictionary<int, bool> SelectedRecipesCraftable = new();
        private static bool keyboardFocus = true;
        private static string newListName = String.Empty;
        private static CraftingList selectedList = new();
        private static Dictionary<uint, Recipe> FilteredList = LuminaSheets.RecipeSheet.Values
                    .DistinctBy(x => x.ItemResult.Value.Name.RawString)
                    .OrderBy(x => x.RecipeLevelTable.Value.ClassJobLevel)
                    .ThenBy(x => x.ItemResult.Value.Name.RawString)
                    .ToDictionary(x => x.RowId, x => x);

        private static List<uint> jobs = new();
        private static List<int> rawIngredientsList = new();
        private static List<int> subtableList = new();
        private static uint selectedListItem;
        public static bool Processing = false;
        internal static uint currentItem;

        internal static void Draw()
        {
            ImGui.TextWrapped($"You can use this tab to see what items you can craft with the items in your inventory. You can also use it to create a quick crafting list that Artisan will try and work through.");
            ImGui.TextWrapped($"Please note that due to heavy computational requirements, filtering the recipe list to show only recipes you have ingredients for will not take into account raw ingredients for any crafted items. This may be addressed in the future. For now, it will only look at final ingredients *only* for a given recipe.");
            ImGui.Separator();

            DrawListOptions();
            ImGui.Spacing();
        }

        private static void DrawListOptions()
        {
            if (ImGui.Button("New List"))
            {
                keyboardFocus = true;
                ImGui.OpenPopup("NewCraftingList");
            }

            DrawNewListPopup();

            if (Processing)
            {
                ImGui.Text("Currently processing list...");
                return;
            }

            if (Service.Configuration.CraftingLists.Count > 0)
            {
                ImGui.BeginGroup();
                float longestName = 0;
                foreach (var list in Service.Configuration.CraftingLists)
                {
                    if (ImGui.CalcTextSize($"{list.Name}").Length() > longestName)
                        longestName = ImGui.CalcTextSize($"{list.Name}").Length();
                }

                longestName = Math.Max(150, longestName);
                ImGui.Text("Crafting Lists");
                if (ImGui.BeginChild("###craftListSelector", new Vector2(longestName + 40, 0), true))
                {
                    foreach (CraftingList l in Service.Configuration.CraftingLists)
                    {
                        var selected = ImGui.Selectable($"{l.Name}###list{l.ID}", l.ID == selectedList.ID);

                        if (selected)
                        {
                            selectedList = l;
                        }
                    }
                    ImGui.EndChild();

                }

                if (selectedList.ID != 0)
                {
                    ImGui.SameLine();
                    if (ImGui.BeginChild("###selectedList", new Vector2(0, ImGui.GetContentRegionAvail().Y), false))
                    {
                        ImGui.Text($"Selected List: {selectedList.Name}");
                        if (ImGui.Button("Delete List (Hold Ctrl)") && ImGui.GetIO().KeyCtrl)
                        {
                            Service.Configuration.CraftingLists.Remove(selectedList);

                            Service.Configuration.Save();
                            selectedList = new();
                        }

                        if (selectedList.Items.Count > 0)
                        {
                            ImGui.Columns(2, null, false);
                            ImGui.Text("Current Items");
                            ImGui.Indent();
                            foreach (var item in CollectionsMarshal.AsSpan(selectedList.Items.Distinct().ToList()))
                            {
                                var selected = ImGui.Selectable($"{FilteredList[item].ItemResult.Value.Name.RawString} x{selectedList.Items.Count(x => x == item)}", selectedListItem == item);

                                if (selected)
                                {
                                    selectedListItem = item;
                                }
                            }
                            ImGui.Unindent();
                            ImGui.NextColumn();
                            if (selectedListItem != 0)
                            {
                                ImGui.Text("Options");
                                if (ImGui.Button("Remove Item"))
                                {
                                    selectedList.Items.RemoveAll(x => x == selectedListItem);
                                    selectedListItem = 0;
                                    Service.Configuration.Save();
                                }
                                ImGui.SameLine();
                                if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.PlusCircle))
                                {
                                    selectedList.Items.Add(selectedListItem);
                                }
                                ImGui.SameLine();
                                if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.MinusCircle))
                                {
                                    selectedList.Items.Remove(selectedListItem);
                                }
                            }

                            ImGui.Columns(1, null, false);
                            if (ImGui.Button("Start Crafting List", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                            {
                                Processing = true;
                            }
                        }
                        ImGui.Spacing();
                        ImGui.Separator();
                        DrawRecipeData();

                        ImGui.EndChild();
                    }
                }

            }
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
                    CraftingList newList = new();
                    newList.Name = newListName;
                    newList.SetID();
                    newList.Save(true);

                    newListName = String.Empty;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private static void DrawRecipeData()
        {
            bool showOnlyCraftable = Service.Configuration.ShowOnlyCraftable;

            if (ImGui.Checkbox("Show only recipes you have materials for (toggle to refresh)", ref showOnlyCraftable))
            {
                Service.Configuration.ShowOnlyCraftable = showOnlyCraftable;
                Service.Configuration.Save();
                CraftableItems.Clear();
            }

            string preview = SelectedRecipe is null ? "" : SelectedRecipe.ItemResult.Value.Name.RawString;
            if (ImGui.BeginCombo("Select Recipe", preview))
            {
                DrawRecipes();

                ImGui.EndCombo();
            }

            if (SelectedRecipe != null)
            {
                DrawRecipeOptions();

                if (SelectedRecipeRawIngredients.Count == 0)
                    AddRecipeIngredientsToList(SelectedRecipe, ref SelectedRecipeRawIngredients);

                ImGui.Text($"Raw Ingredients Required");
                DrawRecipeSubTable();

                ImGui.PushItemWidth(-1f);
                if (ImGui.Button("Add to List", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                {
                    selectedList.Items.Add(SelectedRecipe.RowId);
                    Service.Configuration.Save();
                }
            }
        }

        private static void DrawRecipeSubTable()
        {
            if (ImGui.BeginTable("###SubTable", 3, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Ingredient", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Required", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Inventory", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                if (subtableList.Count == 0)
                    subtableList = SelectedRecipeRawIngredients.Distinct().OrderByDescending(x => x).ToList();

                try
                {
                    foreach (var item in CollectionsMarshal.AsSpan(subtableList))
                    {
                        if (LuminaSheets.ItemSheet.TryGetValue((uint)item, out var sheetItem))
                        {
                            if (SelectedRecipesCraftable[item]) continue;
                            ImGui.PushID(item);
                            var name = sheetItem.Name.RawString;
                            var count = SelectedRecipeRawIngredients.Count(x => x == item);
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text($"{name}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{count}");
                            ImGui.TableNextColumn();
                            if (NumberOfIngredient((uint)item) >= count)
                            {
                                var color = ImGuiColors.HealerGreen;
                                color.W -= 0.3f;
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                            }
                            ImGui.Text($"{NumberOfIngredient((uint)item)}");
                            ImGui.PopID();
                        }
                    }
                }
                catch
                {

                }

                ImGui.EndTable();
            }
        }

        private static void AddRecipeIngredientsToList(Recipe? recipe, ref List<int> ingredientList)
        {
            try
            {
                if (recipe == null) return;

                foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0 && x.ItemIngredient != 0))
                {
                    var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                    SelectedRecipesCraftable[ing.ItemIngredient] = FilteredList.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                    for (int i = 1; i <= ing.AmountIngredient; i++)
                    {
                        ingredientList.Add(ing.ItemIngredient);
                    }

                    if (GetIngredientRecipe(ing.ItemIngredient).RowId != 0)
                        AddRecipeIngredientsToList(GetIngredientRecipe(ing.ItemIngredient), ref ingredientList);
                }
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "ERROR");
            }
        }

        private static void DrawRecipes()
        {
            ImGui.Text("Search");
            ImGui.SameLine();
            ImGui.InputText("###RecipeSearch", ref Search, 100);
            if (ImGui.Selectable("", SelectedRecipe == null))
            {
                SelectedRecipe = null;
            }

            if (Service.Configuration.ShowOnlyCraftable && CraftableItems.Count > 0)
            {
                foreach (var recipe in CraftableItems.Where(x => x.Value).Select(x => x.Key).Where(x => x.ItemResult.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)))
                {
                    ImGui.PushID((int)recipe.RowId);
                    var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name.RawString} ({recipe.RecipeLevelTable.Value.ClassJobLevel})", recipe.RowId == SelectedRecipe?.RowId);

                    if (selected)
                    {
                        subtableList.Clear();
                        SelectedRecipeRawIngredients.Clear();
                        SelectedRecipe = recipe;
                    }
                    ImGui.PopID();
                }
            }
            else
            {
                foreach (var recipe in CollectionsMarshal.AsSpan(FilteredList.Values.ToList()))
                {
                    try
                    {
                        if (string.IsNullOrEmpty(recipe.ItemResult.Value.Name.RawString)) continue;
                        if (!recipe.ItemResult.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)) continue;
                        CheckForIngredients(recipe);
                        rawIngredientsList.Clear();
                        var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name.RawString} ({recipe.RecipeLevelTable.Value.ClassJobLevel})", recipe.RowId == SelectedRecipe?.RowId);

                        if (selected)
                        {
                            subtableList.Clear();
                            SelectedRecipeRawIngredients.Clear();
                            SelectedRecipe = recipe;
                        }

                    }
                    catch (Exception ex)
                    {
                        Dalamud.Logging.PluginLog.Error(ex, "DrawRecipeList");
                    }
                }
            }
        }

        private unsafe static bool CheckForIngredients(Recipe recipe)
        {
            if (CraftableItems.TryGetValue(recipe, out bool canCraft)) return canCraft;

            foreach (var value in recipe.UnkData5.Where(x => x.ItemIngredient != 0 && x.AmountIngredient > 0))
            {
                try
                {
                    int? invNumberNQ = invManager->GetInventoryItemCount((uint)value.ItemIngredient);
                    int? invNumberHQ = invManager->GetInventoryItemCount((uint)value.ItemIngredient, true);


                    if (value.AmountIngredient > (invNumberNQ + invNumberHQ))
                    {
                        invNumberHQ = null;
                        invNumberNQ = null;

                        CraftableItems[recipe] = false;
                        return false;
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

        private static bool HasRawIngredients(int itemIngredient, byte amountIngredient)
        {
            if (GetIngredientRecipe(itemIngredient).RowId == 0) return false;

            return CheckForIngredients(GetIngredientRecipe(itemIngredient));

        }

        private unsafe static int NumberOfIngredient(uint ingredient)
        {
            try
            {
                var invNumberNQ = invManager->GetInventoryItemCount(ingredient);
                var invNumberHQ = invManager->GetInventoryItemCount(ingredient, true);

                return invNumberHQ + invNumberNQ;
            }
            catch
            {
                return 0;
            }
        }
        private static void DrawRecipeOptions()
        {
            {
                List<uint> craftingJobs = LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.Name.RawString == SelectedRecipe.ItemResult.Value.Name.RawString).Select(x => x.CraftType.Value.RowId + 8).ToList();
                string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => craftingJobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
                ImGui.Text($"Crafted by: {String.Join(", ", jobstrings)}");
            }
            var ItemsRequired = SelectedRecipe.UnkData5;

            if (ImGui.BeginTable("###RecipeTable", 5, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Ingredient", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Required", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Inventory", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Method", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();
                try
                {
                    foreach (var value in ItemsRequired.Where(x => x.AmountIngredient > 0))
                    {
                        jobs.Clear();
                        string ingredient = LuminaSheets.ItemSheet[(uint)value.ItemIngredient].Name.RawString;
                        Recipe? ingredientRecipe = GetIngredientRecipe(ingredient);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"{ingredient}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"{value.AmountIngredient}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"{NumberOfIngredient((uint)value.ItemIngredient)}");
                        if (NumberOfIngredient((uint)value.ItemIngredient) >= value.AmountIngredient)
                        {
                            var color = ImGuiColors.HealerGreen;
                            color.W -= 0.3f;
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                        }
                        ImGui.TableNextColumn();
                        if (ingredientRecipe is not null)
                        {
                            if (ImGui.Button($"Crafted###search{ingredientRecipe.RowId}"))
                            {
                                SelectedRecipe = ingredientRecipe;
                            }
                        }
                        else
                        {
                            ImGui.Text("Gathered");
                        }
                        ImGui.TableNextColumn();
                        if (ingredientRecipe is not null)
                        {
                            try
                            {
                                jobs.AddRange(FilteredList.Values.Where(x => x.ItemResult.Value.Name.RawString == ingredient).Select(x => x.CraftType.Value.RowId + 8));
                                string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => jobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
                                ImGui.Text(String.Join(", ", jobstrings));
                            }
                            catch (Exception ex)
                            {
                                Dalamud.Logging.PluginLog.Error(ex, "JobStrings");
                            }

                        }
                        else
                        {
                            try
                            {
                                var gatheringItem = LuminaSheets.GatheringItemSheet?.Where(x => x.Value.Item == value.ItemIngredient).FirstOrDefault().Value;
                                if (gatheringItem != null)
                                {
                                    var jobs = LuminaSheets.GatheringPointBaseSheet?.Values.Where(x => x.Item.Any(y => y == gatheringItem.RowId)).Select(x => x.GatheringType).ToList();
                                    List<string> tempArray = new();
                                    if (jobs.Any(x => x.Value.RowId is 0 or 1)) tempArray.Add(LuminaSheets.ClassJobSheet[16].Abbreviation.RawString);
                                    if (jobs.Any(x => x.Value.RowId is 2 or 3)) tempArray.Add(LuminaSheets.ClassJobSheet[17].Abbreviation.RawString);
                                    if (jobs.Any(x => x.Value.RowId is 4 or 5)) tempArray.Add(LuminaSheets.ClassJobSheet[18].Abbreviation.RawString);
                                    ImGui.Text($"{string.Join(", ", tempArray)}");
                                }
                                else
                                {
                                    var spearfish = LuminaSheets.SpearfishingItemSheet?.Where(x => x.Value.Item.Value.RowId == value.ItemIngredient).FirstOrDefault();
                                    if (spearfish != null && spearfish.Value.Value.Item.Value.Name.RawString == ingredient)
                                    {
                                        ImGui.Text($"{LuminaSheets.ClassJobSheet[18].Abbreviation.RawString}");
                                    }
                                }

                            }
                            catch (Exception ex)
                            {
                                Dalamud.Logging.PluginLog.Error(ex, "JobStrings");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dalamud.Logging.PluginLog.Error(ex, "RecipeIngreds");
                }
                ImGui.EndTable();
            }

        }

        public unsafe static void DrawProcessingWindow()
        {
            if (Processing)
            {
                ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
                if (ImGui.Begin("Processing Crafting List", ref Processing, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
                {
                    ImGui.Text($"Now Processing: {selectedList.Name}");
                    ImGui.Separator();
                    ImGui.Spacing();
                    if (currentItem != 0)
                        ImGuiEx.TextV($"Trying to craft: {FilteredList[currentItem].ItemResult.Value.Name.RawString}");

                    if (ImGui.Button("Cancel"))
                    {
                        Processing = false;
                    }

                    if (currentItem != 0) return;

                    foreach (var rec in selectedList.Items)
                    {
                        currentItem = rec;
                        var recipe = FilteredList[currentItem];
                        var task = new BlockingTask();
                        Service.Framework.RunOnTick(CraftingListFunctions.OpenCraftingMenu, TimeSpan.FromSeconds(3));
                        CraftingListFunctions.OpenRecipeByID(recipe.RowId);

                        currentItem = 0;
                    }


                    Processing = false;
                }
            }
        }

        private static Recipe? GetIngredientRecipe(string ingredient)
        {
            return FilteredList.Values.Any(x => x.ItemResult.Value.Name.RawString == ingredient) ? FilteredList.Values.First(x => x.ItemResult.Value.Name.RawString == ingredient) : null;
        }

        private static Recipe GetIngredientRecipe(int ingredient)
        {
            if (FilteredList.Values.Any(x => x.ItemResult.Value.RowId == ingredient))
                return FilteredList.Values.First(x => x.ItemResult.Value.RowId == ingredient);
            else
                return new Recipe() { RowId = 0 };
        }
    }
}
