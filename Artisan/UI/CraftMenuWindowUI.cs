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

        public bool SolverIs(RecipeConfig config, string type)
        {
            // if no solver is loaded, check the default so things can render correctly
            bool solverLoaded = config.CurrentSolverType != "";
            switch (type)
            {
                case "standard": 
                    return solverLoaded ? config.SolverIsStandard : !LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert;
                case "expert": 
                    return solverLoaded ? config.SolverIsExpert : LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert;
                case "raph": 
                case "raphael":
                    return solverLoaded ? config.SolverIsRaph : false;
                default: return false;
            }
        }

        public override void Draw()
        {
            try
            {
                if (!IsOpen)
                {
                    return;
                }
                var changed = false;
                var foundRecipe = P.Config.RecipeConfigs.GetValueOrDefault(Endurance.RecipeID);
                var config = foundRecipe ?? new();
                var autoMode = P.Config.AutoMode;

                // save a new config entry for expert recipes so per-recipe settings work as expected
                if (foundRecipe == null && LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert)
                    changed = true;

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
                if (Crafting.MaterialMiracleCharges() > 0 && (SolverIs(config, "standard") || SolverIs(config, "expert")))
                {
                    bool useMatMiracle = LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert ? expCfg.OverrideCosmicRecipeSettings ? expCfg.UseMaterialMiracle : config.ExpertUseMaterialMiracle : P.Config.UseMaterialMiracle;
                    int delayMatMiracle = LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert ? expCfg.OverrideCosmicRecipeSettings ? expCfg.MinimumStepsBeforeMiracle : (int)config.ExpertMinimumStepsBeforeMiracle : P.Config.MinimumStepsBeforeMiracle;
                    bool multiMatMiracle = P.Config.MaterialMiracleMulti;

                    string miracleStr = SolverIs(config, "expert") ? "[ex] Use [s!MaterialMiracle]" : "Use [s!MaterialMiracle]";
                    if (expCfg.OverrideCosmicRecipeSettings && SolverIs(config, "expert"))
                    {
                        ImGui.TextWrapped("These settings are overridden by your current expert profile.\r\nDisable that option to set it for each recipe.");
                        ImGui.BeginDisabled();
                    }
                    if (ExpertSettingsUI.CheckboxWithIcons("useMatMiracle", ref useMatMiracle, miracleStr))
                    {
                        if (LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert)
                        {
                            if (expCfg.OverrideCosmicRecipeSettings)
                                expCfg.UseMaterialMiracle = useMatMiracle;
                            else
                                config.expertUseMaterialMiracle = useMatMiracle;
                        }
                        else
                            P.Config.UseMaterialMiracle = useMatMiracle;
                        changed = true;
                    }

                    if (expCfg.OverrideCosmicRecipeSettings && SolverIs(config, "expert")) ImGui.EndDisabled();
                    ImGuiComponents.HelpMarker($"This setting only applies to the standard and expert solvers.\r\nTo change Raphael solver usage, go to Settings > Raphael Solver Settings.");

                    if (expCfg.OverrideCosmicRecipeSettings && SolverIs(config, "expert")) ImGui.BeginDisabled();

                    if (useMatMiracle)
                    {
                        ImGui.Text("After this many steps:");
                        if (ImGui.SliderInt("###MaterialMiracleSlider", ref delayMatMiracle, 0, 20))
                        {
                            if (LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert)
                            {
                                if (expCfg.OverrideCosmicRecipeSettings)
                                    expCfg.MinimumStepsBeforeMiracle = delayMatMiracle;
                                else
                                    config.expertMinimumStepsBeforeMiracle = (uint)delayMatMiracle;
                            }
                            else
                                P.Config.MinimumStepsBeforeMiracle = delayMatMiracle;
                            changed = true;
                        }

                        if (false == LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert)
                        {
                            if (ImGui.Checkbox("Use Multiple Material Miracles", ref multiMatMiracle))
                                P.Config.MaterialMiracleMulti = multiMatMiracle;
                        }
                    }
                    if (expCfg.OverrideCosmicRecipeSettings && SolverIs(config, "expert")) ImGui.EndDisabled();
                }

                // todo: should this set the raph setting, not just tell users where to set it?
                if (Crafting.SteadyHandCharges() > 0)
                {
                    if (LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert && SolverIs(config, "expert"))
                    {
                        int maxSteady = expCfg.OverrideCosmicRecipeSettings ? expCfg.MaxSteadyUses : (int)config.ExpertMaxSteadyUses;

                        ImGui.PushItemWidth(100);
                        if (expCfg.OverrideCosmicRecipeSettings && SolverIs(config, "expert"))
                        {
                            ImGui.TextWrapped("This setting is overridden by your current expert profile.\r\nDisable that option to set it for each recipe.");
                            ImGui.BeginDisabled();
                        }
                        if (ExpertSettingsUI.SliderIntWithIcons("MaxSteadyUses", ref maxSteady, 0, 2, "[ex] Max [s!SteadyHand] uses"))
                        {
                            if (expCfg.OverrideCosmicRecipeSettings)
                                expCfg.MaxSteadyUses = maxSteady;
                            else
                                config.expertMaxSteadyUses = (uint)maxSteady;
                            changed = true;
                        }
                        if (expCfg.OverrideCosmicRecipeSettings) ImGui.EndDisabled();
                        ImGuiComponents.HelpMarker($"This setting only applies to the expert solver.\r\nTo change Raphael solver usage, go to Settings > Raphael Solver Settings.");
                    }
                    else if (config.SolverIsRaph || config.SolverIsStandard)
                    {
                        ImGui.TextWrapped($"This mission supports {Skills.SteadyHand.NameOfAction()}. To configure its usage for the Raphael solver, go to Settings > Raphael Solver Settings.");
                    }
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

                    changed |= config.Draw(Endurance.RecipeID);

                    if (changed)
                    {
                        P.Config.RecipeConfigs[Endurance.RecipeID] = config;
                        P.Config.Save();
                    }
                }
            }
            catch { }
        }
    }
}
