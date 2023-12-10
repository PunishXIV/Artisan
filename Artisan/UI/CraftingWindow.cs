using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.CraftingLogic.Solvers;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System;
using System.Linq;

namespace Artisan.UI
{
    internal class CraftingWindow : Window
    {
        public bool repeatTrial = false;

        public CraftingWindow() : base("Artisan Crafting Window###MainCraftWindow", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
        {
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            this.SizeConstraints = new()
            {
                MinimumSize = new System.Numerics.Vector2(150f, 0f),
                MaximumSize = new System.Numerics.Vector2(310f, 500f)
            };
        }

        public override bool DrawConditions()
        {
            return P.PluginUi.CraftingVisible;
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

        public static TimeSpan MacroTime = new();
        public override void Draw()
        {
            if (!P.Config.DisableHighlightedAction)
                Hotbars.MakeButtonsGlow(CurrentCraft.CurrentRecommendation);

            if (ImGuiEx.AddHeaderIcon("OpenConfig", FontAwesomeIcon.Cog, new ImGuiEx.HeaderIconOptions() { Tooltip = "Open Config" }))
            {
                P.PluginUi.IsOpen = true;
            }

            var solver = CurrentCraft.CurrentRecipe != null && CurrentCraft.CurCraftState != null ? P.GetSolverForRecipe(CurrentCraft.CurrentRecipe.RowId, CurrentCraft.CurCraftState) : default;

            if (CurrentCraft.CurrentRecipe?.IsExpert ?? false)
            {
                if (solver.solver is StandardSolver)
                {
                    ImGui.Dummy(new System.Numerics.Vector2(12f));
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudRed, "This is an expert recipe. It is strongly recommended to use an Artisan macro or manually solve this.", this.SizeConstraints?.MaximumSize.X ?? 0);
                }
                else if (solver.solver is ExpertSolver)
                {
                    ImGui.Dummy(new System.Numerics.Vector2(12f));
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, "This is an expert recipe. You are using the experimental solver currently. Your success rate may vary.", this.SizeConstraints?.MaximumSize.X ?? 0);
                }
            }
            else if (CurrentCraft.CurrentRecipe?.SecretRecipeBook.Row > 0 && CurrentCraft.CurCraftState?.CraftLevel == CurrentCraft.CurCraftState?.StatLevel)
            {
                ImGui.Dummy(new System.Numerics.Vector2(12f));
                ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, "This is a current level master recipe. Your success rate may vary so it is recommended to use an Artisan macro or manually solve this.", this.SizeConstraints?.MaximumSize.X ?? 0);
            }

            bool autoMode = P.Config.AutoMode;
            if (ImGui.Checkbox("Auto Action Mode", ref autoMode))
            {
                if (!autoMode)
                    ActionWatching.BlockAction = false;

                P.Config.AutoMode = autoMode;
                P.Config.Save();
            }

            if (autoMode)
            {
                var delay = P.Config.AutoDelay;
                ImGui.PushItemWidth(200);
                if (ImGui.SliderInt("Set delay (ms)", ref delay, 0, 1000))
                {
                    if (delay < 0) delay = 0;
                    if (delay > 1000) delay = 1000;

                    P.Config.AutoDelay = delay;
                    P.Config.Save();
                }
            }

            if (Endurance.RecipeID != 0 && !CraftingListUI.Processing && Endurance.Enable)
            {
                if (ImGui.Button("Disable Endurance"))
                {
                    Endurance.Enable = false;
                }
            }

            if (!Endurance.Enable && CurrentCraft.DoingTrial)
                ImGui.Checkbox("Trial Craft Repeat", ref repeatTrial);

            var text = $"Using {solver.solver.Name(solver.flavour)}";
            if (CurrentCraft.CurrentRecommendationComment.Length > 0)
                text += $" ({CurrentCraft.CurrentRecommendationComment})";
            ImGui.TextWrapped(text);

            if (P.Config.CraftingX && Endurance.Enable)
                ImGui.Text($"Remaining Crafts: {P.Config.CraftX}");

            if (P.Config.AutoMode && MacroTime.Ticks > 0)
            {
                string duration = string.Format("{0:D2}h {1:D2}m {2:D2}s", MacroTime.Hours, MacroTime.Minutes, MacroTime.Seconds);
                ImGui.Text($"Approximate Remaining Duration: {duration}");
            }

            if (!P.Config.AutoMode)
            {
                ImGui.Text("Semi-Manual Mode");

                if (ImGui.Button("Execute recommended action"))
                {
                    Hotbars.ExecuteRecommended(CurrentCraft.CurrentRecommendation);
                }
                if (ImGui.Button("Fetch Recommendation"))
                {
                    Artisan.Tasks.Clear();
                    P.FetchRecommendation();
                }
            }
        }
    }
}
