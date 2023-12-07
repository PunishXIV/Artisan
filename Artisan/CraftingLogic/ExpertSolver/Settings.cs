using ImGuiNET;

namespace Artisan.CraftingLogic.ExpertSolver;

public class Settings
{
    public bool Enabled = true;
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
    public bool MidBaitPliantWithObserveAfterIQ = true; // if true, when very low on durability and without manip active after iq is stacked, we use observe rather than normal manip or inno+finnesse
    public bool MidPrimedManipPreQuality = true; // if true, allow using primed manipulation during pre-quality phase
    public bool MidPrimedManipAfterIQ = true; // if true, allow using primed manipulation during after iq is stacked
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
    public bool MidFinishProgressBeforeQuality = true; // if true, at 10 iq we first finish progress before starting on quality
    public bool MidObserveGoodOmenForTricks = false; // if true, we'll observe on good omen where otherwise we'd use tricks on good
    public bool FinisherBaitGoodByregot = true; // if true, use careful observations to try baiting good byregot
    public bool EmergencyCPBaitGood = false; // if true, we allow spending careful observations to try baiting good for tricks when we really lack cp

    public bool Draw()
    {
        bool changed = false;
        changed |= ImGui.Checkbox("Enable experimental expert solver", ref Enabled);
        changed |= ImGui.Checkbox("Use Reflect instead of MuMe in opener", ref UseReflectOpener);
        changed |= ImGui.Checkbox("MuMe: allow spending mume on intensive (400p) rather than rapid (500p) if good condition procs", ref MuMeIntensiveGood);
        changed |= ImGui.Checkbox("MuMe: if malleable during mume, use H&S + intensive", ref MuMeIntensiveMalleable);
        changed |= ImGui.Checkbox("MuMe: if at last step of mume and not centered, use intensive (forcing via H&S if necessary)", ref MuMeIntensiveLastResort);
        changed |= ImGui.Checkbox("MuMe: use primed manipulation, if veneration is already active", ref MuMePrimedManip);
        changed |= ImGui.Checkbox("MuMe: observe during unfavourable conditions instead of spending dura on normal rapids", ref MuMeAllowObserve);
        changed |= ImGui.SliderInt("MuMe: allow manipulation only if more than this amount of steps remain on mume", ref MuMeMinStepsForManip, 0, 5);
        changed |= ImGui.SliderInt("MuMe: allow veneration only if more than this amount of steps remain on mume", ref MuMeMinStepsForVene, 0, 5);
        changed |= ImGui.SliderInt("Mid: min iq stacks to spend h&s on precise (10 to disable)", ref MidMinIQForHSPrecise, 0, 10);
        changed |= ImGui.Checkbox("Mid: on low dura, prefer observe to non-pliant manip before iq is stacked", ref MidBaitPliantWithObservePreQuality);
        changed |= ImGui.Checkbox("Mid: on low dura, prefer observe to non-pliant manip / inno+finesse after iq is stacked", ref MidBaitPliantWithObserveAfterIQ);
        changed |= ImGui.Checkbox("Mid: use manipulation on primed before iq is stacked", ref MidPrimedManipPreQuality);
        changed |= ImGui.Checkbox("Mid: use manipulation on primed after iq is stacked, if enough cp is available to utilize durability well", ref MidPrimedManipAfterIQ);
        changed |= ImGui.Checkbox("Mid: allow observes during unfavourable conditions without buffs", ref MidKeepHighDuraUnbuffed);
        changed |= ImGui.Checkbox("Mid: allow observes during unfavourable conditions under veneration", ref MidKeepHighDuraVeneration);
        changed |= ImGui.Checkbox("Mid: allow veneration if we still have large progress deficit (> intensive) on good omen", ref MidAllowVenerationGoodOmen);
        changed |= ImGui.Checkbox("Mid: allow veneration if we still have large progress deficit (> rapid) after iq is stacked", ref MidAllowVenerationAfterIQ);
        changed |= ImGui.Checkbox("Mid: spend good procs on intensive synth if we need more progress without buffs", ref MidAllowIntensiveUnbuffed);
        changed |= ImGui.Checkbox("Mid: spend good procs on intensive synth if we need more progress under veneration", ref MidAllowIntensiveVeneration);
        changed |= ImGui.Checkbox("Mid: spend good procs on precise touch if we need more iq stacks", ref MidAllowPrecise);
        changed |= ImGui.Checkbox("Mid: consider sturdy h&s+precise touch a good move for building iq stacks", ref MidAllowSturdyPreсise);
        changed |= ImGui.Checkbox("Mid: consider centered hasty a good move for building iq stacks (85% success, 10 dura)", ref MidAllowCenteredHasty);
        changed |= ImGui.Checkbox("Mid: consider sturdy hasty a good move for building iq stacks (50% success, 5 dura)", ref MidAllowSturdyHasty);
        changed |= ImGui.Checkbox("Mid: consider prep touch a good move under good+inno+gs, assuming we have enough dura", ref MidAllowGoodPrep);
        changed |= ImGui.Checkbox("Mid: consider prep touch a good move under sturdy+inno, assuming we have enough dura", ref MidAllowSturdyPrep);
        changed |= ImGui.Checkbox("Mid: use gs before inno+quality combos", ref MidGSBeforeInno);
        changed |= ImGui.Checkbox("Mid: finish progress before starting quality phase", ref MidFinishProgressBeforeQuality);
        changed |= ImGui.Checkbox("Mid: observe on good omen if we would otherwise use tricks on good", ref MidObserveGoodOmenForTricks);
        changed |= ImGui.Checkbox("Finisher: use careful observation to try baiting good for byregot", ref FinisherBaitGoodByregot);
        changed |= ImGui.Checkbox("Emergency: use careful observation to try baiting good for tricks if really low on cp", ref EmergencyCPBaitGood);
        return changed;
    }
}
