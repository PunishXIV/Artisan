using ImGuiNET;
using System;
using System.Numerics;

namespace Artisan.UI.Tables
{
    public partial class Interface
    {
        public static Vector2 IconSize;
        public static Vector2 WeatherIconSize;
        public static Vector2 SmallIconSize;
        public static Vector2 LineIconSize;
        public static Vector2 ItemSpacing;
        public static Vector2 FramePadding;
        public static Vector2 IconButtonSize;
        public static float SelectorWidth;
        public static float SetInputWidth;
        public static Vector2 HorizontalSpace = Vector2.Zero;
        public static float TextHeight;
        public static float TextHeightSpacing;
        public static float Scale;

        public static void SetupValues()
        {
            Scale = ImGuiHelpers.GlobalScale;
            ItemSpacing = ImGui.GetStyle().ItemSpacing;
            FramePadding = ImGui.GetStyle().FramePadding;
            SelectorWidth = Math.Max(ImGui.GetWindowSize().X * 0.15f, 150 * Scale);
            SetInputWidth = 200f * ImGuiHelpers.GlobalScale;
            TextHeight = ImGui.GetTextLineHeight();
            TextHeightSpacing = ImGui.GetTextLineHeightWithSpacing();
            IconSize = ImGuiHelpers.ScaledVector2(40, 40);
            WeatherIconSize = ImGuiHelpers.ScaledVector2(30, 30);
            SmallIconSize = ImGuiHelpers.ScaledVector2(20, 20);
            LineIconSize = new Vector2(TextHeight, TextHeight);
            IconButtonSize = new Vector2(ImGui.GetFrameHeight(), 0);
        }

        private static float TextWidth(string text)
            => ImGui.CalcTextSize(text).X + ItemSpacing.X;
    }
}
