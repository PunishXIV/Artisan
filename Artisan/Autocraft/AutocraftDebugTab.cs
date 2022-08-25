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
        internal static void Draw()
        {
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
            if(ImGui.Button("Repair all"))
            {
                RepairManager.Repair();
            }
            ImGuiEx.Text($"Gear condition: {RepairManager.GetMinEquippedPercent()}");
            ImGuiEx.Text($"Selected recipe: {AgentRecipeNote.Instance()->SelectedRecipeIndex}");
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
            
            ImGui.Text($"{CurrentCraft.CurrentDurability}");
            ImGui.Text($"{CurrentCraft.MaxDurability}");
            ImGui.Text($"{CurrentCraft.CurrentProgress}");
            ImGui.Text($"{CurrentCraft.MaxProgress}");
            ImGui.Text($"{CurrentCraft.CurrentQuality}");
            ImGui.Text($"{CurrentCraft.MaxQuality}");
            ImGui.Text($"{CurrentCraft.ItemName}");
            ImGui.Text($"{CurrentCraft.CurrentCondition}");
            ImGui.Text($"{CurrentCraft.CurrentStep}");



        }
    }
}
