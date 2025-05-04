namespace Artisan.UI;

using Autocraft;
using CraftingLists;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using global::Artisan.CraftingLogic;
using global::Artisan.CraftingLogic.Solvers;
using global::Artisan.GameInterop;
using global::Artisan.RawInformation.Character;
using global::Artisan.UI.Tables;
using ImGuiNET;
using IPC;
using Lumina.Excel.Sheets;
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

internal class ListEditor : Window, IDisposable
{
    public bool Minimized = false;

    private Task RegenerateTask = null;
    private CancellationTokenSource source = new CancellationTokenSource();
    private CancellationToken token;

    public bool Processing = false;

    internal List<uint> jobs = new();

    internal List<int> listMaterials = new();

    internal Dictionary<int, int> listMaterialsNew = new();

    internal string newListName = string.Empty;

    internal List<int> rawIngredientsList = new();

    internal Recipe? SelectedRecipe;

    internal Dictionary<uint, int> SelectedRecipeRawIngredients = new();

    internal Dictionary<uint, int> subtableList = new();

    private ListFolders ListsUI = new();

    private bool TidyAfter;

    private int timesToAdd = 1;

    public readonly RecipeSelector RecipeSelector;

    public readonly NewCraftingList SelectedList;

    private string newName = string.Empty;

    private bool RenameMode;

    internal string Search = string.Empty;

    public Dictionary<uint, int> SelectedListMateralsNew = new();

    public IngredientTable? Table;

    private bool ColourValidation = false;

    private bool HQSubcraftsOnly = false;

    private bool NeedsToRefreshTable = false;

    NewCraftingList? copyList;

    IngredientHelpers IngredientHelper = new();

    private bool hqSim = false;

    public ListEditor(int listId)
        : base($"List Editor###{listId}")
    {
        SelectedList = P.Config.NewCraftingLists.First(x => x.ID == listId);
        RecipeSelector = new RecipeSelector(SelectedList.ID);
        RecipeSelector.ItemAdded += RefreshTable;
        RecipeSelector.ItemDeleted += RefreshTable;
        RecipeSelector.ItemSkipTriggered += RefreshTable;
        IsOpen = true;
        P.ws.AddWindow(this);
        Size = new Vector2(1000, 600);
        SizeCondition = ImGuiCond.Appearing;
        ShowCloseButton = true;
        RespectCloseHotkey = false;
        NeedsToRefreshTable = true;

        if (P.Config.DefaultHQCrafts) HQSubcraftsOnly = true;
        if (P.Config.DefaultColourValidation) ColourValidation = true;
    }

    public async Task GenerateTableAsync(CancellationTokenSource source)
    {
        Table?.Dispose();
        var list = await IngredientHelper.GenerateList(SelectedList, source);
        if (list is null)
        {
            Svc.Log.Debug($"Table list empty, aborting.");
            return;
        }

        Table = new IngredientTable(list);
    }

    public void RefreshTable(object? sender, bool e)
    {
        token = source.Token;
        Table = null;
        P.UniversalsisClient.PlayerWorld = Svc.ClientState.LocalPlayer?.CurrentWorld.RowId;
        if (RegenerateTask == null || RegenerateTask.IsCompleted)
        {
            Svc.Log.Debug($"Starting regeneration");
            RegenerateTask = Task.Run(() => GenerateTableAsync(source), token);
        }
        else
        {
            Svc.Log.Debug($"Stopping and restarting regeneration");
            if (source != null)
                source.Cancel();

            source = new();
            token = source.Token;
            RegenerateTask = Task.Run(() => GenerateTableAsync(source), token);
        }
    }

    public override void PreDraw()
    {
        if (!P.Config.DisableTheme)
        {
            P.Style.Push();
            P.StylePushed = true;
        }
    }

