using Artisan.CraftingLogic;
using Artisan.GameInterop;
using Artisan.RawInformation.Character;
using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ImGuiNET;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Condition = Artisan.CraftingLogic.CraftData.Condition;

namespace Artisan.UI;

internal static class SimulatorUIVeynVersion
{
    class Statistics
    {
        public int NumExperiments;
        public int[] NumOutcomes = new int[(int)Simulator.CraftStatus.Count];
        public List<float> QualityPercents = new();
    }

    private static Recipe? _selectedRecipe;
    private static CraftState? _selectedCraft;
    private static SolverRef _selectedSolver;
    private static float _startingQualityPct;

    private static CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();
    private static bool taskRunning = false;   

    // fields for stats
    private static int _statsNumIterations = 100000;
    private static int _statsNumTasks = 128;
    private static Statistics? _statsCurrent;
    private static List<Task<Statistics>> _statsInProgress = new();

    // fields for simulator
    private static Random _simRngForSeeds = new();
    private static int _simCurSeed;
    private static Random _simRngForSim = new();
    private static Solver? _simCurSolver;
    private static List<(StepState step, string comment)> _simCurSteps = new();
    private static Solver.Recommendation _simNextRec;

    public static void Draw()
    {
        var curRecipe = GetCurrentRecipe();
        if (curRecipe != null && curRecipe.Value.RowId != _selectedRecipe?.RowId)
            SetSelectedRecipe(curRecipe);

        if (_selectedRecipe == null || _selectedCraft == null)
        {
            ImGui.TextUnformatted($"Please select a recipe to use simulator");
            return;
        }

        DrawRecipeInfo(_selectedRecipe.Value, _selectedCraft);
        DrawStatistics(_selectedCraft);
        DrawSimulator(_selectedCraft);
    }

    private static void DrawRecipeInfo(Recipe r, CraftState craft)
    {
        using var n = ImRaii.TreeNode($"Recipe: #{r.RowId} {r.CraftType.RowId + Job.CRP} '{r.ItemResult.Value.Name.ToDalamudString()}', solver: {_selectedSolver.Name}###recipe");
        if (!n)
            return;

        if (ImGui.Button("Refresh stats"))
            SetSelectedRecipe(r);
        ImGui.InputFloat("Starting quality percent", ref _startingQualityPct);
        for (int i = 1; i < craft.CraftConditionProbabilities.Length; ++i)
            ImGui.InputFloat($"Transition probability to {(Condition)i}", ref craft.CraftConditionProbabilities[i]);
    }

    private static void DrawStatistics(CraftState craft)
    {
        using var n = ImRaii.TreeNode("Statistics");
        if (!n)
            return;

        ImGui.InputInt("Num iterations", ref _statsNumIterations);
        ImGui.SameLine();
        if (StatisticsInProgress())
        {
            using var d = ImRaii.Disabled();
            ImGui.Button("Please wait...");
        }
        else if (ImGui.Button("Run!"))
        {
            SetSelectedRecipe(_selectedRecipe);
            _statsCurrent = null;
            var iterationsPerTask = _statsNumIterations / _statsNumTasks;
            var startingQuality = (int)(craft.CraftQualityMax * _startingQualityPct / 100.0);
            for (int i = 0; i < _statsNumTasks - 1; ++i)
                _statsInProgress.Add(Task.Run(() => GatherStats(craft, _selectedSolver, iterationsPerTask, startingQuality)));
            _statsInProgress.Add(Task.Run(() => GatherStats(craft, _selectedSolver, _statsNumIterations - iterationsPerTask * (_statsNumTasks - 1), startingQuality)));
        }

        if (_statsCurrent == null || _statsCurrent.NumExperiments == 0)
            return;

        DrawStatistic("Execution errors", _statsCurrent.NumOutcomes[0]);
        DrawStatistic("Fails (durability)", _statsCurrent.NumOutcomes[1]);
        DrawStatistic("Fails (quality)", _statsCurrent.NumOutcomes[2]);
        if (craft.CraftCollectible)
        {
            DrawStatistic("Success Q1", _statsCurrent.NumOutcomes[3]);
            DrawStatistic("Success Q2", _statsCurrent.NumOutcomes[4]);
            DrawStatistic("Success Q3", _statsCurrent.NumOutcomes[5]);
        }
        DrawStatistic("Success Max Quality", _statsCurrent.NumOutcomes[6]);
        var yieldQ1 = 1;
        var yieldQ2 = yieldQ1 + (craft.CraftQualityMin2 > craft.CraftQualityMin1 ? 1 : 0);
        var yieldQ3 = yieldQ2 + (craft.CraftQualityMin3 > craft.CraftQualityMin2 ? 1 : 0);
        var yield = _statsCurrent.NumOutcomes[3] * yieldQ1 + _statsCurrent.NumOutcomes[4] * yieldQ2 + _statsCurrent.NumOutcomes[5] * yieldQ3;
        if (craft.CraftCollectible)
            ImGui.TextUnformatted($"Average yield: {(double)yield / _statsCurrent.NumExperiments:f3}");
        else
            ImGui.TextUnformatted($"Average quality: {Math.Round(_statsCurrent.QualityPercents.Average(), 0)}%");
    }

