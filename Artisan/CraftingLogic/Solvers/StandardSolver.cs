using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using Condition = Artisan.CraftingLogic.CraftData.Condition;
using Skills = Artisan.RawInformation.Character.Skills;

namespace Artisan.CraftingLogic.Solvers
{
    public class StandardSolverDefinition : ISolverDefinition
    {
        public string MouseoverDescription { get; set; } = "This is the normal recipe solver.";

        public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
        {
            if (!craft.CraftExpert && craft.CraftHQ)
                yield return new(this, 0, 2, "Standard Recipe Solver");
        }

        public Solver Create(CraftState craft, int flavour) => new StandardSolver(flavour != 0);
    }

    public class StandardSolver : Solver
    {
        private bool _expert;

        // for normal crafts, we don't ever want to use manip/wn more than once
        private bool _manipulationUsed;
        private bool _wasteNotUsed;
        private bool _qualityStarted;
        private bool _venereationUsed;
        private bool _trainedEyeUsed;
        private bool _materialMiracleUsed;

        private Solver? _fallback; //For Material Miracle

        public StandardSolver(bool expert)
        {
            _expert = expert;
            _fallback = new ExpertSolver();
        }

        public override Recommendation Solve(CraftState craft, StepState step)
        {
            var rec = GetRecommendation(craft, step);

            if (Simulator.GetDurabilityCost(step, rec.Action) == 0)
            {
                if (rec.Action != Skills.MaterialMiracle)
                {
                    if (step.Durability <= 10 && Simulator.CanUseAction(craft, step, Skills.MastersMend)) rec.Action = Skills.MastersMend;
                    if (step.Durability <= 10 && Simulator.CanUseAction(craft, step, Skills.ImmaculateMend) && craft.CraftDurability >= 70) rec.Action = Skills.ImmaculateMend;
                }
            }
            else
            {
                var stepClone = rec.Action;
                if (WillActFail(craft, step, stepClone) && Simulator.CanUseAction(craft, step, Skills.MastersMend)) rec.Action = Skills.MastersMend;
                if (WillActFail(craft, step, stepClone) && Simulator.CanUseAction(craft, step, Skills.ImmaculateMend) && craft.CraftDurability >= 70) rec.Action = Skills.ImmaculateMend;

            }

            if ((rec.Action is not Skills.MastersMend or Skills.ImmaculateMend) &&
                step.Quality < craft.CraftQualityMax &&
                Simulator.CanUseAction(craft, step, Skills.ByregotsBlessing) &&
                step.RemainingCP - Simulator.GetCPCost(step, rec.Action) < Simulator.GetCPCost(step, Skills.ByregotsBlessing) &&
                !WillActFail(craft, step, Skills.ByregotsBlessing))
            {
                rec.Action = Skills.ByregotsBlessing;
            }

            if ((rec.Action is Skills.MastersMend or Skills.ImmaculateMend) &&
                step.Condition is Condition.Good or Condition.Excellent && Simulator.CanUseAction(craft, step, Skills.TricksOfTrade))
                rec.Action = Skills.TricksOfTrade;

            if (Simulator.GetDurabilityCost(step, rec.Action) == 20 && !_trainedEyeUsed && step.TrainedPerfectionAvailable && step.VenerationLeft == 0)
                rec.Action = Skills.TrainedPerfection;

            if (WillActFail(craft, step, rec.Action))
                rec.Action = Skills.BasicSynthesis;

            return rec;
        }

        private static bool InTouchRotation(CraftState craft, StepState step)
            => step.PrevComboAction == Skills.BasicTouch && craft.StatLevel >= Simulator.MinLevel(Skills.StandardTouch) || step.PrevComboAction == Skills.StandardTouch && craft.StatLevel >= Simulator.MinLevel(Skills.AdvancedTouch);

