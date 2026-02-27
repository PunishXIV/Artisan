using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using System.Collections.Generic;
using System.Linq;
using static Artisan.CraftingLogic.Solvers.ExpertSolverProfiles;

namespace Artisan.UI
{
    internal class CraftMenuWindowUI : Window
    {
        public bool EnableMacroOptions { get; set; }
        public ExpertSolverSettingsUI ExpertSettingsUI = new();

        public CraftMenuWindowUI(string windowName, ImGuiWindowFlags flags) : base(windowName, flags)
        {
            IsOpen = false;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            DisableWindowSounds = true;
            PositionCondition = ImGuiCond.Appearing;

            TitleBarButtons.Add(new()
            {
                Icon = FontAwesomeIcon.Cog,
                ShowTooltip = () => ImGui.SetTooltip("Open Config"),
                Click = (x) => P.PluginUi.IsOpen = true,
            });
        }

        public override bool DrawConditions()
        {
            return IsOpen;
        }

        public override void PreDraw()
        {
            if (P.Config.DisableTheme)
            {
                return;
            }

            P.Style.Push();
            P.StylePushed = true;
        }

        public override void PostDraw()
        {
            if (!P.StylePushed)
            {
                return;
            }

            P.Style.Pop();
            P.StylePushed = false;
        }

        public override void Draw()
        {
            try
            {
                if (!IsOpen)
                {
                    return;
                }
                var config = P.Config.RecipeConfigs.GetValueOrDefault(Endurance.RecipeID) ?? new();
                var autoMode = P.Config.AutoMode;

                if (ImGui.Checkbox("Automatic Action Execution Mode", ref autoMode))
                {
                    P.Config.AutoMode = autoMode;
                    P.Config.Save();
                }

                var enable = Endurance.Enable;
                var recipe = LuminaSheets.RecipeSheet!.First(x => x.Key == Endurance.RecipeID).Value;

                if (!CraftingListFunctions.HasItemsForRecipe(Endurance.RecipeID) && !Endurance.Enable)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.Checkbox("Endurance Mode Toggle", ref enable))
                {
                    Endurance.ToggleEndurance(enable);
                }

                if (!CraftingListFunctions.HasItemsForRecipe(Endurance.RecipeID) && !Endurance.Enable)
                {
                    ImGui.EndDisabled();
                    ImGuiEx.Text(ImGuiColors.DalamudYellow, $"Missing Ingredients:\r\n- {string.Join("\r\n- ", PreCrafting.MissingIngredients(recipe))}");
                }

                ExpertProfile profile = CraftingProcessor.GetExpertProfileForRecipe(config);
                ExpertSolverSettings expCfg = profile.ID == 0 ? P.Config.ExpertSolverConfig : profile.Settings;
                if (Crafting.MaterialMiracleCharges() > 0 && (config.SolverIsStandard || config.SolverIsExpert))
                {
                    bool useMatMiracle = LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert ? expCfg.UseMaterialMiracle : P.Config.UseMaterialMiracle;
                    int delayMatMiracle = LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert ? expCfg.MinimumStepsBeforeMiracle : P.Config.MinimumStepsBeforeMiracle;
                    bool multiMatMiracle = P.Config.MaterialMiracleMulti;

                    string miracleStr = config.SolverIsExpert ? "[ex] Use [s!MaterialMiracle]" : "Use [s!MaterialMiracle]";
                    if (ExpertSettingsUI.CheckboxWithIcons("useMatMiracle", ref useMatMiracle, miracleStr))
                    {
                        if (LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert)
                            expCfg.UseMaterialMiracle = useMatMiracle;
                        else
                            P.Config.UseMaterialMiracle = useMatMiracle;
                    }
                    ImGuiComponents.HelpMarker($"This setting only applies to the standard and expert solvers. To change Raphael solver usage, go to Settings > Raphael Solver Settings.");
                    if (useMatMiracle)
                    {
                        ImGui.Text("After this many steps:");
                        if (ImGui.SliderInt("###MaterialMiracleSlider", ref delayMatMiracle, 0, 20))
                        {
                            if (LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert)
                                expCfg.MinimumStepsBeforeMiracle = delayMatMiracle;
                            else
                                P.Config.MinimumStepsBeforeMiracle = delayMatMiracle;
                        }

                        if (false == LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert)
                        {
                            if (ImGui.Checkbox("Use Multiple Material Miracles", ref multiMatMiracle))
                                P.Config.MaterialMiracleMulti = multiMatMiracle;
                        }
                    }
                }

                // todo: this should also reference the raphael steady setting for non-experts
                if (Crafting.SteadyHandCharges() > 0 && LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert)
                {
                    ImGui.PushItemWidth(100);
                    ExpertSettingsUI.SliderIntWithIcons("MaxSteadyUses", ref expCfg.MaxSteadyUses, 0, 2, "[ex] Max [s!SteadyHand] uses");
                    ImGuiComponents.HelpMarker($"This setting only applies to the current expert solver profile. To change Raphael solver usage, go to Settings > Raphael Solver Settings.");
                }

                if (EnableMacroOptions)
                {
                    ImGui.Spacing();

                    if (SimpleTweaks.IsFocusTweakEnabled())
                    {
                        ImGuiEx.TextWrapped(ImGuiColors.DalamudRed, $@"Warning: You have the ""Auto Focus Recipe Search"" SimpleTweak enabled. This is highly incompatible with Artisan and is recommended to disable it.");
                    }

                    if (Endurance.RecipeID == 0)
                    {
                        return;
                    }

                    if (!config.Draw(Endurance.RecipeID))
                    {
                        return;
                    }

                    P.Config.RecipeConfigs[Endurance.RecipeID] = config;
                    P.Config.Save();
                }
            }
            catch { }
        }
    }
}
