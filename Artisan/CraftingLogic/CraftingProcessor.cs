using Artisan.Autocraft;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation.Character;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.CraftingLogic;

// monitors crafting state changes and provides recommendation based on assigned solver algorithm
// TODO: toasts etc should be moved outside - this should provide events instead
public static class CraftingProcessor
{
    public static Solver.Recommendation NextRec => _nextRec;
    public static SolverRef ActiveSolver = new("");

    public delegate void SolverStartedDelegate(Lumina.Excel.Sheets.Recipe recipe, SolverRef solver, CraftState craft, StepState initialStep);
    public static event SolverStartedDelegate? SolverStarted;

    public delegate void SolverFailedDelegate(Lumina.Excel.Sheets.Recipe recipe, string reason);
    public static event SolverFailedDelegate? SolverFailed; // craft started, but solver couldn't

    public delegate void SolverFinishedDelegate(Lumina.Excel.Sheets.Recipe recipe, SolverRef solver, CraftState craft, StepState finalStep);
    public static event SolverFinishedDelegate? SolverFinished;

    public delegate void RecommendationReadyDelegate(Lumina.Excel.Sheets.Recipe recipe, SolverRef solver, CraftState craft, StepState step, Solver.Recommendation recommendation);
    public static event RecommendationReadyDelegate? RecommendationReady;

    private static List<ISolverDefinition> _solverDefs = new();
    private static Solver? _activeSolver; // solver for current or expected crafting session
    private static uint? _expectedRecipe; // non-null and equal to recipe id if we've requested start of a specific craft (with a specific solver) and are waiting for it to start
    private static Solver.Recommendation _nextRec;

    public static void Setup()
    {
        _solverDefs.Add(new StandardSolverDefinition());
        _solverDefs.Add(new ProgressOnlySolverDefinition());
        _solverDefs.Add(new ExpertSolverDefinition());
        _solverDefs.Add(new MacroSolverDefinition());
        _solverDefs.Add(new ScriptSolverDefinition());
        _solverDefs.Add(new RaphaelSolverDefintion());

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
            yield return default;
        }
    }

    public static ISolverDefinition.Desc? FindSolver(CraftState craft, string type, int flavour)
    {
        var solver = type.Length > 0 ? _solverDefs.Find(s => s.GetType().FullName == type) : null;
        if (solver == null)
            return null;
        foreach (var f in solver.Flavours(craft).Where(f => f.Flavour == flavour))
            return f;
        return null;
    }

    public static ISolverDefinition.Desc GetSolverForRecipe(RecipeConfig? recipeConfig, CraftState craft)
    {
        var s = FindSolver(craft, recipeConfig?.SolverType ?? "", recipeConfig?.SolverFlavour ?? 0);
        if (s != null)
            return s.Value;

        var s2 = GetAvailableSolversForRecipe(craft, false);
        if (s2.Count() > 0)
            return s2.MaxBy(x => x.Priority);

        return default;
    }

    private static void OnCraftStarted(Lumina.Excel.Sheets.Recipe recipe, CraftState craft, StepState initialStep, bool trial)
    {
        Svc.Log.Debug($"[CProc] OnCraftStarted #{recipe.RowId} '{recipe.ItemResult.Value.Name.ToDalamudString()}' (trial={trial}) (cosmic={craft.IsCosmic}) (PQ={craft.CraftProgress}/{craft.CraftQualityMax})");
        if (_expectedRecipe != null && _expectedRecipe.Value != recipe.RowId)
        {
            Svc.Log.Error($"Unexpected recipe started: expected {_expectedRecipe}, got {recipe.RowId}");
            _activeSolver = null; // something wrong has happened
            ActiveSolver = new("");
        }
        _expectedRecipe = null;

        // we don't want any solvers running with broken gear
        if (RepairManager.GetMinEquippedPercent() == 0)
        {
            SolverFailed?.Invoke(recipe, "You have broken gear");
            _activeSolver = null;
            ActiveSolver = new("");
            return;
        }

        if (_activeSolver == null)
        {
            // if we didn't provide an explicit solver, create one - but make sure if we have manually assigned one, it is actually supported
            var autoSolver = GetSolverForRecipe(P.Config.RecipeConfigs.GetValueOrDefault(recipe.RowId), craft);
            if (autoSolver.UnsupportedReason.Length > 0)
            {
                SolverFailed?.Invoke(recipe, autoSolver.UnsupportedReason);
                return;
            }
            _activeSolver = autoSolver.CreateSolver(craft);
            ActiveSolver = new(autoSolver.Name, _activeSolver);
        }

        if (_activeSolver is ICraftValidator validator)
        {
            Svc.Log.Information("Validation");
            var validation = validator.Validate(craft);
            if (!validation)
            {
                SolverFailed?.Invoke(recipe, "You have mismatched stats");
                _activeSolver = null;
                ActiveSolver = new("");
                return;
            }
        }

        SolverStarted?.Invoke(recipe, ActiveSolver, craft, initialStep);

        _nextRec = _activeSolver.Solve(craft, initialStep);
        if (_nextRec.Action != Skills.None)
            RecommendationReady?.Invoke(recipe, ActiveSolver, craft, initialStep, _nextRec);
    }

    private static void OnCraftAdvanced(Lumina.Excel.Sheets.Recipe recipe, CraftState craft, StepState step)
    {
        Svc.Log.Debug($"[CProc] OnCraftAdvanced #{recipe.RowId} (solver={ActiveSolver.Name}): {step}");
        if (_activeSolver == null)
            return;
        if (_nextRec.Action != Skills.None && _nextRec.Action != step.PrevComboAction)
            Svc.Log.Warning($"Previous action was different from recommendation: recommended {_nextRec.Action}, used {step.PrevComboAction}");

        _nextRec = _activeSolver.Solve(craft, step);
        Svc.Log.Debug($"Next rec is: {_nextRec.Action}");
        if (_nextRec.Action != Skills.None)
            RecommendationReady?.Invoke(recipe, ActiveSolver, craft, step, _nextRec);
    }

    private static void OnCraftFinished(Lumina.Excel.Sheets.Recipe recipe, CraftState craft, StepState finalStep, bool cancelled)
    {
        Svc.Log.Debug($"[CProc] OnCraftFinished #{recipe.RowId} (cancel={cancelled}, solver={ActiveSolver.Name}): {finalStep}");
        if (_activeSolver == null)
            return;
        if (!cancelled && _nextRec.Action != Skills.None && _nextRec.Action != finalStep.PrevComboAction)
            Svc.Log.Warning($"Previous action was different from recommendation: recommended {_nextRec.Action}, used {finalStep.PrevComboAction}");

        SolverFinished?.Invoke(recipe, ActiveSolver, craft, finalStep);
        _activeSolver = null;
        ActiveSolver = new("");
        _nextRec = new();
    }
}
