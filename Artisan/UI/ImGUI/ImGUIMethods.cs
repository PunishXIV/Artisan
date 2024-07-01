using ImGuiNET;

namespace Artisan.UI.ImGUI
{
    internal static class ImGUIMethods
    {
        public static bool FlippedInputInt(string id, ref int v)
        {
            ImGui.Text(id);
            ImGui.SameLine();
            return ImGui.InputInt($"###{id}", ref v, 0, 0);
        }

        public static bool FlippedCheckbox(string label, ref bool v)
        {
            ImGui.Text(label);
            ImGui.SameLine();
            return ImGui.Checkbox($"###{label}", ref v);
        }

        public static bool SliderInt(string label, ref int v, int v_min, int v_max, bool leftLabel = false)
        {
            if (leftLabel)
            {
                ImGui.Text($"{label}");
                ImGui.SameLine();
            }

            var ret = ImGui.SliderInt(leftLabel ? $"###{label}" : label, ref v, v_min, v_max);
            return ret;
        }

        public static bool InputIntBound(string label, ref int v, int v_min, int v_max, bool leftLabel = false)
        {
            if (leftLabel)
            {
                ImGui.Text($"{label}");
                ImGui.SameLine();
            }

            var ret = ImGui.InputInt(leftLabel ? $"###{label}" : label, ref v, 0, 0);
            if (v < v_min)
                v = v_min;

            if (v > v_max)
                v = v_max;

            return ret;
        }
    }
}
