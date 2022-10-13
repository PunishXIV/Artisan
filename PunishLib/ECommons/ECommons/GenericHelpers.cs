using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.Logging;
using Dalamud.Utility;
using ECommons.ChatMethods;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ECommons
{
    public static unsafe class GenericHelpers
    {
        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        public static bool TryDecodeSender(SeString sender, out Sender senderStruct)
        {
            if (sender == null)
            {
                senderStruct = default;
                return false;
            }
            foreach (var x in sender.Payloads)
            {
                if (x is PlayerPayload p)
                {
                    senderStruct = new(p.PlayerName, p.World.RowId);
                    return true;
                }
            }
            senderStruct = default;
            return false;
        }

        public static bool IsAddonReady(AtkUnitBase* addon)
        {
            return addon->IsVisible && addon->UldManager.LoadedState == AtkLoadState.Loaded;
        }

        public static bool IsAddonReady(AtkComponentNode* addon)
        {
            return addon->AtkResNode.IsVisible && addon->Component->UldManager.LoadedState == AtkLoadState.Loaded;
        }

        public static string ExtractText(this Lumina.Text.SeString s, bool onlyFirst = false)
        {
            return s.ToDalamudString().ExtractText(onlyFirst);
        }

        public static string ExtractText(this SeString seStr, bool onlyFirst = false)
        {
            StringBuilder sb = new();
            foreach(var x in seStr.Payloads)
            {
                if(x is TextPayload tp)
                {
                    sb.Append(tp.Text);
                    if (onlyFirst) break;
                }
            }
            return sb.ToString();
        }

        public static bool StartsWithAny(this string source, IEnumerable<string> compareTo, StringComparison stringComparison = StringComparison.Ordinal)
        {
            foreach(var x in compareTo)
            {
                if (source.StartsWith(x, stringComparison)) return true;
            }
            return false;
        }

        public static SeStringBuilder Add(this SeStringBuilder b, IEnumerable<Payload> payloads)
        {
            foreach(var x in payloads)
            {
                b = b.Add(x);
            }
            return b;
        }

        public static bool Toggle<T>(this HashSet<T> hashSet, T value)
        {
            if (hashSet.Contains(value))
            {
                hashSet.Remove(value);
                return false;
            }
            else
            {
                hashSet.Add(value);
                return true;
            }
        }

        public static IEnumerable<string> Split(this string str, int chunkSize)
        {
            return Enumerable.Range(0, str.Length / chunkSize)
                .Select(i => str.Substring(i * chunkSize, chunkSize));
        }

        public static string GetTerritoryName(this uint terr)
        {
            var t = Svc.Data.GetExcelSheet<TerritoryType>().GetRow(terr);
            return $"{terr} | {t?.ContentFinderCondition.Value?.Name.ToString().Default(t?.PlaceName.Value?.Name.ToString())}";
        }

        public static T FirstOr0<T>(this IEnumerable<T> collection, Func<T, bool> predicate)
        {
            foreach(var x in collection)
            {
                if (predicate(x))
                {
                    return x;
                }
            }
            return collection.First();
        }

        public static string Default(this string s, string defaultValue)
        {
            if (string.IsNullOrEmpty(s)) return defaultValue;
            return s;
        }

        public static bool EqualsIgnoreCase(this string s, string other)
        {
            return s.Equals(other, StringComparison.OrdinalIgnoreCase);
        }

        public static string NullWhenEmpty(this string s)
        {
            return s == string.Empty ? null : s;
        }

        public static bool IsNullOrEmpty(this string s)
        {
            return string.IsNullOrEmpty(s);
        }

        public static IEnumerable<R> SelectMulti<T, R>(this IEnumerable<T> values, params Func<T, R>[] funcs)
        {
            foreach(var v in values)
            foreach(var x in funcs)
            {
                    yield return x(v);
            }
        }

        public static bool TryGetWorldByName(string world, out Lumina.Excel.GeneratedSheets.World worldId) 
        {
            if(Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.World>().TryGetFirst(x => x.Name.ToString().Equals(world, StringComparison.OrdinalIgnoreCase), out var w))
            {
                worldId = w;
                return true;
            }
            worldId = default;
            return false;
        }

        public static Vector4 Invert(this Vector4 v)
        {
            return v with { X = 1f - v.X, Y = 1f - v.Y, Z = 1f - v.Z };
        }

        public static uint ToUint(this Vector4 color)
        {
            return ImGui.ColorConvertFloat4ToU32(color);
        }

        public static Vector4 ToVector4(this uint color)
        {
            return ImGui.ColorConvertU32ToFloat4(color);
        }

        public static void ValidateRange(this ref int i, int min, int max)
        {
            if (i > max) i = max;
            if (i < min) i = min;
        }

        public static void ValidateRange(this ref float i, float min, float max)
        {
            if (i > max) i = max;
            if (i < min) i = min;
        }

        public static void Log(this Exception e)
        {
            PluginLog.Error($"{e.Message}\n{e.StackTrace ?? ""}");
        }

        public static void LogDuo(this Exception e)
        {
            DuoLog.Error($"{e.Message}\n{e.StackTrace ?? ""}");
        }

        public static bool IsNoConditions()
        {
            if (!Svc.Condition[ConditionFlag.NormalConditions]) return false;
            for(var i = 2; i < 100; i++)
            {
                if (i == (int)ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance) continue;
                if (Svc.Condition[i]) return false;
            }
            return true;
        }

        public static bool Invert(this bool b, bool invert)
        {
            return invert ? !b : b;
        }

        public static bool ContainsAll<T>(this IEnumerable<T> source, IEnumerable<T> values)
        {
            foreach(var x in values)
            {
                if (!source.Contains(x)) return false;
            }
            return true;
        }

        public static void ShellStart(string s)
        {
            Safe(delegate
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = s,
                    UseShellExecute = true
                });
            }, (e) =>
            {
                Notify.Error($"Could not open {s.Cut(60)}\n{e}");
            });
        }

        public static string Cut(this string s, int num)
        {
            if (s.Length <= num) return s;
            return s[0..num] + "...";
        }

        public static ushort GetParsedSeSetingColor(int percent)
        {
            if(percent < 25)
            {
                return 3;
            }
            else if(percent < 50)
            {
                return 45;
            }
            else if(percent < 75)
            {
                return 37;
            }
            else if(percent < 95)
            {
                return 541;
            }
            else if(percent < 99)
            {
                return 500;
            }
            else if (percent == 99)
            {
                return 561;
            }
            else if (percent == 100)
            {
                return 573;
            }
            else
            {
                return 518;
            }
        }

        public static string Repeat(this string s, int num)
        {
            StringBuilder str = new();
            for(var i = 0; i < num; i++)
            {
                str.Append(s);
            }
            return str.ToString();
        }

        public static string Join(this IEnumerable<string> e, string separator)
        {
            return string.Join(separator, e);
        }

        public static void Safe(System.Action a, bool suppressErrors = false)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                if (!suppressErrors) PluginLog.Error($"{e.Message}\n{e.StackTrace ?? ""}");
            }
        }

        public static void Safe(System.Action a, Action<string, object[]> logAction)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                logAction($"{e.Message}\n{e.StackTrace ?? ""}", Array.Empty<object>());
            }
        }

        public static void Safe(System.Action a, Action<string> fail, bool suppressErrors = false)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                try
                {
                    fail(e.Message);
                }
                catch(Exception ex)
                {
                    PluginLog.Error("Error while trying to process error handler:");
                    PluginLog.Error($"{ex.Message}\n{ex.StackTrace ?? ""}");
                    suppressErrors = false;
                }
                if (!suppressErrors) PluginLog.Error($"{e.Message}\n{e.StackTrace ?? ""}");
            }
        }

        public static bool TryExecute(System.Action a)
        {
            try
            {
                a();
                return true;
            }
            catch (Exception e)
            {
                PluginLog.Error($"{e.Message}\n{e.StackTrace ?? ""}");
                return false;
            }
        }

        public static bool TryExecute<T>(Func<T> a, out T result)
        {
            try
            {
                result = a();
                return true;
            }
            catch (Exception e)
            {
                PluginLog.Error($"{e.Message}\n{e.StackTrace ?? ""}");
                result = default;
                return false;
            }
        }

        public static bool ContainsAny<T>(this IEnumerable<T> obj, params T[] values)
        {
            foreach (var x in values)
            {
                if (obj.Contains(x))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool ContainsAny<T>(this IEnumerable<T> obj, IEnumerable<T> values)
        {
            foreach (var x in values)
            {
                if (obj.Contains(x))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool ContainsAny(this string obj, params string[] values)
        {
            foreach (var x in values)
            {
                if (obj.Contains(x))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool ContainsAny(this string obj, StringComparison comp, params string[] values)
        {
            foreach (var x in values)
            {
                if (obj.Contains(x, comp))
                {
                    return true;
                }
            }
            return false;
        }


        public static bool EqualsAny<T>(this T obj, params T[] values)
        {
            return values.Any(x => x.Equals(obj));
        }


        public static bool EqualsIgnoreCaseAny(this string obj, params string[] values)
        {
            return values.Any(x => x.Equals(obj, StringComparison.OrdinalIgnoreCase));
        }

        public static bool EqualsAny<T>(this T obj, IEnumerable<T> values)
        {
            return values.Any(x => x.Equals(obj));
        }

        public static IEnumerable<K> FindKeysByValue<K, V>(this IDictionary<K, V> dictionary, V value)
        {
            foreach(var x in dictionary)
            {
                if (value.Equals(x.Value))
                {
                    yield return x.Key;
                }
            }
        }

        public static bool TryGetFirst<K, V>(this IDictionary<K, V> dictionary, Func<KeyValuePair<K, V>, bool> predicate, out KeyValuePair<K, V> keyValuePair)
        {
            try
            {
                keyValuePair = dictionary.First(predicate);
                return true;
            }
            catch(Exception)
            {
                keyValuePair = default;
                return false;
            }
        }

        public static bool TryGetFirst<K>(this IEnumerable<K> enumerable, Func<K, bool> predicate, out K value)
        {
            try
            {
                value = enumerable.First(predicate);
                return true;
            }
            catch (Exception)
            {
                value = default;
                return false;
            }
        }

        public static bool TryGetLast<K>(this IEnumerable<K> enumerable, Func<K, bool> predicate, out K value)
        {
            try
            {
                value = enumerable.Last(predicate);
                return true;
            }
            catch (Exception)
            {
                value = default;
                return false;
            }
        }

        public static bool TryGetAddonByName<T>(string Addon, out T* AddonPtr) where T : unmanaged
        {
            var a = Svc.GameGui.GetAddonByName(Addon, 1);
            if (a == IntPtr.Zero)
            {
                AddonPtr = null;
                return false;
            }
            else
            {
                AddonPtr = (T*)a;
                return true;
            }
        }

        public static bool IsSelectItemEnabled(AtkTextNode* textNodePtr)
        {
            var col = textNodePtr->TextColor;
            //EEE1C5FF
            return (col.A == 0xFF && col.R == 0xEE && col.G == 0xE1 && col.B == 0xC5)
                //7D523BFF
                || (col.A == 0xFF && col.R == 0x7D && col.G == 0x52 && col.B == 0x3B)
                || (col.A == 0xFF && col.R == 0xFF && col.G == 0xFF && col.B == 0xFF);
        }
    }
}