    public override void PostDraw()
    {
        if (P.StylePushed)
        {
            P.Style.Pop();
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

    public class ListOrderCheck
    {
        public uint RecID;
        public int RecipeDepth = 0;
        public int RecipeDiff => Calculations.RecipeDifficulty(LuminaSheets.RecipeSheet[RecID]);
        public uint CraftType => LuminaSheets.RecipeSheet[RecID].CraftType.RowId;

        public int ListQuantity = 0;
        public ListItemOptions ops;
    }

    public async override void Draw()
    {
        var btn = ImGuiHelpers.GetButtonSize("Begin Crafting List");

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

        if (ImGui.Button("Export List"))
        {
            ImGui.SetClipboardText(JsonConvert.SerializeObject(P.Config.NewCraftingLists.Where(x => x.ID == SelectedList.ID).First()));
            Notify.Success("List exported to clipboard.");
        }

        var restock = ImGuiHelpers.GetButtonSize("Restock From Retainers");
        if (RetainerInfo.ATools)
        {
            ImGui.SameLine();

            if (Endurance.Enable || CraftingListUI.Processing)
                ImGui.BeginDisabled();

            if (ImGui.Button($"Restock From Retainers"))
            {
                Task.Run(() => RetainerInfo.RestockFromRetainers(SelectedList));
            }

            if (Endurance.Enable || CraftingListUI.Processing)
                ImGui.EndDisabled();
        }
        else
        {
            ImGui.SameLine();

            if (!RetainerInfo.AToolsInstalled)
                ImGuiEx.Text(ImGuiColors.DalamudYellow, $"Please install Allagan Tools for retainer features.");

            if (RetainerInfo.AToolsInstalled && !RetainerInfo.AToolsEnabled)
                ImGuiEx.Text(ImGuiColors.DalamudYellow, $"Please enable Allagan Tools for retainer features.");

            if (RetainerInfo.AToolsEnabled)
                ImGuiEx.Text(ImGuiColors.DalamudYellow, $"You have turned off Allagan Tools integration.");
        }

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
                    RefreshTable(null, true);
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

            if (ImGui.BeginTabItem("Copy From Other List"))
            {
                DrawCopyFromList();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }


    private void DrawCopyFromList()
    {
        if (P.Config.NewCraftingLists.Count > 1)
        {
            ImGuiEx.TextWrapped($"Select List");
            ImGuiEx.SetNextItemFullWidth();
            if (ImGui.BeginCombo("###ListCopyCombo", copyList is null ? "" : copyList.Name))
            {
                if (ImGui.Selectable($""))
                {
                    copyList = null;
                }
                foreach (var list in P.Config.NewCraftingLists.Where(x => x.ID != SelectedList.ID))
                {
                    if (ImGui.Selectable($"{list.Name}###CopyList{list.ID}"))
                    {
                        copyList = list.JSONClone();
                    }
                }

                ImGui.EndCombo();
            }
        }
        else
        {
            ImGui.Text($"Please add other lists to copy from");
        }

        if (copyList != null)
        {
            ImGui.Text($"This will copy:");
            ImGui.Indent();
            if (ImGui.BeginListBox("###ItemList", new(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 30f)))
            {
                foreach (var rec in copyList.Recipes.Distinct())
                {
                    ImGui.Text($"- {LuminaSheets.RecipeSheet[rec.ID].ItemResult.Value.Name.ToDalamudString()} x{rec.Quantity}");
                }

                ImGui.EndListBox();
            }
            ImGui.Unindent();
            if (ImGui.Button($"Copy Items"))
            {
                foreach (var recipe in copyList.Recipes)
                {
                    if (SelectedList.Recipes.Any(x => x.ID == recipe.ID))
                    {
                        SelectedList.Recipes.First(x => x.ID == recipe.ID).Quantity += recipe.Quantity;
                    }
                    else
                        SelectedList.Recipes.Add(new ListItem() { Quantity = recipe.Quantity, ID = recipe.ID });
                }
                Notify.Success($"All items copied from {copyList.Name} to {SelectedList.Name}.");
                RecipeSelector.Items = SelectedList.Recipes.Distinct().ToList();
                RefreshTable(null, true);
                P.Config.Save();
            }
        }
    }

    public void DrawRecipeSubTable()
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
                    if (LuminaSheets.ItemSheet.ContainsKey(item.Key))
                    {
                        if (CraftingListHelpers.SelectedRecipesCraftable[item.Key]) continue;
                        ImGui.PushID($"###SubTableItem{item}");
                        var sheetItem = LuminaSheets.ItemSheet[item.Key];
                        var name = sheetItem.Name.ToString();
                        var count = item.Value;

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text($"{name}");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{count}");
                        ImGui.TableNextColumn();
                        var invcount = CraftingListUI.NumberOfIngredient(item.Key);
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
                Svc.Log.Error(ex, "SubTableRender");
            }

            ImGui.EndTable();
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
                          : $"{SelectedRecipe.Value.ItemResult.Value.Name.ToDalamudString().ToString()} ({LuminaSheets.ClassJobSheet[SelectedRecipe.Value.CraftType.RowId + 8].Abbreviation.ToString()})";

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
                DrawRecipeSubTable();
            }

            ImGui.Spacing();
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().Length() / 2f);
            ImGui.TextWrapped("Number of times to add");
            ImGui.SameLine();
            ImGui.InputInt("###TimesToAdd", ref timesToAdd, 1, 5);
            ImGui.PushItemWidth(-1f);

            if (timesToAdd < 1)
                ImGui.BeginDisabled();

            if (ImGui.Button("Add to List", new Vector2(ImGui.GetContentRegionAvail().X / 2, 30)))
            {
                SelectedListMateralsNew.Clear();
                listMaterialsNew.Clear();

                if (SelectedList.Recipes.Any(x => x.ID == SelectedRecipe.Value.RowId))
                {
                    SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).Quantity += checked(timesToAdd);
                }
                else
                {
                    SelectedList.Recipes.Add(new ListItem() { ID = SelectedRecipe.Value.RowId, Quantity = checked(timesToAdd) });
                }

                if (TidyAfter)
                    CraftingListHelpers.TidyUpList(SelectedList);

                if (SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).ListItemOptions is null)
                {
                    SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).ListItemOptions = new ListItemOptions { NQOnly = SelectedList.AddAsQuickSynth };
                }
                else
                {
                    SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).ListItemOptions.NQOnly = SelectedList.AddAsQuickSynth;
                }

                RecipeSelector.Items = SelectedList.Recipes.Distinct().ToList();

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

                CraftingListUI.AddAllSubcrafts(SelectedRecipe.Value, SelectedList, 1, timesToAdd);

                Svc.Log.Debug($"Adding: {SelectedRecipe.Value.ItemResult.Value.Name.ToDalamudString().ToString()} {timesToAdd} times");
                if (SelectedList.Recipes.Any(x => x.ID == SelectedRecipe.Value.RowId))
                {
                    SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).Quantity += timesToAdd;
                }
                else
                {
                    SelectedList.Recipes.Add(new ListItem() { ID = SelectedRecipe.Value.RowId, Quantity = timesToAdd });
                }

                if (TidyAfter)
                    CraftingListHelpers.TidyUpList(SelectedList);

                if (SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).ListItemOptions is null)
                {
                    SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).ListItemOptions = new ListItemOptions { NQOnly = SelectedList.AddAsQuickSynth };
                }
                else
                {
                    SelectedList.Recipes.First(x => x.ID == SelectedRecipe.Value.RowId).ListItemOptions.NQOnly = SelectedList.AddAsQuickSynth;
                }

                RecipeSelector.Items = SelectedList.Recipes.Distinct().ToList();
                RefreshTable(null, true);
                P.Config.Save();
                if (P.Config.ResetTimesToAdd)
                    timesToAdd = 1;
            }

            if (timesToAdd < 1)
                ImGui.EndDisabled();

            ImGui.Checkbox("Remove all unnecessary subcrafts after adding", ref TidyAfter);
        }

        ImGui.Separator();

        if (ImGui.Button($"Sort Recipes"))
        {
            List<ListItem> newList = new();
            List<ListOrderCheck> order = new();
            foreach (var item in SelectedList.Recipes.Distinct())
            {
                var orderCheck = new ListOrderCheck();
                var r = LuminaSheets.RecipeSheet[item.ID];
                orderCheck.RecID = r.RowId;
                int maxDepth = 0;
                foreach (var ing in r.Ingredients().Where(x => x.Amount > 0).Select(x => x.Item.RowId))
                {
                    CheckIngredientRecipe(ing, orderCheck);
                    if (orderCheck.RecipeDepth > maxDepth)
                    {
                        maxDepth = orderCheck.RecipeDepth;
                    }
                    orderCheck.RecipeDepth = 0;
                }
                orderCheck.RecipeDepth = maxDepth;
                orderCheck.ListQuantity = item.Quantity;
                orderCheck.ops = item.ListItemOptions ?? new ListItemOptions();
                order.Add(orderCheck);
            }

            foreach (var ord in order.OrderBy(x => x.RecipeDepth).ThenBy(x => x.RecipeDiff).ThenBy(x => x.CraftType))
            {
                newList.Add(new ListItem() { ID = ord.RecID, Quantity = ord.ListQuantity, ListItemOptions = ord.ops });
            }

            SelectedList.Recipes = newList;
            RecipeSelector.Items = SelectedList.Recipes.Distinct().ToList();
            P.Config.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGuiEx.Tooltip($"This will sort your list by recipe depth, then difficulty. Recipe depth is defined by how many of the ingredients depend on other recipes on the list.\n\n" +
                $"For example: {LuminaSheets.RecipeSheet[35508].ItemResult.Value.Name.ToDalamudString()} requires {LuminaSheets.ItemSheet[36186].Name}, which in turn requires {LuminaSheets.ItemSheet[36189].Name}, giving this recipe a depth of 3 if all these items are on the list.\n" +
                $"Items that do not have other recipe dependencies have a depth of 1, so go to the top of the list, e.g {LuminaSheets.RecipeSheet[5299].ItemResult.Value.Name.ToDalamudString()}\n\n" +
                $"Finally, this is sorted by the in-game difficulty of the crafts, hopefully grouping together similar crafts.");
        }

        Task.Run(() =>
        {
            listTime = CraftingListUI.GetListTimer(SelectedList);
        });
        string duration = listTime == TimeSpan.Zero ? "Unknown" : string.Format("{0:D2}d {1:D2}h {2:D2}m {3:D2}s", listTime.Days, listTime.Hours, listTime.Minutes, listTime.Seconds);
        ImGui.SameLine();
        ImGui.Text($"Approximate List Time: {duration}");
    }

    TimeSpan listTime;

    private void CheckIngredientRecipe(uint ing, ListOrderCheck orderCheck)
    {
        foreach (var result in SelectedList.Recipes.Distinct().Select(x => LuminaSheets.RecipeSheet[x.ID]))
        {
            if (result.ItemResult.RowId == ing)
            {
                orderCheck.RecipeDepth += 1;
                foreach (var subIng in result.Ingredients().Where(x => x.Amount > 0).Select(x => x.Item.RowId))
                {
                    CheckIngredientRecipe(subIng, orderCheck);
                }
                return;
            }
        }
    }

    private Dictionary<uint, string> RecipeLabels = new Dictionary<uint, string>();
    private void DrawRecipeList()
    {
        if (P.Config.ShowOnlyCraftable && !RetainerInfo.CacheBuilt)
        {
            if (RetainerInfo.ATools)
                ImGui.TextWrapped($"Building Retainer Cache: {(RetainerInfo.RetainerData.Values.Any() ? RetainerInfo.RetainerData.FirstOrDefault().Value.Count : "0")}/{LuminaSheets.RecipeSheet!.Select(x => x.Value).SelectMany(x => x.Ingredients()).Where(x => x.Item.RowId != 0 && x.Amount > 0).DistinctBy(x => x.Item.RowId).Count()}");
            ImGui.TextWrapped($"Building Craftable Items List: {CraftingListUI.CraftableItems.Count}/{LuminaSheets.RecipeSheet.Count}");
            ImGui.Spacing();
        }

        ImGui.Text("Search");
        ImGui.SameLine();
        ImGui.InputText("###RecipeSearch", ref Search, 150);
        if (ImGui.Selectable(string.Empty, SelectedRecipe == null))
        {
            SelectedRecipe = null;
        }

        if (P.Config.ShowOnlyCraftable && RetainerInfo.CacheBuilt)
        {
            foreach (var recipe in CraftingListUI.CraftableItems.Where(x => x.Value).Select(x => x.Key).Where(x => Regex.Match(x.ItemResult.Value.Name.GetText(true), Search, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase).Success))
            {
                if (recipe.Number == 0) continue;
                ImGui.PushID((int)recipe.RowId);
                if (!RecipeLabels.ContainsKey(recipe.RowId))
                {
                    RecipeLabels[recipe.RowId] = $"{recipe.ItemResult.Value.Name.ToDalamudString()} ({LuminaSheets.ClassJobSheet[recipe.CraftType.RowId + 8].Abbreviation} {recipe.RecipeLevelTable.Value.ClassJobLevel})";
                }
                var selected = ImGui.Selectable(RecipeLabels[recipe.RowId], recipe.RowId == SelectedRecipe?.RowId);

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
            foreach (var recipe in LuminaSheets.RecipeSheet.Values)
            {
                try
                {
                    if (recipe.ItemResult.RowId == 0) continue;
                    if (recipe.Number == 0) continue;
                    if (!string.IsNullOrEmpty(Search) && !Regex.Match(recipe.ItemResult.Value.Name.GetText(true), Search, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase).Success) continue;
                    if (!RecipeLabels.ContainsKey(recipe.RowId))
                    {
                        RecipeLabels[recipe.RowId] = $"{recipe.ItemResult.Value.Name.ToDalamudString()} ({LuminaSheets.ClassJobSheet[recipe.CraftType.RowId + 8].Abbreviation} {recipe.RecipeLevelTable.Value.ClassJobLevel})";
                    }
                    var selected = ImGui.Selectable(RecipeLabels[recipe.RowId], recipe.RowId == SelectedRecipe?.RowId);

                    if (selected)
                    {
                        subtableList.Clear();
                        SelectedRecipeRawIngredients.Clear();
                        SelectedRecipe = recipe;
                    }

                }
                catch (Exception ex)
                {
                    Svc.Log.Error(ex, "DrawRecipeList");
                }
            }
        }
    }


    private void DrawRecipeOptions()
    {
        {
            List<uint> craftingJobs = LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.Name.ToDalamudString().ToString() == SelectedRecipe.Value.ItemResult.Value.Name.ToDalamudString().ToString()).Select(x => x.CraftType.Value.RowId + 8).ToList();
            string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => craftingJobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
            ImGui.Text($"Crafted by: {string.Join(", ", jobstrings)}");
        }

        var ItemsRequired = SelectedRecipe.Value.Ingredients();

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
                foreach (var value in ItemsRequired.Where(x => x.Amount > 0))
                {
                    jobs.Clear();
                    string ingredient = LuminaSheets.ItemSheet[value.Item.RowId].Name.ToString();
                    Recipe? ingredientRecipe = CraftingListHelpers.GetIngredientRecipe(value.Item.RowId);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGuiEx.Text($"{ingredient}");
                    ImGui.TableNextColumn();
                    ImGuiEx.Text($"{value.Amount}");
                    ImGui.TableNextColumn();
                    var invCount = CraftingListUI.NumberOfIngredient(value.Item.RowId);
                    ImGuiEx.Text($"{invCount}");

                    if (invCount >= value.Amount)
                    {
                        var color = ImGuiColors.HealerGreen;
                        color.W -= 0.3f;
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                    }

                    ImGui.TableNextColumn();
                    if (RetainerInfo.ATools && RetainerInfo.CacheBuilt)
                    {
                        int retainerCount = 0;
                        retainerCount = RetainerInfo.GetRetainerItemCount(value.Item.RowId);

                        ImGuiEx.Text($"{retainerCount}");

                        if (invCount + retainerCount >= value.Amount)
                        {
                            var color = ImGuiColors.HealerGreen;
                            color.W -= 0.3f;
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                        }

                        ImGui.TableNextColumn();
                    }

                    if (ingredientRecipe is not null)
                    {
                        if (ImGui.Button($"Crafted###search{ingredientRecipe.Value.RowId}"))
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
                            jobs.AddRange(LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.RowId == ingredientRecipe.Value.ItemResult.RowId).Select(x => x.CraftType.RowId + 8));
                            string[]? jobstrings = LuminaSheets.ClassJobSheet.Values.Where(x => jobs.Any(y => y == x.RowId)).Select(x => x.Abbreviation.ToString()).ToArray();
                            ImGui.Text(string.Join(", ", jobstrings));
                        }
                        catch (Exception ex)
                        {
                            Svc.Log.Error(ex, "JobStrings");
                        }

                    }
                    else
                    {
                        try
                        {
                            var gatheringItem = LuminaSheets.GatheringItemSheet?.Where(x => x.Value.Item.RowId == value.Item.RowId).FirstOrDefault().Value;
                            if (gatheringItem != null)
                            {
                                var jobs = LuminaSheets.GatheringPointBaseSheet?.Values.Where(x => x.Item.Any(y => y.RowId == gatheringItem.Value.RowId)).Select(x => x.GatheringType).ToList();
                                List<string> tempArray = new();
                                if (jobs!.Any(x => x.Value.RowId is 0 or 1)) tempArray.Add(LuminaSheets.ClassJobSheet[16].Abbreviation.ToString());
                                if (jobs!.Any(x => x.Value.RowId is 2 or 3)) tempArray.Add(LuminaSheets.ClassJobSheet[17].Abbreviation.ToString());
                                if (jobs!.Any(x => x.Value.RowId is 4 or 5)) tempArray.Add(LuminaSheets.ClassJobSheet[18].Abbreviation.ToString());
                                ImGui.Text($"{string.Join(", ", tempArray)}");
                                continue;
                            }

                            var spearfish = LuminaSheets.SpearfishingItemSheet?.Where(x => x.Value.Item.Value.RowId == value.Item.RowId).FirstOrDefault().Value;
                            if (spearfish != null && spearfish.Value.Item.Value.Name.ToString() == ingredient)
                            {
                                ImGui.Text($"{LuminaSheets.ClassJobSheet[18].Abbreviation.ToString()}");
                                continue;
                            }

                            var fishSpot = LuminaSheets.FishParameterSheet?.Where(x => x.Value.Item.RowId == value.Item.RowId).FirstOrDefault().Value;
                            if (fishSpot != null)
                            {
                                ImGui.Text($"{LuminaSheets.ClassJobSheet[18].Abbreviation.ToString()}");
                            }


                        }
                        catch (Exception ex)
                        {
                            Svc.Log.Error(ex, "JobStrings");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "RecipeIngreds");
            }

            ImGui.EndTable();
        }

    }

    public override void OnClose()
    {
        Table?.Dispose();
        source.Cancel();
        P.ws.RemoveWindow(this);
    }

    private void DrawIngredients()
    {
        if (SelectedList.ID != 0)
        {
            if (SelectedList.Recipes.Count > 0)
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
        if (Table == null && RegenerateTask.IsCompleted)
        {
            if (ImGui.Button($"Something went wrong creating the table. Try again?"))
            {
                RefreshTable(null, true);
            }
            return;
        }
        if (Table == null)
        {
            ImGui.TextUnformatted($"Ingredient table is still populating. Please wait.");
            var a = IngredientHelper.CurrentIngredient;
            var b = IngredientHelper.MaxIngredient;
            ImGui.ProgressBar((float)a / b, new(ImGui.GetContentRegionAvail().X, default), $"{a * 100.0f / b:f2}% ({a}/{b})");
            return;
        }
        ImGui.BeginChild("###IngredientsListTable", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - (ColourValidation ? (RetainerInfo.ATools ? 90f.Scale() : 60f.Scale()) : 30f.Scale())));
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

        ImGui.SameLine();

        if (ImGui.GetIO().KeyShift)
        {
            if (ImGui.Button($"Export Required Ingredients as Plain Text"))
            {
                StringBuilder sb = new();
                foreach (var item in Table.ListItems.Where(x => x.Required > 0))
                {
                    sb.AppendLine($"{item.Required}x {item.Data.Name}");
                }

                if (!string.IsNullOrEmpty(sb.ToString()))
                {
                    ImGui.SetClipboardText(sb.ToString());
                    Notify.Success($"Required items copied to clipboard.");
                }
                else
                {
                    Notify.Error($"No items required to be copied.");
                }
            }
        }
        else
        {
            if (ImGui.Button($"Export Remaining Ingredients as Plain Text"))
            {
                StringBuilder sb = new();
                foreach (var item in Table.ListItems.Where(x => x.Remaining > 0))
                {
                    sb.AppendLine($"{item.Remaining}x {item.Data.Name}");
                }

                if (!string.IsNullOrEmpty(sb.ToString()))
                {
                    ImGui.SetClipboardText(sb.ToString());
                    Notify.Success($"Remaining items copied to clipboard.");
                }
                else
                {
                    Notify.Error($"No items remaining to be copied.");
                }
            }

            if (ImGui.IsItemHovered())
            {
                ImGuiEx.Tooltip($"Hold shift to change from remaining to required.");
            }

        }


        ImGui.SameLine();
        if (ImGui.Button("Need Help?"))
            ImGui.OpenPopup("HelpPopup");

        if (ColourValidation)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.HealerGreen);
            ImGui.BeginDisabled(true);
            ImGui.Button("", new Vector2(23, 23));
            ImGui.EndDisabled();
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - 7);
            ImGui.Text($" - Inventory has all required items {(SelectedList.SkipIfEnough && SelectedList.SkipLiteral ? "" : "or is not required due to owning crafted materials using this ingredient")}");

            if (RetainerInfo.ATools)
            {
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
        if (ImGui.Checkbox("Skip Crafting Unnecessary Materials", ref skipIfEnough))
        {
            SelectedList.SkipIfEnough = skipIfEnough;
            P.Config.Save();
        }
        ImGuiComponents.HelpMarker($"Will skip crafting any unnecessary materials required for your list.");

        if (skipIfEnough)
        {
            ImGui.Indent();
            if (ImGui.Checkbox("Skip Up To List Amount", ref SelectedList.SkipLiteral))
            {
                P.Config.Save();
            }

            ImGuiComponents.HelpMarker("Will continue to craft materials whilst your inventory has less of a material up to the amount the list would craft if starting from zero.\n\n" +
                "[Recipe Amount Result] x [Number of Crafts] is less than [Inventory Amount].\n\n" +
                "Use this when crafting materials for items not on your list (eg FC workshop projects)\n\n" +
                "This will also adjust the ingredient table's remaining column and colour validation to exclude checking for crafted items the ingredient may be used in.");
            ImGui.Unindent();
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

        ImGuiComponents.HelpMarker($"If enabled, Artisan will automatically repair your gear when any piece reaches the configured repair threshold.\n\nCurrent min gear condition is {RepairManager.GetMinEquippedPercent()}% and cost to repair at a vendor is {RepairManager.GetNPCRepairPrice()} gil.\n\nIf unable to repair with Dark Matter, will try for a nearby repair NPC.");

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

        if (RecipeSelector.Current?.ID > 0)
            ItemDetailsWindow.Draw("Recipe Options", DrawRecipeSettingsHeader, DrawRecipeSettings);
    }

    private void DrawRecipeSettings()
    {
        var selectedListItem = RecipeSelector.Items[RecipeSelector.CurrentIdx].ID;
        var recipe = LuminaSheets.RecipeSheet[RecipeSelector.Current.ID];
        var count = RecipeSelector.Items[RecipeSelector.CurrentIdx].Quantity;

        ImGui.TextWrapped("Adjust Quantity");
        ImGuiEx.SetNextItemFullWidth(-30);
        if (ImGui.InputInt("###AdjustQuantity", ref count))
        {
            if (count >= 0)
            {
                SelectedList.Recipes.First(x => x.ID == selectedListItem).Quantity = count;
                P.Config.Save();
            }

            NeedsToRefreshTable = true;
        }

        if (SelectedList.Recipes.First(x => x.ID == selectedListItem).ListItemOptions is null)
        {
            SelectedList.Recipes.First(x => x.ID == selectedListItem).ListItemOptions = new();
        }

        var options = SelectedList.Recipes.First(x => x.ID == selectedListItem).ListItemOptions;

        if (recipe.CanQuickSynth)
        {
            var NQOnly = options.NQOnly;
            if (ImGui.Checkbox("Quick Synthesis this item", ref NQOnly))
            {
                options.NQOnly = NQOnly;
                P.Config.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("Apply To all###QuickSynthAll"))
            {
                foreach (var r in SelectedList.Recipes.Where(n => LuminaSheets.RecipeSheet[n.ID].CanQuickSynth))
                {
                    if (r.ListItemOptions == null)
                    { r.ListItemOptions = new(); }
                    r.ListItemOptions.NQOnly = options.NQOnly;
                }
                Notify.Success($"Quick Synth applied to all list items.");
                P.Config.Save();
            }

            if (NQOnly && !P.Config.UseConsumablesQuickSynth)
            {
                if (ImGui.Checkbox("You do not have quick synth consumables enabled. Turn this on?", ref P.Config.UseConsumablesQuickSynth))
                    P.Config.Save();
            }
        }
        else
        {
            ImGui.TextWrapped("This item cannot be quick synthed.");
        }

        // Retrieve the list of recipes matching the selected recipe name from the preprocessed lookup table.
        var matchingRecipes = LuminaSheets.recipeLookup[selectedListItem.NameOfRecipe()].ToList();

        if (matchingRecipes.Count > 1)
        {
            var pre = $"{LuminaSheets.ClassJobSheet[recipe.CraftType.RowId + 8].Abbreviation.ToString()}";
            ImGui.TextWrapped("Switch crafted job");
            ImGuiEx.SetNextItemFullWidth(-30);
            if (ImGui.BeginCombo("###SwitchJobCombo", pre))
            {
                foreach (var altJob in matchingRecipes)
                {
                    var altJ = $"{LuminaSheets.ClassJobSheet[altJob.CraftType.RowId + 8].Abbreviation.ToString()}";
                    if (ImGui.Selectable($"{altJ}"))
                    {
                        try
                        {
                            if (SelectedList.Recipes.Any(x => x.ID == altJob.RowId))
                            {
                                SelectedList.Recipes.First(x => x.ID == altJob.RowId).Quantity += SelectedList.Recipes.First(x => x.ID == selectedListItem).Quantity;
                                SelectedList.Recipes.Remove(SelectedList.Recipes.First(x => x.ID == selectedListItem));
                                RecipeSelector.Items.RemoveAt(RecipeSelector.CurrentIdx);
                                RecipeSelector.Current = RecipeSelector.Items.First(x => x.ID == altJob.RowId);
                                RecipeSelector.CurrentIdx = RecipeSelector.Items.IndexOf(RecipeSelector.Current);
                            }
                            else
                            {
                                SelectedList.Recipes.First(x => x.ID == selectedListItem).ID = altJob.RowId;
                                RecipeSelector.Items[RecipeSelector.CurrentIdx].ID = altJob.RowId;
                                RecipeSelector.Current = RecipeSelector.Items[RecipeSelector.CurrentIdx];
                            }
                            NeedsToRefreshTable = true;

                            P.Config.Save();
                        }
                        catch
                        {

                        }
                    }
                }

                ImGui.EndCombo();
            }
        }

        var config = P.Config.RecipeConfigs.GetValueOrDefault(selectedListItem) ?? new();
        {
            if (config.DrawFood(true))
            {
                P.Config.RecipeConfigs[selectedListItem] = config;
                P.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button($"Apply to all###FoodApplyAll"))
            {
                foreach (var r in SelectedList.Recipes.Distinct())
                {
                    var o = P.Config.RecipeConfigs.GetValueOrDefault(r.ID) ?? new();
                    o.requiredFood = config.requiredFood;
                    o.requiredFoodHQ = config.requiredFoodHQ;
                    P.Config.RecipeConfigs[r.ID] = o;
                }
                P.Config.Save();
            }
        }
        {
            if (config.DrawPotion(true))
            {
                P.Config.RecipeConfigs[selectedListItem] = config;
                P.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button($"Apply to all###PotionApplyAll"))
            {
                foreach (var r in SelectedList.Recipes.Distinct())
                {
                    var o = P.Config.RecipeConfigs.GetValueOrDefault(r.ID) ?? new();
                    o.requiredPotion = config.requiredPotion;
                    o.requiredPotionHQ = config.requiredPotionHQ;
                    P.Config.RecipeConfigs[r.ID] = o;
                }
                P.Config.Save();
            }
        }

        {
            if (config.DrawManual(true))
            {
                P.Config.RecipeConfigs[selectedListItem] = config;
                P.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button($"Apply to all###ManualApplyAll"))
            {
                foreach (var r in SelectedList.Recipes.Distinct())
                {
                    var o = P.Config.RecipeConfigs.GetValueOrDefault(r.ID) ?? new();
                    o.requiredManual = config.requiredManual;
                    P.Config.RecipeConfigs[r.ID] = o;
                }
                P.Config.Save();
            }
        }

        {
            if (config.DrawSquadronManual(true))
            {
                P.Config.RecipeConfigs[selectedListItem] = config;
                P.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button($"Apply to all###SquadManualApplyAll"))
            {
                foreach (var r in SelectedList.Recipes.Distinct())
                {
                    var o = P.Config.RecipeConfigs.GetValueOrDefault(r.ID) ?? new();
                    o.requiredSquadronManual = config.requiredSquadronManual;
                    P.Config.RecipeConfigs[r.ID] = o;
                }
                P.Config.Save();
            }
        }

        var stats = CharacterStats.GetBaseStatsForClassHeuristic(Job.CRP + recipe.CraftType.RowId);
        stats.AddConsumables(new(config.RequiredFood, config.RequiredFoodHQ), new(config.RequiredPotion, config.RequiredPotionHQ), CharacterInfo.FCCraftsmanshipbuff);
        var craft = Crafting.BuildCraftStateForRecipe(stats, Job.CRP + recipe.CraftType.RowId, recipe);
        if (config.DrawSolver(craft))
        {
            P.Config.RecipeConfigs[selectedListItem] = config;
            P.Config.Save();
        }
        
        ImGuiEx.TextV("Requirements:");
        ImGui.SameLine();
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0, ImGui.GetStyle().ItemSpacing.Y));
        ImGui.SameLine(137.6f.Scale());
        ImGui.TextWrapped($"Difficulty: {craft.CraftProgress} | Durability: {craft.CraftDurability} | Quality: {(craft.CraftCollectible ? craft.CraftQualityMin3 : craft.CraftQualityMax)}");
        ImGuiComponents.HelpMarker($"Shows the crafting requirements: Progress needed to complete the craft, how much Durability the recipe has, and Quality target required to reach the highest Quality level (In case of a Collectible). Use this information to select an appropriate macro, if desired.");

        ImGui.Checkbox($"Assume Max Starting Quality (for simulator)", ref hqSim);

        var solverHint = Simulator.SimulatorResult(recipe, config, craft, out var hintColor, hqSim);
        if (!recipe.IsExpert)
            ImGuiEx.TextWrapped(hintColor, solverHint);
        else
            ImGuiEx.TextWrapped($"Please run this recipe in the simulator for results.");
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
        source.Cancel();
        RecipeSelector.ItemAdded -= RefreshTable;
        RecipeSelector.ItemDeleted -= RefreshTable;
        RecipeSelector.ItemSkipTriggered -= RefreshTable;
    }
}

