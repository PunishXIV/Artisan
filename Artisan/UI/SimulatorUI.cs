using Artisan.Autocraft;
using Artisan.CraftingLogic;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
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
using System.Numerics;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule;
using Condition = Artisan.CraftingLogic.CraftData.Condition;

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
        private static List<Skills> SimActionIDs = new();
        private static ConsumableChoice? SimFood;
        private static ConsumableChoice? SimMedicine;
        private static CharacterStats SimStats;
        private static bool assumeNormalStatus;

        // data and other imgui things
        private static Dictionary<uint, List<IngredientLayouts>> ingredientLayouts = new();
        private static float layoutWidth = 0;
        private static float widgetSize => P.Config.SimulatorActionSize;

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
            if (SimGS != null)
            {
                DrawActionWidgets();
                ImGui.Separator();
                DrawSimulation();
            }
            else
            {
                ImGui.Text($"Please have a gearset selected from above to use this feature.");
            }
        }

        private static void DrawSimulation()
        {

            if (ImGui.Button($"Reset"))
            {
                _selectedCraft = Crafting.BuildCraftStateForRecipe(SimStats, Job.CRP + SelectedRecipe.CraftType.Row, SelectedRecipe);
                //InitDefaultTransitionProbabilities(_selectedCraft, SelectedRecipe);
                SimActionIDs.Clear();
                _simCurSteps.Clear();
                var initial = Simulator.CreateInitial(_selectedCraft, startingQuality);
                _simCurSteps.Add((initial, ""));
            }

            if (_selectedCraft != null && _simCurSteps != null && _simCurSteps.Count > 0)
            {
                ImGui.Columns(16, null, false);
                var job = (Job)SimGS?.ClassJob;
                for (int i = 0; i < _simCurSteps.Count; i++)
                {
                    if (i + 1 < _simCurSteps.Count)
                    {
                        var currentAction = _simCurSteps[i + 1].step.PrevComboAction;
                        ImGui.Image(P.Icons.LoadIcon(currentAction.IconOfAction(job)).ImGuiHandle, new Vector2(widgetSize));
                        var step = _simCurSteps[i + 1].step;
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGuiEx.Text($"{step.Index - 1}. {currentAction.NameOfAction()}");
                            ImGuiEx.Text($"P: {step.Progress} / {_selectedCraft.CraftProgress} ({Math.Round((float)step.Progress / _selectedCraft.CraftProgress * 100, 0)}%)");
                            ImGuiEx.Text($"Q: {step.Quality} / {_selectedCraft.CraftQualityMax} ({Math.Round((float)step.Quality / _selectedCraft.CraftQualityMax * 100, 0)}%)");
                            ImGuiEx.Text($"D: {step.Durability} / {_selectedCraft.CraftDurability} ({Math.Round((float)step.Durability / _selectedCraft.CraftDurability * 100, 0)}%)");
                            ImGuiEx.Text($"CP: {step.RemainingCP} / {_selectedCraft.StatCP} ({Math.Round((float)step.RemainingCP / _selectedCraft.StatCP * 100, 0)}%)");
                            ImGuiEx.Text($"Condition: {_simCurSteps[i].step.Condition} -> {step.Condition}");
                            ImGui.EndTooltip();
                        }
                        if (ImGui.IsItemClicked())
                        {
                            SimActionIDs.RemoveAt(i);
                            ResolveSteps();
                        }

                        ImGui.NextColumn();
                    }
                }
                ImGui.Columns(1);

                var status = Simulator.Status(_selectedCraft, _simCurSteps.Last().step);
                var successColor = status == Simulator.CraftStatus.InProgress ? ImGuiColors.DalamudWhite : _simCurSteps.Last().step.Progress >= _selectedCraft.CraftProgress && _simCurSteps.Last().step.Quality >= _selectedCraft.CraftQualityMax ? ImGuiColors.HealerGreen : _simCurSteps.Last().step.Progress < _selectedCraft.CraftProgress ? ImGuiColors.DPSRed : ImGuiColors.DalamudOrange;

                float qualityPercent = (float)(_simCurSteps.Last().step.Quality / _selectedCraft.CraftQualityMax);
                float progressPercent = (float)(_simCurSteps.Last().step.Progress / _selectedCraft.CraftProgress);
                float CPPercent = (float)(_simCurSteps.Last().step.RemainingCP / _selectedCraft.StatCP);

                ImGui.PushStyleColor(ImGuiCol.Text, successColor);
                ImGuiEx.ImGuiLineCentered($"SimResults", () => ImGuiEx.TextUnderlined($"Simulator Result - {status.ToOutputString()}"));
                ImGui.Columns(4, null, false);
                ImGuiEx.TextCentered($"Quality");
                ImGuiEx.SetNextItemFullWidth();
                DrawProgress(_simCurSteps.Last().step.Quality, _selectedCraft.CraftQualityMax);
                ImGui.NextColumn();
                ImGuiEx.TextCentered($"Progress");
                ImGuiEx.SetNextItemFullWidth();
                DrawProgress(_simCurSteps.Last().step.Progress, _selectedCraft.CraftProgress);
                ImGui.NextColumn();
                ImGuiEx.TextCentered($"CP");
                ImGuiEx.SetNextItemFullWidth();
                DrawProgress(_simCurSteps.Last().step.RemainingCP, _selectedCraft.StatCP);
                ImGui.NextColumn();
                ImGuiEx.TextCentered($"Durability");
                ImGuiEx.SetNextItemFullWidth();
                DrawProgress(_simCurSteps.Last().step.Durability, _selectedCraft.CraftDurability);
                ImGui.Columns(1);
                ImGui.PopStyleColor();
            }

        }

        private static void ResolveSteps()
        {
            _simCurSteps.Clear();
            _selectedCraft = Crafting.BuildCraftStateForRecipe(SimStats, Job.CRP + SelectedRecipe.CraftType.Row, SelectedRecipe);
            var initial = Simulator.CreateInitial(_selectedCraft, startingQuality);
            _simCurSteps.Add((initial, ""));
            for (int i = 0; i < SimActionIDs.Count; i++)
            {
                var step = Simulator.Execute(_selectedCraft, _simCurSteps.Last().step, SimActionIDs[i], 0, 1);
                if (step.Item1 == Simulator.ExecuteResult.Succeeded)
                    _simCurSteps.Add((step.Item2, ""));
            }
        }

        private static void DrawProgress(int a, int b) => ImGui.ProgressBar((float)a / b, new(0), $"{a * 100.0f / b:f2}% ({a}/{b})");

        private static bool ActionChild(string label, int itemCount, System.Action func)
        {
            Vector2 childSize = new Vector2((widgetSize + ImGui.GetStyle().ItemSpacing.X) * itemCount + ImGui.GetStyle().WindowPadding.X, (ImGui.GetTextLineHeightWithSpacing() + (widgetSize + ImGui.GetStyle().WindowPadding.Y) + 12f));
            if (ImGui.BeginChild(label, childSize, true))
            {
                ImGuiEx.ImGuiLineCentered($"{label}", () => ImGuiEx.TextUnderlined($"{label}"));
                func();
                ImGui.EndChild();
                return true;
            }
            return false;
        }
        private static void DrawActionWidgets()
        {
            ActionChild("Progress Actions", 7, () =>
            {
                DrawActionWidget(Skills.BasicSynthesis);
                DrawActionWidget(Skills.CarefulSynthesis);
                DrawActionWidget(Skills.PrudentSynthesis);
                DrawActionWidget(Skills.RapidSynthesis);
                DrawActionWidget(Skills.Groundwork);
                DrawActionWidget(Skills.FocusedSynthesis);
                DrawActionWidget(Skills.IntensiveSynthesis);
            });

            ImGui.SameLine();
            ActionChild("Quality Actions", 11, () =>
            {
                DrawActionWidget(Skills.BasicTouch);
                DrawActionWidget(Skills.StandardTouch);
                DrawActionWidget(Skills.AdvancedTouch);
                DrawActionWidget(Skills.HastyTouch);
                DrawActionWidget(Skills.ByregotsBlessing);
                DrawActionWidget(Skills.PreciseTouch);
                DrawActionWidget(Skills.FocusedTouch);
                DrawActionWidget(Skills.PrudentTouch);
                DrawActionWidget(Skills.TrainedEye);
                DrawActionWidget(Skills.PreparatoryTouch);
                DrawActionWidget(Skills.TrainedFinesse);

            });

            ActionChild("Buff Actions", 8, () =>
            {
                DrawActionWidget(Skills.WasteNot);
                DrawActionWidget(Skills.WasteNot2);
                DrawActionWidget(Skills.GreatStrides);
                DrawActionWidget(Skills.Innovation);
                DrawActionWidget(Skills.Veneration);
                DrawActionWidget(Skills.FinalAppraisal);
                DrawActionWidget(Skills.MuscleMemory);
                DrawActionWidget(Skills.Reflect);
            });

            ImGui.SameLine();
            ActionChild("Repair", 2, () =>
            {
                DrawActionWidget(Skills.Manipulation);
                DrawActionWidget(Skills.MastersMend);
            });

            ImGui.SameLine();
            ActionChild("Other", 5, () =>
            {
                DrawActionWidget(Skills.Observe);
                DrawActionWidget(Skills.HeartAndSoul);
                DrawActionWidget(Skills.CarefulObservation);
                DrawActionWidget(Skills.DelicateSynthesis);
                DrawActionWidget(Skills.TricksOfTrade);
            });
        }

        private static void DrawActionWidget(Skills action)
        {
            var icon = P.Icons.LoadIcon(action.IconOfAction(Job.CRP + SelectedRecipe.CraftType.Row));
            ImGui.Image(icon.ImGuiHandle, new Vector2(widgetSize));

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGuiEx.Text($"{action.NameOfAction()} - {action.StandardCPCost()} CP");
                ImGuiEx.Text($"{action.GetSkillDescription()}");
                ImGui.EndTooltip();
            }

            if (ImGui.IsItemClicked())
            {
                if (_simCurSteps.Count == 0)
                {
                    _selectedCraft = Crafting.BuildCraftStateForRecipe(SimStats, Job.CRP + SelectedRecipe.CraftType.Row, SelectedRecipe);
                    var initial = Simulator.CreateInitial(_selectedCraft, startingQuality);
                    _simCurSteps.Add((initial, ""));
                    var step = Simulator.Execute(_selectedCraft, initial, action, 0, 1);
                    if (step.Item1 == Simulator.ExecuteResult.CantUse)
                    {
                        Notify.Error($"Cannot use {action.NameOfAction()}.");
                    }
                    if (step.Item1 == Simulator.ExecuteResult.Failed)
                    {
                        Notify.Error($"{action.NameOfAction()} has failed");
                    }
                    if (step.Item1 == Simulator.ExecuteResult.Succeeded)
                    {
                        SimActionIDs.Add(action);
                        _simCurSteps.Add((step.Item2, ""));
                    }
                }
                else
                {
                    var step = Simulator.Execute(_selectedCraft, _simCurSteps.Last().step, action, 0, 1);
                    if (step.Item1 == Simulator.ExecuteResult.CantUse)
                    {
                        Notify.Error($"Cannot use {action.NameOfAction()}.");
                    }
                    if (step.Item1 == Simulator.ExecuteResult.Failed)
                    {
                        Notify.Error($"{action.NameOfAction()} has failed");
                    }
                    if (step.Item1 == Simulator.ExecuteResult.Succeeded)
                    {
                        SimActionIDs.Add(action);
                        _simCurSteps.Add((step.Item2, ""));
                    }
                }
            }

            ImGui.SameLine();
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
                if (ImGui.Button($"Run Simulated Solver"))
                {
                    _selectedCraft = Crafting.BuildCraftStateForRecipe(SimStats, Job.CRP + SelectedRecipe.CraftType.Row, SelectedRecipe);
                    InitDefaultTransitionProbabilities(_selectedCraft, SelectedRecipe);
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

                ImGui.SameLine();
                ImGui.Checkbox($"Assume Normal Condition only", ref assumeNormalStatus);

                if (_simCurSolver != null)
                {
                    ImGui.Columns(Math.Min(16, _simCurSteps.Count), null, false);
                    var job = (Job)SimGS?.ClassJob;
                    for (int i = 0; i < _simCurSteps.Count; i++)
                    {
                        if (i + 1 < _simCurSteps.Count)
                        {
                            var currentAction = _simCurSteps[i + 1].step.PrevComboAction;
                            ImGui.Image(P.Icons.LoadIcon(currentAction.IconOfAction(job)).ImGuiHandle, new Vector2(widgetSize));
                            var step = _simCurSteps[i + 1].step;
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGuiEx.Text($"{step.Index - 1}. {currentAction.NameOfAction()}");
                                ImGuiEx.Text($"P: {step.Progress} / {_selectedCraft.CraftProgress} ({Math.Round((float)step.Progress / _selectedCraft.CraftProgress * 100, 0)}%)");
                                ImGuiEx.Text($"Q: {step.Quality} / {_selectedCraft.CraftQualityMax} ({Math.Round((float)step.Quality / _selectedCraft.CraftQualityMax * 100, 0)}%)");
                                ImGuiEx.Text($"D: {step.Durability} / {_selectedCraft.CraftDurability} ({Math.Round((float)step.Durability / _selectedCraft.CraftDurability * 100, 0)}%)");
                                ImGuiEx.Text($"CP: {step.RemainingCP} / {_selectedCraft.StatCP} ({Math.Round((float)step.RemainingCP / _selectedCraft.StatCP * 100, 0)}%)");
                                ImGuiEx.Text($"Condition: {_simCurSteps[i].step.Condition} -> {step.Condition}");
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
                    ImGuiEx.TextCentered($"Quality: {_simCurSteps.Last().step.Quality} / {_selectedCraft.CraftQualityMax} ({Calculations.GetHQChance((double)_simCurSteps.Last().step.Quality / _selectedCraft.CraftQualityMax * 100)}% HQ Chance)");
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

        private static void InitDefaultTransitionProbabilities(CraftState craft, Recipe recipe)
        {
            if (recipe.IsExpert)
            {
                // TODO: this is all very unconfirmed, we really need a process to gather this data
                var potentialConditions = recipe.RecipeLevelTable.Value?.ConditionsFlag ?? 0;
                var manyConditions = (potentialConditions & 0x1F0) == 0x1F0; // it seems that when all conditions are available, each one has slightly lower probability?
                var haveGoodOmen = (potentialConditions & (1 << (int)Condition.GoodOmen)) != 0; // it seems that when good omen is possible, straight good is quite a bit rarer
                craft.CraftConditionProbabilities = new float[(int)Condition.Unknown];
                craft.CraftConditionProbabilities[(int)Condition.Good] = haveGoodOmen ? 0.04f : 0.12f;
                craft.CraftConditionProbabilities[(int)Condition.Centered] = manyConditions ? 0.12f : 0.15f;
                craft.CraftConditionProbabilities[(int)Condition.Sturdy] = manyConditions ? 0.12f : 0.15f;
                craft.CraftConditionProbabilities[(int)Condition.Pliant] = manyConditions ? 0.10f : 0.12f;
                craft.CraftConditionProbabilities[(int)Condition.Malleable] = manyConditions ? 0.10f : 0.12f;
                craft.CraftConditionProbabilities[(int)Condition.Primed] = manyConditions ? 0.12f : 0.15f;
                craft.CraftConditionProbabilities[(int)Condition.GoodOmen] = 0.12f;
                for (Condition i = Condition.Good; i < Condition.Unknown; ++i)
                    if ((potentialConditions & (1 << (int)i)) == 0)
                        craft.CraftConditionProbabilities[(int)i] = 0;
            }
            else
            {
                if (!assumeNormalStatus)
                    craft.CraftConditionProbabilities = CraftState.NormalCraftConditionProbabilities(craft.StatLevel);
            }
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
                ImGuiEx.Text($"Please add a gearset for {LuminaSheets.ClassJobSheet[SelectedRecipe.CraftType.Row + 8].Abbreviation}");
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

                ImGuiEx.ImGuiLineCentered("StartingQuality", () =>
                {
                    ImGuiEx.Text($"Starting Quality: {startingQuality} / {max} ({hqChance}% HQ chance, {percentage.ToString("N0")}% quality)");
                });
                ImGuiEx.ImGuiLineCentered("ExpertInfo", () =>
                {
                    ImGuiEx.Text($"{(SelectedRecipe.IsExpert ? "Expert Recipe" : SelectedRecipe.SecretRecipeBook.Row > 0 ? "Master Recipe" : "Normal Recipe")}");
                });

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

            ImGuiEx.Text($"Select Recipe");
            ImGui.SameLine(120f.Scale());
            ImGuiEx.SetNextItemFullWidth();
            if (ImGui.BeginCombo("###SimulatorRecipeSelect", preview))
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
                            _simCurSteps.Clear();
                            SimActionIDs.Clear();
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
