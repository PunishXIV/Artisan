using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.MacroSystem;
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
using PluginLog = Dalamud.Logging.PluginLog;

namespace Artisan.Autocraft
{
    internal class EnduranceIngredients
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

                try
                {
                    if (enable)
                    {
                        UpdateMacroTimer();
                    }
                }
                catch (Exception ex)
                {
                    ex.Log();
                }
            }
        }
        internal static void Dispose()
        {
            Svc.Framework.Update -= Framework_Update;
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
            bool requireFoodPot = P.Config.AbortIfNoFoodPot;
            if (ImGui.Checkbox("Use Food, Manuals and/or Medicine", ref requireFoodPot))
            {
                P.Config.AbortIfNoFoodPot = requireFoodPot;
                P.Config.Save();
            }
            ImGuiComponents.HelpMarker("Artisan will require the configured food, manuals or medicine and refuse to craft if it cannot be found.");
            if (requireFoodPot)
            {
                {
                    ImGuiEx.TextV("Food Usage:");
                    ImGui.SameLine(200f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##foodBuff", ConsumableChecker.Food.TryGetFirst(x => x.Id == P.Config.Food, out var item) ? $"{(P.Config.FoodHQ ? " " : "")}{item.Name}" : $"{(P.Config.Food == 0 ? "Disabled" : $"{(P.Config.FoodHQ ? " " : "")}{P.Config.Food}")}"))
                    {
                        if (ImGui.Selectable("Disable"))
                        {
                            P.Config.Food = 0;
                            P.Config.Save();
                        }
                        foreach (var x in ConsumableChecker.GetFood(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                P.Config.Food = x.Id;
                                P.Config.FoodHQ = false;
                                P.Config.Save();
                            }
                        }
                        foreach (var x in ConsumableChecker.GetFood(true, true))
                        {
                            if (ImGui.Selectable($" {x.Name}"))
                            {
                                P.Config.Food = x.Id;
                                P.Config.FoodHQ = true;
                                P.Config.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }

                {
                    ImGuiEx.TextV("Medicine Usage:");
                    ImGui.SameLine(200f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##potBuff", ConsumableChecker.Pots.TryGetFirst(x => x.Id == P.Config.Potion, out var item) ? $"{(P.Config.PotHQ ? " " : "")}{item.Name}" : $"{(P.Config.Potion == 0 ? "Disabled" : $"{(P.Config.PotHQ ? " " : "")}{P.Config.Potion}")}"))
                    {
                        if (ImGui.Selectable("Disable"))
                        {
                            P.Config.Potion = 0;
                            P.Config.Save();
                        }
                        foreach (var x in ConsumableChecker.GetPots(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                P.Config.Potion = x.Id;
                                P.Config.PotHQ = false;
                                P.Config.Save();
                            }
                        }
                        foreach (var x in ConsumableChecker.GetPots(true, true))
                        {
                            if (ImGui.Selectable($" {x.Name}"))
                            {
                                P.Config.Potion = x.Id;
                                P.Config.PotHQ = true;
                                P.Config.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }

                {
                    ImGuiEx.TextV("Manual Usage:");
                    ImGui.SameLine(200f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##manualBuff", ConsumableChecker.Manuals.TryGetFirst(x => x.Id == P.Config.Manual, out var item) ? $"{item.Name}" : $"{(P.Config.Manual == 0 ? "Disabled" : $"{P.Config.Manual}")}"))
                    {
                        if (ImGui.Selectable("Disable"))
                        {
                            P.Config.Manual = 0;
                            P.Config.Save();
                        }
                        foreach (var x in ConsumableChecker.GetManuals(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                P.Config.Manual = x.Id;
                                P.Config.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }

                {
                    ImGuiEx.TextV("Squadron Manual Usage:");
                    ImGui.SameLine(200f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##squadronManualBuff", ConsumableChecker.SquadronManuals.TryGetFirst(x => x.Id == P.Config.SquadronManual, out var item) ? $"{item.Name}" : $"{(P.Config.SquadronManual == 0 ? "Disabled" : $"{P.Config.SquadronManual}")}"))
                    {
                        if (ImGui.Selectable("Disable"))
                        {
                            P.Config.SquadronManual = 0;
                            P.Config.Save();
                        }
                        foreach (var x in ConsumableChecker.GetSquadronManuals(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                P.Config.SquadronManual = x.Id;
                                P.Config.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }
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
                            catch (Exception ex)
                            {
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex, "Setting Recipe ID");
                        RecipeID = 0;
                    }
                }
            }
        }

        internal static void Init()
        {
            Svc.Framework.Update += Framework_Update;
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

        private static void Framework_Update(IFramework framework)
        {
            if ((Enable && P.Config.QuickSynthMode && CurrentCraft.QuickSynthCurrent == CurrentCraft.QuickSynthMax && CurrentCraft.QuickSynthMax > 0) || IPC.IPC.StopCraftingRequest ||
                (Enable && P.Config.Materia && Spiritbond.IsSpiritbondReadyAny() && CharacterInfo.MateriaExtractionUnlocked()))
            {
                SolverLogic.CloseQuickSynthWindow();
            }

            if (Enable && !P.TM.IsBusy && CurrentCraft.State != CraftingState.Crafting)
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
                if (DebugTab.Debug) PluginLog.Verbose("Throttle success");
                if (RecipeID == 0)
                {
                    Svc.Toasts.ShowError("No recipe has been set for Endurance mode. Disabling Endurance mode.");
                    DuoLog.Error("No recipe has been set for Endurance mode. Disabling Endurance mode.");
                    Enable = false;
                    return;
                }
                if (DebugTab.Debug) PluginLog.Verbose("HQ not null");

                if (!Spiritbond.ExtractMateriaTask(P.Config.Materia, isCrafting, preparing))
                    return;

                if (P.Config.Repair && !RepairManager.ProcessRepair(false) && ((P.Config.Materia && !Spiritbond.IsSpiritbondReadyAny()) || (!P.Config.Materia)))
                {
                    if (DebugTab.Debug) PluginLog.Verbose("Entered repair check");
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible && Svc.Condition[ConditionFlag.Crafting])
                    {
                        if (DebugTab.Debug) PluginLog.Verbose("Crafting");
                        if (Throttler.Throttle(1000))
                        {
                            if (DebugTab.Debug) PluginLog.Verbose("Closing crafting log");
                            CommandProcessor.ExecuteThrottled("/clog");
                        }
                    }
                    else
                    {
                        if (DebugTab.Debug) PluginLog.Verbose("Not crafting");
                        if (!Svc.Condition[ConditionFlag.Crafting]) RepairManager.ProcessRepair(true);
                    }
                    return;
                }
                if (DebugTab.Debug) PluginLog.Verbose("Repair ok");
                if (P.Config.AbortIfNoFoodPot && !ConsumableChecker.CheckConsumables(false))
                {
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible && Svc.Condition[ConditionFlag.Crafting])
                    {
                        if (Throttler.Throttle(1000))
                        {
                            if (DebugTab.Debug) PluginLog.Verbose("Closing crafting log");
                            CommandProcessor.ExecuteThrottled("/clog");
                        }
                    }
                    else
                    {
                        if (!Svc.Condition[ConditionFlag.Crafting] && Enable) ConsumableChecker.CheckConsumables(true);
                    }
                    return;
                }
                if (DebugTab.Debug) PluginLog.Verbose("Consumables success");
                {
                    if (CraftingListFunctions.RecipeWindowOpen())
                    {
                        if (DebugTab.Debug) PluginLog.Verbose("Addon visible");

                        if (DebugTab.Debug) PluginLog.Verbose("Error text not visible");

                        if (P.Config.QuickSynthMode && LuminaSheets.RecipeSheet[RecipeID].CanQuickSynth)
                        {
                            P.TM.Enqueue(() => CraftingListFunctions.RecipeWindowOpen(), "EnduranceCheckRecipeWindow");
                            P.TM.DelayNext("EnduranceThrottle", 100);

                            P.TM.Enqueue(() => { if (!CraftingListFunctions.HasItemsForRecipe((uint)RecipeID)) { if (P.Config.PlaySoundFinishEndurance) Sounds.SoundPlayer.PlaySound(); Enable = false; } }, "EnduranceStartCraft");
                            if (P.Config.CraftingX)
                                P.TM.Enqueue(() => SolverLogic.QuickSynthItem(P.Config.CraftX));
                            else
                                P.TM.Enqueue(() => SolverLogic.QuickSynthItem(99));
                        }
                        else
                        {
                            P.TM.Enqueue(() => CraftingListFunctions.RecipeWindowOpen(), "EnduranceCheckRecipeWindow");
                            if (P.Config.MaxQuantityMode)
                                P.TM.Enqueue(() => CraftingListFunctions.SetIngredients(), "EnduranceSetIngredients");
                            else
                                P.TM.Enqueue(() => CraftingListFunctions.SetIngredients(SetIngredients), "EnduranceSetIngredients");

                            P.TM.Enqueue(() => UpdateMacroTimer(), "UpdateEnduranceMacroTimer");
                            P.TM.DelayNext("EnduranceThrottle", 100);
                            P.TM.Enqueue(() => { if (CraftingListFunctions.HasItemsForRecipe((uint)RecipeID)) SolverLogic.RepeatActualCraft(); else { if (P.Config.PlaySoundFinishEndurance) Sounds.SoundPlayer.PlaySound(); Enable = false; } }, "EnduranceStartCraft");
                        }
                    }
                    else
                    {
                        if (!Svc.Condition[ConditionFlag.Crafting])
                        {
                            if (DebugTab.Debug) PluginLog.Verbose("Addon invisible");
                            if (Tasks.Count == 0 && !Svc.Condition[ConditionFlag.Crafting40])
                            {
                                if (DebugTab.Debug) PluginLog.Verbose($"Opening crafting log {RecipeID}");
                                if (RecipeID == 0)
                                {
                                    CommandProcessor.ExecuteThrottled("/clog");
                                }
                                else
                                {
                                    if (DebugTab.Debug) PluginLog.Debug($"Opening recipe {RecipeID}");
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

        private static void UpdateMacroTimer()
        {
            if (P.Config.CraftingX && P.Config.CraftX > 0 && P.Config.IRM.ContainsKey((uint)RecipeID))
            {
                var macro = P.Config.UserMacros.FirstOrDefault(x => x.ID == P.Config.IRM[(uint)RecipeID]);
                Double timeInSeconds = ((MacroUI.GetMacroLength(macro) * P.Config.CraftX)); // Counting crafting duration + 2 seconds between crafts.
                CraftingWindow.MacroTime = TimeSpan.FromSeconds(timeInSeconds);
            }
        }
    }
}