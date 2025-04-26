using System.Collections.Generic;
using Skills = Artisan.RawInformation.Character.Skills;

namespace Artisan.CraftingLogic;

// solver definition describes a family of solvers; it is used to create individual solvers for specific crafts
public interface ISolverDefinition
{
    public record struct Desc(ISolverDefinition Def, int Flavour, int Priority, string Name, string UnsupportedReason = "")
    {
        public Solver? CreateSolver(CraftState craft)
        {
            return this == default ? null : Def.Create(craft, Flavour);
        }
    }

    public IEnumerable<Desc> Flavours(CraftState craft);
    public Solver Create(CraftState craft, int flavour);
}

// base class for solvers; instances of solvers can be stateful, so be sure to clone if you want to do some simulation without disturbing original state
public abstract class Solver
{
    public record struct Recommendation(Skills Action, string Comment = "");

    public virtual Solver Clone() => (Solver)MemberwiseClone(); // shallow copy by default
    public abstract Recommendation Solve(CraftState craft, StepState step); // note that this function potentially mutates state!
}

public interface ICraftValidator
{
    public bool Validate(CraftState craft);
}

// a simple wrapper around solver that allows creating clones on-demand, but does not allow calling solve directly
public struct SolverRef
{
    public string Name { get; private init; } = "";
    private Solver? _solver;

    public SolverRef(string name, Solver? solver = null)
    {
        Name = name;
        _solver = solver;
    }

    public Solver? Clone() => _solver?.Clone();
    public bool IsType<T>() where T : Solver => _solver is T;

    public static implicit operator bool(SolverRef x) => x._solver != null;
}
