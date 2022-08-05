using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;

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

        public unsafe static void DrawSuccessRate(AtkResNode* node, string str, string itemName)
        {
            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);

            position += ImGuiHelpers.MainViewport.Pos;

            ImGuiHelpers.ForceNextWindowMainViewport();
            var textSize = ImGui.CalcTextSize(str);
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
            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            var center = new Vector2((position.X + size.X) / 2, (position.Y - size.Y) / 2);

            position += ImGuiHelpers.MainViewport.Pos;

            var sheetItem = LuminaSheets.RecipeSheet?.Values.Where(x => x.ItemResult.Value.Name!.RawString.Equals(selectedCraftName)).FirstOrDefault();
            if (sheetItem == null)
                return;

            var currentSimulated = Service.Configuration.CurrentSimulated;

            var maxFactor = sheetItem.MaterialQualityFactor == 0 ? 0 : Math.Floor((double)sheetItem.RecipeLevelTable.Value.Quality * ((double)sheetItem.MaterialQualityFactor / 100));
            if (currentSimulated > (int)maxFactor)
                currentSimulated = (int)maxFactor;


            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(position.X, position.Y + node->Height));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(2f, 0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
            ImGui.Begin($"###SliderQuality", ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.AlwaysAutoResize  | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar);
            ImGui.TextUnformatted("Simulated Starting Quality");
            if (ImGui.SliderInt("", ref currentSimulated, 0, (int)maxFactor))
            {
                Service.Configuration.CurrentSimulated = currentSimulated;
                Service.Configuration.Save();
            }
            ImGui.End();
            ImGui.PopStyleVar(2);
        }
    }
}