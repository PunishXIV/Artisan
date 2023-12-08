using Artisan.Autocraft;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using ClickLib.Clicks;
using Dalamud.Game.ClientState.Statuses;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using static Artisan.CraftingLogic.CurrentCraft;
using Condition = Artisan.CraftingLogic.CraftData.Condition;
using Status = Dalamud.Game.ClientState.Statuses.Status;

namespace Artisan.CraftingLogic
{
    public static class SolverLogic
    {
        private static StatusList statusList => Svc.ClientState.LocalPlayer!.StatusList;
        private static bool InTouchRotation => (PreviousAction == Skills.BasicTouch && Skills.StandardTouch.LevelChecked()) || (PreviousAction == Skills.StandardTouch && Skills.AdvancedTouch.LevelChecked());

        private static bool goingForQuality = true;

        public static unsafe bool BestSynthesis(out Skills action, bool allowComplete = true)
        {
            var lastActionId = PreviousAction;
            var agentCraftActionSimulator = AgentModule.Instance()->GetAgentCraftActionSimulator();
            var remainingProgress = (uint)RemainingProgress();
            var progressActions = agentCraftActionSimulator->Progress;

            // Need to take into account MP
            // Rapid(500/50, 0)?
            // Intensive(400, 6) > Groundwork(300, 18) > Focused(200, 5) > Prudent(180, 18) > Careful(150, 7) > Groundwork(150, 18) > Basic(120, 0)

            if (progressActions->DelicateSynthesis.CanComplete(remainingProgress) == true)
            {
                action = Skills.DelicateSynthesis;
                return true;
            }

            if (Calculations.CalculateNewProgress(Skills.BasicSynthesis) >= MaxProgress)
            {
                action = Skills.BasicSynthesis;
                return true;
            }

            if (CanUse(Skills.IntensiveSynthesis))
            {
                action = Skills.IntensiveSynthesis;
                return progressActions->IntensiveSynthesis.CanComplete(remainingProgress)!.Value;
            }

            // Only use Groundwork to speed up if it'll complete or if under muscle memory
            if (Skills.Groundwork.LevelChecked() && (progressActions->Groundwork.CanComplete(remainingProgress) == true || statusList.HasStatus(out int _, CraftingPlayerStatuses.MuscleMemory)))
            {
                action = Skills.Groundwork;
                return true;
            }

            if (PreviousAction == Skills.Observe && progressActions->FocusedSynthesis.IsAvailable())
            {
                action = Skills.FocusedSynthesis;
                return progressActions->FocusedSynthesis.CanComplete(remainingProgress)!.Value;
            }

            if (progressActions->PrudentSynthesis.IsAvailable() && CharacterInfo.CurrentCP < 88)
            {
                action = Skills.PrudentSynthesis;
                return progressActions->PrudentSynthesis.CanComplete(remainingProgress)!.Value;
            }

            if (progressActions->CarefulSynthesis.IsAvailable())
            {
                action = Skills.CarefulSynthesis;
                return progressActions->CarefulSynthesis.CanComplete(remainingProgress)!.Value;
            }


            if (CanSpamBasicToComplete())
            {
                action = Skills.BasicSynthesis;
                return progressActions->BasicSynthesis.CanComplete(remainingProgress)!.Value;
            }

            if (progressActions->RapidSynthesis.IsAvailable() && CharacterInfo.CurrentCP < 18)
            {
                action = Skills.RapidSynthesis;
                return progressActions->RapidSynthesis.CanComplete(remainingProgress)!.Value;
            }

            action = Skills.BasicSynthesis;
            return progressActions->BasicSynthesis.CanComplete(remainingProgress)!.Value;
        }

