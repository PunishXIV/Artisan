using System.Collections.Generic;
using Skills = Artisan.RawInformation.Character.Skills;
using Condition = Artisan.CraftingLogic.CraftData.Condition;

namespace Artisan.CraftingLogic.Solvers
{
    public class StandardSolverDefinition : ISolverDefinition
    {
        public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
        {
            if (craft.CraftExpert)
                yield return new(this, 1, 1, "Standard expert solver");
            else
                yield return new(this, 0, 1, "Standard normal solver");
        }

        public Solver Create(CraftState craft, int flavour) => new StandardSolver(flavour != 0);
    }

    public class StandardSolver : Solver
    {
        private bool _expert;

        // for normal crafts, we don't ever want to use manip/wn more than once
        private bool _manipulationUsed;
        private bool _wasteNotUsed;

        public StandardSolver(bool expert)
        {
            _expert = expert;
        }

        public override Recommendation Solve(CraftState craft, StepState step) => _expert ? GetExpertRecommendation(craft, step) : GetRecommendation(craft, step);

        private static bool InTouchRotation(CraftState craft, StepState step)
            => step.PrevComboAction == Skills.BasicTouch && craft.StatLevel >= Simulator.MinLevel(Skills.StandardTouch) || step.PrevComboAction == Skills.StandardTouch && craft.StatLevel >= Simulator.MinLevel(Skills.AdvancedTouch);

