namespace Artisan.UI;

using Autocraft;
using CraftingLists;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using global::Artisan.UI.Tables;
using ImGuiNET;
using IPC;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using OtterGui;
using OtterGui.Filesystem;
using OtterGui.Raii;
using PunishLib.ImGuiMethods;
using RawInformation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

internal class ListEditor : Window, IDisposable
{
    public bool Minimized = false;

    public bool Processing = false;

    internal List<uint> jobs = new();

    internal List<int> listMaterials = new();

    internal Dictionary<int, int> listMaterialsNew = new();

    internal string newListName = string.Empty;

    internal List<int> rawIngredientsList = new();

    internal uint selectedListItem;

    internal Recipe? SelectedRecipe;

    internal Dictionary<uint, int> SelectedRecipeRawIngredients = new();

    internal Dictionary<int, int> subtableList = new();

    private ListFolders ListsUI = new();

    private string? renameList;

    private bool renameMode;

    private bool TidyAfter;

    private int timesToAdd = 1;

    private readonly RecipeSelector RecipeSelector;

    private readonly CraftingList SelectedList;

    private string newName = string.Empty;

    private bool RenameMode;

    internal string Search = string.Empty;

    public Dictionary<uint, int> SelectedListMateralsNew = new();

    public IngredientTable Table;

    private bool ColourValidation = false;

    private bool HQSubcraftsOnly = false;

    private bool NeedsToRefreshTable = false;
    public ListEditor(int listId)
        : base($"List Editor###{listId}")
    {
        SelectedList = P.Config.CraftingLists.First(x => x.ID == listId);
        RecipeSelector = new RecipeSelector(SelectedList.ID);
        RecipeSelector.ItemAdded += RefreshTable;
        RecipeSelector.ItemDeleted += RefreshTable;
        IsOpen = true;
        P.ws.AddWindow(this);
        Size = new Vector2(1000, 600);
        SizeCondition = ImGuiCond.Appearing;
        ShowCloseButton = true;
        RespectCloseHotkey = false;
        GenerateTableAsync();

        if (P.Config.DefaultHQCrafts) HQSubcraftsOnly = true;
        if (P.Config.DefaultColourValidation) ColourValidation = true;
    }

    private async Task GenerateTableAsync()
    {
        Table?.Dispose();
        var list = await Ingredient.GenerateList(SelectedList);
        Table = new IngredientTable(list);
    }

    private async void RefreshTable(object? sender, bool e)
    {
        await GenerateTableAsync();
    }

    public override void PreDraw()
    {
        if (!P.Config.DisableTheme)
        {
            P.Style.Push();
            ImGui.PushFont(P.CustomFont);
            P.StylePushed = true;
        }

    }

    public override void PostDraw()
    {
        if (P.StylePushed)
        {
            P.Style.Pop();
            ImGui.PopFont();
            P.StylePushed = false;
        }
    }

    private static bool GatherBuddy =>
        DalamudReflector.TryGetDalamudPlugin("GatherBuddy", out var gb, false, true);

    private static bool ItemVendor =>
        DalamudReflector.TryGetDalamudPlugin("Item Vendor Location", out var ivl, false, true);

    private static bool MonsterLookup =>
        DalamudReflector.TryGetDalamudPlugin("Monster Loot Hunter", out var mlh, false, true);

    private static unsafe void SearchItem(uint item) => ItemFinderModule.Instance()->SearchForItem(item);


    public async override void Draw()
    {
        var topRowY = ImGui.GetCursorPosY();
        if (ImGui.BeginTabBar("CraftingListEditor", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("Recipes"))
            {
                DrawRecipes();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Ingredients"))
            {
                if (NeedsToRefreshTable)
                {
                    GenerateTableAsync();
                    NeedsToRefreshTable = false;
                }

                DrawIngredients();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("List Settings"))
            {
                DrawListSettings();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        var btn = ImGuiHelpers.GetButtonSize("Begin Crafting List");
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - btn.X);
        ImGui.SetCursorPosY(topRowY - 5f);

        if (Endurance.Enable || CraftingListUI.Processing)
            ImGui.BeginDisabled();

        if (ImGui.Button("Begin Crafting List"))
        {
            CraftingListUI.selectedList = this.SelectedList;
            CraftingListUI.StartList();
            this.IsOpen = false;
        }

        if (Endurance.Enable || CraftingListUI.Processing)
            ImGui.EndDisabled();

        ImGui.SameLine();
        var export = ImGuiHelpers.GetButtonSize("Export List");
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - export.X - btn.X - 3f);

        if (ImGui.Button("Export List"))
        {
            ImGui.SetClipboardText(JsonConvert.SerializeObject(P.Config.CraftingLists.Where(x => x.ID == SelectedList.ID).First()));
            Notify.Success("List exported to clipboard.");
        }

        if (RetainerInfo.ATools)
        {
            ImGui.SameLine();
            var restock = ImGuiHelpers.GetButtonSize("Restock From Retainers");
            ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - restock.X - export.X - btn.X - 6f);

            if (Endurance.Enable || CraftingListUI.Processing)
                ImGui.BeginDisabled();

            if (ImGui.Button($"Restock From Retainers"))
            {
                RetainerInfo.RestockFromRetainers(SelectedList);
            }

            if (Endurance.Enable || CraftingListUI.Processing)
                ImGui.EndDisabled();
        }


    }

