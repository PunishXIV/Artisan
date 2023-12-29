using Artisan.CraftingLists;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.Sounds;
using Artisan.UI;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static ECommons.GenericHelpers;

namespace Artisan.Autocraft
{
    public class EnduranceIngredients
    {
        public int HQSet { get; set; }
        public int IngredientSlot { get; set; }
        public int NQSet { get; set; }
    }

    internal static unsafe class Endurance
    {
        internal static bool SkipBuffs = false;
        internal static List<Task> Tasks = new();
        internal static CircularBuffer<long> Errors = new(5);

        internal static List<int>? HQData = null;

        internal static uint RecipeID = 0;

        internal static EnduranceIngredients[] SetIngredients = new EnduranceIngredients[6];

        internal static bool Enable
        {
            get => enable;
            set
            {
                Tasks.Clear();
                enable = value;
            }
        }

        internal static string RecipeName
        {
            get => RecipeID == 0 ? "No Recipe Selected" : LuminaSheets.RecipeSheet[RecipeID].ItemResult.Value.Name.RawString.Trim();
        }

        internal static void ToggleEndurance(bool enable)
        {
            if (RecipeID > 0)
            {
                Enable = enable;
            }
        }

        internal static void Dispose()
        {
            Svc.Toasts.ErrorToast -= Toasts_ErrorToast;
            Svc.Toasts.ErrorToast -= CheckNonMaxQuantityModeFinished;
        }

        internal static void Draw()
        {
            if (CraftingListUI.Processing)
            {
                ImGui.TextWrapped("Processing list...");
                return;
            }

            ImGui.TextWrapped("Endurance mode is Artisan's way to repeat the same craft over and over, either so many times or until you run out of materials. It has full capabilities to automatically repair your gear once a piece is under a certain percentage, use food/potions/exp manuals and extract materia from spiritbonding. Please note these settings are independent of crafting list settings, and only intended to be used to craft the one item repeatedly.");
            ImGui.Separator();
            ImGui.Spacing();

            if (RecipeID == 0)
            {
                ImGuiEx.TextV(ImGuiColors.DalamudRed, "No recipe selected");
            }
            else
            {
                if (!CraftingListFunctions.HasItemsForRecipe((uint)RecipeID))
                    ImGui.BeginDisabled();

                if (ImGui.Checkbox("Enable Endurance Mode", ref enable))
                {
                    ToggleEndurance(enable);
                }

                if (!CraftingListFunctions.HasItemsForRecipe((uint)RecipeID))
                {
                    ImGui.EndDisabled();

                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text($"You cannot start Endurance as you do not possess ingredients to craft this recipe.");
                        ImGui.EndTooltip();
                    }
                }

                ImGuiComponents.HelpMarker("In order to begin Endurance Mode crafting you should first select the recipe in the crafting menu.\nEndurance Mode will automatically repeat the selected recipe similar to Auto-Craft but will factor in food/medicine buffs before doing so.");

                ImGuiEx.Text($"Recipe: {RecipeName} {(RecipeID != 0 ? $"({LuminaSheets.ClassJobSheet[LuminaSheets.RecipeSheet[(uint)RecipeID].CraftType.Row + 8].Abbreviation})" : "")}");
            }
            
            bool repairs = P.Config.Repair;
            if (ImGui.Checkbox("Automatic Repairs", ref repairs))
            {
                P.Config.Repair = repairs;
                P.Config.Save();
            }
            ImGuiComponents.HelpMarker($"If enabled, Artisan will automatically repair your gear using Dark Matter when any piece reaches the configured repair threshold.\n\nCurrent min gear condition is {RepairManager.GetMinEquippedPercent()}%");
            if (P.Config.Repair)
            {
                //ImGui.SameLine();
                ImGui.PushItemWidth(200);
                int percent = P.Config.RepairPercent;
                if (ImGui.SliderInt("##repairp", ref percent, 10, 100, $"%d%%"))
                {
                    P.Config.RepairPercent = percent;
                    P.Config.Save();
                }
            }

