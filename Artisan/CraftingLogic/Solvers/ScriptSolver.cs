using System;
using System.Collections.Generic;
using System.IO;

namespace Artisan.CraftingLogic.Solvers;

public class ScriptSolverDefinition : ISolverDefinition
{
    public string? MouseoverDescription { get; set; }

    public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
    {
        foreach (var s in P.Config.ScriptSolverConfig.Scripts)
        {
            var failReason = s.CompilationState() switch
            {
                ScriptSolverSettings.CompilationState.SuccessClean or ScriptSolverSettings.CompilationState.SuccessWarnings => "",
                ScriptSolverSettings.CompilationState.Failed => "Compilation error",
                _ => "Compilation in progress..."
            };
            yield return new(this, s.ID, 0, $"Script: {Path.GetFileNameWithoutExtension(s.SourcePath)}", failReason);
        }
    }

    public Solver Create(CraftState craft, int flavour) => (Solver)Activator.CreateInstance(P.Config.ScriptSolverConfig.FindScript(flavour)?.CompilationResult() ?? typeof(ScriptSolverDummy))!;
}

// used when script fails to compile
public class ScriptSolverDummy : Solver
{
    public override Recommendation Solve(CraftState craft, StepState step) => new(RawInformation.Character.Skills.None);
}
