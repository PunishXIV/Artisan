using Dalamud.Interface;
using ImGuiNET;
using System.Numerics;

namespace PunishLib.ImGuiMethods
{
    public static class IconButtons
    {
        private static Vector2 GetIconSize(FontAwesomeIcon icon)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            var iconSize = ImGui.CalcTextSize(icon.ToIconString());
            ImGui.PopFont();
            return iconSize;
        }

        public static bool IconTextButton(FontAwesomeIcon icon, string text, Vector2 size = new(), bool iconOnRight = false)
        {
            var buttonClicked = false;

            var buttonSize = Vector2.Zero;
            var iconSize = GetIconSize(icon);
            var textSize = ImGui.CalcTextSize(text);
            var padding = ImGui.GetStyle().FramePadding;
            var spacing = ImGui.GetStyle().ItemSpacing;

            var buttonSizeX = iconSize.X + textSize.X + padding.X * 2 + spacing.X;
            var buttonSizeY = (iconSize.Y > textSize.Y ? iconSize.Y : textSize.Y) + padding.Y * 2;
            if (size == Vector2.Zero)
            {
                buttonSize = new Vector2(buttonSizeX, buttonSizeY);
            }
            else
            {
                buttonSize = size;
            }

            if (ImGui.Button("###" + icon.ToIconString() + text, buttonSize))
            {
                buttonClicked = true;
            }

            ImGui.SameLine();
            if (size == Vector2.Zero)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() - buttonSize.X - padding.X);
            }
            else
            {
                ImGui.SetCursorPosX((ImGui.GetContentRegionMax().X - textSize.X - iconSize.X) * 0.5f);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (padding.Y));
            }
            if (iconOnRight)
            {
                ImGui.Text(text);
                ImGui.SameLine();
                if (size != Vector2.Zero)
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (padding.Y));
                }
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Text(icon.ToIconString());
                ImGui.PopFont();
            }
            else
            {
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Text(icon.ToIconString());
                ImGui.PopFont();
                ImGui.SameLine();
                if (size != Vector2.Zero)
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (padding.Y));
                }
                ImGui.Text(text);
            }


            return buttonClicked;
        }
    }
}
