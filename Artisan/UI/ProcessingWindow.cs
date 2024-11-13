using Artisan.CraftingLists;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System;

namespace Artisan.UI
{
    internal class ProcessingWindow : Window
    {
        public ProcessingWindow() : base("Processing List###ProcessingList", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
        {
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            SizeCondition = ImGuiCond.Appearing;
        }

        public override bool DrawConditions()
        {
            if (CraftingListUI.Processing)
                return true;

            return false;
        }

        public override void PreDraw()
        {
            if (!P.Config.DisableTheme)
            {
                P.Style.Push();
                P.StylePushed = true;
            }
        }

        public override void PostDraw()
        {
            if (P.StylePushed)
            {
                P.Style.Pop();
                P.StylePushed = false;
            }
        }

        public unsafe override void Draw()
        {
            if (CraftingListUI.Processing)
            {
                CraftingListFunctions.ProcessList(CraftingListUI.selectedList);

                //if (ImGuiEx.AddHeaderIcon("OpenConfig", FontAwesomeIcon.Cog, new ImGuiEx.HeaderIconOptions() { Tooltip = "Open Config" }))
                //{
                //    P.PluginUi.IsOpen = true;
                //}

                ImGui.Text($"Now Processing: {CraftingListUI.selectedList.Name}");
                ImGui.Separator();
                ImGui.Spacing();
                if (CraftingListUI.CurrentProcessedItem != 0)
                {
                    ImGuiEx.TextV($"Crafting: {LuminaSheets.RecipeSheet[CraftingListUI.CurrentProcessedItem].ItemResult.Value.Name.ToString()}");
                    ImGuiEx.TextV($"Current Item Progress: {CraftingListUI.CurrentProcessedItemCount} / {CraftingListUI.CurrentProcessedItemListCount}");
                    ImGuiEx.TextV($"Overall List Progress: {CraftingListFunctions.CurrentIndex + 1} / {CraftingListUI.selectedList.ExpandedList.Count}");

                    string duration = CraftingListFunctions.ListEndTime == TimeSpan.Zero ? "Unknown" : string.Format("{0:D2}d {1:D2}h {2:D2}m {3:D2}s", CraftingListFunctions.ListEndTime.Days, CraftingListFunctions.ListEndTime.Hours, CraftingListFunctions.ListEndTime.Minutes, CraftingListFunctions.ListEndTime.Seconds);
                    ImGuiEx.TextV($"Approximate Remaining Duration: {duration}");

                }

                if (!CraftingListFunctions.Paused)
                {
                    if (ImGui.Button("Pause"))
                    {
                        CraftingListFunctions.Paused = true;
                        P.TM.Abort();
                        CraftingListFunctions.CLTM.Abort();
                        PreCrafting.Tasks.Clear();
                    }
                }
                else
                {
                    if (ImGui.Button("Resume"))
                    {
                        if (Crafting.CurState is Crafting.State.IdleNormal or Crafting.State.IdleBetween)
                        {
                            var recipe = LuminaSheets.RecipeSheet[CraftingListUI.CurrentProcessedItem];
                            PreCrafting.Tasks.Add((() => PreCrafting.TaskSelectRecipe(recipe), default));
                        }

                        CraftingListFunctions.Paused = false;
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    CraftingListUI.Processing = false;
                    CraftingListFunctions.Paused = false;
                    P.TM.Abort();
                    CraftingListFunctions.CLTM.Abort();
                    PreCrafting.Tasks.Clear();
                    Crafting.CraftFinished -= CraftingListUI.UpdateListTimer;
                }
            }
        }
    }
}
