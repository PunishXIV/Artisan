using Artisan.CraftingLogic.CraftData;

namespace Artisan.CraftingLogic.ExpertSolver;

public class CraftState
{
    public int StatCraftsmanship;
    public int StatControl;
    public int StatCP;
    public int StatLevel;
    public bool Specialist;
    public bool Splendorous;
    public bool CraftExpert;
    public int CraftLevel; // Recipe.RecipeLevelTable.ClassJobLevel
    public int CraftDurability; // Recipe.RecipeLevelTable.Durability * Recipe.DurabilityFactor / 100
    public int CraftProgress; // Recipe.RecipeLevelTable.Difficulty * Recipe.DifficultyFactor / 100
    public int CraftProgressDivider; // Recipe.RecipeLevelTable.ProgressDivider
    public int CraftProgressModifier; // Recipe.RecipeLevelTable.ProgressModifier
    public int CraftQualityDivider; // Recipe.RecipeLevelTable.QualityDivider
    public int CraftQualityModifier; // Recipe.RecipeLevelTable.QualityModifier
    public int CraftQualityMax; // Recipe.RecipeLevelTable.Quality * Recipe.QualityFactor / 100
    public int CraftQualityMin1; // min/first breakpoint
    public int CraftQualityMin2;
    public int CraftQualityMin3;
    public double[] CraftConditionProbabilities = { }; // TODO: this assumes that new condition does not depend on prev - this is what my preliminary findings suggest (except for forced transitions)

    public static double[] NormalCraftConditionProbabilities(int statLevel) => [0, 1, statLevel >= 63 ? 0.25 : 0.2, 0.04];
    public static double[] EWRelicT1CraftConditionProbabilities() => [0, 1, 0.03, 0, 0.12, 0.12, 0.12, 0, 0, 0.12];
    public static double[] EWRelicT2CraftConditionProbabilities() => [0, 1, 0.04, 0, 0, 0.15, 0.12, 0.12, 0.15, 0.12];
    public static double[] EW5StarCraftConditionProbabilities() => [0, 1, 0.04, 0, 0.12, 0.12, 0.10, 0.10, 0.12, 0.12];
}

public class StepState
{
    public int Index;
    public int Progress;
    public int Quality;
    public int Durability;
    public int RemainingCP;
    public Condition Condition;
    public int IQStacks;
    public int WasteNotLeft;
    public int ManipulationLeft;
    public int GreatStridesLeft;
    public int InnovationLeft;
    public int VenerationLeft;
    public int MuscleMemoryLeft;
    public int FinalAppraisalLeft;
    public int CarefulObservationLeft;
    public bool HeartAndSoulActive;
    public bool HeartAndSoulAvailable;
    public uint PrevComboAction;
    public double ActionSuccessRoll;
    public double NextStateRoll;
}
