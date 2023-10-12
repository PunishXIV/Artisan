using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.MacroSystem;
using Artisan.RawInformation;
using Artisan.UI;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static ECommons.GenericHelpers;
using PluginLog = Dalamud.Logging.PluginLog;

namespace Artisan.Autocraft
{
    internal unsafe class Endurance
    {
        private static bool enable = false;
        internal static List<int>? HQData = null;
        internal static uint RecipeID = 0;
        internal static string RecipeName
        {
            get => RecipeID == 0 ? "No Recipe Selected" : LuminaSheets.RecipeSheet[RecipeID].ItemResult.Value.Name.RawString;
        }

        internal static bool Enable
        {
            get => enable;
            set
            {
                Tasks.Clear();
                enable = value;
            }
        }

        internal static CircularBuffer<long> Errors = new(5);
        public static List<Task> Tasks = new();

        public static bool SkipBuffs = false;
        internal static void Init()
        {
            Svc.Framework.Update += Framework_Update;
            Svc.Toasts.ErrorToast += Toasts_ErrorToast;
        }

        private static void Toasts_ErrorToast(ref Dalamud.Game.Text.SeStringHandling.SeString message, ref bool isHandled)
        {
            if (Enable)
            {
                Errors.PushBack(Environment.TickCount64);
                if (Errors.Count() >= 5 && Errors.All(x => x > Environment.TickCount64 - 30 * 1000))
                {
                    Svc.Toasts.ShowError("Endurance has been disabled due to too many errors in succession.");
                    DuoLog.Error("Endurance has been disabled due to too many errors in succession.");
                    Enable = false;
                }
            }
        }

        internal static void Dispose()
        {
            Svc.Framework.Update -= Framework_Update;
            Svc.Toasts.ErrorToast -= Toasts_ErrorToast;
        }

