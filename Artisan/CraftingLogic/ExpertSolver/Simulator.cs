using Artisan.CraftingLogic.CraftData;
using Artisan.RawInformation.Character;
using System;

namespace Artisan.CraftingLogic.ExpertSolver;

public static class Simulator
{
    public enum CraftStatus
    {
        InProgress,
        FailedDurability,
        FailedMinQuality,
        SucceededQ1,
        SucceededQ2,
        SucceededQ3,

        Count
    }

    public enum ExecuteResult
    {
        CantUse,
        Failed,
        Succeeded
    }

    public static StepState CreateInitial(CraftState craft, float actionSuccessRoll, float nextStateRoll)
        => new() { Index = 1, Durability = craft.CraftDurability, RemainingCP = craft.StatCP, CarefulObservationLeft = craft.Specialist ? 3 : 0, HeartAndSoulAvailable = craft.Specialist, ActionSuccessRoll = actionSuccessRoll, NextStateRoll = nextStateRoll };

    public static CraftStatus Status(CraftState craft, StepState step)
    {
        return step.Progress < craft.CraftProgress
            ? (step.Durability > 0 ? CraftStatus.InProgress : CraftStatus.FailedDurability)
            : (step.Quality < craft.CraftQualityMin1 ? CraftStatus.FailedMinQuality : step.Quality < craft.CraftQualityMin2 ? CraftStatus.SucceededQ1 : step.Quality < craft.CraftQualityMin3 ? CraftStatus.SucceededQ2 : CraftStatus.SucceededQ3);
    }

    public static (ExecuteResult, StepState) Execute(CraftState craft, StepState step, uint action, float actionSuccessRoll, float nextStateRoll)
    {
        if (Status(craft, step) != CraftStatus.InProgress)
            return (ExecuteResult.CantUse, step); // can't execute action on craft that is not in progress

        var success = step.ActionSuccessRoll < GetSuccessRate(step, action);

        // TODO: check level requirements
        if (!CanUseAction(craft, step, action))
            return (ExecuteResult.CantUse, step); // can't use action because of special conditions

        var next = new StepState();
        next.Index = SkipUpdates(action) ? step.Index : step.Index + 1;
        next.Progress = step.Progress + (success ? CalculateProgress(craft, step, action) : 0);
        next.Quality = step.Quality + (success ? CalculateQuality(craft, step, action) : 0);
        next.IQStacks = step.IQStacks;
        if (success)
        {
            if (next.Quality != step.Quality)
                ++next.IQStacks;
            if (action is Skills.PreciseTouch or Skills.PreparatoryTouch or Skills.Reflect)
                ++next.IQStacks;
            if (next.IQStacks > 10)
                next.IQStacks = 10;
            if (action == Skills.ByregotsBlessing)
                next.IQStacks = 0;
        }

        next.WasteNotLeft = action switch
        {
            Skills.WasteNot => GetNewBuffDuration(step, 4),
            Skills.WasteNot2 => GetNewBuffDuration(step, 8),
            _ => GetOldBuffDuration(step.WasteNotLeft, action)
        };
        next.ManipulationLeft = action == Skills.Manipulation ? GetNewBuffDuration(step, 8) : GetOldBuffDuration(step.ManipulationLeft, action);
        next.GreatStridesLeft = action == Skills.GreatStrides ? GetNewBuffDuration(step, 3) : GetOldBuffDuration(step.GreatStridesLeft, action, next.Quality != step.Quality);
        next.InnovationLeft = action == Skills.Innovation ? GetNewBuffDuration(step, 4) : GetOldBuffDuration(step.InnovationLeft, action);
        next.VenerationLeft = action == Skills.Veneration ? GetNewBuffDuration(step, 4) : GetOldBuffDuration(step.VenerationLeft, action);
        next.MuscleMemoryLeft = action == Skills.MuscleMemory ? GetNewBuffDuration(step, 5) : GetOldBuffDuration(step.MuscleMemoryLeft, action, next.Progress != step.Progress);
        next.FinalAppraisalLeft = action == Skills.FinalAppraisal ? GetNewBuffDuration(step, 5) : GetOldBuffDuration(step.FinalAppraisalLeft, action, next.Progress >= craft.CraftProgress);
        next.CarefulObservationLeft = step.CarefulObservationLeft - (action == Skills.CarefulObservation ? 1 : 0);
        next.HeartAndSoulActive = action == Skills.HeartAndSoul || step.HeartAndSoulActive && (step.Condition is Condition.Good or Condition.Excellent || !ConsumeHeartAndSoul(action));
        next.HeartAndSoulAvailable = step.HeartAndSoulAvailable && action != Skills.HeartAndSoul;
        next.PrevComboAction = action; // note: even stuff like final appraisal and h&s break combos

        if (step.FinalAppraisalLeft > 0 && next.Progress >= craft.CraftProgress)
            next.Progress = craft.CraftProgress - 1;
        if (action == Skills.TrainedEye)
            next.Quality = craft.CraftQualityMax;

        next.RemainingCP = step.RemainingCP - GetCPCost(step, action);
        if (next.RemainingCP < 0)
            return (ExecuteResult.CantUse, step); // can't use action because of insufficient cp
        if (action == Skills.Tricks) // can't fail
            next.RemainingCP = Math.Min(craft.StatCP, next.RemainingCP + 20);

        // assume these can't fail
        next.Durability = step.Durability - GetDurabilityCost(step, action);
        if (next.Durability > 0)
        {
            int repair = 0;
            if (action == Skills.MastersMend)
                repair += 30;
            if (step.ManipulationLeft > 0 && action != Skills.Manipulation && !SkipUpdates(action))
                repair += 5;
            next.Durability = Math.Min(craft.CraftDurability, next.Durability + repair);
        }

        next.Condition = action is Skills.FinalAppraisal or Skills.HeartAndSoul ? step.Condition : GetNextCondition(craft, step);
        next.ActionSuccessRoll = actionSuccessRoll;
        next.NextStateRoll = nextStateRoll;

        return (success ? ExecuteResult.Succeeded : ExecuteResult.Failed, next);
    }

