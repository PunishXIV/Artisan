using Artisan.Autocraft;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using ClickLib.Clicks;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Linq;
using static Artisan.CraftingLogic.CurrentCraft;
using Condition = Artisan.CraftingLogic.CraftData.Condition;

namespace Artisan.CraftingLogic
{
    public static class CurrentCraftMethods
    {
        public static unsafe bool BestSynthesis(out uint action, bool allowComplete = true)
        {
            var lastActionId = PreviousAction;
            var agentCraftActionSimulator = AgentModule.Instance()->GetAgentCraftActionSimulator();
            var remainingProgress = (uint)RemainingProgress();
            var progressActions = agentCraftActionSimulator->Progress;
            var statusList = Service.ClientState.LocalPlayer!.StatusList;

            // Need to take into account MP
            // Rapid(500/50, 0)?
            // Intensive(400, 6) > Groundwork(300, 18) > Focused(200, 5) > Prudent(180, 18) > Careful(150, 7) > Groundwork(150, 18) > Basic(120, 0)
            if (Calculations.CalculateNewProgress(Skills.BasicSynth) >= MaxProgress)
            {
                action = Skills.BasicSynth;
                return true;
            }

            if (allowComplete)
            {
                if (CanUse(Skills.IntensiveSynthesis))
                {
                    action = Skills.IntensiveSynthesis;
                    return progressActions->IntensiveSynthesis.CanComplete(remainingProgress)!.Value;
                }

                // Only use Groundwork to speed up if it'll complete
                if (progressActions->Groundwork.CanComplete(remainingProgress) == true)
                {
                    action = Skills.Groundwork;
                    return true;
                }

                if (PreviousActionSameAs(Skills.Observe) && progressActions->FocusedSynthesis.IsAvailable())
                {
                    action = Skills.FocusedSynthesis;
                    return progressActions->FocusedSynthesis.CanComplete(remainingProgress)!.Value;
                }

                // Prudent:
                // Not MP efficient unless we're intentionally conserving Durability
                //if (progressActions->PrudentSynthesis.CanComplete(remainingProgress) == allowComplete)
                //    return ActionId.PrudentSynthesis;

                if (progressActions->CarefulSynthesis.IsAvailable())
                {
                    action = Skills.CarefulSynthesis;
                    return progressActions->CarefulSynthesis.CanComplete(remainingProgress)!.Value;
                }
            }
            else
            {
                // Technically this path is only used near the beginning

                if (progressActions->IntensiveSynthesis.CanComplete(remainingProgress) == false)
                {
                    action = Skills.IntensiveSynthesis;
                    return false;
                }

                // Use groundwork if we have any buff up to help with durability, maybe not optimal, but helps preserve buffs
                if (progressActions->Groundwork.CanComplete(remainingProgress) == false &&
                    (
                    (statusList.HasStatus(out _, CraftingPlayerStatuses.WasteNot, CraftingPlayerStatuses.WasteNot2) && CurrentDurability > 10) ||
                    (statusList.HasStatus(out _, CraftingPlayerStatuses.Manipulation) && CurrentDurability > 20)
                    ))
                {
                    action = Skills.Groundwork;
                    return false;
                }

                if (PreviousActionSameAs(Skills.Observe) && progressActions->FocusedSynthesis.CanComplete(remainingProgress) == false)
                {
                    action = Skills.FocusedSynthesis;
                    return false;
                }

                // Prudent:
                // Not MP efficient unless we're intentionally conserving Durability
                //if (progressActions->PrudentSynthesis.CanComplete(remainingProgress) == allowComplete)
                //    return ActionId.PrudentSynthesis;

                if (progressActions->CarefulSynthesis.CanComplete(remainingProgress) == false)
                {
                    action = Skills.CarefulSynthesis;
                    return allowComplete;
                }
            }

            action = Skills.BasicSynth;
            return progressActions->BasicSynthesis.CanComplete(remainingProgress)!.Value;
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
            BestSynthesis(out var act, true);
            if (CurrentStep == 1 && Calculations.CalculateNewProgress(Skills.DelicateSynthesis) >= MaxProgress && Calculations.CalculateNewQuality(Skills.DelicateSynthesis) >= MaxQuality && CanUse(Skills.DelicateSynthesis)) return Skills.DelicateSynthesis;
            if (CanFinishCraft(act)) return act;

            if (CanUse(Skills.TrainedEye) && (HighQualityPercentage < Service.Configuration.MaxPercentage || CurrentRecipe.ItemResult.Value.IsCollectable) && CurrentRecipe.CanHq) return Skills.TrainedEye;
            if (CanUse(Skills.Tricks) && CurrentStep > 2 && ((CurrentCondition == Condition.Good && Service.Configuration.UseTricksGood) || (CurrentCondition == Condition.Excellent && Service.Configuration.UseTricksExcellent))) return Skills.Tricks;

            if (CurrentDurability <= 10 && CanUse(Skills.MastersMend)) return Skills.MastersMend;

            if (MaxQuality == 0 || Service.Configuration.MaxPercentage == 0 || !CurrentRecipe.CanHq)
            {
                if (CurrentStep == 1 && CanUse(Skills.MuscleMemory)) return Skills.MuscleMemory;
                if (Calculations.CalculateNewProgress(act) >= MaxProgress) return act;
                if (GetStatus(Buffs.Veneration) == null && CanUse(Skills.Veneration)) return Skills.Veneration;
                return act;
            }

            if (CharacterInfo.CurrentCP > 0)
            {
                if (MaxDurability >= 60)
                {
                    if (CurrentQuality < MaxQuality && (HighQualityPercentage < Service.Configuration.MaxPercentage || CurrentRecipe.ItemResult.Value.IsCollectable || CurrentRecipe.IsExpert))
                    {
                        if (CurrentStep == 1 && CanUse(Skills.MuscleMemory) && Calculations.CalculateNewProgress(Skills.MuscleMemory) < MaxProgress) return Skills.MuscleMemory;
                        if (CurrentStep == 2 && CanUse(Skills.FinalAppraisal) && !JustUsedFinalAppraisal && Calculations.CalculateNewProgress(CharacterInfo.HighestLevelSynth()) >= MaxProgress) return Skills.FinalAppraisal;
                        if (GetStatus(Buffs.MuscleMemory) != null) return CharacterInfo.HighestLevelSynth();
                        if (!ManipulationUsed && GetStatus(Buffs.Manipulation) is null && CanUse(Skills.Manipulation)) return Skills.Manipulation;
                        if (!WasteNotUsed && GetStatus(Buffs.WasteNot2) is null && CanUse(Skills.WasteNot2)) return Skills.WasteNot2;
                        if (Calculations.CalculateNewQuality(Skills.ByregotsBlessing) >= MaxQuality && CanUse(Skills.ByregotsBlessing)) return Skills.ByregotsBlessing;
                        if (GetStatus(Buffs.Innovation) is null && CanUse(Skills.Innovation)) return Skills.Innovation;
                        if (Calculations.GreatStridesByregotCombo() >= MaxQuality && GetStatus(Buffs.GreatStrides) is null && CanUse(Skills.GreatStrides) && CurrentCondition != Condition.Excellent) return Skills.GreatStrides;
                        if (CurrentCondition == Condition.Poor && CanUse(Skills.CarefulObservation) && Service.Configuration.UseSpecialist) return Skills.CarefulObservation;
                        if (CurrentCondition == Condition.Poor && CanUse(Skills.Observe)) return Skills.Observe;
                        if (GetStatus(Buffs.GreatStrides) is not null && CanUse(Skills.ByregotsBlessing)) return Skills.ByregotsBlessing;
                        if (Skills.AdvancedTouch.LevelChecked() && Service.Configuration.UseAlternativeRotation)
                        {
                            if (PreviousActionSameAs(Skills.BasicTouch) && CanUse(Skills.StandardTouch) && GetStatus(Buffs.Innovation).StackCount >= 1) return Skills.StandardTouch;
                            if (PreviousActionSameAs(Skills.StandardTouch) && CanUse(Skills.AdvancedTouch) && GetStatus(Buffs.Innovation).StackCount >= 2) return Skills.AdvancedTouch;
                            if (CanUse(Skills.BasicTouch) && GetStatus(Buffs.Innovation).StackCount >= 3) return Skills.BasicTouch;
                        }
                        if (CharacterInfo.HighestLevelTouch() != 0) return CharacterInfo.HighestLevelTouch();

                    }
                }

                if (MaxDurability >= 35 && MaxDurability < 60)
                {
                    if (CurrentQuality < MaxQuality && (HighQualityPercentage < Service.Configuration.MaxPercentage || CurrentRecipe.ItemResult.Value.IsCollectable || CurrentRecipe.IsExpert))
                    {
                        if (CurrentStep == 1 && CanUse(Skills.Reflect)) return Skills.Reflect;
                        if (!ManipulationUsed && GetStatus(Buffs.Manipulation) is null && CanUse(Skills.Manipulation)) return Skills.Manipulation;
                        if (!WasteNotUsed && CanUse(Skills.WasteNot2)) return Skills.WasteNot2;
                        if (!InnovationUsed && CanUse(Skills.Innovation)) return Skills.Innovation;
                        if (Calculations.CalculateNewQuality(Skills.ByregotsBlessing) >= MaxQuality && CanUse(Skills.ByregotsBlessing)) return Skills.ByregotsBlessing;
                        if (Calculations.GreatStridesByregotCombo() >= MaxQuality && GetStatus(Buffs.GreatStrides) is null && CanUse(Skills.GreatStrides) && CurrentCondition != Condition.Excellent) return Skills.GreatStrides;
                        if (CurrentCondition == Condition.Poor && CanUse(Skills.CarefulObservation) && Service.Configuration.UseSpecialist) return Skills.CarefulObservation;
                        if (CurrentCondition == Condition.Poor && CanUse(Skills.Observe)) return Skills.Observe;
                        if (GetStatus(Buffs.GreatStrides) is not null && CanUse(Skills.ByregotsBlessing)) return Skills.ByregotsBlessing;
                        if (Skills.AdvancedTouch.LevelChecked() && Service.Configuration.UseAlternativeRotation)
                        {
                            if (PreviousActionSameAs(Skills.BasicTouch) && CanUse(Skills.StandardTouch)) return Skills.StandardTouch;
                            if (PreviousActionSameAs(Skills.StandardTouch) && CanUse(Skills.AdvancedTouch)) return Skills.AdvancedTouch;
                            if (CanUse(Skills.BasicTouch)) return Skills.BasicTouch;
                        }
                        if (CharacterInfo.HighestLevelTouch() != 0) return CharacterInfo.HighestLevelTouch();
                    }
                }
            }

            if (CanUse(Skills.Veneration) && GetStatus(Buffs.Veneration) == null && !VenerationUsed) return Skills.Veneration;
            return act;
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

            var metMaxProg = CurrentQuality >= MaxQuality;
            var usingPercentage = HighQualityPercentage >= Service.Configuration.MaxPercentage && !CurrentRecipe.ItemResult.Value.IsCollectable && !CurrentRecipe.IsExpert;
            return Calculations.CalculateNewProgress(act) >= MaxProgress && (metMaxProg || usingPercentage);
        }

        public unsafe static void RepeatTrialCraft()
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
                ClickRecipeNote.Using(recipeWindow).TrialSynthesis();
                Handler.Tasks.Clear();
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
    }
}