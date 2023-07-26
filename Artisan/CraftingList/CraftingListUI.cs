using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

using Artisan.Autocraft;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.UI;

using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Logging;

using ECommons.Automation;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using ECommons.StringHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using ImGuiNET;

using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;

namespace Artisan.CraftingLists
{
    internal class CraftingListUI
    {
        internal static Recipe? SelectedRecipe;
        internal static string Search = string.Empty;
        public static unsafe InventoryManager* invManager = InventoryManager.Instance();
        public static Dictionary<Recipe, bool> CraftableItems = new();
        internal static Dictionary<int, int> SelectedRecipeRawIngredients = new();
        internal static bool keyboardFocus = true;
        internal static string newListName = string.Empty;
        internal static CraftingList selectedList = new();
        internal static List<uint> jobs = new();
        internal static List<int> rawIngredientsList = new();
        internal static Dictionary<int, int> subtableList = new();
        internal static List<int> listMaterials = new();
        internal static Dictionary<int, int> listMaterialsNew = new();
        internal static uint selectedListItem;
        public static bool Processing;
        public static uint CurrentProcessedItem;
        private static bool renameMode = false;
        private static string? renameList;
        public static bool Minimized;
        private static int timesToAdd = 1;
        private static readonly ListFolders ListsUI = new();
        private static bool GatherBuddy => DalamudReflector.TryGetDalamudPlugin("GatherBuddy", out var gb, false, true);
        private static bool ItemVendor => DalamudReflector.TryGetDalamudPlugin("Item Vendor Location", out var ivl, false, true);

        private static bool MonsterLookup => DalamudReflector.TryGetDalamudPlugin("Monster Loot Hunter", out var mlh, false, true);
        private static bool TidyAfter = false;

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
            if (Endurance.Enable)
            {
                Processing = false;
                ImGui.Text("Endurance mode enabled...");
                return;
            }

            if (Processing)
            {
                ImGui.Text("Currently processing list...");
                return;
            }

            ImGui.BeginChild("ListsSelector", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 200f));
            ListsUI.Draw(ImGui.GetContentRegionAvail().X);
            ImGui.EndChild();