    private static void DrawStatistic(string prompt, int count) => ImGui.TextUnformatted($"{prompt}: {count} ({count * 100.0 / _statsCurrent!.NumExperiments:f2}%)");

    private static void DrawSimulator(CraftState craft)
    {
        using var n = ImRaii.TreeNode("Simulator");
        if (!n)
            return;

        DrawSimulatorRestartRow(craft);
        if (_simCurSolver == null || _simCurSteps.Count == 0)
            return;
        if (!taskRunning)
        {
            DrawSimulatorStepRow(craft);
            DrawSimulatorSteps(craft);
        }
    }

    private static void DrawSimulatorRestartRow(CraftState craft)
    {
        if (taskRunning)
        {
            if (ImGui.Button("Stop Running"))
            {
                _cancelTokenSource.Cancel();
                taskRunning = false;
            }
        }
        else
        {
            if (ImGui.Button("Restart!"))
            {
                RestartSimulator(craft, _simRngForSeeds.Next());
            }
            ImGui.SameLine();
            if (ImGui.Button("Restart and solve"))
            {
                RestartSimulator(craft, _simRngForSeeds.Next());
                SolveRestSimulator(craft);
            }
            ImGui.SameLine();
            if (ImGui.Button("Restart and solve until..."))
            {
                _cancelTokenSource = new CancellationTokenSource();
                ImGui.OpenPopup("SolveUntil");
            }
            ImGui.SameLine();
            if (ImGui.Button($"Restart with seed:"))
            {
                RestartSimulator(craft, _simCurSeed);
            }
            ImGui.SameLine();
            ImGui.InputInt("###Seed", ref _simCurSeed);

            using var popup = ImRaii.Popup("SolveUntil");
            if (popup)
            {
                var token = _cancelTokenSource.Token;
                if (ImGui.MenuItem("Solver error"))
                {
                    Task.Run(() => RestartSimulatorUntil(craft, Simulator.CraftStatus.InProgress), token);
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.MenuItem("Failure due to durability running out"))
                {
                    Task.Run(() => RestartSimulatorUntil(craft, Simulator.CraftStatus.FailedDurability), token);
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.MenuItem("Failure due to lack of quality"))
                {
                    Task.Run(() => RestartSimulatorUntil(craft, Simulator.CraftStatus.FailedMinQuality), token);
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.MenuItem("Breakpoint 1 success"))
                {
                    Task.Run(() => RestartSimulatorUntil(craft, Simulator.CraftStatus.SucceededQ1), token);
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.MenuItem("Breakpoint 2 success"))
                {
                    Task.Run(() => RestartSimulatorUntil(craft, Simulator.CraftStatus.SucceededQ2), token);
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.MenuItem("Breakpoint 3 success"))
                {
                    Task.Run(() => RestartSimulatorUntil(craft, Simulator.CraftStatus.SucceededQ3), token);
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.MenuItem("Max quality"))
                {
                    Task.Run(() => RestartSimulatorUntil(craft, Simulator.CraftStatus.SucceededMaxQuality), token);
                    ImGui.CloseCurrentPopup();
                }
                if (ImGui.MenuItem("Success, some quality"))
                {
                    Task.Run(() => RestartSimulatorUntil(craft, Simulator.CraftStatus.SucceededSomeQuality), token);
                    ImGui.CloseCurrentPopup();
                }
            }
        }
    }

