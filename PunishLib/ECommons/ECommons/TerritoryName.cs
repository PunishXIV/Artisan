using ECommons.DalamudServices;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons
{
    public static class TerritoryName
    {
        static Dictionary<uint, string> Cache = new();
        public static string GetTerritoryName(uint id)
        {
            if(Cache.TryGetValue(id, out var val))
            {
                return val;
            }
            var data = Svc.Data.GetExcelSheet<TerritoryType>().GetRow(id);
            if(data != null)
            {
                var zoneName = data.PlaceName.Value.Name.ToString();
                if (zoneName != string.Empty) 
                {
                    var cfc = data.ContentFinderCondition.Value;
                    if(cfc != null)
                    {
                        var cfcStr = cfc.Name.ToString();
                        if (cfcStr != String.Empty)
                        {
                            Cache[id] = $"{id} | {zoneName} ({cfcStr})";
                            return Cache[id];
                        }
                    }
                    Cache[id] = $"{id} | {zoneName}";
                    return Cache[id];
                }
            }
            Cache[id] = $"{id}";
            return Cache[id];
        }
    }
}