    public static int BaseProgress(CraftState craft)
    {
        int res = craft.StatCraftsmanship * 10 / craft.CraftProgressDivider + 2;
        if (craft.StatLevel <= craft.CraftLevel) // TODO: verify this condition, teamcraft uses 'rlvl' here
            res = res * craft.CraftProgressModifier / 100;
        return res;
    }

    public static int BaseQuality(CraftState craft)
    {
        int res = craft.StatControl * 10 / craft.CraftQualityDivider + 35;
        if (craft.StatLevel <= craft.CraftLevel) // TODO: verify this condition, teamcraft uses 'rlvl' here
            res = res * craft.CraftQualityModifier / 100;
        return res;
    }

    public static bool CanUseAction(CraftState craft, StepState step, uint action) => action switch
    {
        Skills.IntensiveSynthesis or Skills.PreciseTouch or Skills.Tricks => step.Condition is Condition.Good or Condition.Excellent || step.HeartAndSoulActive,
        Skills.PrudentSynthesis or Skills.PrudentTouch => step.WasteNotLeft == 0,
        Skills.MuscleMemory or Skills.Reflect => step.Index == 1,
        Skills.TrainedFinesse => step.IQStacks == 10,
        Skills.ByregotsBlessing => step.IQStacks > 0,
        Skills.TrainedEye => !craft.CraftExpert && craft.StatLevel >= craft.CraftLevel + 10 && step.Index == 1,
        Skills.CarefulObservation => step.CarefulObservationLeft > 0,
        Skills.HeartAndSoul => step.HeartAndSoulAvailable,
        _ => true
    };

    public static bool SkipUpdates(uint action) => action is Skills.CarefulObservation or Skills.FinalAppraisal or Skills.HeartAndSoul;
    public static bool ConsumeHeartAndSoul(uint action) => action is Skills.IntensiveSynthesis or Skills.PreciseTouch or Skills.Tricks;

    public static double GetSuccessRate(StepState step, uint action)
    {
        var rate = action switch
        {
            Skills.FocusedSynthesis or Skills.FocusedTouch => step.PrevComboAction == Skills.Observe ? 1.0 : 0.5,
            Skills.RapidSynthesis => 0.5,
            Skills.HastyTouch => 0.6,
            _ => 1.0
        };
        if (step.Condition == Condition.Centered)
            rate += 0.25;
        return rate;
    }

    public static int GetCPCost(StepState step, uint action)
    {
        var cost = action switch
        {
            Skills.CarefulSynthesis => 7,
            Skills.FocusedSynthesis => 5,
            Skills.Groundwork => 18,
            Skills.IntensiveSynthesis => 6,
            Skills.PrudentSynthesis => 18,
            Skills.MuscleMemory => 6,
            Skills.BasicTouch => 18,
            Skills.StandardTouch => step.PrevComboAction == Skills.BasicTouch ? 18 : 32,
            Skills.AdvancedTouch => step.PrevComboAction == Skills.StandardTouch ? 18 : 46,
            Skills.FocusedTouch => 18,
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
            _ => 0
        };
        if (step.Condition == Condition.Pliant)
            cost -= cost / 2; // round up
        return cost;
    }

