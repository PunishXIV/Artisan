using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.RawInformation;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ECommons;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using ImGuiNET;
using OtterGui;
using OtterGui.Filesystem;
using OtterGui.Raii;
using PunishLib.ImGuiMethods;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

namespace Artisan.UI
{
    internal class ListEditor : Window
    {
        private CraftingList SelectedList;
        private RecipeSelector RecipeSelector;

        public ListEditor(int listId) : base($"List Editor###{listId}")
        {
            SelectedList = P.config.CraftingLists.First(x => x.ID == listId);
            RecipeSelector = new(SelectedList.ID);
            this.IsOpen = true;
            P.ws.AddWindow(this);
            this.Size = new Vector2(600, 600);
            this.SizeCondition = ImGuiCond.Appearing;
            ShowCloseButton = true;
        }

        public override void OnClose()
        {
           P.ws.RemoveWindow(this); 
        }

        public override void Draw()
        {
            if (ImGui.BeginTabBar("CraftingListEditor", ImGuiTabBarFlags.None))
            {
                if (ImGui.BeginTabItem("Recipes"))
                {
                    DrawRecipes();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Ingredients"))
                {
                    DrawIngredients();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("List Settings"))
                {
                    DrawListSettings();
                    ImGui.EndTabItem();
                }
            }

        }

        private void DrawListSettings()
        {
            bool skipIfEnough = SelectedList.SkipIfEnough;
            if (ImGui.Checkbox("Skip items you already have enough of", ref skipIfEnough))
            {
                SelectedList.SkipIfEnough = skipIfEnough;
                Service.Configuration.Save();
            }

            bool materia = SelectedList.Materia;
            if (ImGui.Checkbox("Automatically Extract Materia", ref materia))
            {
                SelectedList.Materia = materia;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Will automatically extract materia from any equipped gear once it's spiritbond is 100%");

            bool repair = SelectedList.Repair;
            if (ImGui.Checkbox("Automatic Repairs", ref repair))
            {
                SelectedList.Repair = repair;
                Service.Configuration.Save();
            }

            ImGuiComponents.HelpMarker("If enabled, Artisan will automatically repair your gear using Dark Matter when any piece reaches the configured repair threshold.");
            if (SelectedList.Repair)
            {
                ImGui.PushItemWidth(200);
                if (ImGui.SliderInt("##repairp", ref SelectedList.RepairPercent, 10, 100, $"%d%%"))
                {
                    Service.Configuration.Save();
                }
            }
            if (ImGui.Checkbox("Set new items added to list as quick synth", ref SelectedList.AddAsQuickSynth))
            {
                Service.Configuration.Save();
            }
        }

        private void DrawIngredients()
        {
            throw new NotImplementedException();
        }

        private void DrawRecipes()
        {
            RecipeSelector.Draw(RecipeSelector.maxSize + 10f);
            ImGui.SameLine();

            if (RecipeSelector.Current > 0)
            ItemDetailsWindow.Draw("Recipe Options", DrawRecipeSettingsHeader, DrawRecipeSettings);
        }

        private void DrawRecipeSettings()
        {
            var selectedListItem = RecipeSelector.Current;
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
                        for (int i = 1; i <= diff; i++)
                        {
                            SelectedList.Items.Insert(SelectedList.Items.IndexOf(selectedListItem), selectedListItem);
                        }
                        Service.Configuration.Save();
                    }
                    if (count < oldCount)
                    {
                        var diff = oldCount - count;
                        for (int i = 1; i <= diff; i++)
                        {
                            SelectedList.Items.Remove(selectedListItem);
                        }
                        Service.Configuration.Save();
                    }
                }
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
                ImGuiEx.SetNextItemFullWidth(-30);
                if (ImGui.BeginCombo("###SwitchJobCombo", pre))
                {
                    foreach (var altJob in LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.Name.RawString == selectedListItem.NameOfRecipe()))
                    {
                        string altJ = $"{LuminaSheets.ClassJobSheet[altJob.CraftType.Row + 8].Abbreviation.RawString}";
                        if (ImGui.Selectable($"{altJ}"))
                        {
                            for (int i = 0; i < SelectedList.Items.Count; i++)
                            {
                                if (SelectedList.Items[i] == selectedListItem)
                                    SelectedList.Items[i] = altJob.RowId;
                            }

                            selectedListItem = altJob.RowId;
                            Service.Configuration.Save();
                        }

                    }

                    ImGui.EndCombo();
                }

            }