        public Skills BestSynthesis(CraftState craft, StepState step, bool progOnly = false)
        {
            // Need to take into account MP
            // Rapid(500/50, 0)?
            // Intensive(400, 6) > Groundwork(300, 18) > Focused(200, 5) > Prudent(180, 18) > Careful(150, 7) > Groundwork(150, 18) > Basic(120, 0)

            var remainingProgress = craft.CraftProgress - step.Progress;
            if (CalculateNewProgress(craft, step, Skills.BasicSynthesis) >= craft.CraftProgress)
            {
                return Skills.BasicSynthesis;
            }

            if (Simulator.CanUseAction(craft, step, Skills.IntensiveSynthesis))
            {
                return Skills.IntensiveSynthesis;
            }

            if (!_qualityStarted && !progOnly)
            {
                if (CalculateNewProgress(craft, step, Skills.BasicSynthesis) >= craft.CraftProgress - Simulator.BaseProgress(craft))
                    return Skills.BasicSynthesis;
            }

            if (Simulator.CanUseAction(craft, step, Skills.Groundwork) && step.Durability > Simulator.GetDurabilityCost(step, Skills.Groundwork))
            {
                return Skills.Groundwork;
            }

            if (Simulator.CanUseAction(craft, step, Skills.PrudentSynthesis))
            {
                return Skills.PrudentSynthesis;
            }

            if (Simulator.CanUseAction(craft, step, Skills.CarefulSynthesis))
            {
                return Skills.CarefulSynthesis;
            }

            if (CanSpamBasicToComplete(craft, step))
            {
                return Skills.BasicSynthesis;
            }

            return Skills.BasicSynthesis;
        }

        private static bool CanSpamBasicToComplete(CraftState craft, StepState step)
        {
            while (true)
            {
                var (res, next) = Simulator.Execute(craft, step, Skills.BasicSynthesis, 0, 1);
                if (res == Simulator.ExecuteResult.CantUse)
                    return step.Progress >= craft.CraftProgress;
                step = next;
            }
        }

