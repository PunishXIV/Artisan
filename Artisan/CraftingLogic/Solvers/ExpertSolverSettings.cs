using Artisan.CraftingLogic.CraftData;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures.TextureWraps;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using System;
using System.Numerics;
using static Artisan.RawInformation.AddonExtensions;

namespace Artisan.CraftingLogic.Solvers;

public class ExpertSolverSettings
{
    public bool MaxIshgardRecipes;
    public bool UseReflectOpener;
    public bool MuMeIntensiveGood = true; // if true, we allow spending mume on intensive (400p) rather than rapid (500p) if good condition procs
    public bool MuMeIntensiveMalleable = false; // if true and we have malleable during mume, use intensive rather than hoping for rapid
    public bool MuMeIntensiveLastResort = true; // if true and we're on last step of mume, use intensive (forcing via H&S if needed) rather than hoping for rapid (unless we have centered)
    public bool MuMePrimedManip = false; // if true, allow using primed manipulation after veneration is up on mume
    public bool MuMeAllowObserve = false; // if true, observe rather than use actions during unfavourable conditions to conserve durability
    public int MuMeMinStepsForManip = 2; // if this or less rounds are remaining on mume, don't use manipulation under favourable conditions
    public int MuMeMinStepsForVene = 1; // if this or less rounds are remaining on mume, don't use veneration
    public int MidMinIQForHSPrecise = 10; // min iq stacks where we use h&s+precise; 10 to disable
    public bool MidBaitPliantWithObservePreQuality = true; // if true, when very low on durability and without manip active during pre-quality phase, we use observe rather than normal manip
    public bool MidBaitPliantWithObserveAfterIQ = true; // if true, when very low on durability and without manip active after iq has 10 stacks, we use observe rather than normal manip or inno+finnesse
    public bool MidPrimedManipPreQuality = true; // if true, allow using primed manipulation during pre-quality phase
    public bool MidPrimedManipAfterIQ = true; // if true, allow using primed manipulation during after iq has 10 stacks
    public enum MidUseTPSetting  // where to use trained perfection
    {
        MidUseTPGroundwork,        // use TP on groundwork for high-prio progress
        MidUseTPPrepIQ,            // use TP on preparatory touch to build IQ stacks
        MidUseTPEitherPreQuality,  // use TP on either of the options above, depending on which status comes up first (default groundwork)
        MidUseTPPrepQuality        // use TP on prep touch after 10 IQ, with GS+Inno
    }
    public string GetMidUseTPSettingName(MidUseTPSetting value)
        => value switch
        {
            MidUseTPSetting.MidUseTPGroundwork => $"(Early) {Skills.Groundwork.NameOfAction()}",
            MidUseTPSetting.MidUseTPPrepIQ => $"(Early) {Skills.PreparatoryTouch.NameOfAction()} (build {Buffs.InnerQuiet.NameOfBuff()})",
            MidUseTPSetting.MidUseTPEitherPreQuality => $"(Early) Either action based on {ConditionString.ToLower()}",
            MidUseTPSetting.MidUseTPPrepQuality or _ => $"(Late) {Skills.PreparatoryTouch.NameOfAction()} at max {Buffs.InnerQuiet.NameOfBuff()} (focus {QualityString.ToLower()})",
        };
    public MidUseTPSetting MidUseTP = MidUseTPSetting.MidUseTPGroundwork;
    public int MidMaxBaitStepsForTP = 0; // how many observes should be used to bait favorable conditions for trained perfection; 0 to disable
    public enum MidKeepHighDuraSetting  // what to do in pre-quality when dura is starting to run low
    {
        MidKeepHighDuraUnbuffed,        // fish for procs with observe to conserve dura, as long as veneration isn't up
        MidKeepHighDuraVeneration,      // fish for procs with observe to conserve dura, no matter what
        MidUseDura                      // don't fish for procs, keep using durability
    }
    public string GetMidKeepHighDuraSettingName(MidKeepHighDuraSetting value)
        => value switch
        {
            MidKeepHighDuraSetting.MidKeepHighDuraUnbuffed => $"Use {Skills.Observe.NameOfAction()} for a better {ConditionString.ToLower()}, as long as {Buffs.Veneration.NameOfBuff()} isn't on",
            MidKeepHighDuraSetting.MidKeepHighDuraVeneration => $"Use {Skills.Observe.NameOfAction()} for a better {ConditionString.ToLower()}, even during {Buffs.Veneration.NameOfBuff()}",
            MidKeepHighDuraSetting.MidUseDura or _ => $"Don't use {Skills.Observe.NameOfAction()}, just keep going",
        };
    public MidKeepHighDuraSetting MidKeepHighDura = MidKeepHighDuraSetting.MidKeepHighDuraUnbuffed;
    public enum MidAllowIntensiveSetting  // how to handle good procs before finishable progress
    {
        MidAllowIntensiveUnbuffed,        // use intensive synthesis no matter what
        MidAllowIntensiveVeneration,      // use intensive synthesis as long as veneration is up
        MidNoIntensive                    // don't use intensive synthesis (good will be used for tricks or precise)
    }
    public string GetMidAllowIntensiveSettingName(MidAllowIntensiveSetting value)
        => value switch
        {
            MidAllowIntensiveSetting.MidNoIntensive => $"Don't use {Skills.IntensiveSynthesis.NameOfAction()}",
            MidAllowIntensiveSetting.MidAllowIntensiveVeneration => $"Use {Skills.IntensiveSynthesis.NameOfAction()} as long as {Buffs.Veneration.NameOfBuff()} is on",
            MidAllowIntensiveSetting.MidAllowIntensiveUnbuffed or _ => $"Use {Skills.IntensiveSynthesis.NameOfAction()} regardless of buffs"
        };
    public MidAllowIntensiveSetting MidAllowIntensive = MidAllowIntensiveSetting.MidNoIntensive;
    public bool MidAllowVenerationGoodOmen = true; // if true, we allow using veneration during iq phase if we lack a lot of progress on good omen
    public bool MidAllowVenerationAfterIQ = true; // if true, we allow using veneration after iq is fully stacked if we still lack a lot of progress
    public bool MidAllowPrecise = true; // if true, we allow spending good condition on precise touch if we still need iq
    public bool MidAllowSturdyPreсise = false; // if true,we consider sturdy+h&s+precise touch a good move for building iq
    public bool MidAllowCenteredHasty = true; // if true, we consider centered hasty touch a good move for building iq (85% reliability)
    public bool MidAllowSturdyHasty = true; // if true, we consider sturdy hasty touch a good move for building iq (50% reliability), otherwise we use combo
    public bool MidAllowGoodPrep = true; // if true, we consider prep touch a good move for finisher under good+inno+gs
    public bool MidAllowSturdyPrep = true; // if true, we consider prep touch a good move for finisher under sturdy+inno
    public bool MidGSBeforeInno = true; // if true, we start quality combos with gs+inno rather than just inno
    public bool MidFinishProgressBeforeQuality = false; // if true, at 10 iq we first finish progress before starting on quality
    public bool MidObserveGoodOmenForTricks = false; // if true, we'll observe on good omen where otherwise we'd use tricks on good
    public bool FinisherBaitGoodByregot = true; // if true, use careful observations to try baiting good byregot
    public bool EmergencyCPBaitGood = false; // if true, we allow spending careful observations to try baiting good for tricks when we really lack cp
	public bool RapidSynthYoloAllowed = true; // if false, expert crafting may lock up midway, so not good for AFK crafting. This yolo however is likely to fail the craft, so disabling gives opportunity for intervention
    public bool UseMaterialMiracle = false;
	public int MinimumStepsBeforeMiracle = 10;

