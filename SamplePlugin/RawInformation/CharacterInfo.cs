using System;
using System.Runtime.InteropServices;

namespace CraftIt.RawInformation
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

        public static bool CanUseTrainedEye
            => Service.ClientState?.LocalPlayer?.Level >= CraftingLogic.CurrentCraft.Recipe?.RecipeLevelTable.Value?.ClassJobLevel + 10 && CraftingLogic.CurrentCraft.CurrentStep == 1 && Service.ClientState?.LocalPlayer?.Level >= 80;
        
    }

    public static class CharacterStats
    {
        private static IntPtr playerStaticAddress;
        private static IntPtr getBaseParamAddress;
        private delegate ulong GetBaseParam(IntPtr playerAddress, uint baseParamId);
        private static GetBaseParam getBaseParam;

        public static ulong Craftsmanship;
        public static ulong Control;

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
