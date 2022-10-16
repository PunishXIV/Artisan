using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.ImGuiMethods
{
    [Obsolete("Deprecated", true)]
    public class ImGuiTrans
    {
        public static void PushStyleColor(ImGuiCol target, Vector4 value)
        {
            ImGui.PushStyleColor(target, value with { W = ImGui.GetStyle().Colors[(int)target].W });
        }

        public static void PushTransparency(float v)
        {
            foreach (var c in Enum.GetValues<ImGuiCol>())
            {
                if (c == ImGuiCol.COUNT) continue;
                var col = ImGui.GetStyle().Colors[(int)c];
                ImGui.PushStyleColor(c, col with { W = col.W * v });
            }
        }

        public static void PopTransparency()
        {
            ImGui.PopStyleColor(Enum.GetValues<ImGuiCol>().Length - 1);
        }

        public static void WithTextColor(Vector4 col, Action func)
        {
            ImGuiTrans.PushStyleColor(ImGuiCol.Text, col);
            GenericHelpers.Safe(func);
            ImGui.PopStyleColor();
        }

        public static void Text(Vector4 col, string s)
        {
            ImGuiTrans.PushStyleColor(ImGuiCol.Text, col);
            ImGui.TextUnformatted(s);
            ImGui.PopStyleColor();
        }

        public static void TextWrapped(Vector4 col, string s)
        {
            ImGui.PushTextWrapPos(0);
            ImGuiTrans.Text(col, s);
            ImGui.PopTextWrapPos();
        }
    }
}