        private static void Framework_Update(IFramework framework)
        {
            if ((Enable && P.Config.QuickSynthMode && CurrentCraft.QuickSynthCurrent == CurrentCraft.QuickSynthMax && CurrentCraft.QuickSynthMax > 0) || IPC.IPC.StopCraftingRequest)
            {
                CurrentCraftMethods.CloseQuickSynthWindow();
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
                                P.TM.Enqueue(() => CurrentCraftMethods.QuickSynthItem(P.Config.CraftX));
                            else
                                P.TM.Enqueue(() => CurrentCraftMethods.QuickSynthItem(99));
                        }
                        else
                        {
                            P.TM.Enqueue(() => CraftingListFunctions.RecipeWindowOpen(), "EnduranceCheckRecipeWindow");
                            if (P.Config.MaxQuantityMode)
                                P.TM.Enqueue(() => CraftingListFunctions.SetIngredients(), "EnduranceSetIngredients");
                            else
                                P.TM.Enqueue(() => { if (!CheckIngredientsSet()) { } });
                            P.TM.Enqueue(() => UpdateMacroTimer(), "UpdateEnduranceMacroTimer");
                            P.TM.DelayNext("EnduranceThrottle", 100);
                            P.TM.Enqueue(() => { if (CraftingListFunctions.HasItemsForRecipe((uint)RecipeID)) CurrentCraftMethods.RepeatActualCraft(); else { if (P.Config.PlaySoundFinishEndurance) Sounds.SoundPlayer.PlaySound(); Enable = false; } }, "EnduranceStartCraft");
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

        private static bool CheckIngredientsSet()
        {
            throw new NotImplementedException();
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
            if (HQManager.TryGetCurrent(out var d))
            {
                HQData = d;
            }
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
                            var text = addon->SelectedRecipeName->NodeText.ExtractText().Replace('', ' ').Trim().Replace($"-{(char)13}", "");
                            var firstCrystal = GetCrystal(addon, 1);
                            var secondCrystal = GetCrystal(addon, 2);

                            /*
                             *  0	3	2	Woodworking
                                1	1	5	Smithing
                                2	3	1	Armorcraft
                                3	2	4	Goldsmithing
                                4	3	4	Leatherworking
                                5	2	5	Clothcraft
                                6	4	6	Alchemy
                                7	5	6	Cooking

                                8	carpenter
                                9	blacksmith
                                10	armorer
                                11	goldsmith
                                12	leatherworker
                                13	weaver
                                14	alchemist
                                15	culinarian
                                (ClassJob - 8)
                             * 
                             * */

                            //if (str.Length == 0) return;

                            //str = str
                            //    .Replace($"{(char)13}", "")
                            //    .Replace("-", "");

                            //if (str[^1] == '')
                            //{
                            //    rName += str.Remove(str.Length - 1, 1).Trim();
                            //}
                            //else
                            //{
                            //    rName += str;
                            //}

                            //if (rName.Length == 0) return;

                            if (firstCrystal > 0 && secondCrystal > 0)
                            {
                                //Svc.Log.Debug($"{LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == 31955).ItemResult.Value.Name}");
                                
                                if (LuminaSheets.RecipeSheet.Values.TryGetFirst(x => x.ItemResult.Value?.Name!.RawString == text && x.UnkData5[8].ItemIngredient == firstCrystal && x.UnkData5[9].ItemIngredient == secondCrystal, out var id))
                                {
                                    RecipeID = id.RowId;
                                }
                            }
                            else if (firstCrystal > 0)
                            {
                                if (LuminaSheets.RecipeSheet.Values.TryGetFirst(x => x.ItemResult.Value?.Name!.RawString == text && x.UnkData5[8].ItemIngredient == firstCrystal, out var id))
                                {
                                    RecipeID = id.RowId;
                                }
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

        private static uint GetSelectedJobTab(AtkUnitBase* addon)
        {
            for (int i = 91; i <= 98; i++)
            {
                if (addon->UldManager.NodeList[i]->GetComponent()->UldManager.NodeList[5]->IsVisible)
                {
                    return i switch
                    {
                        91 => 15,
                        92 => 14,
                        93 => 13,
                        94 => 12,
                        95 => 11,
                        96 => 10,
                        97 => 9,
                        98 => 8,
                        _ => throw new NotImplementedException()
                    };
                }
            }

            return 0;
        }

        private static int GetCrystal(AddonRecipeNoteFixed* addon, int slot)
        {
            try
            {
                var node = slot == 1 ? addon->AtkUnitBase.UldManager.NodeList[29]->GetComponent()->UldManager.NodeList[1]->GetAsAtkImageNode() : addon->AtkUnitBase.UldManager.NodeList[28]->GetComponent()->UldManager.NodeList[1]->GetAsAtkImageNode();
                if (slot == 2 && !node->AtkResNode.IsVisible)
                    return -1;

                var texturePath = node->PartsList->Parts[node->PartId].UldAsset;

                var texFileNameStdString = &texturePath->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle.FileName;
                var texString = texFileNameStdString->Length < 16
                        ? Marshal.PtrToStringAnsi((IntPtr)texFileNameStdString->Buffer)
                        : Marshal.PtrToStringAnsi((IntPtr)texFileNameStdString->BufferPtr);

                if (texString.Contains("020001")) return 2;     //Fire shard
                if (texString.Contains("020002")) return 7;     //Water shard
                if (texString.Contains("020003")) return 3;     //Ice shard
                if (texString.Contains("020004")) return 4;     //Wind shard
                if (texString.Contains("020005")) return 6;     //Lightning shard
                if (texString.Contains("020006")) return 5;     //Earth shard

                if (texString.Contains("020007")) return 8;     //Fire crystal
                if (texString.Contains("020008")) return 13;    //Water crystal
                if (texString.Contains("020009")) return 9;     //Ice crystal
                if (texString.Contains("020010")) return 10;    //Wind crystal
                if (texString.Contains("020011")) return 12;    //Lightning crystal
                if (texString.Contains("020012")) return 11;    //Earth crystal

                if (texString.Contains("020013")) return 14;    //Fire cluster
                if (texString.Contains("020014")) return 19;    //Water cluster
                if (texString.Contains("020015")) return 15;    //Ice cluster
                if (texString.Contains("020016")) return 16;    //Wind cluster
                if (texString.Contains("020017")) return 18;    //Lightning cluster
                if (texString.Contains("020018")) return 17;    //Earth cluster

            }
            catch
            {

            }

            return -1;

        }

        public static void ToggleEndurance(bool enable)
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
