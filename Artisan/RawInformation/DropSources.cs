using ECommons.DalamudServices;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Artisan.RawInformation
{
    public class DropSources
    {
        public static List<DropSources>? Sources = DropList()?.ToList();

        public DropSources(uint itemId, List<uint> monsterId)
        {
            ItemId = itemId;
            MonsterId = monsterId;
            CanObtainFromRetainer = Svc.Data.GetExcelSheet<RetainerTaskNormal>()!.Any(x => x.Item.Row == itemId);
            UsedInRecipes = LuminaSheets.RecipeSheet.Values.Any(y => y.UnkData5.Any(x => x.ItemIngredient == itemId));
        }

        public bool CanObtainFromRetainer { get; set; }
        public uint ItemId { get; set; }

        public List<uint> MonsterId { get; set; }
        public bool UsedInRecipes { get; set; }

        private static List<DropSources>? DropList()
        {
            List<DropSources>? output = new();
            using HttpResponseMessage? sources = Dalamud.Utility.Util.HttpClient?.GetAsync("https://raw.githubusercontent.com/ffxiv-teamcraft/ffxiv-teamcraft/0170e596eb9fb1b7027616fd380ab85a3b6bb717/libs/data/src/lib/json/drop-sources.json").Result;
            sources.EnsureSuccessStatusCode();
            string? data = sources.Content.ReadAsStringAsync().Result;

            if (data != null)
            {
                JObject file = JsonConvert.DeserializeObject<JObject>(data);
                foreach (var item in file)
                {
                    List<uint> monsters = new();
                    foreach (var monster in item.Value)
                    {
                        monsters.Add((uint)monster);
                    }
                    DropSources source = new DropSources(Convert.ToUInt32(item.Key), monsters);
                    if (source.UsedInRecipes && !source.CanObtainFromRetainer)
                    output.Add(source);
                }
            }

            return output;
        }
    }
}