using Artisan.Autocraft;
using Artisan.RawInformation;
using ClickLib.Clicks;
using Dalamud.Utility.Signatures;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;

namespace Artisan.CraftingLogic
{
    public unsafe static class CurrentCraft
    {
        public static class Skills
        {
            public const uint
                BasicSynth = 100001,
                BasicTouch = 100002,
                MastersMend = 100003,
                HastyTouch = 100355,
                RapidSynthesis = 100363,
                Observe = 100010,
                Tricks = 100371,
                WasteNot = 4631,
                Veneration = 19297,
                StandardTouch = 100004,
                GreatStrides = 260,
                Innovation = 19004,
                FinalAppraisal = 19012,
                WasteNot2 = 4639,
                ByregotsBlessing = 100339,
                PreciseTouch = 100128,
                MuscleMemory = 100379,
                CarefulSynthesis = 100203,
                Manipulation = 4574,
                PrudentTouch = 100227,
                FocusedSynthesis = 100235,
                FocusedTouch = 100243,
                Reflect = 100387,
                PreparatoryTouch = 100299,
                Groundwork = 100403,
                DelicateSynthesis = 100323,
                IntensiveSynthesis = 100315,
                TrainedEye = 100283,
                AdvancedTouch = 100411,
                PrudentSynthesis = 100427,
                TrainedFinesse = 100435,
                CarefulObservation = 100395,
                HeartAndSoul = 100419;
        }

        public static class Buffs
        {
            public const ushort
                InnerQuiet = 251,
                Innovation = 2189,
                Veneration = 2226,
                GreatStrides = 254,
                Manipulation = 1164,
                WasteNot = 252,
                WasteNot2 = 257,
                FinalAppraisal = 2190,
                MuscleMemory = 2191;


        }

        public static class Multipliers
        {
            public const double
                BasicSynthesis = 1.2,
                RapidSynthesis = 5,
                MuscleMemory = 3,
                CarefulSynthesis = 1.5,
                FocusedSynthesis = 2,
                GroundWork = 3,
                DelicateSynthesis = 1,
                IntensiveSynthesis = 4,
                PrudentSynthesis = 1.8,
                BasicTouch = 1,
                HastyTouch = 1,
                StandardTouch = 1.25,
                PreciseTouch = 1.5,
                PrudentTouch = 1,
                FocusedTouch = 1.5,
                Reflect = 1,
                PrepatoryTouch = 2,
                AdvancedTouch = 1.5,
                TrainedFinesse = 1;


        }
        public static event EventHandler<int>? StepChanged;

        public static int CurrentDurability { get; set; } = 0;
        public static int MaxDurability { get; set; } = 0;
        public static int CurrentProgress { get; set; } = 0;
        public static int MaxProgress { get; set; } = 0;
        public static int CurrentQuality { get; set; } = 0;
        public static int MaxQuality { get; set; } = 0;
        public static int HighQualityPercentage { get; set; } = 0;
        public static string RecommendationName { get; set; }
        public static Condition CurrentCondition { get; set; }
        private static int currentStep = 0;
        private static int observeCounter;
        private static int quickSynthCurrent = 0;
        private static int quickSynthMax = 0;

        public static int CurrentStep
        {
            get { return currentStep; }
            set
            {
                if (currentStep != value)
                {
                    currentStep = value;
                    Dalamud.Logging.PluginLog.Debug($"Benchmark: {RecommendationName} = {Artisan.Benchmark.ElapsedMilliseconds}ms");
                    StepChanged?.Invoke(currentStep, value);
                }

            }
        }
        public static string? HQLiteral { get; set; }
        public static bool CanHQ { get; set; }
        public static string? CollectabilityLow { get; set; }
        public static string? CollectabilityMid { get; set; }
        public static string? CollectabilityHigh { get; set; }

        public static string? ItemName { get; set; }

        public static Recipe? Recipe { get; set; }

        public static uint CurrentRecommendation { get; set; }

        public static bool CraftingWindowOpen { get; set; } = false;