        private unsafe static bool CanSpamBasicToComplete()
        {
            var multiplier = Calculations.GetMultiplier(Skills.BasicSynthesis);
            statusList.HasStatus(out int venestacks, CraftingPlayerStatuses.Veneration);
            statusList.HasStatus(out int wasteStacks, CraftingPlayerStatuses.WasteNot, CraftingPlayerStatuses.WasteNot2);
            statusList.HasStatus(out int manipStacks, CraftingPlayerStatuses.Manipulation);
            var copyOfDura = CurrentDurability;
            int newProgress = CurrentProgress;

            while (copyOfDura > 0)
            {
                newProgress = (int)Math.Floor(newProgress + (Calculations.BaseProgression() * multiplier * (venestacks > 0 ? 1.5 : 1)));
                if (newProgress >= MaxProgress) return true;

                copyOfDura -= wasteStacks > 0 ? 5 : 10;
                if (copyOfDura <= 0) return false;

                copyOfDura += manipStacks > 0 ? 5 : 0;

                manipStacks--;
                wasteStacks--;
                venestacks--;

            }


            return false;

        }

        public unsafe static Skills GetExpertRecommendation()
        {
            if (P.Config.ExpertSolverConfig.Enabled)
            {
                var weapon = LuminaSheets.ItemSheet?.GetValueOrDefault(InventoryManager.Instance()->GetInventorySlot(InventoryType.EquippedItems, 0)->ItemID);
                var lt = CurrentRecipe?.RecipeLevelTable.Value;
                var craft = new ExpertSolver.CraftState()
                {
                    StatCraftsmanship = (int)CharacterInfo.Craftsmanship,
                    StatControl = (int)CharacterInfo.Control,
                    StatCP = (int)CharacterInfo.MaxCP,
                    StatLevel = CharacterInfo.CharacterLevel ?? 0,
                    Specialist = InventoryManager.Instance()->GetInventorySlot(InventoryType.EquippedItems, 13)->ItemID != 0, // specialist == job crystal equipped
                    Splendorous = weapon?.LevelEquip == 90 && weapon?.Rarity >= 4,
                    CraftExpert = CurrentRecipe?.IsExpert ?? false,
                    CraftLevel = lt?.ClassJobLevel ?? 0,
                    CraftDurability = MaxDurability,
                    CraftProgress = MaxProgress,
                    CraftProgressDivider = lt?.ProgressDivider ?? 180,
                    CraftProgressModifier = lt?.ProgressModifier ?? 100,
                    CraftQualityDivider = lt?.QualityDivider ?? 180,
                    CraftQualityModifier = lt?.QualityModifier ?? 180,
                    CraftQualityMax = MaxQuality,
                    CraftQualityMin1 = Convert.ToInt32(CollectabilityLow) *10,
                    CraftQualityMin2 = Convert.ToInt32(CollectabilityMid) * 10,
                    CraftQualityMin3 = Convert.ToInt32(CollectabilityHigh) * 10,
                };

                if (craft.CraftQualityMin2 < craft.CraftQualityMin1)
                    craft.CraftQualityMin2 = craft.CraftQualityMin1;
                if (craft.CraftQualityMin3 < craft.CraftQualityMin2)
                    craft.CraftQualityMin3 = craft.CraftQualityMin2;
                var step = new ExpertSolver.StepState()
                {
                    Index = CurrentStep,
                    Progress = CurrentProgress,
                    Quality = CurrentQuality,
                    Durability = CurrentDurability,
                    RemainingCP = (int)CharacterInfo.CurrentCP,
                    Condition = CurrentCondition,
                    IQStacks = GetStatus(Buffs.InnerQuiet)?.Param ?? 0,
                    WasteNotLeft = GetStatus(Buffs.WasteNot2)?.Param ?? GetStatus(Buffs.WasteNot)?.Param ?? 0,
                    ManipulationLeft = GetStatus(Buffs.Manipulation)?.Param ?? 0,
                    GreatStridesLeft = GetStatus(Buffs.GreatStrides)?.Param ?? 0,
                    InnovationLeft = GetStatus(Buffs.Innovation)?.Param ?? 0,
                    VenerationLeft = GetStatus(Buffs.Veneration)?.Param ?? 0,
                    MuscleMemoryLeft = GetStatus(Buffs.MuscleMemory)?.Param ?? 0,
                    FinalAppraisalLeft = GetStatus(Buffs.FinalAppraisal)?.Param ?? 0,
                    CarefulObservationLeft = CanUse(Skills.CarefulObservation) ? 1 : 0,
                    HeartAndSoulActive = GetStatus(Buffs.HeartAndSoul) != null,
                    HeartAndSoulAvailable = CanUse(Skills.HeartAndSoul),
                    PrevComboAction = PreviousAction,
                };
                var result = ExpertSolver.Solver.SolveNextStep(P.Config.ExpertSolverConfig, craft, step);
                Svc.Log.Verbose($"{result.Item2}");
                return result.Item1;
            }

            GoingForQuality();

            if (CurrentDurability <= 10 && CanUse(Skills.MastersMend)) return Skills.MastersMend;
            if (CurrentProgress < MaxProgress && !ExpertCraftOpenerFinish)
            {
                if (CurrentStep > 1 && GetStatus(Buffs.MuscleMemory) is null && CanUse(Skills.Innovation)) { ExpertCraftOpenerFinish = true; return GetExpertRecommendation(); }
                if (CanUse(Skills.MuscleMemory)) return Skills.MuscleMemory;
                if (CurrentStep == 2 && CanUse(Skills.Veneration)) return Skills.Veneration;
                if (GetStatus(Buffs.WasteNot2) is null && CanUse(Skills.WasteNot2)) return Skills.WasteNot2;
                if (CurrentCondition is Condition.Good && CanUse(Skills.IntensiveSynthesis)) return Skills.IntensiveSynthesis;
                if (CurrentCondition is Condition.Centered && CanUse(Skills.RapidSynthesis)) return Skills.RapidSynthesis;
                if (CurrentCondition is Condition.Sturdy or Condition.Primed or Condition.Normal && CanUse(Skills.Groundwork)) return Skills.Groundwork;
                if (CurrentCondition is Condition.Malleable && Calculations.CalculateNewProgress(Skills.Groundwork) >= MaxProgress && !JustUsedFinalAppraisal && CanUse(Skills.FinalAppraisal)) return Skills.FinalAppraisal;
                if (CurrentCondition is Condition.Malleable && (Calculations.CalculateNewProgress(Skills.Groundwork) < MaxProgress) || GetStatus(Buffs.FinalAppraisal) is not null && CanUse(Skills.Groundwork)) return Skills.Groundwork;
                if (CurrentCondition is Condition.Pliant && GetStatus(Buffs.Manipulation) is null && GetStatus(Buffs.MuscleMemory) is null && CanUse(Skills.Manipulation)) return Skills.Manipulation;
            }
            if (goingForQuality)
            {
                if (Calculations.GreatStridesByregotCombo() >= MaxQuality && GetStatus(Buffs.GreatStrides) is null && CanUse(Skills.GreatStrides)) return Skills.GreatStrides;
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
        public static Skills GetRecommendation()
        {
            BestSynthesis(out var act);
            GoingForQuality();

            if (CurrentStep == 1 && Calculations.CalculateNewProgress(Skills.DelicateSynthesis) >= MaxProgress && Calculations.CalculateNewQuality(Skills.DelicateSynthesis) >= MaxQuality && CanUse(Skills.DelicateSynthesis)) return Skills.DelicateSynthesis;
            if (CanFinishCraft(act)) return act;

            if (Skills.TrainedEye.LevelChecked() && CanUse(Skills.TrainedEye) && (HighQualityPercentage < P.Config.MaxPercentage || CurrentRecipe.ItemResult.Value.AlwaysCollectable) && CurrentRecipe.CanHq) return Skills.TrainedEye;
            if (CanUse(Skills.TricksOfTrade))
            {
                if (CurrentStep > 2 && ((CurrentCondition == Condition.Good && P.Config.UseTricksGood) || (CurrentCondition == Condition.Excellent && P.Config.UseTricksExcellent)))
                    return Skills.TricksOfTrade;

                if ((CharacterInfo.CurrentCP < 7) ||
                    (!Skills.PreciseTouch.LevelChecked() && CurrentCondition == Condition.Good && GetStatus(Buffs.Innovation) is null && !statusList.HasStatus(out _, CraftingPlayerStatuses.WasteNot2, CraftingPlayerStatuses.WasteNot) &&
                    !InTouchRotation))
                    return Skills.TricksOfTrade;
            }

            if (ShouldMend(act) && CanUse(Skills.MastersMend)) return Skills.MastersMend;


            if (MaxQuality == 0 || P.Config.MaxPercentage == 0 || !CurrentRecipe.CanHq)
            {
                if (CurrentStep == 1 && CanUse(Skills.MuscleMemory)) return Skills.MuscleMemory;
                if (Calculations.CalculateNewProgress(act) >= MaxProgress) return act;
                if (GetStatus(Buffs.Veneration) == null && CanUse(Skills.Veneration)) return Skills.Veneration;
                return act;
            }

            if (goingForQuality)
            {
                if (!P.Config.UseQualityStarter && Skills.MuscleMemory.LevelChecked())
                {
                    if (CanUse(Skills.MuscleMemory) && Calculations.CalculateNewProgress(Skills.MuscleMemory) < MaxProgress) return Skills.MuscleMemory;

                    if (GetStatus(Buffs.MuscleMemory) != null)
                    {
                        if (!Skills.IntensiveSynthesis.LevelChecked() && (CurrentCondition == Condition.Good || CurrentCondition == Condition.Excellent) && CanUse(Skills.PreciseTouch)) return Skills.PreciseTouch;
                        if (GetStatus(Buffs.Veneration) == null && CanUse(Skills.Veneration) && Calculations.CalculateNewProgress(act) < MaxProgress && GetStatus(Buffs.MuscleMemory) != null) return Skills.Veneration;
                        if (CanUse(Skills.FinalAppraisal) && GetStatus(Buffs.FinalAppraisal) == null && Calculations.CalculateNewProgress(act) >= MaxProgress) return Skills.FinalAppraisal;
                        return act;
                    }

                    if (Calculations.CalculateNewProgress(act) < MaxProgress && statusList.HasStatus(out _, CraftingPlayerStatuses.Veneration) && CurrentDurability > 10)
                        return act;
                }

                if (P.Config.UseQualityStarter)
                {
                    if (CurrentStep == 1 && CanUse(Skills.Reflect)) return Skills.Reflect;
                }
 
                if (CanUse(Skills.ByregotsBlessing) && ((CurrentDurability > 10 && !statusList.HasStatus(out _, CraftingPlayerStatuses.WasteNot2, CraftingPlayerStatuses.WasteNot)) || (CurrentDurability > 5 && statusList.HasStatus(out _, CraftingPlayerStatuses.WasteNot2, CraftingPlayerStatuses.WasteNot))))
                {
                    var newQualityPercent = Math.Floor(((double)Calculations.CalculateNewQuality(Skills.ByregotsBlessing) / (double)MaxQuality) * 100);
                    var newHQPercent = Calculations.GetHQChance(newQualityPercent);

                    if (HighQualityPercentage > 0)
                    {
                        if (newHQPercent >= P.Config.MaxPercentage) return Skills.ByregotsBlessing;
                    }
                    else
                    {
                        if (Calculations.CalculateNewQuality(Skills.ByregotsBlessing) >= MaxQuality) return Skills.ByregotsBlessing;
                    }
                }

                if (WasteNotUsed && CanUse(Skills.PreciseTouch) && !statusList.HasStatus(out _, CraftingPlayerStatuses.GreatStrides) && CurrentCondition is Condition.Good or Condition.Excellent) return Skills.PreciseTouch;
                if (!Skills.PreciseTouch.LevelChecked() && !statusList.HasStatus(out _, CraftingPlayerStatuses.GreatStrides) && CurrentCondition is Condition.Excellent)
                {
                    if (BasicTouchUsed && CanUse(Skills.StandardTouch)) return Skills.StandardTouch;
                    if (CanUse(Skills.BasicTouch)) return Skills.BasicTouch;
                }
                if (!ManipulationUsed && GetStatus(Buffs.Manipulation) is null && CanUse(Skills.Manipulation) && CurrentDurability < MaxDurability && !InTouchRotation) return Skills.Manipulation;
                if (!WasteNotUsed && GetStatus(Buffs.WasteNot2) is null && CanUse(Skills.WasteNot2)) return Skills.WasteNot2;
                if (!WasteNotUsed && GetStatus(Buffs.WasteNot) is null && CanUse(Skills.WasteNot) && !Skills.WasteNot2.LevelChecked()) return Skills.WasteNot;
                if (CanUse(Skills.PrudentTouch) && CurrentDurability == 10) return Skills.PrudentTouch;
                if (GetStatus(Buffs.Innovation) is null && CanUse(Skills.Innovation) && !InTouchRotation && CharacterInfo.CurrentCP >= 36) return Skills.Innovation;
                if (GetStatus(Buffs.GreatStrides) is null && CanUse(Skills.GreatStrides) && CurrentCondition != Condition.Excellent)
                {
                    var newQualityPercent = Math.Floor(((double)Calculations.GreatStridesByregotCombo() / (double)MaxQuality) * 100);
                    var newHQPercent = Calculations.GetHQChance(newQualityPercent);

                    if (HighQualityPercentage > 0)
                    {
                        if (newHQPercent >= P.Config.MaxPercentage) return Skills.GreatStrides;
                    }
                    else
                    {
                        if (Calculations.GreatStridesByregotCombo() >= MaxQuality) return Skills.GreatStrides;
                    }
                }

                if (CurrentCondition == Condition.Poor && CanUse(Skills.CarefulObservation) && P.Config.UseSpecialist) return Skills.CarefulObservation;
                if (CurrentCondition == Condition.Poor && CanUse(Skills.Observe))
                {
                    if (statusList.HasStatus(out int innovationstacks, CraftingPlayerStatuses.Innovation) && innovationstacks >= 2 && Skills.FocusedTouch.LevelChecked())
                        return Skills.Observe;

                    if (Calculations.CalculateNewProgress(act) < MaxProgress)
                        return act;

                    return Skills.Observe;
                }
                if (GetStatus(Buffs.GreatStrides) is not null && CanUse(Skills.ByregotsBlessing)) return Skills.ByregotsBlessing;
                if (JustUsedObserve && CanUse(Skills.FocusedTouch)) return Skills.FocusedTouch;
                if (CanCompleteTouchCombo())
                {
                    if (PreviousAction == Skills.BasicTouch && CanUse(Skills.StandardTouch)) return Skills.StandardTouch;
                    if (PreviousAction == Skills.StandardTouch && CanUse(Skills.AdvancedTouch)) return Skills.AdvancedTouch;
                    if (CanUse(Skills.BasicTouch)) return Skills.BasicTouch;
                }
                if (CharacterInfo.HighestLevelTouch() != 0) return CharacterInfo.HighestLevelTouch();
            }

            if (CanFinishCraft(act))
                return act;

            if (CanUse(Skills.Veneration) && GetStatus(Buffs.Veneration) == null && CurrentCondition != Condition.Excellent) return Skills.Veneration;
            return act;
        }

        private static void GoingForQuality()
        {
            int collectibilityCheck = 0;
            if (CurrentRecipe.ItemResult.Value.AlwaysCollectable)
            {
                switch (P.Config.SolverCollectibleMode)
                {
                    case 1:
                        collectibilityCheck = Convert.ToInt32(CollectabilityLow);
                        MaxQuality = collectibilityCheck * 10;
                        break;
                    case 2:
                        if (CollectabilityMid == "0")
                        {
                            collectibilityCheck = Convert.ToInt32(CollectabilityLow);
                        }
                        else
                        {
                            collectibilityCheck = Convert.ToInt32(CollectabilityMid);
                        }
                        MaxQuality = collectibilityCheck * 10;
                        break;
                    case 3:
                        if (CollectabilityHigh == "0")
                        {
                            if (CollectabilityMid == "0")
                            {
                                collectibilityCheck = Convert.ToInt32(CollectabilityLow);
                            }
                            else
                            {
                                collectibilityCheck = Convert.ToInt32(CollectabilityMid);
                            }
                        }
                        else
                        {
                            collectibilityCheck = Convert.ToInt32(CollectabilityHigh);
                        }
                        MaxQuality = collectibilityCheck * 10;
                        break;
                    default:
                        break;
                }
            }

            goingForQuality = ((!CurrentRecipe.ItemResult.Value.AlwaysCollectable && HighQualityPercentage < P.Config.MaxPercentage) ||
                  (CurrentRecipe.ItemResult.Value.AlwaysCollectable && HighQualityPercentage < collectibilityCheck)) && 
                  CharacterInfo.CurrentCP > P.Config.PriorityProgress;
        }

        private static bool ShouldMend(Skills synthOption)
        {
            if (!ManipulationUsed && CanUse(Skills.Manipulation)) return false;
            if (!WasteNotUsed && CanUse(Skills.WasteNot)) return false;

            bool wasteNots = statusList.HasStatus(out var wasteStacks, CraftingPlayerStatuses.WasteNot, CraftingPlayerStatuses.WasteNot2);
            var nextReduction = wasteNots ? 5 : 10;

            int advancedDegrade = 30 - (5 * wasteStacks);
            if (goingForQuality)
            {
                if (CanUse(Skills.PrudentTouch) && CurrentDurability == 10) return false;
                if (!Skills.AdvancedTouch.LevelChecked() && Skills.StandardTouch.LevelChecked() && CurrentDurability <= 20 && MaxDurability >= 50 && CurrentCondition != Condition.Excellent) return true;
                if (Skills.AdvancedTouch.LevelChecked() && CurrentDurability <= advancedDegrade && !InTouchRotation && MaxDurability >= 50 && CurrentCondition != Condition.Excellent) return true;
            }
            else
            {
                if (synthOption == Skills.Groundwork) nextReduction = wasteNots ? 10 : 20;
            }

            if (CurrentDurability - nextReduction <= 0) return true;
            return false;
        }

        private static bool CanCompleteTouchCombo()
        {
            var wasteNot = GetStatus(Buffs.WasteNot);
            var wasteNot2 = GetStatus(Buffs.WasteNot2);
            bool wasteNots = wasteNot != null || wasteNot2 != null;
            int wasteStacks = 0;
            if (wasteNots)
            {
                if (wasteNot is not null)
                    wasteStacks = wasteNot.StackCount;

                if (wasteNot2 is not null)
                    wasteStacks = wasteNot2.StackCount;
            }
            var veneration = GetStatus(Buffs.Innovation);
            var veneStacks = veneration is not null ? veneration.StackCount : 0;
            int reduction = wasteNots ? 5 : 10;
            int duraAfter = CurrentDurability - reduction;

            if (!Skills.StandardTouch.LevelChecked() && duraAfter > 0) return true;

            if (Skills.StandardTouch.LevelChecked() && !Skills.AdvancedTouch.LevelChecked())
            {
                if (PreviousAction == Skills.BasicTouch) return true; //Assume started
                if (CharacterInfo.CurrentCP < 36 || veneStacks < 2) return false;

                var copyofDura = CurrentDurability;
                for (int i = 1; i == 2; i++)
                {
                    copyofDura = wasteStacks > 0 ? copyofDura - 5 : copyofDura - 10;
                    wasteStacks--;
                }
                if (copyofDura > 0) return true;
            }

            if (Skills.AdvancedTouch.LevelChecked())
            {
                if (PreviousAction == Skills.BasicTouch || PreviousAction == Skills.StandardTouch) return true; //Assume started
                if (CharacterInfo.CurrentCP < 54 || veneStacks < 3) return false;

                var copyofDura = CurrentDurability;
                for (int i = 1; i == 3; i++)
                {
                    copyofDura = wasteStacks > 0 ? copyofDura - 5 : copyofDura - 10;
                    wasteStacks--;
                }
                if (copyofDura > 0) return true;
            }


            return false;
        }

        public static bool CheckForSuccess()
        {
            if (CurrentProgress < MaxProgress)
                return false;

            return true;
        }

        public static int RemainingProgress()
        {
            return MaxProgress - CurrentProgress;
        }

        public static bool CanFinishCraft(Skills act)
        {
            if (!CurrentRecipe.CanHq)
                return Calculations.CalculateNewProgress(act) >= MaxProgress;

            return Calculations.CalculateNewProgress(act) >= MaxProgress && !goingForQuality;
        }

        public unsafe static void RepeatTrialCraft()
        {
            try
            {
                if (Throttler.Throttle(500))
                {
                    if (GenericHelpers.TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var recipenote))
                    {
                        Callback.Fire(&recipenote->AtkUnitBase, true, 10);
                        Endurance.Tasks.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "RepeatTrialCraft");
            }
        }

        public unsafe static void QuickSynthItem(int crafts)
        {
            try
            {
                var recipeWindow = Svc.GameGui.GetAddonByName("RecipeNote", 1);
                if (recipeWindow == IntPtr.Zero)
                    return;

                GenericHelpers.TryGetAddonByName<AddonRecipeNoteFixed>("RecipeNote", out var addon);

                if (!int.TryParse(addon->SelectedRecipeQuantityCraftableFromMaterialsInInventory->NodeText.ToString(), out int trueNumberCraftable) || trueNumberCraftable == 0)
                {
                    return;
                }

                var addonPtr = (AddonRecipeNote*)recipeWindow;
                if (addonPtr == null)
                    return;

                try
                {
                    if (Throttler.Throttle(500))
                    {
                        ClickRecipeNote.Using(recipeWindow).QuickSynthesis();

                        var quickSynthPTR = Svc.GameGui.GetAddonByName("SynthesisSimpleDialog", 1);
                        if (quickSynthPTR == IntPtr.Zero)
                            return;

                        var quickSynthWindow = (AtkUnitBase*)quickSynthPTR;
                        if (quickSynthWindow == null)
                            return;

                        var values = stackalloc AtkValue[2];
                        values[0] = new()
                        {
                            Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int,
                            Int = Math.Min(trueNumberCraftable, Math.Min(crafts, 99)),
                        };
                        values[1] = new()
                        {
                            Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Bool,
                            Byte = 1,
                        };

                        quickSynthWindow->FireCallback(3, values);

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

        public unsafe static void CloseQuickSynthWindow()
        {
            try
            {
                var quickSynthPTR = Svc.GameGui.GetAddonByName("SynthesisSimple", 1);
                if (quickSynthPTR == IntPtr.Zero)
                    return;

                var quickSynthWindow = (AtkUnitBase*)quickSynthPTR;
                if (quickSynthWindow == null)
                    return;

                var qsynthButton = (AtkComponentButton*)quickSynthWindow->UldManager.NodeList[2];
                AtkResNodeFunctions.ClickButton(quickSynthWindow, qsynthButton, 0);
            }
            catch (Exception e)
            {
                e.Log();
            }
        }

        public unsafe static void RepeatActualCraft()
        {
            try
            {
                if (Throttler.Throttle(500))
                {
                    if (GenericHelpers.TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var recipenote))
                    {
                        ClickRecipeNote.Using(new IntPtr(&recipenote->AtkUnitBase)).Synthesize();
                        Endurance.Tasks.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "RepeatActualCraft");
            }
        }

        internal unsafe static bool CanUse(Skills id)
        {
            var actionId = id.ActionId(CharacterInfo.JobID);
            return actionId != 0 ? ActionManager.Instance()->GetActionStatus(actionId >= 100000 ? ActionType.CraftAction : ActionType.Action, actionId) == 0 : false;
        }

        internal static Status? GetStatus(uint statusID)
        {
            if (Svc.ClientState.LocalPlayer is null) return null;

            foreach (var status in Svc.ClientState.LocalPlayer?.StatusList)
            {
                if (status.StatusId == statusID)
                    return status;
            }

            return null;
        }
    }
}