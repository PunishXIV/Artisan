using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic.Solvers;
using Artisan.RawInformation.Character;
using Artisan.UI;
using Dalamud.Game.ClientState.Conditions;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Windows.Forms.AxHost;
using Condition = Artisan.CraftingLogic.CraftData.Condition;

namespace Artisan.CraftingLogic
{
    public static unsafe class CurrentCraft
    {
        public static event System.Action? StepChanged;

        public static Recipe? CurrentRecipe { get; private set; }
        public static CraftState? CurCraftState { get; private set; }
        public static StepState? CurStepState { get; private set; }
        public static List<StepState> PrevStepStates { get; private set; } = new();
        public static Skills CurrentRecommendation { get; set; }
        public static string CurrentRecommendationComment { get; set; } = "";

        public static int HighQualityPercentage { get; private set; } = 0;
        public static bool CanHQ  { get; private set; }

        public static int QuickSynthCurrent
        {
            get => quickSynthCurrent;
            private set
            {
                if (value != 0 && quickSynthCurrent != value)
                {
                    CraftingListFunctions.CurrentIndex++;
                    if (P.Config.QuickSynthMode && Endurance.Enable && P.Config.CraftX > 0)
                        P.Config.CraftX--;
                }
                quickSynthCurrent = value;
            }
        }
        public static int QuickSynthMax { get; private set; } = 0;
        public static bool DoingTrial { get; private set; } = false;
        public static CraftingState State { get; private set; } = CraftingState.NotCrafting;

        private static int quickSynthCurrent = 0;
        public static bool LastItemWasHQ { get; private set; } = false;
        public static Item? LastCraftedItem { get; private set; }
        public static Skills PreviousAction { get; private set; } = Skills.None;
        public static bool JustUsedAction { get; private set; } = false;

        public static void SetLastCraftedItem(Item? item, bool hq)
        {
            LastCraftedItem = item;
            LastItemWasHQ = hq;
        }

        public static void NotifyUsedAction(Skills action)
        {
            PreviousAction = action;
            JustUsedAction = true;
            CurrentRecommendation = Skills.None;
            CurrentRecommendationComment = "";
        }