    public void DrawRecipeData()
    {
        var showOnlyCraftable = P.Config.ShowOnlyCraftable;

        if (ImGui.Checkbox("###ShowCraftableCheckbox", ref showOnlyCraftable))
        {
            P.Config.ShowOnlyCraftable = showOnlyCraftable;
            P.Config.Save();

            if (showOnlyCraftable)
            {
                RetainerInfo.TM.Abort();
                RetainerInfo.TM.Enqueue(async () => await RetainerInfo.LoadCache());
            }
        }

        ImGui.SameLine();
        ImGui.TextWrapped("Show only recipes you have materials for (toggle to refresh)");

        if (P.Config.ShowOnlyCraftable && RetainerInfo.ATools)
        {
            var showOnlyCraftableRetainers = P.Config.ShowOnlyCraftableRetainers;
            if (ImGui.Checkbox("###ShowCraftableRetainersCheckbox", ref showOnlyCraftableRetainers))
            {
                P.Config.ShowOnlyCraftableRetainers = showOnlyCraftableRetainers;
                P.Config.Save();

                CraftingListUI.CraftableItems.Clear();
                RetainerInfo.TM.Abort();
                RetainerInfo.TM.Enqueue(async () => await RetainerInfo.LoadCache());
            }

            ImGui.SameLine();
            ImGui.TextWrapped("Include Retainers");
        }

        var preview = SelectedRecipe is null
                          ? string.Empty
                          : $"{SelectedRecipe.ItemResult.Value.Name.RawString} ({LuminaSheets.ClassJobSheet[SelectedRecipe.CraftType.Row + 8].Abbreviation.RawString})";

        if (ImGui.BeginCombo("Select Recipe", preview))
        {
            DrawRecipeList();

            ImGui.EndCombo();
        }


        if (SelectedRecipe != null)
        {
            if (ImGui.CollapsingHeader("Recipe Information")) DrawRecipeOptions();
            if (SelectedRecipeRawIngredients.Count == 0)
                CraftingListHelpers.AddRecipeIngredientsToList(SelectedRecipe, ref SelectedRecipeRawIngredients);

            if (ImGui.CollapsingHeader("Raw Ingredients"))
            {
                ImGui.Text("Raw Ingredients Required");
                CraftingListUI.DrawRecipeSubTable();
            }

            ImGui.Spacing();
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().Length() / 2f);
            ImGui.TextWrapped("Number of times to add");
            ImGui.SameLine();
            ImGui.InputInt("###TimesToAdd", ref timesToAdd, 1, 5);
            ImGui.PushItemWidth(-1f);

            if (ImGui.Button("Add to List", new Vector2(ImGui.GetContentRegionAvail().X / 2, 30)))
            {
                SelectedListMateralsNew.Clear();
                listMaterialsNew.Clear();

                for (var i = 0; i < timesToAdd; i++)
                    if (SelectedList.Items.IndexOf(SelectedRecipe.RowId) == -1)
                    {
                        SelectedList.Items.Add(SelectedRecipe.RowId);
                    }
                    else
                    {
                        var indexOfLast = SelectedList.Items.IndexOf(SelectedRecipe.RowId);
                        SelectedList.Items.Insert(indexOfLast, SelectedRecipe.RowId);
                    }

                if (TidyAfter)
                    CraftingListHelpers.TidyUpList(SelectedList);

                if (SelectedList.ListItemOptions.TryGetValue(SelectedRecipe.RowId, out var opts))
                    opts.NQOnly = SelectedList.AddAsQuickSynth;
                else
                    SelectedList.ListItemOptions.TryAdd(
                        SelectedRecipe.RowId,
                        new ListItemOptions { NQOnly = SelectedList.AddAsQuickSynth });

                RecipeSelector.Items = SelectedList.Items.Distinct().ToList();

                NeedsToRefreshTable = true;

                P.Config.Save();
                if (P.Config.ResetTimesToAdd)
                    timesToAdd = 1;
            }

            ImGui.SameLine();
            if (ImGui.Button("Add to List (with all sub-crafts)", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
            {
                SelectedListMateralsNew.Clear();
                listMaterialsNew.Clear();

                CraftingListUI.AddAllSubcrafts(SelectedRecipe, SelectedList, 1, timesToAdd);

                PluginLog.Debug($"Adding: {SelectedRecipe.ItemResult.Value.Name.RawString} {timesToAdd} times");
                for (var i = 1; i <= timesToAdd; i++)
                    if (SelectedList.Items.IndexOf(SelectedRecipe.RowId) == -1)
                    {
                        SelectedList.Items.Add(SelectedRecipe.RowId);
                    }
                    else
                    {
                        var indexOfLast = SelectedList.Items.IndexOf(SelectedRecipe.RowId);
                        SelectedList.Items.Insert(indexOfLast, SelectedRecipe.RowId);
                    }

                if (TidyAfter)
                    CraftingListHelpers.TidyUpList(SelectedList);

                if (SelectedList.ListItemOptions.TryGetValue(SelectedRecipe.RowId, out var opts))
                    opts.NQOnly = SelectedList.AddAsQuickSynth;
                else
                    SelectedList.ListItemOptions.TryAdd(
                        SelectedRecipe.RowId,
                        new ListItemOptions { NQOnly = SelectedList.AddAsQuickSynth });

                RecipeSelector.Items = SelectedList.Items.Distinct().ToList();
                GenerateTableAsync();
                P.Config.Save();
                if (P.Config.ResetTimesToAdd)
                    timesToAdd = 1;
            }

            ImGui.Checkbox("Remove all unnecessary subcrafts after adding", ref TidyAfter);
        }
    }

    private void DrawRecipeList()
    {
        if (P.Config.ShowOnlyCraftable && !RetainerInfo.CacheBuilt)
        {
            if (RetainerInfo.ATools)
                ImGui.TextWrapped($"Building Retainer Cache: {(RetainerInfo.RetainerData.Values.Any() ? RetainerInfo.RetainerData.FirstOrDefault().Value.Count : "0")}/{CraftingListHelpers.FilteredList.Select(x => x.Value).SelectMany(x => x.UnkData5).Where(x => x.ItemIngredient != 0 && x.AmountIngredient > 0).DistinctBy(x => x.ItemIngredient).Count()}");
            ImGui.TextWrapped($"Building Craftable Items List: {CraftingListUI.CraftableItems.Count}/{CraftingListHelpers.FilteredList.Count}");
            ImGui.Spacing();
        }

        ImGui.Text("Search");
        ImGui.SameLine();
        ImGui.InputText("###RecipeSearch", ref Search, 100);
        if (ImGui.Selectable(string.Empty, SelectedRecipe == null))
        {
            SelectedRecipe = null;
        }

        if (P.Config.ShowOnlyCraftable && RetainerInfo.CacheBuilt)
        {
            foreach (var recipe in CraftingListUI.CraftableItems.Where(x => x.Value).Select(x => x.Key).Where(x => x.ItemResult.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)))
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
        else if (!P.Config.ShowOnlyCraftable)
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


    private void DrawRecipeOptions()
    {
        {
            List<uint> craftingJobs = LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.Name.RawString == SelectedRecipe.ItemResult.Value.Name.RawString).Select(x => x.CraftType.Value.RowId + 8).ToList();
            string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => craftingJobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
            ImGui.Text($"Crafted by: {string.Join(", ", jobstrings)}");
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
                    Recipe? ingredientRecipe = CraftingListHelpers.GetIngredientRecipe((uint)value.ItemIngredient);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGuiEx.Text($"{ingredient}");
                    ImGui.TableNextColumn();
                    ImGuiEx.Text($"{value.AmountIngredient}");
                    ImGui.TableNextColumn();
                    var invCount = CraftingListUI.NumberOfIngredient((uint)value.ItemIngredient);
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
                        int retainerCount = 0;
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
                            ImGui.Text(string.Join(", ", jobstrings));
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

    public override void OnClose()
    {
        Table?.Dispose();
        P.ws.RemoveWindow(this);
    }

    private void DrawIngredients()
    {
        if (SelectedList.ID != 0)
        {
            if (SelectedList.Items.Count > 0)
            {
                DrawTotalIngredientsTable();
            }
            else
            {
                ImGui.Text($"Please add items to your list to populate the ingredients tab.");
            }
        }
    }
    private void DrawTotalIngredientsTable()
    {
        if (Table == null)
        {
            ImGui.Text($"Ingredient table is still populating. Please wait.");
            return;
        }
        ImGui.BeginChild("###IngredientsListTable", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 60f));
        Table._nameColumn.ShowColour = ColourValidation;
        Table._inventoryColumn.HQOnlyCrafts = HQSubcraftsOnly;
        Table._retainerColumn.HQOnlyCrafts = HQSubcraftsOnly;
        Table._nameColumn.ShowHQOnly = HQSubcraftsOnly;
        Table.Draw(ImGui.GetTextLineHeightWithSpacing());
        ImGui.EndChild();

        ImGui.Checkbox($"Only show HQ crafts", ref HQSubcraftsOnly);

        ImGuiComponents.HelpMarker($"For ingredients that can be crafted, this will only show inventory{(RetainerInfo.ATools ? " and retainer" : "")} counts that are HQ.");

        ImGui.SameLine();
        ImGui.Checkbox("Enable Colour Validation", ref ColourValidation);

        if (ColourValidation)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.HealerGreen);
            ImGui.BeginDisabled(true);
            ImGui.Button("", new Vector2(23, 23));
            ImGui.EndDisabled();
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 7);
            ImGui.Text($" - Inventory has all required items");

            if (RetainerInfo.ATools)
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudOrange);
                ImGui.BeginDisabled(true);
                ImGui.Button("", new Vector2(23, 23));
                ImGui.EndDisabled();
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 7);
                ImGui.Text($" - Combination of Retainer & Inventory has all required items");
            }

            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.ParsedBlue);
            ImGui.BeginDisabled(true);
            ImGui.Button("", new Vector2(23, 23));
            ImGui.EndDisabled();
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 7);
            ImGui.Text($" - Combination of Inventory & Craftable has all required items.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Need Help?"))
            ImGui.OpenPopup("HelpPopup");

        var windowSize = new Vector2(1024 * ImGuiHelpers.GlobalScale,
            ImGui.GetTextLineHeightWithSpacing() * 13 + 2 * ImGui.GetFrameHeightWithSpacing());
        ImGui.SetNextWindowSize(windowSize);
        ImGui.SetNextWindowPos((ImGui.GetIO().DisplaySize - windowSize) / 2);

        using var popup = ImRaii.Popup("HelpPopup",
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.Modal);
        if (!popup)
            return;

        ImGui.TextWrapped($"This ingredients table shows you everything needed to craft the items on your list. The basic functionality of the table shows you information such as how many of an ingredient is in your inventory, sources of an ingredient, if it has a zone it can be gathered in etc.");
        ImGui.Dummy(new Vector2(0));
        ImGui.BulletText($"You can click on the column headers to filter the results, either through typing or on a pre-determined filter.");
        ImGui.BulletText($"Right clicking on a header will allow you to show/hide different columns or resize columns.");
        ImGui.BulletText($"Right clicking on an ingredient name opens a context menu with further options.");
        ImGui.BulletText($"Clicking and dragging on the space on the headers between columns (as shown by it lighting up) allows you to re-order the columns.");
        ImGui.BulletText($"Don't see any items? Check the table headers for a red heading. This indicates this column is being filtered on. Right clicking the header will clear the filter.");
        ImGui.BulletText($"You can extend the functionality of the table by installing the following plugins:\n- Allagan Tools (Enables all retainer features)\n- Item Vendor Lookup\n- Gatherbuddy\n- Monster Loot Hunter");
        ImGui.BulletText($"Tip: Filter on \"Remaining Needed\" and \"Sources\" when gathering to help filter on items missing, along with sorting by gathered zone\nto help reduce travel times.");

        ImGui.SetCursorPosY(windowSize.Y - ImGui.GetFrameHeight() - ImGui.GetStyle().WindowPadding.Y);
        if (ImGui.Button("Close Help", -Vector2.UnitX))
            ImGui.CloseCurrentPopup();
    }

    private void DrawListSettings()
    {
        ImGui.BeginChild("ListSettings", ImGui.GetContentRegionAvail(), false);
        var skipIfEnough = SelectedList.SkipIfEnough;
        if (ImGui.Checkbox("Skip items you already have enough of", ref skipIfEnough))
        {
            SelectedList.SkipIfEnough = skipIfEnough;
            P.Config.Save();
        }

        if (!RawInformation.Character.CharacterInfo.MateriaExtractionUnlocked())
            ImGui.BeginDisabled();

        var materia = SelectedList.Materia;
        if (ImGui.Checkbox("Automatically Extract Materia", ref materia))
        {
            SelectedList.Materia = materia;
            P.Config.Save();
        }

        if (!RawInformation.Character.CharacterInfo.MateriaExtractionUnlocked())
        {
            ImGui.EndDisabled();

            ImGuiComponents.HelpMarker("This character has not unlocked materia extraction. This setting will be ignored.");
        }
        else
            ImGuiComponents.HelpMarker("Will automatically extract materia from any equipped gear once it's spiritbond is 100%");

        var repair = SelectedList.Repair;
        if (ImGui.Checkbox("Automatic Repairs", ref repair))
        {
            SelectedList.Repair = repair;
            P.Config.Save();
        }

        ImGuiComponents.HelpMarker($"If enabled, Artisan will automatically repair your gear using Dark Matter when any piece reaches the configured repair threshold.\n\nCurrent min gear condition is {RepairManager.GetMinEquippedPercent()}%");

        if (SelectedList.Repair)
        {
            ImGui.PushItemWidth(200);
            if (ImGui.SliderInt("##repairp", ref SelectedList.RepairPercent, 10, 100, "%d%%"))
                P.Config.Save();
        }

        if (ImGui.Checkbox("Set new items added to list as quick synth", ref SelectedList.AddAsQuickSynth))
            P.Config.Save();

        ImGui.EndChild();
    }

    private void DrawRecipes()
    {
        DrawRecipeData();

        ImGui.Spacing();
        RecipeSelector.Draw(RecipeSelector.maxSize + 16f + ImGui.GetStyle().ScrollbarSize);
        ImGui.SameLine();

        if (RecipeSelector.Current > 0)
            ItemDetailsWindow.Draw("Recipe Options", DrawRecipeSettingsHeader, DrawRecipeSettings);
    }

    private void DrawRecipeSettings()
    {

        var selectedListItem = RecipeSelector.Items[RecipeSelector.CurrentIdx];
        var recipe = CraftingListHelpers.FilteredList[RecipeSelector.Current];
        var count = SelectedList.Items.Count(x => x == selectedListItem);

        ImGui.TextWrapped("Adjust Quantity");
        ImGuiEx.SetNextItemFullWidth(-30);
        if (ImGui.InputInt("###AdjustQuantity", ref count))
        {
            if (count > 0)
            {
                var oldCount = SelectedList.Items.Count(x => x == selectedListItem);
                if (oldCount < count)
                {
                    var diff = count - oldCount;
                    for (var i = 1; i <= diff; i++)
                        SelectedList.Items.Insert(
                            SelectedList.Items.IndexOf(selectedListItem),
                            selectedListItem);
                    P.Config.Save();
                }

                if (count < oldCount)
                {
                    var diff = oldCount - count;
                    for (var i = 1; i <= diff; i++) SelectedList.Items.Remove(selectedListItem);
                    P.Config.Save();
                }
            }

            NeedsToRefreshTable = true;
        }

        if (!SelectedList.ListItemOptions.ContainsKey(selectedListItem))
        {
            SelectedList.ListItemOptions.TryAdd(selectedListItem, new ListItemOptions());
            if (SelectedList.AddAsQuickSynth && recipe.CanQuickSynth)
                SelectedList.ListItemOptions[selectedListItem].NQOnly = true;
        }

        SelectedList.ListItemOptions.TryGetValue(selectedListItem, out var options);

        if (recipe.CanQuickSynth)
        {
            var NQOnly = options.NQOnly;
            if (ImGui.Checkbox("Quick Synthesis this item", ref NQOnly))
            {
                options.NQOnly = NQOnly;
                P.Config.Save();
            }
        }
        else
        {
            ImGui.TextWrapped("This item cannot be quick synthed.");
        }

        if (LuminaSheets.RecipeSheet.Values
                .Where(x => x.ItemResult.Value.Name.RawString == selectedListItem.NameOfRecipe()).Count() > 1)
        {
            var pre = $"{LuminaSheets.ClassJobSheet[recipe.CraftType.Row + 8].Abbreviation.RawString}";
            ImGui.TextWrapped("Switch crafted job");
            ImGuiEx.SetNextItemFullWidth(-30);
            if (ImGui.BeginCombo("###SwitchJobCombo", pre))
            {
                foreach (var altJob in LuminaSheets.RecipeSheet.Values.Where(
                             x => x.ItemResult.Value.Name.RawString == selectedListItem.NameOfRecipe()))
                {
                    var altJ = $"{LuminaSheets.ClassJobSheet[altJob.CraftType.Row + 8].Abbreviation.RawString}";
                    if (ImGui.Selectable($"{altJ}"))
                    {
                        for (var i = 0; i < SelectedList.Items.Count; i++)
                        {
                            if (SelectedList.Items[i] == selectedListItem)
                            {
                                SelectedList.Items[i] = altJob.RowId;
                            }
                        }

                        RecipeSelector.Items[RecipeSelector.CurrentIdx] = altJob.RowId;
                        RecipeSelector.Current = RecipeSelector.Items[RecipeSelector.CurrentIdx];

                        if (RecipeSelector.Items.Count(x => x == altJob.RowId) > 1)
                        {
                            var lastindex = RecipeSelector.Items.ToList().LastIndexOf(altJob.RowId);
                            RecipeSelector.Items.RemoveAt(lastindex);
                            var first = RecipeSelector.Items.ToList().IndexOf(altJob.RowId);
                            RecipeSelector.Current = RecipeSelector.Items[first];
                            RecipeSelector.CurrentIdx = first;
                        }

                        NeedsToRefreshTable = true;

                        P.Config.Save();
                    }
                }

                ImGui.EndCombo();
            }
        }
        var buttonWidth = ImGui.CalcTextSize($"Apply to all");
        {
            ImGui.TextWrapped("Use a food item for this recipe");
            ImGuiEx.SetNextItemFullWidth(-30 - (int)buttonWidth.X);
            if (ImGui.BeginCombo(
                    "##foodBuff",
                    ConsumableChecker.Food.TryGetFirst(x => x.Id == options.Food, out var item)
                        ? $"{(options.FoodHQ ? " " : string.Empty)}{item.Name}"
                        : $"{(options.Food == 0 ? "Disabled" : $"{(options.FoodHQ ? " " : string.Empty)}{options.Food}")}"))
            {
                if (ImGui.Selectable("Disable"))
                {
                    options.Food = 0;
                    P.Config.Save();
                }

                foreach (var x in ConsumableChecker.GetFood(true))
                    if (ImGui.Selectable($"{x.Name}"))
                    {
                        options.Food = x.Id;
                        options.FoodHQ = false;
                        P.Config.Save();
                    }

                foreach (var x in ConsumableChecker.GetFood(true, true))
                    if (ImGui.Selectable($" {x.Name}"))
                    {
                        options.Food = x.Id;
                        options.FoodHQ = true;
                        P.Config.Save();
                    }

                ImGui.EndCombo();
            }

            ImGui.SameLine();

            if (ImGui.Button($"Apply to all###FoodApplyAll"))
            {
                foreach (var r in SelectedList.Items.Distinct())
                {
                    if (!SelectedList.ListItemOptions.ContainsKey(r))
                    {
                        SelectedList.ListItemOptions.TryAdd(r, new ListItemOptions());
                        var re = CraftingListHelpers.FilteredList[r];
                        if (SelectedList.AddAsQuickSynth && re.CanQuickSynth)
                            SelectedList.ListItemOptions[r].NQOnly = true;
                    }

                    SelectedList.ListItemOptions.TryGetValue(r, out var o);

                    o.Food = options.Food;
                    o.FoodHQ = options.FoodHQ;
                }

                P.Config.Save();
            }
        }
        {
            ImGui.TextWrapped("Use a potion item for this recipe");
            ImGuiEx.SetNextItemFullWidth(-30 - (int)buttonWidth.X);
            if (ImGui.BeginCombo(
                    "##potBuff",
                    ConsumableChecker.Pots.TryGetFirst(x => x.Id == options.Potion, out var item)
                        ? $"{(options.PotHQ ? " " : string.Empty)}{item.Name}"
                        : $"{(options.Potion == 0 ? "Disabled" : $"{(options.PotHQ ? " " : string.Empty)}{options.Potion}")}"))
            {
                if (ImGui.Selectable("Disabled"))
                {
                    options.Potion = 0;
                    P.Config.Save();
                }

                foreach (var x in ConsumableChecker.GetPots(true))
                    if (ImGui.Selectable($"{x.Name}"))
                    {
                        options.Potion = x.Id;
                        options.PotHQ = false;
                        P.Config.Save();
                    }

                foreach (var x in ConsumableChecker.GetPots(true, true))
                    if (ImGui.Selectable($" {x.Name}"))
                    {
                        options.Potion = x.Id;
                        options.PotHQ = true;
                        P.Config.Save();
                    }

                ImGui.EndCombo();
            }

            ImGui.SameLine();

            if (ImGui.Button($"Apply to all###PotionApplyAll"))
            {
                foreach (var r in SelectedList.Items.Distinct())
                {
                    if (!SelectedList.ListItemOptions.ContainsKey(r))
                    {
                        SelectedList.ListItemOptions.TryAdd(r, new ListItemOptions());
                        var re = CraftingListHelpers.FilteredList[r];
                        if (SelectedList.AddAsQuickSynth && re.CanQuickSynth)
                            SelectedList.ListItemOptions[r].NQOnly = true;
                    }

                    SelectedList.ListItemOptions.TryGetValue(r, out var o);

                    o.Potion = options.Potion;
                    o.PotHQ = options.PotHQ;
                }

                P.Config.Save();
            }
        }

        if (P.Config.UserMacros.Count > 0)
        {
            ImGui.TextWrapped("Use a macro for this recipe");

            var preview = P.Config.IRM.TryGetValue(selectedListItem, out var prevMacro)
                              ? P.Config.UserMacros.First(x => x.ID == prevMacro).Name
                              : string.Empty;
            ImGuiEx.SetNextItemFullWidth(-30);
            if (ImGui.BeginCombo(string.Empty, preview))
            {
                if (ImGui.Selectable(string.Empty))
                {
                    P.Config.IRM.Remove(selectedListItem);
                    P.Config.Save();
                }

                foreach (var macro in P.Config.UserMacros)
                {
                    var selected = P.Config.IRM.TryGetValue(selectedListItem, out var selectedMacro)
                                   && macro.ID == selectedMacro;
                    if (ImGui.Selectable(macro.Name, selected))
                    {
                        P.Config.IRM[selectedListItem] = macro.ID;
                        P.Config.Save();
                    }
                }

                ImGui.EndCombo();
            }
        }
    }

    private void DrawRecipeSettingsHeader()
    {
        if (!RenameMode)
        {
            if (IconButtons.IconTextButton(FontAwesomeIcon.Pen, $"{SelectedList.Name.Replace($"%", "%%")}"))
            {
                newName = SelectedList.Name;
                RenameMode = true;
            }
        }
        else
        {
            if (ImGui.InputText("###RenameMode", ref newName, 200, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (newName.Length > 0)
                {
                    SelectedList.Name = newName;
                    P.Config.Save();
                }
                RenameMode = false;
            }
        }
    }

    public void Dispose()
    {
        RecipeSelector.ItemAdded -= RefreshTable;
        RecipeSelector.ItemDeleted -= RefreshTable;
    }
}

