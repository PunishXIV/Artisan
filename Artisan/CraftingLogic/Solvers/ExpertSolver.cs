using Artisan.CraftingLogic.CraftData;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using System.Collections.Generic;
using TerraFX.Interop.Windows;
using static Artisan.CraftingLogic.Solvers.ExpertSolverSettings;

namespace Artisan.CraftingLogic.Solvers;

public class ExpertSolverDefinition : ISolverDefinition
{
    public string MouseoverDescription { get; set; } = "This is for expert recipes, it is not a more advanced standard recipe solver.";

    public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
    {
        if (craft.CraftExpert)
            yield return new(this, 0, 2, "Expert Recipe Solver", craft.StatLevel < 90 ? "Requires Level 90" : !craft.UnlockedManipulation ? "You need to unlock Manipulation" : "");
    }

    public Solver Create(CraftState craft, int flavour) => new ExpertSolver();

    public IEnumerable<ISolverDefinition.Desc> Flavours()
    {
        yield return new(this, 0, 2, "Expert Recipe Solver");
    }
}

// todo, a big one: many experts don't use these odds, the solver should dynamically calculate various optimal logic based on actual odds
// some thoughts:
// - any time we want to regain some dura, we can bait pliant and use manip
// - assuming 12% pliant, 3% good, 12% good omen, average cost to get pliant from random starting point is ~24cp
// - this means that estimated manip cost is 24+48 = 72, meaning 10dura is worth ~18cp
// - if we were to use full cost manip as a rule, it would be worth ~24cp instead - but we should rarely be doing that
// - if we start in some non-interesting state, we would need to pay ~31cp to get pliant (because we pay 7cp to start from 'random' state)
// repair actions:
// - if we get random pliant and manip is not active, using manip is effectively winning 24cp (because we don't need to bait), which is the baseline worth of pliant
// - using pliant mm is getting 30dura = 54cp for 44cp, meaning it's effective 10cp win
// - using pliant manip to clip 1 stack is getting 35dura = 63cp for 48cp, meaning it's effective 15cp win, making it better than mm
// - using pliant manip to clip 2 stacks is getting 30dura = 54cp for 48cp, meaning is's 8cp win compared to 10cp mm - which makes sense, as it's a strict loss
// - primed manip is free 10dura = 18cp for full cost, meaning it's ~30cp more expensive than pliant; observing would be ~31cp, meaning generally pliant is an ok alternative
// assuming 60dura craft, the real dura sweet spot is exactly 25dura
// - any more and we can't use manip+mm on double pliant
// - any less and we might be forced to use observe and waste conditions
// - so general flow is - exploit buffs (mume/vene/inno), outside buffs get manip and get to sweet spot before starting next phase, exploit random procs for iq/progress if while waiting
// synth action comparison
// - basic synth is 120p for 18cp = 6.67p/cp - baseline to finish the craft
// - careful synth is 180p for 7+18cp = 7.2p/cp - slightly more efficient way to finish the craft
// - prudent synth is 180p for 18+9cp = 6.67p/cp -  alternative way to finish the craft, with different cp/durability cost spread
// - groundwork is 360p for 18+36cp = 6.67p/cp - very expensive durability wise, TODO consider for veneration/waste-not utilization?
// - rapid synth is 500p for 18cp with 50% success (75% under centered) = 13.89p/cp (20.83p/cp under centered) - this is extremely efficient and thus is our baseline
// - intensive synth is 400p for 6+18cp = 16.67p/cp (9.09p/cp if we assume good is worth 20cp) - very efficient, good alternative to rapid if gamble is undesired, uses up good condition
// touch action comparison
// - basic touch combo is 1iq + 100/125/150p for 18+18cp = 2.78/3.47/4.17p/cp - too restrictive for reacting to procs, too inefficient if broken, maybe useful under waste-not?..
// - hasty touch is 100p for 1iq + 18cp with 60% success (85% under centered) = 3.33p/cp (4.72p/cp under centered) - decent way to exploit centered/sturdy for iq
// - observe+advanced touch is 1iq + 150p for 7+18+18cp = 3.49p/cp - this is a baseline for finisher phase, very good opportunity to react for procs
// - prep touch is 2iq + 200p for 40+36cp = 2.63p/cp - very expensive and not very efficient, but still the best quality action, so decent to exploit good/sturdy for quality if under gs+inno
// - basic+refined touch is 3iq + 200p for 18+24(+18+18)cp = 2.56p/cp - a lot like prep but slightly less efficient and much less flexible, not worth using
// - precise touch is 2iq + 150p for 18+18cp = 4.17p/cp (2.67p/cp if we assume good is worth 20cp) - very efficient way of getting iq, and decent way to exploit good for quality under gs/inno
// - prudent touch is 1iq + 100p for 25+9cp = 2.94p/cp - very efficient way of getting iq, and a reasonable alternative for finisher when low on dura
// - finesse is 100p for 32cp = 3.13p/cp - even better alternative for finisher when low on dura, quite expensive cp wise 
// general flow
// - if starting with mume, use buff for rapid/intensive, and potentially use vene for some more free progress
// -- one concern here is using too much dura here and being forced to bait for pliant and waste good conditions
// - after mume (or from start, if not using it), aim for progress and iq
// -- if low on dura, observe on useless conditions to preserve dura and bait pliant
// -- if high on dura and need more progress, consider vene + rapid spam
// - after reaching 10iq, focus on quality instead - use gs/inno combos
// -- consider what to do with progress: either force finish before starting quality (wastes good opportunities like centered later between combos) or just start quality combos immediately (harder to estimate needed cp to finish the craft)
// - after reaching quality cap, just get progress
public class ExpertSolver : Solver
{
    const int maxIQStacks = 10;
    const int mmDuraRestored = 30;
    // MM is 2.93 cp/dura, so immaculate is a better value at 40+ dura restored. it's still 24 more CP, though, so add a lil buffer
    const int immaculateDuraMinimum = 45;
    const int maxIQStacksForWasteNot = 4;

    public override Recommendation Solve(CraftState craft, StepState step) => SolveNextStep(P.Config.ExpertSolverConfig, craft, step);

    public static Recommendation SolveNextStep(ExpertSolverSettings cfg, CraftState craft, StepState step)
    {
        // see what we need to finish the craft
        var remainingProgress = craft.CraftProgress - step.Progress;
        var estBasicSynthProgress = Simulator.BaseProgress(craft) * 120 / 100;
        var estCarefulSynthProgress = Simulator.BaseProgress(craft) * 180 / 100; // minimal, assuming no procs/buffs
        var reservedCPForProgress = remainingProgress <= estBasicSynthProgress ? 0 : Skills.CarefulSynthesis.StandardCPCost();
        var progressDeficit = remainingProgress - estCarefulSynthProgress; // if >0, we need more progress before we can start finisher
        var cpAvailableForQuality = step.RemainingCP - reservedCPForProgress;

        var qualityTarget = craft.IshgardExpert && cfg.MaxIshgardRecipes ? craft.CraftQualityMax : craft.CraftQualityMin3; // TODO: reconsider, this is a bit of a hack
        if (step.Index == 1)
        {
            // always open with special action
            // comparison:
            // - mume is worth ~800p of progress (assuming we spend the buff on rapid), which is approximately equal to 3.2 rapids, which is 32 dura or ~76.8cp
            // - reflect is worth ~2 prudents of iq stacks minus 100p of quality, which is approximately equal to 50cp + 10 dura or ~74cp minus value of quality
            // so on paper mume seems to be better
            return new(cfg.UseReflectOpener || Simulator.CalculateProgress(craft, step, Skills.MuscleMemory) >= craft.CraftProgress || craft.CraftDurability <= 20 ? Skills.Reflect : Skills.MuscleMemory, "opener");
        }

        if (step.MuscleMemoryLeft > 0) // mume still active - means we have very little progress and want more progress asap
            return new(SafeCraftAction(craft, step, SolveOpenerMuMe(cfg, craft, step)), "mume");

        // todo: don't override useful conditions with material miracle?
        if (cfg.UseMaterialMiracle && step.Index >= cfg.MinimumStepsBeforeMiracle && Simulator.CanUseAction(craft, step, Skills.MaterialMiracle))
            return new(Skills.MaterialMiracle);

        // see if we can do byregot right now and top up quality
        var finishQualityAction = SolveFinishQuality(cfg, craft, step, cpAvailableForQuality, qualityTarget);
        if (finishQualityAction.Action != Skills.None)
            return finishQualityAction;

        var isMid = step.Quality < qualityTarget && (step.Quality < craft.CraftQualityMin1 || cpAvailableForQuality >= Skills.ByregotsBlessing.StandardCPCost());
        if (isMid)
        {
            // we still need quality and have cp available - we're mid craft
            var midAction = SolveMid(cfg, craft, step, progressDeficit, cpAvailableForQuality);
            if (step.RemainingCP >= Simulator.GetCPCost(step, midAction.Action))
                return midAction;
            // try restoring some cp...
            var emergencyAction = EmergencyRestoreCP(cfg, craft, step);
            if (emergencyAction != Skills.None)
                return new(emergencyAction, "mid: emergency cp");
            // oh well, bail...
        }

        // try to finish the craft
        return new(SolveFinishProgress(craft, step, qualityTarget), isMid ? "finish emergency" : "finish");
    }

