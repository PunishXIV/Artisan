using Artisan.QuestSync;
using Dalamud;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Data;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace Artisan.RawInformation
{
    public class LuminaSheets
    {

        public static Dictionary<uint, Recipe>? RecipeSheet = Service.DataManager?.GetExcelSheet<Recipe>()?
            .Where(x => !string.IsNullOrEmpty(x.ItemResult.Value.Name.RawString))
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, GatheringItem>? GatheringItemSheet = Service.DataManager?.GetExcelSheet<GatheringItem>()?
            .Where(x => x.GatheringItemLevel.Value.GatheringItemLevel > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, SpearfishingItem>? SpearfishingItemSheet = Service.DataManager?.GetExcelSheet<SpearfishingItem>()?
            .Where(x => x.GatheringItemLevel.Value.GatheringItemLevel > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, GatheringPointBase>? GatheringPointBaseSheet = Service.DataManager?.GetExcelSheet<GatheringPointBase>()?
            .Where(x => x.GatheringLevel > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, FishParameter>? FishParameterSheet = Service.DataManager?.GetExcelSheet<FishParameter>()?
            .Where(x => x.GatheringItemLevel.Value.GatheringItemLevel > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, ClassJob>? ClassJobSheet = Service.DataManager?.GetExcelSheet<ClassJob>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Item>? ItemSheet = Service.DataManager?.GetExcelSheet<Item>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Action>? ActionSheet = Service.DataManager?.GetExcelSheet<Action>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, CraftAction>? CraftActions = Service.DataManager?.GetExcelSheet<CraftAction>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, CraftLevelDifference>? CraftLevelDifference = Service.DataManager?.GetExcelSheet<CraftLevelDifference>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, RecipeLevelTable>? RecipeLevelTableSheet = Service.DataManager?.GetExcelSheet<RecipeLevelTable>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Addon>? AddonSheet = Service.DataManager?.GetExcelSheet<Addon>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, SpecialShop>? SpecialShopSheet = Service.DataManager?.GetExcelSheet<SpecialShop>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, LogMessage>? LogMessageSheet = Service.DataManager?.GetExcelSheet<LogMessage>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, ItemFood>? ItemFoodSheet = Service.DataManager?.GetExcelSheet<ItemFood>()?
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, ENpcResident>? ENPCResidentSheet = Service.DataManager?.GetExcelSheet<ENpcResident>()?
            .Where(x => x.Singular.ExtractText().Length > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, Quest>? QuestSheet = Service.DataManager?.GetExcelSheet<Quest>()?
            .Where(x => x.Id.ExtractText().Length > 0)
            .ToDictionary(i => i.RowId, i => i);

        public static Dictionary<uint, CompanyCraftPart>? WorkshopPartSheet = Service.DataManager?.GetExcelSheet<CompanyCraftPart>()?
            .ToDictionary( i=> i.RowId, i => i);
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
                    foreach (var step in firstData.Where(x => x.Text.Payloads.Count > 0))
                    {
                        foreach (var payload in step.Text.ToDalamudString().Payloads.Where(x => x.Type == PayloadType.Unknown))
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
