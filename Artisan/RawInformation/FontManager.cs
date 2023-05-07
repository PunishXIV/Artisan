/*
 This file contains source code authored by Anna Clemens from https://git.annaclemens.io/ascclemens/ChatTwo/src/branch/main/ChatTwo which is distributed under EUPL license
 */
using Dalamud.Interface.GameFonts;
using ECommons.DalamudServices;
using ECommons.Schedulers;
using ImGuiNET;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using Dalamud.Interface;
using Dalamud.Logging;
using SharpDX;
using SharpDX.DirectWrite;
using System.IO;
using Microsoft.VisualBasic;

#pragma warning disable CS8632
namespace Artisan.RawInformation;

internal static class Fonts
{
    internal static List<string> GetFonts()
    {
        var fonts = new List<string>();

        using var factory = new Factory();
        using var collection = factory.GetSystemFontCollection(false);
        for (var i = 0; i < collection.FontFamilyCount; i++)
        {
            using var family = collection.GetFontFamily(i);
            var anyItalic = false;
            for (var j = 0; j < family.FontCount; j++)
            {
                try
                {
                    var font = family.GetFont(j);
                    if (font.IsSymbolFont || font.Style is not (FontStyle.Italic or FontStyle.Oblique))
                    {
                        continue;
                    }

                    anyItalic = true;
                    break;
                }
                catch (SharpDXException)
                {
                    // no-op
                }
            }

            if (!anyItalic)
            {
                continue;
            }

            var name = family.FamilyNames.GetString(0);
            fonts.Add(name);
        }

        fonts.Sort();
        return fonts;
    }

    internal static List<string> GetJpFonts()
    {
        var fonts = new List<string>();

        using var factory = new Factory();
        using var collection = factory.GetSystemFontCollection(false);
        for (var i = 0; i < collection.FontFamilyCount; i++)
        {
            using var family = collection.GetFontFamily(i);
            var probablyJp = false;
            for (var j = 0; j < family.FontCount; j++)
            {
                try
                {
                    using var font = family.GetFont(j);
                    if (!font.HasCharacter('気') || font.IsSymbolFont)
                    {
                        continue;
                    }

                    probablyJp = true;
                    break;
                }
                catch (SharpDXException)
                {
                    // no-op
                }
            }

            if (!probablyJp)
            {
                continue;
            }

            var name = family.FamilyNames.GetString(0);
            fonts.Add(name);
        }

        fonts.Sort();
        return fonts;
    }

    internal static FontData? GetFont(string name, bool withItalic)
    {
        using var factory = new Factory();
        using var collection = factory.GetSystemFontCollection(false);
        for (var i = 0; i < collection.FontFamilyCount; i++)
        {
            using var family = collection.GetFontFamily(i);
            if (family.FamilyNames.GetString(0) != name)
            {
                continue;
            }

            using var normal = family.GetFirstMatchingFont(FontWeight.Normal, FontStretch.Normal, FontStyle.Normal);
            if (normal == null)
            {
                return null;
            }

            FaceData? GetFontData(SharpDX.DirectWrite.Font font)
            {
                using var face = new FontFace(font);
                var files = face.GetFiles();
                if (files.Length == 0)
                {
                    return null;
                }

                var key = files[0].GetReferenceKey();
                using var stream = files[0].Loader.CreateStreamFromKey(key);

                stream.ReadFileFragment(out var start, 0, stream.GetFileSize(), out var release);

                var data = new byte[stream.GetFileSize()];
                Marshal.Copy(start, data, 0, data.Length);

                stream.ReleaseFileFragment(release);

                var metrics = font.Metrics;
                var ratio = (metrics.Ascent + metrics.Descent + metrics.LineGap) / (float)metrics.DesignUnitsPerEm;

                return new FaceData(data, ratio);
            }

            var normalData = GetFontData(normal);
            if (normalData == null)
            {
                return null;
            }

            FaceData? italicData = null;
            if (withItalic)
            {
                using var italic = family.GetFirstMatchingFont(FontWeight.Normal, FontStretch.Normal, FontStyle.Italic)
                                   ?? family.GetFirstMatchingFont(FontWeight.Normal, FontStretch.Normal, FontStyle.Oblique);
                if (italic == null)
                {
                    return null;
                }

                italicData = GetFontData(italic);
            }

            if (italicData == null && withItalic)
            {
                return null;
            }

            return new FontData(normalData, italicData);
        }

        return null;
    }
}

internal sealed class FaceData
{
    internal byte[] Data { get; }
    internal float Ratio { get; }

    internal FaceData(byte[] data, float ratio)
    {
        this.Data = data;
        this.Ratio = ratio;
    }
}

internal sealed class FontData
{
    internal FaceData Regular { get; }
    internal FaceData? Italic { get; }

    internal FontData(FaceData regular, FaceData? italic)
    {
        this.Regular = regular;
        this.Italic = italic;
    }
}