            ImGui.BeginChild("ListButtons", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 95f));
            if (selectedList.ID != 0)
            {
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
                        if (ImGui.Button("Restock Inventory From Retainers", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                        {
                            RetainerInfo.RestockFromRetainers(selectedList);
                        }
                    }
                }
            }

            if (ImGui.Button("Import List From Clipboard (Artisan Export)", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
            {
                var clipboard = ImGui.GetClipboardText();
                var import = JsonConvert.DeserializeObject<CraftingList>(clipboard.FromBase64());
                if (import != null)
                {
                    import.SetID();
                    import.Save(true);
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
            if (CraftingListFunctions.RecipeWindowOpen() && selectedList.Items[0] != Endurance.RecipeID)
                CraftingListFunctions.CloseCraftingMenu();

            Processing = true;
            Endurance.Enable = false;
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

                    newListName = string.Empty;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        

        public static void AddAllSubcrafts(Recipe selectedRecipe, CraftingList selectedList, int amounts = 1, int loops = 1)
        {
            PluginLog.Debug($"Processing: {selectedRecipe.ItemResult.Value.Name.RawString}");
            foreach (var subItem in selectedRecipe.UnkData5.Where(x => x.AmountIngredient > 0))
            {
                PluginLog.Debug($"Sub-item: {LuminaSheets.ItemSheet[(uint)subItem.ItemIngredient].Name.RawString} * {subItem.AmountIngredient}");
                var subRecipe = CraftingListHelpers.GetIngredientRecipe((uint)subItem.ItemIngredient);
                if (subRecipe != null)
                {
                    AddAllSubcrafts(subRecipe, selectedList, subItem.AmountIngredient * amounts, loops);

                    PluginLog.Debug($"Adding: {subRecipe.ItemResult.Value.Name.RawString} {Math.Ceiling(subItem.AmountIngredient / (double)subRecipe.AmountResult * loops * amounts)} times");

                    for (int i = 1; i <= Math.Ceiling(subItem.AmountIngredient / (double)subRecipe.AmountResult * loops * amounts); i++)
                    {
                        if (selectedList.Items.IndexOf(subRecipe.RowId) == -1)
                        {
                            selectedList.Items.Add(subRecipe.RowId);
                        }
                        else
                        {
                            var indexOfLast = selectedList.Items.IndexOf(subRecipe.RowId);
                            selectedList.Items.Insert(indexOfLast, subRecipe.RowId);
                        }
                    }

                    PluginLog.Debug($"There are now {selectedList.Items.Count} items on the list");
                }
            }
        }

        public static void DrawRecipeSubTable()
        {
            int colCount = RetainerInfo.ATools ? 4 : 3;
            if (ImGui.BeginTable("###SubTableRecipeData", colCount, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Ingredient", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Required", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Inventory", ImGuiTableColumnFlags.WidthFixed);
                if (RetainerInfo.ATools)
                    ImGui.TableSetupColumn("Retainers", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                if (subtableList.Count == 0)
                    subtableList = SelectedRecipeRawIngredients;

                try
                {
                    foreach (var item in subtableList)
                    {
                        if (LuminaSheets.ItemSheet.ContainsKey((uint)item.Key))
                        {
                            if (CraftingListHelpers.SelectedRecipesCraftable[(uint)item.Key]) continue;
                            ImGui.PushID($"###SubTableItem{item}");
                            var sheetItem = LuminaSheets.ItemSheet[(uint)item.Key];
                            var name = sheetItem.Name.RawString;
                            var count = item.Value;

                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text($"{name}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{count}");
                            ImGui.TableNextColumn();
                            var invcount = NumberOfIngredient((uint)item.Key);
                            if (invcount >= count)
                            {
                                var color = ImGuiColors.HealerGreen;
                                color.W -= 0.3f;
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                            }

                            ImGui.Text($"{invcount}");

                            if (RetainerInfo.ATools && RetainerInfo.CacheBuilt)
                            {
                                ImGui.TableNextColumn();
                                int retainerCount = 0;
                                retainerCount = RetainerInfo.GetRetainerItemCount(sheetItem.RowId);

                                ImGuiEx.Text($"{retainerCount}");

                                if (invcount + retainerCount >= count)
                                {
                                    var color = ImGuiColors.HealerGreen;
                                    color.W -= 0.3f;
                                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                }

                            }

                            ImGui.PopID();

                        }
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "SubTableRender");
                }

                ImGui.EndTable();
            }
        }

        private static void AddRecipeIngredientsToList(Recipe? recipe, ref List<int> ingredientList, bool addSubList = true, CraftingList selectedList = null)
        {
            try
            {
                if (recipe == null) return;

                foreach (var ing in recipe.UnkData5.Where(x => x.AmountIngredient > 0 && x.ItemIngredient != 0))
                {
                    var name = LuminaSheets.ItemSheet[(uint)ing.ItemIngredient].Name.RawString;
                    CraftingListHelpers.SelectedRecipesCraftable[(uint)ing.ItemIngredient] = CraftingListHelpers.FilteredList.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                    for (int i = 1; i <= ing.AmountIngredient; i++)
                    {
                        ingredientList.Add(ing.ItemIngredient);
                        if (CraftingListHelpers.GetIngredientRecipe((uint)ing.ItemIngredient).RowId != 0 && addSubList)
                        {
                            AddRecipeIngredientsToList(CraftingListHelpers.GetIngredientRecipe((uint)ing.ItemIngredient), ref ingredientList);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "ERROR");
            }
        }

        
        public static unsafe bool CheckForIngredients(Recipe recipe, bool fetchFromCache = true, bool checkRetainer = false)
        {
            if (fetchFromCache)
                if (CraftableItems.TryGetValue(recipe, out bool canCraft)) return canCraft;

            foreach (var value in recipe.UnkData5.Where(x => x.ItemIngredient != 0 && x.AmountIngredient > 0))
            {
                try
                {
                    int? invNumberNQ = invManager->GetInventoryItemCount((uint)value.ItemIngredient);
                    int? invNumberHQ = invManager->GetInventoryItemCount((uint)value.ItemIngredient, true);

                    if (!checkRetainer)
                    {
                        if (value.AmountIngredient > (invNumberNQ + invNumberHQ))
                        {
                            invNumberHQ = null;
                            invNumberNQ = null;

                            CraftableItems[recipe] = false;
                            return false;
                        }
                    }
                    else
                    {
                        int retainerCount = RetainerInfo.GetRetainerItemCount((uint)value.ItemIngredient);
                        if (value.AmountIngredient > (invNumberNQ + invNumberHQ + retainerCount))
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
                var invNumberNQ = invManager->GetInventoryItemCount(ingredient, false, false);
                var invNumberHQ = invManager->GetInventoryItemCount(ingredient, true, false, false);

                // PluginLog.Debug($"{invNumberNQ + invNumberHQ}");
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

                            if (item->ItemID == ingredient)
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
            return CraftingListHelpers.FilteredList.Values.Any(x => x.ItemResult.Value.Name.RawString == ingredient) ? CraftingListHelpers.FilteredList.Values.First(x => x.ItemResult.Value.Name.RawString == ingredient) : null;
        }
    }
}
