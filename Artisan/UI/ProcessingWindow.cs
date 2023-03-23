using Artisan.CraftingLists;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System.Linq;

namespace Artisan.UI
{
    internal class ProcessingWindow : Window
    {
        public ProcessingWindow() : base("Processing List###ProcessingList", ImGuiWindowFlags.AlwaysAutoResize)
        {
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
        }

        public override bool DrawConditions()
        {
            if (CraftingListUI.Processing)
                return true;

            return false;  
        }

        public override void PreDraw()
        {
            if (!P.config.DisableTheme)
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
                Service.Framework.RunOnFrameworkThread(() => CraftingListFunctions.ProcessList(CraftingListUI.selectedList));
                
                if (ImGuiEx.AddHeaderIcon("OpenConfig", FontAwesomeIcon.Cog, new ImGuiEx.HeaderIconOptions() { Tooltip = "Open Config" }))
                {
                    P.PluginUi.Visible = true;
                }

                ImGui.Text($"Now Processing: {CraftingListUI.selectedList.Name}");
                ImGui.Separator();
                ImGui.Spacing();
                if (CraftingListUI.CurrentProcessedItem != 0)
                {
                    ImGuiEx.TextV($"Trying to craft: {CraftingListUI.FilteredList[CraftingListUI.CurrentProcessedItem].ItemResult.Value.Name.RawString}");
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
                        CraftingListFunctions.OpenCraftingMenu();
                        CraftingListFunctions.Paused = false;
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    CraftingListUI.Processing = false;
                }
            }
        }
    }
}
