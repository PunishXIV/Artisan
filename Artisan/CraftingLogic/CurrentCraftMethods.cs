using Artisan.Autocraft;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using ClickLib.Clicks;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Logging;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Linq;
using static Artisan.CraftingLogic.CurrentCraft;
using Condition = Artisan.CraftingLogic.CraftData.Condition;
using Status = Dalamud.Game.ClientState.Statuses.Status;

namespace Artisan.CraftingLogic
{
    public static class CurrentCraftMethods
    {
        private static StatusList statusList => Service.ClientState.LocalPlayer!.StatusList;
        private static bool InTouchRotation => (PreviousActionSameAs(Skills.BasicTouch) && Skills.StandardTouch.LevelChecked()) || (PreviousActionSameAs(Skills.StandardTouch) && Skills.AdvancedTouch.LevelChecked());

        private static bool goingForQuality = true;

        public static unsafe bool BestSynthesis(out uint action, bool allowComplete = true)
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

            if (Calculations.CalculateNewProgress(Skills.BasicSynth) >= MaxProgress)
            {
                action = Skills.BasicSynth;
                return true;
            }

            if (CanUse(Skills.IntensiveSynthesis))
            {
                action = Skills.IntensiveSynthesis;
                return progressActions->IntensiveSynthesis.CanComplete(remainingProgress)!.Value;
            }

            // Only use Groundwork to speed up if it'll complete or if under muscle memory
            if (progressActions->Groundwork.CanComplete(remainingProgress) == true || statusList.HasStatus(out int _, CraftingPlayerStatuses.MuscleMemory))
            {
                action = Skills.Groundwork;
                return true;
            }

            if (PreviousActionSameAs(Skills.Observe) && progressActions->FocusedSynthesis.IsAvailable())
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
                action = Skills.BasicSynth;
                return progressActions->BasicSynthesis.CanComplete(remainingProgress)!.Value;
            }

            if (progressActions->RapidSynthesis.IsAvailable() && CharacterInfo.CurrentCP < 18)
            {
                action = Skills.RapidSynthesis;
                return progressActions->RapidSynthesis.CanComplete(remainingProgress)!.Value;
            }

