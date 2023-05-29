using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.MacroSystem;
using Artisan.RawInformation;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan.UI
{
    internal class CraftingWindow : Window
    {
#if DEBUG
        public bool repeatTrial = false;
#endif

        public CraftingWindow() : base("Artisan Crafting Window###MainCraftWindow", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize)
        {
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
        }

        public override bool DrawConditions()
        {
            return P.PluginUi.CraftingVisible;
        }

        public override void PreDraw()
        {
            if (!P.config.DisableTheme)
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

        public override void Draw()
        {
            if (!Service.Configuration.DisableHighlightedAction)
                Hotbars.MakeButtonsGlow(CurrentRecommendation);

            if (ImGuiEx.AddHeaderIcon("OpenConfig", FontAwesomeIcon.Cog, new ImGuiEx.HeaderIconOptions() { Tooltip = "Open Config" }))
            {
                P.PluginUi.IsOpen = true;
            }

            bool autoMode = Service.Configuration.AutoMode;

            if (ImGui.Checkbox("Auto Action Mode", ref autoMode))
            {
                Service.Configuration.AutoMode = autoMode;
                Service.Configuration.Save();
            }

            if (autoMode)
            {
                var delay = Service.Configuration.AutoDelay;
                ImGui.PushItemWidth(200);
                if (ImGui.SliderInt("Set delay (ms)", ref delay, 0, 1000))
                {
                    if (delay < 0) delay = 0;
                    if (delay > 1000) delay = 1000;

                    Service.Configuration.AutoDelay = delay;
                    Service.Configuration.Save();
                }
            }


            if (Handler.RecipeID != 0 && !CraftingListUI.Processing)
            {
                bool enable = Handler.Enable;
                if (ImGui.Checkbox("Endurance Mode Toggle", ref enable))
                {
                    Handler.Enable = enable;
                }
            }

            if (Service.Configuration.CraftingX && Handler.Enable)
            {
                ImGui.Text($"Remaining Crafts: {Service.Configuration.CraftX}");
                if (Service.Configuration.IRM.TryGetValue((uint)Handler.RecipeID, out var prevMacro))
                {
                    Macro? macro = Service.Configuration.UserMacros.First(x => x.ID == prevMacro);
                    if (macro != null)
                    {
                        Double timeInSeconds = ((MacroUI.GetMacroLength(macro) * Service.Configuration.CraftX) + (Service.Configuration.CraftX * 2)); // Counting crafting duration + 2 seconds between crafts.
                        TimeSpan t = TimeSpan.FromSeconds(timeInSeconds);
                        string duration = string.Format("{0:D2}h {1:D2}m {2:D2}s", t.Hours, t.Minutes, t.Seconds);

                        ImGui.Text($"Approximate Remaining Duration: {duration}");
                    }
                }
            }

#if DEBUG
            ImGui.Checkbox("Trial Craft Repeat", ref repeatTrial);
#endif

            if (!Service.Configuration.AutoMode)
            {
                ImGui.Text("Semi-Manual Mode");

                if (ImGui.Button("Execute recommended action"))
                {
                    Hotbars.ExecuteRecommended(CurrentRecommendation);
                }
                if (ImGui.Button("Fetch Recommendation"))
                {
                    FetchRecommendation(CurrentStep);
                }
            }
        }
    }
}
