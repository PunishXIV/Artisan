using Artisan.CraftingLogic.CraftData;
using Artisan.GameInterop;
using Artisan.GameInterop.CSExt;
using Artisan.RawInformation.Character;
using Dalamud.Interface.Colors;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Condition = Artisan.CraftingLogic.CraftData.Condition;

namespace Artisan.CraftingLogic;

public static class Simulator
{
    public enum CraftStatus
    {
        [Description("Craft in progress")]
        InProgress,
        [Description("Craft failed due to durability")]
        FailedDurability,
        [Description("Craft failed due to minimum quality not being met")]
        FailedMinQuality,
        [Description($"Craft has completed 1st quality breakpoint")]
        SucceededQ1,
        [Description($"Craft has completed 2nd quality breakpoint")]
        SucceededQ2,
        [Description($"Craft has completed 3rd quality breakpoint")]
        SucceededQ3,
        [Description($"Craft has completed with max quality")]
        SucceededMaxQuality,
        [Description($"Craft has completed without max quality")]
        SucceededSomeQuality,
        [Description($"Craft has completed, no quality required")]
        SucceededNoQualityReq,

        Count
    }

    public static string ToOutputString(this CraftStatus status)
    {
        return status.GetAttribute<DescriptionAttribute>().Description;
    }

    public enum ExecuteResult
    {
        CantUse,
        Failed,
        Succeeded
    }

    public static StepState CreateInitial(CraftState craft, int startingQuality)
        => new()
        {
            Index = 1,
            Durability = craft.CraftDurability,
            Quality = startingQuality,
            RemainingCP = craft.StatCP,
            CarefulObservationLeft = craft.Specialist ? 3 : 0,
            HeartAndSoulAvailable = craft.Specialist,
            QuickInnoLeft = craft.Specialist ? 1 : 0,
            TrainedPerfectionAvailable = craft.StatLevel >= MinLevel(Skills.TrainedPerfection),
            Condition = Condition.Normal,
            MaterialMiracleCharges = (uint)(craft.MissionHasMaterialMiracle ? 1 : 0),
        };

    public static CraftStatus Status(CraftState craft, StepState step)
    {
        if (step.Progress < craft.CraftProgress)
        {
            if (step.Durability > 0)
                return CraftStatus.InProgress;
            else
                return CraftStatus.FailedDurability;
        }

        if (craft.CraftCollectible || craft.CraftExpert)
        {
            if (step.Quality >= craft.CraftQualityMin3)
                return CraftStatus.SucceededQ3;

            if (step.Quality >= craft.CraftQualityMin2)
                return CraftStatus.SucceededQ2;

            if (step.Quality >= craft.CraftQualityMin1)
                return CraftStatus.SucceededQ1;

            if (step.Quality < craft.CraftRequiredQuality || step.Quality < craft.CraftQualityMin1)
                return CraftStatus.FailedMinQuality;

        }

        if (craft.CraftHQ && !craft.CraftCollectible)
        {
            if (step.Quality >= craft.CraftQualityMax)
                return CraftStatus.SucceededMaxQuality;
            else
                return CraftStatus.SucceededSomeQuality;

        }
        else
        {
            return CraftStatus.SucceededNoQualityReq;
        }
    }