    private static Skills SolveOpenerMuMe(ExpertSolverSettings cfg, CraftState craft, StepState step)
    {
        bool lastChance = step.MuscleMemoryLeft == 1;
        int rapidDura = Simulator.GetDurabilityCost(step, Skills.RapidSynthesis);

        if (step.SteadyHandLeft > 0 && step.Durability > rapidDura && CU(craft, step, Skills.RapidSynthesis))
        {
            // use steady hand while we have it
            return Skills.RapidSynthesis;
        }
        if (step.Condition == Condition.Pliant && !lastChance)
        {
            // pliant is manip > vene > ignore
            if (step.MuscleMemoryLeft > cfg.MuMeMinStepsForManip && step.ManipulationLeft == 0 && CU(craft, step, Skills.Manipulation))
                return Skills.Manipulation;
            if (step.MuscleMemoryLeft > cfg.MuMeMinStepsForVene && step.VenerationLeft == 0 && CU(craft, step, Skills.Veneration))
                return Skills.Veneration;
        }
        if (step.Condition == Condition.Primed && !lastChance)
        {
            // primed is vene > manip > ignore
            if (step.MuscleMemoryLeft > cfg.MuMeMinStepsForVene && step.VenerationLeft == 0 && CU(craft, step, Skills.Veneration))
                return Skills.Veneration;
            if (cfg.MuMePrimedManip && step.MuscleMemoryLeft > cfg.MuMeMinStepsForManip && step.ManipulationLeft == 0 && CU(craft, step, Skills.Manipulation))
                return Skills.Manipulation;
        }
        // high-prio manip regardless of condition (will usually only happen on very low dura recipes)
        if (step.ManipulationLeft == 0 && CU(craft, step, Skills.Manipulation))
        {
            // respect the manip setting on low but usable dura
            if (step.MuscleMemoryLeft > cfg.MuMeMinStepsForManip && step.Durability <= rapidDura + 5 && !lastChance)
                return Skills.Manipulation;
            // force manip regardless of setting if we would fail otherwise
            if (step.Durability <= rapidDura)
                return Skills.Manipulation;
        }
        // set up stellar steady hand to guarantee a rapid
        if (CU(craft, step, Skills.SteadyHand) && step.SteadyHandsUsed < cfg.MaxSteadyUses && !lastChance)
        { 
            // make sure veneration is up first, if we have time
            if (step.VenerationLeft == 0 && step.MuscleMemoryLeft > 2 && CU(craft, step, Skills.Veneration))
                return Skills.Veneration;

            return Skills.SteadyHand;
        }
        if (step.Condition == Condition.Centered && step.Durability > rapidDura)
        {
            // centered rapid is very good value, even disregarding last-chance or veneration concerns
            if (CU(craft, step, Skills.RapidSynthesis))
                return Skills.RapidSynthesis;
        }
        if (step.Condition is Condition.Sturdy or Condition.Robust && step.Durability > rapidDura && lastChance)
        {
            // last-chance half-dura intensive/rapid, regardless of veneration
            return SolveOpenerMuMeTouch(craft, step, cfg.MuMeIntensiveLastResort);
        }
        if (step.Condition == Condition.Malleable && step.Durability > rapidDura)
        {
            // last-chance OR preferred intensive/rapid, regardless of veneration
            return SolveOpenerMuMeTouch(craft, step, cfg.MuMeIntensiveMalleable || cfg.MuMeIntensiveLastResort && lastChance);
        }
        if (step.Condition == Condition.Good && cfg.MuMeIntensiveGood && Simulator.GetDurabilityCost(step, Skills.IntensiveSynthesis) < step.Durability)
        {
            // good and we want to spend on intensive
            if (CU(craft, step, Skills.IntensiveSynthesis))
                return Skills.IntensiveSynthesis;
        }

        // ok we have a condition that isn't as important as manip or veneration
        // force manip, regardless of settings or condition, if we can't do anything else
        if (step.Durability <= rapidDura && step.ManipulationLeft == 0 && CU(craft, step, Skills.Manipulation))
            return Skills.Manipulation;
        if (step.MuscleMemoryLeft > cfg.MuMeMinStepsForVene && step.VenerationLeft == 0 && CU(craft, step, Skills.Veneration))
            return Skills.Veneration;

        // half-dura conditions are a worthy usage of durability at this point (don't observe)
        if (step.Condition is Condition.Sturdy or Condition.Robust && step.Durability > rapidDura && CU(craft, step, Skills.RapidSynthesis))
            return SolveOpenerMuMeTouch(craft, step, cfg.MuMeIntensiveLastResort && lastChance);
        if (cfg.MuMeAllowObserve && step.MuscleMemoryLeft > 1 && step.Durability < craft.CraftDurability)
        {
            // conserve durability rather than gamble away
            if (step.Condition == Condition.Good && CU(craft, step, Skills.TricksOfTrade))
                return Skills.TricksOfTrade; // a better observe than observe
            if (CU(craft, step, Skills.Observe))
                return Skills.Observe;
        }

        // make absolutely sure we're not about to break; maybe manip and mume are both still up
        if (step.Durability <= rapidDura && CU(craft, step, Skills.Observe))
            return Skills.Observe;

        return SolveOpenerMuMeTouch(craft, step, cfg.MuMeIntensiveLastResort && lastChance);
    }

    private static Skills SolveOpenerMuMeTouch(CraftState craft, StepState step, bool intensive)
        => !intensive ? Skills.RapidSynthesis : Simulator.CanUseAction(craft, step, Skills.IntensiveSynthesis) ? Skills.IntensiveSynthesis : step.HeartAndSoulAvailable && CU(craft, step, Skills.HeartAndSoul) ? Skills.HeartAndSoul : Skills.RapidSynthesis;

    private static Recommendation SolveMid(ExpertSolverSettings cfg, CraftState craft, StepState step, int progressDeficit, int availableCP)
    {
        // we'll need to get gs up for byregot and maybe reapply inno if we do not go for the quality finisher now
        var reservedCPForFinisher = Skills.ByregotsBlessing.StandardCPCost() + 
                                    Skills.GreatStrides.StandardCPCost() + 
                                    (step.InnovationLeft > 2 || step.QuickInnoLeft > 0 ? 0 : Skills.Innovation.StandardCPCost()); 
        if (step.IQStacks < maxIQStacks || progressDeficit > 0 && cfg.MidFinishProgressBeforeQuality)
        {
            return SolveMidPreQuality(cfg, craft, step, progressDeficit, availableCP);
        }
        else if (step.GreatStridesLeft == 0 && step.InnovationLeft == 0)
        {
            return SolveMidStartQuality(cfg, craft, step, progressDeficit, availableCP, reservedCPForFinisher);
        }
        else
        {
            return SolveMidQuality(cfg, craft, step, availableCP, reservedCPForFinisher);
        }
    }

    private static Recommendation SolveMidPreQuality(ExpertSolverSettings cfg, CraftState craft, StepState step, int progressDeficit, int availableCP)
    {
        // for some logic that cares about dura left, we need to be more flexible on low-dura crafts
        var duraThreshold = craft.CraftDurability <= 35 ? 15 : 25;

        // build up iq, or finish up progress before moving to quality
        // see if there are nice conditions to exploit
        var venerationActive = progressDeficit > 0 && step.VenerationLeft > 0;
        var allowObserveOnLowDura = venerationActive ? cfg.MidKeepHighDura == ExpertSolverSettings.MidKeepHighDuraSetting.MidKeepHighDuraVeneration : cfg.MidKeepHighDura == ExpertSolverSettings.MidKeepHighDuraSetting.MidKeepHighDuraUnbuffed;
        var allowIntensive = venerationActive ? cfg.MidAllowIntensive == ExpertSolverSettings.MidAllowIntensiveSetting.MidAllowIntensiveVeneration : cfg.MidAllowIntensive == ExpertSolverSettings.MidAllowIntensiveSetting.MidAllowIntensiveUnbuffed;
        var allowPrecise = cfg.MidAllowPrecise && (!allowObserveOnLowDura || step.ManipulationLeft > 0 || step.Durability > duraThreshold) /*&& !venerationActive*/;

        // active steady+rapid is highest prio if we're not super low on dura (rapid + 5)
        if (step.SteadyHandLeft > 0 && progressDeficit > 0 && step.Durability > Simulator.GetDurabilityCost(step, Skills.RapidSynthesis) + 5 && CU(craft, step, Skills.RapidSynthesis))
            return new(SafeCraftAction(craft, step, Skills.RapidSynthesis), "mid pre quality: steady hand");

        // check durability to make sure we don't waste pliant/etc.
        var duraAction = SolveMidDurabilityPreQuality(cfg, craft, step, availableCP, allowObserveOnLowDura, progressDeficit > 0);
        if (duraAction != Skills.None)
            return new(duraAction, "mid pre quality: durability");

        // continue prioritizing steady+rapid; we want to use our very few steps' worth of steady
        if (step.SteadyHandLeft > 0 && progressDeficit > 0 && step.Durability > Simulator.GetDurabilityCost(step, Skills.RapidSynthesis) && CU(craft, step, Skills.RapidSynthesis))
            return new(SafeCraftAction(craft, step, Skills.RapidSynthesis), "mid pre quality: steady hand");

        bool shouldUseSteadyHand = CU(craft, step, Skills.SteadyHand) && step.SteadyHandsUsed < cfg.MaxSteadyUses;
        // keep veneration running if we're forcing progress; otherwise don't because we're mixing quality and progress
        if ((cfg.MidFinishProgressBeforeQuality || shouldUseSteadyHand) && progressDeficit > 0 && step.VenerationLeft == 0 && step.WasteNotLeft == 0 && CU(craft, step, Skills.Veneration))
            return new(Skills.Veneration, "mid pre quality: progress finish vene");
        // similarly, re-up steady hand if we have any allowed uses left
        if (shouldUseSteadyHand && progressDeficit > 0 && step.SteadyHandLeft == 0 && step.WasteNotLeft == 0)
            return new(Skills.SteadyHand, "mid pre quality: progress finish steady");

        // check for progress-friendly conditions, or force progress if specified
        if (progressDeficit > 0 && SolveMidHighPriorityProgress(craft, step, allowIntensive, progressDeficit, cfg) is var highPrioProgress && highPrioProgress != Skills.None)
            return new(SafeCraftAction(craft, step, highPrioProgress), "mid pre quality: high-prio progress");

        // check for IQ-friendly conditions
        if (step.IQStacks < maxIQStacks && SolveMidHighPriorityIQ(cfg, craft, step, allowPrecise) is var highPrioIQ && highPrioIQ != Skills.None)
            return new(highPrioIQ, "mid pre quality: high-prio iq");
        if (step.Condition == Condition.Good && CU(craft, step, Skills.TricksOfTrade))
            return new(Skills.TricksOfTrade, "mid pre quality: high-prio tricks"); // progress/iq below decided not to use good, so spend it on tricks
        // TODO: observe on good omen?..
        if (step.Condition == Condition.GoodOmen && cfg.MidAllowVenerationGoodOmen && cfg.MidAllowIntensive != ExpertSolverSettings.MidAllowIntensiveSetting.MidNoIntensive && progressDeficit > Simulator.CalculateProgress(craft, step, Skills.IntensiveSynthesis) && step.WasteNotLeft == 0 && CU(craft, step, Skills.Veneration))
            return new(Skills.Veneration, "mid pre quality: good omen vene"); // next step would be intensive, vene is a good choice here

        // on recipes without pliant, we really want to set up waste not (WN) as long as it will be profitable
        var effectiveDura = step.Durability + (step.ManipulationLeft * 5);
        var maxWasteNotDuraConsumed = (10 * 4) / 2;  // if we don't proc any sturdy/etc.
        if (!craft.ConditionFlags.HasFlag(ConditionFlags.Pliant) && step.WasteNotLeft == 0 && step.IQStacks <= maxIQStacksForWasteNot && effectiveDura > maxWasteNotDuraConsumed && CU(craft, step, Skills.WasteNot))
            return new(Skills.WasteNot, "mid pre quality: no-pliant waste not");

        // see what else can we do
        if (step.IQStacks < maxIQStacks && !venerationActive)
        {
            // we want more iq:
            // - normal touches are 36cp/iq (27 on pliant/sturdy)
            // - hasty touch is ~30cp/iq (15 on sturdy, 21 on centered), but it's a gamble
            // - prep touch is 38cp/iq, so not worth it
            // - precise touch is 18cp/iq (much better on pliant/sturdy with h&s), but requires good/h&s
            // - prudent touch is 34cp/iq (22 on pliant)
            // this means sturdy hasty (unreliable) = precise > centered hasty (85%) = pliant prudent >> sturdy combo > hasty (unreliable) > prudent  > normal combo
            // note that most conditions are handled before calling this
            if (step.IQStacks >= cfg.MidMinIQForHSPrecise && step.IQStacks < (maxIQStacks - 1) && step.Durability > Simulator.GetDurabilityCost(step, Skills.PreciseTouch))
            {
                if (Simulator.CanUseAction(craft, step, Skills.PreciseTouch))
                    return new(Skills.PreciseTouch, "mid pre quality: iq");
                else if (step.HeartAndSoulAvailable && CU(craft, step, Skills.HeartAndSoul))
                    return new(Skills.HeartAndSoul, "mid pre quality: iq");
            }

            // just use prudent
            if (step.Durability > Simulator.GetDurabilityCost(step, Skills.PrudentTouch) && Simulator.CanUseAction(craft, step, Skills.PrudentTouch))
                return new(Skills.PrudentTouch, "mid pre quality: iq");
        }
        else
        {
            // focus on progress
            if (cfg.MidAllowVenerationAfterIQ && step.VenerationLeft == 0 && progressDeficit > Simulator.CalculateProgress(craft, step, Skills.RapidSynthesis) && step.Durability + 5 * step.ManipulationLeft > 20 && CU(craft, step, Skills.Veneration))
                return new(Skills.Veneration, "mid pre quality: progress"); // TODO: reconsider this heuristic
            if (progressDeficit <= Simulator.CalculateProgress(craft, step, Skills.PrudentSynthesis) && step.Durability > Simulator.GetDurabilityCost(step, Skills.PrudentSynthesis) && CU(craft, step, Skills.PrudentSynthesis))
                return new(SafeCraftAction(craft, step, Skills.PrudentSynthesis), "mid pre quality: progress"); // TODO: reconsider (minimal cost action when we need just a little more progress)
            if (step.Durability > Simulator.GetDurabilityCost(step, Skills.RapidSynthesis) && CU(craft, step, Skills.RapidSynthesis))
                return new(SafeCraftAction(craft, step, Skills.RapidSynthesis), "mid pre quality: progress");
        }
        // wait...
        return new(Skills.Observe, "mid pre quality: no options");
    }

