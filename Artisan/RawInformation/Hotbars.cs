using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;

namespace Artisan.RawInformation
{
    internal class Hotbars : AtkResNodeFunctions, IDisposable
    {
        public static Dictionary<int, HotBarSlot> HotbarDict = new Dictionary<int, HotBarSlot>();
        private static unsafe AtkUnitBase* HotBarRef { get; set; } = null;
        private static unsafe AtkResNode* HotBarSlotRef { get; set; } = null;

        public static unsafe ActionManager* actionManager = ActionManager.Instance();

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
            for (int j = 0; j < 10; j++)
            {
                var hotbar = raptureHotbarModule->HotBarsSpan[j];
                
                for (uint i = 0; i <= 11; i++)
                {
                    var slot = *hotbar.GetHotbarSlot(i);

                    //Svc.Log.Debug($"{slot.CommandId.NameOfAction()}");
                    if (&slot != null)
                    {
                        if (slot.CommandType == HotbarSlotType.Action || slot.CommandType == HotbarSlotType.CraftAction)
                        {
                            HotbarDict.TryAdd(count, slot);
                        }
                    }

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

        internal unsafe static bool ExecuteRecommended(uint currentRecommendation)
        {
            if (currentRecommendation == 0) return false;
            if (actionManager == null)
                return false;

            ActionType actionType = currentRecommendation >= 100000 ? ActionType.CraftAction : ActionType.Action;
            if (actionManager->GetActionStatus(actionType, currentRecommendation, Svc.ClientState.LocalPlayer.ObjectId) != 0) return false;
            ActionWatching.BlockAction = false;
            actionManager->UseAction(actionType, currentRecommendation);
            return true;
        }
    }
}
