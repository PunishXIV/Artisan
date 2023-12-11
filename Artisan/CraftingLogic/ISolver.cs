using Artisan.RawInformation.Character;
using System.Collections.Generic;

namespace Artisan.CraftingLogic;

// solver definition describes a family of solvers; it is used to create individual solvers for specific crafts
public interface ISolverDefinition
{
    public record struct Desc(ISolverDefinition Def, int Flavour, int Priority, string Name, string UnsupportedReason = "")
    {
        public Solver CreateSolver(CraftState craft) => Def.Create(craft, Flavour, Name);
    }

    public IEnumerable<Desc> Flavours(CraftState craft);
    public Solver Create(CraftState craft, int flavour, string name);
}

// base class for solvers; instances of solvers can be stateful, so be sure to clone if you want to do some simulation without disturbing original state
public abstract class Solver
{
    public record struct Recommendation(Skills Action, string Comment = "");

    public string Name { get; private init; }

    public Solver(string name)
    {
        Name = name;
    }

    public virtual Solver Clone() => (Solver)MemberwiseClone(); // shallow copy by default
    public abstract Recommendation Solve(CraftState craft, StepState step); // note that this function potentially mutates state!
}

// a simple wrapper around solver that allows creating clones on-demand, but does not allow calling solve directly
public struct SolverRef
{
    private Solver? _solver;

    public SolverRef(Solver? solver = null) => _solver = solver;

    public string Name => _solver?.Name ?? "";

    public Solver? Clone() => _solver?.Clone();
    public bool IsType<T>() where T : Solver => _solver is T;

    public static implicit operator bool(SolverRef x) => x._solver != null;
}
