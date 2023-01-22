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
using FFXIVClientStructs.FFXIV.Client.System.String;
using static System.Net.Mime.MediaTypeNames;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

        public static string ExtractText(this Utf8String s, bool onlyFirst = false)
        {
            var str = ReadSeString(&s);
            return str.ExtractText(false);
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

        #region Read

        /// <summary>
        /// Reads a generic type from a specified memory address.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <returns>The read in struct.</returns>
        public static T Read<T>(IntPtr memoryAddress) where T : unmanaged
            => Read<T>(memoryAddress, false);

        /// <summary>
        /// Reads a generic type from a specified memory address.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="marshal">Set this to true to enable struct marshalling.</param>
        /// <returns>The read in struct.</returns>
        public static T Read<T>(IntPtr memoryAddress, bool marshal)
        {
            return marshal
                ? Marshal.PtrToStructure<T>(memoryAddress)
                : Unsafe.Read<T>((void*)memoryAddress);
        }

        /// <summary>
        /// Reads a byte array from a specified memory address.
        /// </summary>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="length">The amount of bytes to read starting from the memoryAddress.</param>
        /// <returns>The read in byte array.</returns>
        public static byte[] ReadRaw(IntPtr memoryAddress, int length)
        {
            var value = new byte[length];
            Marshal.Copy(memoryAddress, value, 0, value.Length);
            return value;
        }

        /// <summary>
        /// Reads a generic type array from a specified memory address.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="arrayLength">The amount of array items to read.</param>
        /// <returns>The read in struct array.</returns>
        public static T[] Read<T>(IntPtr memoryAddress, int arrayLength) where T : unmanaged
            => Read<T>(memoryAddress, arrayLength, false);

        /// <summary>
        /// Reads a generic type array from a specified memory address.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="arrayLength">The amount of array items to read.</param>
        /// <param name="marshal">Set this to true to enable struct marshalling.</param>
        /// <returns>The read in struct array.</returns>
        public static T[] Read<T>(IntPtr memoryAddress, int arrayLength, bool marshal)
        {
            var structSize = SizeOf<T>(marshal);
            var value = new T[arrayLength];

            for (var i = 0; i < arrayLength; i++)
            {
                var address = memoryAddress + (structSize * i);
                Read(address, out T result, marshal);
                value[i] = result;
            }

            return value;
        }

        /// <summary>
        /// Reads a null-terminated byte array from a specified memory address.
        /// </summary>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <returns>The read in byte array.</returns>
        public static unsafe byte[] ReadRawNullTerminated(IntPtr memoryAddress)
        {
            var byteCount = 0;
            while (*(byte*)(memoryAddress + byteCount) != 0x00)
            {
                byteCount++;
            }

            return ReadRaw(memoryAddress, byteCount);
        }

        #endregion

        #region Read(out)

        /// <summary>
        /// Reads a generic type from a specified memory address.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="value">Local variable to receive the read in struct.</param>
        public static void Read<T>(IntPtr memoryAddress, out T value) where T : unmanaged
            => value = Read<T>(memoryAddress);

        /// <summary>
        /// Reads a generic type from a specified memory address.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="value">Local variable to receive the read in struct.</param>
        /// <param name="marshal">Set this to true to enable struct marshalling.</param>
        public static void Read<T>(IntPtr memoryAddress, out T value, bool marshal)
            => value = Read<T>(memoryAddress, marshal);

        /// <summary>
        /// Reads raw data from a specified memory address.
        /// </summary>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="length">The amount of bytes to read starting from the memoryAddress.</param>
        /// <param name="value">Local variable to receive the read in bytes.</param>
        public static void ReadRaw(IntPtr memoryAddress, int length, out byte[] value)
            => value = ReadRaw(memoryAddress, length);

        /// <summary>
        /// Reads a generic type array from a specified memory address.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="arrayLength">The amount of array items to read.</param>
        /// <param name="value">The read in struct array.</param>
        public static void Read<T>(IntPtr memoryAddress, int arrayLength, out T[] value) where T : unmanaged
            => value = Read<T>(memoryAddress, arrayLength);

        /// <summary>
        /// Reads a generic type array from a specified memory address.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="arrayLength">The amount of array items to read.</param>
        /// <param name="marshal">Set this to true to enable struct marshalling.</param>
        /// <param name="value">The read in struct array.</param>
        public static void Read<T>(IntPtr memoryAddress, int arrayLength, bool marshal, out T[] value)
            => value = Read<T>(memoryAddress, arrayLength, marshal);

        #endregion

        #region ReadString

        /// <summary>
        /// Read a UTF-8 encoded string from a specified memory address.
        /// </summary>
        /// <remarks>
        /// Attention! If this is an SeString, use the <see cref="SeStringManager"/> to decode or the applicable helper method.
        /// </remarks>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <returns>The read in string.</returns>
        public static string ReadStringNullTerminated(IntPtr memoryAddress)
            => ReadStringNullTerminated(memoryAddress, Encoding.UTF8);

        /// <summary>
        /// Read a string with the given encoding from a specified memory address.
        /// </summary>
        /// <remarks>
        /// Attention! If this is an SeString, use the <see cref="SeStringManager"/> to decode or the applicable helper method.
        /// </remarks>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="encoding">The encoding to use to decode the string.</param>
        /// <returns>The read in string.</returns>
        public static string ReadStringNullTerminated(IntPtr memoryAddress, Encoding encoding)
        {
            var buffer = ReadRawNullTerminated(memoryAddress);
            return encoding.GetString(buffer);
        }

        /// <summary>
        /// Read a UTF-8 encoded string from a specified memory address.
        /// </summary>
        /// <remarks>
        /// Attention! If this is an SeString, use the <see cref="SeStringManager"/> to decode or the applicable helper method.
        /// </remarks>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="maxLength">The maximum length of the string.</param>
        /// <returns>The read in string.</returns>
        public static string ReadString(IntPtr memoryAddress, int maxLength)
            => ReadString(memoryAddress, Encoding.UTF8, maxLength);

        /// <summary>
        /// Read a string with the given encoding from a specified memory address.
        /// </summary>
        /// <remarks>
        /// Attention! If this is an SeString, use the <see cref="SeStringManager"/> to decode or the applicable helper method.
        /// </remarks>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="encoding">The encoding to use to decode the string.</param>
        /// <param name="maxLength">The maximum length of the string.</param>
        /// <returns>The read in string.</returns>
        public static string ReadString(IntPtr memoryAddress, Encoding encoding, int maxLength)
        {
            if (maxLength <= 0)
                return string.Empty;

            ReadRaw(memoryAddress, maxLength, out var buffer);

            var data = encoding.GetString(buffer);
            var eosPos = data.IndexOf('\0');
            return eosPos >= 0 ? data.Substring(0, eosPos) : data;
        }

        /// <summary>
        /// Read a null-terminated SeString from a specified memory address.
        /// </summary>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <returns>The read in string.</returns>
        public static SeString ReadSeStringNullTerminated(IntPtr memoryAddress)
        {
            var buffer = ReadRawNullTerminated(memoryAddress);
            return SeString.Parse(buffer);
        }

        /// <summary>
        /// Read an SeString from a specified memory address.
        /// </summary>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="maxLength">The maximum length of the string.</param>
        /// <returns>The read in string.</returns>
        public static SeString ReadSeString(IntPtr memoryAddress, int maxLength)
        {
            ReadRaw(memoryAddress, maxLength, out var buffer);

            var eos = Array.IndexOf(buffer, (byte)0);
            if (eos < 0)
            {
                return SeString.Parse(buffer);
            }
            else
            {
                var newBuffer = new byte[eos];
                Buffer.BlockCopy(buffer, 0, newBuffer, 0, eos);
                return SeString.Parse(newBuffer);
            }
        }

        /// <summary>
        /// Read an SeString from a specified Utf8String structure.
        /// </summary>
        /// <param name="utf8String">The memory address to read from.</param>
        /// <returns>The read in string.</returns>
        public static unsafe SeString ReadSeString(Utf8String* utf8String)
        {
            if (utf8String == null)
                return string.Empty;

            var ptr = utf8String->StringPtr;
            if (ptr == null)
                return string.Empty;

            var len = Math.Max(utf8String->BufUsed, utf8String->StringLength);

            return ReadSeString((IntPtr)ptr, (int)len);
        }

        #endregion

        #region ReadString(out)

        /// <summary>
        /// Read a UTF-8 encoded string from a specified memory address.
        /// </summary>
        /// <remarks>
        /// Attention! If this is an SeString, use the <see cref="SeStringManager"/> to decode or the applicable helper method.
        /// </remarks>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="value">The read in string.</param>
        public static void ReadStringNullTerminated(IntPtr memoryAddress, out string value)
            => value = ReadStringNullTerminated(memoryAddress);

        /// <summary>
        /// Read a string with the given encoding from a specified memory address.
        /// </summary>
        /// <remarks>
        /// Attention! If this is an SeString, use the <see cref="SeStringManager"/> to decode or the applicable helper method.
        /// </remarks>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="encoding">The encoding to use to decode the string.</param>
        /// <param name="value">The read in string.</param>
        public static void ReadStringNullTerminated(IntPtr memoryAddress, Encoding encoding, out string value)
            => value = ReadStringNullTerminated(memoryAddress, encoding);

        /// <summary>
        /// Read a UTF-8 encoded string from a specified memory address.
        /// </summary>
        /// <remarks>
        /// Attention! If this is an SeString, use the <see cref="SeStringManager"/> to decode or the applicable helper method.
        /// </remarks>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="value">The read in string.</param>
        /// <param name="maxLength">The maximum length of the string.</param>
        public static void ReadString(IntPtr memoryAddress, out string value, int maxLength)
            => value = ReadString(memoryAddress, maxLength);

        /// <summary>
        /// Read a string with the given encoding from a specified memory address.
        /// </summary>
        /// <remarks>
        /// Attention! If this is an SeString, use the <see cref="SeStringManager"/> to decode or the applicable helper method.
        /// </remarks>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="encoding">The encoding to use to decode the string.</param>
        /// <param name="maxLength">The maximum length of the string.</param>
        /// <param name="value">The read in string.</param>
        public static void ReadString(IntPtr memoryAddress, Encoding encoding, int maxLength, out string value)
            => value = ReadString(memoryAddress, encoding, maxLength);

        /// <summary>
        /// Read a null-terminated SeString from a specified memory address.
        /// </summary>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="value">The read in SeString.</param>
        public static void ReadSeStringNullTerminated(IntPtr memoryAddress, out SeString value)
            => value = ReadSeStringNullTerminated(memoryAddress);

        /// <summary>
        /// Read an SeString from a specified memory address.
        /// </summary>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="maxLength">The maximum length of the string.</param>
        /// <param name="value">The read in SeString.</param>
        public static void ReadSeString(IntPtr memoryAddress, int maxLength, out SeString value)
            => value = ReadSeString(memoryAddress, maxLength);

        /// <summary>
        /// Read an SeString from a specified Utf8String structure.
        /// </summary>
        /// <param name="utf8String">The memory address to read from.</param>
        /// <param name="value">The read in string.</param>
        public static unsafe void ReadSeString(Utf8String* utf8String, out SeString value)
            => value = ReadSeString(utf8String);

        #endregion

        #region Write

        /// <summary>
        /// Writes a generic type to a specified memory address.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="item">The item to write to the address.</param>
        public static void Write<T>(IntPtr memoryAddress, T item) where T : unmanaged
            => Write(memoryAddress, item);

        /// <summary>
        /// Writes a generic type to a specified memory address.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="item">The item to write to the address.</param>
        /// <param name="marshal">Set this to true to enable struct marshalling.</param>
        public static void Write<T>(IntPtr memoryAddress, T item, bool marshal)
        {
            if (marshal)
                Marshal.StructureToPtr(item, memoryAddress, false);
            else
                Unsafe.Write((void*)memoryAddress, item);
        }

        /// <summary>
        /// Writes raw data to a specified memory address.
        /// </summary>
        /// <param name="memoryAddress">The memory address to read from.</param>
        /// <param name="data">The bytes to write to memoryAddress.</param>
        public static void WriteRaw(IntPtr memoryAddress, byte[] data)
        {
            Marshal.Copy(data, 0, memoryAddress, data.Length);
        }

        /// <summary>
        /// Writes a generic type array to a specified memory address.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="memoryAddress">The memory address to write to.</param>
        /// <param name="items">The array of items to write to the address.</param>
        public static void Write<T>(IntPtr memoryAddress, T[] items) where T : unmanaged
            => Write(memoryAddress, items, false);

        /// <summary>
        /// Writes a generic type array to a specified memory address.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="memoryAddress">The memory address to write to.</param>
        /// <param name="items">The array of items to write to the address.</param>
        /// <param name="marshal">Set this to true to enable struct marshalling.</param>
        public static void Write<T>(IntPtr memoryAddress, T[] items, bool marshal)
        {
            var structSize = SizeOf<T>(marshal);

            for (var i = 0; i < items.Length; i++)
            {
                var address = memoryAddress + (structSize * i);
                Write(address, items[i], marshal);
            }
        }

        #endregion

        #region WriteString

        /// <summary>
        /// Write a UTF-8 encoded string to a specified memory address.
        /// </summary>
        /// <remarks>
        /// Attention! If this is an SeString, use the <see cref="SeStringManager"/> to encode or the applicable helper method.
        /// </remarks>
        /// <param name="memoryAddress">The memory address to write to.</param>
        /// <param name="value">The string to write.</param>
        public static void WriteString(IntPtr memoryAddress, string value)
            => WriteString(memoryAddress, value, Encoding.UTF8);

        /// <summary>
        /// Write a string with the given encoding to a specified memory address.
        /// </summary>
        /// <remarks>
        /// Attention! If this is an SeString, use the <see cref="SeStringManager"/> to encode or the applicable helper method.
        /// </remarks>
        /// <param name="memoryAddress">The memory address to write to.</param>
        /// <param name="value">The string to write.</param>
        /// <param name="encoding">The encoding to use.</param>
        public static void WriteString(IntPtr memoryAddress, string value, Encoding encoding)
        {
            if (string.IsNullOrEmpty(value))
                return;

            var bytes = encoding.GetBytes(value + '\0');

            WriteRaw(memoryAddress, bytes);
        }

        /// <summary>
        /// Write an SeString to a specified memory address.
        /// </summary>
        /// <param name="memoryAddress">The memory address to write to.</param>
        /// <param name="value">The SeString to write.</param>
        public static void WriteSeString(IntPtr memoryAddress, SeString value)
        {
            if (value is null)
                return;

            WriteRaw(memoryAddress, value.Encode());
        }

        #endregion

        #region Sizing

        /// <summary>
        /// Returns the size of a specific primitive or struct type.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <returns>The size of the primitive or struct.</returns>
        public static int SizeOf<T>()
            => SizeOf<T>(false);

        /// <summary>
        /// Returns the size of a specific primitive or struct type.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="marshal">If set to true; will return the size of an element after marshalling.</param>
        /// <returns>The size of the primitive or struct.</returns>
        public static int SizeOf<T>(bool marshal)
            => marshal ? Marshal.SizeOf<T>() : Unsafe.SizeOf<T>();

        /// <summary>
        /// Returns the size of a specific primitive or struct type.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="elementCount">The number of array elements present.</param>
        /// <returns>The size of the primitive or struct array.</returns>
        public static int SizeOf<T>(int elementCount) where T : unmanaged
            => SizeOf<T>() * elementCount;

        /// <summary>
        /// Returns the size of a specific primitive or struct type.
        /// </summary>
        /// <typeparam name="T">An individual struct type of a class with an explicit StructLayout.LayoutKind attribute.</typeparam>
        /// <param name="elementCount">The number of array elements present.</param>
        /// <param name="marshal">If set to true; will return the size of an element after marshalling.</param>
        /// <returns>The size of the primitive or struct array.</returns>
        public static int SizeOf<T>(int elementCount, bool marshal)
            => SizeOf<T>(marshal) * elementCount;

        #endregion

        #region Game

        /// <summary>
        /// Allocate memory in the game's UI memory space.
        /// </summary>
        /// <param name="size">Amount of bytes to allocate.</param>
        /// <param name="alignment">The alignment of the allocation.</param>
        /// <returns>Pointer to the allocated region.</returns>
        public static IntPtr GameAllocateUi(ulong size, ulong alignment = 0)
        {
            return new IntPtr(IMemorySpace.GetUISpace()->Malloc(size, alignment));
        }

        /// <summary>
        /// Allocate memory in the game's default memory space.
        /// </summary>
        /// <param name="size">Amount of bytes to allocate.</param>
        /// <param name="alignment">The alignment of the allocation.</param>
        /// <returns>Pointer to the allocated region.</returns>
        public static IntPtr GameAllocateDefault(ulong size, ulong alignment = 0)
        {
            return new IntPtr(IMemorySpace.GetDefaultSpace()->Malloc(size, alignment));
        }

        /// <summary>
        /// Allocate memory in the game's animation memory space.
        /// </summary>
        /// <param name="size">Amount of bytes to allocate.</param>
        /// <param name="alignment">The alignment of the allocation.</param>
        /// <returns>Pointer to the allocated region.</returns>
        public static IntPtr GameAllocateAnimation(ulong size, ulong alignment = 0)
        {
            return new IntPtr(IMemorySpace.GetAnimationSpace()->Malloc(size, alignment));
        }

        /// <summary>
        /// Allocate memory in the game's apricot memory space.
        /// </summary>
        /// <param name="size">Amount of bytes to allocate.</param>
        /// <param name="alignment">The alignment of the allocation.</param>
        /// <returns>Pointer to the allocated region.</returns>
        public static IntPtr GameAllocateApricot(ulong size, ulong alignment = 0)
        {
            return new IntPtr(IMemorySpace.GetApricotSpace()->Malloc(size, alignment));
        }

        /// <summary>
        /// Allocate memory in the game's sound memory space.
        /// </summary>
        /// <param name="size">Amount of bytes to allocate.</param>
        /// <param name="alignment">The alignment of the allocation.</param>
        /// <returns>Pointer to the allocated region.</returns>
        public static IntPtr GameAllocateSound(ulong size, ulong alignment = 0)
        {
            return new IntPtr(IMemorySpace.GetSoundSpace()->Malloc(size, alignment));
        }

        /// <summary>
        /// Free memory in the game's memory space.
        /// </summary>
        /// <remarks>The memory you are freeing must be allocated with game allocators.</remarks>
        /// <param name="ptr">Position at which the memory to be freed is located.</param>
        /// <param name="size">Amount of bytes to free.</param>
        public static void GameFree(ref IntPtr ptr, ulong size)
        {
            if (ptr == IntPtr.Zero)
            {
                return;
            }

            IMemorySpace.Free((void*)ptr, size);
            ptr = IntPtr.Zero;
        }

        #endregion

        #region Utility

        /// <summary>
        /// Null-terminate a byte array.
        /// </summary>
        /// <param name="bytes">The byte array to terminate.</param>
        /// <returns>The terminated byte array.</returns>
        public static byte[] NullTerminate(this byte[] bytes)
        {
            if (bytes.Length == 0 || bytes[^1] != 0)
            {
                var newBytes = new byte[bytes.Length + 1];
                Array.Copy(bytes, newBytes, bytes.Length);
                newBytes[^1] = 0;

                return newBytes;
            }

            return bytes;
        }

        #endregion
    }
}