    private static Recommendation SolveMidStartQuality(ExpertSolverSettings cfg, CraftState craft, StepState step, int progressDeficit, int availableCP, int reservedCP)
    {
        // no buffs up, this is a good chance to get some dura back if needed, and then get some iq/progress/quality, maybe start dedicated progress/quality phase
        // todo: it's unlikely to still have waste not going here on a no-pliant craft, but should it force a non-buff action?
        // first see whether we have some nice conditions to exploit for progress
        if (progressDeficit > 0 && SolveMidHighPriorityProgress(craft, step, true, progressDeficit, cfg) is var highPrioProgress && highPrioProgress != Skills.None)
            return new(SafeCraftAction(craft, step, highPrioProgress), "mid start quality: high-prio progress");
        if (step.Condition == Condition.Good && CU(craft, step, Skills.TricksOfTrade))
            return new(Skills.TricksOfTrade, "mid start quality: high-prio tricks");

        // on good omen, our choice is either observe/TP + tricks (+13/20cp) or gs+precise (300p for 50cp+10dura), meaning that using gs+precise is 4.76p/cp effectively
        // our baseline for 10dura is inno+precise (225p for 9+7+18cp = 6.61p/cp) or gs+inno+precise (375p for 32+9+7+18cp = 5.68p/cp)
        // so prefer observing on good omen
        if (cfg.MidObserveGoodOmenForTricks && step.Condition == Condition.GoodOmen)
        {
            if (step.TrainedPerfectionAvailable && CU(craft, step, Skills.TrainedPerfection))
                return new(Skills.TrainedPerfection, "mid start quality: good omen -> high-prio tricks");
            if (CU(craft, step, Skills.Observe))
                return new(Skills.Observe, "mid start quality: good omen -> high-prio tricks");
        }

        // ok, durability management time
        var duraAction = SolveMidDurabilityStartQuality(cfg, craft, step, availableCP);
        if (duraAction != Skills.None)
            return new(duraAction, "mid start quality: durability");

        // see what else can we do
        if (step.Condition == Condition.GoodOmen && cfg.MidAllowVenerationGoodOmen && progressDeficit > Simulator.CalculateProgress(craft, step, Skills.IntensiveSynthesis) && CU(craft, step, Skills.Veneration))
            return new(Skills.Veneration, "mid start quality: good omen vene"); // next step would be intensive, vene is a good choice here

        var freeCP = availableCP - Skills.ByregotsBlessing.StandardCPCost();
        var cpToSpendOnQuality = availableCP - reservedCP;

        // now that most conditions are checked, handle active TP if applicable and we have enough CP
        if (step.TrainedPerfectionActive && cfg.MidUseTP == MidUseTPSetting.MidUseTPDuringQuality)
        {
            // set up gs+inno if we can
            if (cpToSpendOnQuality >= (Simulator.GetCPCost(step, Skills.GreatStrides) + Skills.Innovation.StandardCPCost() + Skills.PreparatoryTouch.StandardCPCost()) && CU(craft, step, Skills.GreatStrides))
                return new(Skills.GreatStrides, "mid start quality: gs -> tp touch");

            // or just use it right now if we're really low on cp
            var emergencyTPSkill = step.Condition is Condition.Good ? Skills.PreciseTouch : Skills.PreparatoryTouch;
            if (cpToSpendOnQuality >= Simulator.GetCPCost(step, emergencyTPSkill) && CU(craft, step, emergencyTPSkill))
                return new(emergencyTPSkill, "mid start quality: emergency tp");
        }

        // turn on TP if applicable
        if (step.TrainedPerfectionAvailable && cfg.MidUseTP == MidUseTPSetting.MidUseTPDuringQuality && CU(craft, step, Skills.TrainedPerfection))
            return new(Skills.TrainedPerfection, "mid start quality: start tp");

        // we need around >20 effective durability to start a new combo
        var effectiveDura = step.Durability + step.ManipulationLeft * 5;
        // TODO: reconsider this condition and the whole block of code below, it's a bit meh, and probably should be a part of dura management function
        var mmPlusNoDuraComboCost = Skills.MastersMend.StandardCPCost() + Skills.Innovation.StandardCPCost() + (Skills.TrainedFinesse.StandardCPCost() * 4);
        if (effectiveDura <= 10 && cpToSpendOnQuality < mmPlusNoDuraComboCost && CU(craft, step, Skills.ByregotsBlessing))
        {
            // we're very low on durability - not enough to even byregot - and not enough cp to regain it normally
            // try some emergency actions
            if (step.Condition != Condition.Pliant && freeCP >= (Skills.MastersMend.StandardCPCost() / 2) + Skills.Observe.StandardCPCost() && CU(craft, step, Skills.Observe))
                return new(Skills.Observe, "mid start quality: critical dura"); // we don't have enough for mm, but might get lucky if we try baiting it with observe...
            // we don't even have enough cp for mm - oh well, get some buff up, otherwise pray for sturdy/good
            if (Simulator.GetDurabilityCost(step, Skills.ByregotsBlessing) < step.Durability) // sturdy, so byregot asap - we won't get a better chance to salvage the situation
                return new(Skills.ByregotsBlessing, "mid start quality: critical dura & sturdy");
            if (freeCP >= Simulator.GetCPCost(step, Skills.GreatStrides) && CU(craft, step, Skills.GreatStrides))
                return new(Skills.GreatStrides, "mid start quality: critical dura");
            if (freeCP >= Simulator.GetCPCost(step, Skills.Innovation) && CU(craft, step, Skills.Innovation))
                return new(Skills.Innovation, "mid start quality: critical dura");
            // nope, too little cp for anything... try observes
            if (freeCP >= Simulator.GetCPCost(step, Skills.Observe) && CU(craft, step, Skills.Observe))
                return new(Skills.Observe, "mid start quality: critical dura & emergency cp");
            if (step.CarefulObservationLeft > 0 && CU(craft, step, Skills.CarefulObservation))
                return new(Skills.CarefulObservation, "mid start quality: critical dura & emergency cp");
            // i give up :)
            return new(Skills.ByregotsBlessing, "mid start quality: critical dura & emergency cp"); // let the caller handle lack of cp
        }

        // check for an emergency situation where we don't have enough CP for inno+gs but have quick inno
        if (cfg.FinisherUseQuickInno && freeCP < Simulator.GetCPCost(step, Skills.Innovation) + Skills.GreatStrides.StandardCPCost() && CU(craft, step, Skills.GreatStrides))
            return new(Skills.GreatStrides, "mid start emergency gs");

        // main choice here is whether to use gs before inno
        // - if we use gs+inno, we'll have 2 steps to use touch - enough for a full half-combo, and an opportunity to react to pliant
        // - gs is 32cp; using it on advanced/precise is extra 150p = 4.69p/cp, which is equal to extra finesse (but with opportunity to react to conditions)
        // - spending (normal) gs on 100p touch is worse than using finesse under inno, so don't bother if we don't have enough dura
        // - gs is a good way to spend pliant if we don't need dura and don't have inno up, even if we're going to use 100p touches
        // as a conclusion, we use gs if we have enough dura or we have pliant
        // TODO: is it a good idea to use gs on primed? it's only marginally useful (if we get pliant on next step), primed inno is a free ~9cp
        var halfComboCPCost = Skills.Innovation.StandardCPCost() + Skills.Observe.StandardCPCost() + Skills.AdvancedTouch.StandardCPCost();
        if (cfg.MidGSBeforeInno && step.Condition != Condition.Primed && (step.Condition == Condition.Pliant || effectiveDura > 20) && freeCP >= Simulator.GetCPCost(step, Skills.GreatStrides) + halfComboCPCost && CU(craft, step, Skills.GreatStrides))
            return new(Skills.GreatStrides, "mid start quality");
        // just inno and react to what happens...
        return new(Skills.Innovation, "mid start quality");
    }