        public unsafe static bool Update()
        {
            var newState = Svc.Condition[ConditionFlag.PreparingToCraft] ? CraftingState.PreparingToCraft : Svc.Condition[ConditionFlag.Crafting] ? CraftingState.Crafting : CraftingState.NotCrafting;
            if (State != newState)
            {
                if (CurCraftState != null && CurStepState != null && !P.Config.QuickSynthMode)
                {
                    bool wasSuccess = CurStepState.Progress >= CurCraftState.CraftProgress;
                    if (!wasSuccess && P.Config.EnduranceStopFail && Endurance.Enable)
                    {
                        Endurance.Enable = false;
                        Svc.Toasts.ShowError("You failed a craft. Disabling Endurance.");
                        DuoLog.Error("You failed a craft. Disabling Endurance.");
                    }

                    if (P.Config.EnduranceStopNQ && !LastItemWasHQ && LastCraftedItem != null && !LastCraftedItem.IsCollectable && LastCraftedItem.CanBeHq && Endurance.Enable)
                    {
                        Endurance.Enable = false;
                        Svc.Toasts.ShowError("You crafted a non-HQ item. Disabling Endurance.");
                        DuoLog.Error("You crafted a non-HQ item. Disabling Endurance.");
                    }
                }

                if (newState != CraftingState.Crafting)
                {
                    CraftingWindow.MacroTime = new();
                }
                else
                {
                    foreach (var window in P.ws.Windows.Where(x => x.GetType() == typeof(MacroEditor)))
                    {
                        window.IsOpen = false;
                    }
                }
                State = newState;
            }

            try
            {
                var quickSynthWindow = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SynthesisSimple", 1);
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
                else
                {
                    QuickSynthCurrent = 0;
                    QuickSynthMax = 0;
                }

                var synthWindow = (AddonSynthesis*)Svc.GameGui.GetAddonByName("Synthesis", 1);
                CharacterInfo.IsCrafting = synthWindow != null && synthWindow->AtkUnitBase.AtkValuesCount >= 26 && synthWindow->AtkUnitBase.UldManager.NodeListCount > 99;
                if (!CharacterInfo.IsCrafting)
                {
                    ClearCraft();
                    return false;
                }

                DoingTrial = synthWindow->AtkUnitBase.UldManager.NodeList[99]->IsVisible;

                var itemID = synthWindow->AtkUnitBase.AtkValues[16].UInt;
                var classID = CharacterInfo.JobID - Job.CRP;
                if (CurCraftState == null || CurrentRecipe == null || CurrentRecipe.ItemResult.Row != itemID || CurrentRecipe.CraftType.Row != classID)
                {
                    CurrentRecipe = Svc.Data.GetExcelSheet<Recipe>()?.FirstOrDefault(r => r.ItemResult.Row == itemID && r.CraftType.Row == classID);
                    if (CurrentRecipe == null)
                    {
                        Svc.Log.Error($"Failed to find recipe for {CharacterInfo.JobID} #{itemID}");
                        ClearCraft();
                        return false;
                    }
                    CanHQ = CurrentRecipe.CanHq;

                    var lt = CurrentRecipe.RecipeLevelTable.Value;
                    var weapon = Svc.Data.GetExcelSheet<Item>()?.GetRow(InventoryManager.Instance()->GetInventorySlot(InventoryType.EquippedItems, 0)->ItemID);
                    CurCraftState = BuildCraftStateForRecipe(CurrentRecipe);
                    if (synthWindow->AtkUnitBase.AtkValues[22].UInt != 0)
                    {
                        // three quality levels
                        CurCraftState.CraftQualityMin1 = synthWindow->AtkUnitBase.AtkValues[22].Int * 10;
                        CurCraftState.CraftQualityMin2 = synthWindow->AtkUnitBase.AtkValues[23].Int * 10;
                        CurCraftState.CraftQualityMin3 = synthWindow->AtkUnitBase.AtkValues[24].Int * 10;
                    }
                    else if (synthWindow->AtkUnitBase.AtkValues[18].Int != 0)
                    {
                        // min quality => assume no point in having more
                        CurCraftState.CraftQualityMin1 = CurCraftState.CraftQualityMin2 = CurCraftState.CraftQualityMin3 = synthWindow->AtkUnitBase.AtkValues[18].Int;
                    }

                    if (CraftingWindow.MacroTime.Ticks <= 0)
                    {
                        CraftingWindow.MacroTime = EstimateCraftTime(CurrentRecipe, CurCraftState);
                    }
                }

                HighQualityPercentage = CanHQ ? synthWindow->AtkUnitBase.AtkValues[10].Int : 0;

                if (CurStepState == null || JustUsedAction)
                {
                    UpdateStep(synthWindow);
                    JustUsedAction = false;
                }
                else if (CurStepState.Index != synthWindow->AtkUnitBase.AtkValues[15].Int || CurStepState.Condition != (Condition)synthWindow->AtkUnitBase.AtkValues[12].Int || CurStepState.PrevComboAction != PreviousAction)
                {
                    Svc.Log.Error("Unexpected step change without recorded action");
                    UpdateStep(synthWindow);
                }

                return true;
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, ex.StackTrace!);
                return false;
            }
        }

        private static void ClearCraft()
        {
            bool wasActive = CurCraftState != null;

            CurrentRecipe = null;
            CurCraftState = null;
            CurStepState = null;
            PrevStepStates.Clear();
            CurrentRecommendation = Skills.None;
            CurrentRecommendationComment = "";

            DoingTrial = false;
            JustUsedAction = false;

            if (wasActive)
                StepChanged.Invoke();
        }

        private static void UpdateStep(AddonSynthesis* synth)
        {
            if (CurStepState != null)
                PrevStepStates.Add(CurStepState);

            CurStepState = new()
            {
                Index = synth->AtkUnitBase.AtkValues[15].Int,
                Progress = synth->AtkUnitBase.AtkValues[5].Int,
                Quality = synth->AtkUnitBase.AtkValues[9].Int,
                Durability = synth->AtkUnitBase.AtkValues[7].Int,
                RemainingCP = (int)CharacterInfo.CurrentCP,
                Condition = (Condition)synth->AtkUnitBase.AtkValues[12].Int,
                IQStacks = GetStatus(Buffs.InnerQuiet)?.Param ?? 0,
                WasteNotLeft = GetStatus(Buffs.WasteNot2)?.Param ?? GetStatus(Buffs.WasteNot)?.Param ?? 0,
                ManipulationLeft = GetStatus(Buffs.Manipulation)?.Param ?? 0,
                GreatStridesLeft = GetStatus(Buffs.GreatStrides)?.Param ?? 0,
                InnovationLeft = GetStatus(Buffs.Innovation)?.Param ?? 0,
                VenerationLeft = GetStatus(Buffs.Veneration)?.Param ?? 0,
                MuscleMemoryLeft = GetStatus(Buffs.MuscleMemory)?.Param ?? 0,
                FinalAppraisalLeft = GetStatus(Buffs.FinalAppraisal)?.Param ?? 0,
                CarefulObservationLeft = P.Config.UseSpecialist && CanUse(Skills.CarefulObservation) ? 1 : 0,
                HeartAndSoulActive = GetStatus(Buffs.HeartAndSoul) != null,
                HeartAndSoulAvailable = P.Config.UseSpecialist && CanUse(Skills.HeartAndSoul),
                PrevComboAction = PreviousAction,
            };

            StepChanged.Invoke();
        }