        public static bool JustUsedFinalAppraisal { get; set; } = false;
        public static bool JustUsedObserve { get; set; } = false;
        public static bool JustUsedGreatStrides { get; set; } = false;
        public static bool ManipulationUsed { get; set; } = false;
        public static bool WasteNotUsed { get; set; } = false;
        public static bool InnovationUsed { get; set; } = false;
        public static bool VenerationUsed { get; set; } = false;

        public static bool BasicTouchUsed { get; set; } = false;

        public static bool StandardTouchUsed { get; set; } = false;

        public static bool AdvancedTouchUsed { get; set; } = false;

        public static bool ExpertCraftOpenerFinish { get; set; } = false;

        public static int QuickSynthCurrent { get => quickSynthCurrent; set { if (value != 0 && quickSynthCurrent != value) { CraftingLists.CraftingListFunctions.CurrentIndex++; } quickSynthCurrent = value; } }
        public static int QuickSynthMax { get => quickSynthMax; set => quickSynthMax = value; }
        public static int MacroStep { get; set; } = 0;

        public unsafe static bool GetCraft()
        {
            try
            {
                var quickSynthPTR = Service.GameGui.GetAddonByName("SynthesisSimple", 1);
                if (quickSynthPTR != IntPtr.Zero)
                {
                    var quickSynthWindow = (AtkUnitBase*)quickSynthPTR;
                    if (quickSynthWindow != null)
                    {
                        try
                        {
                            var currentTextNode = (AtkTextNode*)quickSynthWindow->UldManager.NodeList[20];
                            var maxTextNode = (AtkTextNode*)quickSynthWindow->UldManager.NodeList[18];

                            QuickSynthCurrent = Convert.ToInt32(currentTextNode->NodeText.ToString());
                            QuickSynthMax = Convert.ToInt32(maxTextNode->NodeText.ToString());
                        }
                        catch
                        {

                        }
                        return true;
                    }
                }
                else
                {
                    QuickSynthCurrent = 0;
                    QuickSynthMax = 0;
                }

                IntPtr synthWindow = Service.GameGui.GetAddonByName("Synthesis", 1);
                if (synthWindow == IntPtr.Zero)
                {
                    CurrentStep = 0;
                    CharacterInfo.IsCrafting = false;
                    return false;
                }

                var craft = Marshal.PtrToStructure<AddonSynthesis>(synthWindow);
                if (craft.Equals(default(AddonSynthesis))) return false;
                if (craft.ItemName == null) { CraftingWindowOpen = false; return false; }

                CraftingWindowOpen = true;

                var cd = *craft.CurrentDurability;
                var md = *craft.StartingDurability;
                var mp = *craft.MaxProgress;
                var cp = *craft.CurrentProgress;
                var cq = *craft.CurrentQuality;
                var mq = *craft.MaxQuality;
                var hqp = *craft.HQPercentage;
                var cond = *craft.Condition;
                var cs = *craft.StepNumber;
                var hql = *craft.HQLiteral;
                var collectLow = *craft.CollectabilityLow;
                var collectMid = *craft.CollectabilityMid;
                var collectHigh = *craft.CollectabilityHigh;
                var item = *craft.ItemName;

                CharacterInfo.IsCrafting = true;
                CurrentDurability = Convert.ToInt32(cd.NodeText.ToString());
                MaxDurability = Convert.ToInt32(md.NodeText.ToString());
                CurrentProgress = Convert.ToInt32(cp.NodeText.ToString());
                MaxProgress = Convert.ToInt32(mp.NodeText.ToString());
                CurrentQuality = Convert.ToInt32(cq.NodeText.ToString());
                MaxQuality = Convert.ToInt32(mq.NodeText.ToString());
                ItemName = item.NodeText.ExtractText();
                //ItemName = ItemName.Remove(ItemName.Length - 10, 10);
                if (ItemName[^1] == '')
                {
                    ItemName = ItemName.Remove(ItemName.Length - 1, 1).Trim();
                }
                var sheetItem = LuminaSheets.RecipeSheet?.Values.Where(x => x.ItemResult.Value.Name!.RawString.Equals(ItemName)).FirstOrDefault();

                if (sheetItem != null)
                {
                    Recipe = sheetItem;

                    if (sheetItem.CanHq)
                    {
                        CanHQ = true;
                        HighQualityPercentage = Convert.ToInt32(hqp.NodeText.ToString());
                    }
                    else
                    {
                        CanHQ = false;
                        HighQualityPercentage = 0;
                    }
                }


                CurrentCondition = Condition.Unknown;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[229].Text.RawString) CurrentCondition = Condition.Poor;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[227].Text.RawString) CurrentCondition = Condition.Good;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[226].Text.RawString) CurrentCondition = Condition.Normal;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[228].Text.RawString) CurrentCondition = Condition.Excellent;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[239].Text.RawString) CurrentCondition = Condition.Centered;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[240].Text.RawString) CurrentCondition = Condition.Sturdy;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[241].Text.RawString) CurrentCondition = Condition.Pliant;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[13455].Text.RawString) CurrentCondition = Condition.Malleable;
                if (cond.NodeText.ToString() == LuminaSheets.AddonSheet[13454].Text.RawString) CurrentCondition = Condition.Primed;

                CurrentStep = Convert.ToInt32(cs.NodeText.ToString());
                HQLiteral = hql.NodeText.ToString();
                CollectabilityLow = collectLow.NodeText.ToString();
                CollectabilityMid = collectMid.NodeText.ToString();
                CollectabilityHigh = collectHigh.NodeText.ToString();

                return true;


            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, ex.StackTrace);
                return false;
            }
        }

        public static double BaseQuality(Recipe recipe = null)
        {
            try
            {
                if (recipe == null)
                {
                    if (Recipe != null)
                        recipe = Recipe;
                    else
                        return 0;
                }

                var baseValue = CharacterInfo.Control() * 10 / recipe.RecipeLevelTable.Value.QualityDivider + 35;
                if (CharacterInfo.CharacterLevel() <= recipe.RecipeLevelTable.Value.ClassJobLevel)
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

        public static double BaseProgression()
        {
            try
            {
                if (CraftingWindowOpen)
                {
                    var baseValue = CharacterInfo.Craftsmanship() * 10 / Recipe.RecipeLevelTable.Value.ProgressDivider + 2;
                    var p2 = baseValue;
                    if (CharacterInfo.CharacterLevel() <= Recipe.RecipeLevelTable.Value.ClassJobLevel)
                    {
                        p2 = baseValue * Recipe.RecipeLevelTable.Value.ProgressModifier / 100;
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

        public static double GetMultiplier(uint id, bool isQuality = false)
        {
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

            if (id == Skills.Groundwork && CharacterInfo.CharacterLevel() >= 86)
                baseMultiplier = 3.6;
            if (id == Skills.CarefulSynthesis && CharacterInfo.CharacterLevel() >= 82)
                baseMultiplier = 1.8;
            if (id == Skills.RapidSynthesis && CharacterInfo.CharacterLevel() >= 63)
                baseMultiplier = 5;
            if (id == Skills.BasicSynth && CharacterInfo.CharacterLevel() >= 31)
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

        public static uint CalculateNewQuality(uint id)
        {
            double efficiency = GetMultiplier(id, true);
            double IQStacks = GetStatus(Buffs.InnerQuiet) is null ? 1 : 1 + (GetStatus(Buffs.InnerQuiet).StackCount * 0.1);
            double innovation = GetStatus(Buffs.Innovation) is not null ? 0.5 : 0;
            double greatStrides = GetStatus(Buffs.GreatStrides) is not null ? 1 : 0;

            return (uint)Math.Floor(CurrentQuality + (BaseQuality() * efficiency * IQStacks * (innovation + greatStrides + 1)));

        }

        public static uint CalculateNewProgress(uint id)
        {
            var multiplier = GetMultiplier(id, false);
            double veneration = GetStatus(Buffs.Veneration) != null ? 0.5 : 0;
            double muscleMemory = GetStatus(Buffs.MuscleMemory) != null ? 1 : 0;
            return (uint)Math.Floor(CurrentProgress + (BaseProgression() * multiplier * (veneration + muscleMemory + 1)));

        }

        public static uint GreatStridesByregotCombo()
        {
            if (GetStatus(Buffs.InnerQuiet) is null) return 0;

            double efficiency = GetMultiplier(Skills.ByregotsBlessing);
            double IQStacks = 1 + (GetStatus(Buffs.InnerQuiet).StackCount * 0.1);
            double innovation = (GetStatus(Buffs.Innovation)?.StackCount >= 2 && CurrentCondition != Condition.Excellent) || (GetStatus(Buffs.Innovation)?.StackCount >= 3 && CurrentCondition == Condition.Excellent) || (JustUsedGreatStrides && GetStatus(Buffs.Innovation)?.StackCount >= 1) ? 0.5 : 0;
            double greatStrides = 1;

            return (uint)Math.Floor(CurrentQuality + (BaseQuality() * efficiency * IQStacks * (innovation + greatStrides + 1)));
        }

        public static double ByregotMultiplier()
        {
            int IQStacks = Convert.ToInt32(GetStatus(Buffs.InnerQuiet)?.StackCount);
            return 1 + (IQStacks * 0.2);
        }

        public static uint GetExpertRecommendation()
        {
            if (CurrentDurability <= 10 && CanUse(Skills.MastersMend)) return Skills.MastersMend;
            if (CurrentProgress < MaxProgress && !ExpertCraftOpenerFinish)
            {
                if (CurrentStep > 1 && GetStatus(Buffs.MuscleMemory) is null && CanUse(Skills.Innovation)) { ExpertCraftOpenerFinish = true; return 0; }
                if (CanUse(Skills.MuscleMemory)) return Skills.MuscleMemory;
                if (CurrentStep == 2 && CanUse(Skills.Veneration)) return Skills.Veneration;
                if (GetStatus(Buffs.WasteNot2) is null && CanUse(Skills.WasteNot2)) return Skills.WasteNot2;
                if (CurrentCondition is Condition.Good && CanUse(Skills.IntensiveSynthesis)) return Skills.IntensiveSynthesis;
                if (CurrentCondition is Condition.Centered && CanUse(Skills.RapidSynthesis)) return Skills.RapidSynthesis;
                if (CurrentCondition is Condition.Sturdy or Condition.Primed or Condition.Normal && CanUse(Skills.Groundwork)) return Skills.Groundwork;
                if (CurrentCondition is Condition.Malleable && CalculateNewProgress(Skills.Groundwork) >= MaxProgress && !JustUsedFinalAppraisal && CanUse(Skills.FinalAppraisal)) return Skills.FinalAppraisal;
                if (CurrentCondition is Condition.Malleable && (CalculateNewProgress(Skills.Groundwork) < MaxProgress) || GetStatus(Buffs.FinalAppraisal) is not null && CanUse(Skills.Groundwork)) return Skills.Groundwork;
                if (CurrentCondition is Condition.Pliant && GetStatus(Buffs.Manipulation) is null && GetStatus(Buffs.MuscleMemory) is null && CanUse(Skills.Manipulation)) return Skills.Manipulation;
            }
            if (CurrentQuality < MaxQuality)
            {
                if (GreatStridesByregotCombo() >= MaxQuality && GetStatus(Buffs.GreatStrides) is null && CanUse(Skills.GreatStrides)) return Skills.GreatStrides;
                if (GetStatus(Buffs.GreatStrides) is not null && CanUse(Skills.ByregotsBlessing)) return Skills.ByregotsBlessing;
                if (CurrentCondition == Condition.Pliant && GetStatus(Buffs.WasteNot2) is null && CanUse(Skills.WasteNot2)) return Skills.WasteNot2;
                if (CanUse(Skills.Manipulation) && (GetStatus(Buffs.Manipulation) is null || (GetStatus(Buffs.Manipulation)?.StackCount <= 3 && CurrentCondition == Condition.Pliant))) return Skills.Manipulation;
                if (CurrentCondition == Condition.Pliant && CurrentDurability < MaxDurability - 20 && CanUse(Skills.MastersMend)) return Skills.MastersMend;
                if (CanUse(Skills.Innovation) && GetStatus(Buffs.Innovation) is null) return Skills.Innovation;
                if (CharacterInfo.HighestLevelTouch() == 0) return CharacterInfo.HighestLevelSynth();
                return CharacterInfo.HighestLevelTouch();
            }

            if (CanUse(Skills.CarefulSynthesis)) return Skills.CarefulSynthesis;
            return CharacterInfo.HighestLevelSynth();
        }
        public static uint GetRecommendation()
        {
            if (CanFinishCraft()) return CharacterInfo.HighestLevelSynth();

            if (CanUse(Skills.TrainedEye) && (HighQualityPercentage < Service.Configuration.MaxPercentage || Recipe.ItemResult.Value.IsCollectable) && Recipe.CanHq) return Skills.TrainedEye;
            if (CanUse(Skills.Tricks) && CurrentStep > 2 && ((CurrentCondition == Condition.Good && Service.Configuration.UseTricksGood) || (CurrentCondition == Condition.Excellent && Service.Configuration.UseTricksExcellent))) return Skills.Tricks;

            if (MaxQuality == 0 || Service.Configuration.MaxPercentage == 0 || !Recipe.CanHq)
            {
                if (CurrentStep == 1 && CanUse(Skills.MuscleMemory)) return Skills.MuscleMemory;
                if (CalculateNewProgress(CharacterInfo.HighestLevelSynth()) >= MaxProgress) return CharacterInfo.HighestLevelSynth();
                if (GetStatus(Buffs.Veneration) == null && CanUse(Skills.Veneration)) return Skills.Veneration;
            }

            if (CurrentDurability <= 10 && CanUse(Skills.MastersMend)) return Skills.MastersMend;

            if (CharacterInfo.CurrentCP > 0)
            {
                if (MaxDurability >= 60)
                {
                    if (CurrentQuality < MaxQuality && (HighQualityPercentage < Service.Configuration.MaxPercentage || Recipe.ItemResult.Value.IsCollectable || Recipe.IsExpert))
                    {
                        if (CurrentStep == 1 && CanUse(Skills.MuscleMemory) && CalculateNewProgress(Skills.MuscleMemory) < MaxProgress) return Skills.MuscleMemory;
                        if (CurrentStep == 2 && CanUse(Skills.FinalAppraisal) && !JustUsedFinalAppraisal && CalculateNewProgress(CharacterInfo.HighestLevelSynth()) >= MaxProgress) return Skills.FinalAppraisal;
                        if (GetStatus(Buffs.MuscleMemory) != null) return CharacterInfo.HighestLevelSynth();
                        if (!ManipulationUsed && GetStatus(Buffs.Manipulation) is null && CanUse(Skills.Manipulation)) return Skills.Manipulation;
                        if (!WasteNotUsed && GetStatus(Buffs.WasteNot2) is null && CanUse(Skills.WasteNot2)) return Skills.WasteNot2;
                        if (GetStatus(Buffs.Innovation) is null && CanUse(Skills.Innovation)) return Skills.Innovation;
                        if (GreatStridesByregotCombo() >= MaxQuality && GetStatus(Buffs.GreatStrides) is null && CanUse(Skills.GreatStrides)) return Skills.GreatStrides;
                        if (CurrentCondition == Condition.Poor && CanUse(Skills.CarefulObservation) && Service.Configuration.UseSpecialist) return Skills.CarefulObservation;
                        if (CurrentCondition == Condition.Poor && CanUse(Skills.Observe)) return Skills.Observe;
                        if (GetStatus(Buffs.GreatStrides) is not null && CanUse(Skills.ByregotsBlessing)) return Skills.ByregotsBlessing;
                        //if (PredictFailureTouch(CharacterInfo.HighestLevelTouch())) return CharacterInfo.HighestLevelSynth();
                        if (CharacterInfo.HighestLevelTouch() != 0) return CharacterInfo.HighestLevelTouch();

                    }
                }

                if (MaxDurability >= 35 && MaxDurability < 60)
                {
                    if (CurrentQuality < MaxQuality && (HighQualityPercentage < Service.Configuration.MaxPercentage || Recipe.ItemResult.Value.IsCollectable || Recipe.IsExpert))
                    {
                        if (CurrentStep == 1 && CanUse(Skills.Reflect)) return Skills.Reflect;
                        if (!ManipulationUsed && GetStatus(Buffs.Manipulation) is null && CanUse(Skills.Manipulation)) return Skills.Manipulation;
                        if (!WasteNotUsed && CanUse(Skills.WasteNot2)) return Skills.WasteNot2;
                        if (!InnovationUsed && CanUse(Skills.Innovation)) return Skills.Innovation;
                        if (GreatStridesByregotCombo() >= MaxQuality && GetStatus(Buffs.GreatStrides) is null && CanUse(Skills.GreatStrides)) return Skills.GreatStrides;
                        if (CurrentCondition == Condition.Poor && CanUse(Skills.CarefulObservation) && Service.Configuration.UseSpecialist) return Skills.CarefulObservation;
                        if (CurrentCondition == Condition.Poor && CanUse(Skills.Observe)) return Skills.Observe;
                        if (GetStatus(Buffs.GreatStrides) is not null && CanUse(Skills.ByregotsBlessing)) return Skills.ByregotsBlessing;
                        //if (PredictFailureTouch(CharacterInfo.HighestLevelTouch())) return CharacterInfo.HighestLevelSynth();
                        if (CharacterInfo.HighestLevelTouch() != 0) return CharacterInfo.HighestLevelTouch();
                    }
                }
            }

            if (CanUse(Skills.Veneration) && GetStatus(Buffs.Veneration) == null && !VenerationUsed) return Skills.Veneration;
            return CharacterInfo.HighestLevelSynth();
        }

        private static bool CanFinishCraft()
        {
            var metMaxProg = CurrentQuality >= MaxQuality;
            var usingPercentage = HighQualityPercentage >= Service.Configuration.MaxPercentage && !Recipe.ItemResult.Value.IsCollectable && !Recipe.IsExpert;
            return CalculateNewProgress(CharacterInfo.HighestLevelSynth()) >= MaxProgress && (metMaxProg || usingPercentage);
        }

        private static bool PredictFailureSynth(uint highestLevelSynth)
        {
            if (Service.Configuration.DisableFailurePrediction) return false;

            int durabilityDegrade = 10;

            if (highestLevelSynth == Skills.Groundwork) durabilityDegrade *= 2;
            if (GetStatus(Buffs.WasteNot) != null || GetStatus(Buffs.WasteNot2) != null) durabilityDegrade /= 2;
            if (GetStatus(Buffs.Manipulation) != null) durabilityDegrade += 5;

            int estimatedSynths = EstimateSynths(highestLevelSynth);
            int estimatedDegrade = estimatedSynths * durabilityDegrade;

            return (CurrentDurability - estimatedDegrade) <= 0;
        }

        private static bool PredictFailureTouch(uint highestLevelTouch)
        {
            if (Service.Configuration.DisableFailurePrediction) return false;

            int durabilityDegrade = 10;

            if (highestLevelTouch == Skills.PreparatoryTouch) durabilityDegrade *= 2;
            if (GetStatus(Buffs.WasteNot) != null || GetStatus(Buffs.WasteNot2) != null) durabilityDegrade /= 2;
            if (GetStatus(Buffs.Manipulation) != null) durabilityDegrade += 5;
            var newDegrade = CurrentDurability - durabilityDegrade;

            return newDegrade <= 0;
        }

        private static int EstimateSynths(uint highestLevelSynth)
        {
            var baseProg = (int)Math.Floor(BaseProgression() * GetMultiplier(highestLevelSynth));
            int counter = 0;
            var currentProg = CurrentProgress;
            while (currentProg < MaxProgress)
            {
                currentProg += baseProg;
                counter++;
            }

            return counter;
        }


        public static void RepeatTrialCraft()
        {
            try
            {
                var recipeWindow = Service.GameGui.GetAddonByName("RecipeNote", 1);
                if (recipeWindow == IntPtr.Zero)
                    return;

                var addonPtr = (AddonRecipeNote*)recipeWindow;
                if (addonPtr == null)
                    return;

                var synthButton = addonPtr->TrialSynthesisButton;

                if (synthButton != null && !synthButton->IsEnabled)
                {
                    synthButton->AtkComponentBase.OwnerNode->AtkResNode.Flags ^= 1 << 5;
                }
                else
                {
                    return;
                }

                ClickRecipeNote.Using(recipeWindow).TrialSynthesis();
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "RepeatTrialCraft");
            }
        }

        public static void QuickSynthItem(int crafts)
        {
            try
            {
                var recipeWindow = Service.GameGui.GetAddonByName("RecipeNote", 1);
                if (recipeWindow == IntPtr.Zero)
                    return;

                var addonPtr = (AddonRecipeNote*)recipeWindow;
                if (addonPtr == null)
                    return;
                var synthButton = addonPtr->SynthesizeButton;

                if (synthButton != null && !synthButton->IsEnabled)
                {
                    synthButton->AtkComponentBase.OwnerNode->AtkResNode.Flags ^= 1 << 5;
                }

                try
                {
                    if (Throttler.Throttle(500))
                    {
                        ClickRecipeNote.Using(recipeWindow).QuickSynthesis();

                        var quickSynthPTR = Service.GameGui.GetAddonByName("SynthesisSimpleDialog", 1);
                        if (quickSynthPTR == IntPtr.Zero)
                            return;

                        var quickSynthWindow = (AtkUnitBase*)quickSynthPTR;
                        if (quickSynthWindow == null)
                            return;

                        var values = stackalloc AtkValue[2];
                        values[0] = new()
                        {
                            Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int,
                            Int = crafts,
                        };
                        values[1] = new()
                        {
                            Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Bool,
                            Byte = 1,
                        };

                        quickSynthWindow->FireCallback(3, values);

                        //var qsynthButton = (AtkComponentButton*)quickSynthWindow->UldManager.NodeList[3];
                        //if (qsynthButton != null && !qsynthButton->IsEnabled)
                        //{
                        //    qsynthButton->AtkComponentBase.OwnerNode->AtkResNode.Flags ^= 1 << 5;
                        //}

                        //var checkboxNode = (AtkComponentNode*)quickSynthWindow->UldManager.NodeList[5];
                        //if (checkboxNode == null)
                        //    return;
                        //var checkboxComponent = (AtkComponentCheckBox*)checkboxNode->Component;
                        //if (!checkboxComponent->IsChecked)
                        //{

                        //    //checkboxComponent->AtkComponentButton.Flags ^= 0x40000;

                        //    //AtkResNode* checkmarkNode = checkboxComponent->AtkComponentButton.ButtonBGNode->PrevSiblingNode;

                        //    //checkmarkNode->Color.A = (byte)(true ? 0xFF : 0x7F);
                        //    //checkmarkNode->Flags ^= 0x10;
                        //}

                        //var numericInput = (AtkComponentNode*)quickSynthWindow->UldManager.NodeList[4];
                        //if (numericInput == null)
                        //    return;
                        //var numericComponent = (AtkComponentNumericInput*)numericInput->Component;

                        //if (crafts <= numericComponent->Data.Max)
                        //{
                        //    numericComponent->SetValue(numericComponent->Data.Max);
                        //}
                        //AtkResNodeFunctions.ClickButton(quickSynthWindow, qsynthButton, 1);
                    }


                }
                catch (Exception e)
                {
                    e.Log();
                }

            }
            catch (Exception ex)
            {
                ex.Log();
            }
        }

        public static void CloseQuickSynthWindow()
        {
            try
            {
                if (Throttler.Throttle(500))
                {
                    var quickSynthPTR = Service.GameGui.GetAddonByName("SynthesisSimple", 1);
                    if (quickSynthPTR == IntPtr.Zero)
                        return;

                    var quickSynthWindow = (AtkUnitBase*)quickSynthPTR;
                    if (quickSynthWindow == null)
                        return;

                    var qsynthButton = (AtkComponentButton*)quickSynthWindow->UldManager.NodeList[2];
                    if (qsynthButton != null && !qsynthButton->IsEnabled)
                    {
                        qsynthButton->AtkComponentBase.OwnerNode->AtkResNode.Flags ^= 1 << 5;
                    }
                    AtkResNodeFunctions.ClickButton(quickSynthWindow, qsynthButton, 0);
                }

            }
            catch (Exception e)
            {
                e.Log();
            }
        }

        public static void RepeatActualCraft()
        {
            try
            {
                var recipeWindow = Service.GameGui.GetAddonByName("RecipeNote", 1);
                if (recipeWindow == IntPtr.Zero)
                    return;

                var addonPtr = (AddonRecipeNote*)recipeWindow;
                if (addonPtr == null)
                    return;
                var synthButton = addonPtr->SynthesizeButton;

                if (synthButton != null && !synthButton->IsEnabled)
                {
                    Dalamud.Logging.PluginLog.Verbose("AddonRecipeNote: Enabling synth button");
                    synthButton->AtkComponentBase.OwnerNode->AtkResNode.Flags ^= 1 << 5;
                }

                if (Throttler.Throttle(500))
                {
                    try
                    {
                        Dalamud.Logging.PluginLog.Verbose("AddonRecipeNote: Selecting synth");
                        ClickRecipeNote.Using(recipeWindow).Synthesize();
                    }
                    catch (Exception e)
                    {
                        e.Log();
                    }
                }
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "RepeatActualCraft");
            }
        }
        internal static Dalamud.Game.ClientState.Statuses.Status? GetStatus(uint statusID)
        {
            if (Service.ClientState.LocalPlayer is null) return null;

            foreach (var status in Service.ClientState.LocalPlayer?.StatusList)
            {
                if (status.StatusId == statusID)
                    return status;
            }

            return null;
        }

        internal static int GetResourceCost(uint actionID)
        {
            if (actionID < 100000)
            {
                var cost = LuminaSheets.ActionSheet[actionID].PrimaryCostValue;
                return cost;
            }
            else
            {
                var cost = LuminaSheets.CraftActions[actionID].Cost;
                return cost;

            }
        }

        internal unsafe static uint CanUse2(uint id)
        {
            ActionManager* actionManager = ActionManager.Instance();
            if (actionManager == null)
                return 0;

            if (LuminaSheets.ActionSheet.TryGetValue(id, out var act1))
            {
                var canUse = actionManager->GetActionStatus(ActionType.Spell, id);
                return canUse;
            }
            if (LuminaSheets.CraftActions.TryGetValue(id, out var act2))
            {
                var canUse = actionManager->GetActionStatus(ActionType.CraftAction, id);
                return canUse;
            }

            return 0;
        }

        internal static bool CanUse(uint id)
        {
            if (LuminaSheets.ActionSheet.TryGetValue(id, out var act1))
            {
                string skillName = act1.Name;
                var allOfSameName = LuminaSheets.ActionSheet.Where(x => x.Value.Name == skillName).Select(x => x.Key);
                foreach (var dupe in allOfSameName)
                {
                    if (CanUse2(dupe) == 0) return true;
                }
                return false;
            }

            if (LuminaSheets.CraftActions.TryGetValue(id, out var act2))
            {
                string skillName = act2.Name;
                var allOfSameName = LuminaSheets.CraftActions.Where(x => x.Value.Name == skillName).Select(x => x.Key);
                foreach (var dupe in allOfSameName)
                {
                    if (CanUse2(dupe) == 0) return true;
                }
                return false;
            }

            return false;
        }

        public enum Condition
        {
            Poor,
            Normal,
            Good,
            Excellent,
            Centered,
            Sturdy,
            Pliant,
            Malleable,
            Primed,
            Unknown
        }
    }
}