    private static Recommendation SolveMidQuality(ExpertSolverSettings cfg, CraftState craft, StepState step, int availableCP, int reservedCP)
    {
        // some rough estimations (potency numbers are pre-iq for simplicity, since it just effectively doubles the base quality rate at this point):
        // - typically after iq stacks we need ~2250p worth of quality
        // - byregot under inno+gs would give us 750p, plus extra 562.5p if good
        // - this means we would need around 1500p from normal actions
        // our options (two step 'half combos', so inno covers two; all except finesse and prep costs 10 dura):
        // - observe + advanced = 225p for 25cp (43 effective) = 9.00p/cp (5.23 eff) - baseline for effectiveness
        // - prudent + prudent = 300p for 50cp (68 effective) = 6.00p/cp (4.41 eff) - doesn't seem to be worth it?
        // - [gs before inno] + observe + advanced = 375p for 57cp (75 effective) = 6.58p/cp (5.00 eff) - good way to spend excessive cp and allows reacting to conditions
        // - finesse + finesse = 300p for 64cp (64 effective) = 4.69p/cp (4.69 eff) - does not cost dura, but very expensive cp wise
        // - prep = 300p for 40cp and 20 dura (76 effective) = 3.94p/cp eff - not worth it unless we have some conditions or just want to burn leftover dura
        // - gs + prep = 500p for 72cp and 20 dura (108 effective) = 4.62p/cp eff - not worth it unless we have good omen or just want to burn leftover dura
        // good condition:
        // - tricks (20cp) is worth ~100p, probably less because of inno (but it's a good option if no buffs are up)
        // - prep touch gives extra 225p (or 375p under gs+inno), which is the most efficient use of good (but expensive)
        // - after observe, advanced or precise are equivalent; the good is worth extra ~169p (or ~281p under gs)
        // - otherwise replacing observe with precise is decent (effectively finishes half-combo in 1 step)
        // centered condition
        // - TODO consider hasty?
        // sturdy condition
        // - ignored and wasted if we'd like to use inno/gs
        // - otherwise straight upgrade to any action
        // - prep is the best way to utilize sturdy
        // pliant condition
        // - best used on manip (48cp worth), if we have enough cp to utilize extra durability
        // - also reasonable to use on GS (16cp worth) or prep (20 cp worth), if GS/inno stack is already active
        // - can even be used on advanced (9cp worth) if it pops after observe
        // manip allows us to replace 4 finesse+finesse half-combos with observe+advanced:
        // - finesse+finesse = 300p for 64cp = 4.69p/cp
        // - 1/4 manip + observe+advanced = 225p for 25+24cp = 4.59p/cp; if manip is cast on pliant, that changes to 6.08p/cp
        // how to determine whether we have spare cp/dura for more quality or should just byregot?
        // - we need to always be at >10 durability, so that we can always do a byregot+synth
        // - freeCP accounts for inno+gs+byregot and last progress step, so anything >0 can be used for more quality
        // - we really want byregot to be under inno+gs, so using any quality action now will require 32cp for (re)applying gs + 18cp for (re)applying inno unless we have at least 3 steps left
        // - if we have more cp still, we have following options:
        // -- (inno) + finesse + byregot-combo - needs 32cp and no dura, gives 150p quality
        // -- (inno) + prudent + byregot-combo - needs 25cp and 5 dura, gives 150p quality
        // -- (inno) + half-combo + byregot-combo - needs 25-72cp (observe+advanced - gs+prep) and 10-20 dura, gives 225-500p quality; inno needs to be reapplied unless at 4 steps
        // -- gs + inno + half-combo - needs 57cp+ and 10+ dura, gives 375p quality; it's something to consider only if inno is not running now
        // -- extra cp/durability can be burned by doing multiple half-combos, but that's a decision to be made on later steps
        // -- if we have tons of cp but not enough durability, we might want to manip; this is reasonable if we have enough cp to do extra 4 half-combos (136 cp minimum + manip cost)
        var freeCP = availableCP - reservedCP;
        var effectiveDura = step.Durability + step.ManipulationLeft * 5;
        if (step.InnovationLeft == 0)
        {
            // gs without inno - we generally just want to inno asap, unless some conditions change the priorities
            if (step.Condition == Condition.Pliant)
            {
                // pliant after GS - this is a decent chance to recover some durability if we need it
                // if we're at last step of GS for whatever reason - assume it's ok to waste it (TODO)
                var duraAction = SolveMidDurabilityQualityPliant(cfg, craft, step, freeCP);
                if (duraAction != Skills.None)
                    return new(duraAction, "mid quality gs-only: durability");
            }

            // TODO: consider good tricks and saving gs - some quick simulation shows it to be a slight loss...
            if (step.Condition == Condition.Good)
            {
                // pop quick inno to take advantage of gs+good if settings allow
                if (step.QuickInnoAvailable && CU(craft, step, Skills.QuickInnovation) && (cfg.MidAllowQuickInnoGood == MidAllowQuickInnoGoodSetting.MidAllowQuickInnoGoodAny || (cfg.MidAllowQuickInnoGood == MidAllowQuickInnoGoodSetting.MidAllowQuickInnoGoodPrepTP && step.TrainedPerfectionActive && CU(craft, step, Skills.PreparatoryTouch))))
                    return new(Skills.QuickInnovation, "mid quality gs-only: quick inno -> good");

                // precise is more efficient than prep here, even if TP is active (~25 p/CP vs ~15 p/CP)
                if (CanUseActionSafelyInFinisher(step, Skills.PreciseTouch, freeCP) && CU(craft, step, Skills.PreciseTouch))
                    return new(Skills.PreciseTouch, "mid quality gs-only: utilize good");
            } 
            if (step.PrevComboAction == Skills.Observe && CanUseActionSafelyInFinisher(step, Skills.AdvancedTouch, freeCP) && CU(craft, step, Skills.AdvancedTouch))
                return new(Skills.AdvancedTouch, "mid quality gs-only: after observe?"); // this is weird, why would we do gs->observe?.. maybe we're low on cp?

            if (step.GreatStridesLeft == 1)
            {
                // we really want to use gs now on some kind of quality action, doing inno now would waste it
                // TP+prep is the best use in any non-good condition
                if (step.TrainedPerfectionActive && CanUseActionSafelyInFinisher(step, Skills.PreparatoryTouch, freeCP) && CU(craft, step, Skills.PreparatoryTouch))
                    return new(Skills.PreparatoryTouch, "mid quality gs-only last chance");
                // GS + centered hasty is better than full cost prep or prudent
                if (cfg.MidAllowCenteredHasty && step.Condition == Condition.Centered && CanUseActionSafelyInFinisher(step, Skills.HastyTouch, freeCP) && CU(craft, step, Skills.HastyTouch))
                    return new(Skills.HastyTouch, "mid quality gs-only last chance");
                // TF is the most efficient use of pliant, even more than prep or prudent
                if (step.Condition == Condition.Pliant && CanUseActionSafelyInFinisher(step, Skills.TrainedFinesse, freeCP) && CU(craft, step, Skills.TrainedFinesse))
                    return new(Skills.TrainedFinesse, "mid quality gs-only last chance");
                // otherwise, prudent and TF are still better than prep, even on sturdy
                if (CanUseActionSafelyInFinisher(step, Skills.PrudentTouch, freeCP) && CU(craft, step, Skills.PrudentTouch))
                    return new(Skills.PrudentTouch, "mid quality gs-only last chance");
                if (freeCP >= Simulator.GetCPCost(step, Skills.TrainedFinesse) && CU(craft, step, Skills.TrainedFinesse))
                    return new(Skills.TrainedFinesse, "mid quality gs-only last chance");
            }

            // inno up if it won't cost us the finisher
            if (availableCP - Simulator.GetCPCost(step, Skills.Innovation) >= Skills.ByregotsBlessing.StandardCPCost() + Skills.GreatStrides.StandardCPCost())
                return new(Skills.Innovation, "mid quality: gs->inno");
        }

        // inno (or gs+inno) are up or we're right at the end with quick inno - do some half-combos or emergency steps
        // our options:
        // - gs + byregot - if we're low on cp or will finish the craft with current quality
        // - manip/mm on pliant if needed
        // - prep / precise if good (or pliant?)
        // - prep on sturdy
        // - observe + advanced
        // - prudent
        // - finesse
        // - gs on pliant + some touch
        // - hasty on low cp to burn dura
        if (step.Condition == Condition.Good)
        {
            // good options are prep and precise (advanced after observe is the same as precise, so don't bother)
            // prep is significantly less quality than precise per CP, even with TP, but may be useful in some situations
            if (cfg.MidAllowGoodPrep && step.GreatStridesLeft > 0 && CanUseActionSafelyInFinisher(step, Skills.PreparatoryTouch, freeCP) && CU(craft, step, Skills.PreparatoryTouch))
                return new(Skills.PreparatoryTouch, "mid quality: gs+inno+good");
            if (CanUseActionSafelyInFinisher(step, Skills.PreciseTouch, freeCP) && CU(craft, step, Skills.PreciseTouch))
                return new(Skills.PreciseTouch, "mid quality: good");
            // otherwise ignore good condition and see what else can we do
            // note: using tricks here seems to be a slight loss according to craft, which is expected
        }

        if (step.Condition == Condition.Sturdy || (step.Condition == Condition.Robust && step.InnovationLeft == 1))
        {
            // what's the best thing to do on a single sturdy before the end of inno?
            // on robust we'd prefer to set up observe for the next step's sturdy
            // todo: sturdy hasty is technically the most CP-efficient option...

            // prep is less efficient than advanced touch, if ready, but better than prudent
            if (cfg.MidAllowSturdyPrep && step.PrevComboAction != Skills.Observe && CanUseActionSafelyInFinisher(step, Skills.PreparatoryTouch, freeCP) && CU(craft, step, Skills.PreparatoryTouch))
                return new(Skills.PreparatoryTouch, "mid quality: sturdy");
        }

        if (step.Condition == Condition.Pliant && step.GreatStridesLeft != 1)
        {
            // we won't waste gs if we do manip/mm now - see if we want it
            // we don't really care about wasting last step of inno, it's no different from wasting any other step
            var duraAction = SolveMidDurabilityQualityPliant(cfg, craft, step, freeCP);
            if (duraAction != Skills.None)
                return new(duraAction, "mid quality: durability");
            // otherwise ignore pliant and just save some cp on touch actions
        }

        if (step.Condition == Condition.GoodOmen)
        {
            // if we have TP up and inno/gs will both last, just wait
            if (step.TrainedPerfectionActive && step.GreatStridesLeft > 1 && step.InnovationLeft > 1 && freeCP >= Simulator.GetCPCost(step, Skills.Observe) + Skills.PreciseTouch.StandardCPCost() && CU(craft, step, Skills.Observe))
                return new(Skills.Observe, "mid quality: good omen tp");

            var canQuickInno = cfg.MidAllowQuickInnoGood is MidAllowQuickInnoGoodSetting.MidAllowQuickInnoGoodAny or MidAllowQuickInnoGoodSetting.MidAllowQuickInnoGoodPrepTP && step.QuickInnoLeft > 0;
            if (step.GreatStridesLeft == 0 && (step.InnovationLeft > 1 || canQuickInno))
            {
                // get gs up for gs+inno+good (prep/precise)
                // gs is 32p for at least 225/262.5p (depending on splendorous)
                var nextStepDura = step.Durability + (step.ManipulationLeft > 0 ? 5 : 0);
                if (nextStepDura > 10 && effectiveDura > 20 && freeCP >= Skills.GreatStrides.StandardCPCost() + Skills.PreciseTouch.StandardCPCost() && CU(craft, step, Skills.GreatStrides))
                    return new(Skills.GreatStrides, "mid quality: good omen gs");
            }
        }

        // 0-dura prep touch under buffs? don't mind if I do
        if (step.TrainedPerfectionActive && CanUseActionSafelyInFinisher(step, Skills.PreparatoryTouch, freeCP) && CU(craft, step, Skills.PreparatoryTouch))
            return new(Skills.PreparatoryTouch, "mid quality: prep w/tp");

        if (step.PrevComboAction == Skills.Observe && CanUseActionSafelyInFinisher(step, Skills.AdvancedTouch, freeCP) && CU(craft, step, Skills.AdvancedTouch))
            return new(Skills.AdvancedTouch, "mid quality"); // complete advanced half-combo

        // try spending some durability for using some other half-combo action:
        // - observe + advanced if we have enough time on gs/inno is 150p for 25cp
        // - prudent is 100p for 25cp, so less efficient - but useful if we don't have enough time/durability for full half-combo
        // - pliant gs (+ prudent) is extra ~66p for 16cp, so it's an option i guess, especially considering we might get some better condition (TODO consider this)
        // - finesse is 100p for 32cp, which is even less efficient, but does not cost durability
        // - hasty is a fine way to spend excess durability if low on cp
        if (step.InnovationLeft > 1 && step.GreatStridesLeft > 1)
        {
            // observe, if we can do advanced on next step, and if we're not going to waste it due to good omen
            // note that on good omen we still prefer using observe rather than waste gs on 100p touch (TODO: consider using something else if gs is not up on good omen)
            var nextStepDura = step.Durability + (step.ManipulationLeft > 0 ? 5 : 0);
            if (nextStepDura > 10 && effectiveDura > 20 && freeCP >= Simulator.GetCPCost(step, Skills.Observe) + Skills.AdvancedTouch.StandardCPCost() && CU(craft, step, Skills.Observe))
                return new(Skills.Observe, "mid quality: advanced combo");
        }

        // some less efficient alternatives
        if (CanUseActionSafelyInFinisher(step, Skills.PrudentTouch, freeCP) && CU(craft, step, Skills.PrudentTouch))
            return new(Skills.PrudentTouch, "mid quality: alt");
        if (freeCP >= Simulator.GetCPCost(step, Skills.TrainedFinesse) && CU(craft, step, Skills.TrainedFinesse))
            return new(Skills.TrainedFinesse, "mid quality: alt");

        // we're low on cp, see if we can regain some cp via tricks
        var emergencyAction = EmergencyRestoreCP(cfg, craft, step);
        if (emergencyAction != Skills.None)
            return new(emergencyAction, "mid quality: emergency cp");
        if (CanUseActionSafelyInFinisher(step, Skills.DaringTouch, freeCP) && CU(craft, step, Skills.DaringTouch))
            return new(Skills.DaringTouch, "mid quality: emergency daring");
        if (CanUseActionSafelyInFinisher(step, Skills.HastyTouch, freeCP) && CU(craft, step, Skills.HastyTouch))
            return new(Skills.HastyTouch, "mid quality: emergency hasty"); // better than nothing i guess...

        // ok, we're out of options - use gs + byregot
        if (step.GreatStridesLeft == 0 && availableCP >= Simulator.GetCPCost(step, Skills.GreatStrides) + Skills.ByregotsBlessing.StandardCPCost() && CU(craft, step, Skills.GreatStrides))
            return new(Skills.GreatStrides, "mid quality: emergency gs+byregot");
        if (step.Condition is not Condition.Good and not Condition.Excellent && step.Durability > 10)
        {
            // try baiting good
            var canQuickInno = cfg.FinisherUseQuickInno && step.QuickInnoLeft > 0;
            if (step.GreatStridesLeft != 1 && (step.InnovationLeft > 1 || canQuickInno) && availableCP >= Simulator.GetCPCost(step, Skills.Observe) + Skills.ByregotsBlessing.StandardCPCost() && CU(craft, step, Skills.Observe))
                return new(Skills.Observe, "mid quality: emergency byregot bait good");
            if (cfg.FinisherBaitGoodByregot && step.CarefulObservationLeft > 0 && CU(craft, step, Skills.CarefulObservation))
                return new(Skills.CarefulObservation, "mid quality: emergency byregot bait good");
        }

        // well, gg. might as well use quick inno to get as much quality as we can (helps on stellar missions, for example)
        if (cfg.FinisherUseQuickInno && step.QuickInnoAvailable)
            return new(Skills.QuickInnovation, "mid quality: emergency quick inno->byregot");

        return new(Skills.ByregotsBlessing, "mid quality: emergency byregot");
    }

