using Dalamud.Interface;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Artisan.RawInformation
{
    internal class Hotbars : AtkResNodeFunctions, IDisposable
    {
        public static Dictionary<int, HotBarSlot> HotbarDict = new Dictionary<int, HotBarSlot>();
        private static unsafe AtkUnitBase* HotBarRef { get; set; } = null;
        private static unsafe AtkResNode* HotBarSlotRef { get; set; } = null;

        private static unsafe ActionManager* actionManager = ActionManager.Instance();

        public void Dispose()
        {

        }

        public unsafe Hotbars()
        {
            PopulateHotbarDict();
        }

        public static unsafe void PopulateHotbarDict()
        {
            var raptureHotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
            HotbarDict.Clear();
            int count = 0;
            for (int i = 0; i <= 9; i++)
            {
                var hotbar = raptureHotbarModule->HotBar[i];
                if ((IntPtr)hotbar == IntPtr.Zero)
                    continue;

                for (int j = 0; j <= 11; j++)
                {
                    var slot = hotbar->Slot[j];
                    if ((IntPtr)slot == IntPtr.Zero)
                        continue;

                    var slotOb = *(HotBarSlot*)slot;

                    if (slotOb.CommandType == HotbarSlotType.Action || slotOb.CommandType == HotbarSlotType.CraftAction)
                    HotbarDict.TryAdd(count, slotOb);

                    count++;

                }
            }
        }

        public unsafe static void MakeButtonGlow(HotBarSlot slot, int index)
        {
            var hotbar = index / 12;
            var relativeLocation = index % 12;

            if (hotbar == 0)
            {
                HotBarRef = (AtkUnitBase*)Service.GameGui.GetAddonByName($"_ActionBar", 1);
                if (HotBarRef != null)
                {
                    HotBarSlotRef = HotBarRef->GetNodeById((uint)relativeLocation + 8);
                }

            }
            else
            {
                HotBarRef = (AtkUnitBase*)Service.GameGui.GetAddonByName($"_ActionBar0{hotbar}", 1);
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

        internal unsafe static void MakeButtonsGlow(uint rec)
        {
            if (rec == 0) return;

            PopulateHotbarDict();
            if (HotbarDict.Count == 0) return;

            if (rec >= 100000)
            {
                var sheet = LuminaSheets.CraftActions[rec];
                foreach (var slot in HotbarDict)
                {
                    if (LuminaSheets.CraftActions.TryGetValue(slot.Value.CommandId, out var action))
                    {
                        if (action.Name.RawString.Equals(sheet.Name.RawString, StringComparison.CurrentCultureIgnoreCase) && slot.Value.CommandType == HotbarSlotType.CraftAction)
                        MakeButtonGlow(slot.Value, slot.Key);
                    }
                    
                }
            }
            else
            {
                var sheet = LuminaSheets.ActionSheet[rec];
                foreach (var slot in HotbarDict)
                {
                    if (LuminaSheets.ActionSheet.TryGetValue(slot.Value.CommandId, out var action))
                    {
                        if (action.Name.RawString.Equals(sheet.Name.RawString, StringComparison.CurrentCultureIgnoreCase) && slot.Value.CommandType == HotbarSlotType.Action)
                        {
                            MakeButtonGlow(slot.Value, slot.Key);
                        }
                            
                    }

                }
            }
        }

        internal unsafe static void ExecuteRecommended(uint currentRecommendation)
        {
            if (currentRecommendation == 0) return;
            if (actionManager == null)
                return;

            ActionType actionType = currentRecommendation >= 100000 ? ActionType.CraftAction : ActionType.Spell;
            actionManager->UseAction(actionType, currentRecommendation);
            return;

            //PopulateHotbarDict();
            //if (currentRecommendation >= 100000)
            //{
            //    var sheet = LuminaSheets.CraftActions[currentRecommendation];
            //    foreach (var slot in HotbarDict)
            //    {
            //        if (LuminaSheets.CraftActions.TryGetValue(slot.Value.CommandId, out var action))
            //        {
            //            if (action.Name.RawString.Contains(sheet.Name.RawString, StringComparison.CurrentCultureIgnoreCase))
            //            {
            //                var raptureHotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
            //                var value = slot.Value;
            //                raptureHotbarModule->ExecuteSlot(&value);

            //                return;
            //            }
            //        }

            //    }
            //}
            //else
            //{
            //    var sheet = LuminaSheets.ActionSheet[currentRecommendation];
            //    foreach (var slot in HotbarDict)
            //    {
            //        if (LuminaSheets.ActionSheet.TryGetValue(slot.Value.CommandId, out var action))
            //        {
            //            if (action.Name.RawString.Contains(sheet.Name.RawString, StringComparison.CurrentCultureIgnoreCase))
            //            {
            //                var raptureHotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
            //                var value = slot.Value;
            //                raptureHotbarModule->ExecuteSlot(&value);

            //                return;

            //            }

            //        }

            //    }
            //}
        }
    }
}