internal class RecipeSelector : ItemSelector<ListItem>
{
    public float maxSize = 100;

    private readonly NewCraftingList List;

    public RecipeSelector(int list)
        : base(
            P.Config.NewCraftingLists.First(x => x.ID == list).Recipes.Distinct().ToList(),
            Flags.Add | Flags.Delete | Flags.Move)
    {
        List = P.Config.NewCraftingLists.First(x => x.ID == list);
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
            if (LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.RowId == id))
            {
                var recipe = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.RowId == id);
                if (recipe.Number == 0) return false;
                if (List.Recipes.Any(x => x.ID == recipe.RowId))
                {
                    List.Recipes.First(x => x.ID == recipe.RowId).Quantity += 1;
                }
                else
                {
                    List.Recipes.Add(new ListItem() { ID = recipe.RowId, Quantity = 1 });
                }

                if (!Items.Any(x => x.ID == recipe.RowId)) Items.Add(List.Recipes.First(x => x.ID == recipe.RowId));
            }
        }
        else
        {
            if (LuminaSheets.RecipeSheet.Values.FindFirst(
                    x => x.ItemResult.Value.Name.ToDalamudString().ToString().Equals(name, StringComparison.CurrentCultureIgnoreCase),
                    out var recipe))
            {
                if (recipe.Number == 0) return false;
                if (List.Recipes.Any(x => x.ID == recipe.RowId))
                {
                    List.Recipes.First(x => x.ID == recipe.RowId).Quantity += 1;
                }
                else
                {
                    List.Recipes.Add(new ListItem() { ID = recipe.RowId, Quantity = 1 });
                }

                if (!Items.Any(x => x.ID == recipe.RowId)) Items.Add(List.Recipes.First(x => x.ID == recipe.RowId));
            }
        }

        P.Config.Save();

        return true;
    }

    protected override bool OnDelete(int idx)
    {
        var ItemId = Items[idx];
        List.Recipes.Remove(ItemId);
        Items.RemoveAt(idx);
        P.Config.Save();
        return true;
    }

    protected override bool OnDraw(int idx, out bool changes)
    {
        changes = false;
        var ItemId = Items[idx];
        var itemCount = ItemId.Quantity;
        var yield = LuminaSheets.RecipeSheet[ItemId.ID].AmountResult * itemCount;
        var label =
            $"{idx + 1}. {ItemId.ID.NameOfRecipe()} x{itemCount}{(yield != itemCount ? $" ({yield} total)" : string.Empty)}";
        maxSize = ImGui.CalcTextSize(label).X > maxSize ? ImGui.CalcTextSize(label).X : maxSize;

        if (ItemId.ListItemOptions is null)
        {
            ItemId.ListItemOptions = new();
            P.Config.Save();
        }

        using (var col = ImRaii.PushColor(ImGuiCol.Text, itemCount == 0 || ItemId.ListItemOptions.Skipping ? ImGuiColors.DalamudRed : ImGuiColors.DalamudWhite))
        {
            var res = ImGui.Selectable(label, idx == CurrentIdx);
            ImGuiEx.Tooltip($"Right click to {(ItemId.ListItemOptions.Skipping ? "enable" : "skip")} this recipe.");
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                ItemId.ListItemOptions.Skipping = !ItemId.ListItemOptions.Skipping;
                changes = true;
                P.Config.Save();
            }
            return res;
        }
    }

    protected override bool OnMove(int idx1, int idx2)
    {
        List.Recipes.Move(idx1, idx2);
        Items.Move(idx1, idx2);
        P.Config.Save();
        return true;
    }
}