    private static Skills SolveMidDurabilityPreQuality(ExpertSolverSettings cfg, CraftState craft, StepState step, int availableCP, bool allowObserveOnLowDura, bool wantProgress)
    {
        // during the mid phase, durability is a serious concern
        if (step.ManipulationLeft > 0 && step.Durability + 5 > craft.CraftDurability)
            return Skills.None; // we're maxed out on dura, doing anything here will waste manip durability

        var canBePliant = craft.ConditionFlags.HasFlag(ConditionFlags.Pliant);

        if (step.Condition == Condition.Primed)
        {
            // primed manipulation is a reasonable action (see math in other comments)
            if (cfg.MidPrimedManipPreQuality && step.ManipulationLeft == 0 && step.WasteNotLeft == 0 && availableCP >= Simulator.GetCPCost(step, Skills.Manipulation) && CU(craft, step, Skills.Manipulation))
                return Skills.Manipulation;

            // if we're never gonna see pliant and we need enough IQ to not waste it, six steps of WN1 is worth it
            // WN steps are also profitably used on progress, but that's harder to calculate. low IQ stacks are close enough
            if (!canBePliant && step.WasteNotLeft == 0 && step.IQStacks <= maxIQStacksForWasteNot && CU(craft, step, Skills.WasteNot))
                return Skills.WasteNot;
        }

        if (step.Condition == Condition.Pliant)
        {
            // see if we can utilize pliant for repairs
            if (step.Durability + immaculateDuraMinimum + (step.ManipulationLeft > 0 ? 5 : 0) <= craft.CraftDurability && CU(craft, step, Skills.ImmaculateMend))
                return Skills.ImmaculateMend;
            if (step.ManipulationLeft <= 1 && availableCP >= Simulator.GetCPCost(step, Skills.Manipulation) && CU(craft, step, Skills.Manipulation))
                return Skills.Manipulation;
            if (step.Durability + mmDuraRestored + (step.ManipulationLeft > 0 ? 5 : 0) <= craft.CraftDurability && availableCP >= Simulator.GetCPCost(step, Skills.MastersMend) && CU(craft, step, Skills.MastersMend))
                return Skills.MastersMend;
            return Skills.None;
        }

        var criticalDurabilityThreshold = (step.Condition is Condition.Sturdy or Condition.Robust) ? 5 : 10;
        var wantObserveOnLowDura = allowObserveOnLowDura && canBePliant && step.Condition switch
        {
            Condition.Normal or Condition.Good or Condition.GoodOmen or Condition.Primed => true, // these are all 'observable'
            Condition.Malleable => !wantProgress, // this is useless if we don't need more progress
            _ => false
        };
        var lowDurabilityThreshold = wantObserveOnLowDura ? step.ManipulationLeft > 0 ? 20 : 25 : criticalDurabilityThreshold;
        if (step.Durability <= lowDurabilityThreshold)
        {
            // we really need to do something about durability, we don't even have useful actions to perform
            if (step.Condition == Condition.Good && CU(craft, step, Skills.TricksOfTrade))
                return Skills.TricksOfTrade;

            if (step.ManipulationLeft > 0) 
            {
                // just regen a bit...
                if (step.TrainedPerfectionAvailable && cfg.MidUseTP != MidUseTPSetting.MidUseTPDuringQuality && CU(craft, step, Skills.TrainedPerfection))
                    return Skills.TrainedPerfection;
                if (CU(craft, step, Skills.Observe))
                    return Skills.Observe; 
            }
            if (cfg.MidBaitPliantWithObservePreQuality && canBePliant)
            {
                // try baiting pliant - this will save us 48cp at the cost of ~7+24cp
                // TODO: consider careful observation to bait pliant - this sounds much worse than using them to try baiting good byregot
                if (step.TrainedPerfectionAvailable && cfg.MidUseTP != MidUseTPSetting.MidUseTPDuringQuality && CU(craft, step, Skills.TrainedPerfection) && step.RemainingCP > (Skills.Manipulation.StandardCPCost() / 2))
                    return Skills.TrainedPerfection;
                if (CU(craft, step, Skills.Observe) && step.RemainingCP > Skills.Observe.StandardCPCost() + (Skills.Manipulation.StandardCPCost() / 2))
                    return Skills.Observe;
            }
            if (step.Durability <= criticalDurabilityThreshold && CU(craft, step, Skills.Manipulation))
                return Skills.Manipulation; // bait the bullet and manip on normal
        }

        // we still have some durability left, do nothing...
        return Skills.None;
    }

