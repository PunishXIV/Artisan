namespace Artisan.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;

using ECommons;
using ECommons.ImGuiMethods;
using ECommons.Reflection;

using global::Artisan.Autocraft;
using global::Artisan.CraftingLists;
using global::Artisan.IPC;
using global::Artisan.RawInformation;

using ImGuiNET;

using Lumina.Excel.GeneratedSheets;

using OtterGui;
using OtterGui.Filesystem;
using OtterGui.Raii;

using PunishLib.ImGuiMethods;

internal class ListEditor : Window
{
    public static Dictionary<Recipe, bool> CraftableItems = new();

    public static bool Minimized = false;

    public static bool Processing = false;

    internal static List<uint> jobs = new();

    internal static List<int> listMaterials = new();

    internal static Dictionary<int, int> listMaterialsNew = new();

    internal static string newListName = string.Empty;

    internal static List<int> rawIngredientsList = new();

    internal static CraftingList selectedList = new();

    internal static uint selectedListItem;

    internal static Recipe? SelectedRecipe;

    internal static Dictionary<int, int> SelectedRecipeRawIngredients = new();

    internal static Dictionary<int, int> subtableList = new();

    private static ListFolders ListsUI = new();

    private static string? renameList;

    private static bool renameMode = false;

    private static bool TidyAfter;

    private static int timesToAdd = 1;

    private readonly RecipeSelector RecipeSelector;

    private readonly CraftingList SelectedList;

    private string newName = string.Empty;

    private bool RenameMode;

    internal static string Search = string.Empty;

    public ListEditor(int listId)
        : base($"List Editor###{listId}")
    {
        this.SelectedList = P.config.CraftingLists.First(x => x.ID == listId);
        this.RecipeSelector = new RecipeSelector(this.SelectedList.ID);
        this.IsOpen = true;
        P.ws.AddWindow(this);
        this.Size = new Vector2(600, 600);
        this.SizeCondition = ImGuiCond.Appearing;
        this.ShowCloseButton = true;
    }

