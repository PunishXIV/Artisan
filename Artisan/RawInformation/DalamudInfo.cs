using ECommons.DalamudServices;
using ECommons.Reflection;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace Artisan.RawInformation
{
    internal static class DalamudInfo
    {
        public static bool StagingChecked = false;
        public static bool IsStaging = false;
        public static bool IsOnStaging()
        {
            if (StagingChecked)
            {
                return IsStaging;
            }

            if (DalamudReflector.TryGetDalamudStartInfo(out var startinfo, Svc.PluginInterface))
            {
                try
                {
                    HttpClient client = new HttpClient();
                    var dalDeclarative = "https://raw.githubusercontent.com/goatcorp/dalamud-declarative/refs/heads/main/config.yaml";
                    using (var stream = client.GetStreamAsync(dalDeclarative).Result)
                    using (var reader = new StreamReader(stream))
                    {
                        for (int i = 0; i <= 4; i++)
                        {
                            var line = reader.ReadLine().Trim();
                            if (i != 4) continue;
                            var version = line.Split(":").Last().Trim().Replace("'", "");
                            if (version != startinfo.GameVersion.ToString())
                            {
                                StagingChecked = true;
                                IsStaging = false;
                                return false;
                            }
                        }
                    }
                }
                catch
                {
                    // Something has gone wrong with checking the Dalamud github file, just allow plugin load anyway
                    StagingChecked = true;
                    IsStaging = false;
                    return false;
                }

                if (File.Exists(startinfo.ConfigurationPath))
                {
                    try
                    {
                        var file = File.ReadAllText(startinfo.ConfigurationPath);
                        var ob = JsonConvert.DeserializeObject<dynamic>(file);
                        string type = ob.DalamudBetaKind;
                        if (type is not null && !string.IsNullOrEmpty(type) && type != "release")
                        {
                            StagingChecked = true;
                            IsStaging = true;
                            return true;
                        }
                        else
                        {
                            StagingChecked = true;
                            IsStaging = false;
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Svc.Chat.PrintError($"Unable to detrermine Dalamud staging due to file being config being unreadable.");
                        StagingChecked = true;
                        IsStaging = false;
                        return false;
                    }
                }
                else
                {
                    StagingChecked = true;
                    IsStaging = false;
                    return false;
                }
            }
            return false;
        }
    }
}