    public static int GetDurabilityCost(StepState step, uint action)
    {
        var cost = action switch
        {
            Skills.BasicSynth or Skills.CarefulSynthesis or Skills.RapidSynthesis or Skills.FocusedSynthesis or Skills.IntensiveSynthesis or Skills.MuscleMemory => 10,
            Skills.BasicTouch or Skills.StandardTouch or Skills.AdvancedTouch or Skills.HastyTouch or Skills.FocusedTouch or Skills.PreciseTouch or Skills.Reflect => 10,
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
    public static int GetOldBuffDuration(int prevDuration, uint action, bool consume = false) => consume || prevDuration == 0 ? 0 : SkipUpdates(action) ? prevDuration : prevDuration - 1;

    public static int CalculateProgress(CraftState craft, StepState step, uint action)
    {
        int potency = action switch
        {
            Skills.BasicSynth => craft.StatLevel >= 31 ? 120 : 100,
            Skills.CarefulSynthesis => craft.StatLevel >= 82 ? 180 : 150,
            Skills.RapidSynthesis => craft.StatLevel >= 63 ? 500 : 250,
            Skills.FocusedSynthesis => 200,
            Skills.Groundwork => step.Durability >= GetDurabilityCost(step, action) ? (craft.StatLevel >= 86 ? 360 : 300) : (craft.StatLevel >= 86 ? 180 : 150),
            Skills.IntensiveSynthesis => 400,
            Skills.PrudentSynthesis => 180,
            Skills.MuscleMemory => 300,
            Skills.DelicateSynthesis => 100,
            _ => 0
        };
        if (potency == 0)
            return 0;

        float buffMod = 1 + (step.MuscleMemoryLeft > 0 ? 1 : 0) + (step.VenerationLeft > 0 ? 0.5f : 0);
        float effPotency = potency * buffMod;

        float condMod = step.Condition == Condition.Malleable ? 1.5f : 1;
        return (int)(BaseProgress(craft) * condMod * effPotency / 100);
    }

    public static int CalculateQuality(CraftState craft, StepState step, uint action)
    {
        int potency = action switch
        {
            Skills.BasicTouch => 100,
            Skills.StandardTouch => 125,
            Skills.AdvancedTouch => 150,
            Skills.HastyTouch => 100,
            Skills.FocusedTouch => 150,
            Skills.PreparatoryTouch => 200,
            Skills.PreciseTouch => 150,
            Skills.PrudentTouch => 100,
            Skills.TrainedFinesse => 100,
            Skills.Reflect => 100,
            Skills.ByregotsBlessing => 100 + 20 * step.IQStacks,
            Skills.DelicateSynthesis => 100,
            _ => 0
        };
        if (potency == 0)
            return 0;

        float buffMod = (1 + 0.1f * step.IQStacks) * (1 + (step.GreatStridesLeft > 0 ? 1 : 0) + (step.InnovationLeft > 0 ? 0.5f : 0));
        float effPotency = potency * buffMod;

        float condMod = step.Condition switch
        {
            Condition.Good => craft.Splendorous ? 1.75f : 1.5f,
            Condition.Excellent => 4,
            Condition.Poor => 0.5f,
            _ => 1
        };
        return (int)(BaseQuality(craft) * condMod * effPotency / 100);
    }

    public static bool WillFinishCraft(CraftState craft, StepState step, uint action) => step.FinalAppraisalLeft == 0 && step.Progress + CalculateProgress(craft, step, action) >= craft.CraftProgress;

    public static uint NextTouchCombo(StepState step) => step.PrevComboAction switch
    {
        Skills.BasicTouch => Skills.StandardTouch,
        Skills.StandardTouch => Skills.AdvancedTouch,
        _ => Skills.BasicTouch
    };

    public static Condition GetNextCondition(CraftState craft, StepState step) => step.Condition switch
    {
        Condition.Normal => GetTransitionByRoll(craft, step),
        Condition.Good => craft.CraftExpert ? GetTransitionByRoll(craft, step) : Condition.Normal,
        Condition.Excellent => Condition.Poor,
        Condition.Poor => Condition.Normal,
        Condition.GoodOmen => Condition.Good,
        _ => GetTransitionByRoll(craft, step)
    };

    public static Condition GetTransitionByRoll(CraftState craft, StepState step)
    {
        double roll = step.NextStateRoll;
        for (int i = 2; i < craft.CraftConditionProbabilities.Length; ++i)
        {
            roll -= craft.CraftConditionProbabilities[i];
            if (roll < 0)
                return (Condition)i;
        }
        return Condition.Normal;
    }
}