            {
                ImGui.TextWrapped($"Use a food item for this recipe");
                ImGuiEx.SetNextItemFullWidth(-30);
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

            {
                ImGui.TextWrapped($"Use a potion item for this recipe");
                ImGuiEx.SetNextItemFullWidth(-30);
                if (ImGui.BeginCombo("##potBuff", ConsumableChecker.Pots.TryGetFirst(x => x.Id == options.Potion, out var item) ? $"{(options.PotHQ ? " " : "")}{item.Name}" : $"{(options.Potion == 0 ? "Disabled" : $"{(options.PotHQ ? " " : "")}{options.Potion}")}"))
                {
                    if (ImGui.Selectable("Disabled"))
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
                ImGui.TextWrapped($"Use a macro for this recipe");

                string? preview = Service.Configuration.IRM.TryGetValue((uint)selectedListItem, out var prevMacro) ? Service.Configuration.UserMacros.First(x => x.ID == prevMacro).Name : "";
                ImGuiEx.SetNextItemFullWidth(-30);
                if (ImGui.BeginCombo("", preview))
                {
                    if (ImGui.Selectable(""))
                    {
                        Service.Configuration.IRM.Remove(selectedListItem);
                        Service.Configuration.Save();
                    }
                    foreach (var macro in Service.Configuration.UserMacros)
                    {
                        bool selected = Service.Configuration.IRM.TryGetValue((uint)selectedListItem, out var selectedMacro);
                        if (ImGui.Selectable(macro.Name, selected))
                        {
                            Service.Configuration.IRM[(uint)selectedListItem] = macro.ID;
                            Service.Configuration.Save();
                        }
                    }

                    ImGui.EndCombo();
                }
            }
        }

        private bool RenameMode = false;
        private string newName = string.Empty;
        private void DrawRecipeSettingsHeader()
        {
            if (!RenameMode)
            {
                if (IconButtons.IconTextButton(Dalamud.Interface.FontAwesomeIcon.Pen, $"{SelectedList.Name}"))
                {
                    newName = SelectedList.Name;
                    RenameMode = true;
                }
            }
            else
            {
                if (ImGui.InputText("###RenameMode", ref newName, 200, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    SelectedList.Name = newName;
                    P.config.Save();
                    RenameMode = false;
                }
            }

            if (ImGuiEx.ButtonCtrl("Delete List"))
            {
                P.config.CraftingLists.RemoveAll(x => x.ID == SelectedList.ID);
                P.config.Save();
                P.ws.RemoveWindow(this);
            }
        }
    }

    internal class RecipeSelector : ItemSelector<uint>
    {
        CraftingList List;
        public float maxSize = 100;

        public RecipeSelector(int list)
            : base(P.config.CraftingLists.First(x => x.ID == list).Items.Distinct().ToList(), Flags.Add | Flags.Delete | Flags.Move)
        {
            this.List = P.config.CraftingLists.First(x => x.ID == list);
        }

        protected override bool OnDraw(int idx)
        {
            var itemId = Items[idx];
            var itemCount = List.Items.Count(x => x == itemId);
            var yield = LuminaSheets.RecipeSheet[itemId].AmountResult * itemCount;
            string label = $"{idx + 1}. {Items[idx].NameOfRecipe()} x{itemCount}{(yield != itemCount ? $" ({yield} total)" : "")}";
            maxSize = ImGui.CalcTextSize(label).X > maxSize ? ImGui.CalcTextSize(label).X : maxSize;

            return ImGui.Selectable(label, idx == CurrentIdx);
        }

        protected override bool OnDelete(int idx)
        {
            var itemId = Items[idx];
            List.Items.RemoveAll(x => x == itemId);
            Items.RemoveAt(idx);
            P.config.Save();
            return true;
        }

        protected override bool OnAdd(string name)
        {
            if (name.Trim().All(char.IsDigit))
            {
                uint id = Convert.ToUInt32(name);
                if (LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Row == id))
                {
                    var recipe = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == id);
                    List.Items.Add(recipe.RowId);
                    if (!Items.Contains(recipe.RowId))
                        Items.Add(recipe.RowId);
                }
            }
            else
            {
                if (LuminaSheets.RecipeSheet.Values.FindFirst(x => x.ItemResult.Value.Name.RawString.Equals(name, StringComparison.CurrentCultureIgnoreCase), out var recipe))
                {
                    List.Items.Add(recipe.RowId);
                    if (!Items.Contains(recipe.RowId))
                        Items.Add(recipe.RowId);
                }
            }

            P.config.Save();

            return true;
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

                for (int i = 1; i <= item1Count; i++)
                {
                    var index = List.Items.LastIndexOf(item2);
                    List.Items.Insert(index + 1, item1);
                }
            }
            else
            {
                List.Items.RemoveAll(x => x == item1);

                for (int i = 1; i <= item1Count; i++)
                {
                    var index = List.Items.IndexOf(item2);
                    List.Items.Insert(index, item1);
                }
            }

            Items.Move(idx1, idx2);
            P.config.Save();
            return true;
        }

        protected override bool Filtered(int idx)
            => false;
    }


    internal class ListFolders : ItemSelector<CraftingList>
    {
        public ListFolders()
            : base(P.config.CraftingLists, Flags.Add | Flags.Delete | Flags.Move | Flags.Filter) 
        {
            CurrentIdx = -1;
        }

        protected override bool Filtered(int idx)
            => Filter.Length != 0 && !Items[idx].Name.Contains(Filter, StringComparison.InvariantCultureIgnoreCase);

        protected override bool OnDraw(int idx)
        {
            using var id = ImRaii.PushId(idx);
            var selected = ImGui.Selectable(P.config.CraftingLists[idx].Name, idx == CurrentIdx);
            if (selected)
            {
                ListEditor editor = new(P.config.CraftingLists[idx].ID);
                CraftingListUI.selectedList = P.config.CraftingLists[idx];
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                if (CurrentIdx == idx)
                {
                    CurrentIdx = -1;
                    CraftingListUI.selectedList = new();
                }
                else
                {
                    CurrentIdx = idx;
                    CraftingListUI.selectedList = P.config.CraftingLists[idx];
                }
            }

            return selected;
        }

        protected override bool OnMove(int idx1, int idx2)
        {
            P.config.CraftingLists.Move(idx1, idx2);
            return true;
        }

        protected override bool OnAdd(string name)
        {
            var list = new CraftingList() { Name = name };
            list.SetID();
            list.Save(true);

            return true;
        }

        protected override bool OnDelete(int idx)
        {
            P.config.CraftingLists.RemoveAt(idx);
            P.config.Save();
            CraftingListUI.selectedList = new();
            return true;
        }
    }
}