    private static Skills SolveMidDurabilityStartQuality(ExpertSolverSettings cfg, CraftState craft, StepState step, int availableCP)
    {
        // when we start doing quality, we do a lot of observes/buffs, so effective dura matters more than actual
        var effectiveDura = step.Durability + step.ManipulationLeft * 5;
        if (effectiveDura > craft.CraftDurability)
            return Skills.None; // we're high on dura, doing anything here will waste manip durability

        if (step.Condition == Condition.Pliant)
        {
            return SolveMidDurabilityQualityPliant(cfg, craft, step, availableCP);
        }

        if (cfg.MidPrimedManipAfterIQ && step.Condition == Condition.Primed && step.ManipulationLeft == 0 && availableCP >= Simulator.GetCPCost(step, Skills.Manipulation) + EstimateCPToUtilizeDurabilityForQuality(effectiveDura, 5) && CU(craft, step, Skills.Manipulation))
        {
            return Skills.Manipulation;
        }

        if (effectiveDura <= 10)
        {
            // we're very low on durability - not enough to even byregot
            // try to recover some, even if we can't utilize it well later - at worst we can do some hasty's
            // we really don't want to waste cp on non-pliant manip/mm, so try exploring some alternatives:
            // - observe and wait for pliant, then do normal half-combos (~31cp to save ~48cp)
            // - inno + finesse - quite expensive cp-wise (600p for 146=18+4*32cp = 4.11p/cp), but slightly more effective than using full-cost manip + observe+advanced (450p for 116=96/2+18+2*25cp = 3.88p/cp)
            var mastersMendFinisherCP = Skills.MastersMend.StandardCPCost() + Skills.Innovation.StandardCPCost() + Skills.GreatStrides.StandardCPCost() + Skills.ByregotsBlessing.StandardCPCost();
            var freeCP = availableCP - mastersMendFinisherCP;

            // try baiting pliant; TP is also a valid alternate observe here if it's up
            if (cfg.MidBaitPliantWithObserveAfterIQ && craft.ConditionFlags.HasFlag(ConditionFlags.Pliant))
            {
                if (step.TrainedPerfectionAvailable && CU(craft, step, Skills.TrainedPerfection))
                    return Skills.TrainedPerfection;
                // using observe for this will save us 48cp at the cost of ~7+24cp.
                if (freeCP >= Skills.Observe.StandardCPCost() && CU(craft, step, Skills.Observe))
                    return Skills.Observe;
            }

            var zeroDuraComboCP = Skills.Innovation.StandardCPCost() + (Skills.TrainedFinesse.StandardCPCost() * 4);
            if (freeCP >= zeroDuraComboCP) // inno + 4xfinesse
                return Skills.None;
            // just do a normal manip/mm
            if (step.ManipulationLeft <= 1 && availableCP >= Simulator.GetCPCost(step, Skills.Manipulation) + Skills.ByregotsBlessing.StandardCPCost() && CU(craft, step, Skills.Manipulation))
                return Skills.Manipulation;
            if (availableCP >= Simulator.GetCPCost(step, Skills.MastersMend) + Skills.ByregotsBlessing.StandardCPCost() && CU(craft, step, Skills.MastersMend))
                return Skills.MastersMend;
        }

        // TODO: consider doing something (baiting?) if effective durability is <= 20 (enough for one half-combo) or 30 (enough for two half-combos)
        return Skills.None;
    }

    private static Skills SolveMidDurabilityQualityPliant(ExpertSolverSettings cfg, CraftState craft, StepState step, int availableCP)
    {
        var effectiveDura = step.Durability + step.ManipulationLeft * 5; // since we are going to use a lot of non-dura actions (buffs/observes), this is what really matters
        if (effectiveDura + immaculateDuraMinimum <= craft.CraftDurability && availableCP >= Simulator.GetCPCost(step, Skills.ImmaculateMend) + EstimateCPToUtilizeDurabilityForQuality(effectiveDura, 3) && CU(craft, step, Skills.ImmaculateMend))
            return Skills.ImmaculateMend;
        if (step.ManipulationLeft <= 1 && availableCP >= Simulator.GetCPCost(step, Skills.Manipulation) + EstimateCPToUtilizeDurabilityForQuality(effectiveDura, 4) && CU(craft, step, Skills.Manipulation))
            return Skills.Manipulation;
        if (effectiveDura + mmDuraRestored <= craft.CraftDurability && availableCP >= Simulator.GetCPCost(step, Skills.MastersMend) + EstimateCPToUtilizeDurabilityForQuality(effectiveDura, 3) && CU(craft, step, Skills.MastersMend))
            return Skills.MastersMend;
        return Skills.None;
    }

    private static int EstimateCPToUtilizeDurabilityForQuality(int effectiveDura, int extraHalfCombos)
    {
        var estHalfComboCost = (Skills.Innovation.StandardCPCost() / 2) + Skills.Observe.StandardCPCost() + Skills.AdvancedTouch.StandardCPCost(); // rough baseline - every 10 extra dura is one half-combo, which requires 34cp (1/2 inno + observe+advanced) - TODO: should it also include 1/2 GS?
        var estNumHalfCombosWithCurrentDura = effectiveDura <= 20 ? 0 : (effectiveDura + 9) / 10; // 11-20 dura is 0 half-combos, 21-30 is 1, ...
        var estCPNeededToUtilizeCurrentDura = estHalfComboCost * estNumHalfCombosWithCurrentDura;
        return effectiveDura <= 10 ? 0 : estCPNeededToUtilizeCurrentDura + extraHalfCombos * estHalfComboCost;
    }

    private static Skills SolveMidHighPriorityProgress(CraftState craft, StepState step, bool allowIntensive, int progressDeficit, ExpertSolverSettings cfg)
    {
        // high-priority progress actions (exploit conditions)
        // intensive is worth spending TP on if enabled by current settings
        if (step.Condition == Condition.Good && allowIntensive && (!step.TrainedPerfectionActive || cfg.MidUseTP is MidUseTPSetting.MidUseTPGroundwork or MidUseTPSetting.MidUseTPEitherPreQuality) && step.Durability > Simulator.GetDurabilityCost(step, Skills.IntensiveSynthesis) && CU(craft, step, Skills.IntensiveSynthesis))
            return Skills.IntensiveSynthesis;

        if (step.TrainedPerfectionActive)
        {
            // skip the rest of the progress logic entirely if saving TP for the quality phase
            if (cfg.MidUseTP is MidUseTPSetting.MidUseTPPrepIQ or MidUseTPSetting.MidUseTPDuringQuality)
                return Skills.None;

            // *don't* spend TP on Good outside of intensive, it should prioritize prep or tricks; even if forcing progress we'd rather get the free CP
            if (step.Condition == Condition.Good && cfg.MidUseTP != MidUseTPSetting.MidUseTPEitherPreQuality)
                return (cfg.MidFinishProgressBeforeQuality && CU(craft, step, Skills.TricksOfTrade)) ? Skills.TricksOfTrade : Skills.None;

            // do spend TP on a 0-dura groundwork for malleable, or if it will finish the deficit regardless
            var isGWEnough = Simulator.CalculateProgress(craft, step, Skills.Groundwork) >= progressDeficit;
            if ((step.Condition == Condition.Malleable || isGWEnough) && CU(craft, step, Skills.Groundwork))
                return Skills.Groundwork;

            // check for a (gasp!) non-progress action here if appropriate (<10 IQ)
            if (cfg.MidUseTP == MidUseTPSetting.MidUseTPEitherPreQuality && step.Condition is Condition.Good or Condition.Pliant)
                return Skills.PreparatoryTouch;

            // bait conditions per the setting; we've already accounted for conditions we don't want to miss
            if (step.ObserveCounter < cfg.MidMaxBaitStepsForTP && (craft.ConditionFlags.HasFlag(ConditionFlags.Malleable) || step.MaterialMiracleActive || cfg.MidUseTP == MidUseTPSetting.MidUseTPEitherPreQuality) && CU(craft, step, Skills.Observe))
                return Skills.Observe;

            if (CU(craft, step, Skills.Groundwork))
                return Skills.Groundwork;
        }

        // see if a safe crafting action can get us within striking distance before resorting to rapid
        if (Simulator.CalculateProgress(craft, step, Skills.PrudentSynthesis) >= progressDeficit)
        {
            // normally careful is slightly better progress per cp/dura, but we know this will be the penultimate progress action, so prudent to save dura
            if (step.Durability > Simulator.GetDurabilityCost(step, Skills.PrudentSynthesis) && CU(craft, step, Skills.PrudentSynthesis))
                return Skills.PrudentSynthesis;
            if (step.Durability > Simulator.GetDurabilityCost(step, Skills.CarefulSynthesis) && CU(craft, step, Skills.CarefulSynthesis)) // if not enough CP for prudent
                return Skills.CarefulSynthesis;
            if (Simulator.CalculateProgress(craft, step, Skills.BasicSynthesis) >= progressDeficit && step.Durability > Simulator.GetDurabilityCost(step, Skills.BasicSynthesis)) // if things are really dire but we're close to done
                return Skills.BasicSynthesis;
        }

        // if we're forcing progress, the logic differs quite a bit
        if (cfg.MidFinishProgressBeforeQuality)
        {
            // similarly, if a non-malleable groundwork will get us there, set up TP for it instead of risking rapid...
            var willGWBeEnough = (Simulator.BaseProgress(craft) * 360 / 100) >= progressDeficit;
            if (cfg.MidUseTP is MidUseTPSetting.MidUseTPGroundwork or MidUseTPSetting.MidUseTPEitherPreQuality && step.TrainedPerfectionAvailable && willGWBeEnough && step.WasteNotLeft == 0 && CU(craft, step, Skills.TrainedPerfection))
                return Skills.TrainedPerfection;

            // ...and while we favor rapid if it has a favorable condition...
            if (step.Condition is Condition.Centered or Condition.Sturdy or Condition.Robust or Condition.Malleable && step.Durability > Simulator.GetDurabilityCost(step, Skills.RapidSynthesis) && CU(craft, step, Skills.RapidSynthesis))
                return Skills.RapidSynthesis;

            // ...we should still set up TP->groundwork before rapid on any other condition
            if (cfg.MidUseTP is MidUseTPSetting.MidUseTPGroundwork or MidUseTPSetting.MidUseTPEitherPreQuality && step.TrainedPerfectionAvailable && step.WasteNotLeft == 0 && CU(craft, step, Skills.TrainedPerfection))
                return Skills.TrainedPerfection;

            // now it's time to risk it for the progress biscuit, even on bad conditions
            if (step.Durability > Simulator.GetDurabilityCost(step, Skills.RapidSynthesis) && CU(craft, step, Skills.RapidSynthesis))
                return Skills.RapidSynthesis;
        }

        // use rapid if there's a nice condition for it
        if (step.Condition is Condition.Centered or Condition.Sturdy or Condition.Robust or Condition.Malleable && step.Durability > Simulator.GetDurabilityCost(step, Skills.RapidSynthesis) && CU(craft, step, Skills.RapidSynthesis))
            return Skills.RapidSynthesis;

        if (step.TrainedPerfectionAvailable && cfg.MidUseTP is MidUseTPSetting.MidUseTPGroundwork or MidUseTPSetting.MidUseTPEitherPreQuality && step.WasteNotLeft == 0)
        {
            // don't burn a Good on setting up TP; if we didn't already use it on intensive, let the IQ solver figure it out
            if (step.Condition == Condition.Good)
                return Skills.None;

            // set up TP->groundwork
            if (step.TrainedPerfectionAvailable && CU(craft, step, Skills.TrainedPerfection))
                return Skills.TrainedPerfection;
        }

        return Skills.None;
    }

