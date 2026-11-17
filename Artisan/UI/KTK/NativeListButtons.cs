using Dalamud.Game.ClientState.Keys;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
using System;
using System.Numerics;

namespace Artisan.UI.KTK
{
    internal unsafe class NativeListButtons
    {
        const float TimersButtonHeight = 28f;
        const float TimersGrowBy = TimersButtonHeight * 2 + 6;

        AddonController? workshopController;
        AddonController? supplyController;
        AddonController? timersController;

        TextButtonNode? workshopPhase;
        TextButtonNode? workshopPhasePrecrafts;
        TextButtonNode? supplyList;
        TextButtonNode? supplyListSubcrafts;
        TextButtonNode? timersList;
        TextButtonNode? timersListSubcrafts;

        bool timersResized;
        bool? timersShiftShown;

        static bool Shift => Svc.KeyState[VirtualKey.SHIFT];

        public NativeListButtons()
        {
            workshopController = new()
            {
                AddonName = "SubmarinePartsMenu",
                OnUpdate = WorkshopUpdate,
                OnFinalize = _ => DisposeWorkshop(),
            };
            workshopController.Enable();

            supplyController = new()
            {
                AddonName = "GrandCompanySupplyList",
                OnUpdate = SupplyUpdate,
                OnFinalize = _ => DisposeSupply(),
            };
            supplyController.Enable();

            timersController = new()
            {
                AddonName = "ContentsInfoDetail",
                OnUpdate = TimersUpdate,
                OnFinalize = _ => DisposeTimers(),
            };
            timersController.Enable();
        }

        static Vector2 LocalPosition(AtkUnitBase* addon, AtkResNode* node)
        {
            var pos = Vector2.Zero;
            for (var n = node; n != null && n != addon->RootNode; n = n->ParentNode)
                pos += new Vector2(n->X, n->Y);
            return pos;
        }

        static TextButtonNode MakeButton(AtkUnitBase* addon, Vector2 position, Vector2 size, uint nodeId, Action onClick)
        {
            var button = new TextButtonNode
            {
                Position = position,
                Size = size,
                OnClick = onClick,
                TextTooltip = "Button added by Artisan.",
                NodeId = nodeId,
            };
            button.AttachNode(addon);
            return button;
        }

        static string GCLabel(bool subcrafts) => $"Create Crafting List{(subcrafts ? " (with subcrafts)" : "")}{(Shift ? " (Star only)" : "")}";

        static void OpenLists()
        {
            P.PluginUi.IsOpen = true;
            P.PluginUi.OpenWindow = OpenWindow.Lists;
        }

        private void WorkshopUpdate(AtkUnitBase* addon)
        {
            if (addon->UldManager.NodeListCount < 38)
                return;

            var node = addon->UldManager.NodeList[2];
            if (workshopPhase is null)
            {
                var pos = LocalPosition(addon, node);
                var bottom = pos.Y + node->Height;
                var size = new Vector2(380, 28);
                workshopPhase = MakeButton(addon, new Vector2(pos.X + 4, bottom - 48), size, 100100, () => RecipeWindowUI.CreateWorkshopPhaseList(addon, false));
                workshopPhase.String = "Create crafting list for this phase";
                workshopPhasePrecrafts = MakeButton(addon, new Vector2(pos.X + 4, bottom - 18), size, 100101, () => RecipeWindowUI.CreateWorkshopPhaseList(addon, true));
                workshopPhasePrecrafts.String = "Create crafting list for this phase (including precrafts)";
            }

            var visible = node->IsVisible();
            workshopPhase.IsVisible = visible;
            workshopPhasePrecrafts!.IsVisible = visible;
        }

        private void SupplyUpdate(AtkUnitBase* addon)
        {
            var gcAddon = (AddonGrandCompanySupplyList*)addon;
            if (gcAddon->SupplyRadioButton is null)
                return;

            var node = addon->GetNodeById(27);
            if (node is null)
                return;

            if (supplyList is null)
            {
                var pos = LocalPosition(addon, node) - new Vector2(0, 10);
                var size = new Vector2(node->Width / 2f, 28);
                supplyList = MakeButton(addon, pos, size, 100110, () => { RecipeWindowUI.CreateGCListAgent(addon, false, Shift); OpenLists(); });
                supplyListSubcrafts = MakeButton(addon, pos + new Vector2(size.X, 0), size, 100111, () => { RecipeWindowUI.CreateGCListAgent(addon, true, Shift); OpenLists(); });
            }

            var radioNode = gcAddon->SupplyRadioButton->UldManager.NodeList[1];
            var onSupplyTab = radioNode is null || !radioNode->IsVisible();

            if (onSupplyTab && node->IsVisible())
                node->ToggleVisibility(false);

            supplyList.IsVisible = onSupplyTab;
            supplyListSubcrafts!.IsVisible = onSupplyTab;
            supplyList.String = GCLabel(false);
            supplyListSubcrafts.String = GCLabel(true);
        }

        private void TimersUpdate(AtkUnitBase* addon)
        {
            var node = addon->GetNodeById(15);
            if (node is null)
                return;

            var isSupply = addon->AtkValuesCount > 233 && addon->AtkValues[233].Type == AtkValueType.Int;

            if (isSupply && !timersResized)
            {
                var splitY = LocalPosition(addon, node).Y;
                var pos = new Vector2(LocalPosition(addon, node).X, splitY + 2);

                addon->SetSize((ushort)addon->RootNode->Width, (ushort)(addon->RootNode->Height + TimersGrowBy));
                var movedNode = false;
                for (var child = addon->RootNode->ChildNode; child != null; child = child->PrevSiblingNode)
                {
                    if (child->Y < splitY)
                        continue;
                    child->SetYFloat(child->Y + TimersGrowBy);
                    movedNode |= child == node;
                }
                if (!movedNode)
                    node->SetYFloat(node->Y + TimersGrowBy);
                timersResized = true;

                var size = new Vector2(node->Width, TimersButtonHeight);
                timersList = MakeButton(addon, pos, size, 100120, () => { RecipeWindowUI.CreateGCList(addon, false, Shift); OpenLists(); });
                timersListSubcrafts = MakeButton(addon, pos + new Vector2(0, TimersButtonHeight + 2), size, 100121, () => { RecipeWindowUI.CreateGCList(addon, true, Shift); OpenLists(); });
            }

            if (timersList is null)
                return;

            var visible = isSupply && node->IsVisible();
            timersList.IsVisible = visible;
            timersListSubcrafts!.IsVisible = visible;
            if (timersShiftShown != Shift)
            {
                timersShiftShown = Shift;
                timersList.String = GCLabel(false);
                timersListSubcrafts.String = GCLabel(true);
            }
        }

        private void DisposeWorkshop()
        {
            workshopPhase?.Dispose();
            workshopPhase = null;
            workshopPhasePrecrafts?.Dispose();
            workshopPhasePrecrafts = null;
        }

        private void DisposeSupply()
        {
            supplyList?.Dispose();
            supplyList = null;
            supplyListSubcrafts?.Dispose();
            supplyListSubcrafts = null;
        }

        private void DisposeTimers()
        {
            timersResized = false;
            timersShiftShown = null;
            timersList?.Dispose();
            timersList = null;
            timersListSubcrafts?.Dispose();
            timersListSubcrafts = null;
        }

        public void Dispose()
        {
            workshopController?.Dispose();
            workshopController = null;
            supplyController?.Dispose();
            supplyController = null;
            timersController?.Dispose();
            timersController = null;

            DisposeWorkshop();
            DisposeSupply();
            DisposeTimers();
        }
    }
}