        public unsafe static bool CanUse(Skills id)
        {
            var actionId = id.ActionId(CharacterInfo.JobID);
            return actionId != 0 ? ActionManager.Instance()->GetActionStatus(actionId >= 100000 ? ActionType.CraftAction : ActionType.Action, actionId) == 0 : false;
        }

        private static Dalamud.Game.ClientState.Statuses.Status? GetStatus(uint statusID) => Svc.ClientState.LocalPlayer?.StatusList.FirstOrDefault(s => s.StatusId == statusID);

        public static CraftState BuildCraftStateForRecipe(Recipe recipe)
        {
            var lt = recipe.RecipeLevelTable.Value;
            var weapon = Svc.Data.GetExcelSheet<Item>()?.GetRow(InventoryManager.Instance()->GetInventorySlot(InventoryType.EquippedItems, 0)->ItemID);
            var res = new CraftState()
            {
                StatCraftsmanship = CharacterInfo.Craftsmanship,
                StatControl = CharacterInfo.Control,
                StatCP = (int)CharacterInfo.MaxCP,
                StatLevel = CharacterInfo.CharacterLevel ?? 0,
                UnlockedManipulation = CharacterInfo.IsManipulationUnlocked(),
                Specialist = InventoryManager.Instance()->GetInventorySlot(InventoryType.EquippedItems, 13)->ItemID != 0, // specialist == job crystal equipped
                Splendorous = weapon?.LevelEquip == 90 && weapon?.Rarity >= 4,
                CraftExpert = recipe.IsExpert,
                CraftLevel = lt?.ClassJobLevel ?? 0,
                CraftDurability = Calculations.RecipeDurability(recipe), // atkvalue[8]
                CraftProgress = Calculations.RecipeDifficulty(recipe), // atkvalue[6]
                CraftProgressDivider = lt?.ProgressDivider ?? 180,
                CraftProgressModifier = lt?.ProgressModifier ?? 100,
                CraftQualityDivider = lt?.QualityDivider ?? 180,
                CraftQualityModifier = lt?.QualityModifier ?? 180,
                CraftQualityMax = Calculations.RecipeMaxQuality(recipe), // atkvalue[17]
            };
            // TODO: figure out a way to get quality breakpoints from data
            if (recipe.CanHq)
            {
                res.CraftQualityMin2 = res.CraftQualityMin3 = res.CraftQualityMax;
            }
            return res;
        }

        public static TimeSpan EstimateCraftTime(Recipe recipe, CraftState craft)
        {
            var s = P.GetSolverForRecipe(recipe.RowId, craft);
            if (s.solver == null)
                return default;

            var delay = (double)P.Config.AutoDelay + (P.Config.DelayRecommendation ? P.Config.RecommendationDelay : 0);
            var delaySeconds = delay / 1000;

            double duration = 0;
            var step = Simulator.CreateInitial(craft);
            var prev = new List<StepState>();
            while (Simulator.Status(craft, step) == Simulator.CraftStatus.InProgress)
            {
                var action = s.solver.Solve(craft, step, prev, s.flavour).action;
                if (action == Skills.None)
                    break;

                duration += (action.ActionIsLengthyAnimation() ? 2.5 : 1.25) + delaySeconds;

                prev.Add(step);
                var (res, next) = Simulator.Execute(craft, step, action, 0, 1);
                if (res == Simulator.ExecuteResult.CantUse)
                    break;
                step = next;
            }

            return TimeSpan.FromSeconds(Math.Round(duration, 2)); // Counting crafting duration + 2 seconds between crafts.
        }

        public static int EstimateHQPercent(Recipe recipe, CraftState craft)
        {
            var s = P.GetSolverForRecipe(recipe.RowId, craft);
            if (s.solver == null)
                return 0;

            var step = Simulator.CreateInitial(craft);
            var prev = new List<StepState>();
            while (Simulator.Status(craft, step) == Simulator.CraftStatus.InProgress)
            {
                var action = s.solver.Solve(craft, step, prev, s.flavour).action;
                if (action == Skills.None)
                    return 0;

                prev.Add(step);
                var (res, next) = Simulator.Execute(craft, step, action, 0, 1);
                if (res == Simulator.ExecuteResult.CantUse)
                    return 0;
                step = next;
            }

            return step.Progress < craft.CraftProgress ? 0 : craft.CraftQualityMin3 == 0 ? 100 : Calculations.GetHQChance(step.Quality * 100.0 / craft.CraftQualityMin3);
        }
    }
}
