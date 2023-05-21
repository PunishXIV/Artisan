using Artisan.Autocraft;
using Artisan.IPC;
using Artisan.RawInformation;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Logging;
using ECommons;
using ECommons.Automation;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
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
        public unsafe static InventoryManager* invManager = InventoryManager.Instance();
        public static Dictionary<Recipe, bool> CraftableItems = new();
        internal static Dictionary<int, int> SelectedRecipeRawIngredients = new();
        internal static bool keyboardFocus = true;
        internal static string newListName = String.Empty;
        internal static CraftingList selectedList = new();
        internal static List<uint> jobs = new();
        internal static List<int> rawIngredientsList = new();
        internal static Dictionary<int, int> subtableList = new();
        internal static List<int> listMaterials = new();
        internal static Dictionary<int, int> listMaterialsNew = new();
        internal static uint selectedListItem;
        public static bool Processing = false;
        public static uint CurrentProcessedItem;
        private static bool renameMode = false;
        private static string? renameList;
        public static bool Minimized = false;
        private static int timesToAdd = 1;
        private static bool GatherBuddy => DalamudReflector.TryGetDalamudPlugin("GatherBuddy", out var gb, false, true);
        private static bool ItemVendor => DalamudReflector.TryGetDalamudPlugin("Item Vendor Location", out var ivl, false, true);
        private static bool TidyAfter = false;

        private unsafe static void SearchItem(uint item) => ItemFinderModule.Instance()->SearchForItem(item);
        internal static void Draw()
        {
            string hoverText = "!! HOVER FOR INFORMATION !!";
            var hoverLength = ImGui.CalcTextSize(hoverText);
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X / 2) - (hoverLength.Length() / 2));
            ImGui.TextColored(ImGuiColors.DalamudYellow, hoverText);
            if (ImGui.IsItemHovered())
            {
                ImGui.TextWrapped($"You can use this tab to see what items you can craft with the items in your inventory. You can also use it to create a quick crafting list that Artisan will try and work through.");
                ImGui.TextWrapped($"Please note that due to heavy computational requirements, filtering the recipe list to show only recipes you have ingredients for will not take into account raw ingredients for any crafted items. This may be addressed in the future. For now, it will only look at final ingredients *only* for a given recipe.");
                ImGui.TextWrapped($"Crafting lists process from top to bottom, so ensure any pre-requsite crafts come first.");
                ImGui.TextWrapped("Please ensure that you have gearsets saved for each job you have items to craft. Also if you require the usage of food and/or potions, please do so before starting the crafting list as this will not be automated.");
            }
            ImGui.Separator();

            if (Minimized)
            {
                if (ImGuiEx.IconButton(FontAwesomeIcon.ArrowRight, "Maximize", new Vector2(80f, 0)))
                {
                    Minimized = false;
                }
                ImGui.Spacing();
            }

            DrawListOptions();
            ImGui.Spacing();
        }

        private static void DrawListOptions()
        {
            if (Handler.Enable)
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

            if (!Minimized)
            {
                if (ImGui.Button("New List"))
                {
                    keyboardFocus = true;
                    ImGui.OpenPopup("NewCraftingList");
                }

                DrawNewListPopup();

                if (Service.Configuration.CraftingLists.Count > 0)
                {
                    float longestName = 0;
                    foreach (var list in Service.Configuration.CraftingLists)
                    {
                        if (ImGui.CalcTextSize($"{list.Name}").Length() > longestName)
                            longestName = ImGui.CalcTextSize($"{list.Name}").Length();
                    }

                    longestName = Math.Max(150, longestName);
                    if (ImGui.BeginChild("###craftListSelector", new Vector2(longestName + 40, 0), true))
                    {
                        if (ImGuiEx.IconButton(FontAwesomeIcon.ArrowLeft, "MinimizeButton", new Vector2(longestName + 20, 0)))
                        {
                            Minimized = true;
                        }

                        ImGui.Separator();
                        if (ImGui.BeginChild("###craftingLists", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 90)))
                        {
                            foreach (CraftingList l in Service.Configuration.CraftingLists)
                            {
                                var selected = ImGui.Selectable($"{l.Name}###list{l.ID}", l.ID == selectedList.ID);

                                if (selected)
                                {
                                    selectedList = l;
                                    CraftingListHelpers.SelectedListMateralsNew.Clear();
                                    listMaterialsNew.Clear();
                                    selectedListItem = 0;
                                }
                            }
                        }
                        ImGui.EndChild();
                        Teamcraft.DrawTeamCraftListButtons();
                    }
                    ImGui.EndChild();
                }
                else
                {
                    Teamcraft.DrawTeamCraftListButtons();
                }


            }

            if (selectedList.ID != 0)
            {
                if (!Minimized)
                    ImGui.SameLine();
                if (ImGui.BeginChild("###selectedList", new Vector2(0, ImGui.GetContentRegionAvail().Y), false))
                {
                    if (!renameMode)
                    {
                        ImGui.Text($"Selected List: {selectedList.Name}");
                        ImGui.SameLine();
                        if (ImGuiComponents.IconButton(FontAwesomeIcon.Pen))
                        {
                            renameMode = true;
                        }
                    }
                    else
                    {
                        renameList = selectedList.Name;
                        if (ImGui.InputText("", ref renameList, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            selectedList.Name = renameList;
                            Service.Configuration.Save();

                            renameMode = false;
                            renameList = String.Empty;
                        }
                    }
                    if (ImGui.Button("Delete List (Hold Ctrl)") && ImGui.GetIO().KeyCtrl)
                    {
                        Service.Configuration.CraftingLists.Remove(selectedList);

                        Service.Configuration.Save();
                        selectedList = new();
                        CraftingListHelpers.
                                                SelectedListMateralsNew.Clear();
                        listMaterialsNew.Clear();
                    }

                    bool skipIfEnough = selectedList.SkipIfEnough;
                    if (ImGui.Checkbox("Skip items you already have enough of", ref skipIfEnough))
                    {
                        selectedList.SkipIfEnough = skipIfEnough;
                        Service.Configuration.Save();
                    }

                    bool materia = selectedList.Materia;
                    if (ImGui.Checkbox("Automatically Extract Materia", ref materia))
                    {
                        selectedList.Materia = materia;
                        Service.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker("Will automatically extract materia from any equipped gear once it's spiritbond is 100%");

                    bool repair = selectedList.Repair;
                    if (ImGui.Checkbox("Automatic Repairs", ref repair))
                    {
                        selectedList.Repair = repair;
                        Service.Configuration.Save();
                    }

                    ImGuiComponents.HelpMarker("If enabled, Artisan will automatically repair your gear using Dark Matter when any piece reaches the configured repair threshold.");
                    if (selectedList.Repair)
                    {
                        ImGui.PushItemWidth(200);
                        if (ImGui.SliderInt("##repairp", ref selectedList.RepairPercent, 10, 100, $"%d%%"))
                        {
                            Service.Configuration.Save();
                        }
                    }
                    if (ImGui.Checkbox("Set new items added to list as quick synth", ref selectedList.AddAsQuickSynth))
                    {
                        Service.Configuration.Save();
                    }

                    if (selectedList.Items.Count > 0)
                    {
                        if (ImGui.CollapsingHeader("List Items"))
                        {

                            ImGui.Columns(2, null, false);
                            ImGui.Text("Current Items");
                            ImGui.Indent();
                            var loop = 1;
                            foreach (var item in CollectionsMarshal.AsSpan(selectedList.Items.Distinct().ToList()))
                            {
                                var selected = ImGui.Selectable($"{loop}. {CraftingListHelpers.FilteredList[item].ItemResult.Value.Name.RawString} x{selectedList.Items.Count(x => x == item)} {(CraftingListHelpers.FilteredList[item].AmountResult > 1 ? $"({CraftingListHelpers.FilteredList[item].AmountResult * selectedList.Items.Count(x => x == item)} total)" : $"")}", selectedListItem == item);

                                if (selected)
                                {
                                    selectedListItem = item;
                                }

                                loop++;
                            }
                            ImGui.Unindent();
                            ImGui.NextColumn();
                            if (selectedListItem != 0)
                            {
                                var recipe = CraftingListHelpers.FilteredList[selectedListItem];
                                ImGui.Text("Options");
                                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                                {
                                    selectedList.Items.RemoveAll(x => x == selectedListItem);
                                    selectedListItem = 0;
                                    Service.Configuration.Save();
                                    CraftingListHelpers.
                                                                        SelectedListMateralsNew.Clear();
                                    listMaterialsNew.Clear();
                                }
                                ImGui.SameLine();
                                var count = selectedList.Items.Count(x => x == selectedListItem);

                                ImGui.PushItemWidth(150);
                                if (ImGui.InputInt("Adjust quantity", ref count))
                                {
                                    if (count > 0)
                                    {
                                        var oldCount = selectedList.Items.Count(x => x == selectedListItem);
                                        if (oldCount < count)
                                        {
                                            var diff = count - oldCount;
                                            for (int i = 1; i <= diff; i++)
                                            {
                                                selectedList.Items.Insert(selectedList.Items.IndexOf(selectedListItem), selectedListItem);
                                            }
                                            Service.Configuration.Save();
                                            CraftingListHelpers.
                                                                                        SelectedListMateralsNew.Clear();
                                            listMaterialsNew.Clear();
                                        }
                                        if (count < oldCount)
                                        {
                                            var diff = oldCount - count;
                                            for (int i = 1; i <= diff; i++)
                                            {
                                                selectedList.Items.Remove(selectedListItem);
                                            }
                                            Service.Configuration.Save();
                                            CraftingListHelpers.
                                                                                        SelectedListMateralsNew.Clear();
                                            listMaterialsNew.Clear();
                                        }
                                    }
                                }


                                if (!selectedList.ListItemOptions.ContainsKey(selectedListItem))
                                {
                                    selectedList.ListItemOptions.TryAdd(selectedListItem, new ListItemOptions());
                                    if (selectedList.AddAsQuickSynth && recipe.CanQuickSynth)
                                        selectedList.ListItemOptions[selectedListItem].NQOnly = true;
                                }
                                selectedList.ListItemOptions.TryGetValue(selectedListItem, out var options);

                                if (recipe.CanQuickSynth)
                                {
                                    bool NQOnly = options.NQOnly;
                                    if (ImGui.Checkbox("Quick Synthesis this item", ref NQOnly))
                                    {
                                        options.NQOnly = NQOnly;
                                        Service.Configuration.Save();
                                    }
                                }
                                else
                                {
                                    ImGui.TextWrapped("This item cannot be quick synthed.");
                                }

                                if (LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.Name.RawString == selectedListItem.NameOfRecipe()).Count() > 1)
                                {
                                    string pre = $"{LuminaSheets.ClassJobSheet[recipe.CraftType.Row + 8].Abbreviation.RawString}";
                                    ImGui.TextWrapped("Switch crafted job");
                                    if (ImGui.BeginCombo("###SwitchJobCombo", pre))
                                    {
                                        foreach (var altJob in LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.Name.RawString == selectedListItem.NameOfRecipe()))
                                        {
                                            string altJ = $"{LuminaSheets.ClassJobSheet[altJob.CraftType.Row + 8].Abbreviation.RawString}";
                                            if (ImGui.Selectable($"{altJ}"))
                                            {
                                                for (int i = 0; i < selectedList.Items.Count; i++)
                                                {
                                                    if (selectedList.Items[i] == selectedListItem)
                                                        selectedList.Items[i] = altJob.RowId;
                                                }

                                                selectedListItem = altJob.RowId;
                                                Service.Configuration.Save();
                                            }

                                        }

                                        ImGui.EndCombo();
                                    }

                                }

                                string? preview = Service.Configuration.IndividualMacros.TryGetValue((uint)selectedListItem, out var prevMacro) && prevMacro != null ? Service.Configuration.IndividualMacros[(uint)selectedListItem].Name : "";
                                if (prevMacro is not null && !Service.Configuration.UserMacros.Where(x => x.ID == prevMacro.ID).Any())
                                {
                                    preview = "";
                                    Service.Configuration.IndividualMacros[(uint)selectedListItem] = null;
                                    Service.Configuration.Save();
                                }

                                if (true)
                                {
                                    ImGui.Spacing();
                                    ImGui.TextWrapped($"Use a food item for this recipe");
                                    if (ImGui.BeginCombo("##foodBuff", ConsumableChecker.Food.TryGetFirst(x => x.Id == options.Food, out var item) ? $"{(options.FoodHQ ? " " : "")}{item.Name}" : $"{(options.Food == 0 ? "Disabled" : $"{(options.FoodHQ ? " " : "")}{options.Food}")}"))
                                    {
                                        if (ImGui.Selectable("Disable"))
                                        {
                                            options.Food = 0;
                                            Service.Configuration.Save();
                                        }
                                        foreach (var x in ConsumableChecker.GetFood(true))
                                        {
                                            if (ImGui.Selectable($"{x.Name}"))
                                            {
                                                options.Food = x.Id;
                                                options.FoodHQ = false;
                                                Service.Configuration.Save();
                                            }
                                        }
                                        foreach (var x in ConsumableChecker.GetFood(true, true))
                                        {
                                            if (ImGui.Selectable($" {x.Name}"))
                                            {
                                                options.Food = x.Id;
                                                options.FoodHQ = true;
                                                Service.Configuration.Save();
                                            }
                                        }

                                        ImGui.EndCombo();
                                    }
                                }

                                if (true)
                                {
                                    ImGui.Spacing();
                                    ImGuiEx.SetNextItemFullWidth();
                                    ImGui.TextWrapped($"Use a potion item for this recipe");
                                    if (ImGui.BeginCombo("##potBuff", ConsumableChecker.Pots.TryGetFirst(x => x.Id == options.Potion, out var item) ? $"{(options.PotHQ ? " " : "")}{item.Name}" : $"{(options.Potion == 0 ? "Disabled" : $"{(options.PotHQ ? " " : "")}{options.Potion}")}"))
                                    {
                                        if (ImGui.Selectable("Disable"))
                                        {
                                            options.Potion = 0;
                                            Service.Configuration.Save();
                                        }
                                        foreach (var x in ConsumableChecker.GetPots(true))
                                        {
                                            if (ImGui.Selectable($"{x.Name}"))
                                            {
                                                options.Potion = x.Id;
                                                options.PotHQ = false;
                                                Service.Configuration.Save();
                                            }
                                        }
                                        foreach (var x in ConsumableChecker.GetPots(true, true))
                                        {
                                            if (ImGui.Selectable($" {x.Name}"))
                                            {
                                                options.Potion = x.Id;
                                                options.PotHQ = true;
                                                Service.Configuration.Save();
                                            }
                                        }

                                        ImGui.EndCombo();
                                    }
                                }


                                if (Service.Configuration.UserMacros.Count > 0)
                                {
                                    ImGui.Spacing();
                                    ImGui.TextWrapped($"Use a macro for this recipe (only when Macro mode is enabled)");
                                    if (ImGui.BeginCombo("", preview))
                                    {
                                        if (ImGui.Selectable(""))
                                        {
                                            Service.Configuration.IndividualMacros[selectedListItem] = null;
                                            Service.Configuration.Save();
                                        }
                                        foreach (var macro in Service.Configuration.UserMacros)
                                        {
                                            bool selected = Service.Configuration.IndividualMacros.TryGetValue((uint)selectedListItem, out var selectedMacro) && selectedMacro != null;
                                            if (ImGui.Selectable(macro.Name, selected))
                                            {
                                                Service.Configuration.IndividualMacros[(uint)selectedListItem] = macro;
                                                Service.Configuration.Save();
                                            }
                                        }

                                        ImGui.EndCombo();
                                    }
                                }
                                ImGui.Spacing();

                                if (selectedList.Items.Distinct().Count() > 1)
                                {
                                    ImGui.Text("Re-order list");
                                    ImGui.SameLine();

                                    bool isFirstItem = selectedList.Items.IndexOf(selectedListItem) == 0;
                                    bool isLastItem = selectedList.Items.LastIndexOf(selectedListItem) == selectedList.Items.Count - 1;

                                    if (!isFirstItem)
                                    {
                                        if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp))
                                        {
                                            var loops = selectedList.Items.Count(x => x == selectedListItem);
                                            var previousNum = selectedList.Items[selectedList.Items.IndexOf(selectedListItem) - 1];
                                            var insertionIndex = selectedList.Items.IndexOf(previousNum);

                                            selectedList.Items.RemoveAll(x => x == selectedListItem);
                                            for (int i = 1; i <= loops; i++)
                                            {
                                                selectedList.Items.Insert(insertionIndex, selectedListItem);
                                            }

                                        }
                                        if (!isLastItem) ImGui.SameLine();
                                    }

                                    if (!isLastItem)
                                    {
                                        if (isFirstItem)
                                        {
                                            ImGui.Dummy(new Vector2(22));
                                            ImGui.SameLine();
                                        }

                                        if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown))
                                        {
                                            var nextNum = selectedList.Items[selectedList.Items.LastIndexOf(selectedListItem) + 1];
                                            var loops = selectedList.Items.Count(x => x == nextNum);
                                            var insertionIndex = selectedList.Items.IndexOf(selectedListItem);

                                            selectedList.Items.RemoveAll(x => x == nextNum);
                                            for (int i = 1; i <= loops; i++)
                                            {
                                                selectedList.Items.Insert(insertionIndex, nextNum);
                                            }

                                        }
                                    }
                                }

                            }
                        }
                        ImGui.Columns(1, null, false);
                        if (ImGui.CollapsingHeader("Total Ingredients"))
                        {
                            if (GatherBuddy)
                            {
                                ImGui.TextWrapped($"Click the item name to copy to clipboard.\nHold shift and click the item name to perform a GatherBuddy gather command for the item.\nHold ctrl and click the item name to perform an Item Search command for the item.");
                            }
                            else
                            {
                                ImGui.TextWrapped($"Click the item name to copy to clipboard.\nHold ctrl and click the item name to perform an Item Search command for the item.\nInstall GatherBuddy for more functionality.");
                            }
                            if (ItemVendor)
                            {
                                ImGui.TextWrapped($"Hold alt and click the item name to perform an Item Vendor Lookup.");
                            }
                            else
                            {
                                ImGui.TextWrapped($"Install Item Vendor Location for more functionality.");
                            }

                            ImGui.Spacing();
                            ImGui.Separator();
                            DrawTotalIngredientsTable();
                        }
                        if (ImGui.Button("Start Crafting List", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                        {
                            CraftingListFunctions.CurrentIndex = 0;
                            if (CraftingListFunctions.RecipeWindowOpen())
                                CraftingListFunctions.CloseCraftingMenu();

                            Processing = true;
                            Handler.Enable = false;
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
                    ImGui.Spacing();
                    ImGui.Separator();

                    DrawRecipeData();


                }

                ImGui.EndChild();
            }


        }



        private async static void DrawTotalIngredientsTable()
        {
            int colCount = RetainerInfo.ATools ? 4 : 3;
            try
            {
                if (ImGui.BeginTable("###ListMaterialTableRaw", colCount, ImGuiTableFlags.Borders))
                {
                    ImGui.TableSetupColumn("Ingredient", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Required", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Inventory", ImGuiTableColumnFlags.WidthFixed);
                    if (RetainerInfo.ATools)
                        ImGui.TableSetupColumn("Retainers", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableHeadersRow();

                    if (CraftingListHelpers.SelectedListMateralsNew.Count == 0)
                    {
                        foreach (var item in selectedList.Items.Distinct())
                        {
                            Recipe r = CraftingListHelpers.FilteredList[item];
                            CraftingListHelpers.AddRecipeIngredientsToList(r, ref CraftingListHelpers.SelectedListMateralsNew, false, selectedList);
                        }
                    }

                    if (listMaterialsNew.Count == 0)
                        listMaterialsNew = CraftingListHelpers.SelectedListMateralsNew;

                    try
                    {
                        foreach (var item in listMaterialsNew.OrderByDescending(x => x.Key))
                        {
                            if (LuminaSheets.ItemSheet.TryGetValue((uint)item.Key, out var sheetItem))
                            {
                                if (CraftingListHelpers.SelectedRecipesCraftable[item.Key]) continue;
                                ImGui.PushID(item.Key);
                                var name = sheetItem.Name.RawString;
                                var count = item.Value;
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text($"{name}");
                                if (GatherBuddy && ImGui.IsItemClicked() && ImGui.GetIO().KeyShift && !ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyAlt)
                                {
                                    if (sheetItem.ItemUICategory.Row == 47)
                                        Chat.Instance.SendMessage($"/gatherfish {name}");
                                    else
                                        Chat.Instance.SendMessage($"/gather {name}");
                                }
                                if (ItemVendor && ImGui.IsItemClicked() && ImGui.GetIO().KeyAlt && !ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyShift)
                                {
                                    ItemVendorLocation.OpenContextMenu((uint)item.Key);
                                    //Chat.Instance.SendMessage($"/xlvendor {name}");
                                }

                                if (ImGui.IsItemClicked() && ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyShift && !ImGui.GetIO().KeyAlt)
                                {
                                    SearchItem((uint)item.Key);
                                }
                                if (ImGui.IsItemClicked() && !ImGui.GetIO().KeyShift && !ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyAlt)
                                {
                                    ImGui.SetClipboardText(name);
                                    Notify.Success("Name copied to clipboard");
                                }
                                ImGui.TableNextColumn();
                                ImGui.Text($"{count}");
                                ImGui.TableNextColumn();
                                var invCount = NumberOfIngredient((uint)item.Key);
                                if (invCount >= count)
                                {
                                    var color = ImGuiColors.HealerGreen;
                                    color.W -= 0.3f;
                                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                }
                                ImGui.Text($"{invCount}");
                                if (RetainerInfo.ATools)
                                {
                                    ImGui.TableNextColumn();

                                    if (RetainerInfo.CacheBuilt)
                                    {
                                        uint retainerCount = RetainerInfo.GetRetainerItemCount(sheetItem.RowId);
                                        ImGui.Text($"{(retainerCount)}");

                                        if (invCount >= count)
                                        {
                                            var color = ImGuiColors.HealerGreen;
                                            color.W -= 0.3f;
                                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                        }
                                        else if (retainerCount >= count)
                                        {
                                            var color = ImGuiColors.DalamudOrange;
                                            color.W -= 0.6f;
                                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                        }
                                    }
                                    else
                                    {
                                        ImGui.Text($"Cache Building. Please wait.");
                                    }
                                }

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
            catch (Exception ex)
            {
                PluginLog.Debug(ex, "TotalIngredsTable");
            }


            if (ImGui.BeginTable("###ListMaterialTableSub", colCount, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Ingredient", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Required", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Inventory", ImGuiTableColumnFlags.WidthFixed);
                if (RetainerInfo.ATools)
                    ImGui.TableSetupColumn("Retainers", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                if (CraftingListHelpers.SelectedListMateralsNew.Count == 0)
                {
                    foreach (var item in selectedList.Items)
                    {
                        Recipe r = CraftingListHelpers.FilteredList[item];
                        CraftingListHelpers.AddRecipeIngredientsToList(r, ref CraftingListHelpers.SelectedListMateralsNew, false, selectedList);
                    }
                }

                if (listMaterialsNew.Count == 0)
                    listMaterialsNew = CraftingListHelpers.SelectedListMateralsNew;

                try
                {
                    foreach (var item in listMaterialsNew)
                    {
                        if (LuminaSheets.ItemSheet.TryGetValue((uint)item.Key, out var sheetItem))
                        {
                            if (CraftingListHelpers.SelectedRecipesCraftable[item.Key])
                            {
                                ImGui.PushID(item.Key);
                                var name = sheetItem.Name.RawString;
                                var count = item.Value;
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.Text($"{name}");
                                if (GatherBuddy && ImGui.IsItemClicked() && ImGui.GetIO().KeyShift && !ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyAlt)
                                {
                                    if (sheetItem.ItemUICategory.Row == 47)
                                        Chat.Instance.SendMessage($"/gatherfish {name}");
                                    else
                                        Chat.Instance.SendMessage($"/gather {name}");
                                }
                                if (ItemVendor && ImGui.IsItemClicked() && ImGui.GetIO().KeyAlt && !ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyShift)
                                {
                                    ItemVendorLocation.OpenContextMenu((uint)item.Key);
                                    //Chat.Instance.SendMessage($"/xlvendor {name}");
                                }

                                if (ImGui.IsItemClicked() && ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyShift && !ImGui.GetIO().KeyAlt)
                                {
                                    SearchItem((uint)item.Key);
                                }
                                if (ImGui.IsItemClicked() && !ImGui.GetIO().KeyShift && !ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyAlt)
                                {
                                    ImGui.SetClipboardText(name);
                                    Notify.Success("Name copied to clipboard");
                                }
                                ImGui.TableNextColumn();
                                ImGui.Text($"{count}");
                                ImGui.TableNextColumn();
                                var invCount = NumberOfIngredient((uint)item.Key);
                                if (invCount >= count)
                                {
                                    var color = ImGuiColors.HealerGreen;
                                    color.W -= 0.3f;
                                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                }
                                ImGui.Text($"{invCount}");
                                if (RetainerInfo.ATools)
                                {
                                    ImGui.TableNextColumn();
                                    if (RetainerInfo.CacheBuilt)
                                    {
                                        uint retainerCount = RetainerInfo.GetRetainerItemCount(sheetItem.RowId);
                                        ImGui.Text($"{(retainerCount)}");

                                        if (invCount >= count)
                                        {
                                            var color = ImGuiColors.HealerGreen;
                                            color.W -= 0.3f;
                                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                        }
                                        else if (retainerCount >= count)
                                        {
                                            var color = ImGuiColors.DalamudOrange;
                                            color.W -= 0.6f;
                                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                        }
                                    }
                                    else
                                    {
                                        ImGui.Text($"Cache Building. Please wait.");
                                    }

                                }
                                ImGui.PopID();
                            }
                        }
                    }
                }
                catch
                {

                }

                ImGui.EndTable();
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

        private async static void DrawRecipeData()
        {
            bool showOnlyCraftable = Service.Configuration.ShowOnlyCraftable;

            if (ImGui.Checkbox("###ShowcraftableCheckbox", ref showOnlyCraftable))
            {
                Service.Configuration.ShowOnlyCraftable = showOnlyCraftable;
                Service.Configuration.Save();

                if (showOnlyCraftable)
                {
                    RetainerInfo.TM.Abort();
                    RetainerInfo.TM.Enqueue(async () => await RetainerInfo.LoadCache());
                }
            }
            ImGui.SameLine();
            ImGui.TextWrapped($"Show only recipes you have materials for (toggle to refresh)");

            if (Service.Configuration.ShowOnlyCraftable && RetainerInfo.ATools)
            {
                bool showOnlyCraftableRetainers = Service.Configuration.ShowOnlyCraftableRetainers;
                if (ImGui.Checkbox($"###ShowCraftableRetainersCheckbox", ref showOnlyCraftableRetainers))
                {
                    Service.Configuration.ShowOnlyCraftableRetainers = showOnlyCraftableRetainers;
                    Service.Configuration.Save();

                    CraftableItems.Clear();
                    RetainerInfo.TM.Abort();
                    RetainerInfo.TM.Enqueue(async () => await RetainerInfo.LoadCache());
                }

                ImGui.SameLine();
                ImGui.TextWrapped("Include Retainers");
            }

            string preview = SelectedRecipe is null ? "" : $"{SelectedRecipe.ItemResult.Value.Name.RawString} ({LuminaSheets.ClassJobSheet[SelectedRecipe.CraftType.Row + 8].Abbreviation.RawString})";
            if (ImGui.BeginCombo("Select Recipe", preview))
            {
                DrawRecipes();

                ImGui.EndCombo();
            }

            if (SelectedRecipe != null)
            {
                if (ImGui.CollapsingHeader("Recipe Information"))
                {
                    DrawRecipeOptions();
                }
                if (SelectedRecipeRawIngredients.Count == 0)
                    CraftingListHelpers.AddRecipeIngredientsToList(SelectedRecipe, ref SelectedRecipeRawIngredients);

                if (ImGui.CollapsingHeader("Raw Ingredients"))
                {
                    ImGui.Text($"Raw Ingredients Required");
                    DrawRecipeSubTable();

                }

                ImGui.Spacing();
                ImGui.PushItemWidth(ImGui.GetContentRegionAvail().Length() / 2f);
                ImGui.TextWrapped("Number of times to add");
                ImGui.SameLine();
                ImGui.InputInt("###TimesToAdd", ref timesToAdd, 1, 5);
                ImGui.PushItemWidth(-1f);

                if (ImGui.Button("Add to List", new Vector2(ImGui.GetContentRegionAvail().X / 2, 30)))
                {
                    CraftingListHelpers.SelectedListMateralsNew.Clear();
                    listMaterialsNew.Clear();

                    for (int i = 0; i < timesToAdd; i++)
                    {
                        if (selectedList.Items.IndexOf(SelectedRecipe.RowId) == -1)
                        {
                            selectedList.Items.Add(SelectedRecipe.RowId);
                        }
                        else
                        {
                            var indexOfLast = selectedList.Items.IndexOf(SelectedRecipe.RowId);
                            selectedList.Items.Insert(indexOfLast, SelectedRecipe.RowId);
                        }
                    }

                    if (TidyAfter)
                        CraftingListHelpers.TidyUpList(selectedList);

                    if (selectedList.ListItemOptions.TryGetValue(SelectedRecipe.RowId, out var opts))
                    {
                        opts.NQOnly = selectedList.AddAsQuickSynth;
                    }
                    else
                    {
                        selectedList.ListItemOptions.TryAdd(SelectedRecipe.RowId, new ListItemOptions() { NQOnly = selectedList.AddAsQuickSynth });
                    }

                    Service.Configuration.Save();
                    if (Service.Configuration.ResetTimesToAdd)
                        timesToAdd = 1;
                }
                ImGui.SameLine();
                if (ImGui.Button("Add to List (with all subcrafts)", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
                {
                    CraftingListHelpers.SelectedListMateralsNew.Clear();
                    listMaterialsNew.Clear();

                    AddAllSubcrafts(SelectedRecipe, selectedList, 1, timesToAdd);

                    PluginLog.Debug($"Adding: {SelectedRecipe.ItemResult.Value.Name.RawString} {timesToAdd} times");
                    for (int i = 1; i <= timesToAdd; i++)
                    {
                        if (selectedList.Items.IndexOf(SelectedRecipe.RowId) == -1)
                        {
                            selectedList.Items.Add(SelectedRecipe.RowId);
                        }
                        else
                        {
                            var indexOfLast = selectedList.Items.IndexOf(SelectedRecipe.RowId);
                            selectedList.Items.Insert(indexOfLast, SelectedRecipe.RowId);
                        }
                    }

                    if (TidyAfter)
                        CraftingListHelpers.TidyUpList(selectedList);

                    if (selectedList.ListItemOptions.TryGetValue(SelectedRecipe.RowId, out var opts))
                    {
                        opts.NQOnly = selectedList.AddAsQuickSynth;
                    }
                    else
                    {
                        selectedList.ListItemOptions.TryAdd(SelectedRecipe.RowId, new ListItemOptions() { NQOnly = selectedList.AddAsQuickSynth });
                    }

                    Service.Configuration.Save();
                    if (Service.Configuration.ResetTimesToAdd)
                        timesToAdd = 1;
                }
                ImGui.Checkbox($"Remove all unnecessary subcrafts after adding", ref TidyAfter);
            }
        }

        public static void AddAllSubcrafts(Recipe selectedRecipe, CraftingList selectedList, int amounts = 1, int loops = 1)
        {
            PluginLog.Debug($"Processing: {selectedRecipe.ItemResult.Value.Name.RawString}");
            foreach (var subItem in selectedRecipe.UnkData5.Where(x => x.AmountIngredient > 0))
            {
                PluginLog.Debug($"Sub-item: {LuminaSheets.ItemSheet[(uint)subItem.ItemIngredient].Name.RawString} * {subItem.AmountIngredient}");
                var subRecipe = CraftingListHelpers.GetIngredientRecipe(subItem.ItemIngredient);
                if (subRecipe != null)
                {
                    AddAllSubcrafts(subRecipe, selectedList, subItem.AmountIngredient * amounts, loops);

                    PluginLog.Debug($"Adding: {subRecipe.ItemResult.Value.Name.RawString} {Math.Ceiling((double)subItem.AmountIngredient / (double)subRecipe.AmountResult * (double)loops * amounts)} times");

                    for (int i = 1; i <= Math.Ceiling((double)subItem.AmountIngredient / (double)subRecipe.AmountResult * (double)loops * amounts); i++)
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

        private static void DrawRecipeSubTable()
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
                            if (CraftingListHelpers.SelectedRecipesCraftable[item.Key]) continue;
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
                                uint retainerCount = 0;
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
                    CraftingListHelpers.SelectedRecipesCraftable[ing.ItemIngredient] = CraftingListHelpers.FilteredList.Any(x => x.Value.ItemResult.Value.Name.RawString == name);

                    for (int i = 1; i <= ing.AmountIngredient; i++)
                    {
                        ingredientList.Add(ing.ItemIngredient);
                        if (CraftingListHelpers.GetIngredientRecipe(ing.ItemIngredient).RowId != 0 && addSubList)
                        {
                            AddRecipeIngredientsToList(CraftingListHelpers.GetIngredientRecipe(ing.ItemIngredient), ref ingredientList);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "ERROR");
            }
        }

        private static void DrawRecipes()
        {
            if (Service.Configuration.ShowOnlyCraftable && !RetainerInfo.CacheBuilt)
            {
                if (RetainerInfo.ATools)
                    ImGui.TextWrapped($"Building Retainer Cache: {(RetainerInfo.RetainerData.Values.Any() ? RetainerInfo.RetainerData.FirstOrDefault().Value.Count : "0")}/{CraftingListHelpers.FilteredList.Select(x => x.Value).SelectMany(x => x.UnkData5).Where(x => x.ItemIngredient != 0 && x.AmountIngredient > 0).DistinctBy(x => x.ItemIngredient).Count()}");
                ImGui.TextWrapped($"Building Craftable Items List: {CraftableItems.Count}/{CraftingListHelpers.FilteredList.Count}");
                ImGui.Spacing();
            }
            ImGui.Text("Search");
            ImGui.SameLine();
            ImGui.InputText("###RecipeSearch", ref Search, 100);
            if (ImGui.Selectable("", SelectedRecipe == null))
            {
                SelectedRecipe = null;
            }

            if (Service.Configuration.ShowOnlyCraftable && RetainerInfo.CacheBuilt)
            {
                foreach (var recipe in CraftableItems.Where(x => x.Value).Select(x => x.Key).Where(x => x.ItemResult.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)))
                {
                    ImGui.PushID((int)recipe.RowId);
                    var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name.RawString} ({LuminaSheets.ClassJobSheet[recipe.CraftType.Row + 8].Abbreviation.RawString} {recipe.RecipeLevelTable.Value.ClassJobLevel})", recipe.RowId == SelectedRecipe?.RowId);

                    if (selected)
                    {
                        subtableList.Clear();
                        SelectedRecipeRawIngredients.Clear();
                        SelectedRecipe = recipe;
                    }
                    ImGui.PopID();
                }
            }
            else if (!Service.Configuration.ShowOnlyCraftable)
            {
                foreach (var recipe in CollectionsMarshal.AsSpan(CraftingListHelpers.FilteredList.Values.ToList()))
                {
                    try
                    {
                        if (string.IsNullOrEmpty(recipe.ItemResult.Value.Name.RawString)) continue;
                        if (!recipe.ItemResult.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)) continue;
                        rawIngredientsList.Clear();
                        var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name.RawString} ({LuminaSheets.ClassJobSheet[recipe.CraftType.Row + 8].Abbreviation.RawString} {recipe.RecipeLevelTable.Value.ClassJobLevel})", recipe.RowId == SelectedRecipe?.RowId);

                        if (selected)
                        {
                            subtableList.Clear();
                            SelectedRecipeRawIngredients.Clear();
                            SelectedRecipe = recipe;
                        }

                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex, "DrawRecipeList");
                    }
                }
            }
        }

        public unsafe static bool CheckForIngredients(Recipe recipe, bool fetchFromCache = true, bool checkRetainer = false)
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
                        uint retainerCount = RetainerInfo.GetRetainerItemCount((uint)value.ItemIngredient);
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

        private static bool HasRawIngredients(int itemIngredient, byte amountIngredient)
        {
            if (CraftingListHelpers.GetIngredientRecipe(itemIngredient) == null) return false;

            return CheckForIngredients(CraftingListHelpers.GetIngredientRecipe(itemIngredient));

        }

        public unsafe static int NumberOfIngredient(uint ingredient)
        {
            try
            {
                var invNumberNQ = invManager->GetInventoryItemCount(ingredient, false, false);
                var invNumberHQ = invManager->GetInventoryItemCount(ingredient, true, false, false);
                //PluginLog.Debug($"{invNumberNQ + invNumberHQ}");
                return invNumberHQ + invNumberNQ;
            }
            catch
            {
                return 0;
            }
        }
        private unsafe static void DrawRecipeOptions()
        {
            {
                List<uint> craftingJobs = LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.Name.RawString == SelectedRecipe.ItemResult.Value.Name.RawString).Select(x => x.CraftType.Value.RowId + 8).ToList();
                string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => craftingJobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
                ImGui.Text($"Crafted by: {String.Join(", ", jobstrings)}");
            }
            var ItemsRequired = SelectedRecipe.UnkData5;

            int numRows = RetainerInfo.ATools ? 6 : 5;
            if (ImGui.BeginTable("###RecipeTable", numRows, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Ingredient", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Required", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Inventory", ImGuiTableColumnFlags.WidthFixed);
                if (RetainerInfo.ATools)
                    ImGui.TableSetupColumn("Retainers", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Method", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();
                try
                {
                    foreach (var value in ItemsRequired.Where(x => x.AmountIngredient > 0))
                    {
                        jobs.Clear();
                        string ingredient = LuminaSheets.ItemSheet[(uint)value.ItemIngredient].Name.RawString;
                        Recipe? ingredientRecipe = CraftingListHelpers.GetIngredientRecipe(value.ItemIngredient);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"{ingredient}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"{value.AmountIngredient}");
                        ImGui.TableNextColumn();
                        var invCount = NumberOfIngredient((uint)value.ItemIngredient);
                        ImGuiEx.Text($"{invCount}");
                        if (invCount >= value.AmountIngredient)
                        {
                            var color = ImGuiColors.HealerGreen;
                            color.W -= 0.3f;
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                        }
                        ImGui.TableNextColumn();
                        if (RetainerInfo.ATools && RetainerInfo.CacheBuilt)
                        {
                            uint retainerCount = 0;
                            retainerCount = RetainerInfo.GetRetainerItemCount((uint)value.ItemIngredient);

                            ImGuiEx.Text($"{retainerCount}");

                            if (invCount + retainerCount >= value.AmountIngredient)
                            {
                                var color = ImGuiColors.HealerGreen;
                                color.W -= 0.3f;
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                            }
                            ImGui.TableNextColumn();
                        }

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
                                jobs.AddRange(CraftingListHelpers.FilteredList.Values.Where(x => x.ItemResult == ingredientRecipe.ItemResult).Select(x => x.CraftType.Row + 8));
                                string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => jobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
                                ImGui.Text(String.Join(", ", jobstrings));
                            }
                            catch (Exception ex)
                            {
                                PluginLog.Error(ex, "JobStrings");
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
                                    if (jobs!.Any(x => x.Value.RowId is 0 or 1)) tempArray.Add(LuminaSheets.ClassJobSheet[16].Abbreviation.RawString);
                                    if (jobs!.Any(x => x.Value.RowId is 2 or 3)) tempArray.Add(LuminaSheets.ClassJobSheet[17].Abbreviation.RawString);
                                    if (jobs!.Any(x => x.Value.RowId is 4 or 5)) tempArray.Add(LuminaSheets.ClassJobSheet[18].Abbreviation.RawString);
                                    ImGui.Text($"{string.Join(", ", tempArray)}");
                                    continue;
                                }

                                var spearfish = LuminaSheets.SpearfishingItemSheet?.Where(x => x.Value.Item.Value.RowId == value.ItemIngredient).FirstOrDefault().Value;
                                if (spearfish != null && spearfish.Item.Value.Name.RawString == ingredient)
                                {
                                    ImGui.Text($"{LuminaSheets.ClassJobSheet[18].Abbreviation.RawString}");
                                    continue;
                                }

                                var fishSpot = LuminaSheets.FishParameterSheet?.Where(x => x.Value.Item == value.ItemIngredient).FirstOrDefault().Value;
                                if (fishSpot != null)
                                {
                                    ImGui.Text($"{LuminaSheets.ClassJobSheet[18].Abbreviation.RawString}");
                                    continue;
                                }


                            }
                            catch (Exception ex)
                            {
                                PluginLog.Error(ex, "JobStrings");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "RecipeIngreds");
                }
                ImGui.EndTable();
            }

        }



        public static Recipe? GetIngredientRecipe(string ingredient)
        {
            return CraftingListHelpers.FilteredList.Values.Any(x => x.ItemResult.Value.Name.RawString == ingredient) ? CraftingListHelpers.FilteredList.Values.First(x => x.ItemResult.Value.Name.RawString == ingredient) : null;
        }
    }
}
