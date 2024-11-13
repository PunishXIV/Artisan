using ECommons.DalamudServices;
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

        public DropSources(uint ItemId, List<uint> monsterId)
        {
            ItemId = ItemId;
            MonsterId = monsterId;
            CanObtainFromRetainer = Svc.Data.GetExcelSheet<RetainerTaskNormal>()!.Any(x => x.Item.RowId == ItemId);
            UsedInRecipes = LuminaSheets.RecipeSheet.Values.Any(y => y.UnkData5.Any(x => x.ItemIngredient == ItemId));
        }

        public bool CanObtainFromRetainer { get; set; }
        public uint ItemId { get; set; }

        public List<uint> MonsterId { get; set; }
        public bool UsedInRecipes { get; set; }

        private static List<DropSources>? DropList()
        {
            List<DropSources>? output = new();
            try
            {
                using HttpResponseMessage? sources = new HttpClient().GetAsync("https://raw.githubusercontent.com/ffxiv-teamcraft/ffxiv-teamcraft/master/libs/data/src/lib/json/drop-sources.json").Result;
                sources.EnsureSuccessStatusCode();
                string? data = sources.Content.ReadAsStringAsync().Result;

                if (data != null)
                {
                    JObject? file = JsonConvert.DeserializeObject<JObject>(data);
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
            }
            catch (Exception ex)
            {
            }

            return output;
        }
    }
}