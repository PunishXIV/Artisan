using Artisan.CraftingLists;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ImGuiNET;

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
                ImGui.PushFont(P.CustomFont);
                P.StylePushed = true;
            }
        }

        public override void PostDraw()
        {
            if (P.StylePushed)
            {
                P.Style.Pop();
                ImGui.PopFont();
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
                    ImGuiEx.TextV($"Crafting: {CraftingListHelpers.FilteredList[CraftingListUI.CurrentProcessedItem].ItemResult.Value.Name.RawString}");
                    ImGuiEx.TextV($"Overall Progress: {CraftingListFunctions.CurrentIndex + 1} / {CraftingListUI.selectedList.Items.Count}");
                }

                if (!CraftingListFunctions.Paused)
                {
                    if (ImGui.Button("Pause"))
                    {
                        CraftingListFunctions.Paused = true;
                    }
                }
                else
                {
                    if (ImGui.Button("Resume"))
                    {
                        if (CraftingListFunctions.RecipeWindowOpen())
                            CraftingListFunctions.CloseCraftingMenu();

                        P.TM.Enqueue(() => CraftingListFunctions.OpenRecipeByID(CraftingListUI.CurrentProcessedItem, true));

                        CraftingListFunctions.Paused = false;
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    CraftingListUI.Processing = false;
                    CraftingListFunctions.Paused = false;
                    P.TM.Abort();
                }
            }
        }
    }
}
