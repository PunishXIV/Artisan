using Artisan.Autocraft;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation.Character;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using System;
using System.Collections.Generic;
using System.Linq;
using static OtterGui.Widgets.Tutorial;

namespace Artisan.CraftingLogic;

// monitors crafting state changes and provides recommendation based on assigned solver algorithm
// TODO: toasts etc should be moved outside - this should provide events instead
public static class CraftingProcessor
{
    public static Solver.Recommendation NextRec => _nextRec;
    public static SolverRef ActiveSolver => new(_activeSolver);

    public delegate void SolverStartedDelegate(Lumina.Excel.GeneratedSheets.Recipe recipe, SolverRef solver, CraftState craft, StepState initialStep);
    public static event SolverStartedDelegate? SolverStarted;

    public delegate void SolverFailedDelegate(Lumina.Excel.GeneratedSheets.Recipe recipe, string reason);
    public static event SolverFailedDelegate? SolverFailed; // craft started, but solver couldn't

    public delegate void SolverFinishedDelegate(Lumina.Excel.GeneratedSheets.Recipe recipe, SolverRef solver, CraftState craft, StepState finalStep);
    public static event SolverFinishedDelegate? SolverFinished;

    public delegate void RecommendationReadyDelegate(Lumina.Excel.GeneratedSheets.Recipe recipe, SolverRef solver, CraftState craft, StepState step, Solver.Recommendation recommendation);
    public static event RecommendationReadyDelegate? RecommendationReady;

    private static List<ISolverDefinition> _solverDefs = new();
    private static Solver? _activeSolver; // solver for current or expected crafting session
    private static uint? _expectedRecipe; // non-null and equal to recipe id if we've requested start of a specific craft (with a specific solver) and are waiting for it to start
    private static Solver.Recommendation _nextRec;

    public static void Setup()
    {
        _solverDefs.Add(new StandardSolverDefinition());
        _solverDefs.Add(new ExpertSolverDefinition());
        _solverDefs.Add(new MacroSolverDefinition());

        Crafting.CraftStarted += OnCraftStarted;
        Crafting.CraftAdvanced += OnCraftAdvanced;
        Crafting.CraftFinished += OnCraftFinished;
    }

    public static void Dispose()
    {
        Crafting.CraftStarted -= OnCraftStarted;
        Crafting.CraftAdvanced -= OnCraftAdvanced;
        Crafting.CraftFinished -= OnCraftFinished;
    }

    public static IEnumerable<ISolverDefinition.Desc> GetAvailableSolversForRecipe(CraftState craft, bool returnUnsupported, Type? skipSolver = null)
    {
        foreach (var solver in _solverDefs)
        {
            if (solver.GetType() == skipSolver)
                continue;

            foreach (var f in solver.Flavours(craft))
            {
                if (returnUnsupported || f.UnsupportedReason.Length == 0)
                {
                    yield return f;
                }
            }
        }
    }

    public static ISolverDefinition.Desc GetSolverForRecipe(uint recipeID, CraftState craft)
    {
        if (P.Config.RecipeSolverAssignment.TryGetValue(recipeID, out var assignment))
        {
            var solver = _solverDefs.Find(s => s.GetType().FullName == assignment.type);
            if (solver != null)
            {
                foreach (var f in solver.Flavours(craft).Where(f => f.Flavour == assignment.flavour))
                {
                    return f;
                }
            }
        }
        return GetAvailableSolversForRecipe(craft, false).MaxBy(f => f.Priority);
    }

    private static void OnCraftStarted(Lumina.Excel.GeneratedSheets.Recipe recipe, CraftState craft, StepState initialStep, bool trial)
    {
        Svc.Log.Debug($"[CProc] OnCraftStarted #{recipe.RowId} (trial={trial})");
        if (_expectedRecipe != null && _expectedRecipe.Value != recipe.RowId)
        {
            Svc.Log.Error($"Unexpected recipe started: expected {_expectedRecipe}, got {recipe.RowId}");
            _activeSolver = null; // something wrong has happened
        }
        _expectedRecipe = null;

        // we don't want any solvers running with broken gear
        if (RepairManager.GetMinEquippedPercent() == 0)
        {
            SolverFailed?.Invoke(recipe, "You have broken gear");
            _activeSolver = null;
            return;
        }

        if (_activeSolver == null)
        {
            // if we didn't provide an explicit solver, create one - but make sure if we have manually assigned one, it is actually supported
            var autoSolver = GetSolverForRecipe(recipe.RowId, craft);
            if (autoSolver.UnsupportedReason.Length > 0)
            {
                SolverFailed?.Invoke(recipe, autoSolver.UnsupportedReason);
                return;
            }
            _activeSolver = autoSolver.CreateSolver(craft);
        }

        var solverRef = new SolverRef(_activeSolver);
        SolverStarted?.Invoke(recipe, solverRef, craft, initialStep);

        Svc.Framework.RunOnTick(() =>
        {
            _nextRec = _activeSolver.Solve(craft, initialStep);
            if (_nextRec.Action != Skills.None)
                RecommendationReady?.Invoke(recipe, solverRef, craft, initialStep, _nextRec);
        }, TimeSpan.FromMilliseconds(P.Config.DelayRecommendation ? P.Config.RecommendationDelay : 0));
    }

    private static void OnCraftAdvanced(Lumina.Excel.GeneratedSheets.Recipe recipe, CraftState craft, StepState step)
    {
        Svc.Log.Debug($"[CProc] OnCraftAdvanced #{recipe.RowId} (solver={_activeSolver != null})");
        if (_activeSolver == null)
            return;

        Svc.Framework.RunOnTick(() =>
        {
            _nextRec = _activeSolver.Solve(craft, step);
            if (_nextRec.Action != Skills.None)
                RecommendationReady?.Invoke(recipe, new(_activeSolver), craft, step, _nextRec);
        }, TimeSpan.FromMilliseconds(P.Config.DelayRecommendation ? P.Config.RecommendationDelay : 0));
    }

    private static void OnCraftFinished(Lumina.Excel.GeneratedSheets.Recipe recipe, CraftState craft, StepState finalStep, bool cancelled)
    {
        Svc.Log.Debug($"[CProc] OnCraftFinished #{recipe.RowId} (cancel={cancelled}, solver={_activeSolver != null})");
        if (_activeSolver == null)
            return;

        SolverFinished?.Invoke(recipe, new(_activeSolver), craft, finalStep);
        _activeSolver = null;
        _nextRec = default;
    }
}
