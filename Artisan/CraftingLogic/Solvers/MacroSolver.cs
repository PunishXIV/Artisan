using Artisan.CraftingLists;
using ECommons.DalamudServices;
using SharpDX.DirectWrite;
using System.Collections.Generic;
using System.Linq;
using Condition = Artisan.CraftingLogic.CraftData.Condition;
using Skills = Artisan.RawInformation.Character.Skills;

namespace Artisan.CraftingLogic.Solvers;

public class MacroSolverDefinition : ISolverDefinition
{
    public string MouseoverDescription { get; set; } = "This is the equivalent of an in-game macro, with less restrictions.";

    public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
    {
        foreach (var m in P.Config.MacroSolverConfig.Macros)
        {
            if (m.Steps.Count == 0) continue;

            var statsOk = m.Options.MinCraftsmanship <= craft.StatCraftsmanship && m.Options.MinControl <= craft.StatControl && m.Options.MinCP <= craft.StatCP;
            yield return new(this, m.ID, 0, $"Macro: {m.Name}", statsOk ? "" : "You do not meet the minimum stats for this macro");
        }
    }

    public Solver Create(CraftState craft, int flavour) => new MacroSolver(P.Config.MacroSolverConfig.FindMacro(flavour) ?? new(), craft);
}

public class MacroSolver : Solver, ICraftValidator
{
    private MacroSolverSettings.Macro _macro;
    private Solver? _fallback;
    private int _nextStep;

    public MacroSolver(MacroSolverSettings.Macro m, CraftState craft)
    {
        _macro = m;
        _fallback = CraftingProcessor.GetAvailableSolversForRecipe(craft, false, typeof(MacroSolverDefinition)).MaxBy(f => f.Priority).CreateSolver(craft);
    }

    public override Solver Clone()
    {
        var res = (MacroSolver)MemberwiseClone();
        res._fallback = _fallback?.Clone();
        return res;
    }

    public override Recommendation Solve(CraftState craft, StepState step)
    {
        var fallback = _fallback?.Solve(craft, step); // note: we need to call it, even if we provide rec from macro, to ensure fallback solver's state is updated

        while (_nextStep < _macro.Steps.Count)
        {
            var s = _macro.Steps[_nextStep++];
            var action = s.Action;
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
                if (s.ReplaceOnExclude)
                {
                    action = s.ReplacementAction;
                }
                else
                {
                    continue;
                }
            }

            if (_macro.Options.SkipQualityIfMet && step.Quality >= craft.CraftQualityMin3 && ActionIsQuality(action))
            {
                continue;
            }

            if (_macro.Options.SkipObservesIfNotPoor && step.Condition != Condition.Poor && action is Skills.Observe or Skills.CarefulObservation)
            {
                continue;
            }

            if (P.Config.SkipMacroStepIfUnable && !Simulator.CanUseAction(craft, step, action))
            {
                continue;
            }


            if (action == Skills.TouchCombo)
            {
                action = Simulator.NextTouchCombo(step, craft);
            }

            if (action == Skills.TouchComboRefined)
            {
                action = Simulator.NextTouchComboRefined(step, craft);
            }

            if (action == Skills.None)
            {
                action = fallback?.Action ?? Skills.None;
            }

            if (!s.ExcludeFromUpgrade && step.Condition is Condition.Good or Condition.Excellent)
            {
                if (_macro.Options.UpgradeQualityActions && ActionIsUpgradeableQuality(action) && Simulator.CanUseAction(craft, step, Skills.PreciseTouch))
                {
                    action = Skills.PreciseTouch;
                }
                if (_macro.Options.UpgradeProgressActions && ActionIsUpgradeableProgress(action) && Simulator.CanUseAction(craft, step, Skills.IntensiveSynthesis))
                {
                    action = Skills.IntensiveSynthesis;
                }
            }

            return new(action, $"{_nextStep}/{_macro.Steps.Count}");
        }

        // we've run out of macro steps, see if we can use solver to continue
        // TODO: this is not a very good condition, it depends on external state...
        if (!P.Config.DisableMacroArtisanRecommendation || CraftingListUI.Processing)
            return new(fallback?.Action ?? Skills.None, fallback?.Action is null ? $"Macro has completed, the fallback solver is not working{(craft.UnlockedManipulation ? " " : " (You need to unlock Manipulation) ")}so you will have to manually finish this" : "Macro has completed. Now continuing with solver.");
        return new(Skills.None, "Macro has completed. Please continue to manually craft.");
    }

    private static bool ActionIsQuality(Skills skill) => skill is Skills.BasicTouch or Skills.StandardTouch or Skills.AdvancedTouch or Skills.HastyTouch or Skills.PreparatoryTouch
        or Skills.PreciseTouch or Skills.PrudentTouch or Skills.TrainedFinesse or Skills.ByregotsBlessing or Skills.GreatStrides or Skills.Innovation or Skills.TouchCombo or Skills.TouchComboRefined;

    private static bool ActionIsUpgradeableQuality(Skills skill) => skill is Skills.HastyTouch or Skills.PreparatoryTouch or Skills.AdvancedTouch or Skills.StandardTouch or Skills.BasicTouch;
    private static bool ActionIsUpgradeableProgress(Skills skill) => skill is Skills.Groundwork or Skills.PrudentSynthesis or Skills.CarefulSynthesis or Skills.BasicSynthesis;

    public bool Validate(CraftState craft)
    {
        if(_macro.Options.ExactCraftsmanship != 0)
        {
            return _macro.Options.ExactCraftsmanship == craft.StatCraftsmanship && _macro.Options.MinControl <= craft.StatControl && _macro.Options.MinCP <= craft.StatCP;
        }


        return _macro.Options.MinCraftsmanship <= craft.StatCraftsmanship && _macro.Options.MinControl <= craft.StatControl && _macro.Options.MinCP <= craft.StatCP;
    }
}
