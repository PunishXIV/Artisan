using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json.Serialization;
using System.Text.Json;
using static ECommons.GenericHelpers;
using Artisan.Autocraft;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;
using ECommons.Automation;

namespace Artisan.UI
{
    internal unsafe class DebugTab
    {
        internal static int offset = 0;
        internal static int SelRecId = 0;
        internal static bool Debug = false;
        public static int DebugValue = 1;

        internal static void Draw()
        {
            try
            {
                ImGui.Checkbox("Debug logging", ref Debug);
                if (ImGui.CollapsingHeader("Crafter's food"))
                {
                    foreach (var x in ConsumableChecker.GetFood())
                    {
                        ImGuiEx.Text($"{x.Id}: {x.Name}");
                    }
                }
                if (ImGui.CollapsingHeader("Crafter's food in inventory"))
                {
                    foreach (var x in ConsumableChecker.GetFood(true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("Crafter's HQ food in inventory"))
                {
                    foreach (var x in ConsumableChecker.GetFood(true, true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id, true);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("Crafter's pots"))
                {
                    foreach (var x in ConsumableChecker.GetPots())
                    {
                        ImGuiEx.Text($"{x.Id}: {x.Name}");
                    }
                }
                if (ImGui.CollapsingHeader("Crafter's pots in inventory"))
                {
                    foreach (var x in ConsumableChecker.GetPots(true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("Crafter's HQ pots in inventory"))
                {
                    foreach (var x in ConsumableChecker.GetPots(true, true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id, true);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("Manuals"))
                {
                    foreach (var x in ConsumableChecker.GetManuals())
                    {
                        ImGuiEx.Text($"{x.Id}: {x.Name}");
                    }
                }
                if (ImGui.CollapsingHeader("Manuals in inventory"))
                {
                    foreach (var x in ConsumableChecker.GetManuals(true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id);
                        }
                    }
                }
                if (ImGui.CollapsingHeader("Squadron Manuals"))
                {
                    foreach (var x in ConsumableChecker.GetSquadronManuals())
                    {
                        ImGuiEx.Text($"{x.Id}: {x.Name}");
                    }
                }
                if (ImGui.CollapsingHeader("SquadronManuals in inventory"))
                {
                    foreach (var x in ConsumableChecker.GetSquadronManuals(true))
                    {
                        if (ImGui.Selectable($"{x.Id}: {x.Name}"))
                        {
                            ConsumableChecker.UseItem(x.Id);
                        }
                    }
                }

                if (ImGui.CollapsingHeader("Crafting Stats"))
                {
                    CurrentCraftMethods.BestSynthesis(out var act);
                    ImGui.Text($"Control: {CharacterInfo.Control}");
                    ImGui.Text($"Craftsmanship: {CharacterInfo.Craftsmanship}");
                    ImGui.Text($"Current Durability: {CurrentCraft.CurrentDurability}");
                    ImGui.Text($"Max Durability: {CurrentCraft.MaxDurability}");
                    ImGui.Text($"Current Progress: {CurrentCraft.CurrentProgress}");
                    ImGui.Text($"Max Progress: {CurrentCraft.MaxProgress}");
                    ImGui.Text($"Current Quality: {CurrentCraft.CurrentQuality}");
                    ImGui.Text($"Max Quality: {CurrentCraft.MaxQuality}");
                    ImGui.Text($"Item name: {CurrentCraft.ItemName}");
                    ImGui.Text($"Current Condition: {CurrentCraft.CurrentCondition}");
                    ImGui.Text($"Current Step: {CurrentCraft.CurrentStep}");
                    ImGui.Text($"Current Quick Synth Step: {CurrentCraft.QuickSynthCurrent}");
                    ImGui.Text($"Max Quick Synth Step: {CurrentCraft.QuickSynthMax}");
                    ImGui.Text($"GS+ByregotCombo: {Calculations.GreatStridesByregotCombo()}");
                    ImGui.Text($"Base Quality: {Calculations.BaseQuality()}");
                    ImGui.Text($"Predicted Quality: {Calculations.CalculateNewQuality(CurrentCraft.CurrentRecommendation)}");
                    ImGui.Text($"Predicted Progress: {Calculations.CalculateNewProgress(CurrentCraft.CurrentRecommendation)}");
                    ImGui.Text($"Macro Step: {CurrentCraft.MacroStep}");
                    ImGui.Text($"Collectibility Low: {CurrentCraft.CollectabilityLow}");
                    ImGui.Text($"Collectibility Mid: {CurrentCraft.CollectabilityMid}");
                    ImGui.Text($"Collectibility High: {CurrentCraft.CollectabilityHigh}");
                    ImGui.Text($"Crafting State: {CurrentCraft.State}");
                    ImGui.Text($"Can Finish: {CurrentCraftMethods.CanFinishCraft(act)}");
                    ImGui.Text($"Current Rec: {CurrentCraft.RecommendationName}");
                    ImGui.Text($"Previous Action: {CurrentCraft.PreviousAction.NameOfAction()}");
                    ImGui.Text($"Tasks?: {Artisan.Tasks.Count}");
                    ImGui.Text($"{CurrentCraft.CurrentStep == 1 && Calculations.CalculateNewProgress(Skills.DelicateSynthesis) >= CurrentCraft.MaxProgress && Calculations.CalculateNewQuality(Skills.DelicateSynthesis) >= CurrentCraft.MaxQuality}");
                }

                if (ImGui.CollapsingHeader("Spiritbonds"))
                {
                    ImGui.Text($"Weapon Spiritbond: {Spiritbond.Weapon}");
                    ImGui.Text($"Off-hand Spiritbond: {Spiritbond.Offhand}");
                    ImGui.Text($"Helm Spiritbond: {Spiritbond.Helm}");
                    ImGui.Text($"Body Spiritbond: {Spiritbond.Body}");
                    ImGui.Text($"Hands Spiritbond: {Spiritbond.Hands}");
                    ImGui.Text($"Legs Spiritbond: {Spiritbond.Legs}");
                    ImGui.Text($"Feet Spiritbond: {Spiritbond.Feet}");
                    ImGui.Text($"Earring Spiritbond: {Spiritbond.Earring}");
                    ImGui.Text($"Neck Spiritbond: {Spiritbond.Neck}");
                    ImGui.Text($"Wrist Spiritbond: {Spiritbond.Wrist}");
                    ImGui.Text($"Ring 1 Spiritbond: {Spiritbond.Ring1}");
                    ImGui.Text($"Ring 2 Spiritbond: {Spiritbond.Ring2}");

                    ImGui.Text($"Spiritbond Ready Any: {Spiritbond.IsSpiritbondReadyAny()}");

                }

                if (ImGui.CollapsingHeader("Quests"))
                {
                    QuestManager* qm = QuestManager.Instance();
                    foreach (var quest in qm->DailyQuestsSpan)
                    {
                        ImGui.TextWrapped($"Quest ID: {quest.QuestId}, Sequence: {QuestManager.GetQuestSequence(quest.QuestId)}, Name: {quest.QuestId.NameOfQuest()}, Flags: {quest.Flags}");
                    }

                }

                if (ImGui.CollapsingHeader("IPC"))
                {
                    ImGui.Text($"AutoRetainer: {AutoRetainer.IsEnabled()}");
                    if (ImGui.Button("Suppress"))
                    {
                        AutoRetainer.Suppress();
                    }
                    if (ImGui.Button("Unsuppress"))
                    {
                        AutoRetainer.Unsuppress();
                    }

                    ImGui.Text($"Endurance IPC: {Svc.PluginInterface.GetIpcSubscriber<bool>("Artisan.GetEnduranceStatus").InvokeFunc()}");
                    if (ImGui.Button("Enable"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetEnduranceStatus").InvokeAction(true);
                    }
                    if (ImGui.Button("Disable"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetEnduranceStatus").InvokeAction(false);
                    }

                    if (ImGui.Button("Send Stop Request (true)"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetStopRequest").InvokeAction(true);
                    }

                    if (ImGui.Button("Send Stop Request (false)"))
                    {
                        Svc.PluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetStopRequest").InvokeAction(false);
                    }

                    foreach (var retainer in Service.Configuration.RetainerIDs.Where(x => x.Value == Svc.ClientState.LocalContentId))
                    {
                        ImGui.Text($"ATools IPC: {RetainerInfo.ATools} {RetainerInfo.GetRetainerInventoryItem(5111, retainer.Key)}");
                    }
                    ImGui.Text($"ATools IPC: {RetainerInfo.ATools} {RetainerInfo.GetRetainerItemCount(5111, false)}");
                }

                if (ImGui.CollapsingHeader("Collectables"))
                {
                    foreach (var item in LuminaSheets.ItemSheet.Values.Where(x => x.IsCollectable).OrderBy(x => x.LevelItem.Row))
                    {
                        if (Svc.Data.GetExcelSheet<CollectablesShopItem>().TryGetFirst(x => x.Item.Row == item.RowId, out var collectibleSheetItem))
                        {
                            if (collectibleSheetItem != null)
                            {
                                ImGui.Text($"{item.Name} - {collectibleSheetItem.CollectablesShopRewardScrip.Value.LowReward}");
                            }
                        }
                    }
                }

                ImGui.Separator();

                if (ImGui.Button("Repair all"))
                {
                    RepairManager.ProcessRepair();
                }
                ImGuiEx.Text($"Gear condition: {RepairManager.GetMinEquippedPercent()}");

                ImGui.Text($"Endurance Item: {Endurance.RecipeID} {Endurance.RecipeName}");
                if (ImGui.Button($"Open Endurance Item"))
                {
                    CraftingListFunctions.OpenRecipeByID(Endurance.RecipeID);
                }

                ImGui.InputInt("Debug Value", ref DebugValue);

                ImGui.Text($"Item Count? {CraftingListUI.NumberOfIngredient((uint)DebugValue)}");

                ImGui.Text($"Completed Recipe? {((uint)DebugValue).NameOfRecipe()} {P.ri.HasRecipeCrafted((uint)DebugValue)}");

                if (ImGui.Button($"Open And Quick Synth"))
                {
                    CurrentCraftMethods.QuickSynthItem(DebugValue);
                }
                if (ImGui.Button($"Close Quick Synth Window"))
                {
                    CurrentCraftMethods.CloseQuickSynthWindow();
                }
                if (ImGui.Button($"Open Materia Window"))
                {
                    Spiritbond.OpenMateriaMenu();
                }
                if (ImGui.Button($"Extract First Materia"))
                {
                    Spiritbond.ExtractFirstMateria();
                }
            }
            catch (Exception e)
            {
                e.Log();
            }


        }

        public class Item
        {
            public uint Key { get; set; }
            public string Name { get; set; }
            public ushort CraftingTime { get; set; }
            public uint UIIndex { get; set; }
        }
    }
}
