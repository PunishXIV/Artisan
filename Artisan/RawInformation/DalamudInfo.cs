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

            var v = Svc.PluginInterface.GetDalamudVersion();
            if (v.BetaTrack.Equals("release", StringComparison.CurrentCultureIgnoreCase))
            {
                StagingChecked = true;
                IsStaging = false;
                return false;
            }
            else
            {
                StagingChecked = false;
                IsStaging = true;
                return true;
            }
        }
    }
}