            action = Skills.BasicSynth;
            return progressActions->BasicSynthesis.CanComplete(remainingProgress)!.Value;
        }

        private unsafe static bool CanSpamBasicToComplete()
        {
            var agentCraftActionSimulator = AgentModule.Instance()->GetAgentCraftActionSimulator();
            var remainingProgress = (uint)RemainingProgress();
            var progressActions = agentCraftActionSimulator->Progress;

            var firstUse = progressActions->BasicSynthesis.ProgressIncrease;
            if (progressActions->BasicSynthesis.CanComplete(remainingProgress)!.Value)
                return true;

            var progressAfterFirst = remainingProgress - firstUse;
            var durabilityAfterFirst = CurrentDurability - (10 / (GetStatus(Buffs.WasteNot) != null || GetStatus(Buffs.WasteNot2) != null ? 2 : 1)) + (GetStatus(Buffs.Manipulation) != null ? 5 : 0);

            var manipStacks = GetStatus(Buffs.Manipulation) != null ? GetStatus(Buffs.Manipulation).StackCount - 1 : 0;
            var wasteNotStacks = GetStatus(Buffs.WasteNot) != null ? GetStatus(Buffs.WasteNot).StackCount - 1 : 0;
            var wasteNot2Stacks = GetStatus(Buffs.WasteNot2) != null ? GetStatus(Buffs.WasteNot2).StackCount - 1 : 0;
            var totalwastestacks = wasteNot2Stacks + wasteNotStacks;

            var basicIncrease = Math.Floor(Calculations.BaseProgression() * Calculations.GetMultiplier(Skills.BasicSynth));
            var progressAfterNext = progressAfterFirst - basicIncrease;

            while (durabilityAfterFirst > 0)
            {
                if (progressAfterNext <= 0)
                    return true;

                durabilityAfterFirst = durabilityAfterFirst - (10 / (totalwastestacks > 0 ? 2 : 1) + (manipStacks > 0 ? 5 : 0));
                progressAfterNext = progressAfterNext - basicIncrease;

                PluginLog.Debug($"{durabilityAfterFirst} {progressAfterNext}");
            }

            return false;

        }

        public static uint GetExpertRecommendation()
        {
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
            if (CurrentQuality < MaxQuality)
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
        public static uint GetRecommendation()
        {
            BestSynthesis(out var act);

            int collectibilityCheck = 0;
            if (CurrentRecipe.ItemResult.Value.IsCollectable)
            {
                collectibilityCheck = Service.Configuration.SolverCollectibleMode switch
                {
                    1 => Convert.ToInt32(CollectabilityLow),
                    2 => Convert.ToInt32(CollectabilityMid),
                    3 => Convert.ToInt32(CollectabilityHigh)
                };
            }

            goingForQuality = (!CurrentRecipe.ItemResult.Value.IsCollectable && HighQualityPercentage < Service.Configuration.MaxPercentage) ||
                  (CurrentRecipe.ItemResult.Value.IsCollectable && HighQualityPercentage < collectibilityCheck);

            if (CurrentStep == 1 && Calculations.CalculateNewProgress(Skills.DelicateSynthesis) >= MaxProgress && Calculations.CalculateNewQuality(Skills.DelicateSynthesis) >= MaxQuality && CanUse(Skills.DelicateSynthesis)) return Skills.DelicateSynthesis;
            if (CanFinishCraft(act)) return act;

            if (CanUse(Skills.TrainedEye) && (HighQualityPercentage < Service.Configuration.MaxPercentage || CurrentRecipe.ItemResult.Value.IsCollectable) && CurrentRecipe.CanHq) return Skills.TrainedEye;
            if (CanUse(Skills.Tricks) && CurrentStep > 2 && ((CurrentCondition == Condition.Good && Service.Configuration.UseTricksGood) || (CurrentCondition == Condition.Excellent && Service.Configuration.UseTricksExcellent))) return Skills.Tricks;

            if ((CharacterInfo.CurrentCP < 7 || ShouldMend(act) || (!Skills.PreciseTouch.LevelChecked() && CurrentCondition == Condition.Good && GetStatus(Buffs.Innovation) is null)) && CanUse(Skills.Tricks) && !InTouchRotation) return Skills.Tricks;
            if (ShouldMend(act) && CanUse(Skills.MastersMend)) return Skills.MastersMend;

            if (MaxQuality == 0 || Service.Configuration.MaxPercentage == 0 || !CurrentRecipe.CanHq)
            {
                if (CurrentStep == 1 && CanUse(Skills.MuscleMemory)) return Skills.MuscleMemory;
                if (Calculations.CalculateNewProgress(act) >= MaxProgress) return act;
                if (GetStatus(Buffs.Veneration) == null && CanUse(Skills.Veneration)) return Skills.Veneration;
                return act;
            }

            if (goingForQuality)
            {
                if (!Service.Configuration.UseQualityStarter)
                {
                    if (CurrentStep == 1 && CanUse(Skills.MuscleMemory) && Calculations.CalculateNewProgress(Skills.MuscleMemory) < MaxProgress) return Skills.MuscleMemory;
                    if (CurrentStep == 2 && CanUse(Skills.Veneration) && Calculations.CalculateNewProgress(act) < MaxProgress && GetStatus(Buffs.MuscleMemory) != null) return Skills.Veneration;
                    if ((CurrentStep == 2 || CurrentStep == 3 && GetStatus(Buffs.MuscleMemory) != null) && CanUse(Skills.FinalAppraisal) && !JustUsedFinalAppraisal && Calculations.CalculateNewProgress(act) >= MaxProgress) return Skills.FinalAppraisal;
                    if (GetStatus(Buffs.MuscleMemory) != null) return act;
                }

                if (Service.Configuration.UseQualityStarter)
                {
                    if (CurrentStep == 1 && CanUse(Skills.Reflect)) return Skills.Reflect;
                }

                if (CanUse(Skills.PreciseTouch) && !statusList.HasStatus(out _, CraftingPlayerStatuses.GreatStrides) && CurrentCondition is Condition.Good or Condition.Excellent) return Skills.PreciseTouch;
                if (!Skills.PreciseTouch.LevelChecked() && !statusList.HasStatus(out _, CraftingPlayerStatuses.GreatStrides) && CurrentCondition is Condition.Excellent)
                {
                    if (BasicTouchUsed && CanUse(Skills.StandardTouch)) return Skills.StandardTouch;
                    return Skills.BasicTouch;
                }

                if (!ManipulationUsed && GetStatus(Buffs.Manipulation) is null && CanUse(Skills.Manipulation) && CurrentDurability < MaxDurability && !InTouchRotation) return Skills.Manipulation;
                if (!WasteNotUsed && GetStatus(Buffs.WasteNot2) is null && CanUse(Skills.WasteNot2)) return Skills.WasteNot2;
                if (!WasteNotUsed && GetStatus(Buffs.WasteNot) is null && CanUse(Skills.WasteNot) && !Skills.WasteNot2.LevelChecked()) return Skills.WasteNot;
                if (Calculations.CalculateNewQuality(Skills.ByregotsBlessing) >= MaxQuality && CanUse(Skills.ByregotsBlessing)) return Skills.ByregotsBlessing;
                if (GetStatus(Buffs.Innovation) is null && CanUse(Skills.Innovation) && !InTouchRotation) return Skills.Innovation;
                if (Calculations.GreatStridesByregotCombo() >= MaxQuality && GetStatus(Buffs.GreatStrides) is null && CanUse(Skills.GreatStrides) && CurrentCondition != Condition.Excellent) return Skills.GreatStrides;
                if (CurrentCondition == Condition.Poor && CanUse(Skills.CarefulObservation) && Service.Configuration.UseSpecialist) return Skills.CarefulObservation;
                if (CurrentCondition == Condition.Poor && CanUse(Skills.Observe))
                {
                    if (statusList.HasStatus(out int innovationstacks, CraftingPlayerStatuses.Innovation) && innovationstacks >= 2 && !Skills.FocusedTouch.LevelChecked())
                        return Skills.Observe;

                    if (!CanFinishCraft(act))
                        return act;

                    return Skills.Observe;
                }
                if (GetStatus(Buffs.GreatStrides) is not null && CanUse(Skills.ByregotsBlessing)) return Skills.ByregotsBlessing;
                if (JustUsedObserve && CanUse(Skills.FocusedTouch)) return Skills.FocusedTouch;
                if (CanCompleteTouchCombo())
                {
                    if (PreviousActionSameAs(Skills.BasicTouch) && CanUse(Skills.StandardTouch)) return Skills.StandardTouch;
                    if (PreviousActionSameAs(Skills.StandardTouch) && CanUse(Skills.AdvancedTouch)) return Skills.AdvancedTouch;
                    if (CanUse(Skills.BasicTouch)) return Skills.BasicTouch;
                }
                if (CharacterInfo.HighestLevelTouch() != 0) return CharacterInfo.HighestLevelTouch();
            }

            if (CanFinishCraft(act))
                return act;

            if (CanUse(Skills.Veneration) && GetStatus(Buffs.Veneration) == null && CurrentCondition != Condition.Excellent) return Skills.Veneration;
            return act;
        }

        private static bool ShouldMend(uint synthOption)
        {
            bool wasteNots = GetStatus(Buffs.WasteNot) != null || GetStatus(Buffs.WasteNot2) != null;
            var nextReduction = wasteNots ? 5 : 10;

            if (goingForQuality)
            {
                if (!Skills.AdvancedTouch.LevelChecked() && Skills.StandardTouch.LevelChecked() && CurrentDurability <= 20 && MaxDurability >= 50) return true;
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
                if (PreviousActionSameAs(Skills.BasicTouch)) return true; //Assume started
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
                if (PreviousActionSameAs(Skills.BasicTouch) || PreviousActionSameAs(Skills.StandardTouch)) return true; //Assume started
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

        public static bool PreviousActionSameAs(uint id) => PreviousAction.NameOfAction() == id.NameOfAction();

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

        public static bool CanFinishCraft(uint act)
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
                    var recipeWindow = Service.GameGui.GetAddonByName("RecipeNote", 1);
                    if (recipeWindow == IntPtr.Zero)
                        return;

                    var addonPtr = (AddonRecipeNote*)recipeWindow;
                    if (addonPtr == null)
                        return;

                    var synthButton = addonPtr->TrialSynthesisButton;
                    ClickRecipeNote.Using(recipeWindow).TrialSynthesis();
                    Handler.Tasks.Clear();
                }
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "RepeatTrialCraft");
            }
        }

        public unsafe static void QuickSynthItem(int crafts)
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

        public unsafe static void RepeatActualCraft()
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

                        Handler.Tasks.Clear();
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

        internal unsafe static uint CanUse2(uint id)
        {
            ActionManager* actionManager = ActionManager.Instance();
            if (actionManager == null)
                return 1;

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

            return 1;
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

        internal static Status? GetStatus(uint statusID)
        {
            if (Service.ClientState.LocalPlayer is null) return null;

            foreach (var status in Service.ClientState.LocalPlayer?.StatusList)
            {
                if (status.StatusId == statusID)
                    return status;
            }

            return null;
        }
    }
}