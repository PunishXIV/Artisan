using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Internal.Notifications;
using ECommons.DalamudServices;
using ECommons.Reflection;
using ImGuiNET;

namespace ECommons.ImGuiMethods
{
    public static class ImGuiEx
    {
        public static bool IsKeyPressed(int key, bool repeat)
        {
            byte repeat2 = (byte)(repeat ? 1 : 0);
            return ImGuiNative.igIsKeyPressed((ImGuiKey)key, repeat2) != 0;
        }

        public static void TextUnderlined(uint color, string text)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            TextUnderlined(text);
            ImGui.PopStyleColor();
        }

        public static void TextUnderlined(Vector4 color, string text)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            TextUnderlined(text);
            ImGui.PopStyleColor();
        }

        public static void TextUnderlined(string text)
        {
            var size = ImGui.CalcTextSize(text);
            var cur = ImGui.GetCursorScreenPos();
            cur.Y += size.Y;
            ImGui.GetForegroundDrawList().PathLineTo(cur);
            cur.X += size.X;
            ImGui.GetForegroundDrawList().PathLineTo(cur);
            ImGui.GetForegroundDrawList().PathStroke(ImGuiColors.DalamudWhite.ToUint());
            ImGuiEx.Text(text);
        }

        public static float GetWindowContentRegionWidth()
        {
            return ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
        }

        public static void Spacing(float pix = 10f, bool accountForScale = true)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (accountForScale ? pix : pix * ImGuiHelpers.GlobalScale));
        }

        public static float Scale(this float f)
        {
            return f * ImGuiHelpers.GlobalScale;
        }

        public static void SetTooltip(string text)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(text);
            ImGui.EndTooltip();
        }

        static readonly Dictionary<string, float> CenteredLineWidths = new();
        public static void ImGuiLineCentered(string id, Action func)
        {
            if (CenteredLineWidths.TryGetValue(id, out var dims))
            {
                ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X / 2 - dims / 2);
            }
            var oldCur = ImGui.GetCursorPosX();
            func();
            ImGui.SameLine(0, 0);
            CenteredLineWidths[id] = ImGui.GetCursorPosX() - oldCur;
            ImGui.Dummy(Vector2.Zero);
        }

        public static void SetNextItemFullWidth(int mod = 0)
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X + mod);
        }

        static Dictionary<string, float> InputWithRightButtonsAreaValues = new();
        public static void InputWithRightButtonsArea(string id, Action inputAction, Action rightAction)
        {
            if (InputWithRightButtonsAreaValues.ContainsKey(id))
            {
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - InputWithRightButtonsAreaValues[id]); 
            }
            inputAction();
            ImGui.SameLine();
            var cur1 = ImGui.GetCursorPosX();
            rightAction();
            ImGui.SameLine(0, 0);
            InputWithRightButtonsAreaValues[id] = ImGui.GetCursorPosX() - cur1 + ImGui.GetStyle().ItemSpacing.X;
            ImGui.Dummy(Vector2.Zero);
        }

        static Dictionary<string, Box<string>> InputListValuesString = new();
        public static void InputListString(string name, List<string> list, Dictionary<string, string> overrideValues = null)
        {
            if (!InputListValuesString.ContainsKey(name)) InputListValuesString[name] = new("");
            InputList(name, list, overrideValues, delegate
            {
                var buttonSize = ImGuiHelpers.GetButtonSize("Add");
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - buttonSize.X - ImGui.GetStyle().ItemSpacing.X);
                ImGui.InputText($"##{name.Replace("#", "_")}", ref InputListValuesString[name].Value, 100);
                ImGui.SameLine();
                if (ImGui.Button("Add"))
                {
                    list.Add(InputListValuesString[name].Value);
                    InputListValuesString[name].Value = "";
                }
            });
        }

        static Dictionary<string, Box<uint>> InputListValuesUint = new();
        public static void InputListUint(string name, List<uint> list, Dictionary<uint, string> overrideValues = null)
        {
            if (!InputListValuesUint.ContainsKey(name)) InputListValuesUint[name] = new(0);
            InputList(name, list, overrideValues, delegate
            {
                var buttonSize = ImGuiHelpers.GetButtonSize("Add");
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - buttonSize.X - ImGui.GetStyle().ItemSpacing.X);
                ImGuiEx.InputUint($"##{name.Replace("#", "_")}", ref InputListValuesUint[name].Value);
                ImGui.SameLine();
                if (ImGui.Button("Add"))
                {
                    list.Add(InputListValuesUint[name].Value);
                    InputListValuesUint[name].Value = 0;
                }
            });
        }

        public static void InputList<T>(string name, List<T> list, Dictionary<T, string> overrideValues, Action addFunction)
        {
            var text = list.Count == 0 ? "- No values -" : (list.Count == 1 ? $"{(overrideValues != null && overrideValues.ContainsKey(list[0]) ? overrideValues[list[0]] : list[0])}" : $"- {list.Count} elements -");
            if(ImGui.BeginCombo(name, text))
            {
                addFunction();
                var rem = -1;
                for (var i = 0;i<list.Count;i++)
                {
                    var id = $"{name}ECommonsDeleItem{i}";
                    var x = list[i];
                    ImGui.Selectable($"{(overrideValues != null && overrideValues.ContainsKey(x) ? overrideValues[x]:x)}");
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    {
                        ImGui.OpenPopup(id);
                    }
                    if (ImGui.BeginPopup(id))
                    {
                        if (ImGui.Selectable("Delete##ECommonsDeleItem"))
                        {
                            rem = i;
                        }
                        if (ImGui.Selectable("Clear (hold shift+ctrl)##ECommonsDeleItem")
                            && ImGui.GetIO().KeyShift && ImGui.GetIO().KeyCtrl)
                        {
                            rem = -2;
                        }
                        ImGui.EndPopup();
                    }
                }
                if(rem > -1)
                {
                    list.RemoveAt(rem);
                }
                if(rem == -2)
                {
                    list.Clear();
                }
                ImGui.EndCombo();
            }
        }

        public static void WithTextColor(Vector4 col, Action func)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, col);
            GenericHelpers.Safe(func);
            ImGui.PopStyleColor();
        }

        public static void Tooltip(string s)
        {
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(s);
            }
        }

        public static void TextV(string s)
        {
            var cur = ImGui.GetCursorPos();
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0);
            ImGui.Button("");
            ImGui.PopStyleVar();
            ImGui.SameLine();
            ImGui.SetCursorPos(cur);
            ImGui.TextUnformatted(s);
        }

        public static void Text(string s)
        {
            ImGui.TextUnformatted(s);
        }

        public static void Text(Vector4 col, string s)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, col);
            ImGui.TextUnformatted(s);
            ImGui.PopStyleColor();
        }

        public static void Text(uint col, string s)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, col);
            ImGui.TextUnformatted(s);
            ImGui.PopStyleColor();
        }

        public static void TextWrapped(string s)
        {
            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(s);
            ImGui.PopTextWrapPos();
        }

        public static void TextWrapped(Vector4 col, string s)
        {
            ImGui.PushTextWrapPos(0);
            ImGuiEx.Text(col, s);
            ImGui.PopTextWrapPos();
        }

        public static void TextWrapped(uint col, string s)
        {
            ImGui.PushTextWrapPos();
            ImGuiEx.Text(col, s);
            ImGui.PopTextWrapPos();
        }

        public static Vector4 GetParsedColor(int percent)
        {
            if (percent < 25)
            {
                return ImGuiColors.ParsedGrey;
            }
            else if (percent < 50)
            {
                return ImGuiColors.ParsedGreen;
            }
            else if (percent < 75)
            {
                return ImGuiColors.ParsedBlue;
            }
            else if (percent < 95)
            {
                return ImGuiColors.ParsedPurple;
            }
            else if (percent < 99)
            {
                return ImGuiColors.ParsedOrange;
            }
            else if (percent == 99)
            {
                return ImGuiColors.ParsedPink;
            }
            else if (percent == 100)
            {
                return ImGuiColors.ParsedGold;
            }
            else
            {
                return ImGuiColors.DalamudRed;
            }
        }

        public static void EzTabBar(string id, params (string name, Action function, Vector4? color, bool child)[] tabs)
        {
            ImGui.BeginTabBar(id);
            foreach(var x in tabs)
            {
                if (x.name == null) continue;
                if(x.color != null)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, x.color.Value);
                }
                if (ImGui.BeginTabItem(x.name))
                {
                    if (x.color != null)
                    {
                        ImGui.PopStyleColor();
                    }
                    if(x.child) ImGui.BeginChild(x.name + "child");
                    x.function();
                    if(x.child) ImGui.EndChild();
                    ImGui.EndTabItem();
                }
                else
                {
                    if (x.color != null)
                    {
                        ImGui.PopStyleColor();
                    }
                }
            }
            ImGui.EndTabBar();
        }
        
        public static void InvisibleButton(int width = 0)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0);
            ImGui.Button(" ");
            ImGui.PopStyleVar();
        }

        public static Dictionary<string, Box<string>> EnumComboSearch = new();
        public static void EnumCombo<T>(string name, ref T refConfigField, Dictionary<T, string> names) where T:IConvertible
        {
            EnumCombo(name, ref refConfigField, null, names);
        }

        public static bool EnumCombo<T>(string name, ref T refConfigField, Func<T, bool> filter = null, Dictionary<T, string> names = null) where T : IConvertible
        {
            var ret = false;
            if (ImGui.BeginCombo(name, (names != null && names.TryGetValue(refConfigField, out var n)) ? n : refConfigField.ToString().Replace("_", " ")))
            {
                var values = Enum.GetValues(typeof(T));
                Box<string> fltr = null;
                if (values.Length > 10)
                {
                    if (!EnumComboSearch.ContainsKey(name)) EnumComboSearch.Add(name, new(""));
                    fltr = EnumComboSearch[name];
                    ImGuiEx.SetNextItemFullWidth();
                    ImGui.InputTextWithHint($"##{name.Replace("#", "_")}", "Filter...", ref fltr.Value, 50);
                }
                foreach(var x in values)
                {
                    var equals = EqualityComparer<T>.Default.Equals((T)x, refConfigField);
                    var element = (names != null && names.TryGetValue((T)x, out n)) ? n : x.ToString().Replace("_", " ");
                    if ((filter == null || filter((T)x))
                        && (fltr == null || element.Contains(fltr.Value, StringComparison.OrdinalIgnoreCase))
                        && ImGui.Selectable(element, equals)
                        )
                    {
                        ret = true;
                        refConfigField = (T)x;
                    }
                    if (ImGui.IsWindowAppearing() && equals) ImGui.SetScrollHereY();
                }
                ImGui.EndCombo();
            }
            return ret;
        }

        public static Dictionary<string, Box<string>> ComboSearch = new();
        public static bool Combo<T>(string name, ref T refConfigField, IEnumerable<T> values, Func<T, bool> filter = null, Dictionary<T, string> names = null)
        {
            var ret = false;
            if (ImGui.BeginCombo(name, (names != null && names.TryGetValue(refConfigField, out var n)) ? n : refConfigField.ToString()))
            {
                Box<string> fltr = null;
                if (values.Count() > 10)
                {
                    if (!ComboSearch.ContainsKey(name)) ComboSearch.Add(name, new(""));
                    fltr = ComboSearch[name];
                    ImGuiEx.SetNextItemFullWidth();
                    ImGui.InputTextWithHint($"##{name}fltr", "Filter...", ref fltr.Value, 50);
                }
                foreach (var x in values)
                {
                    var equals = EqualityComparer<T>.Default.Equals(x, refConfigField);
                    var element = (names != null && names.TryGetValue(x, out n)) ? n : x.ToString();
                    if ((filter == null || filter(x))
                        && (fltr == null || element.Contains(fltr.Value, StringComparison.OrdinalIgnoreCase))
                        && ImGui.Selectable(element, equals)
                        )
                    {
                        ret = true;
                        refConfigField = x;
                    }
                    if (ImGui.IsWindowAppearing() && equals) ImGui.SetScrollHereY();
                }
                ImGui.EndCombo();
            }
            return ret;
        }

        public static bool IconButton(FontAwesomeIcon icon, string id = "ECommonsButton")
        {
            ImGui.PushFont(UiBuilder.IconFont);
            var result = ImGui.Button($"{icon.ToIconString()}##{icon.ToIconString()}-{id}");
            ImGui.PopFont();
            return result;
        }

        public static Vector2 CalcIconSize(FontAwesomeIcon icon)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            var result = ImGui.CalcTextSize($"{icon.ToIconString()}");
            ImGui.PopFont();
            return result;
        }

        public static float Measure(Action func, bool includeSpacing = true)
        {
            var pos = ImGui.GetCursorPosX();
            func();
            ImGui.SameLine(0, 0);
            var diff = ImGui.GetCursorPosX() - pos;
            ImGui.Dummy(Vector2.Zero);
            return diff + (includeSpacing?ImGui.GetStyle().ItemSpacing.X:0);
        }

        public static void InputHex(string name, ref uint hexInt)
        {
            var text = $"{hexInt:X}";
            if (ImGui.InputText(name, ref text, 8))
            {
                if (uint.TryParse(text.Replace("0x", ""), NumberStyles.HexNumber, null, out var num))
                {
                    hexInt = num;
                }
            }
        }

        public static void InputHex(string name, ref byte hexByte)
        {
            var text = $"{hexByte:X}";
            if (ImGui.InputText(name, ref text, 2))
            {
                if (byte.TryParse(text, NumberStyles.HexNumber, null, out var num))
                {
                    hexByte = num;
                }
            }
        }

        public static void InputUint(string name, ref uint uInt)
        {
            var text = $"{uInt}";
            if (ImGui.InputText(name, ref text, 16))
            {
                if (uint.TryParse(text, out var num))
                {
                    uInt = num;
                }
            }
        }

        public static void TextCopy(string text)
        {
            ImGui.TextUnformatted(text);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                ImGui.SetClipboardText(text);
                Svc.PluginInterface.UiBuilder.AddNotification("Text copied to clipboard", null, NotificationType.Success);
            }
        }

        public static void TextWrappedCopy(string text)
        {
            ImGuiEx.TextWrapped(text);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                ImGui.SetClipboardText(text);
                Svc.PluginInterface.UiBuilder.AddNotification("Text copied to clipboard", DalamudReflector.GetPluginName(), NotificationType.Success);
            }
        }

        public static void TextWrappedCopy(Vector4 col, string text)
        {
            ImGuiEx.TextWrapped(col, text);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                ImGui.SetClipboardText(text);
                Svc.PluginInterface.UiBuilder.AddNotification("Text copied to clipboard", DalamudReflector.GetPluginName(), NotificationType.Success);
            }
        }

        public static void ButtonCopy(string buttonText, string copy)
        {
            if(ImGui.Button(buttonText.Replace("$COPY", copy)))
            {
                ImGui.SetClipboardText(copy);
                Svc.PluginInterface.UiBuilder.AddNotification("Text copied to clipboard", null, NotificationType.Success);
            }
        }
    }
}