internal class RecipeSelector : ItemSelector<uint>
{
    public float maxSize = 100;

    private readonly CraftingList List;

    public RecipeSelector(int list)
        : base(
            P.Config.CraftingLists.First(x => x.ID == list).Items.Distinct().ToList(),
            Flags.Add | Flags.Delete | Flags.Move)
    {
        List = P.Config.CraftingLists.First(x => x.ID == list);
    }

    protected override bool Filtered(int idx)
    {
        return false;
    }

    protected override bool OnAdd(string name)
    {
        if (name.Trim().All(char.IsDigit))
        {
            var id = Convert.ToUInt32(name);
            if (LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Row == id))
            {
                var recipe = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == id);
                List.Items.Add(recipe.RowId);
                if (!Items.Contains(recipe.RowId)) Items.Add(recipe.RowId);
            }
        }
        else
        {
            if (LuminaSheets.RecipeSheet.Values.FindFirst(
                    x => x.ItemResult.Value.Name.RawString.Equals(name, StringComparison.CurrentCultureIgnoreCase),
                    out var recipe))
            {
                List.Items.Add(recipe.RowId);
                if (!Items.Contains(recipe.RowId)) Items.Add(recipe.RowId);
            }
        }

        P.Config.Save();

        return true;
    }

    protected override bool OnDelete(int idx)
    {
        var itemId = Items[idx];
        List.Items.RemoveAll(x => x == itemId);
        Items.RemoveAt(idx);
        P.Config.Save();
        return true;
    }

    protected override bool OnDraw(int idx)
    {
        var itemId = Items[idx];
        var itemCount = List.Items.Count(x => x == itemId);
        var yield = LuminaSheets.RecipeSheet[itemId].AmountResult * itemCount;
        var label =
            $"{idx + 1}. {Items[idx].NameOfRecipe()} x{itemCount}{(yield != itemCount ? $" ({yield} total)" : string.Empty)}";
        maxSize = ImGui.CalcTextSize(label).X > maxSize ? ImGui.CalcTextSize(label).X : maxSize;

        return ImGui.Selectable(label, idx == CurrentIdx);
    }

    protected override bool OnMove(int idx1, int idx2)
    {
        var item1Idx = List.Items.IndexOf(Items[idx1]);
        var item2Idx = List.Items.IndexOf(Items[idx2]);

        var item1 = Items[idx1];
        var item2 = Items[idx2];

        var item1Count = List.Items.Count(x => x == Items[idx1]);
        var item2Count = List.Items.Count(y => y == Items[idx2]);

        if (item1Idx < item2Idx)
        {
            List.Items.RemoveAll(x => x == item1);

            for (var i = 1; i <= item1Count; i++)
            {
                var index = List.Items.LastIndexOf(item2);
                List.Items.Insert(index + 1, item1);
            }
        }
        else
        {
            List.Items.RemoveAll(x => x == item1);

            for (var i = 1; i <= item1Count; i++)
            {
                var index = List.Items.IndexOf(item2);
                List.Items.Insert(index, item1);
            }
        }

        Items.Move(idx1, idx2);
        P.Config.Save();
        return true;
    }
}

