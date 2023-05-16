using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.RawInformation;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Utility.Signatures;
using ECommons;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static ECommons.GenericHelpers;
using PluginLog = Dalamud.Logging.PluginLog;

namespace Artisan.Autocraft
{
    internal unsafe class Handler
    {
        private static bool enable = false;
        internal static List<int>? HQData = null;
        internal static int RecipeID = 0;
        internal static string RecipeName { get => recipeName; set { if (value != recipeName) PluginLog.Verbose($"{value}"); recipeName = value; } }

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
        private static string recipeName = "No Recipe Picked";
        public static List<Task> Tasks = new();


        internal static void Init()
        {
            SignatureHelper.Initialise(new Handler());
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
                    //Svc.Chat.Print($"{Errors.Select(x => x.ToString()).Join(",")}");
                    DuoLog.Error("Endurance has been disabled due to too many errors in succession.");
                    Enable = false;
                }
            }
        }

        internal static void Dispose()
        {
            //BeginSynthesisHook?.Disable();
            //BeginSynthesisHook?.Dispose();
            Svc.Framework.Update -= Framework_Update;
            Svc.Toasts.ErrorToast -= Toasts_ErrorToast;
        }

        private static void Framework_Update(Dalamud.Game.Framework framework)
        {
            if (Enable && !P.TM.IsBusy)
            {
                var isCrafting = Service.Condition[ConditionFlag.Crafting];
                var preparing = Service.Condition[ConditionFlag.PreparingToCraft];

                if (!Throttler.Throttle(0))
                {
                    return;
                }
                if (Service.Configuration.CraftingX && Service.Configuration.CraftX == 0)
                {
                    Enable = false;
                    Service.Configuration.CraftingX = false;
                    DuoLog.Information("Craft X has completed.");
                    return;
                }
                if (Svc.Condition[ConditionFlag.Occupied39])
                {
                    Throttler.Rethrottle(1000);
                }
                if (AutocraftDebugTab.Debug) PluginLog.Verbose("Throttle success");
                if (RecipeID == 0)
                {
                    DuoLog.Error("No recipe has been set for Endurance mode. Disabling Endurance mode.");
                    Enable = false;
                    return;
                }
                if (AutocraftDebugTab.Debug) PluginLog.Verbose("HQ not null");
                if (Service.Configuration.Materia && Spiritbond.IsSpiritbondReadyAny())
                {
                    if (AutocraftDebugTab.Debug) PluginLog.Verbose("Entered materia extraction");
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible && Svc.Condition[ConditionFlag.Crafting])
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Crafting");
                        if (Throttler.Throttle(1000))
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("Closing crafting log");
                            CommandProcessor.ExecuteThrottled("/clog");
                        }
                    }
                    if (!Spiritbond.IsMateriaMenuOpen() && !isCrafting && !preparing)
                    {
                        Spiritbond.OpenMateriaMenu();
                        return;
                    }
                    if (Spiritbond.IsMateriaMenuOpen() && !isCrafting && !preparing)
                    {
                        Spiritbond.ExtractFirstMateria();
                        return;
                    }

                    return;
                }
                else
                {
                    if (Spiritbond.IsMateriaMenuOpen())
                    {
                        Spiritbond.CloseMateriaMenu();
                        return;
                    }
                }

                if (Service.Configuration.Repair && !RepairManager.ProcessRepair(false) && ((Service.Configuration.Materia && !Spiritbond.IsSpiritbondReadyAny()) || (!Service.Configuration.Materia)))
                {
                    if (AutocraftDebugTab.Debug) PluginLog.Verbose("Entered repair check");
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible && Svc.Condition[ConditionFlag.Crafting])
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Crafting");
                        if (Throttler.Throttle(1000))
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("Closing crafting log");
                            CommandProcessor.ExecuteThrottled("/clog");
                        }
                    }
                    else
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Not crafting");
                        if (!Svc.Condition[ConditionFlag.Crafting]) RepairManager.ProcessRepair(true);
                    }
                    return;
                }
                if (AutocraftDebugTab.Debug) PluginLog.Verbose("Repair ok");
                if (Service.Configuration.AbortIfNoFoodPot && !ConsumableChecker.CheckConsumables(false))
                {
                    if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible && Svc.Condition[ConditionFlag.Crafting])
                    {
                        if (Throttler.Throttle(1000))
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("Closing crafting log");
                            CommandProcessor.ExecuteThrottled("/clog");
                        }
                    }
                    else
                    {
                        if (!Svc.Condition[ConditionFlag.Crafting] && Enable) ConsumableChecker.CheckConsumables(true);
                    }
                    return;
                }
                if (AutocraftDebugTab.Debug) PluginLog.Verbose("Consumables success");
                {
                    if (CraftingListFunctions.RecipeWindowOpen())
                    {
                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Addon visible");

                        if (AutocraftDebugTab.Debug) PluginLog.Verbose("Error text not visible");
                        //if (!HQManager.RestoreHQData(HQData, out var fin) || !fin)
                        //{
                        //    if (AutocraftDebugTab.Debug) PluginLog.Verbose($"HQ data finalised");
                        //}
                        //if (AutocraftDebugTab.Debug) PluginLog.Verbose("HQ data restored");

                        if (!P.TM.IsBusy)
                        {
                            P.TM.Enqueue(() => CraftingListFunctions.SetIngredients());
                            P.TM.DelayNext(300);
                            P.TM.Enqueue(() => { if (CraftingListFunctions.HasItemsForRecipe((uint)RecipeID)) CurrentCraft.RepeatActualCraft(); });
                        }

                    }
                    else
                    {
                        if (!Svc.Condition[ConditionFlag.Crafting])
                        {
                            if (AutocraftDebugTab.Debug) PluginLog.Verbose("Addon invisible");
                            if (Tasks.Count == 0 && !Svc.Condition[ConditionFlag.Crafting40])
                            {
                                if (AutocraftDebugTab.Debug) PluginLog.Verbose($"Opening crafting log {RecipeID}");
                                if (RecipeID == 0)
                                {
                                    CommandProcessor.ExecuteThrottled("/clog");
                                }
                                else
                                {
                                    if (AutocraftDebugTab.Debug) PluginLog.Debug($"Opening recipe {RecipeID}");
                                    AgentRecipeNote.Instance()->OpenRecipeByRecipeIdInternal((uint)RecipeID);
                                }
                            }
                        }
                    }

                }
            }
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
                if (ImGui.Checkbox("Enable Endurance Mode", ref enable))
                {
                    Enable = enable;
                }
                ImGuiComponents.HelpMarker("In order to begin Endurance Mode crafting you should first select the recipe in the crafting menu.\nEndurance Mode will automatically repeat the selected recipe similar to Auto-Craft but will factor in food/medicine buffs before doing so.");

                ImGuiEx.Text($"Recipe: {RecipeName} {(RecipeID != 0 ? $"({LuminaSheets.ClassJobSheet[LuminaSheets.RecipeSheet[(uint)RecipeID].CraftType.Row + 8].Abbreviation})" : "")}");
            }
            bool requireFoodPot = Service.Configuration.AbortIfNoFoodPot;
            if (ImGui.Checkbox("Use Food, Manuals and/or Medicine", ref requireFoodPot))
            {
                Service.Configuration.AbortIfNoFoodPot = requireFoodPot;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Artisan will require the configured food, manuals or medicine and refuse to craft if it cannot be found.");
            if (requireFoodPot)
            {

                {
                    ImGuiEx.TextV("Food Usage:");
                    ImGui.SameLine(200f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##foodBuff", ConsumableChecker.Food.TryGetFirst(x => x.Id == Service.Configuration.Food, out var item) ? $"{(Service.Configuration.FoodHQ ? " " : "")}{item.Name}" : $"{(Service.Configuration.Food == 0 ? "Disabled" : $"{(Service.Configuration.FoodHQ ? " " : "")}{Service.Configuration.Food}")}"))
                    {
                        if (ImGui.Selectable("Disable"))
                        {
                            Service.Configuration.Food = 0;
                            Service.Configuration.Save();
                        }
                        foreach (var x in ConsumableChecker.GetFood(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                Service.Configuration.Food = x.Id;
                                Service.Configuration.FoodHQ = false;
                                Service.Configuration.Save();
                            }
                        }
                        foreach (var x in ConsumableChecker.GetFood(true, true))
                        {
                            if (ImGui.Selectable($" {x.Name}"))
                            {
                                Service.Configuration.Food = x.Id;
                                Service.Configuration.FoodHQ = true;
                                Service.Configuration.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }

                {
                    ImGuiEx.TextV("Medicine Usage:");
                    ImGui.SameLine(200f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##potBuff", ConsumableChecker.Pots.TryGetFirst(x => x.Id == Service.Configuration.Potion, out var item) ? $"{(Service.Configuration.PotHQ ? " " : "")}{item.Name}" : $"{(Service.Configuration.Potion == 0 ? "Disabled" : $"{(Service.Configuration.PotHQ ? " " : "")}{Service.Configuration.Potion}")}"))
                    {
                        if (ImGui.Selectable("Disable"))
                        {
                            Service.Configuration.Potion = 0;
                            Service.Configuration.Save();
                        }
                        foreach (var x in ConsumableChecker.GetPots(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                Service.Configuration.Potion = x.Id;
                                Service.Configuration.PotHQ = false;
                                Service.Configuration.Save();
                            }
                        }
                        foreach (var x in ConsumableChecker.GetPots(true, true))
                        {
                            if (ImGui.Selectable($" {x.Name}"))
                            {
                                Service.Configuration.Potion = x.Id;
                                Service.Configuration.PotHQ = true;
                                Service.Configuration.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }

                {
                    ImGuiEx.TextV("Manual Usage:");
                    ImGui.SameLine(200f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##manualBuff", ConsumableChecker.Manuals.TryGetFirst(x => x.Id == Service.Configuration.Manual, out var item) ? $"{item.Name}" : $"{(Service.Configuration.Manual == 0 ? "Disabled" : $"{Service.Configuration.Manual}")}"))
                    {
                        if (ImGui.Selectable("Disable"))
                        {
                            Service.Configuration.Manual = 0;
                            Service.Configuration.Save();
                        }
                        foreach (var x in ConsumableChecker.GetManuals(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                Service.Configuration.Manual = x.Id;
                                Service.Configuration.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }

                {
                    ImGuiEx.TextV("Squadron Manual Usage:");
                    ImGui.SameLine(200f.Scale());
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.BeginCombo("##squadronManualBuff", ConsumableChecker.SquadronManuals.TryGetFirst(x => x.Id == Service.Configuration.SquadronManual, out var item) ? $"{item.Name}" : $"{(Service.Configuration.SquadronManual == 0 ? "Disabled" : $"{Service.Configuration.SquadronManual}")}"))
                    {
                        if (ImGui.Selectable("Disable"))
                        {
                            Service.Configuration.SquadronManual = 0;
                            Service.Configuration.Save();
                        }
                        foreach (var x in ConsumableChecker.GetSquadronManuals(true))
                        {
                            if (ImGui.Selectable($"{x.Name}"))
                            {
                                Service.Configuration.SquadronManual = x.Id;
                                Service.Configuration.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }

            }

            bool repairs = Service.Configuration.Repair;
            if (ImGui.Checkbox("Automatic Repairs", ref repairs))
            {
                Service.Configuration.Repair = repairs;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("If enabled, Artisan will automatically repair your gear using Dark Matter when any piece reaches the configured repair threshold.");
            if (Service.Configuration.Repair)
            {
                //ImGui.SameLine();
                ImGui.PushItemWidth(200);
                int percent = Service.Configuration.RepairPercent;
                if (ImGui.SliderInt("##repairp", ref percent, 10, 100, $"%d%%"))
                {
                    Service.Configuration.RepairPercent = percent;
                    Service.Configuration.Save();
                }
            }

            bool materia = Service.Configuration.Materia;
            if (ImGui.Checkbox("Automatically Extract Materia", ref materia))
            {
                Service.Configuration.Materia = materia;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("Will automatically extract materia from any equipped gear once it's spiritbond is 100%");

            ImGui.Checkbox("Craft only X times", ref Service.Configuration.CraftingX);
            if (Service.Configuration.CraftingX)
            {
                ImGui.Text("Number of Times:");
                ImGui.SameLine();
                ImGui.PushItemWidth(200);
                if (ImGui.InputInt("###TimesRepeat", ref Service.Configuration.CraftX))
                {
                    if (Service.Configuration.CraftX < 0)
                        Service.Configuration.CraftX = 0;

                }
            }

            bool stopIfFail = Service.Configuration.EnduranceStopFail;
            if (ImGui.Checkbox("Disable Endurance Mode Upon Failed Craft", ref stopIfFail))
            {
                Service.Configuration.EnduranceStopFail = stopIfFail;
                Service.Configuration.Save();
            }

            bool stopIfNQ = Service.Configuration.EnduranceStopNQ;
            if (ImGui.Checkbox("Disable Endurance Mode Upon Crafting an NQ item", ref stopIfNQ))
            {
                Service.Configuration.EnduranceStopNQ = stopIfNQ;
                Service.Configuration.Save();
            }
        }

        internal static void DrawRecipeData()
        {
            if (HQManager.TryGetCurrent(out var d))
            {
                HQData = d;
            }
            var addonPtr = Service.GameGui.GetAddonByName("RecipeNote", 1);
            if (addonPtr == IntPtr.Zero)
            {
                return;
            }

            var addon = (AtkUnitBase*)addonPtr;
            if (addon == null)
            {
                return;
            }

            if (addon->IsVisible && addon->UldManager.NodeListCount >= 49)
            {
                try
                {
                    if (addon->UldManager.NodeList[88]->IsVisible)
                    {
                        RecipeID = 0;
                        RecipeName = "";
                        return;
                    }

                    if (addon->UldManager.NodeList[49]->IsVisible)
                    {
                        var text = addon->UldManager.NodeList[49]->GetAsAtkTextNode()->NodeText;
                        var jobText = addon->UldManager.NodeList[101]->GetAsAtkTextNode()->NodeText.ExtractText();
                        uint jobTab = GetSelectedJobTab(addon);
                        var firstCrystal = GetCrystal(addon, 1);
                        var secondCrystal = GetCrystal(addon, 2);
                        var str = MemoryHelper.ReadSeString(&text);
                        var rName = "";

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

                        if (str.ExtractText().Length == 0) return;

                        if (str.ExtractText()[^1] == '')
                        {
                            rName += str.ExtractText().Remove(str.ExtractText().Length - 1, 1).Trim();
                        }
                        else
                        {

                            rName += str.ExtractText().Trim();
                        }

                        if (firstCrystal > 0)
                        {
                            if (RecipeName != rName)
                            {
                                if (LuminaSheets.RecipeSheet.Values.TryGetFirst(x => x.ItemResult.Value?.Name!.ExtractText() == rName && x.UnkData5[8].ItemIngredient == firstCrystal && x.UnkData5[9].ItemIngredient == secondCrystal, out var id))
                                {
                                    RecipeID = (int)id.RowId;
                                    RecipeName = id.ItemResult.Value.Name.ExtractText();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "Setting Recipe ID");
                    RecipeID = 0;
                    RecipeName = "";
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

        private static int GetCrystal(AtkUnitBase* addon, int slot)
        {
            try
            {
                var node = slot == 1 ? addon->UldManager.NodeList[29]->GetComponent()->UldManager.NodeList[1]->GetAsAtkImageNode() : addon->UldManager.NodeList[28]->GetComponent()->UldManager.NodeList[1]->GetAsAtkImageNode();
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
    }
}
