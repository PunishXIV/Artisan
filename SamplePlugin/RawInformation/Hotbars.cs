using Dalamud.Interface;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace CraftIt.RawInformation
{
    internal class Hotbars : IDisposable
    {
        public static Dictionary<int, HotBarSlot> HotbarDict = new Dictionary<int, HotBarSlot>();
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
            var raptureHotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
            HotbarDict.Clear();
            int count = 0;
            for (int i = 0; i <= 9; i++)
            {
                var hotbar = raptureHotbarModule->HotBar[i];

                for (int j = 0; j <= 11; j++)
                {
                    var slot = hotbar->Slot[j];
                    var slotOb = *(HotBarSlot*)slot;

                    HotbarDict.Add(count, slotOb);
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

            if (HotBarSlotRef != null)
            {
                DrawOutline(HotBarSlotRef);
            }

        }

        internal unsafe static void MakeButtonsGlow(uint rec)
        {
            if (rec == 0) return;

            PopulateHotbarDict();
            if (rec >= 100000)
            {
                var sheet = LuminaSheets.CraftActions[rec];
                foreach (var slot in HotbarDict)
                {
                    if (LuminaSheets.CraftActions.TryGetValue(slot.Value.CommandId, out var action))
                    {
                        if (action.Name.RawString.Contains(sheet.Name.RawString, StringComparison.CurrentCultureIgnoreCase))
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
                        if (action.Name.RawString.Contains(sheet.Name.RawString, StringComparison.CurrentCultureIgnoreCase))
                        {
                            Dalamud.Logging.PluginLog.Debug($"{slot.Key}");
                            MakeButtonGlow(slot.Value, slot.Key);
                        }
                            
                    }

                }
            }
        }

        private static unsafe Vector2 GetNodePosition(AtkResNode* node)
        {
            var pos = new Vector2(node->X, node->Y);
            var par = node->ParentNode;
            while (par != null)
            {
                pos *= new Vector2(par->ScaleX, par->ScaleY);
                pos += new Vector2(par->X, par->Y);
                par = par->ParentNode;
            }

            return pos;
        }

        private static unsafe Vector2 GetNodeScale(AtkResNode* node)
        {
            if (node == null) return new Vector2(1, 1);
            var scale = new Vector2(node->ScaleX, node->ScaleY);
            while (node->ParentNode != null)
            {
                node = node->ParentNode;
                scale *= new Vector2(node->ScaleX, node->ScaleY);
            }

            return scale;
        }

        private unsafe static void DrawOutline(AtkResNode* node)
        {
            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            var center = new Vector2((position.X + size.X) /2, (position.Y - size.Y) / 2);

            position += ImGuiHelpers.MainViewport.Pos;

            ImGui.GetForegroundDrawList(ImGuiHelpers.MainViewport).AddRect(position, position + size, 0xFFFFFF00, 0, ImDrawFlags.RoundCornersAll, 8);
        }

        internal unsafe static void ExecuteRecommended(uint currentRecommendation)
        {
            if (currentRecommendation == 0) return;

            PopulateHotbarDict();
            if (currentRecommendation >= 100000)
            {
                var sheet = LuminaSheets.CraftActions[currentRecommendation];
                foreach (var slot in HotbarDict)
                {
                    if (LuminaSheets.CraftActions.TryGetValue(slot.Value.CommandId, out var action))
                    {
                        if (action.Name.RawString.Contains(sheet.Name.RawString, StringComparison.CurrentCultureIgnoreCase))
                        {
                            var raptureHotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
                            var value = slot.Value;
                            raptureHotbarModule->ExecuteSlot(&value);

                            return;
                        }
                    }

                }
            }
            else
            {
                var sheet = LuminaSheets.ActionSheet[currentRecommendation];
                foreach (var slot in HotbarDict)
                {
                    if (LuminaSheets.ActionSheet.TryGetValue(slot.Value.CommandId, out var action))
                    {
                        if (action.Name.RawString.Contains(sheet.Name.RawString, StringComparison.CurrentCultureIgnoreCase))
                        {
                            var raptureHotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
                            var value = slot.Value;
                            raptureHotbarModule->ExecuteSlot(&value);

                            return;

                        }

                    }

                }
            }
        }
    }
}