    private static Skills SolveMidHighPriorityIQ(ExpertSolverSettings cfg, CraftState craft, StepState step, bool allowPrecise)
    {
        // high-priority IQ-building actions (exploit conditions)
        var hastyAction = CU(craft, step, Skills.DaringTouch) ? Skills.DaringTouch : Skills.HastyTouch;

        // precise is worth spending TP on if enabled by current settings
        if (step.Condition is Condition.Good or Condition.Excellent && allowPrecise && step.Durability > Simulator.GetDurabilityCost(step, Skills.PreciseTouch) && CU(craft, step, Skills.PreciseTouch))
            return Skills.PreciseTouch;

        if (step.TrainedPerfectionActive)
        {
            if (step.Condition is Condition.Good or Condition.Pliant && CU(craft, step, Skills.PreparatoryTouch))
                return Skills.PreparatoryTouch;

            // bait good or pliant per the setting
            if (step.ObserveCounter < cfg.MidMaxBaitStepsForTP && CU(craft, step, Skills.Observe))
                return Skills.Observe;

            // TODO: check for or force inno/gs? seems not worth it at low iq stacks
            if (CU(craft, step, Skills.PreparatoryTouch))
                return Skills.PreparatoryTouch;
        }

        // waste not might have set up the refined touch combo
        if (step.PrevComboAction == Skills.BasicTouch && step.WasteNotLeft > 0 && step.Durability > Simulator.GetDurabilityCost(step, Skills.RefinedTouch) && CU(craft, step, Skills.RefinedTouch))
            return Skills.RefinedTouch;

        // use hasty/daring on favorable conditions, but only if the appropriate settings are enabled
        if (step.Condition == Condition.Centered && cfg.MidAllowCenteredHasty && step.Durability > Simulator.GetDurabilityCost(step, hastyAction) && CU(craft, step, hastyAction))
            return hastyAction;

        if (step.Condition is Condition.Sturdy or Condition.Robust)
        {
            // if we have pre-max-iq inno for any reason, sturdy prep is great value
            if (step.InnovationLeft > 0 && step.Durability > Simulator.GetDurabilityCost(step, Skills.PreparatoryTouch) && CU(craft, step, Skills.PreparatoryTouch))
                return Skills.PreparatoryTouch;

            // hasty is the best (non-inno) value during sturdy, but it might be turned off by settings
            if (cfg.MidAllowSturdyHasty && step.Durability > Simulator.GetDurabilityCost(step, hastyAction) && CU(craft, step, hastyAction))
                return hastyAction;

            // force h&s->precise on sturdy/robust if enabled
            if (cfg.MidAllowSturdyPreсise && (step.HeartAndSoulActive || step.HeartAndSoulAvailable) && step.Durability > Simulator.GetDurabilityCost(step, Skills.PreciseTouch))
                return step.HeartAndSoulActive && CU(craft, step, Skills.PreciseTouch) ? Skills.PreciseTouch : Skills.HeartAndSoul;

            // otherwise, start or continue a touch combo - worst case it's a discounted prudent
            var touchComboAction = Simulator.NextTouchCombo(step, craft);
            if (step.WasteNotLeft == 0 && step.Durability > Simulator.GetDurabilityCost(step, touchComboAction) && CU(craft, step, touchComboAction))
                return touchComboAction;
        }

        if (step.WasteNotLeft > 0)
        {
            // iq stacks math under WN, assuming 10 dura is worth 24 cp (no pliant manip) or less if WN is saving >5 dura per action
            //  - prudent: 25(+8) @ 1 iq, 100p                       = 32.2 cp/iq, 3.1 p/cp (baseline)
            //  - full touch combo: 18+32+18(+12+12+12) @ 3 iq, 375p = 34.6 cp/iq, 3.6 p/cp (very inflexible)
            //  - refined combo: 18+24(+12+12) @ 3 iq, 200p          = 22.0 cp/iq, 3.0 p/cp (slightly inflexible)
            //  - prep: 40(+~20) @ 2 iq, 200p                        = 30.0 cp/iq, 3.3 p/cp (most flexible for 2 stacks)
            //  - precise: 18(+12) @ 2 iq, 150p                      = 15.0 cp/iq, 5.0 p/cp (requires Good or H+S)
            //  - hasty 60%: 0(+12)/0.6 @ 0.6 iq, ~60p               = 33.3 cp/iq, 3.0 p/cp (not worth)
            //  - hasty 85%: 0(+12)/0.85 @ 0.85 iq, ~85p             = 15.7 cp/iq, 6.4 p/cp (very worth)
            // tl;dr: under WN, centered/sturdy hasty > refined combo (w/2+ WNs left) > prep (w/1 WN left) 
            //        precise and hasty are already addressed, so we can just figure out the rest of it

            // we need at least two steps of WN and a deficit of 3+ IQ for the combo to be worth it
            // the actual use of refined is checked earlier
            if (step.WasteNotLeft > 1 && step.IQStacks <= (maxIQStacks - 3) && step.Durability > Simulator.GetDurabilityCost(step, Skills.BasicTouch) && CU(craft, step, Skills.BasicTouch))
                return Skills.BasicTouch;

            // a half-dura prep also sounds nice if we need at least 2 IQ stacks
            if (step.IQStacks <= (maxIQStacks - 2) && step.Durability > Simulator.GetDurabilityCost(step, Skills.PreparatoryTouch) && CU(craft, step, Skills.PreparatoryTouch))
                return Skills.PreparatoryTouch;

            // if we're at 1 WN step left or 9 stacks of IQ, just let the rest of the solver figure it out
        }

        if (step.TrainedPerfectionAvailable && cfg.MidUseTP is MidUseTPSetting.MidUseTPPrepIQ or MidUseTPSetting.MidUseTPEitherPreQuality && step.WasteNotLeft == 0)
        {
            // don't burn a Good on setting up TP, it's leaving free CP on the table
            if (step.Condition == Condition.Good)
                return Skills.None;

            // set up TP->prep
            if (step.TrainedPerfectionAvailable && CU(craft, step, Skills.TrainedPerfection))
                return Skills.TrainedPerfection;
        }

        return Skills.None;
    }

    // see if we can do gs+inno+byregot right now to get to the quality goal
    private static Recommendation SolveFinishQuality(ExpertSolverSettings cfg, CraftState craft, StepState step, int availableCP, int qualityTarget)
    {
        if (step.IQStacks == 0)
            return new(Skills.None, "fq: no iq"); // we can't even byregot now...

        var missingQuality = qualityTarget - step.Quality;
        if (missingQuality <= 0)
            return new(Skills.None, "fq: at cap"); // we're already at cap

        var byregotDura = Simulator.GetDurabilityCost(step, Skills.ByregotsBlessing);
        var byregotCP = Simulator.GetCPCost(step, Skills.ByregotsBlessing);
        if (step.Durability <= byregotDura || availableCP < byregotCP)
            return new(Skills.None, "fq: no cp/dura"); // can't use

        var byregotQuality = Simulator.CalculateQuality(craft, step, Skills.ByregotsBlessing);
        if (missingQuality <= byregotQuality && CU(craft, step, Skills.ByregotsBlessing))
            return new(Skills.ByregotsBlessing, "fq: immediate"); // byregot now to complete the craft

        if (step.GreatStridesLeft > 1 && step.InnovationLeft == 0 && availableCP >= Simulator.GetCPCost(step, Skills.Innovation) + Skills.ByregotsBlessing.StandardCPCost())
        {
            // try [gs]+inno+byregot
            var estByregotQuality = CalculateByregotQuality(craft, step, true, 1);
            if (missingQuality <= estByregotQuality && CU(craft, step, Skills.Innovation))
                return new(Skills.Innovation, "fq: inno->byregot");
        }
        else if (step.GreatStridesLeft >= 1 && step.InnovationLeft == 0 && cfg.FinisherUseQuickInno && step.QuickInnoLeft > 0 && availableCP >= Simulator.GetCPCost(step, Skills.ByregotsBlessing))
        {
            // [gs]+quick inno+byregot
            var estByregotQuality = CalculateByregotQuality(craft, step, true, 0);
            if (missingQuality <= estByregotQuality && CU(craft, step, Skills.QuickInnovation))
                return new(Skills.QuickInnovation, "fq: quick inno->byregot");
        }
        else if (step.GreatStridesLeft == 0 && availableCP >= Simulator.GetCPCost(step, Skills.GreatStrides) + Skills.ByregotsBlessing.StandardCPCost())
        {
            // try gs+byregot
            var estByregotQuality = CalculateByregotQuality(craft, step, step.InnovationLeft > 1, 1);
            if (missingQuality <= estByregotQuality && CU(craft, step, Skills.GreatStrides))
                return new(Skills.GreatStrides, "fq: gs->byregot");

            if (step.InnovationLeft <= 1 && availableCP >= Simulator.GetCPCost(step, Skills.GreatStrides) + Skills.Innovation.StandardCPCost() + Skills.ByregotsBlessing.StandardCPCost())
            {
                // try gs+inno+byregot
                estByregotQuality = CalculateByregotQuality(craft, step, true, 2);
                if (missingQuality <= estByregotQuality && CU(craft, step, Skills.GreatStrides))
                    return new(Skills.GreatStrides, "fq: gs->inno->byregot");
            }

            if (step.InnovationLeft == 0 && cfg.FinisherUseQuickInno && step.QuickInnoLeft > 0 && availableCP >= Simulator.GetCPCost(step, Skills.GreatStrides) + Skills.ByregotsBlessing.StandardCPCost())
            {
                // try gs+quick inno+byregot
                estByregotQuality = CalculateByregotQuality(craft, step, true, 1);
                if (missingQuality <= estByregotQuality && CU(craft, step, Skills.GreatStrides))
                    return new(Skills.GreatStrides, "fq: gs->quick inno->byregot");
            }
        }

        return new(Skills.None, "fq: not enough"); // byregot is not enough
    }