    public unsafe static string SimulatorResult(Recipe recipe, RecipeConfig config, CraftState craft, out Vector4 hintColor, bool assumeMaxStartingQuality = false)
    {
        hintColor = ImGuiColors.DalamudWhite;
        var solver = CraftingProcessor.GetSolverForRecipe(config, craft).CreateSolver(craft);
        if (solver == null) return "No valid solver found.";
        var startingQuality = GetStartingQuality(recipe, assumeMaxStartingQuality, craft.StatLevel);
        var time = SolverUtils.EstimateCraftTime(solver, craft, startingQuality);
        var result = SolverUtils.SimulateSolverExecution(solver, craft, startingQuality);
        var status = result != null ? Status(craft, result) : CraftStatus.InProgress;
        var hq = result != null ? Calculations.GetHQChance((float)result.Quality / craft.CraftQualityMax * 100) : 0;

        string solverHint = status switch
        {
            CraftStatus.InProgress => "Craft did not finish (solver failed to return any more steps before finishing).",
            CraftStatus.FailedDurability => $"Craft failed due to durability shortage. (P: {(float)result.Progress / craft.CraftProgress * 100:f0}%, Q: {(float)result.Quality / craft.CraftQualityMax * 100:f0}%)",
            CraftStatus.FailedMinQuality => $"Craft completed but didn't meet minimum quality(P: {(float)result.Progress / craft.CraftProgress * 100:f0}%, Q: {(float)result.Quality / craft.CraftQualityMax * 100:f0}%).",
            CraftStatus.SucceededQ1 => $"Craft completed and managed to hit 1st quality threshold in {time.TotalSeconds:f0}s.",
            CraftStatus.SucceededQ2 => $"Craft completed and managed to hit 2nd quality threshold in {time.TotalSeconds:f0}s.",
            CraftStatus.SucceededQ3 => $"Craft completed and managed to hit 3rd quality threshold in {time.TotalSeconds:f0}s!",
            CraftStatus.SucceededMaxQuality => $"Craft completed with full quality in {time.TotalSeconds:f0}s!",
            CraftStatus.SucceededSomeQuality => $"Craft completed but didn't max out quality ({hq}%) in {time.TotalSeconds:f0}s",
            CraftStatus.SucceededNoQualityReq => $"Craft completed, no quality required in {time.TotalSeconds:f0}s!",
            CraftStatus.Count => "You shouldn't be able to see this. Report it please.",
            _ => "You shouldn't be able to see this. Report it please.",
        };


        hintColor = status switch
        {
            CraftStatus.InProgress => ImGuiColors.DalamudWhite,
            CraftStatus.FailedDurability => ImGuiColors.DalamudRed,
            CraftStatus.FailedMinQuality => ImGuiColors.DalamudRed,
            CraftStatus.SucceededQ1 => new Vector4(0.7f, 0.5f, 0.5f, 1f),
            CraftStatus.SucceededQ2 => new Vector4(0.5f, 0.5f, 0.7f, 1f),
            CraftStatus.SucceededQ3 => new Vector4(0.5f, 1f, 0.5f, 1f),
            CraftStatus.SucceededMaxQuality => ImGuiColors.ParsedGreen,
            CraftStatus.SucceededSomeQuality => new Vector4(1 - (hq / 100f), 0 + (hq / 100f), 1 - (hq / 100f), 255),
            CraftStatus.SucceededNoQualityReq => ImGuiColors.ParsedGreen,
            CraftStatus.Count => ImGuiColors.DalamudWhite,
            _ => ImGuiColors.DalamudWhite,
        };

        return solverHint;
    }

    public unsafe static int GetStartingQuality(Recipe recipe, bool assumeMaxStartingQuality, int characterLevel)
    {
        var rd = RecipeNoteRecipeData.Ptr();
        var re = rd != null ? rd->FindRecipeById(recipe.RowId) : null;
        var shqf = (float)recipe.MaterialQualityFactor / 100;
        var lt = recipe.Number == 0 && characterLevel < 100 ? Svc.Data.GetExcelSheet<RecipeLevelTable>().First(x => x.ClassJobLevel == characterLevel) : recipe.RecipeLevelTable.Value;
        var startingQuality = assumeMaxStartingQuality ? (int)(Calculations.RecipeMaxQuality(recipe, lt) * shqf) : re != null ? Calculations.GetStartingQuality(recipe, re->GetAssignedHQIngredients(), lt) : 0;
        return startingQuality;
    }

