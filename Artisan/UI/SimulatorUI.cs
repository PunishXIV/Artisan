using Artisan.Autocraft;
using Artisan.CraftingLogic;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Microsoft.CodeAnalysis;
using OtterGui;
using System;
using System.Collections.Generic;
using System.Linq;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule;
using static OtterGui.Widgets.Tutorial;

namespace Artisan.UI
{
    public static class SimulatorUI
    {
        static Recipe? SelectedRecipe;
        internal static string Search = string.Empty;
        private static CraftState? _selectedCraft;


        // fields for simulator
        private static Random _simRngForSeeds = new();
        private static int _simCurSeed;
        private static Random _simRngForSim = new();
        private static SolverRef? _selectedSolver;
        private static Solver? _simCurSolver;
        private static int startingQuality = 0;
        private static List<(StepState step, string comment)> _simCurSteps = new();
        private static Solver.Recommendation _simNextRec;
        private static GearsetEntry? SimGS;
        private static string SimGSName;
        private static uint[] SimActionIDs;
        private static ConsumableChoice? SimFood;
        private static ConsumableChoice? SimMedicine;
        private static CharacterStats SimStats;

        // data and other imgui things
        private static Dictionary<uint, List<IngredientLayouts>> ingredientLayouts = new();
        private static float layoutWidth = 0;

        private class IngredientLayouts
        {
            public int Idx;
            public int ID;
            public int NQ;
            public int HQ;
        }

        private class ConsumableChoice
        {
            public uint Id;
            public ConsumableStats Stats;
            public bool ConsumableHQ;
            public string ConsumableString => string.Join(", ", Stats.Stats.Where(x => x.Param != 0).Select(x => $"{Svc.Data.Excel.GetSheet<BaseParam>().GetRow((uint)x.Param).Name} +{x.Percent}% - max {x.Max}"));
        }

