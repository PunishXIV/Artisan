using Dalamud.Game.ClientState.Statuses;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.RawInformation.Character
{
    public static class CharacterInfo
    {
        public static unsafe void UpdateCharaStats()
        {
            if (Svc.ClientState.LocalPlayer is null) return;

            JobID = (Job)(Svc.ClientState.LocalPlayer?.ClassJob.Value.RowId ?? 0);
            CharacterLevel = Svc.ClientState.LocalPlayer?.Level;
            CurrentCP = Svc.ClientState.LocalPlayer.CurrentCp;
            MaxCP = Svc.ClientState.LocalPlayer.MaxCp;
            Craftsmanship = PlayerState.Instance()->Attributes[70];
            Control = PlayerState.Instance()->Attributes[71];
            FCCraftsmanshipbuff = Svc.ClientState.LocalPlayer?.StatusList.FirstOrDefault(x => x.StatusId == 356);
        }

        public static byte? CharacterLevel;

        public static Job JobID;

        public static uint CurrentCP;

        public static uint MaxCP;

        public static unsafe int Craftsmanship;

        public static unsafe int Control;

        public static unsafe Dalamud.Game.ClientState.Statuses.Status? FCCraftsmanshipbuff;

        public static unsafe int JobLevel(Job job) => PlayerState.Instance()->ClassJobLevels[Svc.Data.GetExcelSheet<ClassJob>()?.GetRow((uint)job).ExpArrayIndex ?? 0];

        internal static bool IsManipulationUnlocked(Job job) =>  job switch
        {
            Job.CRP => QuestUnlocked(67979),
            Job.BSM => QuestUnlocked(68153),
            Job.ARM => QuestUnlocked(68132),
            Job.GSM => QuestUnlocked(68137),
            Job.LTW => QuestUnlocked(68147),
            Job.WVR => QuestUnlocked(67969),
            Job.ALC => QuestUnlocked(67974),
            Job.CUL => QuestUnlocked(68142),
            _ => false,
        };

        private unsafe static bool QuestUnlocked(int v)
        {
            return QuestManager.IsQuestComplete((uint)v);
        }

        public static bool MateriaExtractionUnlocked() => QuestUnlocked(66174);

        internal static uint CraftLevel() => CharacterLevel switch
        {
            <= 50 => (uint)CharacterLevel,
            51 => 120,
            52 => 125,
            53 => 130,
            54 => 133,
            55 => 136,
            56 => 139,
            57 => 142,
            58 => 145,
            59 => 148,
            60 => 150,
            61 => 260,
            62 => 265,
            63 => 270,
            64 => 273,
            65 => 276,
            66 => 279,
            67 => 282,
            68 => 285,
            69 => 288,
            70 => 290,
            71 => 390,
            72 => 395,
            73 => 400,
            74 => 403,
            75 => 406,
            76 => 409,
            77 => 412,
            78 => 415,
            79 => 418,
            80 => 420,
            81 => 517,
            82 => 520,
            83 => 525,
            84 => 530,
            85 => 535,
            86 => 540,
            87 => 545,
            88 => 550,
            89 => 555,
            90 => 560,
            _ => 0,
        };
    }

    internal unsafe class RecipeInformation : IDisposable
    {
        delegate byte HasItemBeenCraftedDelegate(uint recipe);
        [Signature("40 53 48 83 EC 20 8B D9 81 F9")]
        HasItemBeenCraftedDelegate GetIsGatheringItemGathered = null!;

        private List<uint> Uncompletables = new List<uint>()
        {
            30971, 30987, 31023, 31052,31094, 31157, 31192, 31217, 30001, 30002, 30003, 30004, 30005, 30006, 
            30007, 30008, 30009, 30010, 30011, 30012, 30013, 30014, 30015, 30016, 30017, 30018, 30019, 30020,
            30021, 30022, 30023, 30024, 30025, 30026, 30027, 30028, 30029, 30030, 30031, 30032, 30033, 30034, 
            30035, 30036, 30037, 30038, 30039, 30040, 30041, 30042, 30043, 30044, 30045, 30046, 30047, 30048, 
            30049, 30050, 30051, 30052, 30053, 30054, 30055, 30056, 30057, 30058, 30059, 30060, 30061, 30062, 
            30063, 30064, 30065, 30066, 30067, 30068, 30069, 30070, 30071, 30072, 30073, 30074, 30075, 30076, 
            30077, 30078, 30079, 30080, 30081, 30082, 30083, 30084, 30085, 30086, 30087, 30088, 30089, 30090, 
            30091, 30092, 30093, 30094, 30095, 30096, 30097, 30098, 30099, 30100, 30101, 30102, 30103, 30104, 
            30105, 30106, 30107, 30108, 30109, 30110, 30111, 30112, 30113, 30114, 30115, 30116, 30117, 30118, 
            30119, 30120, 30121, 30122, 30123, 30124, 30125, 30126, 30127, 30128, 30129, 30130, 30131, 30132, 
            30133, 30134, 30135, 30136, 30137, 30138, 30139, 30140, 30141, 30142, 30143, 30144, 30145, 30146, 
            30147, 30148, 30149, 30150, 30151, 30152, 30354, 30355, 30356, 30357, 30358, 30359, 30360, 30361, 
            30362, 30363, 30364, 30365, 30366, 30367, 30368, 30369, 30370, 30371, 30372, 30373, 30374, 30375, 
            30376, 30377, 30378, 30379, 30380, 30381, 30382, 30383, 30384, 30385, 30386, 30387, 30388, 30389, 
            30390, 30391, 30392, 30393, 30394, 30395, 30396, 30397, 30398, 30399, 30400, 30401
        };

        public bool HasRecipeCrafted(uint recipe)
        {
            if (Uncompletables.Any(x => x == recipe)) return true;
            if (!LuminaSheets.RecipeSheet.ContainsKey(recipe)) return false;
            if (LuminaSheets.RecipeSheet[recipe].SecretRecipeBook.RowId > 0) return true;

            return GetIsGatheringItemGathered(recipe) != 0;
        }

        public void Dispose()
        {
            
        }

        internal RecipeInformation()
        {
            Svc.Hook.InitializeFromAttributes(this);

        }
    }
}