    public static (ExecuteResult, StepState) Execute(CraftState craft, StepState step, Skills action, float actionSuccessRoll, float nextStateRoll)
    {
        if (Status(craft, step) != CraftStatus.InProgress)
            return (ExecuteResult.CantUse, step); // can't execute action on craft that is not in progress

        var success = actionSuccessRoll < GetSuccessRate(step, action);

        if (!CanUseAction(craft, step, action))
            return (ExecuteResult.CantUse, step); // can't use action because of level, insufficient cp or special conditions

        var next = new StepState();
        next.Index = SkipUpdates(action) ? step.Index : step.Index + 1;
        next.Progress = step.Progress + (success ? CalculateProgress(craft, step, action) : 0);
        next.Quality = step.Quality + (success ? CalculateQuality(craft, step, action) : 0);
        next.IQStacks = step.IQStacks;
        if (success)
        {
            if (next.Quality != step.Quality)
                ++next.IQStacks;
            if (action is Skills.PreciseTouch or Skills.PreparatoryTouch or Skills.Reflect or Skills.RefinedTouch)
                ++next.IQStacks;
            if (next.IQStacks > 10)
                next.IQStacks = 10;
            if (action == Skills.ByregotsBlessing)
                next.IQStacks = 0;
            if (action == Skills.HastyTouch)
                next.ExpedienceLeft = 1;
            else
                next.ExpedienceLeft = 0;
        }

        next.WasteNotLeft = action switch
        {
            Skills.WasteNot => GetNewBuffDuration(step, 4),
            Skills.WasteNot2 => GetNewBuffDuration(step, 8),
            _ => GetOldBuffDuration(step.WasteNotLeft, action)
        };
        next.ManipulationLeft = action == Skills.Manipulation ? GetNewBuffDuration(step, 8) : GetOldBuffDuration(step.ManipulationLeft, action);
        next.GreatStridesLeft = action == Skills.GreatStrides ? GetNewBuffDuration(step, 3) : GetOldBuffDuration(step.GreatStridesLeft, action, next.Quality != step.Quality);
        next.InnovationLeft = action == Skills.Innovation ? GetNewBuffDuration(step, 4) : action == Skills.QuickInnovation ? GetNewBuffDuration(step, 1) : GetOldBuffDuration(step.InnovationLeft, action);
        next.VenerationLeft = action == Skills.Veneration ? GetNewBuffDuration(step, 4) : GetOldBuffDuration(step.VenerationLeft, action);
        next.MuscleMemoryLeft = action == Skills.MuscleMemory ? GetNewBuffDuration(step, 5) : GetOldBuffDuration(step.MuscleMemoryLeft, action, next.Progress != step.Progress);
        next.FinalAppraisalLeft = action == Skills.FinalAppraisal ? GetNewBuffDuration(step, 5) : GetOldBuffDuration(step.FinalAppraisalLeft, action, next.Progress >= craft.CraftProgress);
        next.CarefulObservationLeft = step.CarefulObservationLeft - (action == Skills.CarefulObservation ? 1 : 0);
        next.HeartAndSoulActive = action == Skills.HeartAndSoul || step.HeartAndSoulActive && (step.Condition is Condition.Good or Condition.Excellent || !ConsumeHeartAndSoul(action));
        next.HeartAndSoulAvailable = step.HeartAndSoulAvailable && action != Skills.HeartAndSoul;
        next.QuickInnoLeft = step.QuickInnoLeft - (action == Skills.QuickInnovation ? 1 : 0);
        next.QuickInnoAvailable = step.QuickInnoLeft > 0 && next.InnovationLeft == 0;
        next.PrevActionFailed = !success;
        next.PrevComboAction = action; // note: even stuff like final appraisal and h&s break combos
        next.TrainedPerfectionActive = action == Skills.TrainedPerfection || (step.TrainedPerfectionActive && !HasDurabilityCost(action));
        next.TrainedPerfectionAvailable = step.TrainedPerfectionAvailable && action != Skills.TrainedPerfection;
        next.MaterialMiracleCharges = action == Skills.MaterialMiracle ? step.MaterialMiracleCharges - 1 : step.MaterialMiracleCharges;
        next.MaterialMiracleActive = step.MaterialMiracleActive; //This is a timed buff, can't really use this in the simulator, just copy the real result
        next.ObserveCounter = action == Skills.Observe ? step.ObserveCounter + 1 : 0;

        if (step.FinalAppraisalLeft > 0 && next.Progress >= craft.CraftProgress)
            next.Progress = craft.CraftProgress - 1;

        next.RemainingCP = step.RemainingCP - GetCPCost(step, action);
        if (action == Skills.TricksOfTrade) // can't fail
            next.RemainingCP = Math.Min(craft.StatCP, next.RemainingCP + 20);

        // assume these can't fail
        next.Durability = step.Durability - GetDurabilityCost(step, action);
        if (next.Durability > 0)
        {
            int repair = 0;
            if (action == Skills.MastersMend)
                repair += 30;
            if (action == Skills.ImmaculateMend)
                repair = craft.CraftDurability;
            if (step.ManipulationLeft > 0 && action != Skills.Manipulation && !SkipUpdates(action) && next.Progress < craft.CraftProgress)
                repair += 5;
            next.Durability = Math.Min(craft.CraftDurability, next.Durability + repair);
        }

        next.Condition = action is Skills.FinalAppraisal or Skills.HeartAndSoul ? step.Condition : GetNextCondition(craft, step, nextStateRoll);

        return (success ? ExecuteResult.Succeeded : ExecuteResult.Failed, next);
    }

