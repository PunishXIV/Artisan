using Artisan.CraftingLogic;
using Artisan.RawInformation;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.Autocraft
{
    internal unsafe static class AutocraftDebugTab
    {
        internal static int offset = 0;
        internal static int SelRecId = 0;
        internal static bool Debug = false;

        public static int DebugValue = 0;

        internal static void Draw()
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

                ImGui.Text($"Control: {CharacterInfo.Control()}");
                ImGui.Text($"Craftsmanship: {CharacterInfo.Craftsmanship()}");
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
                ImGui.Text($"GS+ByregotCombo: {CurrentCraft.GreatStridesByregotCombo()}");
                ImGui.Text($"Base Quality: {CurrentCraft.BaseQuality()}");
                ImGui.Text($"Predicted Quality: {CurrentCraft.CalculateNewQuality(CurrentCraft.CurrentRecommendation)}");
                ImGui.Text($"Predicted Progress: {CurrentCraft.CalculateNewProgress(CurrentCraft.CurrentRecommendation)}");
                ImGui.Text($"Macro Step: {CurrentCraft.MacroStep}");
                ImGui.Text($"Collectibility Low: {CurrentCraft.CollectabilityLow}");
                ImGui.Text($"Collectibility Mid: {CurrentCraft.CollectabilityMid}");
                ImGui.Text($"Collectibility High: {CurrentCraft.CollectabilityHigh}");
                ImGui.Text($"Crafting State: {CurrentCraft.State}");
                ImGui.Text($"Can Finish: {CurrentCraft.CanFinishCraft()}");
                ImGui.Text($"Previous Action: {CurrentCraft.PreviousAction.NameOfAction()}");
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
            ImGui.Separator();

            if (ImGui.Button("Repair all"))
            {
                RepairManager.ProcessRepair();
            }
            ImGuiEx.Text($"Gear condition: {RepairManager.GetMinEquippedPercent()}");

            if (ImGui.Button($"Open Endurance Item"))
            {
                CraftingLists.CraftingListFunctions.OpenRecipeByID((uint)Handler.RecipeID);
            }

            ImGui.InputInt("Debug Value", ref DebugValue);

            if (ImGui.Button($"Open And Quick Synth"))
            {
                CurrentCraft.QuickSynthItem(DebugValue);
            }
            if (ImGui.Button($"Close Quick Synth Window"))
            {
                CurrentCraft.CloseQuickSynthWindow();
            }
            if (ImGui.Button($"Open Materia Window"))
            {
                Spiritbond.OpenMateriaMenu();
            }
            if (ImGui.Button($"Extract First Materia"))
            {
                Spiritbond.ExtractFirstMateria();
            }


            /*ImGui.InputInt("id", ref SelRecId);
            if (ImGui.Button("OpenRecipeByRecipeId"))
            {
                AgentRecipeNote.Instance()->OpenRecipeByRecipeId((uint)SelRecId);
            }
            if (ImGui.Button("OpenRecipeByItemId"))
            {
                AgentRecipeNote.Instance()->OpenRecipeByItemId((uint)SelRecId);
            }*/
            //ImGuiEx.Text($"Selected recipe id: {*(int*)(((IntPtr)AgentRecipeNote.Instance()) + 528)}");




        }
    }
}
