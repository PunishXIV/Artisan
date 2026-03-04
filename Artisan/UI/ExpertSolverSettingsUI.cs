using Artisan.CraftingLogic.CraftData;
using Artisan.CraftingLogic.Solvers;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using PunishLib.ImGuiMethods;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using static Artisan.CraftingLogic.Solvers.ExpertSolverSettings;
using static Artisan.RawInformation.AddonExtensions;

namespace Artisan.UI;

internal class ExpertSolverSettingsUI
{
    public IDalamudTextureWrap? expertIcon;

    public enum SkillIconID
    {
        BasicSynthesis = 1501, 
        CarefulSynthesis = 1986, 
        RapidSynthesis = 1988, 
        Groundwork = 1518, 
        IntensiveSynthesis = 1514, 
        PrudentSynthesis = 1520, 
        MuscleMemory = 1994, 

        BasicTouch = 1502, 
        StandardTouch = 1516, 
        AdvancedTouch = 1519, 
        HastyTouch = 1989, 
        PreparatoryTouch = 1507, 
        PreciseTouch = 1524, 
        PrudentTouch = 1535, 
        TrainedFinesse = 1997, 
        Reflect = 1982, 
        RefinedTouch = 1522, 
        DaringTouch = 1998, 

        ByregotsBlessing = 1975, 
        TrainedEye = 1981, 
        DelicateSynthesis = 1503, 

        Veneration = 1995, 
        Innovation = 1987, 
        GreatStrides = 1955, 
        TricksOfTrade = 1990, 
        MastersMend = 1952, 
        Manipulation = 1985, 
        WasteNot = 1992, 
        WasteNot2 = 1993, 
        Observe = 1954, 
        CarefulObservation = 1984, 
        FinalAppraisal = 1983, 
        HeartAndSoul = 1996, 
        QuickInnovation = 1999, 
        ImmaculateMend = 1950, 
        TrainedPerfection = 1926, 

        MaterialMiracle = 61277, 
        SteadyHand = 1953, 
    }

    public Dictionary<string, Vector4> ConditionColors = new()
    {
        { "Normal",    new(1.000f, 1.000f, 1.000f, 1f) },
        { "Centered",  new(0.949f, 0.863f, 0.137f, 1f) },
        { "Sturdy",    new(0.153f, 0.718f, 0.871f, 1f) },
        { "Pliant",    new(0.043f, 0.831f, 0.043f, 1f) },
        { "Malleable", new(0.200f, 0.400f, 1.000f, 1f) },
        { "Primed",    new(0.769f, 0.220f, 0.984f, 1f) },
        { "Good",      new(1.000f, 0.353f, 0.408f, 1f) },
        { "Good Omen", new(1.000f, 0.843f, 0.722f, 1f) },
        { "Robust",    new(0.373f, 0.773f, 1.000f, 1f) },
    };

    public ExpertSolverSettingsUI()
    {
        var tex = Svc.PluginInterface.UiBuilder.LoadUld("ui/uld/RecipeNoteBook.uld");
        expertIcon = tex?.LoadTexturePart("ui/uld/RecipeNoteBook_hr1.tex", 14);
    }

