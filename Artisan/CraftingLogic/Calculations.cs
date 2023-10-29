using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using static Artisan.CraftingLogic.CurrentCraft;
using Condition = Artisan.CraftingLogic.CraftData.Condition;

namespace Artisan.CraftingLogic
{
    internal static class Calculations
    {
        public static double BaseProgression()
        {
            try
            {
                if (CraftingWindowOpen)
                {
                    var baseValue = CharacterInfo.Craftsmanship * 10 / CurrentRecipe.RecipeLevelTable.Value.ProgressDivider + 2;
                    var p2 = baseValue;
                    if (CharacterInfo.CharacterLevel <= CurrentRecipe.RecipeLevelTable.Value.ClassJobLevel)
                    {
                        p2 = baseValue * CurrentRecipe.RecipeLevelTable.Value.ProgressModifier / 100;
                    }

                    return p2;
                }
                return 0;
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "BaseProgression");
                return 0;
            }
        }

        public static double BaseQuality(Recipe? recipe = null)
        {
            try
            {
                if (recipe == null)
                {
                    if (CurrentRecipe != null)
                        recipe = CurrentRecipe;
                    else
                        return 0;
                }

                var baseValue = CharacterInfo.Control * 10 / recipe.RecipeLevelTable.Value.QualityDivider + 35;
                if (CharacterInfo.CharacterLevel <= recipe.RecipeLevelTable.Value.ClassJobLevel)
                {
                    return baseValue * recipe.RecipeLevelTable.Value.QualityModifier * 0.01;
                }

                return baseValue;
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "BaseQuality");
                return 0;
            }
        }

        public static double ByregotMultiplier()
        {
            int IQStacks = Convert.ToInt32(SolverLogic.GetStatus(Buffs.InnerQuiet)?.StackCount);
            return 1 + (IQStacks * 0.2);
        }

        public unsafe static uint CalculateNewProgress(uint id)
        {
            //var multiplier = GetMultiplier(id, false);
            //double veneration = GetStatus(Buffs.Veneration) != null ? 0.5 : 0;
            //double muscleMemory = GetStatus(Buffs.MuscleMemory) != null ? 1 : 0;

            var agentCraftActionSimulator = AgentModule.Instance()->GetAgentCraftActionSimulator();
            var acts = agentCraftActionSimulator->Progress;
            uint baseIncrease = id switch
            {
                Skills.BasicSynth => acts->BasicSynthesis.ProgressIncrease,
                Skills.RapidSynthesis => acts->RapidSynthesis.ProgressIncrease,
                Skills.MuscleMemory => acts->MuscleMemory.ProgressIncrease,
                Skills.CarefulSynthesis => acts->CarefulSynthesis.ProgressIncrease,
                Skills.FocusedSynthesis => acts->FocusedSynthesis.ProgressIncrease,
                Skills.Groundwork => acts->Groundwork.ProgressIncrease,
                Skills.DelicateSynthesis => acts->DelicateSynthesis.ProgressIncrease,
                Skills.IntensiveSynthesis => acts->IntensiveSynthesis.ProgressIncrease,
                Skills.PrudentSynthesis => acts->PrudentSynthesis.ProgressIncrease,
                _ => 0
            };

            return (uint)CurrentProgress + baseIncrease;

            //return (uint)Math.Floor(CurrentProgress + (BaseProgression() * multiplier * (veneration + muscleMemory + 1)));

        }

        public unsafe static uint CalculateNewQuality(uint id)
        {
            //double efficiency = GetMultiplier(id, true);
            //double IQStacks = GetStatus(Buffs.InnerQuiet) is null ? 1 : 1 + (GetStatus(Buffs.InnerQuiet).StackCount * 0.1);
            //double innovation = GetStatus(Buffs.Innovation) is not null ? 0.5 : 0;
            //double greatStrides = GetStatus(Buffs.GreatStrides) is not null ? 1 : 0;

            var agentCraftActionSimulator = AgentModule.Instance()->GetAgentCraftActionSimulator();
            var acts = agentCraftActionSimulator->Quality;
            uint baseIncrease = id switch
            {
                Skills.BasicTouch => acts->BasicTouch.QualityIncrease,
                Skills.HastyTouch => acts->HastyTouch.QualityIncrease,
                Skills.StandardTouch => acts->StandardTouch.QualityIncrease,
                Skills.ByregotsBlessing => acts->ByregotsBlessing.QualityIncrease,
                Skills.PreciseTouch => acts->PreciseTouch.QualityIncrease,
                Skills.PrudentTouch => acts->PrudentTouch.QualityIncrease,
                Skills.FocusedTouch => acts->FocusedTouch.QualityIncrease,
                Skills.Reflect => acts->Reflect.QualityIncrease,
                Skills.PreparatoryTouch => acts->PreparatoryTouch.QualityIncrease,
                Skills.DelicateSynthesis => acts->DelicateSynthesis.QualityIncrease,
                Skills.TrainedEye => acts->TrainedEye.QualityIncrease,
                Skills.AdvancedTouch => acts->AdvancedTouch.QualityIncrease,
                Skills.TrainedFinesse => acts->TrainedFinesse.QualityIncrease,
                _ => 0
            };

            return (uint)CurrentQuality + baseIncrease;

            //return (uint)Math.Floor(CurrentQuality + (BaseQuality() * efficiency * IQStacks * (innovation + greatStrides + 1)));

        }

        public static double GetMultiplier(uint id, bool isQuality = false)
        {
            if (id == 0) return 1;

            if (id < 100000)
            {
                var newAct = LuminaSheets.ActionSheet.Values.Where(x => x.Name.RawString == id.NameOfAction() && x.ClassJob.Row == 8).FirstOrDefault();
                id = newAct.RowId;
            }
            else
            {
                var newAct = LuminaSheets.CraftActions.Values.Where(x => x.Name.RawString == id.NameOfAction() && x.ClassJob.Row == CharacterInfo.JobID).FirstOrDefault();
                id = newAct.CRP.Row;
            }


            double baseMultiplier = id switch
            {
                Skills.BasicSynth => Multipliers.BasicSynthesis,
                Skills.RapidSynthesis => Multipliers.RapidSynthesis,
                Skills.MuscleMemory => Multipliers.MuscleMemory,
                Skills.CarefulSynthesis => Multipliers.CarefulSynthesis,
                Skills.FocusedSynthesis => Multipliers.FocusedSynthesis,
                Skills.Groundwork => Multipliers.GroundWork,
                Skills.DelicateSynthesis => Multipliers.DelicateSynthesis,
                Skills.IntensiveSynthesis => Multipliers.IntensiveSynthesis,
                Skills.PrudentSynthesis => Multipliers.PrudentSynthesis,
                Skills.BasicTouch => Multipliers.BasicTouch,
                Skills.HastyTouch => Multipliers.HastyTouch,
                Skills.StandardTouch => Multipliers.StandardTouch,
                Skills.PreciseTouch => Multipliers.PreciseTouch,
                Skills.PrudentTouch => Multipliers.PrudentTouch,
                Skills.FocusedTouch => Multipliers.FocusedTouch,
                Skills.Reflect => Multipliers.Reflect,
                Skills.PreparatoryTouch => Multipliers.PrepatoryTouch,
                Skills.AdvancedTouch => Multipliers.AdvancedTouch,
                Skills.TrainedFinesse => Multipliers.TrainedFinesse,
                Skills.ByregotsBlessing => ByregotMultiplier(),
                _ => 1
            };

            if (id == Skills.Groundwork && CharacterInfo.CharacterLevel >= 86)
                baseMultiplier = 3.6;
            if (id == Skills.CarefulSynthesis && CharacterInfo.CharacterLevel >= 82)
                baseMultiplier = 1.8;
            if (id == Skills.RapidSynthesis && CharacterInfo.CharacterLevel >= 63)
                baseMultiplier = 5;
            if (id == Skills.BasicSynth && CharacterInfo.CharacterLevel >= 31)
                baseMultiplier = 1.2;

            if (!isQuality)
            {

                if (CurrentCondition == Condition.Malleable)
                    return baseMultiplier * 1.5;

                return baseMultiplier;
            }



            var conditionMod = CurrentCondition switch
            {
                Condition.Poor => 0.5,
                Condition.Normal => 1,
                Condition.Good => 1.5,
                Condition.Excellent => 4,
                Condition.Unknown => 1,
                _ => 1
            };

            return conditionMod * baseMultiplier;
        }

        public static uint GreatStridesByregotCombo()
        {
            if (SolverLogic.GetStatus(Buffs.InnerQuiet) is null) return 0;

            if (!Skills.ByregotsBlessing.LevelChecked()) return 0;
            if (CharacterInfo.CurrentCP < 56) return 0;

            double efficiency = GetMultiplier(Skills.ByregotsBlessing);
            double IQStacks = 1 + (SolverLogic.GetStatus(Buffs.InnerQuiet).StackCount * 0.1);
            double innovation = (SolverLogic.GetStatus(Buffs.Innovation)?.StackCount >= 2 && CurrentCondition != Condition.Excellent) || (SolverLogic.GetStatus(Buffs.Innovation)?.StackCount >= 3 && CurrentCondition == Condition.Excellent) || (JustUsedGreatStrides && SolverLogic.GetStatus(Buffs.Innovation)?.StackCount >= 1) ? 0.5 : 0;
            double greatStrides = 1;

            return (uint)Math.Floor(CurrentQuality + (BaseQuality() * efficiency * IQStacks * (innovation + greatStrides + 1)));
        }
    }
}