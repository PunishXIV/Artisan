using Artisan.RawInformation.Character;
using System.Collections.Generic;

namespace Artisan.CraftingLogic;

public interface ISolver
{
    public string Name(int flavour);
    public IEnumerable<(int flavour, int priority, string unsupportedReason)> Flavours(CraftState craft);
    public (Skills action, string comment) Solve(CraftState craft, StepState step, List<StepState> prevSteps, int flavour);
}
