using ECommons.DalamudServices;
using ECommons.Reflection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.RawInformation
{
    internal static class DalamudInfo
    {
        public static bool IsOnStaging()
        {
            if (DalamudReflector.TryGetDalamudStartInfo(out var startinfo, Svc.PluginInterface))
            {
                if (File.Exists(startinfo.ConfigurationPath))
                {
                    var file = File.ReadAllText(startinfo.ConfigurationPath);
                    var ob = JsonConvert.DeserializeObject<dynamic>(file);
                    string type = ob.DalamudBetaKind;
                    if (type is not null && !string.IsNullOrEmpty(type) && type != "release")
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            return false;
        }
    }
}