    private static bool HasDurabilityCost(Skills action)
    {
        var cost = action switch
        {
            Skills.BasicSynthesis or Skills.CarefulSynthesis or Skills.RapidSynthesis or Skills.IntensiveSynthesis or Skills.MuscleMemory => 10,
            Skills.BasicTouch or Skills.StandardTouch or Skills.AdvancedTouch or Skills.HastyTouch or Skills.PreciseTouch or Skills.Reflect or Skills.RefinedTouch => 10,
            Skills.ByregotsBlessing or Skills.DelicateSynthesis => 10,
            Skills.Groundwork or Skills.PreparatoryTouch => 20,
            Skills.PrudentSynthesis or Skills.PrudentTouch => 5,
            _ => 0
        };

        return cost > 0;
    }

    public static int BaseProgress(CraftState craft)
    {
        float res = craft.StatCraftsmanship * 10.0f / craft.CraftProgressDivider + 2;
        if (craft.StatLevel <= craft.CraftLevel) // TODO: verify this condition, teamcraft uses 'rlvl' here
            res = res * craft.CraftProgressModifier / 100;
        return (int)res;
    }

    public static int BaseQuality(CraftState craft)
    {
        float res = craft.StatControl * 10.0f / craft.CraftQualityDivider + 35;
        if (craft.StatLevel <= craft.CraftLevel) // TODO: verify this condition, teamcraft uses 'rlvl' here
            res = res * craft.CraftQualityModifier / 100;
        return (int)res;
    }

    public static int MinLevel(Skills action) => action.Level();

    public static bool CanUseAction(CraftState craft, StepState step, Skills action) => action switch
    {
        Skills.IntensiveSynthesis or Skills.PreciseTouch or Skills.TricksOfTrade => step.Condition is Condition.Good or Condition.Excellent || step.HeartAndSoulActive,
        Skills.PrudentSynthesis or Skills.PrudentTouch => step.WasteNotLeft == 0,
        Skills.MuscleMemory or Skills.Reflect => step.Index == 1,
        Skills.TrainedFinesse => step.IQStacks == 10,
        Skills.ByregotsBlessing => step.IQStacks > 0,
        Skills.TrainedEye => !craft.CraftExpert && craft.StatLevel >= craft.CraftLevel + 10 && step.Index == 1,
        Skills.Manipulation => craft.UnlockedManipulation,
        Skills.CarefulObservation => step.CarefulObservationLeft > 0,
        Skills.HeartAndSoul => step.HeartAndSoulAvailable,
        Skills.TrainedPerfection => step.TrainedPerfectionAvailable,
        Skills.DaringTouch => step.ExpedienceLeft > 0,
        Skills.QuickInnovation => step.QuickInnoLeft > 0 && step.InnovationLeft == 0,
        Skills.MaterialMiracle => step.MaterialMiracleCharges > 0 && !step.MaterialMiracleActive,
        _ => true
    } && craft.StatLevel >= MinLevel(action) && step.RemainingCP >= GetCPCost(step, action);