    public override void PreDraw()
    {
        if (!P.config.DisableTheme)
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

    public override void Draw()
    {
        if (ImGui.BeginTabBar("CraftingListEditor", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("Recipes"))
            {
                this.DrawRecipes();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Ingredients"))
            {
                this.DrawIngredients();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("List Settings"))
            {
                this.DrawListSettings();
                ImGui.EndTabItem();
            }
        }
    }

    public void DrawRecipeData()
    {
        var showOnlyCraftable = Service.Configuration.ShowOnlyCraftable;

        if (ImGui.Checkbox("###ShowCraftableCheckbox", ref showOnlyCraftable))
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
        ImGui.TextWrapped("Show only recipes you have materials for (toggle to refresh)");

        if (Service.Configuration.ShowOnlyCraftable && RetainerInfo.ATools)
        {
            var showOnlyCraftableRetainers = Service.Configuration.ShowOnlyCraftableRetainers;
            if (ImGui.Checkbox("###ShowCraftableRetainersCheckbox", ref showOnlyCraftableRetainers))
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

        var preview = SelectedRecipe is null
                          ? string.Empty
                          : $"{SelectedRecipe.ItemResult.Value.Name.RawString} ({LuminaSheets.ClassJobSheet[SelectedRecipe.CraftType.Row + 8].Abbreviation.RawString})";

        if (ImGui.BeginCombo("Select Recipe", preview))
        {
            this.DrawRecipeList();

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
                CraftingListHelpers.SelectedListMateralsNew.Clear();
                listMaterialsNew.Clear();

                for (var i = 0; i < timesToAdd; i++)
                    if (this.SelectedList.Items.IndexOf(SelectedRecipe.RowId) == -1)
                    {
                        this.SelectedList.Items.Add(SelectedRecipe.RowId);
                    }
                    else
                    {
                        var indexOfLast = this.SelectedList.Items.IndexOf(SelectedRecipe.RowId);
                        this.SelectedList.Items.Insert(indexOfLast, SelectedRecipe.RowId);
                    }

                if (TidyAfter)
                    CraftingListHelpers.TidyUpList(this.SelectedList);

                if (this.SelectedList.ListItemOptions.TryGetValue(SelectedRecipe.RowId, out var opts))
                    opts.NQOnly = this.SelectedList.AddAsQuickSynth;
                else
                    this.SelectedList.ListItemOptions.TryAdd(
                        SelectedRecipe.RowId,
                        new ListItemOptions {NQOnly = this.SelectedList.AddAsQuickSynth});

                this.RecipeSelector.Items = this.SelectedList.Items.Distinct().ToList();
                Service.Configuration.Save();
                if (Service.Configuration.ResetTimesToAdd)
                    timesToAdd = 1;
            }

            ImGui.SameLine();
            if (ImGui.Button("Add to List (with all sub-crafts)", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
            {
                CraftingListHelpers.SelectedListMateralsNew.Clear();
                listMaterialsNew.Clear();

                CraftingListUI.AddAllSubcrafts(SelectedRecipe, this.SelectedList, 1, timesToAdd);

                PluginLog.Debug($"Adding: {SelectedRecipe.ItemResult.Value.Name.RawString} {timesToAdd} times");
                for (var i = 1; i <= timesToAdd; i++)
                    if (this.SelectedList.Items.IndexOf(SelectedRecipe.RowId) == -1)
                    {
                        this.SelectedList.Items.Add(SelectedRecipe.RowId);
                    }
                    else
                    {
                        var indexOfLast = this.SelectedList.Items.IndexOf(SelectedRecipe.RowId);
                        this.SelectedList.Items.Insert(indexOfLast, SelectedRecipe.RowId);
                    }

                if (TidyAfter)
                    CraftingListHelpers.TidyUpList(this.SelectedList);

                if (this.SelectedList.ListItemOptions.TryGetValue(SelectedRecipe.RowId, out var opts))
                    opts.NQOnly = this.SelectedList.AddAsQuickSynth;
                else
                    this.SelectedList.ListItemOptions.TryAdd(
                        SelectedRecipe.RowId,
                        new ListItemOptions {NQOnly = this.SelectedList.AddAsQuickSynth});

                this.RecipeSelector.Items = this.SelectedList.Items.Distinct().ToList();
                Service.Configuration.Save();
                if (Service.Configuration.ResetTimesToAdd)
                    timesToAdd = 1;
            }

            ImGui.Checkbox("Remove all unnecessary subcrafts after adding", ref TidyAfter);
        }
    }

    private void DrawRecipeList()
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
        if (ImGui.Selectable(string.Empty, SelectedRecipe == null))
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


    private static void DrawRecipeOptions()
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
                    Recipe? ingredientRecipe = CraftingListHelpers.GetIngredientRecipe(value.ItemIngredient);
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
        P.ws.RemoveWindow(this);
    }

    private void DrawIngredients()
    {
       
    }

    private void DrawListSettings()
    {
        var skipIfEnough = this.SelectedList.SkipIfEnough;
        if (ImGui.Checkbox("Skip items you already have enough of", ref skipIfEnough))
        {
            this.SelectedList.SkipIfEnough = skipIfEnough;
            Service.Configuration.Save();
        }

        var materia = this.SelectedList.Materia;
        if (ImGui.Checkbox("Automatically Extract Materia", ref materia))
        {
            this.SelectedList.Materia = materia;
            Service.Configuration.Save();
        }

        ImGuiComponents.HelpMarker(
            "Will automatically extract materia from any equipped gear once it's spiritbond is 100%");

        var repair = this.SelectedList.Repair;
        if (ImGui.Checkbox("Automatic Repairs", ref repair))
        {
            this.SelectedList.Repair = repair;
            Service.Configuration.Save();
        }

        ImGuiComponents.HelpMarker(
            "If enabled, Artisan will automatically repair your gear using Dark Matter when any piece reaches the configured repair threshold.");
        if (this.SelectedList.Repair)
        {
            ImGui.PushItemWidth(200);
            if (ImGui.SliderInt("##repairp", ref this.SelectedList.RepairPercent, 10, 100, "%d%%"))
                Service.Configuration.Save();
        }

        if (ImGui.Checkbox("Set new items added to list as quick synth", ref this.SelectedList.AddAsQuickSynth))
            Service.Configuration.Save();
    }

    private void DrawRecipes()
    {
        this.DrawRecipeData();

        ImGui.Spacing();
        this.RecipeSelector.Draw(this.RecipeSelector.maxSize + 10f);
        ImGui.SameLine();

        if (this.RecipeSelector.Current > 0)
            ItemDetailsWindow.Draw("Recipe Options", this.DrawRecipeSettingsHeader, this.DrawRecipeSettings);
    }

    private void DrawRecipeSettings()
    {
        var selectedListItem = this.RecipeSelector.Current;
        var recipe = CraftingListHelpers.FilteredList[this.RecipeSelector.Current];
        var count = this.SelectedList.Items.Count(x => x == selectedListItem);

        ImGui.TextWrapped("Adjust Quantity");
        ImGuiEx.SetNextItemFullWidth(-30);
        if (ImGui.InputInt("###AdjustQuantity", ref count))
            if (count > 0)
            {
                var oldCount = this.SelectedList.Items.Count(x => x == selectedListItem);
                if (oldCount < count)
                {
                    var diff = count - oldCount;
                    for (var i = 1; i <= diff; i++)
                        this.SelectedList.Items.Insert(
                            this.SelectedList.Items.IndexOf(selectedListItem),
                            selectedListItem);
                    Service.Configuration.Save();
                }

                if (count < oldCount)
                {
                    var diff = oldCount - count;
                    for (var i = 1; i <= diff; i++) this.SelectedList.Items.Remove(selectedListItem);
                    Service.Configuration.Save();
                }
            }

        if (!this.SelectedList.ListItemOptions.ContainsKey(selectedListItem))
        {
            this.SelectedList.ListItemOptions.TryAdd(selectedListItem, new ListItemOptions());
            if (this.SelectedList.AddAsQuickSynth && recipe.CanQuickSynth)
                this.SelectedList.ListItemOptions[selectedListItem].NQOnly = true;
        }

        this.SelectedList.ListItemOptions.TryGetValue(selectedListItem, out var options);

        if (recipe.CanQuickSynth)
        {
            var NQOnly = options.NQOnly;
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
                        for (var i = 0; i < this.SelectedList.Items.Count; i++)
                            if (this.SelectedList.Items[i] == selectedListItem)
                                this.SelectedList.Items[i] = altJob.RowId;

                        selectedListItem = altJob.RowId;
                        Service.Configuration.Save();
                    }
                }

                ImGui.EndCombo();
            }
        }
        {
            ImGui.TextWrapped("Use a food item for this recipe");
            ImGuiEx.SetNextItemFullWidth(-30);
            if (ImGui.BeginCombo(
                    "##foodBuff",
                    ConsumableChecker.Food.TryGetFirst(x => x.Id == options.Food, out var item)
                        ? $"{(options.FoodHQ ? " " : string.Empty)}{item.Name}"
                        : $"{(options.Food == 0 ? "Disabled" : $"{(options.FoodHQ ? " " : string.Empty)}{options.Food}")}"))
            {
                if (ImGui.Selectable("Disable"))
                {
                    options.Food = 0;
                    Service.Configuration.Save();
                }

                foreach (var x in ConsumableChecker.GetFood(true))
                    if (ImGui.Selectable($"{x.Name}"))
                    {
                        options.Food = x.Id;
                        options.FoodHQ = false;
                        Service.Configuration.Save();
                    }

                foreach (var x in ConsumableChecker.GetFood(true, true))
                    if (ImGui.Selectable($" {x.Name}"))
                    {
                        options.Food = x.Id;
                        options.FoodHQ = true;
                        Service.Configuration.Save();
                    }

                ImGui.EndCombo();
            }
        }
        {
            ImGui.TextWrapped("Use a potion item for this recipe");
            ImGuiEx.SetNextItemFullWidth(-30);
            if (ImGui.BeginCombo(
                    "##potBuff",
                    ConsumableChecker.Pots.TryGetFirst(x => x.Id == options.Potion, out var item)
                        ? $"{(options.PotHQ ? " " : string.Empty)}{item.Name}"
                        : $"{(options.Potion == 0 ? "Disabled" : $"{(options.PotHQ ? " " : string.Empty)}{options.Potion}")}"))
            {
                if (ImGui.Selectable("Disabled"))
                {
                    options.Potion = 0;
                    Service.Configuration.Save();
                }

                foreach (var x in ConsumableChecker.GetPots(true))
                    if (ImGui.Selectable($"{x.Name}"))
                    {
                        options.Potion = x.Id;
                        options.PotHQ = false;
                        Service.Configuration.Save();
                    }

                foreach (var x in ConsumableChecker.GetPots(true, true))
                    if (ImGui.Selectable($" {x.Name}"))
                    {
                        options.Potion = x.Id;
                        options.PotHQ = true;
                        Service.Configuration.Save();
                    }

                ImGui.EndCombo();
            }
        }

        if (Service.Configuration.UserMacros.Count > 0)
        {
            ImGui.TextWrapped("Use a macro for this recipe");

            var preview = Service.Configuration.IRM.TryGetValue(selectedListItem, out var prevMacro)
                              ? Service.Configuration.UserMacros.First(x => x.ID == prevMacro).Name
                              : string.Empty;
            ImGuiEx.SetNextItemFullWidth(-30);
            if (ImGui.BeginCombo(string.Empty, preview))
            {
                if (ImGui.Selectable(string.Empty))
                {
                    Service.Configuration.IRM.Remove(selectedListItem);
                    Service.Configuration.Save();
                }

                foreach (var macro in Service.Configuration.UserMacros)
                {
                    var selected = Service.Configuration.IRM.TryGetValue(selectedListItem, out var selectedMacro)
                                   && macro.ID == selectedMacro;
                    if (ImGui.Selectable(macro.Name, selected))
                    {
                        Service.Configuration.IRM[selectedListItem] = macro.ID;
                        Service.Configuration.Save();
                    }
                }

                ImGui.EndCombo();
            }
        }
    }