    public bool DrawOpenerSettings(ExpertSolverSettings s)
    {
        bool changed = false;
        try
        {
            changed |= CheckboxWithIcons("UseReflectOpener", ref s.UseReflectOpener, "Use [s!Reflect] instead of [s!MuscleMemory]");
            ImGui.Dummy(new Vector2(0, 5f));
            if (!s.UseReflectOpener)
            {
                DrawIconText("These settings only apply while [s!MuscleMemory] is active at the start of a craft.", color: ImGuiColors.DalamudYellow);
                ImGui.Dummy(new Vector2(0, 5f));

                changed |= CheckboxWithIcons("MuMeIntensiveGood", ref s.MuMeIntensiveGood, "When [c!Good], prioritize [s!IntensiveSynthesis] (400%) over [s!RapidSynthesis] (500%)");
                changed |= CheckboxWithIcons("MuMeIntensiveMalleable", ref s.MuMeIntensiveMalleable, "When [c!Malleable], use [s!HeartAndSoul] → [s!IntensiveSynthesis] (if available)");
                changed |= CheckboxWithIcons("MuMePrimedManip", ref s.MuMePrimedManip, "When [c!Primed] and [s!Veneration] is already active, use [s!Manipulation]");
                HelpMarkerWithIcons("If this is disabled, [s!Manipulation] will only be used during [c!Pliant] while [s!MuscleMemory] is active.");
                changed |= CheckboxWithIcons("MuMeAllowObserve", ref s.MuMeAllowObserve, "When [c!Normal] or other irrelevant {0}, use [s!Observe] instead of [s!RapidSynthesis]", [ConditionString.ToLower()]);
                HelpMarkerWithIcons("This saves {0} at the cost of [s!MuscleMemory] steps.", [DurabilityString.ToLower()]);
                changed |= CheckboxWithIcons("MuMeIntensiveLastResort", ref s.MuMeIntensiveLastResort, "When 1 step left on [s!MuscleMemory] and not [c!Centered], use [s!IntensiveSynthesis] (via [s!HeartAndSoul] if necessary)");
                HelpMarkerWithIcons("[s!RapidSynthesis] will still be used if the last step is [c!Centered].");

                ImGui.Dummy(new Vector2(0, 5f));
                DrawIconText("Use these skills only if [s!MuscleMemory] has more than this many steps left:");
                ImGuiComponents.HelpMarker($"The solver will still only use these skills under an appropriate {ConditionString.ToLower()}.");
                // these have a minimum of 1 to avoid using a buff on the final turn of MuMe
                ImGui.PushItemWidth(250);
                SliderIntWithIcons("MuMeMinStepsForManip", ref s.MuMeMinStepsForManip, 1, 5, "[s!Manipulation]");
                ImGui.PushItemWidth(250);
                SliderIntWithIcons("MuMeMinStepsForVene", ref s.MuMeMinStepsForVene, 1, 5, "[s!Veneration]");
            }
        }
        catch (Exception ex)
        {
            ex.Log();
        }
        return changed;
    }

