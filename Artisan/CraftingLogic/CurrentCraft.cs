using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic.CraftData;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using ClickLib.Clicks;
using ECommons;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Condition = Artisan.CraftingLogic.CraftData.Condition;

namespace Artisan.CraftingLogic
{
    public static unsafe class CurrentCraft
    {
        public static event EventHandler<int>? StepChanged;
        public static int CurrentDurability { get; set; } = 0;
        public static int MaxDurability { get; set; } = 0;
        public static int CurrentProgress { get; set; } = 0;
        public static int MaxProgress { get; set; } = 0;
        public static int CurrentQuality { get; set; } = 0;
        public static int MaxQuality { get; set; } = 0;
        public static int HighQualityPercentage { get; set; } = 0;
        public static string? RecommendationName { get; set; }
        public static Condition CurrentCondition { get; set; }
        public static int CurrentStep
        {
            get { return currentStep; }
            set
            {
                if (currentStep != value)
                {
                    currentStep = value;
                    StepChanged?.Invoke(currentStep, value);
                    P.TM.Abort();
                }

            }
        }
        public static string? HQLiteral { get; set; }
        public static bool CanHQ { get; set; }
        public static string? CollectabilityLow { get; set; }
        public static string? CollectabilityMid { get; set; }
        public static string? CollectabilityHigh { get; set; }
        public static string? ItemName { get; set; }
        public static Recipe? CurrentRecipe { get; set; }
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
        public static int QuickSynthCurrent { get => quickSynthCurrent; set { if (value != 0 && quickSynthCurrent != value) { CraftingListFunctions.CurrentIndex++; } quickSynthCurrent = value; } }
        public static int QuickSynthMax { get => quickSynthMax; set => quickSynthMax = value; }
        public static int MacroStep { get; set; } = 0;
        public static CraftingState State
        {
            get { return state; }
            set
            {
                if (value != state)
                {
                    if (state == CraftingState.Crafting)
                    {
                        bool wasSuccess = CurrentCraftMethods.CheckForSuccess();
                        if (!wasSuccess && Service.Configuration.EnduranceStopFail && Handler.Enable)
                        {
                            Handler.Enable = false;
                            DuoLog.Error("You failed a craft. Disabling Endurance.");
                        }

                        if (Service.Configuration.EnduranceStopNQ && !LastItemWasHQ && LastCraftedItem != null && !LastCraftedItem.IsCollectable && LastCraftedItem.CanBeHq && Handler.Enable)
                        {
                            Handler.Enable = false;
                            DuoLog.Error("You crafted a non-HQ item. Disabling Endurance.");
                        }
                    }
                }

                state = value;
            }
        }

        private static int currentStep = 0;
        private static int quickSynthCurrent = 0;
        private static int quickSynthMax = 0;
        private static CraftingState state = CraftingState.NotCrafting;
        public static bool LastItemWasHQ = false;
        public static Item? LastCraftedItem;
        public static uint PreviousAction = 0;

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

                if (CurrentRecipe is null || CurrentRecipe.ItemResult.Value.Name.ExtractText() != ItemName)
                {
                    var sheetItem = LuminaSheets.RecipeSheet?.Values.Where(x => x.ItemResult.Value.Name!.ExtractText().Equals(ItemName) && x.CraftType.Value.RowId == CharacterInfo.JobID() - 8).FirstOrDefault();
                    if (sheetItem != null)
                    {
                        CurrentRecipe = sheetItem;
                    }
                }
                if (CurrentRecipe != null)
                {
                    if (CurrentRecipe.CanHq)
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
                if (LuminaSheets.AddonSheet.ContainsKey(14214) && cond.NodeText.ToString() == LuminaSheets.AddonSheet[14214].Text.RawString) CurrentCondition = Condition.GoodOmen;

                CurrentStep = Convert.ToInt32(cs.NodeText.ToString());
                HQLiteral = hql.NodeText.ToString();
                CollectabilityLow = collectLow.NodeText.ToString();
                CollectabilityMid = collectMid.NodeText.ToString();
                CollectabilityHigh = collectHigh.NodeText.ToString();

                return true;


            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, ex.StackTrace!);
                return false;
            }
        }
    }
}