            if (!RawInformation.Character.CharacterInfo.MateriaExtractionUnlocked())
                ImGui.BeginDisabled();

            bool materia = P.Config.Materia;
            if (ImGui.Checkbox("Automatically Extract Materia", ref materia))
            {
                P.Config.Materia = materia;
                P.Config.Save();
            }

            if (!RawInformation.Character.CharacterInfo.MateriaExtractionUnlocked())
            {
                ImGui.EndDisabled();

                ImGuiComponents.HelpMarker("This character has not unlocked materia extraction. This setting will be ignored.");
            }
            else
                ImGuiComponents.HelpMarker("Will automatically extract materia from any equipped gear once it's spiritbond is 100%");

            ImGui.Checkbox("Craft only X times", ref P.Config.CraftingX);
            if (P.Config.CraftingX)
            {
                ImGui.Text("Number of Times:");
                ImGui.SameLine();
                ImGui.PushItemWidth(200);
                if (ImGui.InputInt("###TimesRepeat", ref P.Config.CraftX))
                {
                    if (P.Config.CraftX < 0)
                        P.Config.CraftX = 0;
                }
            }

            if (ImGui.Checkbox("Use Quick Synthesis where possible", ref P.Config.QuickSynthMode))
            {
                P.Config.Save();
            }

            bool stopIfFail = P.Config.EnduranceStopFail;
            if (ImGui.Checkbox("Disable Endurance Mode Upon Failed Craft", ref stopIfFail))
            {
                P.Config.EnduranceStopFail = stopIfFail;
                P.Config.Save();
            }

            bool stopIfNQ = P.Config.EnduranceStopNQ;
            if (ImGui.Checkbox("Disable Endurance Mode Upon Crafting an NQ item", ref stopIfNQ))
            {
                P.Config.EnduranceStopNQ = stopIfNQ;
                P.Config.Save();
            }

            if (ImGui.Checkbox("Max Quantity Mode", ref P.Config.MaxQuantityMode))
            {
                P.Config.Save();
            }

            ImGuiComponents.HelpMarker("Will set ingredients for you, to maximise the amount of crafts possible.");
        }