    public bool DrawPreQualitySettings(ExpertSolverSettings s)
    {
        bool changed = false;
        try
        {
            ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"These settings apply after the opener, but before reaching max {Buffs.InnerQuiet.NameOfBuff()} stacks.");

            // Pre-quality dura/CP settings
            ImGui.Dummy(new Vector2(0, 5f));
            ImGui.TextWrapped($"General");
            ImGui.Indent();
            DrawIconText("Use [s!TrainedPerfection] on:");
            HelpMarkerWithIcons(["The \"(Late)\" option will try to use [s!PreparatoryTouch] under [s!Innovation] and [s!GreatStrides].", "The \"Either action\" option is most effective when paired with the {0} setting below.", "\"Either action\" defaults to [s!Groundwork] on a neutral {1}."], [Skills.Observe.NameOfAction(), ConditionString.ToLower()]);
            ImGui.PushItemWidth(400);
            if (ImGui.BeginCombo("##midUseTPSetting", s.GetMidUseTPSettingName(s.MidUseTP)))
            {
                foreach (MidUseTPSetting x in Enum.GetValues<MidUseTPSetting>())
                {
                    if (ImGui.Selectable(s.GetMidUseTPSettingName(x)))
                    {
                        s.MidUseTP = x;
                        changed = true;
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.Dummy(new Vector2(0, 5f));
            ImGui.PushItemWidth(150);
            changed |= SliderIntWithIcons("MidMaxBaitStepsForTP", ref s.MidMaxBaitStepsForTP, 0, 5, "[s!Observe] this many times for better {0} during [s!TrainedPerfection] (0 to disable)", [ConditionString.ToLower()]);
            HelpMarkerWithIcons(["Looks for [c!Malleable] for [s!Groundwork].", "Looks for [c!Good] or [c!Pliant] for [s!PreparatoryTouch]."]);
            changed |= CheckboxWithIcons("MidBaitPliantWithObservePreQuality", ref s.MidBaitPliantWithObservePreQuality, "When {0} is critical, use [s!Observe] to try and proc a favorable {1} for [s!Manipulation]", [DurabilityString.ToLower(), ConditionString.ToLower()]);
            HelpMarkerWithIcons(["Fishes for [c!Pliant] (and [c!Primed] if the appropriate option is enabled.)", "If disabled, [s!Manipulation] will be used immediately regardless of {0}."], [ConditionString.ToLower()]);
            changed |= CheckboxWithIcons("MidPrimedManipPreQuality", ref s.MidPrimedManipPreQuality, "Use [s!Manipulation] during [c!Primed]");
            HelpMarkerWithIcons("If disabled, [c!Primed] will generally be treated like [c!Normal] during this phase.");
            ImGui.Unindent();

            // Pre-quality progress settings
            ImGui.Dummy(new Vector2(0, 5f));
            ImGui.TextWrapped($"{ProgressString}");
            ImGui.Indent();
            changed |= CheckboxWithIcons("MidFinishProgressBeforeQuality", ref s.MidFinishProgressBeforeQuality, "Prioritize {0} over {1} and {2}", [ProgressString.ToLower(), Buffs.InnerQuiet.NameOfBuff(), QualityString.ToLower()]);
            HelpMarkerWithIcons(["This setting will use [s!Veneration] and [s!RapidSynthesis] to max out progress ASAP, regardless of {0} stacks or the current step's {1}.", "This is less flexible, but tries to ensure that {2} will always finish.", "If disabled, the solver won't prioritize {2} actions or force [s!Veneration] until reaching max {0} stacks.", "This is more flexible, but might fail to finish the craft in a worst-case scenario."], [Buffs.InnerQuiet.NameOfBuff(), ConditionString.ToLower(), ProgressString.ToLower()]);

            ImGui.Dummy(new Vector2(0, 5f));
            DrawIconText("When {0} starts to run low and we need to use [s!RapidSynthesis]:", [DurabilityString.ToLower()]);
            ImGui.PushItemWidth(400);
            if (ImGui.BeginCombo("##midKeepHighDuraSetting", s.GetMidKeepHighDuraSettingName(s.MidKeepHighDura)))
            {
                foreach (MidKeepHighDuraSetting x in Enum.GetValues<MidKeepHighDuraSetting>())
                {
                    if (ImGui.Selectable(s.GetMidKeepHighDuraSettingName(x)))
                    {
                        s.MidKeepHighDura = x;
                        changed = true;
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.Dummy(new Vector2(0, 5f));
            DrawIconText("When [c!Good] and still working on {0}:", [ProgressString.ToLower()]);
            HelpMarkerWithIcons("If disabled, [c!Good] will be used on [s!PreciseTouch] or [s!TricksOfTrade] (if allowed by other settings), even with {0} remaining.", [ProgressString.ToLower()]);
            if (ImGui.BeginCombo("##midAllowIntensiveSetting", s.GetMidAllowIntensiveSettingName(s.MidAllowIntensive)))
            {
                foreach (MidAllowIntensiveSetting x in Enum.GetValues<MidAllowIntensiveSetting>())
                {
                    if (ImGui.Selectable(s.GetMidAllowIntensiveSettingName(x)))
                    {
                        s.MidAllowIntensive = x;
                        changed = true;
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.Dummy(new Vector2(0, 5f));
            changed |= CheckboxWithIcons("MidAllowVenerationGoodOmen", ref s.MidAllowVenerationGoodOmen, "Use [s!Veneration] during [c!GoodOmen] with large {0} deficit", [ProgressString.ToLower()]);
            HelpMarkerWithIcons("Specifically if the upcoming [c!Good] step's [s!IntensiveSynthesis] won't max out {0} without [s!Veneration].", [ProgressString.ToLower()]);
            ImGui.Unindent();

            // Pre-quality Inner Quiet settings
            ImGui.Dummy(new Vector2(0, 5f));
            ImGui.TextWrapped($"{Buffs.InnerQuiet.NameOfBuff()}");
            ImGui.Indent();
            changed |= CheckboxWithIcons("MidAllowPrecise", ref s.MidAllowPrecise, "When [c!Good], use [s!PreciseTouch]");
            HelpMarkerWithIcons(["[s!IntensiveSynthesis] takes priority with {0} remaining, unless disabled by other settings.", "If both options are disabled, [c!Good] will be used on [s!TricksOfTrade]."], [ProgressString.ToLower()]);

            ImGui.Dummy(new Vector2(0, 5f));
            DrawIconText("Use [s!HeartAndSoul] to force [s!PreciseTouch]:");
            ImGui.Indent();
            changed |= CheckboxWithIcons("MidAllowSturdyPreсise", ref s.MidAllowSturdyPreсise, "When [c!Sturdy]/[c!Robust]");
            ImGui.PushItemWidth(250);
            changed |= SliderIntWithIcons("MidMinIQForHSPrecise", ref s.MidMinIQForHSPrecise, 0, 10, "At this many {0} stacks (10 to disable)", [Buffs.InnerQuiet.NameOfBuff()]);
            ImGui.Unindent();

            ImGui.Dummy(new Vector2(0, 5f));
            DrawIconText("Use [s!HastyTouch] and [s!DaringTouch]:");
            ImGui.Indent();
            changed |= CheckboxWithIcons("MidAllowCenteredHasty", ref s.MidAllowCenteredHasty, "When [c!Centered] (85% success, 10 {0})", [DurabilityString.ToLower()]);
            changed |= CheckboxWithIcons("MidAllowSturdyHasty", ref s.MidAllowSturdyHasty, "When [c!Sturdy]/[c!Robust] (60% success, 5 {0})", [DurabilityString.ToLower()]);
            ImGui.Unindent();
            ImGui.Unindent();
        }
        catch (Exception ex)
        {
            ex.Log();
        }
        return changed;
    }

    public bool DrawQualitySettings(ExpertSolverSettings s)
    {
        bool changed = false;
        try
        {
            ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"These settings apply after reaching max {Buffs.InnerQuiet.NameOfBuff()} stacks.");

            // Mid-quality dura/CP settings
            ImGui.Dummy(new Vector2(0, 5f));
            ImGui.TextWrapped($"General");
            ImGui.Indent();
            changed |= CheckboxWithIcons("MidBaitPliantWithObserveAfterIQ", ref s.MidBaitPliantWithObserveAfterIQ, "When {0} is very low, use [s!Observe] to proc a favorable {1} for restoring {0}", [DurabilityString.ToLower(), ConditionString.ToLower()]);
            HelpMarkerWithIcons(["Fishes for [c!Pliant] (and possibly [c!Primed]).", "If disabled, actions that restore or require 0 {0} will be used immediately regardless of {1}."], [DurabilityString.ToLower(), ConditionString.ToLower()]);
            changed |= CheckboxWithIcons("MidPrimedManipAfterIQ", ref s.MidPrimedManipAfterIQ, "Use [s!Manipulation] during [c!Primed] if enough CP is left to effectively use the restored {0}", [DurabilityString.ToLower()]);
            changed |= CheckboxWithIcons("MidObserveGoodOmenForTricks", ref s.MidObserveGoodOmenForTricks, "On [c!GoodOmen], prioritize [s!Observe] → [s!TricksOfTrade] when not under buffs");
            HelpMarkerWithIcons(["If disabled, the solver will prioritize a buff skill and spend the [c!Good] turn on {0} or {1}.", "Enabling this option is generally more efficient."], [ProgressString.ToLower(), QualityString.ToLower()]);
            ImGui.Unindent();

            // Mid-quality progress settings
            ImGui.Dummy(new Vector2(0, 5f));
            ImGui.TextWrapped($"{ProgressString}");
            ImGui.Indent();
            changed |= CheckboxWithIcons("MidAllowVenerationAfterIQ", ref s.MidAllowVenerationAfterIQ, "Use [s!Veneration] with large {0} deficit", [ProgressString.ToLower()]);
            HelpMarkerWithIcons(["Specifically if a single [s!IntensiveSynthesis] couldn't finish the craft without [s!Veneration], even this late in the craft.", "Overridden by the \"Prioritize {0}\" setting, if enabled."], [ProgressString.ToLower()]);
            ImGui.Unindent();

            // Mid-quality action settings
            ImGui.Dummy(new Vector2(0, 5f));
            ImGui.TextWrapped($"{QualityString}");
            ImGui.Indent();

            DrawIconText("Use [s!PreparatoryTouch]:");
            ImGui.Indent();
            changed |= CheckboxWithIcons("MidAllowGoodPrep", ref s.MidAllowGoodPrep, "Under [c!Good] + [s!Innovation] + [s!GreatStrides]");
            HelpMarkerWithIcons("Less efficient than [s!PreciseTouch], despite the big quality bump.");
            changed |= CheckboxWithIcons("MidAllowSturdyPrep", ref s.MidAllowSturdyPrep, "Under [c!Sturdy]/[c!Robust] + [s!Innovation]");
            ImGui.Unindent();

            ImGui.Dummy(new Vector2(0, 5f));
            changed |= CheckboxWithIcons("MidGSBeforeInno", ref s.MidGSBeforeInno, "Use [s!GreatStrides] before non-finisher {0} combos", [QualityString.ToLower()]);
            HelpMarkerWithIcons(["ex. [s!Innovation] → [s!Observe] → [s!AdvancedTouch].", "Enabling this uses more CP but less {0}, and may help avoid a usage of an expensive {0}-related action."], [DurabilityString.ToLower()]);

            ImGui.Dummy(new Vector2(0, 5f));
            DrawIconText("When [c!Good] and only [s!GreatStrides] is up:");
            HelpMarkerWithIcons(["\"Free\" [s!PreparatoryTouch] refers to [s!TrainedPerfection], which can be enabled in the Pre-{0} settings.", "Saving [s!QuickInnovation] for an emergency [s!ByregotsBlessing] in the finisher is the most efficient, but might not be necessary."], [QualityString]);
            ImGui.PushItemWidth(350);
            if (ImGui.BeginCombo("##midAllowQuickInnoGoodSetting", s.GetMidAllowQuickInnoGoodSettingName(s.MidAllowQuickInnoGood)))
            {
                foreach (MidAllowQuickInnoGoodSetting x in Enum.GetValues<MidAllowQuickInnoGoodSetting>())
                {
                    if (ImGui.Selectable(s.GetMidAllowQuickInnoGoodSettingName(x)))
                    {
                        s.MidAllowQuickInnoGood = x;
                        changed = true;
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.Unindent();
        }
        catch (Exception ex)
        {
            ex.Log();
        }
        return changed;
    }

    public bool DrawFinisherSettings(ExpertSolverSettings s)
    {
        bool changed = false;
        try
        {
            ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"These settings apply when close to max {QualityString.ToLower()} or when running out of other options.");

            ImGui.Dummy(new Vector2(0, 5f));
            DrawIconText("Use [s!CarefulObservation] to try and proc [c!Good]:");
            ImGui.Indent();
            changed |= CheckboxWithIcons("FinisherBaitGoodByregot", ref s.FinisherBaitGoodByregot, "For [s!ByregotsBlessing] as a makeshift [s!GreatStrides]");
            HelpMarkerWithIcons("Invoked when [s!GreatStrides] + [s!ByregotsBlessing] would get us there, but we don't have enough CP for [s!GreatStrides] or standard [s!Observe].");
            changed |= CheckboxWithIcons("EmergencyCPBaitGood", ref s.EmergencyCPBaitGood, "For [s!TricksOfTrade] if really low on CP");
            HelpMarkerWithIcons("Invoked when totally out of other options and even [s!ByregotsBlessing] wouldn't be enough {0}.", [QualityString.ToLower()]);
            ImGui.Unindent();

            ImGui.Dummy(new Vector2(0, 5f));
            changed |= CheckboxWithIcons("FinisherUseQuickInno", ref s.FinisherUseQuickInno, "Use [s!QuickInnovation] to finish when low on CP");
            HelpMarkerWithIcons("When there's not enough CP to use [s!Innovation] and/or [s!GreatStrides], but [s!QuickInnovation] is enough to reach the {} goal.", [QualityString.ToLower()]);
            changed |= CheckboxWithIcons("RapidSynthYoloAllowed", ref s.RapidSynthYoloAllowed, "Allow finishing with [s!RapidSynthesis] when out of options");
            ImGuiComponents.HelpMarker($"If disabled, the solver will do nothing instead, which may interrupt AFK expert crafting. Usually safe to enable, as it will only be invoked with no CP or {DurabilityString.ToLower()} left.");
        }
        catch (Exception ex)
        {
            ex.Log();
        }
        return changed;
    }

    public bool DrawMiscSettings(ExpertSolverSettings s)
    {
        bool changed = false;
        try
        {
            ImGui.TextWrapped($"Ishgardian Restoration");
            ImGui.Indent();
            changed |= ImGui.Checkbox("Max out Ishgard Restoration recipes instead of just hitting max breakpoint", ref s.MaxIshgardRecipes);
            ImGuiComponents.HelpMarker("This will try to maximise quality to earn more Skyward points.");
            ImGui.Unindent();

            ImGui.TextWrapped($"Cosmic Exploration");
            changed |= ImGui.Checkbox("Override per-recipe Cosmic Exploration settings###overrideCosmic", ref s.OverrideCosmicRecipeSettings);
            ImGuiComponents.HelpMarker("By default, Cosmic Exploration settings are tracked for each recipe and ignore the selected expert profile. Enable this option to instead use the settings below.");

            ImGui.Indent();
            if (!s.OverrideCosmicRecipeSettings) ImGui.BeginDisabled();
            changed |= CheckboxWithIcons("UseMaterialMiracle", ref s.UseMaterialMiracle, "Use [s!MaterialMiracle]");
            ImGui.PushItemWidth(250);
            if (s.UseMaterialMiracle)
            {
                changed |= SliderIntWithIcons("MinimumStepsBeforeMiracle", ref s.MinimumStepsBeforeMiracle, 0, 20, "Minimum steps to execute before trying [s!MaterialMiracle]");
                ImGui.Dummy(new Vector2(0, 5f));
            }
            changed |= SliderIntWithIcons("MaxSteadyUses", ref s.MaxSteadyUses, 0, 2, "Max [s!SteadyHand] uses per craft");
            HelpMarkerWithIcons(["[s!SteadyHand] will be used ASAP to guarantee [s!RapidSynthesis].", "Set to 0 to disable."]);
            if (!s.OverrideCosmicRecipeSettings) ImGui.EndDisabled();
            ImGui.Unindent();
        }
        catch (Exception ex)
        {
            ex.Log();
        }
        return changed;
    }

    public bool DrawAllSettings(ExpertSolverSettings s, bool startOpen)
    {
        bool changed = false;

        changed |= DrawMiscSettings(s);
        ImGui.Dummy(new Vector2(0, 5f));

        ImGuiTreeNodeFlags flags = startOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
        if (ImGui.CollapsingHeader("Opener", flags))
        {
            ImGui.Dummy(new Vector2(0, 5f));
            changed |= DrawOpenerSettings(s);
            ImGui.Dummy(new Vector2(0, 5f));
        }

        if (ImGui.CollapsingHeader($"Main Rotation - Pre-{QualityString} Phase", flags))
        {
            ImGui.Dummy(new Vector2(0, 5f));
            changed |= DrawPreQualitySettings(s);
            ImGui.Dummy(new Vector2(0, 5f));
        }

        if (ImGui.CollapsingHeader($"Main Rotation - {QualityString} Phase", flags))
        {
            ImGui.Dummy(new Vector2(0, 5f));
            changed |= DrawQualitySettings(s);
            ImGui.Dummy(new Vector2(0, 5f));
        }

        if (ImGui.CollapsingHeader($"Finisher", flags))
        {
            ImGui.Dummy(new Vector2(0, 5f));
            changed |= DrawFinisherSettings(s);
        }

        return changed;
    }

    public bool DrawGlobalSettings(ExpertSolverSettings s)
    {
        bool changed = false;
        try
        {
            ImGui.TextWrapped($"The expert recipe solver is not an alternative to the standard solver. This is used exclusively with expert recipes.");
            if (expertIcon != null)
            {
                ImGui.TextWrapped($"This solver only applies to recipes with the");
                ImGui.SameLine();
                ImGui.Image(expertIcon.Handle, expertIcon.Size, new Vector2(0, 0), new Vector2(1, 1), new Vector4(0.94f, 0.57f, 0f, 1f));
                ImGui.SameLine();
                ImGui.TextWrapped($"icon in the crafting log.");
            }

            ImGui.Dummy(new Vector2(0, 5f));
            if (IconButtons.IconTextButton(Dalamud.Interface.FontAwesomeIcon.ExternalLinkAlt, "Create/Edit Expert Solver Profiles"))
            {
                P.PluginUi.OpenWindow = OpenWindow.ExpertProfiles;
            }

            ImGui.Indent();
            ImGui.Dummy(new Vector2(0, 5f));
            changed |= DrawAllSettings(s, false);
            ImGui.Unindent();

            ImGui.Dummy(new Vector2(0, 5f));
            if (ImGuiEx.ButtonCtrl("Reset Expert Solver Settings To Default"))
            {
                s = new();
                changed |= true;
            }
            ImGui.Dummy(new Vector2(0, 5f));

            return changed;
        }
        catch { }
        return changed;
    }

    /// <summary>
    /// Custom HelpMarker that supports skill icons and colorful condition dots.
    /// </summary>
    /// <param name="str">The helpText string with custom markup.</param>
    /// <param name="args">Substitution strings for the helpText string.</param>
    public void HelpMarkerWithIcons(string str, object[]? args = null) => HelpMarkerWithIcons([str], args);

    /// <summary>
    /// Custom HelpMarker that supports skill icons and colorful condition dots.
    /// </summary>
    /// <param name="lines">An Array of helpText strings with custom markup.</param>
    /// <param name="args">Substitution strings for each helpText string.</param>
    public void HelpMarkerWithIcons(string[] lines, object[]? args = null)
    {
        if (args == null)
            args = [];

        ImGui.SameLine();

        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextDisabled(FontAwesomeIcon.InfoCircle.ToIconString());

        if (ImGui.IsItemHovered())
        {
            using (ImRaii.Tooltip())
            {
                foreach (string str in lines)
                    DrawIconText(str, args);
            }
        }
    }

    /// <summary>
    /// Custom ImGui.Checkbox that supports skill icons and colorful condition dots in its label.
    /// </summary>
    /// <param name="ID">A unique ID for the checkbox.</param>
    /// <param name="val">The boolean setting to attach to the checkbox.</param>
    /// <param name="str">The string with custom markup for the checkbox's label.</param>
    /// <param name="args">Substitution strings for the checkbox's label.</param>
    public bool CheckboxWithIcons(string ID, ref bool val, string str, object[]? args = null)
    {
        if (args == null)
            args = [];

        bool changed = false;

        ImGui.PushID(ID);
        changed |= ImGui.Checkbox($"##{ID}", ref val);
        ImGui.SameLine(0.0f, 4.0f);

        DrawIconText(str, args);

        ImGui.PopID();
        return changed;
    }

    /// <summary>
    /// Custom ImGui.SliderInt that supports skill icons and colorful condition dots in its label.
    /// </summary>
    /// <param name="ID">A unique ID for the slider.</param>
    /// <param name="val">The int setting to attach to the slider.</param>
    /// <param name="min">Minimum value for the slider.</param>
    /// <param name="max">Maximum value for the slider.</param>
    /// <param name="str">The string with custom markup for the slider's label.</param>
    /// <param name="args">Substitution strings for the slider's label.</param>
    public bool SliderIntWithIcons(string ID, ref int val, int min, int max, string str, object[]? args = null)
    {
        if (args == null)
            args = [];

        bool changed = false;

        ImGui.PushID(ID);
        changed |= ImGui.SliderInt($"##{ID}", ref val, min, max);
        ImGui.SameLine(0.0f, 4.0f);

        DrawIconText(str, args);

        ImGui.PopID();
        return changed;
    }

    /// <summary>
    /// Draws Text, colorized Text, and Image elements from a string with custom markup.
    /// </summary>
    /// <param name="str">The string with custom markup to be rendered.</param>
    /// <param name="args">Substitution strings for the primary string.</param>
    /// <param name="color">The color to be used for standard strings.</param>
    public void DrawIconText(string str, object[]? args = null, Vector4? color = null)
    {
        if (args == null)
            args = [];
        if (color == null)
            color = ImGuiColors.DalamudWhite;

        SkillIconID skillIcon;
        Condition condition;
        Skills skill;
        string formatStr = String.Format(str, args);
        string[] parts = Regex.Split(formatStr, @"(\[.+?\])");
        for (int i = 0; i < parts.Length; i++)
        {
            float spacing = 2.0f;
            string part = parts[i];
            if (part.StartsWith("[c!"))
            {
                // Render a condition dot (●) with the appropriate color and the localized condition name
                Vector4 condColor;
                string c = part[3..^1];
                if (ConditionColors.TryGetValue(c, out condColor))
                {
                    ImGuiEx.Text(condColor, "● ");
                    ImGui.SameLine(0.0f, 0.0f);
                }
                if (Enum.TryParse(c, out condition))
                    ImGuiEx.Text(color, condition.ToLocalizedString());
                spacing = 0.0f;
            }
            else if (part.StartsWith("[s!"))
            {
                // Render a skill icon and the localized skill name
                string s = part[3..^1];
                if (Enum.TryParse(s, out skillIcon))
                {
                    uint iconID = (uint)skillIcon;
                    ImGui.Image(P.Icons.TryLoadIconAsync(iconID).Result.Handle, new Vector2(20f, 20f));
                    ImGui.SameLine(0.0f, 4.0f);
                }
                if (Enum.TryParse(s, out skill))
                {
                    ImGuiEx.Text(color, skill.NameOfAction());
                    spacing = 0.0f;
                }
            }
            else if (part == "[ex]")
            {
                // Render the expert crafting icon
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4.0f);
                ImGui.Image(expertIcon.Handle, new Vector2(36f, 18f), new Vector2(0, 0), new Vector2(1, 1), new Vector4(0.94f, 0.57f, 0f, 1f));
                spacing = 4.0f;
            }
            else
            {
                // Plain text
                ImGuiEx.Text(color, part);
            }

            if (i < parts.Length - 1)
                ImGui.SameLine(0.0f, spacing);
        }
    }
}
