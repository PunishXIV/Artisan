using Artisan.QuestSync;
using Dalamud;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Data;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Status = Lumina.Excel.GeneratedSheets.Status;

namespace Artisan.RawInformation
{
    public class LuminaSheets
    {

        public static Dictionary<uint, Recipe>? RecipeSheet = Svc.Data?.GetExcelSheet<Recipe>()?
            .Where(x => !string.IsNullOrEmpty(x.ItemResult.Value.Name.RawString))
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, GatheringItem>? GatheringItemSheet = Svc.Data?.GetExcelSheet<GatheringItem>()?
            .Where(x => x.GatheringItemLevel.Value.GatheringItemLevel > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, SpearfishingItem>? SpearfishingItemSheet = Svc.Data?.GetExcelSheet<SpearfishingItem>()?
            .Where(x => x.GatheringItemLevel.Value.GatheringItemLevel > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, GatheringPointBase>? GatheringPointBaseSheet = Svc.Data?.GetExcelSheet<GatheringPointBase>()?
            .Where(x => x.GatheringLevel > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, FishParameter>? FishParameterSheet = Svc.Data?.GetExcelSheet<FishParameter>()?
            .Where(x => x.GatheringItemLevel.Value.GatheringItemLevel > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, ClassJob>? ClassJobSheet = Svc.Data?.GetExcelSheet<ClassJob>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Item>? ItemSheet = Svc.Data?.GetExcelSheet<Item>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Action>? ActionSheet = Svc.Data?.GetExcelSheet<Action>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Status>? StatusSheet = Svc.Data?.GetExcelSheet<Status>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, CraftAction>? CraftActions = Svc.Data?.GetExcelSheet<CraftAction>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, CraftLevelDifference>? CraftLevelDifference = Svc.Data?.GetExcelSheet<CraftLevelDifference>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, RecipeLevelTable>? RecipeLevelTableSheet = Svc.Data?.GetExcelSheet<RecipeLevelTable>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Addon>? AddonSheet = Svc.Data?.GetExcelSheet<Addon>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, SpecialShop>? SpecialShopSheet = Svc.Data?.GetExcelSheet<SpecialShop>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, LogMessage>? LogMessageSheet = Svc.Data?.GetExcelSheet<LogMessage>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, ItemFood>? ItemFoodSheet = Svc.Data?.GetExcelSheet<ItemFood>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, ENpcResident>? ENPCResidentSheet = Svc.Data?.GetExcelSheet<ENpcResident>()?
            .Where(x => x.Singular.ExtractText().Length > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Quest>? QuestSheet = Svc.Data?.GetExcelSheet<Quest>()?
            .Where(x => x.Id.ExtractText().Length > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, CompanyCraftPart>? WorkshopPartSheet = Svc.Data?.GetExcelSheet<CompanyCraftPart>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, CompanyCraftProcess>? WorkshopProcessSheet = Svc.Data?.GetExcelSheet<CompanyCraftProcess>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, CompanyCraftSequence>? WorkshopSequenceSheet = Svc.Data?.GetExcelSheet<CompanyCraftSequence>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, CompanyCraftSupplyItem>? WorkshopSupplyItemSheet = Svc.Data?.GetExcelSheet<CompanyCraftSupplyItem>()?
            .ToDictionary(i => i.RowId, i => i);

        public static void Dispose()
        {
            var type = typeof(LuminaSheets);
            foreach (var prop in type.GetFields(System.Reflection.BindingFlags.Static))
            {
                prop.SetValue(null, null);  
            }
        }
    }

    public static class SheetExtensions
    {
        public static string NameOfAction(this uint id)
        {
            if (id == 0) return "Artisan Recommendation";

            if (id < 100000)
            {
                return LuminaSheets.ActionSheet[id].Name.RawString;
            }
            else
            {
                return LuminaSheets.CraftActions[id].Name.RawString;
            }
        }

        public static string NameOfBuff(this ushort id)
        {
            if (id == 0) return "";

            return LuminaSheets.StatusSheet[id].Name.RawString;
        }

        public static string NameOfItem(this uint id)
        {
            if (id == 0) return "";

            return LuminaSheets.ItemSheet[id].Name.ExtractText();
        }

        public static string NameOfRecipe(this uint id)
        {
            if (id == 0) return "";

            return LuminaSheets.RecipeSheet[id].ItemResult.Value.Name.RawString;
        }

        public static string NameOfQuest(this ushort id)
        {
            if (id == 9998 || id == 9999)
                id = 1493;

            if (id > 0)
            {
                var digits = id.ToString().Length;
                if (LuminaSheets.QuestSheet!.Any(x => Convert.ToInt16(x.Value.Id.RawString.GetLast(digits)) == id))
                {
                    return LuminaSheets.QuestSheet!.First(x => Convert.ToInt16(x.Value.Id.RawString.GetLast(digits)) == id).Value.Name.ExtractText().Replace("", "").Trim();
                }
            }
            return "";

        }

        public static unsafe string GetSequenceInfo(this ushort id)
        {
            if (id > 0)
            {
                var digits = id.ToString().Length;
                if (LuminaSheets.QuestSheet!.Any(x => Convert.ToInt16(x.Value.Id.RawString.GetLast(digits)) == id))
                {
                    var quest = LuminaSheets.QuestSheet!.First(x => Convert.ToInt16(x.Value.Id.RawString.GetLast(digits)) == id).Value;
                    var sequence = QuestManager.GetQuestSequence(id);
                    if (sequence == 255) return "NULL";

                    var lang = Svc.ClientState.ClientLanguage switch
                    {
                        ClientLanguage.English => Language.English,
                        ClientLanguage.Japanese => Language.Japanese,
                        ClientLanguage.German => Language.German,
                        ClientLanguage.French => Language.French,
                        _ => Language.English,
                    };

                    var path = $"quest/{id.ToString("00000")[..3]}/{quest.Id.RawString}";
                    // FIXME: this is gross, but lumina caches incorrectly
                    Svc.Data.Excel.RemoveSheetFromCache<QuestData>();
                    var sheet = Svc.Data.Excel.GetSheet<QuestData>(path);
                    var seqPath = $"SEQ_{sequence.ToString("00")}";
                    var firstData = sheet?.Where(x => x.Id.Contains(seqPath)).FirstOrDefault();
                    if (firstData != null)
                    {
                        return firstData.Text.ExtractText();
                    }
                }
            }
            return "";
        }

        public static string GetToDoInfo(this ushort id)
        {
            if (id > 0)
            {
                var digits = id.ToString().Length;
                if (LuminaSheets.QuestSheet!.Any(x => Convert.ToInt16(x.Value.Id.RawString.GetLast(digits)) == id))
                {
                    var quest = LuminaSheets.QuestSheet!.First(x => Convert.ToInt16(x.Value.Id.RawString.GetLast(digits)) == id).Value;

                    var lang = Svc.ClientState.ClientLanguage switch
                    {
                        ClientLanguage.English => Language.English,
                        ClientLanguage.Japanese => Language.Japanese,
                        ClientLanguage.German => Language.German,
                        ClientLanguage.French => Language.French,
                        _ => Language.English,
                    };

                    var path = $"quest/{id.ToString("00000")[..3]}/{quest.Id.RawString}";
                    // FIXME: this is gross, but lumina caches incorrectly
                    Svc.Data.Excel.RemoveSheetFromCache<QuestData>();
                    var sheet = Svc.Data.Excel.GetSheet<QuestData>(path);
                    var seqPath = $"TODO_";
                    var firstData = sheet?.Where(x => x.Id.Contains(seqPath)).ToList();
                    string output = "";
                    foreach (var step in firstData?.Where(x => x.Text.Payloads.Count > 0))
                    {
                        foreach (var payload in step.Text?.ToDalamudString().Payloads.Where(x => x.Type == PayloadType.Unknown))
                        {
                            var line = step.Text.RawString[10..];
                            output += line;
                        }
                    }
                    return output;

                }
            }
            return "";
        }
    }
}
