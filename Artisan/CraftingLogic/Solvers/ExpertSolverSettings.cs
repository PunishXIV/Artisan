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
        MidUseTPDuringQuality      // use TP on prep or precise after 10 IQ, with GS+Inno
    }
    public string GetMidUseTPSettingName(MidUseTPSetting value)
        => value switch
        {
            MidUseTPSetting.MidUseTPGroundwork => $"(Early) {Skills.Groundwork.NameOfAction()}",
            MidUseTPSetting.MidUseTPPrepIQ => $"(Early) {Skills.PreparatoryTouch.NameOfAction()} (build {Buffs.InnerQuiet.NameOfBuff()})",
            MidUseTPSetting.MidUseTPEitherPreQuality => $"(Early) Either action based on {ConditionString.ToLower()}",
            MidUseTPSetting.MidUseTPDuringQuality or _ => $"(Late) Optimal {QualityString.ToLower()} action at max {Buffs.InnerQuiet.NameOfBuff()} (focus {QualityString.ToLower()})",
        };
    public MidUseTPSetting MidUseTP = MidUseTPSetting.MidUseTPDuringQuality;
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
    public bool MidAllowGoodPrep = false; // if true, we consider prep touch a good move for finisher under good+inno+gs
    public bool MidAllowSturdyPrep = true; // if true, we consider prep touch a good move for finisher under sturdy+inno
    public bool MidGSBeforeInno = true; // if true, we start quality combos with gs+inno rather than just inno
    public enum MidAllowQuickInnoGoodSetting  // how to use quick innovation on Good+GS procs
    {
        MidAllowQuickInnoGoodAny,             // use on precise (or whatever)
        MidAllowQuickInnoGoodPrepTP,          // only use if TP+prep is set up
        MidAllowQuickInnoGoodDisable          // save it for finisher
    }
    public string GetMidAllowQuickInnoGoodSettingName(MidAllowQuickInnoGoodSetting value)
        => value switch
        {
            MidAllowQuickInnoGoodSetting.MidAllowQuickInnoGoodAny => $"Use {Skills.QuickInnovation.NameOfAction()} on any quality action",
            MidAllowQuickInnoGoodSetting.MidAllowQuickInnoGoodPrepTP => $"Only use {Skills.QuickInnovation.NameOfAction()} on free {Skills.PreparatoryTouch.NameOfAction()}",
            MidAllowQuickInnoGoodSetting.MidAllowQuickInnoGoodDisable or _ => $"Don't use {Skills.QuickInnovation.NameOfAction()} (save for finisher)"
        };
    public MidAllowQuickInnoGoodSetting MidAllowQuickInnoGood = MidAllowQuickInnoGoodSetting.MidAllowQuickInnoGoodAny;
    public bool MidFinishProgressBeforeQuality = false; // if true, at 10 iq we first finish progress before starting on quality
    public bool MidObserveGoodOmenForTricks = false; // if true, we'll observe on good omen where otherwise we'd use tricks on good
    public bool FinisherBaitGoodByregot = true; // if true, use careful observations to try baiting good byregot
    public bool FinisherUseQuickInno = true; // if true, use quick innovation to finish in an emergency
    public bool EmergencyCPBaitGood = false; // if true, we allow spending careful observations to try baiting good for tricks when we really lack cp
	public bool RapidSynthYoloAllowed = true; // if false, expert crafting may lock up midway, so not good for AFK crafting. This yolo however is likely to fail the craft, so disabling gives opportunity for intervention

    public bool OverrideCosmicRecipeSettings = false; // if true, use this profile's cosmic settings instead of the per-recipe settings
    public bool UseMaterialMiracle = false;
	public int MinimumStepsBeforeMiracle = 10;
    public int MaxSteadyUses = 1; // how many charges of Stellar Steady Hand to use per craft, if available
}