        public static void Draw()
        {
            DrawIntro();
            ImGui.Separator();
            DrawRecipeSelector();

            if (SelectedRecipe != null)
            {
                DrawIngredientLayout();

                DrawRecipeInfo();

                DrawGearSetDropdown();

                DrawConsumablesDropdown();

                DrawStatInfo();

                if (ImGui.BeginTabBar("ModeSelection"))
                {
                    if (ImGui.BeginTabItem("Preconfigured Mode"))
                    {
                        DrawPreconfiguredMode();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Solver Mode"))
                    {
                        DrawSolverMode();
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
            }

        }

        private static void DrawIntro()
        {
            ImGuiEx.TextWrapped($"In this simulator, you can test out different solvers against recipes and analyze how well they perform. You can set your HQ ingredient layouts, set consumables and even which gearset to use. The simulator assumes all \"Normal\" conditions, so mileage may vary in actual execution.");
        }

        private static void DrawSolverMode()
        {
            ImGui.Text($"Coming soon?");
        }

        private static void DrawPreconfiguredMode()
        {
            DrawSolverCombo();
            DrawSolverActions();
        }

        private static void DrawSolverActions()
        {
            if (_selectedSolver != null && SimGS != null)
            {
                ImGuiEx.SetNextItemFullWidth();
                if (ImGui.Button($"Solve"))
                {
                    _selectedCraft = Crafting.BuildCraftStateForRecipe(SimStats, Job.CRP + SelectedRecipe.CraftType.Row, SelectedRecipe);
                    _simCurSteps.Clear();
                    _simCurSolver = _selectedSolver?.Clone();
                    if (_simCurSolver != null)
                    {
                        var initial = Simulator.CreateInitial(_selectedCraft, startingQuality);
                        _simCurSteps.Add((initial, ""));
                        _simNextRec = _simCurSolver.Solve(_selectedCraft, initial);
                    }

                    while (SolveNextSimulator(_selectedCraft)) ;
                }

                if (_simCurSolver != null)
                {
                    ImGui.Columns(Math.Min(16, _simCurSteps.Count), null, false);
                    var job = (Job)SimGS?.ClassJob;
                    for (int i = 0; i < _simCurSteps.Count; i++)
                    {
                        if (i + 1 < _simCurSteps.Count)
                        {
                            var currentAction = _simCurSteps[i + 1].step.PrevComboAction;
                            ImGui.Image(P.Icons.LoadIcon(currentAction.IconOfAction(job)).ImGuiHandle, new System.Numerics.Vector2(40));
                            var step = _simCurSteps[i + 1].step;
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGuiEx.Text($"{step.Index - 1}. {currentAction.NameOfAction()}");
                                ImGuiEx.Text($"P: {step.Progress} / {_selectedCraft.CraftProgress} ({Math.Round((float)step.Progress / _selectedCraft.CraftProgress * 100, 0)}%)");
                                ImGuiEx.Text($"Q: {step.Quality} / {_selectedCraft.CraftQualityMax} ({Math.Round((float)step.Quality / _selectedCraft.CraftQualityMax * 100, 0)}%)");
                                ImGuiEx.Text($"D: {step.Durability} / {_selectedCraft.CraftDurability} ({Math.Round((float)step.Durability / _selectedCraft.CraftDurability * 100, 0)}%)");
                                ImGuiEx.Text($"CP: {step.RemainingCP} / {_selectedCraft.StatCP} ({Math.Round((float)step.RemainingCP / _selectedCraft.StatCP * 100, 0)}%)");
                                ImGui.EndTooltip();
                            }

                            ImGui.NextColumn();
                        }
                    }
                    ImGui.Columns(1);

                    var successColor = _simCurSteps.Last().step.Progress >= _selectedCraft.CraftProgress && _simCurSteps.Last().step.Quality >= _selectedCraft.CraftQualityMax ? ImGuiColors.HealerGreen : _simCurSteps.Last().step.Progress < _selectedCraft.CraftProgress ? ImGuiColors.DPSRed : ImGuiColors.DalamudOrange;

                    ImGui.PushStyleColor(ImGuiCol.Text, successColor);  
                    ImGuiEx.ImGuiLineCentered($"SimResults", () => ImGuiEx.TextUnderlined($"Simulator Result"));
                    ImGui.Columns(3, null, false);
                    ImGuiEx.TextCentered($"Quality: {_simCurSteps.Last().step.Quality} / {_selectedCraft.CraftQualityMax} ({Calculations.GetHQChance((double)_simCurSteps.Last().step.Quality/ _selectedCraft.CraftQualityMax * 100)}% HQ Chance)");
                    ImGui.NextColumn();
                    ImGuiEx.TextCentered($"Progress: {_simCurSteps.Last().step.Progress} / {_selectedCraft.CraftProgress}");
                    ImGui.NextColumn();
                    ImGuiEx.TextCentered($"Remaining CP: {_simCurSteps.Last().step.RemainingCP} / {_selectedCraft.StatCP}");
                    ImGui.Columns(1);
                    ImGui.PopStyleColor();
                }
            }
        }

        private static bool SolveNextSimulator(CraftState craft)
        {
            if (_simCurSolver == null || _simCurSteps.Count == 0)
                return false;
            var step = _simCurSteps.Last().step;
            var (res, next) = Simulator.Execute(craft, step, _simNextRec.Action, _simRngForSim.NextSingle(), _simRngForSim.NextSingle());
            if (res == Simulator.ExecuteResult.CantUse)
                return false;
            _simCurSteps[_simCurSteps.Count - 1] = (step, _simNextRec.Comment);
            _simCurSteps.Add((next, ""));
            _simNextRec = _simCurSolver.Solve(craft, next);
            return true;
        }

        private static void DrawSolverCombo()
        {
            ImGui.Text($"Select Solver");
            ImGui.SameLine(120f);
            ImGuiEx.SetNextItemFullWidth();
            using var solverCombo = ImRaii.Combo("###SolverCombo", _selectedSolver == null ? "" : $"{_selectedSolver?.Name}");
            if (!solverCombo)
                return;

            _selectedCraft = Crafting.BuildCraftStateForRecipe(SimStats, Job.CRP + SelectedRecipe.CraftType.Row, SelectedRecipe);
            foreach (var opt in CraftingProcessor.GetAvailableSolversForRecipe(_selectedCraft, false))
            {
                bool selected = ImGui.Selectable(opt.Name);
                if (selected)
                {
                    _selectedSolver = new(opt.Name, opt.CreateSolver(_selectedCraft));
                }
            }
        }

        private static void DrawConsumablesDropdown()
        {
            DrawFoodDropdown();
            DrawMedicineDropdown();
        }

        private static void DrawFoodDropdown()
        {
            ImGui.Text($"Select Food");
            ImGui.SameLine(120f);
            ImGuiEx.SetNextItemFullWidth();
            using var foodCombo = ImRaii.Combo("###SimFood", SimFood is null ? "" : $"{(SimFood.ConsumableHQ ? " " : "")} {LuminaSheets.ItemSheet[SimFood.Id].Name.RawString} ({SimFood.ConsumableString})");
            if (!foodCombo)
                return;

            if (ImGui.Selectable($""))
                SimFood = null;

            foreach (var food in ConsumableChecker.GetFood().OrderBy(x => x.Id))
            {
                var consumableStats = new ConsumableStats(food.Id, false);
                ConsumableChoice choice = new ConsumableChoice() { Id = food.Id, Stats = consumableStats };
                var selected = ImGui.Selectable($"{food.Name} ({choice.ConsumableString})");

                if (selected)
                {
                    choice.ConsumableHQ = false;
                    SimFood = choice;
                    continue;
                }

                consumableStats = new ConsumableStats(food.Id, true);
                choice.Stats = consumableStats;
                if (LuminaSheets.ItemSheet[food.Id].CanBeHq)
                {
                    selected = ImGui.Selectable($" {food.Name} ({choice.ConsumableString})");

                    if (selected)
                    {
                        choice.ConsumableHQ = true;
                        SimFood = choice;
                    }
                }
            }
        }

        private static void DrawMedicineDropdown()
        {
            ImGui.Text($"Select Medicine");
            ImGui.SameLine(120f);
            ImGuiEx.SetNextItemFullWidth();
            using var medicineCombo = ImRaii.Combo("###SimMedicine", SimMedicine is null ? "" : $"{(SimMedicine.ConsumableHQ ? " " : "")} {LuminaSheets.ItemSheet[SimMedicine.Id].Name.RawString} ({SimMedicine.ConsumableString})");
            if (!medicineCombo)
                return;

            if (ImGui.Selectable($""))
                SimMedicine = null;

            foreach (var medicine in ConsumableChecker.GetPots().OrderBy(x => x.Id))
            {
                var consumable = ConsumableChecker.GetItemConsumableProperties(LuminaSheets.ItemSheet[medicine.Id], false);
                if (consumable.UnkData1.Any(x => x.BaseParam is 69 or 68))
                    continue;

                var consumableStats = new ConsumableStats(medicine.Id, false);
                ConsumableChoice choice = new ConsumableChoice() { Id = medicine.Id, Stats = consumableStats };
                var selected = ImGui.Selectable($"{medicine.Name} ({choice.ConsumableString})");

                if (selected)
                {
                    choice.ConsumableHQ = false;
                    SimMedicine = choice;
                    continue;
                }

                consumableStats = new ConsumableStats(medicine.Id, true);
                choice.Stats = consumableStats;
                if (LuminaSheets.ItemSheet[medicine.Id].CanBeHq)
                {
                    selected = ImGui.Selectable($" {medicine.Name} ({choice.ConsumableString})");

                    if (selected)
                    {
                        choice.ConsumableHQ = true;
                        SimMedicine = choice;
                    }
                }
            }
        }

        private static void DrawStatInfo()
        {
            if (SimGS != null)
            {
                ImGuiEx.ImGuiLineCentered("SimulatorStats", () => ImGuiEx.TextUnderlined("Crafter Stats"));
                var gs = SimGS.Value; //Ugh, can't pass nullable refs
                var gsStats = CharacterStats.GetBaseStatsGearset(ref gs);
                var craftsmanshipBoost = (SimFood == null ? 0 : SimFood.Stats.Stats.FirstOrDefault(x => x.Param == 70).Effective(gsStats.Craftsmanship)) + (SimMedicine == null ? 0 : SimMedicine.Stats.Stats.FirstOrDefault(x => x.Param == 70).Effective(gsStats.Craftsmanship));
                var controlBoost = (SimFood == null ? 0 : SimFood.Stats.Stats.FirstOrDefault(x => x.Param == 71).Effective(gsStats.Control)) + (SimMedicine == null ? 0 : SimMedicine.Stats.Stats.FirstOrDefault(x => x.Param == 71).Effective(gsStats.Control));
                var cpBoost = (SimFood == null ? 0 : SimFood.Stats.Stats.FirstOrDefault(x => x.Param == 11).Effective(gsStats.CP)) + (SimMedicine == null ? 0 : SimMedicine.Stats.Stats.FirstOrDefault(x => x.Param == 11).Effective(gsStats.CP));

                ImGui.Columns(3, null, false);
                ImGui.TextWrapped($"Craftsmanship: {gsStats.Craftsmanship + craftsmanshipBoost} ({gsStats.Craftsmanship} + {craftsmanshipBoost})");
                ImGui.NextColumn();
                ImGui.TextWrapped($"Control: {gsStats.Control + controlBoost} ({gsStats.Control} + {controlBoost})");
                ImGui.NextColumn();
                ImGui.TextWrapped($"CP: {gsStats.CP + cpBoost} ({gsStats.CP} + {cpBoost})");
                ImGui.NextColumn();
                ImGui.TextWrapped($"Splendorous Tool: {gsStats.Splendorous}");
                ImGui.NextColumn();
                ImGui.TextWrapped($"Specialist: {gsStats.Specialist}");
                ImGui.Columns(1);

                SimStats = new CharacterStats()
                {
                    Craftsmanship = gsStats.Craftsmanship + craftsmanshipBoost,
                    Control = gsStats.Control + controlBoost,
                    CP = gsStats.CP + cpBoost,
                    Specialist = gsStats.Specialist,
                    Splendorous = gsStats.Splendorous,
                };
            }
        }

        private static unsafe void DrawGearSetDropdown()
        {
            var validGS = RaptureGearsetModule.Instance()->EntriesSpan.ToArray().Count(x => RaptureGearsetModule.Instance()->IsValidGearset(x.ID) && x.ClassJob == SelectedRecipe?.CraftType.Row + 8);

            if (validGS == 0)
            {
                ImGuiEx.Text($"Please add a gearset for {(Job)SelectedRecipe?.CraftType.Row + 8}");
                SimGS = null;
                return;
            }
            if (validGS == 1)
            {
                var gs = RaptureGearsetModule.Instance()->EntriesSpan.ToArray().First(x => RaptureGearsetModule.Instance()->IsValidGearset(x.ID) && x.ClassJob == SelectedRecipe?.CraftType.Row + 8);
                SimGS = gs;
                string name = MemoryHelper.ReadStringNullTerminated(new IntPtr(gs.Name));
                ImGuiEx.Text($"Gearset");
                ImGui.SameLine(120f);
                ImGuiEx.SetNextItemFullWidth();
                ImGuiEx.Text($"{name} (ilvl {SimGS?.ItemLevel})");
                return;
            }


            ImGui.Text($"Select Gearset");
            ImGui.SameLine(120f);
            ImGuiEx.SetNextItemFullWidth();
            using var combo = ImRaii.Combo("###SimGS", SimGS is null ? "" : SimGSName);
            if (!combo)
                return;

            if (ImGui.Selectable($""))
            {
                SimGSName = "";
                SimGS = null;
            }

            foreach (var gs in RaptureGearsetModule.Instance()->EntriesSpan)
            {
                if (!RaptureGearsetModule.Instance()->IsValidGearset(gs.ID)) continue;
                if (gs.ClassJob != SelectedRecipe?.CraftType.Row + 8)
                    continue;

                string name = MemoryHelper.ReadStringNullTerminated(new IntPtr(gs.Name));
                var selected = ImGui.Selectable($"{name} (ilvl {gs.ItemLevel})###GS{gs.ID}");

                if (selected)
                {
                    SimGS = gs;
                    SimGSName = $"{name} (ilvl {gs.ItemLevel})";
                }
            }
        }

        private static void DrawRecipeInfo()
        {
            if (ingredientLayouts.TryGetValue(SelectedRecipe.RowId, out var layouts))
            {
                startingQuality = Calculations.GetStartingQuality(SelectedRecipe, layouts.OrderBy(x => x.Idx).Select(x => x.HQ).ToArray());
                var max = Calculations.RecipeMaxQuality(SelectedRecipe);
                var percentage = Math.Clamp((double)startingQuality / max * 100, 0, 100);
                var hqChance = Calculations.GetHQChance(percentage);

                ImGuiEx.ImGuiLineCentered("StartingQuality", () => ImGuiEx.Text($"Starting Quality: {startingQuality} / {max} ({hqChance}% HQ chance, {percentage.ToString("N0")}% quality)"));
            }

        }

        private static void DrawIngredientLayout()
        {
            bool hasHQ = false;
            foreach (var i in SelectedRecipe.UnkData5.Where(x => x.AmountIngredient > 0))
            {
                if (LuminaSheets.ItemSheet[(uint)i.ItemIngredient].CanBeHq)
                    hasHQ = true;
            }
            using var group = ImRaii.Group();
            if (!group)
                return;

            ImGuiEx.ImGuiLineCentered("###LayoutIngredients", () => ImGuiEx.TextUnderlined("Ingredient Layouts"));
            using var table = ImRaii.Table("###SimulatorRecipeIngredients", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX);
            if (!table)
                return;

            ImGui.TableSetupColumn("Material", ImGuiTableColumnFlags.WidthFixed, ImGui.GetContentRegionAvail().X - (hasHQ ? 200f.Scale() : 80f.Scale()));
            ImGui.TableSetupColumn("NQ", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("HQ", ImGuiTableColumnFlags.WidthFixed);

            ImGui.TableHeadersRow();

            var idx = 0;
            foreach (var i in SelectedRecipe.UnkData5.Where(x => x.AmountIngredient > 0))
            {
                if (ingredientLayouts.TryGetValue(SelectedRecipe.RowId, out var layouts))
                {
                    if (layouts.FindFirst(x => x.ID == i.ItemIngredient, out var layout))
                    {
                        ImGui.TableNextRow();
                        var item = LuminaSheets.ItemSheet[(uint)i.ItemIngredient];
                        ImGui.TableNextColumn();
                        ImGui.Text($"{item.Name}");
                        ImGui.TableNextColumn();
                        if (item.CanBeHq)
                        {
                            ImGui.SetNextItemWidth(80f.Scale());
                            if (ImGui.InputInt($"###InputNQ{item.RowId}", ref layout.NQ))
                            {
                                if (layout.NQ < 0)
                                    layout.NQ = 0;

                                if (layout.NQ > i.AmountIngredient)
                                    layout.NQ = i.AmountIngredient;

                                layout.HQ = i.AmountIngredient - layout.NQ;
                            }
                        }
                        else
                        {
                            ImGui.Text($"{layout.NQ}");
                        }
                        ImGui.TableNextColumn();
                        if (item.CanBeHq)
                        {
                            ImGui.SetNextItemWidth(80f.Scale());
                            if (ImGui.InputInt($"###InputHQ{item.RowId}", ref layout.HQ))
                            {
                                if (layout.HQ < 0)
                                    layout.HQ = 0;

                                if (layout.HQ > i.AmountIngredient)
                                    layout.HQ = i.AmountIngredient;

                                layout.NQ = Math.Min(i.AmountIngredient - layout.HQ, i.AmountIngredient);
                            }
                        }
                        else
                        {
                            ImGui.Text($"{layout.HQ}");
                        }

                    }
                    else
                    {
                        layout = new();
                        layout.Idx = idx;
                        layout.ID = i.ItemIngredient;
                        layout.NQ = i.AmountIngredient;
                        layout.HQ = 0;

                        layouts.Add(layout);
                    }



                }
                else
                {
                    ingredientLayouts.TryAdd(SelectedRecipe.RowId, new List<IngredientLayouts>());
                }
                idx++;
            }
        }

        private static void DrawRecipeSelector()
        {
            var preview = SelectedRecipe is null
                                      ? string.Empty
                                      : $"{SelectedRecipe?.ItemResult.Value.Name.RawString} ({LuminaSheets.ClassJobSheet[SelectedRecipe.CraftType.Row + 8].Abbreviation.RawString})";

            if (ImGui.BeginCombo("Select Recipe", preview))
            {
                try
                {
                    ImGui.Text("Search");
                    ImGui.SameLine();
                    ImGui.InputText("###RecipeSearch", ref Search, 100);

                    if (ImGui.Selectable(string.Empty, SelectedRecipe == null))
                    {
                        SelectedRecipe = null;
                    }


                    foreach (var recipe in LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        ImGui.PushID($"###simRecipe{recipe.RowId}");
                        var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name.RawString} ({LuminaSheets.ClassJobSheet[recipe.CraftType.Row + 8].Abbreviation.RawString} {recipe.RecipeLevelTable.Value.ClassJobLevel})", recipe.RowId == SelectedRecipe?.RowId);

                        if (selected)
                        {
                            SelectedRecipe = recipe;
                            if (SimGS is not null && recipe.CraftType.Row + 8 != SimGS.Value.ClassJob)
                                SimGS = null;

                            _selectedCraft = null;
                            _selectedSolver = null;
                            _simCurSolver = null;
                        }

                        ImGui.PopID();
                    }
                }
                catch (Exception ex)
                {
                    ex.Log();
                }

                ImGui.EndCombo();
            }
        }
    }
}