        public Recommendation GetRecommendation(CraftState craft, StepState step)
        {
            var fallbackRec = _fallback.Solve(craft, step);

            _manipulationUsed |= step.PrevComboAction == Skills.Manipulation;
            _trainedEyeUsed |= step.PrevComboAction == Skills.TrainedEye;
            _wasteNotUsed |= step.PrevComboAction is Skills.WasteNot or Skills.WasteNot2;
            _qualityStarted |= step.PrevComboAction is Skills.BasicTouch or Skills.StandardTouch or Skills.AdvancedTouch or Skills.HastyTouch or Skills.ByregotsBlessing or Skills.PrudentTouch
                or Skills.PreciseTouch or Skills.TrainedEye or Skills.PreparatoryTouch or Skills.TrainedFinesse or Skills.Innovation;
            _venereationUsed |= step.PrevComboAction == Skills.Veneration;
            _materialMiracleUsed |= step.PrevComboAction == Skills.MaterialMiracle && !P.Config.MaterialMiracleMulti;

            if (step.MaterialMiracleActive)
                return fallbackRec;

            if (P.Config.UseMaterialMiracle && !_materialMiracleUsed && Simulator.CanUseAction(craft, step, Skills.MaterialMiracle))
                return new(Skills.MaterialMiracle);

            bool inCombo = (step.PrevComboAction == Skills.BasicTouch && Simulator.CanUseAction(craft, step, Skills.StandardTouch)) || (step.PrevComboAction == Skills.StandardTouch && Simulator.CanUseAction(craft, step, Skills.AdvancedTouch));
            var act = BestSynthesis(craft, step);
            var goingForQuality = GoingForQuality(craft, step, out var maxQuality);

            if (step.Index == 1 && CanFinishCraft(craft, step, Skills.DelicateSynthesis) && CalculateNewQuality(craft, step, Skills.DelicateSynthesis) >= maxQuality && Simulator.CanUseAction(craft, step, Skills.DelicateSynthesis)) return new(Skills.DelicateSynthesis);
            if (!goingForQuality && CanFinishCraft(craft, step, act)) return new(act);

            if (Simulator.CanUseAction(craft, step, Skills.TrainedEye) && goingForQuality) return new(Skills.TrainedEye);
            if (Simulator.CanUseAction(craft, step, Skills.TricksOfTrade))
            {
                if (step.Index > 2 && (step.Condition == Condition.Good && P.Config.UseTricksGood || step.Condition == Condition.Excellent && P.Config.UseTricksExcellent))
                    return new(Skills.TricksOfTrade);

                if (step.RemainingCP < 7 ||
                    craft.StatLevel < Simulator.MinLevel(Skills.PreciseTouch) && step.Condition == Condition.Good && step.InnovationLeft == 0 && step.WasteNotLeft == 0 && !InTouchRotation(craft, step))
                    return new(Skills.TricksOfTrade);
            }

            if ((maxQuality == 0 || P.Config.MaxPercentage == 0) && !craft.CraftCollectible)
            {
                if (step.Index == 1 && Simulator.CanUseAction(craft, step, Skills.MuscleMemory)) return new(Skills.MuscleMemory);
                if (CanFinishCraft(craft, step, act)) return new(act);
                return new(act);
            }

            if (goingForQuality)
            {
                if (!P.Config.UseQualityStarter && craft.StatLevel >= Simulator.MinLevel(Skills.MuscleMemory))
                {
                    if (Simulator.CanUseAction(craft, step, Skills.MuscleMemory) && !CanFinishCraft(craft, step, Skills.MuscleMemory)) return new(Skills.MuscleMemory);

                    if (step.MuscleMemoryLeft > 0 && !CanFinishCraft(craft, step, Skills.BasicSynthesis))
                    {
                        if (craft.StatLevel < Simulator.MinLevel(Skills.IntensiveSynthesis) && step.Condition is Condition.Good or Condition.Excellent && Simulator.CanUseAction(craft, step, Skills.PreciseTouch)) return new(Skills.PreciseTouch);
                        if (Simulator.CanUseAction(craft, step, Skills.FinalAppraisal) && step.FinalAppraisalLeft == 0 && CanFinishCraft(craft, step, act)) return new(Skills.FinalAppraisal);
                        return new(act);
                    }

                    //if (!CanFinishCraft(craft, step, act) && step.VenerationLeft > 0 && step.Durability > 10)
                    //    return new(act);
                }

                if (P.Config.UseQualityStarter)
                {
                    if (Simulator.CanUseAction(craft, step, Skills.Reflect)) return new(Skills.Reflect);
                }

                if (Simulator.CanUseAction(craft, step, Skills.BasicTouch) && CalculateNewQuality(craft, step, Skills.BasicTouch) >= craft.CraftQualityMax && step.Index == 1)
                    return new(Skills.BasicTouch);

                if (Simulator.CanUseAction(craft, step, Skills.Manipulation) && step.ManipulationLeft == 0 && !_manipulationUsed) return new(Skills.Manipulation);

                if (step.Progress < craft.CraftProgress - 1 && (!_qualityStarted || !Simulator.CanUseAction(craft, step, Skills.FinalAppraisal)))
                {
                    bool canUseAct = step.Progress + Simulator.BaseProgress(craft) < craft.CraftProgress;
                    if (canUseAct)
                    {
                        bool shouldUseVeneration = CheckIfVenerationIsWorth(craft, step, act);

                        if (Simulator.CanUseAction(craft, step, Skills.Veneration) && step.VenerationLeft == 0 && shouldUseVeneration) return new(Skills.Veneration);
                        if (Simulator.CanUseAction(craft, step, Skills.WasteNot2) && step.WasteNotLeft == 0 && !_wasteNotUsed) return new(Skills.WasteNot2);
                        if (Simulator.CanUseAction(craft, step, Skills.WasteNot) && step.WasteNotLeft == 0 && !_wasteNotUsed) return new(Skills.WasteNot);
                        if (Simulator.CanUseAction(craft, step, Skills.FinalAppraisal) && step.FinalAppraisalLeft == 0 && CanFinishCraft(craft, step, act)) return new(Skills.FinalAppraisal, $"Synth is {act}");
                        if (!CanFinishCraft(craft, step, act))
                        return new(act);
                    }
                }

                if (Simulator.CanUseAction(craft, step, Skills.ByregotsBlessing) && !WillActFail(craft, step, Skills.ByregotsBlessing))
                {
                    var newQuality = CalculateNewQuality(craft, step, Skills.ByregotsBlessing);
                    var newHQPercent = maxQuality > 0 ? Calculations.GetHQChance(newQuality * 100.0 / maxQuality) : 100;
                    var newDone = craft.CraftQualityMin1 == 0 ? newHQPercent >= P.Config.MaxPercentage : newQuality >= maxQuality;
                    if (newDone) return new(Skills.ByregotsBlessing);
                }

                if (_wasteNotUsed && Simulator.CanUseAction(craft, step, Skills.PreciseTouch) && step.GreatStridesLeft == 0 && step.Condition is Condition.Good or Condition.Excellent && !WillActFail(craft, step, Skills.PreciseTouch)) return new(Skills.PreciseTouch);
                if (craft.StatLevel < Simulator.MinLevel(Skills.PreciseTouch) && step.GreatStridesLeft == 0 && step.Condition is Condition.Excellent)
                {
                    if (step.PrevComboAction == Skills.BasicTouch && Simulator.CanUseAction(craft, step, Skills.StandardTouch) && step.Durability - Simulator.GetDurabilityCost(step, Skills.StandardTouch) > 0) return new(Skills.StandardTouch);
                    if (Simulator.CanUseAction(craft, step, Skills.BasicTouch) && step.Durability - Simulator.GetDurabilityCost(step, Skills.BasicTouch) > 0) return new(Skills.BasicTouch);
                    if (Simulator.CanUseAction(craft, step, Skills.TricksOfTrade)) return new(Skills.TricksOfTrade);
                }
                if (step.InnovationLeft == 0 && Simulator.CanUseAction(craft, step, Skills.Innovation) && !inCombo && step.RemainingCP >= 36) return new(Skills.Innovation);
                if (!_wasteNotUsed && step.WasteNotLeft == 0 && Simulator.CanUseAction(craft, step, Skills.WasteNot2)) return new(Skills.WasteNot2);
                if (!_wasteNotUsed && step.WasteNotLeft == 0 && Simulator.CanUseAction(craft, step, Skills.WasteNot) && craft.StatLevel < Simulator.MinLevel(Skills.WasteNot2)) return new(Skills.WasteNot);
                if (Simulator.CanUseAction(craft, step, Skills.PrudentTouch) && step.Durability == 10) return new(Skills.PrudentTouch);
                if (step.GreatStridesLeft == 0 && Simulator.CanUseAction(craft, step, Skills.GreatStrides) && step.Condition != Condition.Excellent && step.RemainingCP >= Simulator.GetCPCost(step, Skills.GreatStrides) + Simulator.GetCPCost(step, Skills.ByregotsBlessing) && !WillActFail(craft, step, Skills.ByregotsBlessing))
                {
                    var newQuality = GreatStridesByregotCombo(craft, step);
                    var newHQPercent = maxQuality > 0 ? Calculations.GetHQChance(newQuality * 100.0 / maxQuality) : 100;
                    var newDone = craft.CraftQualityMin1 == 0 ? newHQPercent >= P.Config.MaxPercentage : newQuality >= maxQuality;
                    if (newDone) return new(Skills.GreatStrides, "GS Combo");
                }

                if (step.Condition == Condition.Poor && Simulator.CanUseAction(craft, step, Skills.CarefulObservation) && P.Config.UseSpecialist) return new(Skills.CarefulObservation);
                if (step.Condition == Condition.Poor && Simulator.CanUseAction(craft, step, Skills.Observe))
                {
                    if (step.InnovationLeft >= 2 && craft.StatLevel >= Simulator.MinLevel(Skills.AdvancedTouch))
                        return new(Skills.Observe);

                    if (!CanFinishCraft(craft, step, act))
                        return new(act);

                    return new(Skills.Observe);
                }
                if (step.GreatStridesLeft != 0 && Simulator.CanUseAction(craft, step, Skills.ByregotsBlessing) && !WillActFail(craft, step, Skills.ByregotsBlessing)) return new(Skills.ByregotsBlessing);
                if (step.HeartAndSoulAvailable && Simulator.CanUseAction(craft, step, Skills.HeartAndSoul) && P.Config.UseSpecialist) return new(Skills.HeartAndSoul);
                if (HighestLevelTouch(craft, step) is var touch && touch != Skills.None) return new(touch);
            }

            if (CanFinishCraft(craft, step, act))
                return new(act);

            if (Simulator.CanUseAction(craft, step, Skills.Veneration) && step.VenerationLeft == 0 && step.Condition != Condition.Excellent) return new(Skills.Veneration);
            return new(act);
        }