    [NonSerialized]
    public IDalamudTextureWrap? expertIcon;

    public ExpertSolverSettings()
    {
        var tex = Svc.PluginInterface.UiBuilder.LoadUld("ui/uld/RecipeNoteBook.uld");
        expertIcon = tex?.LoadTexturePart("ui/uld/RecipeNoteBook_hr1.tex", 14);
    }

    public bool Draw()
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

            ImGui.Indent();
            ImGui.Dummy(new Vector2(0, 5f));
            if (ImGui.CollapsingHeader("Opener"))
            {
                changed |= ImGui.Checkbox($"Use {Skills.Reflect.NameOfAction()} instead of {Skills.MuscleMemory.NameOfAction()}", ref UseReflectOpener);
                if (!UseReflectOpener)
                {
                    ImGui.Dummy(new Vector2(0, 5f));
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"These settings only apply while {Skills.MuscleMemory.NameOfAction()} is active at the start of a craft.");
                    ImGui.Dummy(new Vector2(0, 5f));
                    changed |= ImGui.Checkbox($"When ● {Condition.Good.ToLocalizedString()}, prioritize {Skills.IntensiveSynthesis.NameOfAction()} (400%) over {Skills.RapidSynthesis.NameOfAction()} (500%)", ref MuMeIntensiveGood);
                    changed |= ImGui.Checkbox($"When ● {Condition.Malleable.ToLocalizedString()}, use {Skills.HeartAndSoul.NameOfAction()} + {Skills.IntensiveSynthesis.NameOfAction()} (if available)", ref MuMeIntensiveMalleable);
                    changed |= ImGui.Checkbox($"When ● {Condition.Primed.ToLocalizedString()} and {Skills.Veneration.NameOfAction()} is already active, use {Skills.Manipulation.NameOfAction()}", ref MuMePrimedManip);
                    ImGuiComponents.HelpMarker($"If this is disabled, {Skills.Manipulation.NameOfAction()} will only be used during ● {Condition.Pliant.ToLocalizedString()} while {Skills.MuscleMemory.NameOfAction()} is active.");
                    changed |= ImGui.Checkbox($"When ● {Condition.Normal.ToLocalizedString()} or other irrelevant {ConditionString.ToLower()}, use {Skills.Observe.NameOfAction()} instead of {Skills.RapidSynthesis.NameOfAction()}", ref MuMeAllowObserve);
                    ImGuiComponents.HelpMarker($"This saves {DurabilityString.ToLower()} at the cost of {Skills.MuscleMemory.NameOfAction()} steps.");
                    changed |= ImGui.Checkbox($"When 1 step left on {Skills.MuscleMemory.NameOfAction()} and not ● {Condition.Centered.ToLocalizedString()}, use {Skills.IntensiveSynthesis.NameOfAction()} (forcing via {Skills.HeartAndSoul.NameOfAction()} if necessary)", ref MuMeIntensiveLastResort);
                    ImGuiComponents.HelpMarker($"{Skills.RapidSynthesis.NameOfAction()} will still be used if the last step is ● {Condition.Centered.ToLocalizedString()}.");
                    ImGui.Text($"Use these skills only if {Skills.MuscleMemory.NameOfAction()} has at least this many steps left:");
                    ImGuiComponents.HelpMarker($"The solver will still only use these skills under an appropriate {ConditionString.ToLower()}.");
                    // these have a minimum of 1 to avoid using a buff on the final turn of MuMe
                    ImGui.PushItemWidth(250);
                    changed |= ImGui.SliderInt($"{Skills.Manipulation.NameOfAction()}###MumeMinStepsForManip", ref MuMeMinStepsForManip, 1, 5);
                    ImGui.PushItemWidth(250);
                    changed |= ImGui.SliderInt($"{Skills.Veneration.NameOfAction()}###MuMeMinStepsForVene", ref MuMeMinStepsForVene, 1, 5);
                    ImGui.Dummy(new Vector2(0, 5f));
                }
            }

            if (ImGui.CollapsingHeader($"Main Rotation - Pre-{QualityString} Phase"))
            {
                ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"These settings apply after the opener, but before reaching max {Buffs.InnerQuiet.NameOfBuff()} stacks.");

                // Pre-quality dura/CP settings
                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"General");
                ImGui.Indent();
                ImGui.TextWrapped($"Use {Skills.TrainedPerfection.NameOfAction()} on:");
                ImGuiComponents.HelpMarker($"The \"(Late)\" option will try to use {Skills.PreparatoryTouch.NameOfAction()} under {Buffs.Innovation.NameOfBuff()} and {Buffs.GreatStrides.NameOfBuff()}. \"Either action\" is most effective when paired with the {Skills.Observe.NameOfAction()} setting below, and will default to {Skills.Groundwork.NameOfAction()} on a neutral {ConditionString.ToLower()}.");
                ImGui.PushItemWidth(400);
                if (ImGui.BeginCombo("##midUseTPSetting", GetMidUseTPSettingName(MidUseTP)))
                {
                    foreach (MidUseTPSetting x in Enum.GetValues<MidUseTPSetting>())
                    {
                        if (ImGui.Selectable(GetMidUseTPSettingName(x)))
                        {
                            MidUseTP = x;
                            changed = true;
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.PushItemWidth(150);
                changed |= ImGui.SliderInt($"{Skills.Observe.NameOfAction()} this many times for {ConditionString.ToLower()} during {Skills.TrainedPerfection.NameOfAction()}  (0 to disable)###MidMaxBaitStepsForTP", ref MidMaxBaitStepsForTP, 0, 5);
                ImGuiComponents.HelpMarker($"Fishes for ● {Condition.Malleable.ToLocalizedString()} when {Skills.TrainedPerfection.NameOfAction()} is being used for {Skills.Groundwork.NameOfAction()}, or ● {Condition.Good.ToLocalizedString()}/{Condition.Pliant.ToLocalizedString()} for {Skills.PreparatoryTouch.NameOfAction()}.");
                changed |= ImGui.Checkbox($"When {DurabilityString.ToLower()} is critical, use {Skills.Observe.NameOfAction()} to try and proc a favorable {ConditionString.ToLower()} for {Skills.Manipulation.NameOfAction()}", ref MidBaitPliantWithObservePreQuality);
                ImGuiComponents.HelpMarker($"Fishes for ● {Condition.Pliant.ToLocalizedString()} (and ● {Condition.Primed.ToLocalizedString()} if the appropriate option is enabled.) If disabled, {Skills.Manipulation.NameOfAction()} will be used immediately regardless of {ConditionString.ToLower()}.");
                changed |= ImGui.Checkbox($"Use {Skills.Manipulation.NameOfAction()} during ● {Condition.Primed.ToLocalizedString()}", ref MidPrimedManipPreQuality);
                ImGuiComponents.HelpMarker($"If disabled, ● {Condition.Primed.ToLocalizedString()} will generally be treated like ● {Condition.Normal.ToLocalizedString()} during this phase.");
                ImGui.Unindent();

                // Pre-quality progress settings
                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"{ProgressString}");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"Prioritize {ProgressString.ToLower()} over {Buffs.InnerQuiet.NameOfBuff()} and {QualityString.ToLower()}", ref MidFinishProgressBeforeQuality);
                ImGuiComponents.HelpMarker($"This setting will use {Buffs.Veneration.NameOfBuff()} and {Skills.RapidSynthesis.NameOfAction()} to max out progress ASAP, regardless of {Buffs.InnerQuiet.NameOfBuff()} stacks or the current step's {ConditionString.ToLower()} (less flexible, but tries to ensure craft completion.) If disabled, the solver won't prioritize {ProgressString.ToLower()} actions or force {Buffs.Veneration.NameOfBuff()} until reaching max {Buffs.InnerQuiet.NameOfBuff()} stacks (more flexible, but might fail to finish the craft in a worst-case scenario.)");
                ImGui.TextWrapped($"When {DurabilityString.ToLower()} starts to run low and we need to use {Skills.RapidSynthesis.NameOfAction()}:");
                ImGui.PushItemWidth(400);
                if (ImGui.BeginCombo("##midKeepHighDuraSetting", GetMidKeepHighDuraSettingName(MidKeepHighDura)))
                {
                    foreach (MidKeepHighDuraSetting x in Enum.GetValues<MidKeepHighDuraSetting>())
                    {
                        if (ImGui.Selectable(GetMidKeepHighDuraSettingName(x)))
                        {
                            MidKeepHighDura = x;
                            changed = true;
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.TextWrapped($"When ● {Condition.Good.ToLocalizedString()} and still working on {ProgressString.ToLower()}:");
                ImGuiComponents.HelpMarker($"If disabled, ● {Condition.Good.ToLocalizedString()} will be used on {Skills.PreciseTouch.NameOfAction()} or {Skills.TricksOfTrade.NameOfAction()} (if allowed by other settings), even with {ProgressString.ToLower()} remaining.");
                if (ImGui.BeginCombo("##midAllowIntensiveSetting", GetMidAllowIntensiveSettingName(MidAllowIntensive)))
                {
                    foreach (MidAllowIntensiveSetting x in Enum.GetValues<MidAllowIntensiveSetting>())
                    {
                        if (ImGui.Selectable(GetMidAllowIntensiveSettingName(x)))
                        {
                            MidAllowIntensive = x;
                            changed = true;
                        }
                    }
                    ImGui.EndCombo();
                }
                changed |= ImGui.Checkbox($"Use {Skills.Veneration.NameOfAction()} during ● {Condition.GoodOmen.ToLocalizedString()} with large {ProgressString.ToLower()} deficit", ref MidAllowVenerationGoodOmen);
                ImGuiComponents.HelpMarker($"Specifically if the upcoming ● {Condition.Good.ToLocalizedString()} step's {Skills.IntensiveSynthesis.NameOfAction()} won't max out {ProgressString.ToLower()} without {Skills.Veneration.NameOfAction()}.");
                ImGui.Unindent();

                // Pre-quality Inner Quiet settings
                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"{Buffs.InnerQuiet.NameOfBuff()}");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"When ● {Condition.Good.ToLocalizedString()}, use {Skills.PreciseTouch.NameOfAction()}", ref MidAllowPrecise);
                ImGuiComponents.HelpMarker($"{Skills.IntensiveSynthesis.NameOfAction()} takes priority with {ProgressString.ToLower()} remaining, unless disabled by other settings. If both options are disabled, ● {Condition.Good.ToLocalizedString()} will be used on {Skills.TricksOfTrade.NameOfAction()}.");
                ImGui.TextWrapped($"Use {Skills.HeartAndSoul.NameOfAction()} to force {Skills.PreciseTouch.NameOfAction()}:");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"When ● {Condition.Sturdy.ToLocalizedString()}/{Condition.Robust.ToLocalizedString()}", ref MidAllowSturdyPreсise);
                ImGui.PushItemWidth(250);
                changed |= ImGui.SliderInt($"At this many {Buffs.InnerQuiet.NameOfBuff()} stacks (10 to disable)###MidMinIQForHSPrecise", ref MidMinIQForHSPrecise, 0, 10);
                ImGui.Unindent();
                ImGui.TextWrapped($"Use {Skills.HastyTouch.NameOfAction()} and {Skills.DaringTouch.NameOfAction()}:");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"When ● {Condition.Centered.ToLocalizedString()} (85% success, 10 {DurabilityString.ToLower()})", ref MidAllowCenteredHasty);
                changed |= ImGui.Checkbox($"When ● {Condition.Sturdy.ToLocalizedString()}/{Condition.Robust.ToLocalizedString()} (60% success, 5 {DurabilityString.ToLower()})", ref MidAllowSturdyHasty);
                ImGui.Unindent();
                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 5f));
            }

            if (ImGui.CollapsingHeader($"Main Rotation - {QualityString} Phase"))
            {
                ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"These settings apply after reaching max {Buffs.InnerQuiet.NameOfBuff()} stacks.");

                // Mid-quality dura/CP settings
                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"General");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"When {DurabilityString.ToLower()} is very low, use {Skills.Observe.NameOfAction()} to proc a favorable {ConditionString.ToLower()} for restoring {DurabilityString.ToLower()}", ref MidBaitPliantWithObserveAfterIQ);
                ImGuiComponents.HelpMarker($"Fishes for ● {Condition.Pliant.ToLocalizedString()} (and possibly ● {Condition.Primed.ToLocalizedString()}). If disabled, actions that restore or require 0 {DurabilityString.ToLower()} will be used immediately regardless of {ConditionString.ToLower()}.");
                changed |= ImGui.Checkbox($"Use {Skills.Manipulation.NameOfAction()} during ● {Condition.Primed.ToLocalizedString()} if enough CP is left to effectively use the restored {DurabilityString.ToLower()}", ref MidPrimedManipAfterIQ);
                changed |= ImGui.Checkbox($"On ● {Condition.GoodOmen.ToLocalizedString()}, prioritize {Skills.Observe.NameOfAction()} → {Skills.TricksOfTrade.NameOfAction()} when not under buffs", ref MidObserveGoodOmenForTricks);
                ImGuiComponents.HelpMarker($"If disabled, the solver will prioritize a buff skill and spend the ● {Condition.Good.ToLocalizedString()} turn on {ProgressString.ToLower()} or {QualityString.ToLower()}. Enabling this option is generally more efficient.");
                ImGui.Unindent();

                // Mid-quality progress settings
                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"{ProgressString}");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"Use {Skills.Veneration.NameOfAction()} with large {ProgressString.ToLower()} deficit", ref MidAllowVenerationAfterIQ);
                ImGuiComponents.HelpMarker($"Specifically if a single {Skills.IntensiveSynthesis.NameOfAction()} couldn't finish the craft without {Skills.Veneration.NameOfAction()}, even this late in the craft. Overridden by the \"Prioritize {ProgressString.ToLower()}\" setting, if enabled.");
                ImGui.Unindent();

                // Mid-quality action settings
                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"{QualityString}");
                ImGui.Indent();
                ImGui.TextWrapped($"Use {Skills.PreparatoryTouch.NameOfAction()}:");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"Under ● {Condition.Good.ToLocalizedString()} + {Buffs.Innovation.NameOfBuff()} + {Buffs.GreatStrides.NameOfBuff()}", ref MidAllowGoodPrep);
                changed |= ImGui.Checkbox($"Under ● {Condition.Sturdy.ToLocalizedString()}/{Condition.Robust.ToLocalizedString()} + {Buffs.Innovation.NameOfBuff()}", ref MidAllowSturdyPrep);
                ImGui.Unindent();
                changed |= ImGui.Checkbox($"Use {Skills.GreatStrides.NameOfAction()} before non-finisher {QualityString.ToLower()} combos", ref MidGSBeforeInno);
                ImGuiComponents.HelpMarker($"ex. {Buffs.Innovation.NameOfBuff()} → {Skills.Observe.NameOfAction()} → {Skills.AdvancedTouch.NameOfAction()}. Enabling this uses more CP but less {DurabilityString.ToLower()}, and may help avoid a usage of an expensive {DurabilityString.ToLower()}-related action.");
                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 5f));
            }

            if (ImGui.CollapsingHeader($"Finisher"))
            {
                ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"These settings apply when close to max {QualityString.ToLower()} or when running out of other options.");

                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.TextWrapped($"Use {Skills.CarefulObservation.NameOfAction()} to try and proc ● {Condition.Good.ToLocalizedString()}:");
                ImGui.Indent();
                changed |= ImGui.Checkbox($"For {Skills.ByregotsBlessing.NameOfAction()} as a makeshift {Skills.GreatStrides.NameOfAction()}", ref FinisherBaitGoodByregot);
                ImGuiComponents.HelpMarker($"Invoked when {Skills.GreatStrides.NameOfAction()} + {Skills.ByregotsBlessing.NameOfAction()} would get us there, but we don't have enough CP for {Skills.GreatStrides.NameOfAction()} or standard {Skills.Observe.NameOfAction()}.");
                changed |= ImGui.Checkbox($"For {Skills.TricksOfTrade.NameOfAction()} if really low on CP", ref EmergencyCPBaitGood);
                ImGuiComponents.HelpMarker($"Invoked when totally out of other options and even {Skills.ByregotsBlessing.NameOfAction()} wouldn't be enough {QualityString.ToLower()}.");
                ImGui.Unindent();
                changed |= ImGui.Checkbox($"Allow finishing with {Skills.RapidSynthesis.NameOfAction()} when out of options", ref RapidSynthYoloAllowed);
                ImGuiComponents.HelpMarker($"If disabled, the solver will do nothing instead, which may interrupt AFK expert crafting. Usually safe to enable, as it will only be invoked with no CP or {DurabilityString.ToLower()} left.");
                ImGui.Dummy(new Vector2(0, 5f));
            }
            ImGui.Unindent();

            // Misc. settings
            ImGui.Dummy(new Vector2(0, 5f));
            changed |= ImGui.Checkbox("Max out Ishgard Restoration recipes instead of just hitting max breakpoint", ref MaxIshgardRecipes);
            ImGuiComponents.HelpMarker("This will try to maximise quality to earn more Skyward points.");
            changed |= ImGui.Checkbox($"Use {Skills.MaterialMiracle.NameOfAction()} in Cosmic Exploration", ref UseMaterialMiracle);
            ImGui.PushItemWidth(250);
            changed |= ImGui.SliderInt($"Minimum steps to execute before trying {Skills.MaterialMiracle.NameOfAction()}###MinimumStepsBeforeMiracle", ref MinimumStepsBeforeMiracle, 0, 20);
            if (ImGuiEx.ButtonCtrl("Reset Expert Solver Settings To Default"))
            {
                P.Config.ExpertSolverConfig = new();
                changed |= true;
            }
            return changed;
        }
        catch { }
        return changed;
    }
}