internal class ListFolders : ItemSelector<NewCraftingList>
{
    public ListFolders()
        : base(P.Config.NewCraftingLists, Flags.Add | Flags.Delete | Flags.Move | Flags.Filter | Flags.Duplicate)
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
        var list = new NewCraftingList { Name = name };
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

        P.Config.NewCraftingLists.RemoveAt(idx);
        P.Config.Save();

        if (!CraftingListUI.Processing)
            CraftingListUI.selectedList = new NewCraftingList();
        return true;
    }

    protected override bool OnDraw(int idx, out bool changes)
    {
        changes = false;
        if (CraftingListUI.Processing && CraftingListUI.selectedList.ID == P.Config.NewCraftingLists[idx].ID)
            ImGui.BeginDisabled();

        using var id = ImRaii.PushId(idx);
        var selected = ImGui.Selectable($"{P.Config.NewCraftingLists[idx].Name} (ID: {P.Config.NewCraftingLists[idx].ID})", idx == CurrentIdx);
        if (selected)
        {
            if (!P.ws.Windows.Any(x => x.WindowName.Contains(P.Config.NewCraftingLists[idx].ID.ToString())))
            {
                Interface.SetupValues();
                ListEditor editor = new(P.Config.NewCraftingLists[idx].ID);
            }
            else
            {
                P.ws.Windows.FindFirst(
                    x => x.WindowName.Contains(P.Config.NewCraftingLists[idx].ID.ToString()),
                    out var window);
                window.BringToFront();
            }

            if (!CraftingListUI.Processing)
                CraftingListUI.selectedList = P.Config.NewCraftingLists[idx];
        }

        if (!CraftingListUI.Processing)
        {
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                if (CurrentIdx == idx)
                {
                    CurrentIdx = -1;
                    CraftingListUI.selectedList = new NewCraftingList();
                }
                else
                {
                    CurrentIdx = idx;
                    CraftingListUI.selectedList = P.Config.NewCraftingLists[idx];
                }
            }
        }

        if (CraftingListUI.Processing && CraftingListUI.selectedList.ID == P.Config.NewCraftingLists[idx].ID)
            ImGui.EndDisabled();

        return selected;
    }

    protected override bool OnDuplicate(string name, int idx)
    {
        var baseList = P.Config.NewCraftingLists[idx];
        NewCraftingList newList = new NewCraftingList();
        newList = baseList.JSONClone();
        newList.Name = name;
        newList.SetID();
        newList.Save();
        return true;
    }

    protected override bool OnMove(int idx1, int idx2)
    {
        P.Config.NewCraftingLists.Move(idx1, idx2);
        return true;
    }
}