        private bool CheckIfVenerationIsWorth(CraftState craft, StepState step, Skills act)
        {
            if (step.Condition is Condition.Good or Condition.Excellent) return false;
            if (_venereationUsed) return false;
            if (step.FinalAppraisalLeft > 0) return false;  

            var (result, next) = Simulator.Execute(craft, step with { Durability = 40 }, act, 0, 1);
            if (next.Progress >= craft.CraftProgress) return false;
            var (result2, next2) = Simulator.Execute(craft, next with { Durability = 40 }, act, 0, 1);
            if (next2.Progress >= craft.CraftProgress) return false;
            //var (result3, next3) = Simulator.Execute(craft, next2 with { Durability = 40 }, act, 0, 1);
            //if (next3.Progress >= craft.CraftProgress) return false;

            return true;
        }

        private static bool WillActFail(CraftState craft, StepState step, Skills act)
        {
            bool result = step.Durability - Simulator.GetDurabilityCost(step, act) <= 0 && CalculateNewProgress(craft, step, act) < craft.CraftProgress;
            return result;
        }

        private static bool GoingForQuality(CraftState craft, StepState step, out int maxQuality)
        {
            bool wantMoreQuality;
            if (craft.CraftQualityMin1 == 0)
            {
                // normal craft
                maxQuality = craft.CraftQualityMax;
                wantMoreQuality = maxQuality > 0 && Calculations.GetHQChance(step.Quality * 100.0 / maxQuality) < P.Config.MaxPercentage;
            }
            else
            {
                // collectible
                maxQuality = P.Config.SolverCollectibleMode switch
                {
                    1 => craft.CraftQualityMin1,
                    2 => craft.CraftQualityMin2,
                    _ => craft.CraftQualityMin3,
                };
                wantMoreQuality = step.Quality < maxQuality;
            }

            return wantMoreQuality;
        }