        internal static void DrawRecipeData()
        {
            var addonPtr = Svc.GameGui.GetAddonByName("RecipeNote", 1);
            if (TryGetAddonByName<AddonRecipeNoteFixed>("RecipeNote", out var addon))
            {
                if (addonPtr == IntPtr.Zero)
                {
                    return;
                }

                if (addon->AtkUnitBase.IsVisible && addon->AtkUnitBase.UldManager.NodeListCount >= 49)
                {
                    try
                    {
                        if (addon->AtkUnitBase.UldManager.NodeList[88]->IsVisible)
                        {
                            RecipeID = 0;
                            return;
                        }

                        if (addon->SelectedRecipeName is null)
                            return;

                        if (addon->AtkUnitBase.UldManager.NodeList[49]->IsVisible)
                        {
                            RecipeID = RecipeNote.Instance()->RecipeList->SelectedRecipe->RecipeId;
                        }
                        Array.Clear(SetIngredients);

                        for (int i = 0; i <= 5; i++)
                        {
                            try
                            {
                                var node = addon->AtkUnitBase.UldManager.NodeList[23 - i]->GetAsAtkComponentNode();
                                if (node->Component->UldManager.NodeListCount < 16)
                                    return;

                                if (node is null || !node->AtkResNode.IsVisible)
                                {
                                    break;
                                }

                                var hqSetButton = node->Component->UldManager.NodeList[6]->GetAsAtkComponentNode();
                                var nqSetButton = node->Component->UldManager.NodeList[9]->GetAsAtkComponentNode();

                                var hqSetText = hqSetButton->Component->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText;
                                var nqSetText = nqSetButton->Component->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText;

                                int hqSet = Convert.ToInt32(hqSetText.ToString().GetNumbers());
                                int nqSet = Convert.ToInt32(nqSetText.ToString().GetNumbers());

                                EnduranceIngredients ingredients = new EnduranceIngredients()
                                {
                                    IngredientSlot = i,
                                    HQSet = hqSet,
                                    NQSet = nqSet,
                                };

                                SetIngredients[i] = ingredients;
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Svc.Log.Error(ex, "Setting Recipe ID");
                        RecipeID = 0;
                    }
                }
            }
        }

        internal static void Init()
        {
            Svc.Toasts.ErrorToast += Toasts_ErrorToast;
            Svc.Toasts.ErrorToast += CheckNonMaxQuantityModeFinished;
        }

        private static bool enable = false;
        private static void CheckNonMaxQuantityModeFinished(ref SeString message, ref bool isHandled)
        {
            if (!P.Config.MaxQuantityMode && Enable &&
                (message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == 1147).Text.ExtractText() ||
                 message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == 1146).Text.ExtractText() ||
                 message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == 1145).Text.ExtractText() ||
                 message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == 1144).Text.ExtractText()))
            {
                if (P.Config.PlaySoundFinishEndurance)
                    SoundPlayer.PlaySound();

                ToggleEndurance(false);
            }
        }

        public static void Update()
        {
            if ((Enable && P.Config.QuickSynthMode && Crafting.QuickSynthCompleted) || IPC.IPC.StopCraftingRequest ||
                (Enable && P.Config.Materia && Spiritbond.IsSpiritbondReadyAny() && CharacterInfo.MateriaExtractionUnlocked()))
            {
                Operations.CloseQuickSynthWindow();
            }

            if (Enable && !P.TM.IsBusy && Crafting.CurState is Crafting.State.IdleNormal or Crafting.State.IdleBetween)
            {
                var isCrafting = Svc.Condition[ConditionFlag.Crafting];
                var preparing = Svc.Condition[ConditionFlag.PreparingToCraft];

                if (!Throttler.Throttle(0))
                {
                    return;
                }
                if (P.Config.CraftingX && P.Config.CraftX == 0)
                {
                    Enable = false;
                    P.Config.CraftingX = false;
                    DuoLog.Information("Craft X has completed.");
                    if (P.Config.PlaySoundFinishEndurance)
                        Sounds.SoundPlayer.PlaySound();
                    return;
                }
                if (Svc.Condition[ConditionFlag.Occupied39])
                {
                    Throttler.Rethrottle(1000);
                }
                if (DebugTab.Debug) Svc.Log.Verbose("Throttle success");
                if (RecipeID == 0)
                {
                    Svc.Toasts.ShowError("No recipe has been set for Endurance mode. Disabling Endurance mode.");
                    DuoLog.Error("No recipe has been set for Endurance mode. Disabling Endurance mode.");
                    Enable = false;
                    return;
                }
                if (DebugTab.Debug) Svc.Log.Verbose("HQ not null");

                if (!Spiritbond.ExtractMateriaTask(P.Config.Materia, isCrafting, preparing))
                    return;

                if (P.Config.Repair && !RepairManager.ProcessRepair(false) && ((P.Config.Materia && !Spiritbond.IsSpiritbondReadyAny()) || (!P.Config.Materia)))
                {
                    if (DebugTab.Debug) Svc.Log.Verbose("Entered repair check");
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible && Svc.Condition[ConditionFlag.Crafting])
                    {
                        if (DebugTab.Debug) Svc.Log.Verbose("Crafting");
                        if (Throttler.Throttle(1000))
                        {
                            if (DebugTab.Debug) Svc.Log.Verbose("Closing crafting log");
                            CommandProcessor.ExecuteThrottled("/clog");
                        }
                    }
                    else
                    {
                        if (DebugTab.Debug) Svc.Log.Verbose("Not crafting");
                        if (!Svc.Condition[ConditionFlag.Crafting]) RepairManager.ProcessRepair(true);
                    }
                    return;
                }
                if (DebugTab.Debug) Svc.Log.Verbose("Repair ok");
                var config = P.Config.RecipeConfigs.GetValueOrDefault(RecipeID) ?? new();
                if (P.Config.AbortIfNoFoodPot && !ConsumableChecker.CheckConsumables(config, false))
                {
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible && Svc.Condition[ConditionFlag.Crafting])
                    {
                        if (Throttler.Throttle(1000))
                        {
                            if (DebugTab.Debug) Svc.Log.Verbose("Closing crafting log");
                            CommandProcessor.ExecuteThrottled("/clog");
                        }
                    }
                    else
                    {
                        if (!Svc.Condition[ConditionFlag.Crafting] && Enable) ConsumableChecker.CheckConsumables(config, true);
                    }
                    return;
                }
                if (DebugTab.Debug) Svc.Log.Verbose("Consumables success");
                {
                    if (CraftingListFunctions.RecipeWindowOpen())
                    {
                        if (DebugTab.Debug) Svc.Log.Verbose("Addon visible");

                        if (DebugTab.Debug) Svc.Log.Verbose("Error text not visible");

                        if (P.Config.QuickSynthMode && LuminaSheets.RecipeSheet[RecipeID].CanQuickSynth)
                        {
                            P.TM.Enqueue(() => CraftingListFunctions.RecipeWindowOpen(), "EnduranceCheckRecipeWindow");
                            P.TM.DelayNext("EnduranceThrottle", 100);

                            P.TM.Enqueue(() => { if (!CraftingListFunctions.HasItemsForRecipe((uint)RecipeID)) { if (P.Config.PlaySoundFinishEndurance) Sounds.SoundPlayer.PlaySound(); Enable = false; } }, "EnduranceStartCraft");
                            if (P.Config.CraftingX)
                                P.TM.Enqueue(() => Operations.QuickSynthItem(P.Config.CraftX));
                            else
                                P.TM.Enqueue(() => Operations.QuickSynthItem(99));
                        }
                        else
                        {
                            P.TM.Enqueue(() => CraftingListFunctions.RecipeWindowOpen(), "EnduranceCheckRecipeWindow");
                            if (P.Config.MaxQuantityMode)
                                P.TM.Enqueue(() => CraftingListFunctions.SetIngredients(), "EnduranceSetIngredients");
                            else
                                P.TM.Enqueue(() => CraftingListFunctions.SetIngredients(SetIngredients), "EnduranceSetIngredients");

                            P.TM.DelayNext("EnduranceThrottle", 100);
                            P.TM.Enqueue(() => { if (CraftingListFunctions.HasItemsForRecipe((uint)RecipeID)) Operations.RepeatActualCraft(); else { if (P.Config.PlaySoundFinishEndurance) Sounds.SoundPlayer.PlaySound(); Enable = false; } }, "EnduranceStartCraft");
                        }
                    }
                    else
                    {
                        if (!Svc.Condition[ConditionFlag.Crafting])
                        {
                            if (DebugTab.Debug) Svc.Log.Verbose("Addon invisible");
                            if (Tasks.Count == 0 && !Svc.Condition[ConditionFlag.Crafting40])
                            {
                                if (DebugTab.Debug) Svc.Log.Verbose($"Opening crafting log {RecipeID}");
                                if (RecipeID == 0)
                                {
                                    CommandProcessor.ExecuteThrottled("/clog");
                                }
                                else
                                {
                                    if (DebugTab.Debug) Svc.Log.Debug($"Opening recipe {RecipeID}");
                                    AgentRecipeNote.Instance()->OpenRecipeByRecipeIdInternal((uint)RecipeID);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void Toasts_ErrorToast(ref Dalamud.Game.Text.SeStringHandling.SeString message, ref bool isHandled)
        {
            if (Enable)
            {
                Errors.PushBack(Environment.TickCount64);
                if (Errors.Count() >= 5 && Errors.All(x => x > Environment.TickCount64 - 10 * 1000))
                {
                    Svc.Toasts.ShowError("Endurance has been disabled due to too many errors in succession.");
                    DuoLog.Error("Endurance has been disabled due to too many errors in succession.");
                    Enable = false;
                    Errors.Clear();
                }
            }
        }
    }
}
