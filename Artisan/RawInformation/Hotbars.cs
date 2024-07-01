using Artisan.CraftingLogic;
using Artisan.GameInterop;
using Artisan.RawInformation.Character;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;

namespace Artisan.RawInformation
{
    internal class Hotbars : AtkResNodeFunctions, IDisposable
    {
        private static Skills[] HotBarSkills = new Skills[10 * 12];
        private static unsafe AtkUnitBase* HotBarRef { get; set; } = null;
        private static unsafe AtkResNode* HotBarSlotRef { get; set; } = null;

        public void Dispose()
        {

        }

        public unsafe Hotbars()
        {
            PopulateHotbarDict();
        }

        public static unsafe void PopulateHotbarDict()
        {
            var raptureHotbarModule = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule();
            int index = 0;
            foreach (ref var hotbar in raptureHotbarModule->Hotbars.Slice(0, 10))
            {
                foreach (ref var slot in hotbar.Slots.Slice(0, 12))
                {
                    HotBarSkills[index++] = slot.CommandType is HotbarSlotType.Action or HotbarSlotType.CraftAction ? SkillActionMap.ActionToSkill(slot.CommandId) : Skills.None;
                }
            }
        }

        public unsafe static void MakeButtonGlow(int index)
        {
            var hotbar = index / 12;
            var relativeLocation = index % 12;

            if (hotbar == 0)
            {
                HotBarRef = (AtkUnitBase*)Svc.GameGui.GetAddonByName($"_ActionBar", 1);
                if (HotBarRef != null)
                {
                    HotBarSlotRef = HotBarRef->GetNodeById((uint)relativeLocation + 8);
                }

            }
            else
            {
                HotBarRef = (AtkUnitBase*)Svc.GameGui.GetAddonByName($"_ActionBar0{hotbar}", 1);
                if (HotBarRef != null)
                {
                    HotBarSlotRef = HotBarRef->GetNodeById((uint)relativeLocation + 8);
                }
            }

            if (HotBarSlotRef != null && HotBarRef->IsVisible)
            {
                DrawOutline(HotBarSlotRef);
            }

        }

        internal unsafe static void MakeButtonsGlow(Skills rec)
        {
            if (rec == Skills.None || Crafting.CurCraft == null) return;

            if (!Simulator.CanUseAction(Crafting.CurCraft, Crafting.CurStep, CraftingProcessor.NextRec.Action))
                return;

            PopulateHotbarDict();
            for (int i = 0; i < HotBarSkills.Length; ++i)
                if (HotBarSkills[i] == rec)
                    MakeButtonGlow(i);
        }
    }
}
