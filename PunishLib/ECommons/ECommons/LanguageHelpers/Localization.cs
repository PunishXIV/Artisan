using Dalamud;
using ECommons.Logging;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.LanguageHelpers
{
    public static class Localization
    {
        public static string PararmeterSymbol = "??";
        public static string Separator = "==";
        internal static Dictionary<string, string> CurrentLocalization = new();
        internal static List<string> AvailableLanguages;
        public static string CurrentLanguage { get; internal set; } = null;
        public static bool Logging = false;

        public static void Init(string Language = null)
        {
            CurrentLocalization.Clear();
            CurrentLanguage = null;
            if (Language != null)
            {
                var file = GetLocFileLocation(Language);
                if (File.Exists(file))
                {
                    CurrentLanguage = Language;
                    var text = File.ReadAllText(file, Encoding.UTF8);
                    var list = text.Replace("\r\n", "\n").Replace("\r", "").Split("\n");
                    for (var i = 0; i < list.Length; i++)
                    {
                        var x = list[i].Replace("\\n", "\n");
                        var e = x.Split(Separator);
                        if (e.Length == 2)
                        {
                            if (CurrentLocalization.ContainsKey(e[0]))
                            {
                                PluginLog.Warning($"[Localization] Duplicate localization entry {e[0]} found in localization file {file}");
                            }
                            CurrentLocalization[e[0]] = e[1];
                        }
                        else
                        {
                            PluginLog.Warning($"[Localization] Invalid entry {x} (line {i}) found in localization file {file}");
                        }
                    }
                    PluginLog.Information($"[Localization] Loaded {CurrentLocalization.Count} entries");
                }
                else
                {
                    PluginLog.Information($"[Localization] Requested localization file {file} does not exists");
                }
            }
            else
            {
                PluginLog.Information("[Localization] No special localization");
            }
        }

        public static List<string> GetAvaliableLanguages(bool rescan = false)
        {
            if(AvailableLanguages == null || rescan)
            {
                AvailableLanguages = new() { "English" };
                foreach (var x in Directory.GetFiles(Svc.PluginInterface.AssemblyLocation.DirectoryName))
                {
                    var name = Path.GetFileName(x);
                    if (name.StartsWith("Language") && name.EndsWith(".ini"))
                    {
                        var lang = name[8..^4];
                        if(!AvailableLanguages.Contains(lang)) AvailableLanguages.Add(lang);
                        PluginLog.Information($"[Localization] Found language data {lang}");
                    }
                }
            }
            return AvailableLanguages;
        }

        public static string GameLanguageString => Svc.Data.Language switch
        {
            ClientLanguage.Japanese => "Japanese",
            ClientLanguage.French => "French",
            ClientLanguage.German => "German",
            (ClientLanguage)4 => "Chinese",
            _ => "English"
        };

        public static void Save(string lang)
        {
            var file = GetLocFileLocation(lang);
            File.WriteAllText(file, CurrentLocalization.Select(x => $"{x.Key.Replace("\n", "\\n")}{Separator}{x.Value.Replace("\n", "\\n")}").Join("\n"));
        }

        public static string GetLocFileLocation(string lang)
        {
            return Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName, $"Language{lang}.ini");
        }

        public static string Loc(string s)
        {
            return s.Loc();
        }

        public static string Loc(string s, params object[] values)
        {
            return s.Loc(values);
        }
    }
}