        private bool ShouldMend(CraftState craft, StepState step,bool goingForQuality)
        {
            var synthOption = BestSynthesis(craft, step);
            var touchOption = HighestLevelTouch(craft, step);

            if (goingForQuality && _qualityStarted)
            {
                if (WillActFail(craft, step, touchOption)) return true;
            }
            else
            {
                if (WillActFail(craft, step, synthOption)) return true;
            }

            return false;
        }

        private static int GetComboDurability(CraftState craft, StepState step, params Skills[] comboskills)
        {
            int output = step.Durability;
            foreach (var skill in comboskills)
            {
                var (result, next) = Simulator.Execute(craft, step, skill, 1, 0);
                if (result == Simulator.ExecuteResult.CantUse)
                    continue;

                output = next.Durability;
                step = next;
            }

            return output;
        }
        private static bool CanCompleteTouchCombo(CraftState craft, StepState step)
        {
            int wasteStacks = step.WasteNotLeft;
            var innoStacks = step.InnovationLeft;

            if (craft.StatLevel < Simulator.MinLevel(Skills.StandardTouch))
            {
                return step.Durability > Simulator.GetDurabilityCost(step, Skills.BasicTouch);
            }
            else if (craft.StatLevel < Simulator.MinLevel(Skills.AdvancedTouch))
            {
                if (step.PrevComboAction == Skills.BasicTouch) return true; //Assume started
                if (step.RemainingCP < 36 || innoStacks < 2) return false;

                var copyofDura = step.Durability;
                for (int i = 1; i == 2; i++)
                {
                    copyofDura = wasteStacks > 0 ? copyofDura - 5 : copyofDura - 10;
                    wasteStacks--;
                }
                return copyofDura > 0;
            }
            else
            {
                if (step.PrevComboAction is Skills.BasicTouch or Skills.StandardTouch) return true; //Assume started
                if (step.RemainingCP < 54 || innoStacks < 3) return false;

                var copyofDura = step.Durability;
                for (int i = 1; i == 3; i++)
                {
                    copyofDura = wasteStacks > 0 ? copyofDura - 5 : copyofDura - 10;
                    wasteStacks--;
                }
                return copyofDura > 0;
            }
        }