    public static bool CannotUseAction(CraftState craft, StepState step, Skills action, out string reason)
    {
        if (!CanUseAction(craft, step, action))
        {
            reason = action switch
            {
                Skills.IntensiveSynthesis or Skills.PreciseTouch or Skills.TricksOfTrade => "Condition is not Good/Excellent or Heart and Soul is not active",
                Skills.PrudentSynthesis or Skills.PrudentTouch => "You have a Waste Not buff",
                Skills.MuscleMemory or Skills.Reflect => "You are not on the first step of the craft",
                Skills.TrainedFinesse => "You have less than 10 Inner Quiet stacks",
                Skills.ByregotsBlessing => "You have 0 Inner Quiet stacks",
                Skills.TrainedEye => craft.CraftExpert ? "Craft is expert" : step.Index != 1 ? "You are not on the first step of the craft" : "Craft is not 10 or more levels lower than your current level",
                Skills.Manipulation => "You haven't unlocked Manipulation",
                Skills.CarefulObservation => craft.Specialist ? Crafting.DelineationCount() == 0 ? "You have run out of Delineations." : $"You already used Careful Observation 3 times" : "You are not a specialist",
                Skills.HeartAndSoul => craft.Specialist ? Crafting.DelineationCount() == 0 ? "You have run out of Delineations." : "You don't have Heart & Soul available anymore for this craft" : "You are not a specialist",
                Skills.TrainedPerfection => "You have already used Trained Perfection",
                Skills.DaringTouch => "Hasty Touch did not succeed",
                Skills.QuickInnovation => !craft.Specialist ? "You are not a specialist" : Crafting.DelineationCount() == 0 ? "You have run out of Delineations." : step.QuickInnoLeft == 0 ? "You don't have Quick Innovation available anymore for this craft" : step.InnovationLeft > 0 ? "You have an Innovation buff" : "",
                Skills.MaterialMiracle => !craft.MissionHasMaterialMiracle ? "This craft cannot use Material Miracle" : step.MaterialMiracleActive ? "You already have Material Miracle active" : step.MaterialMiracleCharges == 0 ? "You have no more charges" : ""
            };

            return true;
        }
        reason = "";
        return false;
    }

    public static bool SkipUpdates(Skills action) => action is Skills.CarefulObservation or Skills.FinalAppraisal or Skills.HeartAndSoul or Skills.MaterialMiracle;
    public static bool ConsumeHeartAndSoul(Skills action) => action is Skills.IntensiveSynthesis or Skills.PreciseTouch or Skills.TricksOfTrade;

    public static double GetSuccessRate(StepState step, Skills action)
    {
        var rate = action switch
        {
            Skills.RapidSynthesis => 0.5,
            Skills.HastyTouch or Skills.DaringTouch => 0.6,
            _ => 1.0
        };
        if (step.Condition == Condition.Centered)
            rate += 0.25;
        return rate;
    }