    private static void DrawSimulatorStepRow(CraftState craft)
    {
        if (ImGui.Button("Solve next"))
            SolveNextSimulator(craft);
        ImGui.SameLine();
        if (ImGui.Button("Solve all"))
            SolveRestSimulator(craft);
        ImGui.SameLine();
        if (ImGui.Button("Manual..."))
            ImGui.OpenPopup("Manual");
        ImGui.SameLine();

        if (_simCurSteps.Count == 0) return;

        ImGui.TextUnformatted($"Status: {Simulator.Status(craft, _simCurSteps.Last().step)}, Suggestion: {_simNextRec.Action} ({_simNextRec.Comment})");

        using var popup = ImRaii.Popup("Manual");
        if (popup)
        {
            var step = _simCurSteps.Last().step;
            foreach (var opt in Enum.GetValues(typeof(Skills)).Cast<Skills>())
            {
                if (opt == Skills.None)
                    continue;

                if (ImGui.MenuItem($"{opt} ({Simulator.GetCPCost(step, opt)}cp, {Simulator.GetDurabilityCost(step, opt)}dur)", Simulator.CanUseAction(craft, step, opt)))
                {
                    var (res, next) = Simulator.Execute(craft, step, opt, _simRngForSim.NextSingle(), _simRngForSim.NextSingle());
                    if (res != Simulator.ExecuteResult.CantUse)
                    {
                        _simCurSteps[_simCurSteps.Count - 1] = (step, "manual");
                        _simCurSteps.Add((next, ""));
                        _simNextRec = _simCurSolver.Solve(craft, next);
                    }
                }
            }
        }
    }

    private static void DrawSimulatorSteps(CraftState craft)
    {
        if (taskRunning) return;
        int restartAt = -1;
        for (int i = 0; i < _simCurSteps.Count; ++i)
        {
            var step = _simCurSteps[i].step;
            if (ImGui.Button($">##{i}"))
                restartAt = i;
            ImGui.SameLine();
            DrawProgress(step.Progress, craft.CraftProgress);
            ImGui.SameLine();
            DrawProgress(step.Quality, craft.CraftQualityMax);
            ImGui.SameLine();
            DrawProgress(step.Durability, craft.CraftDurability);
            ImGui.SameLine();
            DrawProgress(step.RemainingCP, craft.StatCP);
            ImGui.SameLine();

            var sb = new StringBuilder($"{step.Condition}; {step.BuffsString()}");
            if (i + 1 < _simCurSteps.Count)
                sb.Append($"; used {_simCurSteps[i + 1].step.PrevComboAction}{(_simCurSteps[i + 1].step.PrevActionFailed ? " (fail)" : "")} ({_simCurSteps[i].comment})");
            ImGui.TextUnformatted(sb.ToString());
        }

        if (restartAt >= 0)
        {
            RestartSimulator(craft, _simCurSeed);
            while (_simCurSteps.Count <= restartAt)
                SolveNextSimulator(craft);
        }

    }

    private static void DrawProgress(int a, int b) => ImGui.ProgressBar((float)a / b, new(150, 0), $"{a * 100.0f / b:f2}% ({a}/{b})");

    private static unsafe Recipe? GetCurrentRecipe()
    {
        if (Crafting.CurRecipe != null)
            return Crafting.CurRecipe; // crafting in progress

        var re = Operations.GetSelectedRecipeEntry();
        if (re != null)
            return Svc.Data.GetExcelSheet<Recipe>()?.GetRow(re->RecipeId); // recipenote opened

        return null;
    }

