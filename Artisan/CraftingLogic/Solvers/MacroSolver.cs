using Artisan.CraftingLists;
using System.Collections.Generic;
using System.Linq;
using Condition = Artisan.CraftingLogic.CraftData.Condition;
using Skills = Artisan.RawInformation.Character.Skills;

namespace Artisan.CraftingLogic.Solvers;

public class MacroSolver : ISolver
{
    public string Name(int flavour) => $"Macro: {P.Config.MacroSolverConfig.FindMacro(flavour)?.Name}";

    public IEnumerable<(int flavour, int priority, string unsupportedReason)> Flavours(CraftState craft)
    {
        foreach (var m in P.Config.MacroSolverConfig.Macros)
        {
            var statsOk = m.Options.MinCraftsmanship <= craft.StatCraftsmanship && m.Options.MinControl <= craft.StatControl && m.Options.MinCP <= craft.StatCP;
            yield return (m.ID, 0, statsOk ? "" : "You do not meet the minimum stats for this macro");
        }
    }

    public (Skills action, string comment) Solve(CraftState craft, StepState step, List<StepState> prevSteps, int flavour)
    {
        var macro = P.Config.MacroSolverConfig.FindMacro(flavour);
        if (macro == null)
            return (Skills.None, "Macro not found");

        // TODO: this is a terrible terrible hack, we probably need stateful solvers
        while (prevSteps.Count < macro.Steps.Count)
        {
            var s = macro.Steps[prevSteps.Count];
            var action = s.Action;

            if (macro.Options.SkipQualityIfMet && step.Quality >= craft.CraftQualityMin3 && ActionIsQuality(action))
            {
                prevSteps.Add(step);
                continue;
            }

            if (macro.Options.SkipObservesIfNotPoor && step.Condition != Condition.Poor && action is Skills.Observe or Skills.CarefulObservation)
            {
                prevSteps.Add(step);
                continue;
            }

            if (P.Config.SkipMacroStepIfUnable && !Simulator.CanUseAction(craft, step, action))
            {
                prevSteps.Add(step);
                continue;
            }

            if ((s.ExcludeNormal && step.Condition == Condition.Normal) ||
                (s.ExcludeGood && step.Condition == Condition.Good) ||
                (s.ExcludePoor && step.Condition == Condition.Poor) ||
                (s.ExcludeExcellent && step.Condition == Condition.Excellent) ||
                (s.ExcludeCentered && step.Condition == Condition.Centered) ||
                (s.ExcludeSturdy && step.Condition == Condition.Sturdy) ||
                (s.ExcludePliant && step.Condition == Condition.Pliant) ||
                (s.ExcludeMalleable && step.Condition == Condition.Malleable) ||
                (s.ExcludePrimed && step.Condition == Condition.Primed) ||
                (s.ExcludeGoodOmen && step.Condition == Condition.GoodOmen))
            {
                prevSteps.Add(step);
                continue;
            }

            if (action == Skills.None)
            {
                action = SolveFallback(craft, step, prevSteps);
            }

            if (!s.ExcludeFromUpgrade && step.Condition is Condition.Good or Condition.Excellent)
            {
                if (macro.Options.UpgradeQualityActions && ActionIsUpgradeableQuality(action) && Simulator.CanUseAction(craft, step, Skills.PreciseTouch))
                {
                    action = Skills.PreciseTouch;
                }
                if (macro.Options.UpgradeProgressActions && ActionIsUpgradeableProgress(action) && Simulator.CanUseAction(craft, step, Skills.IntensiveSynthesis))
                {
                    action = Skills.IntensiveSynthesis;
                }
            }

            return (action, $"{prevSteps.Count + 1}/{macro.Steps.Count}");
        }

        // we've run out of macro steps, see if we can use solver to continue
        // TODO: this is not a very good condition, it depends on external state...
        if (!P.Config.DisableMacroArtisanRecommendation || CraftingListUI.Processing)
            return (SolveFallback(craft, step, prevSteps), "Macro has completed. Now continuing with solver.");
        return (Skills.None, "Macro has completed. Please continue to manually craft.");
    }

    private Skills SolveFallback(CraftState craft, StepState step, List<StepState> prevSteps)
    {
        var s = P.GetAvailableSolversForRecipe(craft, false, this).MaxBy(f => f.priority);
        return s.solver != null ? s.solver.Solve(craft, step, prevSteps, s.flavour).action : Skills.None;
    }

    private static bool ActionIsQuality(Skills skill) => skill is Skills.BasicTouch or Skills.StandardTouch or Skills.AdvancedTouch or Skills.HastyTouch or Skills.FocusedTouch or Skills.PreparatoryTouch
        or Skills.PreciseTouch or Skills.PrudentTouch or Skills.TrainedFinesse or Skills.ByregotsBlessing or Skills.GreatStrides or Skills.Innovation;

    private static bool ActionIsUpgradeableQuality(Skills skill) => skill is Skills.HastyTouch or Skills.FocusedTouch or Skills.PreparatoryTouch or Skills.AdvancedTouch or Skills.StandardTouch or Skills.BasicTouch;
    private static bool ActionIsUpgradeableProgress(Skills skill) => skill is Skills.FocusedSynthesis or Skills.Groundwork or Skills.PrudentSynthesis or Skills.CarefulSynthesis or Skills.BasicSynthesis;
}
