using ECommons.DalamudServices;
using ECommons.ExcelServices;
using System;
using System.Collections.Generic;

namespace Artisan.RawInformation.Character
{
    public enum Skills
    {
        None,

        BasicSynthesis, // 120p progress, 10dur cost
        CarefulSynthesis, // 180p progress, 7cp + 10 dur cost
        RapidSynthesis, // 500p progress, 10 dur cost, 50% success
        FocusedSynthesis, // 200p progress, 5cp + 10 dur cost, 50% success unless after observe
        Groundwork, // 360p progress, 18cp + 20 dur cost, half potency if durability left is less than required
        IntensiveSynthesis, // 400p progress, 6cp + 10 dur cost, requires good/excellent condition or heart&soul
        PrudentSynthesis, // 180p progress, 18cp + 5 dur cost, can't be used under waste-not
        MuscleMemory, // 300p progress, 6cp + 10 dur cost, requires first step, applies buff

        BasicTouch, // 100p quality, 18cp + 10 dur cost
        StandardTouch, // 125p quality, 18cp + 10 dur cost if used after basic touch (otherwise 32cp)
        AdvancedTouch, // 150p quality, 18cp + 10 dur cost if used after standard touch (otherwise 46cp)
        HastyTouch, // 100p quality, 10 dur cost, 60% success
        FocusedTouch, // 150p quality, 18cp + 10 dur cost, 50% success unless after observe
        PreparatoryTouch, // 200p quality, 40cp + 20 dur cost, 1 extra iq stack
        PreciseTouch, // 150p quality, 18cp + 10 dur cost, 1 extra iq stack, requires good/excellent condition or heart&soul
        PrudentTouch, // 100p quality, 25cp + 5 dur cost, can't be used under waste-not
        TrainedFinesse, // 100p quality, 32cp cost, requires 10 iq stacks
        Reflect, // 100p quality, 6cp + 10 dur cost, requires first step, 1 extra iq stack

        ByregotsBlessing, // 100p+20*IQ quality, 24cp + 10 dur cost, removes iq
        TrainedEye, // max quality, 250cp, requires first step & low level recipe
        DelicateSynthesis, // 100p progress + 100p quality, 32cp + 10 dur cost

        Veneration, // increases progress gains, 18cp cost
        Innovation, // increases quality gains, 18cp cost
        GreatStrides, // next quality action is significantly better, 32cp cost
        TricksOfTrade, // gain 20 cp, requires good/excellent condition or heart&soul
        MastersMend, // gain 30 durability, 88cp cost
        Manipulation, // gain 5 durability/step, 96cp cost
        WasteNot, // reduce durability costs, 56cp cost
        WasteNot2, // reduce durability costs, 98cp cost
        Observe, // do nothing, 7cp cost
        CarefulObservation, // change condition
        FinalAppraisal, // next progress action can't finish craft, does not tick buffs or change conditions, 1cp cost
        HeartAndSoul, // next good-only action can be used without condition, does not tick buffs or change conditions

        Count
    }

    public static class SkillActionMap
    {
        private static Dictionary<uint, Skills> _actionToSkill = new();
        private static uint[,] _skillToAction = new uint[(int)Skills.Count, 8];

        public static Skills ActionToSkill(uint actionId) => _actionToSkill.GetValueOrDefault(actionId);
        public static uint ActionId(this Skills skill, Job job) => job is >= Job.CRP and <= Job.CUL && skill is > Skills.None and < Skills.Count ? _skillToAction[(int)skill, job - Job.CRP] : 0;

