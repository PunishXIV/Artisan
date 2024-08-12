using ECommons.ExcelServices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.RawInformation.Character
{
    public enum Skills
    {
        None = 0,
        TouchCombo = 1,

        BasicSynthesis = 100001, // 120p progress, 10dur cost
        CarefulSynthesis = 100203, // 180p progress, 7cp + 10 dur cost
        RapidSynthesis = 100363, // 500p progress, 10 dur cost, 50% success
        Groundwork = 100403, // 360p progress, 18cp + 20 dur cost, half potency if durability left is less than required
        IntensiveSynthesis = 100315, // 400p progress, 6cp + 10 dur cost, requires good/excellent condition or heart&soul
        PrudentSynthesis = 100427, // 180p progress, 18cp + 5 dur cost, can't be used under waste-not
        MuscleMemory = 100379, // 300p progress, 6cp + 10 dur cost, requires first step, applies buff

        BasicTouch = 100002, // 100p quality, 18cp + 10 dur cost
        StandardTouch = 100004, // 125p quality, 18cp + 10 dur cost if used after basic touch (otherwise 32cp)
        AdvancedTouch = 100411, // 150p quality, 18cp + 10 dur cost if used after standard touch (otherwise 46cp)
        HastyTouch = 100355, // 100p quality, 10 dur cost, 60% success
        PreparatoryTouch = 100299, // 200p quality, 40cp + 20 dur cost, 1 extra iq stack
        PreciseTouch = 100128, // 150p quality, 18cp + 10 dur cost, 1 extra iq stack, requires good/excellent condition or heart&soul
        PrudentTouch = 100227, // 100p quality, 25cp + 5 dur cost, can't be used under waste-not
        TrainedFinesse = 100435, // 100p quality, 32cp cost, requires 10 iq stacks
        Reflect = 100387, // 100p quality, 6cp + 10 dur cost, requires first step, 1 extra iq stack
        RefinedTouch = 100443, // 100p quality, 24cp + 10 dur cost, combo from Basic Touch increases IQ by 1
        DaringTouch = 100451, //150p quality, 60% success rate, 0 cp, requires Hasty Touch Expedience buff

        ByregotsBlessing = 100339, // 100p+20*IQ quality, 24cp + 10 dur cost, removes iq
        TrainedEye = 100283, // max quality, 250cp, requires first step & low level recipe
        DelicateSynthesis = 100323, // 100p progress + 100p quality, 32cp + 10 dur cost

        Veneration = 19297, // increases progress gains, 18cp cost
        Innovation = 19004, // increases quality gains, 18cp cost
        GreatStrides = 260, // next quality action is significantly better, 32cp cost
        TricksOfTrade = 100371, // gain 20 cp, requires good/excellent condition or heart&soul
        MastersMend = 100003, // gain 30 durability, 88cp cost
        Manipulation = 4574, // gain 5 durability/step, 96cp cost
        WasteNot = 4631, // reduce durability costs, 56cp cost
        WasteNot2 = 4639, // reduce durability costs, 98cp cost
        Observe = 100010, // do nothing, 7cp cost
        CarefulObservation = 100395, // change condition
        FinalAppraisal = 19012, // next progress action can't finish craft, does not tick buffs or change conditions, 1cp cost
        HeartAndSoul = 100419, // next good-only action can be used without condition, does not tick buffs or change conditions
        QuickInnovation = 100459, // grants one stack of innovation, 0 cp, specialist
        ImmaculateMend = 100467, // Full durability, 112 cp
        TrainedPerfection = 100475 // Reduces next action durability loss to 0, 0 cp, once per craft
    }

    public static class SkillActionMap
    {
        private static Dictionary<uint, Skills> _actionToSkill = new();
        private static uint[,] _skillToAction = new uint[Enum.GetValues(typeof(Skills)).Length, 8];

        public static Skills ActionToSkill(uint actionId) => _actionToSkill.GetValueOrDefault(actionId);

        public static int Level(this Skills skill) => skill.ActionId(Job.CRP) >= 100000 ? LuminaSheets.CraftActions[skill.ActionId(Job.CRP)].ClassJobLevel : LuminaSheets.ActionSheet[skill.ActionId(Job.CRP)].ClassJobLevel;
        public static uint ActionId(this Skills skill, Job job) => job is >= Job.CRP and <= Job.CUL ? _skillToAction[Math.Max(Array.IndexOf(Enum.GetValues(typeof(Skills)), skill), (int)Skills.None), job - Job.CRP] : 0;

        static SkillActionMap()
        {
            foreach (Skills skill in (Skills[])Enum.GetValues(typeof(Skills)))
            {
                if (skill == Skills.None || skill == Skills.TouchCombo) continue;
                AssignActionIDs(skill);
            }
        }

        private static void AssignActionIDs(Skills skill)
        {
            var id = (uint)skill;
            var skillName = id >= 100000 ? LuminaSheets.CraftActions[id].Name.RawString.Trim() : LuminaSheets.ActionSheet[id].Name.RawString;

            for (Job i = Job.CRP; i <= Job.CUL; i++)
            {
                var enumIndex = Array.IndexOf(Enum.GetValues(typeof(Skills)), skill);
                var convertedId = id >= 100000 ? LuminaSheets.CraftActions.Values.FirstOrDefault(x => x.ClassJobCategory.Row == (int)i + 1 && x.Name.RawString == skillName).RowId : LuminaSheets.ActionSheet.Values.FirstOrDefault(x => x.ClassJob.Row == (int)i && x.Name.RawString == skillName).RowId;
                ref var entry = ref _skillToAction[enumIndex, i - Job.CRP];
                if (entry != 0)
                    throw new Exception($"Duplicate entry for {i} {skill}: {id} and {entry}");
                entry = convertedId;
                _actionToSkill[convertedId] = skill;
            }
        }
    }
}