    public static int GetBaseCPCost(Skills action, Skills prevAction) => action switch
    {
        Skills.CarefulSynthesis => 7,
        Skills.Groundwork => 18,
        Skills.IntensiveSynthesis => 6,
        Skills.PrudentSynthesis => 18,
        Skills.MuscleMemory => 6,
        Skills.BasicTouch => 18,
        Skills.StandardTouch => prevAction == Skills.BasicTouch ? 18 : 32,
        Skills.AdvancedTouch => prevAction is Skills.StandardTouch or Skills.Observe ? 18 : 46,
        Skills.PreparatoryTouch => 40,
        Skills.PreciseTouch => 18,
        Skills.PrudentTouch => 25,
        Skills.TrainedFinesse => 32,
        Skills.Reflect => 6,
        Skills.ByregotsBlessing => 24,
        Skills.TrainedEye => 250,
        Skills.DelicateSynthesis => 32,
        Skills.Veneration => 18,
        Skills.Innovation => 18,
        Skills.GreatStrides => 32,
        Skills.MastersMend => 88,
        Skills.Manipulation => 96,
        Skills.WasteNot => 56,
        Skills.WasteNot2 => 98,
        Skills.Observe => 7,
        Skills.FinalAppraisal => 1,
        Skills.RefinedTouch => 24,
        Skills.ImmaculateMend => 112,
        _ => 0
    };

    public static int GetCPCost(StepState step, Skills action)
    {
        var cost = GetBaseCPCost(action, step.PrevComboAction);
        if (step.Condition == Condition.Pliant)
            cost -= cost / 2; // round up
        return cost;
    }

    public static int GetDurabilityCost(StepState step, Skills action)
    {
        if (step.TrainedPerfectionActive) return 0;
        var cost = action switch
        {
            Skills.BasicSynthesis or Skills.CarefulSynthesis or Skills.RapidSynthesis or Skills.IntensiveSynthesis or Skills.MuscleMemory => 10,
            Skills.BasicTouch or Skills.StandardTouch or Skills.AdvancedTouch or Skills.HastyTouch or Skills.DaringTouch or Skills.PreciseTouch or Skills.Reflect or Skills.RefinedTouch => 10,
            Skills.ByregotsBlessing or Skills.DelicateSynthesis => 10,
            Skills.Groundwork or Skills.PreparatoryTouch => 20,
            Skills.PrudentSynthesis or Skills.PrudentTouch => 5,
            _ => 0
        };
        if (step.WasteNotLeft > 0)
            cost -= cost / 2; // round up
        if (step.Condition == Condition.Sturdy)
            cost -= cost / 2; // round up
        return cost;
    }

    public static int GetNewBuffDuration(StepState step, int baseDuration) => baseDuration + (step.Condition == Condition.Primed ? 2 : 0);
    public static int GetOldBuffDuration(int prevDuration, Skills action, bool consume = false) => consume || prevDuration == 0 ? 0 : SkipUpdates(action) ? prevDuration : prevDuration - 1;

    public static int CalculateProgress(CraftState craft, StepState step, Skills action)
    {
        int potency = action switch
        {
            Skills.BasicSynthesis => craft.StatLevel >= 31 ? 120 : 100,
            Skills.CarefulSynthesis => craft.StatLevel >= 82 ? 180 : 150,
            Skills.RapidSynthesis => craft.StatLevel >= 63 ? 500 : 250,
            Skills.Groundwork => step.Durability >= GetDurabilityCost(step, action) ? craft.StatLevel >= 86 ? 360 : 300 : craft.StatLevel >= 86 ? 180 : 150,
            Skills.IntensiveSynthesis => 400,
            Skills.PrudentSynthesis => 180,
            Skills.MuscleMemory => 300,
            Skills.DelicateSynthesis => craft.StatLevel >= 94 ? 150 : 100,
            _ => 0
        };
        if (potency == 0)
            return 0;

        float buffMod = 1 + (step.MuscleMemoryLeft > 0 ? 1 : 0) + (step.VenerationLeft > 0 ? 0.5f : 0);
        float effPotency = potency * buffMod;

        float condMod = step.Condition == Condition.Malleable ? 1.5f : 1;
        return (int)(BaseProgress(craft) * condMod * effPotency / 100);
    }

