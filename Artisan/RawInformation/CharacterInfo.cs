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
            => Service.ClientState?.LocalPlayer?.Level >= Recipe?.RecipeLevelTable.Value?.ClassJobLevel + 10 && CraftingLogic.CurrentCraft.CurrentStep == 1 && Service.ClientState?.LocalPlayer?.Level >= 80;

        public static uint HighestLevelTouch()
        {
            if (CanUse(Skills.TrainedFinesse) && GetStatus(Buffs.InnerQuiet)?.StackCount == 10) return Skills.TrainedFinesse;
            if (CanUse(Skills.PreciseTouch) && (CurrentCondition is Condition.Good or Condition.Excellent)) return Skills.PreciseTouch;
            if (CanUse(Skills.PreparatoryTouch) && CurrentDurability > 20) return Skills.PreparatoryTouch;
            if (CanUse(Skills.AdvancedTouch) && GetResourceCost(Skills.AdvancedTouch) == 18) return Skills.AdvancedTouch;
            if (CanUse(Skills.FocusedTouch) && JustUsedObserve) return Skills.FocusedTouch;
            if (CanUse(Skills.PrudentTouch) && GetStatus(Buffs.WasteNot2) == null && GetStatus(Buffs.WasteNot) == null) return Skills.PrudentTouch;
            if (CanUse(Skills.StandardTouch) && GetResourceCost(Skills.StandardTouch) == 18) return Skills.StandardTouch;
            if (CanUse(Skills.BasicTouch)) return Skills.BasicTouch;

            return 0;
        }

        public static uint HighestLevelSynth()
        {
            if (CanUse(Skills.IntensiveSynthesis)) return Skills.IntensiveSynthesis;
            if (CanUse(Skills.FocusedSynthesis) && JustUsedObserve) return Skills.FocusedSynthesis;
            if (CanUse(Skills.Groundwork) && CurrentDurability > 20) return Skills.Groundwork;
            if (CanUse(Skills.PrudentSynthesis)) return Skills.PrudentSynthesis;
            if (CanUse(Skills.CarefulSynthesis)) return Skills.CarefulSynthesis;
            if (CanUse(Skills.BasicSynth)) return Skills.BasicSynth;

            return 0;
        }
    }

    public static class CharacterStats
    {
        private static IntPtr playerStaticAddress;
        private static IntPtr getBaseParamAddress;
        private delegate ulong GetBaseParam(IntPtr playerAddress, uint baseParamId);
        private static GetBaseParam getBaseParam;

        public static ulong Craftsmanship { get; set; }
        public static ulong Control { get; set; }

        private static void FetchMemory()
        {
            try
            {
                if (getBaseParamAddress == IntPtr.Zero)
                {
                    getBaseParamAddress = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 44 8B C0 33 D2 48 8B CB E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8D 0D");
                    getBaseParam = Marshal.GetDelegateForFunctionPointer<GetBaseParam>(getBaseParamAddress);
                }

                if (playerStaticAddress == IntPtr.Zero)
                {
                    playerStaticAddress = Service.SigScanner.GetStaticAddressFromSig("8B D7 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B7 E8");
                }

            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex.Message);
            }
        }

        public static void FetchStats()
        {
            if (playerStaticAddress != IntPtr.Zero)
            {
                Craftsmanship = getBaseParam(playerStaticAddress, 70);
                Control = getBaseParam(playerStaticAddress, 71);
            }
            else
            {
                FetchMemory();
            }
        }

    }
}
