using Artisan.Autocraft;
using Artisan.CraftingLogic;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI.ImGUI;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.Sheets;
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
        public static Recipe? SelectedRecipe;
        internal static string Search = string.Empty;
        private static CraftState? _selectedCraft;
        private static string macroName = string.Empty;

        // fields for simulator
        private static Random _simRngForSeeds = new();
        private static int _simCurSeed;
        private static Random _simRngForSim = new();
        public static SolverRef? _selectedSolver;
        private static Solver? _simCurSolver;
        private static int startingQuality = 0;
        private static List<(StepState step, string comment)> _simCurSteps = new();
        private static Solver.Recommendation _simNextRec;
        public static GearsetEntry? SimGS;
        private static bool CustomStatMode = false;
        private static int gsLevel = 1, gsCraftsmanship = 1, gsControl = 1, gsCP = 1;
        private static bool gsSplend, gsSpecialist, gsManip;
        public static string SimGSName
        {
            get
            {
                if (SimGS is null)
                    return "";

                unsafe
                {
                    fixed (GearsetEntry?* gs = &SimGS)
                    {
                        var val = gs->Value;
                        string name = val.NameString;
                        bool materiaDiff = gs->Value.Items.ToArray().Any(x => x.Flags.HasFlag(GearsetItemFlag.MateriaDiffers));

                        return $"{name} (ilvl {val.ItemLevel}){(materiaDiff ? " Warning: Detected Materia difference. Please update gearset" : "")}";
                    }
                }
            }
        }

        private static List<Skills> SimActionIDs = new();
        public static ConsumableChoice? SimFood;
        public static ConsumableChoice? SimMedicine;
        public static CharacterStats SimStats;
        private static bool assumeNormalStatus;

        // data and other imgui things
        private static Dictionary<uint, List<IngredientLayouts>> ingredientLayouts = new();
        private static float layoutWidth = 0;
        private static float widgetSize => P.Config.SimulatorActionSize;
        private static bool inManualMode = false;
        private static bool hoverMode = false;
        private static bool hoverStepAdded = false;
        private static string simGSName;

        private class IngredientLayouts
        {
            public int Idx;
            public int ID;
            public int NQ;
            public int HQ;
        }

        public class ConsumableChoice
        {
            public uint Id;
            public ConsumableStats Stats;
            public bool ConsumableHQ;
            public string ConsumableString => string.Join(", ", Stats.Stats.Where(x => x.Param != 0).Select(x => $"{Svc.Data.Excel.GetSheet<BaseParam>().GetRow((uint)x.Param).Name} +{x.Percent}% - max {x.Max}"));
        }

        public static void Draw()
        {
            if (ImGui.BeginTabBar("Simulator Select"))
            {
                if (ImGui.BeginTabItem("GUI Sim"))
                {
                    DrawGUISim();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Mass Sim Mode"))
                {
                    SimulatorUIVeynVersion.Draw();
                    ImGui.EndTabItem();
                }
            }


        }

        private static void DrawGUISim()
        {
            DrawIntro();
            ImGui.Separator();
            DrawRecipeSelector();

            if (SelectedRecipe != null)
            {
                DrawIngredientLayout();

                DrawRecipeInfo();

                DrawGearSetDropdown();

                if (!CustomStatMode)
                DrawConsumablesDropdown();

                DrawStatInfo();

                if (ImGui.BeginTabBar("ModeSelection"))
                {
                    if (ImGui.BeginTabItem("Preconfigured Mode"))
                    {
                        inManualMode = false;
                        DrawPreconfiguredMode();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.IsItemClicked())
                    {
                        ResetSim();
                    }

                    if (ImGui.BeginTabItem("Manual Mode"))
                    {
                        inManualMode = true;
                        DrawSolverMode();
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }
            }
        }

        private static void DrawIntro()
        {
            ImGuiEx.TextWrapped($"In this simulator, you can test out different solvers against recipes and analyze how well they perform. You can set your HQ ingredient layouts, set consumables and even which gearset to use. The simulator can be configured to randomize conditions or use \"Normal\" condition only, so actual execution mileage may vary.");
        }

        private static void DrawSolverMode()
        {
            if (!CustomStatMode)
            {
                if (SimGS != null)
                {
                    _selectedCraft ??= Crafting.BuildCraftStateForRecipe(SimStats, Job.CRP + SelectedRecipe.Value.CraftType.RowId, SelectedRecipe.Value);
                    if (_simCurSteps.Count == 0)
                    {
                        var initial = Simulator.CreateInitial(_selectedCraft, startingQuality);
                        _simCurSteps.Add((initial, ""));
                    }

                    ImGui.BeginChild("###ManualSolver", new Vector2(0), false, ImGuiWindowFlags.HorizontalScrollbar);
                    DrawActionWidgets();
                    ImGui.Separator();
                    DrawSimulation();
                    if (hoverStepAdded)
                    {
                        _simCurSteps.RemoveAt(_simCurSteps.Count - 1);
                        SimActionIDs.RemoveAt(SimActionIDs.Count - 1);
                    }
                    hoverStepAdded = false;
                    hoverMode = false;
                    ImGui.EndChild();
                }
                else
                {
                    ImGui.Text($"Please have a gearset selected from above to use this feature.");
                }
            }
            else
            {
                try
                {
                    _selectedCraft ??= Crafting.BuildCraftStateForRecipe(SimStats, Job.CRP + SelectedRecipe.Value.CraftType.RowId, SelectedRecipe.Value);
                    if (_simCurSteps.Count == 0)
                    {
                        var initial = Simulator.CreateInitial(_selectedCraft, startingQuality);
                        _simCurSteps.Add((initial, ""));
                    }

                    ImGui.BeginChild("###ManualSolver", new Vector2(0), false, ImGuiWindowFlags.HorizontalScrollbar);
                    DrawActionWidgets();
                    ImGui.Separator();
                    DrawSimulation();
                    if (hoverStepAdded)
                    {
                        _simCurSteps.RemoveAt(_simCurSteps.Count - 1);
                        SimActionIDs.RemoveAt(SimActionIDs.Count - 1);
                    }
                    hoverStepAdded = false;
                    hoverMode = false;
                    ImGui.EndChild();
                }
                catch (Exception ex) 
                {
                    ex.Log();
                }
            }
        }

        private static void DrawExports()
        {
            if (SimActionIDs.Count > 0 && (_simCurSolver is not MacroSolver || inManualMode) && !hoverMode)
            {
                ImGui.SameLine();
                ImGuiEx.Text($"Macro Name");
                ImGui.SameLine();
                ImGuiEx.SetNextItemFullWidth(-120);
                ImGui.InputText($"###MacroName", ref macroName, 300, ImGuiInputTextFlags.EnterReturnsTrue);
                ImGui.SameLine();
                if (ImGui.Button($"Export As Macro"))
                {
                    if (string.IsNullOrEmpty(macroName))
                    {
                        Notify.Error("Please provide a name for the macro");
                        return;
                    }
                    MacroSolverSettings.Macro newMacro = new();
                    foreach (var step in SimActionIDs)
                    {
                        newMacro.Steps.Add(new MacroSolverSettings.MacroStep() { Action = step });
                    }
                    newMacro.Name = macroName;
                    P.Config.MacroSolverConfig.AddNewMacro(newMacro);

                    var config = P.Config.RecipeConfigs.GetValueOrDefault(SelectedRecipe.Value.RowId) ?? new();
                    config.SolverType = typeof(MacroSolverDefinition).FullName!;
                    config.SolverFlavour = newMacro.ID;
                    P.Config.RecipeConfigs[SelectedRecipe.Value.RowId] = config;
                    P.Config.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGuiEx.Text($"This will also automatically assign the macro to this recipe.");
                    ImGui.EndTooltip();
                }
            }
        }

        private static void DrawSimulation()
        {

            if (ImGui.Button($"Reset"))
            {
                ResetSim();
            }

            DrawExports();


            if (_selectedCraft != null && _simCurSteps != null && _simCurSteps.Count > 0)
            {
                ImGui.Columns(16, null, false);
                var job = Job.CRP + SelectedRecipe.Value.CraftType.RowId;
                for (int i = 0; i < _simCurSteps.Count; i++)
                {
                    if (_simCurSteps.Count == 1)
                    {
                        ImGui.Dummy(new Vector2(widgetSize));
                    }

                    if (i + 1 < _simCurSteps.Count)
                    {
                        bool highlightLast = hoverMode && i + 1 == _simCurSteps.Count - 1;
                        var currentAction = _simCurSteps[i + 1].step.PrevComboAction;
                        var step = _simCurSteps[i + 1].step;
                        var x = ImGui.GetCursorPosX();
                        ImGui.Image(P.Icons.TryLoadIconAsync(currentAction.IconOfAction(job)).Result.ImGuiHandle, new Vector2(widgetSize), new Vector2(), new Vector2(1, 1), highlightLast ? new Vector4(0f, 0.75f, 0.25f, 1f) : new Vector4(1f, 1f, 1f, 1f));
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
                        }
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(x);
                        ImGuiEx.Text(new Vector4(0, 0, 0, 1), $"{i + 1}.");
                        ImGui.SameLine();
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 1f);
                        ImGui.SetCursorPosX(x + 2f);
                        ImGuiEx.Text(new Vector4(1, 1, 1, 1), $"{i + 1}.");
                        ImGui.NextColumn();
                    }
                }
                ImGui.Columns(1);

                DrawSimResult();
            }

        }

        public static void ResetSim()
        {
            _selectedCraft = Crafting.BuildCraftStateForRecipe(SimStats, Job.CRP + SelectedRecipe.Value.CraftType.RowId, SelectedRecipe.Value);
            SimActionIDs.Clear();
            _simCurSteps.Clear();
            var initial = Simulator.CreateInitial(_selectedCraft, startingQuality);
            _simCurSteps.Add((initial, ""));
            if (_simCurSolver != null)
                _simNextRec = _simCurSolver.Solve(_selectedCraft, initial);
        }

        private static void ResolveSteps()
        {
            if (!inManualMode) return;
            _simCurSteps.Clear();
            _selectedCraft = Crafting.BuildCraftStateForRecipe(SimStats, Job.CRP + SelectedRecipe.Value.CraftType.RowId, SelectedRecipe.Value);
            var initial = Simulator.CreateInitial(_selectedCraft, startingQuality);
            _simCurSteps.Add((initial, ""));
            for (int i = 0; i < SimActionIDs.Count; i++)
            {
                var step = Simulator.Execute(_selectedCraft, _simCurSteps.Last().step, SimActionIDs[i], 0, 1);
                if (step.Item1 == Simulator.ExecuteResult.Succeeded)
                    _simCurSteps.Add((step.Item2, ""));
            }
        }

        private unsafe static void DrawProgress(int a, int b)
        {
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(36f / 255f, 96f / 255f, 144f / 255f, 255f / 255f));
            ImGui.ProgressBar((float)a / b, new(0), $"{a * 100.0f / b:f2}% ({a}/{b})");
            ImGui.PopStyleColor();
        }

        private static void ActionChild(string label, int itemCount, System.Action func)
        {
            Vector2 childSize = new Vector2((widgetSize + ImGui.GetStyle().ItemSpacing.X) * itemCount + ImGui.GetStyle().WindowPadding.X, (ImGui.GetTextLineHeightWithSpacing() + (widgetSize + ImGui.GetStyle().WindowPadding.Y) + 12f));
            ImGui.BeginChild(label, childSize, true);
            ImGuiEx.ImGuiLineCentered($"{label}", () => ImGuiEx.TextUnderlined($"{label}"));
            func();
            ImGui.EndChild();
        }
        private static void DrawActionWidgets()
        {
            ActionChild("Progress Actions", 6, () =>
            {
                DrawActionWidget(Skills.BasicSynthesis);
                DrawActionWidget(Skills.CarefulSynthesis);
                DrawActionWidget(Skills.PrudentSynthesis);
                DrawActionWidget(Skills.RapidSynthesis);
                DrawActionWidget(Skills.Groundwork);
                DrawActionWidget(Skills.IntensiveSynthesis);
            });

            ImGui.SameLine();
            ActionChild("Quality Actions", 12, () =>
            {
                DrawActionWidget(Skills.BasicTouch);
                DrawActionWidget(Skills.StandardTouch);
                DrawActionWidget(Skills.AdvancedTouch);
                DrawActionWidget(Skills.HastyTouch);
                DrawActionWidget(Skills.ByregotsBlessing);
                DrawActionWidget(Skills.PreciseTouch);
                DrawActionWidget(Skills.PrudentTouch);
                DrawActionWidget(Skills.TrainedEye);
                DrawActionWidget(Skills.PreparatoryTouch);
                DrawActionWidget(Skills.TrainedFinesse);
                DrawActionWidget(Skills.RefinedTouch);
                DrawActionWidget(Skills.DaringTouch);
            });

            ActionChild("Buff Actions", 9, () =>
            {
                DrawActionWidget(Skills.WasteNot);
                DrawActionWidget(Skills.WasteNot2);
                DrawActionWidget(Skills.GreatStrides);
                DrawActionWidget(Skills.Innovation);
                DrawActionWidget(Skills.Veneration);
                DrawActionWidget(Skills.FinalAppraisal);
                DrawActionWidget(Skills.MuscleMemory);
                DrawActionWidget(Skills.Reflect);
                DrawActionWidget(Skills.QuickInnovation);
            });

            ImGui.SameLine();
            ActionChild("Repair", 3, () =>
            {
                DrawActionWidget(Skills.Manipulation);
                DrawActionWidget(Skills.MastersMend);
                DrawActionWidget(Skills.ImmaculateMend);
            });

            ImGui.SameLine();
            ActionChild("Other", 6, () =>
            {
                DrawActionWidget(Skills.Observe);
                DrawActionWidget(Skills.HeartAndSoul);
                DrawActionWidget(Skills.CarefulObservation);
                DrawActionWidget(Skills.DelicateSynthesis);
                DrawActionWidget(Skills.TricksOfTrade);
                DrawActionWidget(Skills.TrainedPerfection);
            });

        }

        private static void DrawActionWidget(Skills action)
        {
            var icon = P.Icons.TryLoadIconAsync(action.IconOfAction(Job.CRP + SelectedRecipe.Value.CraftType.RowId)).Result;
            ImGui.Image(icon.ImGuiHandle, new Vector2(widgetSize));

            var nextstep = Simulator.Execute(_selectedCraft, _simCurSteps.Last().step, action, 0, 1);

            if (ImGui.IsItemHovered())
            {
                if (!P.Config.DisableSimulatorActionTooltips)
                {
                    ImGui.BeginTooltip();
                    ImGuiEx.Text($"{action.NameOfAction()} - {action.StandardCPCost()} CP");
                    ImGuiEx.Text($"{action.GetSkillDescription()}");
                    ImGui.EndTooltip();
                }

                if (P.Config.SimulatorHoverMode)
                {
                    hoverMode = true;
                    if (!hoverStepAdded)
                    {
                        if (_simCurSteps.Count == 0)
                            ResetSim();

                        if (nextstep.Item1 == Simulator.ExecuteResult.Succeeded)
                        {
                            _simCurSteps.Add((nextstep.Item2, ""));
                            hoverStepAdded = true;
                            SimActionIDs.Add(action);
                        }
                        else
                        {
                            hoverMode = false;
                        }
                    }
                }
            }

            if (ImGui.IsItemClicked())
            {
                if (P.Config.SimulatorHoverMode)
                {
                    if (nextstep.Item1 == Simulator.ExecuteResult.CantUse)
                    {
                        Notify.Error($"Cannot use {action.NameOfAction()}.");
                    }
                    if (nextstep.Item1 == Simulator.ExecuteResult.Failed)
                    {
                        Notify.Error($"{action.NameOfAction()} has failed");
                    }
                    hoverStepAdded = false;
                }
                else
                {
                    if (_simCurSteps.Count == 0)
                    {
                        _selectedCraft = Crafting.BuildCraftStateForRecipe(SimStats, Job.CRP + SelectedRecipe.Value.CraftType.RowId, SelectedRecipe.Value);
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
            }

            ImGui.SameLine();
        }

        private static void DrawPreconfiguredMode()
        {
            if (SimGS is null && !CustomStatMode)
            {
                ImGui.Text($"Please have a gearset selected from above to use this feature.");
                return;
            }
            DrawSolverCombo();
            DrawSolverActions();
        }

        private static void DrawSolverActions()
        {
            if (_selectedSolver != null && (SimGS != null || CustomStatMode))
            {
                ImGuiEx.SetNextItemFullWidth();
                if (ImGui.Button($"Run Simulated Solver"))
                {
                    _simCurSolver = _selectedSolver?.Clone();
                    ResetSim();
                    InitDefaultTransitionProbabilities(_selectedCraft, SelectedRecipe.Value);
                    while (SolveNextSimulator(_selectedCraft)) ;
                }
                ImGui.SameLine();
                if (ImGui.Checkbox($"Assume Normal Condition only", ref assumeNormalStatus))
                {
                    _selectedCraft = Crafting.BuildCraftStateForRecipe(SimStats, Job.CRP + SelectedRecipe.Value.CraftType.RowId, SelectedRecipe.Value);
                    _simCurSteps.Clear();
                }

                if (assumeNormalStatus)
                    DrawExports();

                if (_simCurSolver != null && _simCurSteps.Count > 0)
                {
                    ImGui.Columns(Math.Min(16, _simCurSteps.Count), null, false);
                    var job = Job.CRP + SelectedRecipe.Value.CraftType.RowId;
                    for (int i = 0; i < _simCurSteps.Count; i++)
                    {
                        if (i + 1 < _simCurSteps.Count)
                        {
                            var currentAction = _simCurSteps[i + 1].step.PrevComboAction;
                            ImGui.Image(P.Icons.TryLoadIconAsync(currentAction.IconOfAction(job)).Result.ImGuiHandle, new Vector2(widgetSize));
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

                    DrawSimResult();
                }
            }
        }

        private static void DrawSimResult()
        {
            if (_simCurSteps.Count == 1) return;

            var status = Simulator.Status(_selectedCraft, _simCurSteps.Last().step);
            Vector4 successColor = status switch
            {
                Simulator.CraftStatus.InProgress => ImGuiColors.DalamudWhite,
                Simulator.CraftStatus.FailedDurability => ImGuiColors.DalamudRed,
                Simulator.CraftStatus.FailedMinQuality => ImGuiColors.DalamudRed,
                Simulator.CraftStatus.SucceededQ1 => ImGuiColors.DalamudOrange,
                Simulator.CraftStatus.SucceededQ2 => ImGuiColors.DalamudOrange,
                Simulator.CraftStatus.SucceededQ3 => ImGuiColors.ParsedGreen,
                Simulator.CraftStatus.SucceededMaxQuality => ImGuiColors.ParsedGreen,
                Simulator.CraftStatus.SucceededSomeQuality => ImGuiColors.DalamudOrange,
                Simulator.CraftStatus.SucceededNoQualityReq => ImGuiColors.ParsedGreen,
                _ => throw new NotImplementedException(),
            };

            float qualityPercent = _simCurSteps.Last().step.Quality / _selectedCraft.CraftQualityMax;
            float progressPercent = _simCurSteps.Last().step.Progress / _selectedCraft.CraftProgress;
            float CPPercent = _simCurSteps.Last().step.RemainingCP / _selectedCraft.StatCP;

            ImGui.PushStyleColor(ImGuiCol.Text, successColor);
            ImGuiEx.LineCentered($"SimResults", () => ImGuiEx.TextUnderlined($"Simulator Result - {status.ToOutputString()}"));
            ImGui.Columns(4, null, false);
            ImGuiEx.TextCentered($"Quality (IQ: {_simCurSteps.Last().step.IQStacks})");
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
            ImGui.NextColumn();
            ImGuiEx.TextCentered($"{Skills.MuscleMemory.NameOfAction()}: {_simCurSteps.Last().step.MuscleMemoryLeft}");
            ImGuiEx.TextCentered($"{Skills.FinalAppraisal.NameOfAction()}: {_simCurSteps.Last().step.FinalAppraisalLeft}");
            ImGui.NextColumn();
            ImGuiEx.TextCentered($"{Skills.WasteNot.NameOfAction()}: {_simCurSteps.Last().step.WasteNotLeft}");
            ImGuiEx.TextCentered($"{Skills.Manipulation.NameOfAction()}: {_simCurSteps.Last().step.ManipulationLeft}");
            ImGui.NextColumn();
            ImGuiEx.TextCentered($"{Skills.Innovation.NameOfAction()}: {_simCurSteps.Last().step.InnovationLeft}");
            ImGuiEx.TextCentered($"{Skills.Veneration.NameOfAction()}: {_simCurSteps.Last().step.VenerationLeft}");
            ImGui.NextColumn();
            ImGuiEx.TextCentered($"{Skills.GreatStrides.NameOfAction()}: {_simCurSteps.Last().step.GreatStridesLeft}");
            ImGuiEx.TextCentered($"{Skills.CarefulObservation.NameOfAction()}: {_simCurSteps.Last().step.CarefulObservationLeft}");
            ImGui.Columns(1);
            ImGui.PopStyleColor();
        }

        private static bool SolveNextSimulator(CraftState craft)
        {
            if (_simCurSolver == null || _simCurSteps.Count == 0 || _simNextRec.Action == Skills.None)
                return false;

            var step = _simCurSteps.Last().step;
            var (res, next) = Simulator.Execute(craft, step, _simNextRec.Action, _simRngForSim.NextSingle(), _simRngForSim.NextSingle());
            if (res == Simulator.ExecuteResult.CantUse)
                return false;
            SimActionIDs.Add(_simNextRec.Action);
            _simCurSteps[_simCurSteps.Count - 1] = (step, _simNextRec.Comment);
            _simCurSteps.Add((next, ""));
            _simNextRec = _simCurSolver.Solve(craft, next);

            return true;
        }

        private static void InitDefaultTransitionProbabilities(CraftState craft, Recipe recipe)
        {
            if (assumeNormalStatus)
                return;

            if (recipe.IsExpert) //Todo update with Lumina fix
            {
                // TODO: this is all very unconfirmed, we really need a process to gather this data
                var potentialConditions = recipe.RecipeLevelTable.Value.ConditionsFlag;
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

            _selectedCraft = Crafting.BuildCraftStateForRecipe(SimStats, Job.CRP + SelectedRecipe.Value.CraftType.RowId, SelectedRecipe.Value);
            foreach (var opt in CraftingProcessor.GetAvailableSolversForRecipe(_selectedCraft, false))
            {
                if (opt == default) continue;
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
            using var foodCombo = ImRaii.Combo("###SimFood", SimFood is null ? "" : $"{(SimFood.ConsumableHQ ? " " : "")} {LuminaSheets.ItemSheet[SimFood.Id].Name.ToString()} ({SimFood.ConsumableString})");
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
            using var medicineCombo = ImRaii.Combo("###SimMedicine", SimMedicine is null ? "" : $"{(SimMedicine.ConsumableHQ ? " " : "")} {LuminaSheets.ItemSheet[SimMedicine.Id].Name.ToString()} ({SimMedicine.ConsumableString})");
            if (!medicineCombo)
                return;

            if (ImGui.Selectable($""))
                SimMedicine = null;

            foreach (var medicine in ConsumableChecker.GetPots().OrderBy(x => x.Id))
            {
                var consumable = ConsumableChecker.GetItemConsumableProperties(LuminaSheets.ItemSheet[medicine.Id], false);
                if (consumable.Value.Params.Any(x => x.BaseParam.RowId is 69 or 68))
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
                ImGuiEx.LineCentered("SimulatorStats", () => ImGuiEx.TextUnderlined("Crafter Stats"));
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
                ImGui.TextWrapped($"Splendorous/Cosmic Tool: {gsStats.SplendorCosmic}");
                ImGui.NextColumn();
                ImGui.TextWrapped($"Specialist: {gsStats.Specialist}");
                ImGui.NextColumn();
                ImGui.TextWrapped($"Manipulation Unlocked: {gsStats.Manipulation}");
                ImGui.Columns(1);

                SimStats = new CharacterStats()
                {
                    Craftsmanship = gsStats.Craftsmanship + craftsmanshipBoost,
                    Control = gsStats.Control + controlBoost,
                    CP = gsStats.CP + cpBoost,
                    Specialist = gsStats.Specialist,
                    SplendorCosmic = gsStats.SplendorCosmic,
                    Manipulation = gsStats.Manipulation
                };

                ResolveSteps();
            }
        }

        private static unsafe void DrawGearSetDropdown()
        {
            if (!CustomStatMode)
            {
                if (ImGui.Button($"Switch to Custom Stat Mode", new (ImGui.GetContentRegionAvail().X, 0)))
                    CustomStatMode = true;
            }
            else
            {
                if (ImGui.Button($"Switch to Gearset Mode", new(ImGui.GetContentRegionAvail().X, 0)))
                    CustomStatMode = false;
            }

            if (!CustomStatMode)
            {
                var validGS = RaptureGearsetModule.Instance()->Entries.ToArray().Count(x => RaptureGearsetModule.Instance()->IsValidGearset(x.Id) && x.ClassJob == SelectedRecipe?.CraftType.RowId + 8);

                if (validGS == 0)
                {
                    ImGuiEx.Text($"Please add a gearset for {LuminaSheets.ClassJobSheet[SelectedRecipe.Value.CraftType.RowId + 8].Abbreviation}");
                    SimGS = null;
                    return;
                }
                if (validGS == 1)
                {
                    var gs = RaptureGearsetModule.Instance()->Entries.ToArray().First(x => RaptureGearsetModule.Instance()->IsValidGearset(x.Id) && x.ClassJob == SelectedRecipe?.CraftType.RowId + 8);
                    SimGS = gs;
                    string name = gs.NameString;
                    bool materiaDiff = gs.Items.ToArray().Any(x => x.Flags.HasFlag(GearsetItemFlag.MateriaDiffers));
                    ImGuiEx.Text($"Gearset");
                    ImGui.SameLine(120f);
                    ImGuiEx.SetNextItemFullWidth();
                    ImGuiEx.Text($"{name} (ilvl {SimGS?.ItemLevel}){(materiaDiff ? " Warning: Detected Materia difference. Please update gearset" : "")}");
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
                    SimGS = null;
                }

                foreach (var gs in RaptureGearsetModule.Instance()->Entries)
                {
                    if (!RaptureGearsetModule.Instance()->IsValidGearset(gs.Id)) continue;
                    if (gs.ClassJob != SelectedRecipe?.CraftType.RowId + 8)
                        continue;

                    string name = gs.NameString;
                    bool materiaDiff = gs.Items.ToArray().Any(x => x.Flags.HasFlag(GearsetItemFlag.MateriaDiffers));
                    var selected = ImGui.Selectable($"{name} (ilvl {gs.ItemLevel}){(materiaDiff ? " Warning: Detected Materia difference. Please update gearset" : "")}##GS{gs.Id}");

                    if (selected)
                    {
                        SimGS = gs;
                    }
                }
            }
            else
            {
                SimGS = null;

                ImGui.Columns(4, null, false);
                ImGUIMethods.InputIntBound($"Level:", ref gsLevel, 1, 100, true);
                ImGui.NextColumn();
                ImGUIMethods.InputIntBound($"Craftsmanship:", ref gsCraftsmanship, 1, 99999, true);
                ImGui.NextColumn();
                ImGUIMethods.InputIntBound($"Control:", ref gsControl, 1, 99999, true);
                ImGui.NextColumn();
                ImGUIMethods.InputIntBound($"CP:", ref gsCP, 1, 99999, true);
                ImGui.NextColumn();
                ImGui.Columns(3, null, false);
                ImGUIMethods.FlippedCheckbox($"Splendorous/Cosmic:", ref gsSplend);
                ImGui.NextColumn();
                ImGUIMethods.FlippedCheckbox($"Specialist:", ref gsSpecialist);
                ImGui.NextColumn();
                ImGUIMethods.FlippedCheckbox($"Manipulation Unlocked:", ref gsManip);
                ImGui.Columns(1);

                SimStats = new CharacterStats()
                {
                    Craftsmanship = gsCraftsmanship,
                    Control = gsControl,
                    CP = gsCP,
                    Specialist = gsSpecialist,
                    SplendorCosmic = gsSplend,
                    Manipulation = gsManip,
                    Level = gsLevel,    
                };

                ResolveSteps();
            }
        }

        private static void DrawRecipeInfo()
        {
            if (ingredientLayouts.TryGetValue(SelectedRecipe.Value.RowId, out var layouts))
            {
                startingQuality = Calculations.GetStartingQuality(SelectedRecipe.Value, layouts.OrderBy(x => x.Idx).Select(x => x.HQ).ToArray());
                var max = Calculations.RecipeMaxQuality(SelectedRecipe.Value);
                var percentage = Math.Clamp((double)startingQuality / max * 100, 0, 100);
                var hqChance = Calculations.GetHQChance(percentage);

                ImGuiEx.ImGuiLineCentered("StartingQuality", () =>
                {
                    ImGuiEx.Text($"Starting Quality: {startingQuality} / {max} ({hqChance}% HQ chance, {percentage.ToString("N0")}% quality)");
                });
                ImGuiEx.ImGuiLineCentered("ExpertInfo", () =>
                {
                    ImGuiEx.Text($"{(SelectedRecipe.Value.IsExpert ? "Expert Recipe" : SelectedRecipe.Value.SecretRecipeBook.RowId > 0 ? "Master Recipe" : "Normal Recipe")}");
                });

            }

        }

        private static void DrawIngredientLayout()
        {
            bool hasHQ = false;
            foreach (var i in SelectedRecipe.Value.Ingredients().Where(x => x.Amount > 0))
            {
                if (LuminaSheets.ItemSheet[i.Item.RowId].CanBeHq)
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
            foreach (var i in SelectedRecipe.Value.Ingredients().Where(x => x.Amount > 0))
            {
                if (ingredientLayouts.TryGetValue(SelectedRecipe.Value.RowId, out var layouts))
                {
                    if (layouts.FindFirst(x => x.ID == i.Item.RowId, out var layout))
                    {
                        ImGui.TableNextRow();
                        var item = LuminaSheets.ItemSheet[i.Item.RowId];
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

                                if (layout.NQ > i.Amount)
                                    layout.NQ = i.Amount;

                                layout.HQ = i.Amount - layout.NQ;
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

                                if (layout.HQ > i.Amount)
                                    layout.HQ = i.Amount;

                                layout.NQ = Math.Min(i.Amount - layout.HQ, i.Amount);
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
                        layout.ID = (int)i.Item.RowId;
                        layout.NQ = i.Amount;
                        layout.HQ = 0;

                        layouts.Add(layout);
                    }



                }
                else
                {
                    ingredientLayouts.TryAdd(SelectedRecipe.Value.RowId, new List<IngredientLayouts>());
                }
                idx++;
            }
        }

        private static void DrawRecipeSelector()
        {
            var preview = SelectedRecipe is null
                                      ? string.Empty
                                      : $"{SelectedRecipe?.ItemResult.Value.Name.ToDalamudString().ToString()} ({LuminaSheets.ClassJobSheet[SelectedRecipe.Value.CraftType.RowId + 8].Abbreviation.ToString()})";

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


                    foreach (var recipe in LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.Name.ToDalamudString().ToString().Contains(Search, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        ImGui.PushID($"###simRecipe{recipe.RowId}");
                        var selected = ImGui.Selectable($"{recipe.ItemResult.Value.Name.ToDalamudString().ToString()} ({LuminaSheets.ClassJobSheet[recipe.CraftType.RowId + 8].Abbreviation.ToString()} {recipe.RecipeLevelTable.Value.ClassJobLevel})", recipe.RowId == SelectedRecipe?.RowId);

                        if (selected)
                        {
                            SelectedRecipe = recipe;
                            if (SimGS is not null && recipe.CraftType.RowId + 8 != SimGS.Value.ClassJob)
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