    public static int CalculateQuality(CraftState craft, StepState step, Skills action)
    {
        if (action == Skills.TrainedEye)
            return craft.CraftQualityMax;

        int potency = action switch
        {
            Skills.BasicTouch => 100,
            Skills.StandardTouch => 125,
            Skills.AdvancedTouch => 150,
            Skills.HastyTouch => 100,
            Skills.DaringTouch => 150,
            Skills.PreparatoryTouch => 200,
            Skills.PreciseTouch => 150,
            Skills.PrudentTouch => 100,
            Skills.TrainedFinesse => 100,
            Skills.Reflect => 300,
            Skills.ByregotsBlessing => 100 + 20 * step.IQStacks,
            Skills.DelicateSynthesis => 100,
            Skills.RefinedTouch => 100,
            _ => 0
        };
        if (potency == 0)
            return 0;

        float buffMod = (1 + (step.GreatStridesLeft > 0 ? 1 : 0) + (step.InnovationLeft > 0 ? 0.5f : 0)) * (100 + 10 * step.IQStacks) / 100;
        float effPotency = potency * buffMod;

        float condMod = step.Condition switch
        {
            Condition.Good => craft.SplendorCosmic ? 1.75f : 1.5f,
            Condition.Excellent => 4,
            Condition.Poor => 0.5f,
            _ => 1
        };
        return (int)(BaseQuality(craft) * condMod * effPotency / 100);
    }

    public static bool WillFinishCraft(CraftState craft, StepState step, Skills action) => step.FinalAppraisalLeft == 0 && step.Progress + CalculateProgress(craft, step, action) >= craft.CraftProgress;

    public static Skills NextTouchCombo(StepState step, CraftState craft)
    {
        if (step.PrevComboAction == Skills.BasicTouch && craft.StatLevel >= MinLevel(Skills.StandardTouch)) return Skills.StandardTouch;
        if (step.PrevComboAction == Skills.StandardTouch && craft.StatLevel >= MinLevel(Skills.AdvancedTouch)) return Skills.AdvancedTouch;
        return Skills.BasicTouch;
    }

    internal static Skills NextTouchComboRefined(StepState step, CraftState craft)
    {
        if (step.PrevComboAction == Skills.BasicTouch && craft.StatLevel >= MinLevel(Skills.RefinedTouch)) return Skills.RefinedTouch;
        return Skills.BasicTouch;
    }

    public static Condition GetNextCondition(CraftState craft, StepState step, float roll) => step.Condition switch
    {
        Condition.Normal => GetTransitionByRoll(craft, step, roll),
        Condition.Good => craft.CraftExpert ? GetTransitionByRoll(craft, step, roll) : Condition.Normal,
        Condition.Excellent => Condition.Poor,
        Condition.Poor => Condition.Normal,
        Condition.GoodOmen => Condition.Good,
        _ => GetTransitionByRoll(craft, step, roll)
    };

    public static Condition GetTransitionByRoll(CraftState craft, StepState step, float roll)
    {
        for (int i = 1; i < craft.CraftConditionProbabilities.Length; ++i)
        {
            roll -= craft.CraftConditionProbabilities[i];
            if (roll < 0)
                return (Condition)i;
        }
        return Condition.Normal;
    }

    public static ConditionFlags ConditionToFlag(this Condition condition)
    {
        return condition switch
        {
            Condition.Normal => ConditionFlags.Normal,
            Condition.Good => ConditionFlags.Good,
            Condition.Excellent => ConditionFlags.Excellent,
            Condition.Poor => ConditionFlags.Poor,
            Condition.Centered => ConditionFlags.Centered,
            Condition.Sturdy => ConditionFlags.Sturdy,
            Condition.Pliant => ConditionFlags.Pliant,
            Condition.Malleable => ConditionFlags.Malleable,
            Condition.Primed => ConditionFlags.Primed,
            Condition.GoodOmen => ConditionFlags.GoodOmen,
            Condition.Unknown => throw new NotImplementedException(),
        };
    }

}