        public static Skills BestSynthesis(CraftState craft, StepState step)
        {
            // Need to take into account MP
            // Rapid(500/50, 0)?
            // Intensive(400, 6) > Groundwork(300, 18) > Focused(200, 5) > Prudent(180, 18) > Careful(150, 7) > Groundwork(150, 18) > Basic(120, 0)

            var remainingProgress = craft.CraftProgress - step.Progress;
            if (Simulator.CanUseAction(craft, step, Skills.DelicateSynthesis) && Simulator.CalculateProgress(craft, step, Skills.DelicateSynthesis) >= remainingProgress)
            {
                return Skills.DelicateSynthesis;
            }

            if (Simulator.CalculateProgress(craft, step, Skills.BasicSynthesis) >= remainingProgress)
            {
                return Skills.BasicSynthesis;
            }

            if (Simulator.CanUseAction(craft, step, Skills.IntensiveSynthesis))
            {
                return Skills.IntensiveSynthesis;
            }

            // Only use Groundwork to speed up if it'll complete or if under muscle memory
            if (Simulator.CanUseAction(craft, step, Skills.Groundwork) && (Simulator.CalculateProgress(craft, step, Skills.Groundwork) >= remainingProgress || step.MuscleMemoryLeft > 0))
            {
                return Skills.Groundwork;
            }

            if (step.PrevComboAction == Skills.Observe && Simulator.CanUseAction(craft, step, Skills.FocusedSynthesis))
            {
                return Skills.FocusedSynthesis;
            }

            if (Simulator.CanUseAction(craft, step, Skills.PrudentSynthesis) && step.RemainingCP < 88) // TODO: what's up with this cp condition?
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

            //if (Simulator.CanUseAction(craft, step, Skills.RapidSynthesis) && step.RemainingCP < 18)
            //{
            //    return Skills.RapidSynthesis;
            //}

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

        public Recommendation GetExpertRecommendation(CraftState craft, StepState step)
        {
            var goingForQuality = GoingForQuality(craft, step, out var maxQuality);

            if (step.Durability <= 10 && Simulator.CanUseAction(craft, step, Skills.MastersMend)) return new(Skills.MastersMend);
            if (step.Index == 1 || step.MuscleMemoryLeft != 0 || !Simulator.CanUseAction(craft, step, Skills.Innovation))
            {
                if (Simulator.CanUseAction(craft, step, Skills.MuscleMemory)) return new(Skills.MuscleMemory);
                if (step.Index == 2 && Simulator.CanUseAction(craft, step, Skills.Veneration)) return new(Skills.Veneration);
                if (step.WasteNotLeft == 0 && Simulator.CanUseAction(craft, step, Skills.WasteNot2)) return new(Skills.WasteNot2);
                if (step.Condition is Condition.Good && Simulator.CanUseAction(craft, step, Skills.IntensiveSynthesis)) return new(Skills.IntensiveSynthesis);
                if (step.Condition is Condition.Centered && Simulator.CanUseAction(craft, step, Skills.RapidSynthesis)) return new(Skills.RapidSynthesis);
                if (step.Condition is Condition.Sturdy or Condition.Primed or Condition.Normal && Simulator.CanUseAction(craft, step, Skills.Groundwork)) return new(Skills.Groundwork);
                if (step.Condition is Condition.Malleable && Simulator.WillFinishCraft(craft, step, Skills.Groundwork) && Simulator.CanUseAction(craft, step, Skills.FinalAppraisal)) return new(Skills.FinalAppraisal);
                if (step.Condition is Condition.Malleable && !Simulator.WillFinishCraft(craft, step, Skills.Groundwork) && Simulator.CanUseAction(craft, step, Skills.Groundwork)) return new(Skills.Groundwork);
                if (step.Condition is Condition.Pliant && step.ManipulationLeft == 0 && step.MuscleMemoryLeft == 0 && Simulator.CanUseAction(craft, step, Skills.Manipulation)) return new(Skills.Manipulation);
            }
            if (goingForQuality)
            {
                if (GreatStridesByregotCombo(craft, step) >= maxQuality && step.GreatStridesLeft == 0 && Simulator.CanUseAction(craft, step, Skills.GreatStrides)) return new(Skills.GreatStrides);
                if (step.GreatStridesLeft > 0 && Simulator.CanUseAction(craft, step, Skills.ByregotsBlessing)) return new(Skills.ByregotsBlessing);
                if (step.Condition == Condition.Pliant && step.WasteNotLeft == 0 && Simulator.CanUseAction(craft, step, Skills.WasteNot2)) return new(Skills.WasteNot2);
                if (Simulator.CanUseAction(craft, step, Skills.Manipulation) && (step.ManipulationLeft == 0 || step.ManipulationLeft <= 3 && step.Condition == Condition.Pliant)) return new(Skills.Manipulation);
                if (step.Condition == Condition.Pliant && step.Durability < craft.CraftDurability - 20 && Simulator.CanUseAction(craft, step, Skills.MastersMend)) return new(Skills.MastersMend);
                if (Simulator.CanUseAction(craft, step, Skills.Innovation) && step.InnovationLeft == 0) return new(Skills.Innovation);
                var touch = HighestLevelTouch(craft, step);
                return new(touch != Skills.None ? touch : HighestLevelSynth(craft, step));
            }

            if (Simulator.CanUseAction(craft, step, Skills.CarefulSynthesis)) return new(Skills.CarefulSynthesis);
            return new(HighestLevelSynth(craft, step));
        }

        public Recommendation GetRecommendation(CraftState craft, StepState step)
        {
            _manipulationUsed |= step.PrevComboAction == Skills.Manipulation;
            _wasteNotUsed |= step.PrevComboAction is Skills.WasteNot or Skills.WasteNot2;

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

            if (ShouldMend(craft, step, act, goingForQuality) && Simulator.CanUseAction(craft, step, Skills.MastersMend)) return new(Skills.MastersMend);

            if ((maxQuality == 0 || P.Config.MaxPercentage == 0) && !craft.CraftCollectible)
            {
                if (step.Index == 1 && Simulator.CanUseAction(craft, step, Skills.MuscleMemory)) return new(Skills.MuscleMemory);
                if (CanFinishCraft(craft, step, act)) return new(act);
                if (step.VenerationLeft == 0 && Simulator.CanUseAction(craft, step, Skills.Veneration)) return new(Skills.Veneration);
                return new(act);
            }

            if (goingForQuality)
            {
                if (!P.Config.UseQualityStarter && craft.StatLevel >= Simulator.MinLevel(Skills.MuscleMemory))
                {
                    if (Simulator.CanUseAction(craft, step, Skills.MuscleMemory) && !CanFinishCraft(craft, step, Skills.MuscleMemory)) return new(Skills.MuscleMemory);

                    if (step.MuscleMemoryLeft > 0)
                    {
                        if (craft.StatLevel < Simulator.MinLevel(Skills.IntensiveSynthesis) && step.Condition is Condition.Good or Condition.Excellent && Simulator.CanUseAction(craft, step, Skills.PreciseTouch)) return new(Skills.PreciseTouch);
                        if (step.VenerationLeft == 0 && Simulator.CanUseAction(craft, step, Skills.Veneration) && !CanFinishCraft(craft, step, act)) return new(Skills.Veneration);
                        if (Simulator.CanUseAction(craft, step, Skills.FinalAppraisal) && step.FinalAppraisalLeft == 0 && CanFinishCraft(craft, step, act)) return new(Skills.FinalAppraisal);
                        return new(act);
                    }

                    if (!CanFinishCraft(craft, step, act) && step.VenerationLeft > 0 && step.Durability > 10)
                        return new(act);
                }

                if (P.Config.UseQualityStarter)
                {
                    if (Simulator.CanUseAction(craft, step, Skills.Reflect)) return new(Skills.Reflect);
                }

                if (Simulator.CanUseAction(craft, step, Skills.ByregotsBlessing) && step.Durability > Simulator.GetDurabilityCost(step, Skills.ByregotsBlessing))
                {
                    var newQuality = CalculateNewQuality(craft, step, Skills.ByregotsBlessing);
                    var newHQPercent = maxQuality > 0 ? Calculations.GetHQChance(newQuality * 100.0 / maxQuality) : 100;
                    var newDone = craft.CraftQualityMin1 == 0 ? newHQPercent >= P.Config.MaxPercentage : newQuality >= maxQuality;
                    if (newDone) return new(Skills.ByregotsBlessing);
                }

                if (_wasteNotUsed && Simulator.CanUseAction(craft, step, Skills.PreciseTouch) && step.GreatStridesLeft == 0 && step.Condition is Condition.Good or Condition.Excellent) return new(Skills.PreciseTouch);
                if (craft.StatLevel < Simulator.MinLevel(Skills.PreciseTouch) && step.GreatStridesLeft == 0 && step.Condition is Condition.Excellent)
                {
                    if (step.PrevComboAction == Skills.BasicTouch && Simulator.CanUseAction(craft, step, Skills.StandardTouch)) return new(Skills.StandardTouch);
                    if (Simulator.CanUseAction(craft, step, Skills.BasicTouch)) return new(Skills.BasicTouch);
                }
                if (!_manipulationUsed && step.ManipulationLeft == 0 && Simulator.CanUseAction(craft, step, Skills.Manipulation) && step.Durability < craft.CraftDurability && !InTouchRotation(craft, step)) return new(Skills.Manipulation);
                if (!_wasteNotUsed && step.WasteNotLeft == 0 && Simulator.CanUseAction(craft, step, Skills.WasteNot2)) return new(Skills.WasteNot2);
                if (!_wasteNotUsed && step.WasteNotLeft == 0 && Simulator.CanUseAction(craft, step, Skills.WasteNot) && craft.StatLevel < Simulator.MinLevel(Skills.WasteNot2)) return new(Skills.WasteNot);
                if (Simulator.CanUseAction(craft, step, Skills.PrudentTouch) && step.Durability == 10) return new(Skills.PrudentTouch);
                if (step.InnovationLeft == 0 && Simulator.CanUseAction(craft, step, Skills.Innovation) && !InTouchRotation(craft, step) && step.RemainingCP >= 36) return new(Skills.Innovation);
                if (step.GreatStridesLeft == 0 && Simulator.CanUseAction(craft, step, Skills.GreatStrides) && step.Condition != Condition.Excellent)
                {
                    var newQuality = GreatStridesByregotCombo(craft, step);
                    var newHQPercent = maxQuality > 0 ? Calculations.GetHQChance(newQuality * 100.0 / maxQuality) : 100;
                    var newDone = craft.CraftQualityMin1 == 0 ? newHQPercent >= P.Config.MaxPercentage : newQuality >= maxQuality;
                    if (newDone) return new(Skills.GreatStrides);
                }

                if (step.Condition == Condition.Poor && Simulator.CanUseAction(craft, step, Skills.CarefulObservation) && P.Config.UseSpecialist) return new(Skills.CarefulObservation);
                if (step.Condition == Condition.Poor && Simulator.CanUseAction(craft, step, Skills.Observe))
                {
                    if (step.InnovationLeft >= 2 && craft.StatLevel >= Simulator.MinLevel(Skills.FocusedTouch))
                        return new(Skills.Observe);

                    if (!CanFinishCraft(craft, step, act))
                        return new(act);

                    return new(Skills.Observe);
                }
                if (step.GreatStridesLeft != 0 && Simulator.CanUseAction(craft, step, Skills.ByregotsBlessing)) return new(Skills.ByregotsBlessing);
                if (step.PrevComboAction == Skills.Observe && Simulator.CanUseAction(craft, step, Skills.FocusedTouch)) return new(Skills.FocusedTouch);
                if (CanCompleteTouchCombo(craft, step))
                {
                    if (step.PrevComboAction == Skills.BasicTouch && Simulator.CanUseAction(craft, step, Skills.StandardTouch)) return new(Skills.StandardTouch);
                    if (step.PrevComboAction == Skills.StandardTouch && Simulator.CanUseAction(craft, step, Skills.AdvancedTouch)) return new(Skills.AdvancedTouch);
                    if (Simulator.CanUseAction(craft, step, Skills.BasicTouch)) return new(Skills.BasicTouch);
                }
                if (HighestLevelTouch(craft, step) is var touch && touch != Skills.None) return new(touch);
            }

            if (CanFinishCraft(craft, step, act))
                return new(act);

            if (Simulator.CanUseAction(craft, step, Skills.Veneration) && step.VenerationLeft == 0 && step.Condition != Condition.Excellent) return new(Skills.Veneration);
            return new(act);
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

            return wantMoreQuality && step.RemainingCP > P.Config.PriorityProgress;
        }

        private bool ShouldMend(CraftState craft, StepState step, Skills synthOption, bool goingForQuality)
        {
            bool wasteNots = step.WasteNotLeft > 0;
            var nextReduction = wasteNots ? 5 : 10;

            int advancedDegrade = 30 - 5 * step.WasteNotLeft;
            if (goingForQuality)
            {
                if (!_manipulationUsed && Simulator.CanUseAction(craft, step, Skills.Manipulation)) return false;
                if (!_wasteNotUsed && Simulator.CanUseAction(craft, step, Skills.WasteNot)) return false;

                if (Simulator.CanUseAction(craft, step, Skills.PrudentTouch) && step.Durability == 10) return false;
                if (craft.StatLevel < Simulator.MinLevel(Skills.AdvancedTouch) && craft.StatLevel >= Simulator.MinLevel(Skills.StandardTouch) && step.Durability <= 20 && craft.CraftDurability >= 50 && step.Condition != Condition.Excellent) return true;
                if (craft.StatLevel >= Simulator.MinLevel(Skills.AdvancedTouch) && step.Durability <= advancedDegrade && !InTouchRotation(craft, step) && craft.CraftDurability >= 50 && step.Condition != Condition.Excellent) return true;
            }
            else
            {
                if (synthOption == Skills.Groundwork) nextReduction = wasteNots ? 10 : 20;
            }

            if (step.Durability - nextReduction <= 0) return true;
            return false;
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

        public static int CalculateNewProgress(CraftState craft, StepState step, Skills action) => step.Progress + Simulator.CalculateProgress(craft, step, action);
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

            if (craft.CraftExpert)
            {
                if (Simulator.CanUseAction(craft, step, Skills.HastyTouch) && step.Condition is Condition.Centered) return Skills.HastyTouch;
                if (Simulator.CanUseAction(craft, step, Skills.PreciseTouch) && step.Condition is Condition.Good) return Skills.PreciseTouch;
                if (step.PrevComboAction == Skills.AdvancedTouch && Simulator.CanUseAction(craft, step, Skills.PrudentTouch)) return Skills.PrudentTouch;
                if (Simulator.CanUseAction(craft, step, Skills.AdvancedTouch) && step.PrevComboAction == Skills.StandardTouch) return Skills.AdvancedTouch;
                if (Simulator.CanUseAction(craft, step, Skills.StandardTouch) && step.PrevComboAction == Skills.BasicTouch) return Skills.StandardTouch;
                if (Simulator.CanUseAction(craft, step, Skills.BasicTouch)) return Skills.BasicTouch;
            }
            else
            {
                if (Simulator.CanUseAction(craft, step, Skills.FocusedTouch) && step.PrevComboAction == Skills.Observe) return Skills.FocusedTouch;
                if (Simulator.CanUseAction(craft, step, Skills.PreciseTouch) && step.Condition is Condition.Good or Condition.Excellent) return Skills.PreciseTouch;
                if (Simulator.CanUseAction(craft, step, Skills.PreparatoryTouch) && step.Durability > (wasteNots ? 10 : 20) && step.IQStacks < 10) return Skills.PreparatoryTouch;
                if (Simulator.CanUseAction(craft, step, Skills.PrudentTouch) && !wasteNots) return Skills.PrudentTouch;
                if (Simulator.CanUseAction(craft, step, Skills.BasicTouch)) return Skills.BasicTouch;
            }

            return Skills.None;
        }

        public static Skills HighestLevelSynth(CraftState craft, StepState step)
        {
            if (Simulator.CanUseAction(craft, step, Skills.IntensiveSynthesis)) return Skills.IntensiveSynthesis;
            if (Simulator.CanUseAction(craft, step, Skills.FocusedSynthesis) && step.PrevComboAction == Skills.Observe) return Skills.FocusedSynthesis;
            if (Simulator.CanUseAction(craft, step, Skills.Groundwork) && step.Durability > 20) return Skills.Groundwork;
            if (Simulator.CanUseAction(craft, step, Skills.PrudentSynthesis)) return Skills.PrudentSynthesis;
            if (Simulator.CanUseAction(craft, step, Skills.CarefulSynthesis)) return Skills.CarefulSynthesis;
            if (Simulator.CanUseAction(craft, step, Skills.BasicSynthesis)) return Skills.BasicSynthesis;

            return Skills.None;
        }
    }
}