internal class ListFolders : ItemSelector<CraftingList>
{
    public ListFolders()
        : base(P.Config.CraftingLists, Flags.Add | Flags.Delete | Flags.Move | Flags.Filter | Flags.Duplicate)
    {
        CurrentIdx = -1;
    }

    protected override string DeleteButtonTooltip()
    {
        return "Permanently delete this crafting list.\r\nHold Ctrl + Click.\r\nThis cannot be undone.";
    }

    protected override bool Filtered(int idx)
    {
        return Filter.Length != 0 && !Items[idx].Name.Contains(
                   Filter,
                   StringComparison.InvariantCultureIgnoreCase);
    }

    protected override bool OnAdd(string name)
    {
        var list = new CraftingList { Name = name };
        list.SetID();
        list.Save(true);

        return true;
    }

    protected override bool OnDelete(int idx)
    {
        if (P.ws.Windows.FindFirst(
                x => x.WindowName.Contains(CraftingListUI.selectedList.ID.ToString()) && x.GetType() == typeof(ListEditor),
                out var window))
        {
            P.ws.RemoveWindow(window);
        }

        P.Config.CraftingLists.RemoveAt(idx);
        P.Config.Save();

        if (!CraftingListUI.Processing)
        CraftingListUI.selectedList = new CraftingList();
        return true;
    }

