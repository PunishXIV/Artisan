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
using System;
using System.Collections.Generic;
using System.Linq;
using static Artisan.CraftingLogic.Solvers.ExpertSolverProfiles;
using static Artisan.CraftingLogic.Solvers.ExpertSolverSettings;

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
                var expertRecipe = LuminaSheets.RecipeSheet[Endurance.RecipeID].IsExpert;

                // save a new config entry for expert recipes so per-recipe settings work as expected
                if (foundRecipe == null && expertRecipe)
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
                    int maxMiracles = SolverIs(config, "expert") ? expCfg.OverrideCosmicRecipeSettings ? expCfg.MaxMaterialMiracleUses : (int)config.ExpertMaxMaterialMiracleUses : P.Config.StandardMMUses;
                    int delayMatMiracle = SolverIs(config, "expert") ? expCfg.OverrideCosmicRecipeSettings ? expCfg.MinimumStepsBeforeMiracle : (int)config.ExpertMinimumStepsBeforeMiracle : P.Config.StandardMMSteps;
                    MMSet useMMWhen = SolverIs(config, "expert") ? expCfg.OverrideCosmicRecipeSettings ? expCfg.UseMMWhen : config.expertUseMMWhen : MMSet.Steps;

                    if (expCfg.OverrideCosmicRecipeSettings && SolverIs(config, "expert"))
                    {
                        ImGui.TextWrapped("These settings are overridden by your current expert profile.\r\nDisable that option to set it for each recipe.");
                        ImGui.BeginDisabled();
                    }

                    ImGui.PushItemWidth(100);
                    if (ExpertSettingsUI.SliderIntWithIcons("MaxMaterialMiracles", ref maxMiracles, 0, 3, $"{(SolverIs(config, "expert") ? "[ex] " : "")}Max [s!MaterialMiracle] uses"))
                    {
                        if (SolverIs(config, "expert"))
                        {
                            if (expCfg.OverrideCosmicRecipeSettings)
                                expCfg.MaxMaterialMiracleUses = maxMiracles;
                            else
                                config.expertMaxMaterialMiracleUses = (uint)maxMiracles;
                        }
                        else
                            P.Config.MaxMaterialMiracles = maxMiracles;
                        changed = true;
                    }

                    if (expCfg.OverrideCosmicRecipeSettings && SolverIs(config, "expert")) ImGui.EndDisabled();
                    var mmNote = "To change Raphael solver usage, go to Settings > Raphael Solver Settings.";
                    if (SolverIs(config, "expert"))
                        ImGuiComponents.HelpMarker($"This setting only applies to the expert solver.\r\n{mmNote}");
                    if (SolverIs(config, "standard"))
                        ImGuiComponents.HelpMarker($"This will switch the Standard Recipe Solver over to the Expert Solver for the duration of the buff.\r\n{mmNote}");

                    if (expCfg.OverrideCosmicRecipeSettings && SolverIs(config, "expert")) ImGui.BeginDisabled();

                    if (maxMiracles > 0)
                    {
                        if (SolverIs(config, "expert"))
                        {
                            ImGui.PushItemWidth(250);
                            if (ImGui.BeginCombo("##mmSet", expCfg.GetMMSet(useMMWhen)))
                            {
                                foreach (MMSet x in Enum.GetValues<MMSet>())
                                {
                                    if (ImGui.Selectable(expCfg.GetMMSet(x)))
                                    {
                                        if (expCfg.OverrideCosmicRecipeSettings)
                                            expCfg.UseMMWhen = x;
                                        else
                                            config.expertUseMMWhen = x;
                                        changed = true;
                                    }
                                }
                                ImGui.EndCombo();
                            }
                            ImGui.SameLine(0.0f, 4.0f);
                            ExpertSettingsUI.DrawIconText("[ex] When to start");
                        }
                        else
                            ImGui.Text("Use after this many steps:");

                        if ((SolverIs(config, "expert") && useMMWhen == MMSet.Steps) || SolverIs(config, "standard"))
                        {
                            ImGui.PushItemWidth(250);
                            if (ImGui.SliderInt($"###MaterialMiracleSlider", ref delayMatMiracle, 0, 20))
                            {
                                if (SolverIs(config, "expert"))
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
                            if (SolverIs(config, "expert"))
                            {
                                ImGui.SameLine(0.0f, 4.0f);
                                ExpertSettingsUI.DrawIconText("[ex] Number of steps");
                            }
                        }
                    }
                    if (expCfg.OverrideCosmicRecipeSettings && SolverIs(config, "expert")) ImGui.EndDisabled();
                }

                // todo: should this set the raph setting, not just tell users where to set it?
                if (Crafting.SteadyHandCharges() > 0)
                {
                    if (expertRecipe && SolverIs(config, "expert"))
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