    private void DrawRecipeSettingsHeader()
    {
        if (!this.RenameMode)
        {
            if (IconButtons.IconTextButton(FontAwesomeIcon.Pen, $"{this.SelectedList.Name}"))
            {
                this.newName = this.SelectedList.Name;
                this.RenameMode = true;
            }
        }
        else
        {
            if (ImGui.InputText("###RenameMode", ref this.newName, 200, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                this.SelectedList.Name = this.newName;
                P.config.Save();
                this.RenameMode = false;
            }
        }
    }
}

internal class RecipeSelector : ItemSelector<uint>
{
    public float maxSize = 100;

    private readonly CraftingList List;

    public RecipeSelector(int list)
        : base(
            P.config.CraftingLists.First(x => x.ID == list).Items.Distinct().ToList(),
            Flags.Add | Flags.Delete | Flags.Move)
    {
        this.List = P.config.CraftingLists.First(x => x.ID == list);
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
                this.List.Items.Add(recipe.RowId);
                if (!this.Items.Contains(recipe.RowId)) this.Items.Add(recipe.RowId);
            }
        }
        else
        {
            if (LuminaSheets.RecipeSheet.Values.FindFirst(
                    x => x.ItemResult.Value.Name.RawString.Equals(name, StringComparison.CurrentCultureIgnoreCase),
                    out var recipe))
            {
                this.List.Items.Add(recipe.RowId);
                if (!this.Items.Contains(recipe.RowId)) this.Items.Add(recipe.RowId);
            }
        }

