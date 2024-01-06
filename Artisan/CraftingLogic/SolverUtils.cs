using Artisan.RawInformation.Character;
using System;

namespace Artisan.CraftingLogic;

public static class SolverUtils
{
    public static StepState? SimulateSolverExecution(Solver csolver, CraftState craft, int startingQuality)
    {
        var solver = csolver.Clone();
        var step = Simulator.CreateInitial(craft, startingQuality);
        while (Simulator.Status(craft, step) == Simulator.CraftStatus.InProgress)
        {
            var rec = solver.Solve(craft, step);
            var action = rec.Action;
            if (action == Skills.None)
                return null;

            var (res, next) = Simulator.Execute(craft, step, action, 0, 1);
            if (res == Simulator.ExecuteResult.CantUse)
                return null;

            step = next;
        }
        return step;
    }

    public static TimeSpan EstimateCraftTime(Solver csolver, CraftState craft, int startingQuality)
    {
        var delay = (double)P.Config.AutoDelay + (P.Config.DelayRecommendation ? P.Config.RecommendationDelay : 0);
        var delaySeconds = delay / 1000;
        var solver = csolver.Clone();

        double duration = 0;
        var step = Simulator.CreateInitial(craft, startingQuality);
        while (Simulator.Status(craft, step) == Simulator.CraftStatus.InProgress)
        {
            var action = solver.Solve(craft, step).Action;
            if (action == Skills.None)
                break;

            duration += (action.ActionIsLengthyAnimation() ? 2.5 : 1.25) + delaySeconds;

            var (res, next) = Simulator.Execute(craft, step, action, 0, 1);
            if (res == Simulator.ExecuteResult.CantUse)
                break;
            step = next;
        }
        return TimeSpan.FromSeconds(duration); // Counting crafting duration + 2 seconds between crafts.
    }

    public static double EstimateQualityPercent(Solver solver, CraftState craft, int startingQuality)
    {
        var res = SimulateSolverExecution(solver, craft, startingQuality);
        return res != null ? res.Quality * 100.0 / craft.CraftQualityMax : 0;
    }

    public static bool EstimateProgressChance(Solver solver, CraftState craft, int startingQuality)
    {
        var res = SimulateSolverExecution(solver, craft, startingQuality);
        return res != null && res.Progress >= craft.CraftProgress;
    }

    public static string EstimateCollectibleThreshold(Solver solver, CraftState craft, int startingQuality)
    {
        var res = SimulateSolverExecution(solver, craft, startingQuality);
        string finalBreakpoint = craft.CraftQualityMin2 != craft.CraftQualityMin1 ? "3rd" : "2nd";
        return res == null || res.Quality < craft.CraftQualityMin1 || res.Progress < craft.CraftProgress ? "Fail" : res.Quality >= craft.CraftQualityMin3 ? $"{finalBreakpoint}" : res.Quality >= craft.CraftQualityMin2 && craft.CraftQualityMin2 != craft.CraftQualityMin1 ? "2nd" : "1st";
    }
}
