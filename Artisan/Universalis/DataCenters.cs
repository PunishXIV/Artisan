using ECommons;
using ECommons.DalamudServices;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.Universalis
{
    public static class DataCenters
    {
        public static readonly uint[]
            Elemental = { 45, 49, 50, 58, 68, 72, 90, 94 },
            Gaia = { 43, 46, 51, 59, 69, 76, 92, 98 },
            Mana = { 23, 28, 44, 47, 48, 61, 70, 96 },
            Aether = { 40, 54, 57, 63, 65, 73, 79, 99 },
            Primal = { 35, 53, 55, 64, 77, 78, 93, 95 },
            Chaos = { 39, 71, 80, 83, 85, 97, 400, 401 },
            Light = { 33, 36, 42, 56, 66, 67, 402, 403 },
            Crystal = { 34, 37, 41, 62, 74, 75, 81, 91 },
            Materia = { 21, 22, 86, 87, 88 },
            Meteor = { 24, 29, 30, 31, 32, 52, 60, 82 },
            Dynamis = { 404, 405, 406, 407 },
            陆行鸟 = { 1167, 1081, 1042, 1044, 1060, 1173, 1174, 1175 },
            莫古力 = { 1172, 1076, 1171, 1170, 1113, 1121, 1166, 1176 },
            猫小胖 = { 1192, 1183, 1180, 1186, 1201, 1068, 1064, 1187 },
            한국 = { 2075, 2076, 2077, 2078, 2080 };

        public static readonly Dictionary<string, uint[]> AllDataCenters = new()
            {
                { "Elemental", Elemental },
                { "Gaia", Gaia },
                { "Mana", Mana},
                { "Aether", Aether },
                { "Primal", Primal },
                { "Chaos", Chaos  },
                { "Light", Light },
                { "Crystal", Crystal },
                { "Materia", Materia },
                { "Meteor", Meteor },
                { "Dynamis", Dynamis },
                { "陆行鸟", 陆行鸟 },
                { "莫古力", 莫古力 },
                { "猫小胖", 猫小胖 },
                { "한국", 한국 },
            };

        public static uint[]? GetDataCenterByWorld(uint world)
        {
            return Array.Find(AllDataCenters.Values.ToArray(), (worlds) => worlds.Contains(world));
        }

        public static string? GetDataCenterNameByWorld(uint world)
        {
            var worlds = GetDataCenterByWorld(world);
            if (worlds == null)
                return null;

            return AllDataCenters.FindKeysByValue(worlds).First();
        }

        public static string? GetWorldName(uint world)
        {
            var name = Svc.Data.GetExcelSheet<World>()?.FirstOrDefault(x => x.RowId == world).Name;

            if (name != null)
            return name;

            return null;
        }
    }

    public static class Regions
    {
        public static readonly Dictionary<string, List<uint[]>>
            Japan = new() { { "Japan", new() { DataCenters.Elemental, DataCenters.Gaia, DataCenters.Mana, DataCenters.Meteor } } },
            NorthAmerica = new() { { "North-America", new() { DataCenters.Crystal, DataCenters.Aether, DataCenters.Primal, DataCenters.Dynamis } } },
            Oceania = new() { { "Oceania", new() { DataCenters.Materia } } },
            Europe = new() { { "Europe", new() { DataCenters.Chaos, DataCenters.Light } } },
            中国 = new() { { "中国", new() { DataCenters.陆行鸟, DataCenters.莫古力, DataCenters.猫小胖, DataCenters.한국 } } };

        public static readonly Dictionary<string, List<uint[]>>[]
            AllRegions = { Japan, NorthAmerica, Europe, Oceania, 中国 };

        public static string GetRegionByWorld(uint world)
        {
            foreach (var region in AllRegions)
            {
                foreach (var dc in region.Values)
                {
                    foreach (var worlds in dc)
                    {
                        if (worlds.Contains(world))
                            return region.FindKeysByValue(dc).First();
                    }
                }
            }

            return null;
        }

        public static Dictionary<string, List<uint[]>>? GetRegionByString(string region)
        {
            return region switch
            {
                "Japan" => Japan,
                "North-America" => NorthAmerica,
                "Europe" => Europe,
                "Oceania" => Oceania,
                "中国" => 中国,
                _ => null
            };
        }
    }
}