        static SkillActionMap()
        {
            AssignActionIDs(Skills.BasicSynthesis, 100001, 100015, 100030, 100075, 100045, 100060, 100090, 100105);
            AssignActionIDs(Skills.CarefulSynthesis, 100203, 100204, 100205, 100206, 100207, 100208, 100209, 100210);
            AssignActionIDs(Skills.RapidSynthesis, 100363, 100364, 100365, 100366, 100367, 100368, 100369, 100370);
            AssignActionIDs(Skills.FocusedSynthesis, 100235, 100236, 100237, 100238, 100239, 100240, 100241, 100242);
            AssignActionIDs(Skills.Groundwork, 100403, 100404, 100405, 100406, 100407, 100408, 100409, 100410);
            AssignActionIDs(Skills.IntensiveSynthesis, 100315, 100316, 100317, 100318, 100319, 100320, 100321, 100322);
            AssignActionIDs(Skills.PrudentSynthesis, 100427, 100428, 100429, 100430, 100431, 100432, 100433, 100434);
            AssignActionIDs(Skills.MuscleMemory, 100379, 100380, 100381, 100382, 100383, 100384, 100385, 100386);
            AssignActionIDs(Skills.BasicTouch, 100002, 100016, 100031, 100076, 100046, 100061, 100091, 100106);
            AssignActionIDs(Skills.StandardTouch, 100004, 100018, 100034, 100078, 100048, 100064, 100093, 100109);
            AssignActionIDs(Skills.AdvancedTouch, 100411, 100412, 100413, 100414, 100415, 100416, 100417, 100418);
            AssignActionIDs(Skills.HastyTouch, 100355, 100356, 100357, 100358, 100359, 100360, 100361, 100362);
            AssignActionIDs(Skills.FocusedTouch, 100243, 100244, 100245, 100246, 100247, 100248, 100249, 100250);
            AssignActionIDs(Skills.PreparatoryTouch, 100299, 100300, 100301, 100302, 100303, 100304, 100305, 100306);
            AssignActionIDs(Skills.PreciseTouch, 100128, 100129, 100130, 100131, 100132, 100133, 100134, 100135);
            AssignActionIDs(Skills.PrudentTouch, 100227, 100228, 100229, 100230, 100231, 100232, 100233, 100234);
            AssignActionIDs(Skills.TrainedFinesse, 100435, 100436, 100437, 100438, 100439, 100440, 100441, 100442);
            AssignActionIDs(Skills.Reflect, 100387, 100388, 100389, 100390, 100391, 100392, 100393, 100394);
            AssignActionIDs(Skills.ByregotsBlessing, 100339, 100340, 100341, 100342, 100343, 100344, 100345, 100346);
            AssignActionIDs(Skills.TrainedEye, 100283, 100284, 100285, 100286, 100287, 100288, 100289, 100290);
            AssignActionIDs(Skills.DelicateSynthesis, 100323, 100324, 100325, 100326, 100327, 100328, 100329, 100330);
            AssignActionIDs(Skills.Veneration, 19297, 19298, 19299, 19300, 19301, 19302, 19303, 19304);
            AssignActionIDs(Skills.Innovation, 19004, 19005, 19006, 19007, 19008, 19009, 19010, 19011);
            AssignActionIDs(Skills.GreatStrides, 260, 261, 262, 263, 264, 265, 266, 267);
            AssignActionIDs(Skills.TricksOfTrade, 100371, 100372, 100373, 100374, 100375, 100376, 100377, 100378);
            AssignActionIDs(Skills.MastersMend, 100003, 100017, 100032, 100047, 100062, 100077, 100092, 100107);
            AssignActionIDs(Skills.Manipulation, 4574, 4575, 4576, 4577, 4578, 4579, 4580, 4581);
            AssignActionIDs(Skills.WasteNot, 4631, 4632, 4633, 4634, 4635, 4636, 4637, 4638);
            AssignActionIDs(Skills.WasteNot2, 4639, 4640, 4641, 4642, 4643, 4644, 19002, 19003);
            AssignActionIDs(Skills.Observe, 100010, 100023, 100040, 100053, 100070, 100082, 100099, 100113);
            AssignActionIDs(Skills.CarefulObservation, 100395, 100396, 100397, 100398, 100399, 100400, 100401, 100402);
            AssignActionIDs(Skills.FinalAppraisal, 19012, 19013, 19014, 19015, 19016, 19017, 19018, 19019);
            AssignActionIDs(Skills.HeartAndSoul, 100419, 100420, 100421, 100422, 100423, 100424, 100425, 100426);
        }

        private static void AssignActionIDs(Skills skill, params uint[] ids)
        {
            var sheetAction = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>();
            var sheetCraftAction = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.CraftAction>();
            foreach (var id in ids)
            {
                var classRow = id >= 100000 ? sheetCraftAction.GetRow(id)?.ClassJob : sheetAction.GetRow(id)?.ClassJob;
                if (classRow == null)
                    throw new Exception($"Failed to find definition for {skill} {id}");
                var job = (Job)classRow.Row;
                if (job is < Job.CRP or > Job.CUL)
                    throw new Exception($"Unexpected class {classRow.Row} ({classRow.Value?.Abbreviation}) for {skill} {id}");
                ref var entry = ref _skillToAction[(int)skill, job - Job.CRP];
                if (entry != 0)
                    throw new Exception($"Duplicate entry for {classRow.Value?.Abbreviation} {skill}: {id} and {entry}");
                entry = id;
                _actionToSkill[id] = skill;
            }
        }
    }
}
