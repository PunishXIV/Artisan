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
    // General settings
    public bool EnableExpertProfiles = false;
    public int ImmacMissingDura = 45; // prioritize immaculate mend over manipulation when missing this much durability
    public int ManipClipTurns = 0; // reuse Manipulation with this many turns left
    public enum MidUseTPSetting  // where to use trained perfection
    {
        MidUseTPGroundwork,        // use TP on groundwork for high-prio progress
        MidUseTPPrepIQ,            // use TP on preparatory touch to build IQ stacks
        MidUseTPEitherPreQuality,  // use TP on either of the options above, depending on which condition comes up first (default groundwork)
        MidUseTPDuringQuality      // use TP on prep or precise after 10 IQ, with GS+Inno
    }
    public string GetMidUseTPSettingName(MidUseTPSetting value)
        => value switch
        {
            MidUseTPSetting.MidUseTPGroundwork => $"(Early) {Skills.Groundwork.NameOfAction()}",
            MidUseTPSetting.MidUseTPPrepIQ => $"(Early) {Skills.PreparatoryTouch.NameOfAction()} (build {Buffs.InnerQuiet.NameOfBuff()})",
            MidUseTPSetting.MidUseTPEitherPreQuality => $"(Early) Either action based on {ConditionString.ToLower()}",
            MidUseTPSetting.MidUseTPDuringQuality => $"(Late) Optimal {QualityString.ToLower()} action at max {Buffs.InnerQuiet.NameOfBuff()} (focus {QualityString.ToLower()})",
            _ => throw new NotImplementedException()
        };
    public MidUseTPSetting MidUseTP = MidUseTPSetting.MidUseTPDuringQuality;
    public int MidMaxBaitStepsForTP = 0; // how many observes should be used to bait favorable conditions for trained perfection; 0 to disable
    public bool MaxIshgardRecipes;
    public bool MaxCosmicRecipes = true; // if true, goes for max quality on CE recipes with multiple quality thresholds instead of q3
    public bool OverrideCosmicRecipeSettings = false; // if true, use this profile's cosmic settings instead of the per-recipe settings
    public int MaxMaterialMiracleUses = 0; // how many charges of Material Miracle to use per craft, if available
    public enum MMSet  // when should we use material miracle?
    {
        Steps,         // after X steps, regardless of what else is happening
        Opener,        // at the start of the opener, after the initial action
        AfterOpener,   // before building IQ
        Quality        // at 10 IQ stacks
    }
    public string GetMMSet(MMSet value)
        => value switch
        {
            MMSet.Steps => $"After X steps",
            MMSet.Opener => $"After the special opener action",
            MMSet.AfterOpener => $"At the start of the {Buffs.InnerQuiet.NameOfBuff()} phase",
            MMSet.Quality => $"At the start of the {QualityString} phase",
            _ => throw new NotImplementedException()
        };
    public MMSet UseMMWhen = MMSet.Steps;
    public int MinimumStepsBeforeMiracle = 10;
    public int MaxSteadyUses = 1; // how many charges of Stellar Steady Hand to use per craft, if available
    public bool DebugObserveOnly = false; // special debug flag for collecting condition data
    public bool DebugInnovateOnly = false; // special debug flag for collecting condition data


    // Opener settings
    public enum OpenerSet  // what should we open with?
    {
        Best,              // let artisan decide based on ???
        Reflect,
        MuMe
    }
    public string GetOpenerSet(OpenerSet value)
        => value switch
        {
            OpenerSet.Best => $"Automatically decide",
            OpenerSet.Reflect => $"Always use {Skills.Reflect.NameOfAction()}",
            OpenerSet.MuMe => $"Always use {Skills.MuscleMemory.NameOfAction()}",
            _ => throw new NotImplementedException()
        };
    public OpenerSet OpenerAction = OpenerSet.MuMe;
    public bool ReflectQuickInno = false; // if true, use quick innovation before reflect
    public bool MuMeIntensiveGood = true; // if true, we allow spending mume on intensive (400p) rather than rapid (500p) if good condition procs
    public bool MuMeIntensiveMalleable = false; // if true and we have malleable during mume, use intensive rather than hoping for rapid
    public bool MuMePrimedManip = false; // if true, allow using primed manipulation after veneration is up on mume
    public bool MuMeAllowObserve = false; // if true, observe rather than use actions during unfavourable conditions to conserve durability
    public bool MuMeIntensiveLastResort = true; // if true and we're on last step of mume, use intensive (forcing via H&S if needed) rather than hoping for rapid (unless we have centered)
    public int MuMeMinStepsForManip = 2; // if this or less rounds are remaining on mume, don't use manipulation under favourable conditions
    public int MuMeMinStepsForVene = 1; // if this or less rounds are remaining on mume, don't use veneration


    // Pre-quality settings - General
    public bool MidBaitPliantWithObservePreQuality = true; // if true, when very low on durability and without manip active during pre-quality phase, we use observe rather than normal manip
    public enum PQPrimedManipSet  // (pre-quality) when to use manipulation on primed
    {
        NoPliant,                 // only if the recipe doesn't have pliant
        Always,                   // on any recipe
        Never                     // don't do it girl. I know you wanna but don't
    }
    public string GetPQPrimedManipSet(PQPrimedManipSet value)
        => value switch
        {
            PQPrimedManipSet.NoPliant => $"Only if the recipe does not have {Condition.Pliant.ToLocalizedString()}",
            PQPrimedManipSet.Always => $"Always",
            PQPrimedManipSet.Never => $"Never",
            _ => throw new NotImplementedException()
        };
    public PQPrimedManipSet PQPrimedManip = PQPrimedManipSet.NoPliant;
    public enum PQWasteNotSet  // (pre-quality) when to use waste not
    {
        NoPliant,              // only if the recipe doesn't have pliant
        Always,                // on any recipe
        Never                  // waste not bad. I love waste
    }
    public string GetPQWasteNotSet(PQWasteNotSet value)
        => value switch
        {
            PQWasteNotSet.NoPliant => $"Only if the recipe does not have {Condition.Pliant.ToLocalizedString()}",
            PQWasteNotSet.Always => $"Always",
            PQWasteNotSet.Never => $"Never",
            _ => throw new NotImplementedException()
        };
    public PQWasteNotSet PQWasteNot = PQWasteNotSet.Never;
    public int PQWasteNotMaxIQ = 4; // (pre-quality) don't use waste not above this many IQ stacks


    // Pre-quality settings - Progress
    public enum WhenToForceProgressSetting  // when in the crafting logic should we ensure finishable progress?
    {
        WhenToForceProgressNever,           // never force progress, just hope for procs
        WhenToForceProgressBeforeQuality,   // finish progress at 10 IQ stacks
        WhenToForceProgressASAP             // finish progress immediately after opener
    }
    public string GetWhenToForceProgressSettingName(WhenToForceProgressSetting value)
        => value switch
        {
            WhenToForceProgressSetting.WhenToForceProgressNever => $"Never force progress",
            WhenToForceProgressSetting.WhenToForceProgressBeforeQuality => $"Force progress upon reaching 10 {Buffs.InnerQuiet.NameOfBuff()} stacks",
            WhenToForceProgressSetting.WhenToForceProgressASAP => $"Force progress ASAP after opener",
            _ => throw new NotImplementedException()
        };
    public WhenToForceProgressSetting WhenToForceProgress = WhenToForceProgressSetting.WhenToForceProgressBeforeQuality;
    public bool ForceProgressVene = false; // use veneration when forcing progress if we need more than one rapid's worth of progress
    public int ForceProgressMaxBait = 0; // number of observes to use on better conditions when forcing progress; -1 for no limit, 0 to disable
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
            MidKeepHighDuraSetting.MidUseDura => $"Don't use {Skills.Observe.NameOfAction()}, just keep going",
            _ => throw new NotImplementedException()
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
            MidAllowIntensiveSetting.MidAllowIntensiveUnbuffed => $"Use {Skills.IntensiveSynthesis.NameOfAction()} regardless of buffs",
            _ => throw new NotImplementedException()
        };
    public MidAllowIntensiveSetting MidAllowIntensive = MidAllowIntensiveSetting.MidNoIntensive;
    public bool MidAllowVenerationGoodOmen = true; // if true, we allow using veneration during iq phase if we lack a lot of progress on good omen


    // Pre-quality settings - Inner Quiet
    public enum PQGoodPreciseSet  // can we use precise touch on good?
    {
        NoPliant,                 // only on recipes without Pliant
        Always,
        Never
    }
    public string GetPQGoodPreciseSet(PQGoodPreciseSet value)
        => value switch
        {
            PQGoodPreciseSet.NoPliant => $"Only if the recipe does not have {Condition.Pliant.ToLocalizedString()}",
            PQGoodPreciseSet.Always => $"Always",
            PQGoodPreciseSet.Never => $"Never",
            _ => throw new NotImplementedException()
        };
    public PQGoodPreciseSet PQGoodPrecise = PQGoodPreciseSet.Always;
    public bool MidAllowSturdyPreсise = false; // if true,we consider sturdy+h&s+precise touch a good move for building iq
    public int MidMinIQForHSPrecise = 10; // min iq stacks where we use h&s+precise; 10 to disable
    public bool PQAdvancedCombo = false; // if true, prefer observe + advanced combo over prudent to build IQ
    public bool MidAllowCenteredHasty = true; // if true, we consider centered hasty touch a good move for building iq (85% reliability)
    public bool MidAllowSturdyHasty = true; // if true, we consider sturdy hasty touch a good move for building iq (60% reliability), otherwise we use combo


    // Pre-quality settings - Quality
    public int PQPrimedInnoIQ = 10; // (pre-quality) use inno under primed at this many IQ stacks (10 to disable)
    public int PQOtherInnoIQ = 10;  // (pre-quality) use inno otherwise at this many IQ stacks (10 to disable)


    // Quality settings - General
    public bool MidBaitPliantWithObserveAfterIQ = true; // if true, when very low on durability and without manip active after iq has 10 stacks, we use observe rather than normal manip or inno+finnesse
    public bool MidPrimedManipAfterIQ = true; // if true, allow using primed manipulation during after iq has 10 stacks
    public bool MidObserveGoodOmenForTricks = false; // if true, we'll observe on good omen where otherwise we'd use tricks on good


    // Quality settings - Progress
    public bool MidAllowVenerationAfterIQ = true; // if true, we allow using veneration after iq is fully stacked if we still lack a lot of progress


    // Quality settings - Quality
    public bool MidAllowGoodPrep = false; // if true, we consider prep touch a good move for finisher under good+inno+gs
    public bool MidAllowSturdyPrep = true; // if true, we consider prep touch a good move for finisher under sturdy+inno
    public bool MidGSBeforeInno = true; // if true, we start quality combos with gs+inno rather than just inno
    public enum QQuickInnoGoodSet  // how to use quick innovation on Good+GS procs
    {
        Any,                       // use on precise (or whatever)
        PrepTP,                    // only use if TP+prep is set up
        Disable                    // save it for finisher
    }
    public string GetQQuickInnoGoodSet(QQuickInnoGoodSet value)
        => value switch
        {
            QQuickInnoGoodSet.Any => $"Use {Skills.QuickInnovation.NameOfAction()} on any quality action",
            QQuickInnoGoodSet.PrepTP => $"Only use {Skills.QuickInnovation.NameOfAction()} on free {Skills.PreparatoryTouch.NameOfAction()}",
            QQuickInnoGoodSet.Disable => $"Don't use {Skills.QuickInnovation.NameOfAction()} (save for finisher)",
            _ => throw new NotImplementedException()
        };
    public QQuickInnoGoodSet QQuickInnoGood = QQuickInnoGoodSet.Disable;


    // Finisher settings
    public bool FinisherBaitGoodByregot = true; // if true, use careful observations to try baiting good byregot
    public bool EmergencyCPBaitGood = false; // if true, we allow spending careful observations to try baiting good for tricks when we really lack cp
    public bool FinisherUseQuickInno = true; // if true, use quick innovation to finish in an emergency
	public bool RapidSynthYoloAllowed = true; // if false, expert crafting may lock up midway, so not good for AFK crafting. This yolo however is likely to fail the craft, so disabling gives opportunity for intervention
}