internal sealed class Font
{
    internal string Name { get; }
    internal string ResourcePath { get; }
    internal string ResourcePathItalic { get; }

    internal Font(string name, string resourcePath, string resourcePathItalic)
    {
        this.Name = name;
        this.ResourcePath = resourcePath;
        this.ResourcePathItalic = resourcePathItalic;
    }
}

internal class FontManager
{
    internal GameFontHandle? SourceAxisFont { get; set; }

    internal ImFontPtr? CustomFont { get; set; }

    ImFontConfigPtr FontConfig;
    (GCHandle, int, float) customFontHandle;
    ImVector ranges;
    GCHandle symbolsRange = GCHandle.Alloc(new ushort[] { 0xE020, 0xE0DB, 0 }, GCHandleType.Pinned);

    internal unsafe FontManager()
    {
        FontConfig = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig())
        {
            FontDataOwnedByAtlas = false,
        };
        SetUpRanges();
        SetUpUserFonts();
        Svc.PluginInterface.UiBuilder.BuildFonts += BuildFonts;
        Svc.PluginInterface.UiBuilder.RebuildFonts();
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.BuildFonts -= BuildFonts;
        if (customFontHandle.Item1.IsAllocated)
        {
            customFontHandle.Item1.Free();
        }
        if (symbolsRange.IsAllocated)
        {
            symbolsRange.Free();
        }
        FontConfig.Destroy();
    }

    unsafe void SetUpRanges()
    {
        ImVector BuildRange(IReadOnlyList<ushort> chars, params IntPtr[] ranges)
        {
            var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
            foreach (var range in ranges)
            {
                builder.AddRanges(range);
            }
            if (chars != null)
            {
                for (var i = 0; i < chars.Count; i += 2)
                {
                    if (chars[i] == 0)
                    {
                        break;
                    }

                    for (var j = (uint)chars[i]; j <= chars[i + 1]; j++)
                    {
                        builder.AddChar((ushort)j);
                    }
                }
            }

            // various symbols
            builder.AddText("←→↑↓《》■※☀★★☆♥♡ヅツッシ☀☁☂℃℉°♀♂♠♣♦♣♧®©™€$£♯♭♪✓√◎◆◇♦■□〇●△▽▼▲‹›≤≥<«“”─＼～");
            // French
            builder.AddText("Œœ");
            // Romanian
            builder.AddText("ĂăÂâÎîȘșȚț");

            // "Enclosed Alphanumerics" (partial) https://www.compart.com/en/unicode/block/U+2460
            for (var i = 0x2460; i <= 0x24B5; i++)
            {
                builder.AddChar((char)i);
            }

            builder.AddChar('⓪');

            var result = new ImVector();
            builder.BuildRanges(out result);
            builder.Destroy();

            return result;
        }

        var ranges = new List<IntPtr> {
            ImGui.GetIO().Fonts.GetGlyphRangesDefault(),
        };

        this.ranges = BuildRange(null, ranges.ToArray());
    }

    void SetUpUserFonts()
    {
        string path = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Fonts", "CaviarDreams_Bold.ttf");
        if (File.Exists(path))
        {
            var memory = new MemoryStream();
            var stream = File.OpenRead(path);
            stream.CopyTo(memory);
            var regular = new FaceData(memory.ToArray(), 1f);
            var italic = new FaceData(memory.ToArray(), 1f);
            FontData fontData = new FontData(regular, italic);

            if (fontData == null)
            {
                PluginLog.Error($"Font not found: \"Caviar Dreams\"");
                return;
            }

            if (customFontHandle.Item1.IsAllocated)
            {
                customFontHandle.Item1.Free();
            }

            customFontHandle = (
                GCHandle.Alloc(fontData.Regular.Data, GCHandleType.Pinned),
                fontData.Regular.Data.Length,
                fontData.Regular.Ratio
            );
        }
    }

    void BuildFonts()
    {
        CustomFont = null;

        SetUpRanges();
        SetUpUserFonts();

        CustomFont = ImGui.GetIO().Fonts.AddFontFromMemoryTTF(
            customFontHandle.Item1.AddrOfPinnedObject(),
            customFontHandle.Item2,
            ImGui.GetFontSize(),
            FontConfig,
            ranges.Data
        );

        SourceAxisFont = Svc.PluginInterface.UiBuilder.GetGameFontHandle(new GameFontStyle(GameFontFamily.Axis, ImGui.GetFontSize()));

        new TickScheduler(delegate
        {
            ImGuiHelpers.CopyGlyphsAcrossFonts(SourceAxisFont.ImFont, CustomFont, true, true);
        });
    }
}

[Serializable]
[Flags]
public enum ExtraGlyphRanges
{
    ChineseFull = 1 << 0,
    ChineseSimplifiedCommon = 1 << 1,
    Cyrillic = 1 << 2,
    Japanese = 1 << 3,
    Korean = 1 << 4,
    Thai = 1 << 5,
    Vietnamese = 1 << 6,
}