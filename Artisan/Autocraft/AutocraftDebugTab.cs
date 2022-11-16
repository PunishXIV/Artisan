using Artisan.CraftingLogic;
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

            if (ImGui.CollapsingHeader("Crafting Stats"))
            {
                ImGui.Text($"Current Durability: {CurrentCraft.CurrentDurability}");
                ImGui.Text($"Max Durability: {CurrentCraft.MaxDurability}");
                ImGui.Text($"Current Progress: {CurrentCraft.CurrentProgress}");
                ImGui.Text($"Max Progress: {CurrentCraft.MaxProgress}");
                ImGui.Text($"Current Quality: {CurrentCraft.CurrentQuality}");
                ImGui.Text($"Max Quality: {CurrentCraft.MaxQuality}");
                ImGui.Text($"Item name: {CurrentCraft.ItemName}");
                ImGui.Text($"Current Condition: {CurrentCraft.CurrentCondition}");
                ImGui.Text($"Current Step: {CurrentCraft.CurrentStep}");
                ImGui.Text($"GS+ByregotCombo: {CurrentCraft.GreatStridesByregotCombo()}");
                ImGui.Text($"Predicted Quality: {CurrentCraft.CalculateNewQuality(CurrentCraft.CurrentRecommendation)}");
            }
            ImGui.Separator();

            if (ImGui.Button("Repair all"))
            {
                RepairManager.ProcessRepair();
            }
            ImGuiEx.Text($"Gear condition: {RepairManager.GetMinEquippedPercent()}");
            ImGuiEx.Text($"Selected recipe: {AgentRecipeNote.Instance()->SelectedRecipeIndex}");
            ImGuiEx.Text($"Insufficient Materials: {HQManager.InsufficientMaterials}");

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
