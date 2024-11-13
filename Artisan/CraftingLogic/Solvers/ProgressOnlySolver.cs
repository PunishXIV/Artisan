using Artisan.RawInformation.Character;
using System.Collections.Generic;

namespace Artisan.CraftingLogic.Solvers
{
    public class ProgressOnlySolverDefinition : ISolverDefinition
    {
        public Solver Create(CraftState craft, int flavour) => new ProgressOnlySolver();

        public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
        {
            if (!craft.CraftExpert && !craft.CraftCollectible)
            yield return new(this, 0, 1, "Progress Only Solver");
        }
    }

    public class ProgressOnlySolver : Solver
    {
        public override Recommendation Solve(CraftState craft, StepState step)
        {
            if (Simulator.CanUseAction(craft, step, Skills.MuscleMemory))
                return new(Skills.MuscleMemory);

            if (step.VenerationLeft == 0 && Simulator.CanUseAction(craft, step, Skills.Veneration))
                return new(Skills.Veneration);

            Skills synthOption = new StandardSolver(false).BestSynthesis(craft, step, true);
            if (Simulator.GetDurabilityCost(step, synthOption) >= step.Durability)
            {
                if (Simulator.CanUseAction(craft, step, Skills.ImmaculateMend) && craft.CraftDurability >= 70) return new(Skills.ImmaculateMend);
                if (Simulator.CanUseAction(craft, step, Skills.MastersMend)) return new(Skills.MastersMend);
            }

            return new(synthOption);
        }
    }
}