    protected override bool OnDraw(int idx)
    {
        if (CraftingListUI.Processing && CraftingListUI.selectedList.ID == P.Config.CraftingLists[idx].ID)
            ImGui.BeginDisabled();

        using var id = ImRaii.PushId(idx);
        var selected = ImGui.Selectable($"{P.Config.CraftingLists[idx].Name} (ID: {P.Config.CraftingLists[idx].ID})", idx == CurrentIdx);
        if (selected)
        {
            if (!P.ws.Windows.Any(x => x.WindowName.Contains(P.Config.CraftingLists[idx].ID.ToString())))
            {
                Interface.SetupValues();
                ListEditor editor = new(P.Config.CraftingLists[idx].ID);
            }
            else
            {
                P.ws.Windows.FindFirst(
                    x => x.WindowName.Contains(P.Config.CraftingLists[idx].ID.ToString()),
                    out var window);
                window.BringToFront();
            }

            if (!CraftingListUI.Processing)
            CraftingListUI.selectedList = P.Config.CraftingLists[idx];
        }

        if (!CraftingListUI.Processing)
        {
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                if (CurrentIdx == idx)
                {
                    CurrentIdx = -1;
                    CraftingListUI.selectedList = new CraftingList();
                }
                else
                {
                    CurrentIdx = idx;
                    CraftingListUI.selectedList = P.Config.CraftingLists[idx];
                }
            }
        }

        if (CraftingListUI.Processing && CraftingListUI.selectedList.ID == P.Config.CraftingLists[idx].ID)
            ImGui.EndDisabled();

        return selected;
    }

    protected override bool OnDuplicate(string name, int idx)
    {
        var baseList = P.Config.CraftingLists[idx];
        CraftingList newList = new CraftingList();
        newList.Name = name;
        newList.SetID();
        newList.Items = baseList.Items.ToList();
        newList.Save();
        return true;
    }

    protected override bool OnMove(int idx1, int idx2)
    {
        P.Config.CraftingLists.Move(idx1, idx2);
        return true;
    }
}