        P.config.Save();

        return true;
    }

    protected override bool OnDelete(int idx)
    {
        var itemId = this.Items[idx];
        this.List.Items.RemoveAll(x => x == itemId);
        this.Items.RemoveAt(idx);
        P.config.Save();
        return true;
    }

    protected override bool OnDraw(int idx)
    {
        var itemId = this.Items[idx];
        var itemCount = this.List.Items.Count(x => x == itemId);
        var yield = LuminaSheets.RecipeSheet[itemId].AmountResult * itemCount;
        var label =
            $"{idx + 1}. {this.Items[idx].NameOfRecipe()} x{itemCount}{(yield != itemCount ? $" ({yield} total)" : string.Empty)}";
        this.maxSize = ImGui.CalcTextSize(label).X > this.maxSize ? ImGui.CalcTextSize(label).X : this.maxSize;

        return ImGui.Selectable(label, idx == this.CurrentIdx);
    }

    protected override bool OnMove(int idx1, int idx2)
    {
        var item1Idx = this.List.Items.IndexOf(this.Items[idx1]);
        var item2Idx = this.List.Items.IndexOf(this.Items[idx2]);

        var item1 = this.Items[idx1];
        var item2 = this.Items[idx2];

        var item1Count = this.List.Items.Count(x => x == this.Items[idx1]);
        var item2Count = this.List.Items.Count(y => y == this.Items[idx2]);

        if (item1Idx < item2Idx)
        {
            this.List.Items.RemoveAll(x => x == item1);

            for (var i = 1; i <= item1Count; i++)
            {
                var index = this.List.Items.LastIndexOf(item2);
                this.List.Items.Insert(index + 1, item1);
            }
        }
        else
        {
            this.List.Items.RemoveAll(x => x == item1);

            for (var i = 1; i <= item1Count; i++)
            {
                var index = this.List.Items.IndexOf(item2);
                this.List.Items.Insert(index, item1);
            }
        }

        this.Items.Move(idx1, idx2);
        P.config.Save();
        return true;
    }
}

