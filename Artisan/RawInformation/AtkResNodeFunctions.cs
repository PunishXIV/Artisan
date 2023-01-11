using Artisan.Autocraft;
using ClickLib.Enums;
using ClickLib.Structures;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Artisan.RawInformation
{
    internal class AtkResNodeFunctions
    {
        public unsafe static void DrawOutline(AtkResNode* node)
        {
            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);

            position += ImGuiHelpers.MainViewport.Pos;

            ImGui.GetForegroundDrawList(ImGuiHelpers.MainViewport).AddRect(position, position + size, 0xFFFFFF00, 0, ImDrawFlags.RoundCornersAll, 8);
        }

        public unsafe static void DrawOptions(AtkResNode* node)
        {
            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);
            position += ImGuiHelpers.MainViewport.Pos;

            ImGuiHelpers.ForceNextWindowMainViewport();

            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X + size.Length() * 2, position.Y));

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(4f, 4f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
            ImGui.Begin($"###Options{node->NodeID}", ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);

            DrawCopyOfCraftMenu();

            ImGui.End();
            ImGui.PopStyleVar(2);
        }

        private static void DrawCopyOfCraftMenu()
        {
            bool autoMode = Service.Configuration.AutoMode;

            if (ImGui.Checkbox("Auto Mode", ref autoMode))
            {
                Service.Configuration.AutoMode = autoMode;
                Service.Configuration.Save();
            }

            //if (autoMode)
            //{
            //    var delay = Service.Configuration.AutoDelay;
            //    ImGui.PushItemWidth(200);
            //    if (ImGui.SliderInt("Set delay (ms)", ref delay, 0, 1000))
            //    {
            //        if (delay < 0) delay = 0;
            //        if (delay > 1000) delay = 1000;

            //        Service.Configuration.AutoDelay = delay;
            //        Service.Configuration.Save();
            //    }
            //}

            ImGui.Checkbox("Endurance Mode Toggle", ref Handler.Enable);

            bool macroMode = Service.Configuration.UseMacroMode;
            if (ImGui.Checkbox("Macro Mode", ref macroMode))
            {
                Service.Configuration.UseMacroMode = macroMode;
                Service.Configuration.Save();
            }

        }

        public unsafe static void DrawMacroOptions(AtkResNode* node)
        {
            if (Service.Configuration.UserMacros.Count == 0)
                return;

            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);
            position += ImGuiHelpers.MainViewport.Pos;

            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X + size.Length() * 2, position.Y));

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(4f, 4f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
            ImGui.Begin($"###Options{node->NodeID}", ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);

            string preview = Service.Configuration.IndividualMacros.TryGetValue((uint)Handler.RecipeID, out var prevMacro) && prevMacro != null ? Service.Configuration.IndividualMacros[(uint)Handler.RecipeID].Name : "";
            if (prevMacro is not null && !Service.Configuration.UserMacros.Where(x => x.ID == prevMacro.ID).Any())
            {
                preview = "";
                Service.Configuration.IndividualMacros[(uint)Handler.RecipeID] = null;
                Service.Configuration.Save();
            }

            ImGui.Spacing();
            ImGui.Text($"Use a macro for this recipe ({Handler.RecipeName})");
            if (ImGui.BeginCombo("", preview))
            {
                if (ImGui.Selectable(""))
                {
                    Service.Configuration.IndividualMacros.Remove((uint)Handler.RecipeID);
                    Service.Configuration.Save();
                }
                foreach (var macro in Service.Configuration.UserMacros)
                {
                    bool selected = Service.Configuration.IndividualMacros.TryGetValue((uint)Handler.RecipeID, out var selectedMacro) && selectedMacro != null;
                    if (ImGui.Selectable(macro.Name, selected))
                    {
                        Service.Configuration.IndividualMacros[(uint)Handler.RecipeID] = macro;
                        Service.Configuration.Save();
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.End();
            ImGui.PopStyleVar(2);
        }

        internal static unsafe void DrawEnduranceCounter(AtkResNode* node)
        {
            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);
            position += ImGuiHelpers.MainViewport.Pos;

            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X + size.Length() + 20, position.Y));

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(4f, 4f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
            ImGui.Begin($"###Options{node->NodeID}", ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);

            ImGui.Text("Craft X Times:");
            ImGui.SameLine();
            ImGui.PushItemWidth(100);
            if (ImGui.InputInt($"###TimesRepeat{node->NodeID}", ref Service.Configuration.CraftX))
            {
                if (Service.Configuration.CraftX < 0)
                    Service.Configuration.CraftX = 0;

            }
            ImGui.SameLine();
            if (ImGui.Button("Craft") && Service.Configuration.CraftX > 0)
            {
                Service.Configuration.CraftingX = true;
                Handler.Enable = true;
            }

            ImGui.End();
            ImGui.PopStyleVar(2);
        }

        public unsafe static void DrawSuccessRate(AtkResNode* node, string str, string itemName, bool isMainWindow = false)
        {
            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);

            position += ImGuiHelpers.MainViewport.Pos;

            ImGuiHelpers.ForceNextWindowMainViewport();
            var textSize = ImGui.CalcTextSize(str);
            if (isMainWindow)
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X + 5f, position.Y));
            else
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X, position.Y + (node->Height - textSize.Y)));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(2f, 0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
            ImGui.Begin($"###EHQ{itemName}{node->NodeID}", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);
            ImGui.TextUnformatted(str);
            ImGui.End();
            ImGui.PopStyleVar(2);
        }

        public static unsafe Vector2 GetNodePosition(AtkResNode* node)
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

        public static unsafe Vector2 GetNodeScale(AtkResNode* node)
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

        internal static unsafe void DrawQualitySlider(AtkResNode* node, string selectedCraftName)
        {
            if (!Service.Configuration.UseSimulatedStartingQuality) return;

            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);

            position += ImGuiHelpers.MainViewport.Pos;

            var sheetItem = LuminaSheets.RecipeSheet?.Values.Where(x => x.ItemResult.Value.Name!.RawString.Equals(selectedCraftName)).FirstOrDefault();
            if (sheetItem == null)
                return;

            var currentSimulated = Service.Configuration.CurrentSimulated;
            if (sheetItem.MaterialQualityFactor == 0) return;
            var maxFactor = sheetItem.MaterialQualityFactor == 0 ? 0 : Math.Floor((double)sheetItem.RecipeLevelTable.Value.Quality * ((double)sheetItem.MaterialQualityFactor / 100) * ((double)sheetItem.QualityFactor / 100));
            if (currentSimulated > (int)maxFactor)
                currentSimulated = (int)maxFactor;


            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X - 50f, position.Y + node->Height));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(2f, 0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
            ImGui.Begin($"###SliderQuality", ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar);
            var textSize = ImGui.CalcTextSize("Simulated Starting Quality");
            ImGui.TextUnformatted($"Simulated Starting Quality");
            ImGui.PushItemWidth(textSize.Length());
            if (ImGui.SliderInt("", ref currentSimulated, 0, (int)maxFactor))
            {
                Service.Configuration.CurrentSimulated = currentSimulated;
                Service.Configuration.Save();
            }
            ImGui.End();
            ImGui.PopStyleVar(2);
        }

        public static unsafe void ClickButton(AtkUnitBase* window, AtkComponentButton* target, uint which, EventType type = EventType.CHANGE)
            => ClickAddonComponent(window, target->AtkComponentBase.OwnerNode, which, type);

        public static unsafe void ClickAddonCheckBox(AtkUnitBase* window, AtkComponentCheckBox* target, uint which, EventType type = EventType.CHANGE)
             => ClickAddonComponent(window, target->AtkComponentButton.AtkComponentBase.OwnerNode, which, type);


        public static unsafe void ClickAddonComponent(AtkUnitBase* UnitBase, AtkComponentNode* target, uint which, EventType type, EventData? eventData = null, InputData? inputData = null)
        {
            eventData ??= EventData.ForNormalTarget(target, UnitBase);
            inputData ??= InputData.Empty();

            InvokeReceiveEvent(&UnitBase->AtkEventListener, type, which, eventData, inputData);
        }

        /// <summary>
        /// AtkUnitBase receive event delegate.
        /// </summary>
        /// <param name="eventListener">Type receiving the event.</param>
        /// <param name="evt">Event type.</param>
        /// <param name="which">Internal routing number.</param>
        /// <param name="eventData">Event data.</param>
        /// <param name="inputData">Keyboard and mouse data.</param>
        /// <returns>The addon address.</returns>
        internal unsafe delegate IntPtr ReceiveEventDelegate(AtkEventListener* eventListener, EventType evt, uint which, void* eventData, void* inputData);


        /// <summary>
        /// Invoke the receive event delegate.
        /// </summary>
        /// <param name="eventListener">Type receiving the event.</param>
        /// <param name="type">Event type.</param>
        /// <param name="which">Internal routing number.</param>
        /// <param name="eventData">Event data.</param>
        /// <param name="inputData">Keyboard and mouse data.</param>
        private static unsafe void InvokeReceiveEvent(AtkEventListener* eventListener, EventType type, uint which, EventData eventData, InputData inputData)
        {
            var receiveEvent = GetReceiveEvent(eventListener);
            receiveEvent(eventListener, type, which, eventData.Data, inputData.Data);
        }

        private static unsafe ReceiveEventDelegate GetReceiveEvent(AtkEventListener* listener)
        {
            var receiveEventAddress = new IntPtr(listener->vfunc[2]);
            return Marshal.GetDelegateForFunctionPointer<ReceiveEventDelegate>(receiveEventAddress)!;
        }

        private static unsafe ReceiveEventDelegate GetReceiveEvent(AtkComponentBase* listener)
            => GetReceiveEvent(&listener->AtkEventListener);

        private static unsafe ReceiveEventDelegate GetReceiveEvent(AtkUnitBase* listener)
            => GetReceiveEvent(&listener->AtkEventListener);
    }
}