    private static void SetSelectedRecipe(Recipe? recipe)
    {
        _selectedRecipe = recipe;
        _selectedCraft = null;
        _selectedSolver = default;
        _statsCurrent = null;
        _statsInProgress.Clear();
        _simCurSolver = null;
        _simCurSteps.Clear();
        _simNextRec = default;

        if (recipe != null)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipe.Value.RowId) ?? new();
            var stats = CharacterStats.GetBaseStatsForClassHeuristic(Job.CRP + recipe.Value.CraftType.RowId);
            stats.AddConsumables(new(config.RequiredFood, config.RequiredFoodHQ), new(config.RequiredPotion, config.RequiredPotionHQ), CharacterInfo.FCCraftsmanshipbuff);
            _selectedCraft = Crafting.BuildCraftStateForRecipe(stats, Job.CRP + recipe.Value.CraftType.RowId, recipe.Value);
            InitDefaultTransitionProbabilities(_selectedCraft, recipe.Value);
            var solverDesc = CraftingProcessor.GetSolverForRecipe(config, _selectedCraft);
            _selectedSolver = new(solverDesc.Name, solverDesc.CreateSolver(_selectedCraft));
        }
    }

    private static void InitDefaultTransitionProbabilities(CraftState craft, Recipe recipe)
    {
        if (recipe.IsExpert)
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

    private static bool StatisticsInProgress()
    {
        if (_statsInProgress.Count > 0)
        {
            if (!_statsInProgress.All(s => s.IsCompleted))
                return true;

            _statsCurrent = new();
            foreach (var s in _statsInProgress.Select(t => t.Result))
            {
                _statsCurrent.NumExperiments += s.NumExperiments;
                for (int i = 0; i < s.NumOutcomes.Length; ++i)
                    _statsCurrent.NumOutcomes[i] += s.NumOutcomes[i];
                _statsCurrent.QualityPercents = s.QualityPercents;
            }
        }
        return false;
    }

    private static Statistics GatherStats(CraftState craft, SolverRef solver, int numIterations, int startingQuality)
    {
        var rng = new Random();
        Statistics res = new();
        for (int i = 0; i < numIterations; ++i)
        {
            var s = solver.Clone();
            var step = Simulator.CreateInitial(craft, startingQuality);
            while (Simulator.Status(craft, step) == Simulator.CraftStatus.InProgress)
            {
                var action = s.Solve(craft, step).Action;
                if (action == Skills.None)
                    break;
                var outcome = Simulator.Execute(craft, step, action, rng.NextSingle(), rng.NextSingle());
                if (outcome.Item1 == Simulator.ExecuteResult.CantUse)
                    break;
                step = outcome.Item2;
            }
            ++res.NumOutcomes[(int)Simulator.Status(craft, step)];
            res.QualityPercents.Add(Math.Min(((float)step.Quality / craft.CraftQualityMax) * 100f, 100));
        }
        res.NumExperiments = numIterations;
        return res;
    }

    private static void RestartSimulator(CraftState craft, int rngSeed)
    {
        _simCurSeed = rngSeed;
        _simRngForSim = new(rngSeed);
        _simCurSolver = _selectedSolver.Clone();
        _simCurSteps.Clear();
        _simNextRec = default;
        if (_simCurSolver != null)
        {
            var initial = Simulator.CreateInitial(craft, (int)(craft.CraftQualityMax * _startingQualityPct / 100.0));
            _simCurSteps.Add((initial, ""));
            _simNextRec = _simCurSolver.Solve(craft, initial);
        }
    }

    private static Task RestartSimulatorUntil(CraftState craft, Simulator.CraftStatus status)
    {
        taskRunning = true;
        for (int i = 0; i < 100000; ++i)
        {
            if (_cancelTokenSource.IsCancellationRequested) break;

            RestartSimulator(craft, _simRngForSeeds.Next());
            SolveRestSimulator(craft);
            if (_simCurSteps.Count == 0 || Simulator.Status(craft, _simCurSteps.Last().step) == status)
            {
                taskRunning = false;
                return Task.CompletedTask;
            }
        }
        // failed to get desired state in a reasonable number of attempts
        _simCurSolver = null;
        _simCurSteps.Clear();
        _simNextRec = default;
        taskRunning = false;
        return Task.CompletedTask;
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

    private static void SolveRestSimulator(CraftState craft)
    {
        while (SolveNextSimulator(craft))
            ;
    }
}