    private static Skills SolveFinishProgress(CraftState craft, StepState step, int qualityTarget)
    {
        var remainingProgress = craft.CraftProgress - step.Progress;
        var remainingQuality = qualityTarget - step.Quality;

        // can we just finish it with a single action and not worry about all of this?
        if (remainingQuality <= 0)
        {
            if (Simulator.CalculateProgress(craft, step, Skills.BasicSynthesis) >= remainingProgress)
                return Skills.BasicSynthesis;
            if (Simulator.CalculateProgress(craft, step, Skills.CarefulSynthesis) >= remainingProgress && CU(craft, step, Skills.CarefulSynthesis))
                return Skills.CarefulSynthesis;
        }
        
        // intensive is always the best bet if it's safe to do so; otherwise, use the condition on CP because we might need it
        if (step.Condition is Condition.Good or Condition.Excellent)
        {
            if (CanUseSynthForFinisher(craft, step, Skills.IntensiveSynthesis) && CU(craft, step, Skills.IntensiveSynthesis))
                return Skills.IntensiveSynthesis;
            if (CU(craft, step, Skills.TricksOfTrade))
                return Skills.TricksOfTrade;
        }

        // sometimes we have a tiny sliver of quality left after byregot but still have enough CP for delicate, usually from tricks or pliant
        // todo: is it worth adding logic for the situation where we need slightly more than one delicate's worth of quality? does existing logic handle that?
        if (Simulator.CalculateProgress(craft, step, Skills.DelicateSynthesis) >= remainingProgress && CU(craft, step, Skills.DelicateSynthesis))
            return Skills.DelicateSynthesis;

        // can't finish in one action, so we have some options:
        // - rapid spam is efficient, but can fail - we use it if we can't do a guaranteed finish
        // - prudent can be thought of converting 11cp into 5 dura, which is less efficient than baiting pliant (which is random to an extent), but more efficient than normal manip
        // current algo:
        // - hope for pliant to restore dura
        // - otherwise as long as we have cp, we use most efficient actions; we observe if we're low on dura, trying to bait better conditions
        // - if we're out of cp, we spam rapid, and then finish with careful/basic
        // TODO: veneration outside pliant/good-omen
        // TODO: primed? probably quite pointless at this point...
        if (step.Condition == Condition.Pliant)
        {
            // rough guess as to how much CP we'd want left over to make a repair action worth it
            var extraFinishCP = Skills.CarefulSynthesis.StandardCPCost() * 4;
            if (step.ManipulationLeft <= 1 && step.RemainingCP >= Simulator.GetCPCost(step, Skills.Manipulation) + extraFinishCP && CU(craft, step, Skills.Manipulation))
                return Skills.Manipulation;
            if (step.Durability + immaculateDuraMinimum <= craft.CraftDurability && step.RemainingCP >= Simulator.GetCPCost(step, Skills.ImmaculateMend) + extraFinishCP && CU(craft, step, Skills.ImmaculateMend))
                return Skills.ImmaculateMend;
            if (step.Durability + mmDuraRestored + (step.ManipulationLeft > 0 ? 5 : 0) <= craft.CraftDurability && step.RemainingCP >= Simulator.GetCPCost(step, Skills.MastersMend) + extraFinishCP && CU(craft, step, Skills.MastersMend))
                return Skills.MastersMend;
            if (step.RemainingCP >= Simulator.GetCPCost(step, Skills.Veneration) && step.VenerationLeft <= 1 && CU(craft, step, Skills.Veneration))
                return Skills.Veneration; // good use of pliant
            if (CanUseSynthForFinisher(craft, step, Skills.PrudentSynthesis) && CU(craft, step, Skills.PrudentSynthesis))
                return Skills.PrudentSynthesis; // biggest cp cost synth
            // nothing good to use pliant for...
        }

        if (step.Condition == Condition.GoodOmen && step.RemainingCP >= Simulator.GetCPCost(step, Skills.Veneration) + Skills.IntensiveSynthesis.StandardCPCost() && step.VenerationLeft <= 1 && CU(craft, step, Skills.Veneration))
        {
            return Skills.Veneration; // we'll use intensive next...
        }

        // TODO: prioritize rapid during centered?..
        //if (step.Condition is Condition.Centered && step.Durability > Simulator.GetDurabilityCost(step, Skills.RapidSynthesis))
        //    return Skills.RapidSynthesis; // use centered condition

        // best possible use of malleable is hs+intensive - but only bother if careful won't suffice
        if (step.Condition == Condition.Malleable && CanUseSynthForFinisher(craft, step, Skills.IntensiveSynthesis) && (step.HeartAndSoulAvailable || step.HeartAndSoulActive) && step.Progress + Simulator.CalculateProgress(craft, step, step.RemainingCP >= Skills.CarefulSynthesis.StandardCPCost() ? Skills.CarefulSynthesis : Skills.BasicSynthesis) < craft.CraftProgress)
            return step.HeartAndSoulActive && CU(craft, step, Skills.IntensiveSynthesis) ? Skills.IntensiveSynthesis : Skills.HeartAndSoul;

        if (step.Condition is Condition.Normal or Condition.Pliant or Condition.Centered or Condition.Primed && step.ManipulationLeft > 0 && step.Durability <= 10 && step.RemainingCP >= Simulator.GetCPCost(step, Skills.Observe) + Skills.CarefulSynthesis.StandardCPCost() && CU(craft, step, Skills.Observe))
            return Skills.Observe; // regen a bit of dura and use focused

        if (CanUseSynthForFinisher(craft, step, Skills.CarefulSynthesis) && CU(craft, step, Skills.CarefulSynthesis))
            return Skills.CarefulSynthesis;

        if (CanUseSynthForFinisher(craft, step, Skills.PrudentSynthesis) && CU(craft, step, Skills.PrudentSynthesis))
            return Skills.PrudentSynthesis;

        // we're out of cp, use rapids if we have some dura left
        if (Simulator.GetDurabilityCost(step, Skills.RapidSynthesis) < step.Durability && CU(craft, step, Skills.RapidSynthesis))
            return Skills.RapidSynthesis;

        // and we're out of dura - finish craft with basic if it's ok, otherwise try rapid
        if (step.Progress + Simulator.CalculateProgress(craft, step, Skills.BasicSynthesis) >= craft.CraftProgress)
            return Skills.BasicSynthesis;

        // try to finish with hs+intensive
        if (CanUseSynthForFinisher(craft, step, Skills.IntensiveSynthesis))
        {
            if (CU(craft, step, Skills.IntensiveSynthesis))
                return Skills.IntensiveSynthesis;
            else if (CU(craft, step, Skills.HeartAndSoul))
                return Skills.HeartAndSoul;
        }

        // try to restore dura if we're out
        if (step.Durability <= 10)
        {
            if (step.Durability + immaculateDuraMinimum <= craft.CraftDurability && CU(craft, step, Skills.ImmaculateMend))
                return Skills.ImmaculateMend;
            if (step.ManipulationLeft <= 1 && CU(craft, step, Skills.Manipulation))
                return Skills.Manipulation;
            if (CU(craft, step, Skills.MastersMend))
                return Skills.MastersMend;
        }

        // just pray
        if (step.Durability > 10)
            return Skills.RapidSynthesis;
        if (CU(craft, step, Skills.Observe))
            return Skills.Observe;
        return P.Config.ExpertSolverConfig.RapidSynthYoloAllowed ? Skills.RapidSynthesis : Skills.None;
    }

    private static bool CanUseSynthForFinisher(CraftState craft, StepState step, Skills action)
        => step.RemainingCP >= Simulator.GetCPCost(step, action) && (step.Durability > Simulator.GetDurabilityCost(step, action) || step.Progress + Simulator.CalculateProgress(craft, step, action) >= craft.CraftProgress);

    private static bool CanUseActionSafelyInFinisher(StepState step, Skills action, int availableCP)
    {
        var duraCost = Simulator.GetDurabilityCost(step, action);
        return duraCost == 0 || (step.Durability > duraCost && step.Durability + 5 * step.ManipulationLeft - duraCost > 10 && availableCP >= Simulator.GetCPCost(step, action));
    }

    public static Skills SafeCraftAction(CraftState craft, StepState step, Skills action) => Simulator.WillFinishCraft(craft, step, action) ? Skills.FinalAppraisal : action;

    // try to use tricks, if needed use h&s
    public static Skills EmergencyRestoreCP(ExpertSolverSettings cfg, CraftState craft, StepState step)
    {
        if (Simulator.CanUseAction(craft, step, Skills.TricksOfTrade))
            return Skills.TricksOfTrade;
        if (step.HeartAndSoulAvailable && CU(craft, step, Skills.HeartAndSoul))
            return Skills.HeartAndSoul;
        if (cfg.EmergencyCPBaitGood && step.CarefulObservationLeft > 0 && CU(craft, step, Skills.CarefulObservation))
            return Skills.CarefulObservation; // try baiting good?..
        return Skills.None;
    }

    private static int CalculateByregotQuality(CraftState craft, StepState step, bool includeInno, int stepsAhead)
    {
        // stepsAhead is which step's condition we care about; 0 = current step
        var adjBuffMod = (1 + 0.1f * step.IQStacks) * (includeInno ? 2.5f : 2.0f);
        float effPotency = (100 + 20 * step.IQStacks) * adjBuffMod;
        bool useGood = stepsAhead == 0 ? step.Condition == Condition.Good : stepsAhead == 1 && step.Condition == Condition.GoodOmen;
        float condMod = useGood ? craft.SplendorCosmic ? 1.75f : 1.5f : 1;
        return (int)(Simulator.BaseQuality(craft) * condMod * effPotency / 100);
    }

    private static bool CU(CraftState craft, StepState step, Skills skill) => Simulator.CanUseAction(craft, step, skill);
}
