using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Runtime.InteropServices;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan.RawInformation
{
    public static class CharacterInfo
    {
        public static byte CharacterLevel()
            => Service.ClientState.LocalPlayer.Level;

        public static uint JobID()
            => Service.ClientState.LocalPlayer.ClassJob.Id;

        public static bool IsCrafting { get; set; }

        public static uint CurrentCP =>
            Service.ClientState.LocalPlayer.CurrentCp;

        public static uint MaxCP =>
    Service.ClientState.LocalPlayer.MaxCp;

        public static ulong Craftsmanship()
        {
            CharacterStats.FetchStats();
            return CharacterStats.Craftsmanship;
        }

        public static ulong Control()
        {
            CharacterStats.FetchStats();
            return CharacterStats.Control;
        }

        public static bool LevelChecked(this uint id)
        {
            if (LuminaSheets.ActionSheet.TryGetValue(id, out var act1))
            {
                return CharacterLevel() >= act1.ClassJobLevel;
            }
            if (LuminaSheets.CraftActions.TryGetValue(id, out var act2))
            {
                return CharacterLevel() >= act2.ClassJobLevel;
            }

            return false;
        }
        public static bool CanUseTrainedEye
            => Service.ClientState?.LocalPlayer?.Level >= Recipe?.RecipeLevelTable.Value?.ClassJobLevel + 10 && CurrentStep == 1 && Service.ClientState?.LocalPlayer?.Level >= 80;

        public static uint HighestLevelTouch()
        {
            if (Recipe.IsExpert)
            {
                if (CanUse(Skills.HastyTouch) && CurrentCondition is Condition.Centered) return Skills.HastyTouch;
                if (CanUse(Skills.PreciseTouch) && CurrentCondition is Condition.Good) return Skills.PreciseTouch;
                if (AdvancedTouchUsed && CanUse(Skills.PrudentTouch)) return Skills.PrudentTouch;
                if (CanUse(Skills.AdvancedTouch) && StandardTouchUsed) return Skills.AdvancedTouch;
                if (CanUse(Skills.StandardTouch) && BasicTouchUsed) return Skills.StandardTouch;
                if (CanUse(Skills.BasicTouch)) return Skills.BasicTouch;
            }
            else
            {
                if (CanUse(Skills.FocusedTouch) && JustUsedObserve) return Skills.FocusedTouch;
                if (CanUse(Skills.PreciseTouch) && (CurrentCondition is Condition.Good or Condition.Excellent)) return Skills.PreciseTouch;
                if (CanUse(Skills.PreparatoryTouch) && CurrentDurability > 20 && (GetStatus(Buffs.InnerQuiet)?.StackCount < 10 || GetStatus(Buffs.InnerQuiet) is null)) return Skills.PreparatoryTouch;
                if (CanUse(Skills.PrudentTouch) && GetStatus(Buffs.WasteNot2) == null && GetStatus(Buffs.WasteNot) == null) return Skills.PrudentTouch;
                if (CanUse(Skills.AdvancedTouch) && StandardTouchUsed) return Skills.AdvancedTouch;
                if (CanUse(Skills.StandardTouch) && BasicTouchUsed) return Skills.StandardTouch;
                if (CanUse(Skills.BasicTouch)) return Skills.BasicTouch;
            }

            return 0;
        }

        public static uint HighestLevelSynth()
        {
            if (CanUse(Skills.IntensiveSynthesis)) return Skills.IntensiveSynthesis;
            if (CanUse(Skills.FocusedSynthesis) && JustUsedObserve) return Skills.FocusedSynthesis;
            if (CanUse(Skills.Groundwork) && CurrentDurability > 20 && MaxDurability > 35) return Skills.Groundwork;
            if (CanUse(Skills.PrudentSynthesis)) return Skills.PrudentSynthesis;
            if (CanUse(Skills.CarefulSynthesis)) return Skills.CarefulSynthesis;
            if (CanUse(Skills.BasicSynth)) return Skills.BasicSynth;

            return 0;
        }

        internal static bool IsManipulationUnlocked()
        {
            return JobID() switch
            {
                8 => QuestUnlocked(67979),
                9 => QuestUnlocked(68153),
                10 => QuestUnlocked(68132),
                11 => QuestUnlocked(67974),
                12 => QuestUnlocked(68147),
                13 => QuestUnlocked(67969),
                14 => QuestUnlocked(67974),
                15 => QuestUnlocked(68142),
                _ => false,
            };
        }

        private unsafe static bool QuestUnlocked(int v)
        {
            return QuestManager.IsQuestComplete((uint)v);
        }

        internal static uint CraftLevel()
        {
            return CharacterLevel() switch
            {
                <= 50 => CharacterLevel(),
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
    }

    public unsafe static class CharacterStats
    {
        private static IntPtr playerStaticAddress;
        private static IntPtr getBaseParamAddress;
        private unsafe delegate ulong GetBaseParam(PlayerState* playerAddress, uint baseParamId);
        private static GetBaseParam getBaseParam;

        public static ulong Craftsmanship { get; set; }
        public static ulong Control { get; set; }

        private unsafe static void FetchMemory()
        {
            try
            {
                if (getBaseParamAddress == IntPtr.Zero)
                {
                    getBaseParamAddress = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 44 8B C0 33 D2 48 8B CB E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 0D");
                    getBaseParam = Marshal.GetDelegateForFunctionPointer<GetBaseParam>(getBaseParamAddress);
                }

            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex.Message);
            }
        }

        public unsafe static void FetchStats()
        {
            FetchMemory();
            if (UIState.Instance() is not null)
            {
                Craftsmanship = getBaseParam(&UIState.Instance()->PlayerState, 70);
                Control = getBaseParam(&UIState.Instance()->PlayerState, 71);
            }
        }

    }
}
