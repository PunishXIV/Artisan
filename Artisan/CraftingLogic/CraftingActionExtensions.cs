using Artisan.RawInformation.Character;
using Dalamud.Game.ClientState.Statuses;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using System.Linq;

namespace Artisan.CraftingLogic
{
    public static class CraftingActionExtensions
    {
        public static int ProgressIncrease(this uint Id)
        {
            var multiplier = Calculations.GetMultiplier(Id, false);
            double veneration = CurrentCraftMethods.GetStatus(Buffs.Veneration) != null ? 0.5 : 0;
            double muscleMemory = CurrentCraftMethods.GetStatus(Buffs.MuscleMemory) != null ? 1 : 0;
            return (int)Math.Floor(Calculations.BaseProgression() * multiplier * (veneration + muscleMemory + 1));
        }

        public static int QualityIncrease(this uint id)
        {
            double efficiency = Calculations.GetMultiplier(id, true);
            double IQStacks = CurrentCraftMethods.GetStatus(Buffs.InnerQuiet) is null ? 1 : 1 + (CurrentCraftMethods.GetStatus(Buffs.InnerQuiet).StackCount * 0.1);
            double innovation = CurrentCraftMethods.GetStatus(Buffs.Innovation) is not null ? 0.5 : 0;
            double greatStrides = CurrentCraftMethods.GetStatus(Buffs.GreatStrides) is not null ? 1 : 0;

            return (int)Math.Floor(Calculations.BaseQuality() * efficiency * IQStacks * (innovation + greatStrides + 1));

        }

        public static bool IsAvailable(this ProgressEfficiencyCalculation progressActionInfo)
    => progressActionInfo.Status == ActionStatus.Available;

        public static bool IsAvailable(this QualityEfficiencyCalculation progressActionInfo)
            => progressActionInfo.Status == ActionStatus.Available;

        public static bool? CanComplete(this ProgressEfficiencyCalculation progressActionInfo, uint remainingProgress)
        {
            if (!progressActionInfo.IsAvailable())
                return null;

            return progressActionInfo.ProgressIncrease >= remainingProgress;
        }

        public static bool CanComplete(this QualityEfficiencyCalculation qualityActionInfo, uint remainingQuality)
            => qualityActionInfo.IsAvailable() && qualityActionInfo.QualityIncrease >= remainingQuality;

        public static bool HasStatus(this StatusList? statusList, out int stack, params CraftingPlayerStatuses[] elligable)
        {
            stack = 0;

            var status = statusList?.FirstOrDefault(buff => elligable.Any(s => buff.StatusId == (uint)s));
            if (status == null)
                return false;

            stack = status.StackCount;
            return true;
        }
    }
}
