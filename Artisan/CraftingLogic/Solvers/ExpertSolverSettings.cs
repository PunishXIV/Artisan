using Artisan.CraftingLogic.CraftData;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures.TextureWraps;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System;
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
    public bool MidKeepHighDuraUnbuffed = true; // if true, observe rather than use actions during unfavourable conditions to conserve durability when no buffs are active
    public bool MidKeepHighDuraVeneration = false; // if true, observe rather than use actions during unfavourable conditions to conserve durability when veneration is active
    public bool MidAllowVenerationGoodOmen = true; // if true, we allow using veneration during iq phase if we lack a lot of progress on good omen
    public bool MidAllowVenerationAfterIQ = true; // if true, we allow using veneration after iq is fully stacked if we still lack a lot of progress
    public bool MidAllowIntensiveUnbuffed = false; // if true, we allow spending good condition on intensive if we still need progress when no buffs are active
    public bool MidAllowIntensiveVeneration = false; // if true, we allow spending good condition on intensive if we still need progress when veneration is active
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
    public bool UseMaterialMiracle = false;

    [NonSerialized]
    public IDalamudTextureWrap? expertIcon;

    public ExpertSolverSettings()
    {
        var tex = Svc.PluginInterface.UiBuilder.LoadUld("ui/uld/RecipeNoteBook.uld");
        expertIcon = tex.LoadTexturePart("ui/uld/RecipeNoteBook_hr1.tex", 14);
    }

    public bool Draw()
    {
        ImGui.TextWrapped($"The expert recipe solver is not an alternative to the standard solver. This is used exclusively with expert recipes.");
        if (expertIcon != null)
        {
            ImGui.TextWrapped($"This solver only applies to recipes with the");
            ImGui.SameLine();
            ImGui.Image(expertIcon.ImGuiHandle, expertIcon.Size, new(0, 0), new(1, 1), new(0.94f, 0.57f, 0f, 1f));
            ImGui.SameLine();
            ImGui.TextWrapped($"icon in the crafting log.");
        }
        bool changed = false;
        ImGui.Indent();
        if (ImGui.CollapsingHeader("Opener Settings"))
        {
            changed |= ImGui.Checkbox($"Use {Skills.Reflect.NameOfAction()} instead of {Skills.MuscleMemory.NameOfAction()} for the opener", ref UseReflectOpener);
            changed |= ImGui.Checkbox($"Allow spending {Skills.MuscleMemory.NameOfAction()} on {Skills.IntensiveSynthesis.NameOfAction()} (400%) rather than {Skills.RapidSynthesis.NameOfAction()} (500%) if {Condition.Good.ToLocalizedString()} {ConditionString}", ref MuMeIntensiveGood);
            changed |= ImGui.Checkbox($"If {Condition.Malleable.ToLocalizedString()} {ConditionString} during {Skills.MuscleMemory.NameOfAction()}, use {Skills.HeartAndSoul.NameOfAction()} + {Skills.IntensiveSynthesis.NameOfAction()}", ref MuMeIntensiveMalleable);
            changed |= ImGui.Checkbox($"If at last step of {Skills.MuscleMemory.NameOfAction()} and not {Condition.Centered.ToLocalizedString()} {ConditionString}, use {Skills.IntensiveSynthesis.NameOfAction()} (forcing via {Skills.HeartAndSoul.NameOfAction()} if necessary)", ref MuMeIntensiveLastResort);
            changed |= ImGui.Checkbox($"Use {Skills.Manipulation.NameOfAction()} on {Condition.Primed.ToLocalizedString()} {ConditionString}, if {Skills.Veneration.NameOfAction()} is already active", ref MuMePrimedManip);
            changed |= ImGui.Checkbox($"{Skills.Observe.NameOfAction()} during unfavourable {ConditionString} instead of spending {DurabilityString} on {Skills.RapidSynthesis.NameOfAction()}", ref MuMeAllowObserve);
            ImGui.Text($"Allow {Skills.Manipulation.NameOfAction()} only if more than this amount of steps remain on {Skills.MuscleMemory.NameOfAction()}");
            ImGui.PushItemWidth(250);
            changed |= ImGui.SliderInt("###MumeMinStepsForManip", ref MuMeMinStepsForManip, 0, 5);
            ImGui.Text($"Allow {Skills.Veneration.NameOfAction()} only if more than this amount of steps remain on {Skills.MuscleMemory.NameOfAction()}");
            ImGui.PushItemWidth(250);
            changed |= ImGui.SliderInt("###MuMeMinStepsForVene", ref MuMeMinStepsForVene, 0, 5);
        }
        if (ImGui.CollapsingHeader("Main Rotation Settings"))
        {
            ImGui.Text($"Minimum {Buffs.InnerQuiet.NameOfBuff()} stacks to spend {Skills.HeartAndSoul.NameOfAction()} on {Skills.PreciseTouch.NameOfAction()} (10 to disable)");
            ImGui.PushItemWidth(250);
            changed |= ImGui.SliderInt($"###MidMinIQForHSPrecise", ref MidMinIQForHSPrecise, 0, 10);
            changed |= ImGui.Checkbox($"On low {DurabilityString}, prefer {Skills.Observe.NameOfAction()} over non-{Condition.Pliant.ToLocalizedString()} {Skills.Manipulation.NameOfAction()} before {Buffs.InnerQuiet.NameOfBuff()} has 10 stacks", ref MidBaitPliantWithObservePreQuality);
            changed |= ImGui.Checkbox($"On low {DurabilityString}, prefer {Skills.Observe.NameOfAction()} over non-{Condition.Pliant.ToLocalizedString()} {Skills.Manipulation.NameOfAction()} / {Skills.Innovation.NameOfAction()}+{Skills.TrainedFinesse.NameOfAction()} after {Buffs.InnerQuiet.NameOfBuff()} has 10 stacks", ref MidBaitPliantWithObserveAfterIQ);
            changed |= ImGui.Checkbox($"Use {Skills.Manipulation.NameOfAction()} on {Condition.Primed.ToLocalizedString()} {ConditionString} before {Buffs.InnerQuiet.NameOfBuff()} has 10 stacks", ref MidPrimedManipPreQuality);
            changed |= ImGui.Checkbox($"Use {Skills.Manipulation.NameOfAction()} on {Condition.Primed.ToLocalizedString()} {ConditionString} after {Buffs.InnerQuiet.NameOfBuff()} has 10 stacks, if enough CP is available to utilize {DurabilityString} well", ref MidPrimedManipAfterIQ);
            changed |= ImGui.Checkbox($"Allow {Skills.Observe.NameOfAction()} during unfavourable {ConditionString} without buffs", ref MidKeepHighDuraUnbuffed);
            changed |= ImGui.Checkbox($"Allow {Skills.Observe.NameOfAction()} during unfavourable {ConditionString} under {Buffs.Veneration.NameOfBuff()}", ref MidKeepHighDuraVeneration);
            changed |= ImGui.Checkbox($"Allow {Skills.Veneration.NameOfAction()} if we still have large {ProgressString} deficit (more than {Skills.IntensiveSynthesis.NameOfAction()} can complete) on {Condition.GoodOmen.ToLocalizedString()}", ref MidAllowVenerationGoodOmen);
            changed |= ImGui.Checkbox($"Allow {Skills.Veneration.NameOfAction()} if we still have large {ProgressString} deficit (more than {Skills.RapidSynthesis.NameOfAction()} can complete) after {Buffs.InnerQuiet.NameOfBuff()} has 10 stacks", ref MidAllowVenerationAfterIQ);
            changed |= ImGui.Checkbox($"Spend {Condition.Good.ToLocalizedString()} {ConditionString} on {Skills.IntensiveSynthesis.NameOfAction()} if we need more {ProgressString} without buffs", ref MidAllowIntensiveUnbuffed);
            changed |= ImGui.Checkbox($"Spend {Condition.Good.ToLocalizedString()} {ConditionString} on {Skills.IntensiveSynthesis.NameOfAction()} if we need more {ProgressString} under {Skills.Veneration.NameOfAction()}", ref MidAllowIntensiveVeneration);
            changed |= ImGui.Checkbox($"Spend {Condition.Good.ToLocalizedString()} {ConditionString} on {Skills.PreciseTouch.NameOfAction()} if we need more {Buffs.InnerQuiet.NameOfBuff()} stacks", ref MidAllowPrecise);
            changed |= ImGui.Checkbox($"Consider {Condition.Sturdy.ToLocalizedString()} {ConditionString} {Skills.HeartAndSoul.NameOfAction()} + {Skills.PreciseTouch.NameOfAction()} a good move for building {Buffs.InnerQuiet.NameOfBuff()} stacks", ref MidAllowSturdyPreсise);
            changed |= ImGui.Checkbox($"Consider {Condition.Centered.ToLocalizedString()} {ConditionString} {Skills.HastyTouch.NameOfAction()} a good move for building {Buffs.InnerQuiet.NameOfBuff()} stacks (85% success, 10 {DurabilityString})", ref MidAllowCenteredHasty);
            changed |= ImGui.Checkbox($"Consider {Condition.Sturdy.ToLocalizedString()} {ConditionString} {Skills.HastyTouch.NameOfAction()} a good move for building {Buffs.InnerQuiet.NameOfBuff()} stacks (50% success, 5 {DurabilityString})", ref MidAllowSturdyHasty);
            changed |= ImGui.Checkbox($"Consider {Skills.PreparatoryTouch.NameOfAction()} a good move under {Condition.Good.ToLocalizedString()} {ConditionString} + {Buffs.Innovation.NameOfBuff()} + {Buffs.GreatStrides.NameOfBuff()}, assuming we have enough {DurabilityString}", ref MidAllowGoodPrep);
            changed |= ImGui.Checkbox($"Consider {Skills.PreparatoryTouch.NameOfAction()} a good move under {Condition.Sturdy.ToLocalizedString()} {ConditionString} + {Buffs.Innovation.NameOfBuff()}, assuming we have enough {DurabilityString}", ref MidAllowSturdyPrep);
            changed |= ImGui.Checkbox($"Use {Skills.GreatStrides.NameOfAction()} before {Skills.Innovation.NameOfAction()} + {QualityString} combos", ref MidGSBeforeInno);
            changed |= ImGui.Checkbox($"Finish {ProgressString} before starting {QualityString} phase", ref MidFinishProgressBeforeQuality);
            changed |= ImGui.Checkbox($"{Skills.Observe.NameOfAction()} on {Condition.GoodOmen.ToLocalizedString()} {ConditionString} if we would otherwise use {Skills.TricksOfTrade.NameOfAction()} on {Condition.Good.ToLocalizedString()} {ConditionString}", ref MidObserveGoodOmenForTricks);
        }
        ImGui.Unindent();
        changed |= ImGui.Checkbox("Max out Ishgard Restoration recipes instead of just hitting max breakpoint", ref MaxIshgardRecipes);
        ImGuiComponents.HelpMarker("This will try to maximise quality to earn more Skyward points.");
        changed |= ImGui.Checkbox($"Finisher: use {Skills.CarefulObservation.NameOfAction()} to try baiting {Condition.Good.ToLocalizedString()} {ConditionString} for {Skills.ByregotsBlessing.NameOfAction()}", ref FinisherBaitGoodByregot);
        changed |= ImGui.Checkbox($"Emergency: use {Skills.CarefulObservation.NameOfAction()} to try baiting {Condition.Good.ToLocalizedString()} {ConditionString} for {Skills.TricksOfTrade.NameOfAction()} if really low on CP", ref EmergencyCPBaitGood);
        changed |= ImGui.Checkbox($"Use Material Miracle in Cosmic Exploration", ref UseMaterialMiracle);
        if (ImGuiEx.ButtonCtrl("Reset Expert Solver Settings To Default"))
        {
            P.Config.ExpertSolverConfig = new();
            changed |= true;
        }
        return changed;
    }
}