internal class ListFolders : ItemSelector<CraftingList>
{
    public ListFolders()
        : base(P.config.CraftingLists, Flags.Add | Flags.Delete | Flags.Move | Flags.Filter | Flags.Duplicate)
    {
        this.CurrentIdx = -1;
    }

    protected override string DeleteButtonTooltip()
    {
        return "Permanently delete this crafting list.\r\nHold Ctrl + Click.\r\nThis cannot be undone.";
    }

    protected override bool Filtered(int idx)
    {
        return this.Filter.Length != 0 && !this.Items[idx].Name.Contains(
                   this.Filter,
                   StringComparison.InvariantCultureIgnoreCase);
    }

    protected override bool OnAdd(string name)
    {
        var list = new CraftingList {Name = name};
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

        P.config.CraftingLists.RemoveAt(idx);
        P.config.Save();
        CraftingListUI.selectedList = new CraftingList();
        return true;
    }

    protected override bool OnDraw(int idx)
    {
        using var id = ImRaii.PushId(idx);
        var selected = ImGui.Selectable(P.config.CraftingLists[idx].Name, idx == this.CurrentIdx);
        if (selected)
        {
            if (!P.ws.Windows.Any(x => x.WindowName.Contains(P.config.CraftingLists[idx].ID.ToString())))
            {
                ListEditor editor = new(P.config.CraftingLists[idx].ID);
            }
            else
            {
                P.ws.Windows.FindFirst(
                    x => x.WindowName.Contains(P.config.CraftingLists[idx].ID.ToString()),
                    out var window);
                window.BringToFront();
            }

            CraftingListUI.selectedList = P.config.CraftingLists[idx];
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            if (this.CurrentIdx == idx)
            {
                this.CurrentIdx = -1;
                CraftingListUI.selectedList = new CraftingList();
            }
            else
            {
                this.CurrentIdx = idx;
                CraftingListUI.selectedList = P.config.CraftingLists[idx];
            }
        }

        return selected;
    }

    protected override bool OnDuplicate(string name, int idx)
    {
        var baseList = P.config.CraftingLists[idx];
        CraftingList newList = new CraftingList(); 
        newList.Name = name;
        newList.SetID();
        newList.Items = baseList.Items;
        newList.Save();
        return true;
    }

    protected override bool OnMove(int idx1, int idx2)
    {
        P.config.CraftingLists.Move(idx1, idx2);
        return true;
    }
}