        public static int CalculateNewProgress(CraftState craft, StepState step, Skills action) => step.FinalAppraisalLeft > 0 ? Math.Min(step.Progress + Simulator.CalculateProgress(craft, step, action), craft.CraftProgress -1) : step.Progress + Simulator.CalculateProgress(craft, step, action);
        public static int CalculateNewQuality(CraftState craft, StepState step, Skills action) => step.Quality + Simulator.CalculateQuality(craft, step, action);
        public static bool CanFinishCraft(CraftState craft, StepState step, Skills act) => CalculateNewProgress(craft, step, act) >= craft.CraftProgress;

        public static int GreatStridesByregotCombo(CraftState craft, StepState step)
        {
            if (!Simulator.CanUseAction(craft, step, Skills.ByregotsBlessing) || step.RemainingCP < 56)
                return 0;

            var (res, next) = Simulator.Execute(craft, step, Skills.GreatStrides, 0, 1);
            if (res != Simulator.ExecuteResult.Succeeded)
                return 0;

            return CalculateNewQuality(craft, next, Skills.ByregotsBlessing);
        }

        public static Skills HighestLevelTouch(CraftState craft, StepState step)
        {
            bool wasteNots = step.WasteNotLeft > 0;

            if (Simulator.CanUseAction(craft, step, Skills.AdvancedTouch) && step.PrevComboAction == Skills.Observe) return Skills.AdvancedTouch;
            if (Simulator.CanUseAction(craft, step, Skills.PreciseTouch) && Simulator.CanUseAction(craft, step, Skills.PreciseTouch)) return Skills.PreciseTouch;
            if (Simulator.CanUseAction(craft, step, Skills.PreparatoryTouch) && step.IQStacks < P.Config.MaxIQPrepTouch && step.InnovationLeft > 0) return Skills.PreparatoryTouch;
            if (Simulator.CanUseAction(craft, step, Skills.AdvancedTouch) && step.PrevComboAction == Skills.StandardTouch) return Skills.AdvancedTouch;
            if (Simulator.CanUseAction(craft, step, Skills.StandardTouch) && step.PrevComboAction == Skills.BasicTouch) return Skills.StandardTouch;
            if (Simulator.CanUseAction(craft, step, Skills.PrudentTouch) && GetComboDurability(craft, step, Skills.BasicTouch, Skills.StandardTouch, Skills.AdvancedTouch) <= 0) return Skills.PrudentTouch;
            if (Simulator.CanUseAction(craft, step, Skills.TrainedFinesse) && step.Durability <= 10) return Skills.TrainedFinesse;
            if (Simulator.CanUseAction(craft, step, Skills.BasicTouch)) return Skills.BasicTouch;
            if (Simulator.CanUseAction(craft, step, Skills.DaringTouch)) return Skills.DaringTouch;
            if (Simulator.CanUseAction(craft, step, Skills.HastyTouch)) return Skills.HastyTouch;

            return Skills.None;
        }

        public static Skills HighestLevelSynth(CraftState craft, StepState step)
        {
            if (Simulator.CanUseAction(craft, step, Skills.IntensiveSynthesis)) return Skills.IntensiveSynthesis;
            if (Simulator.CanUseAction(craft, step, Skills.Groundwork) && step.Durability > 20) return Skills.Groundwork;
            if (Simulator.CanUseAction(craft, step, Skills.PrudentSynthesis)) return Skills.PrudentSynthesis;
            if (Simulator.CanUseAction(craft, step, Skills.CarefulSynthesis)) return Skills.CarefulSynthesis;
            if (Simulator.CanUseAction(craft, step, Skills.BasicSynthesis)) return Skills.BasicSynthesis;

            return Skills.None;
        }
    }
}
