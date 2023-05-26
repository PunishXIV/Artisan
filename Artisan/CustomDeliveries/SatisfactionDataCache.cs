using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.Interop.Attributes;
using System;
using System.Linq;

namespace Artisan.CustomDeliveries
{
    internal class SatisfactionDataCache
    {
    }

    public static unsafe class SatisfactionManagerHelper
    {
        //LoadNPCHook 40 53 55 41 55 41 56 48 83 EC 

        public delegate void AgentUpdateDelegate(AgentSatisfactionSupply* agent);
        public static Hook<AgentUpdateDelegate> AgentUpdateHook;

        public delegate byte AgentValuesUpdated(AgentSatisfactionSupply* agent);
        public static Hook<AgentValuesUpdated> AgentValuesUpdatedHook;

        public delegate byte LoadNPCDelegate(SatisfactionSupplyManager* manager, uint satisfactionNPCID, uint* npcID);
        public static Hook<LoadNPCDelegate> LoadNPCHook;

        public static AgentSatisfactionSupply* Agent = (AgentSatisfactionSupply*)AgentModule.Instance()->GetAgentByInternalID((uint)AgentId.SatisfactionSupply);
        public static void AgentUpdateDetour(AgentSatisfactionSupply* agent)
        {
            AgentUpdateHook.Original(agent);
        }

        public static byte AgentValuesUpdatedDetour(AgentSatisfactionSupply* agent)
        {
            agent->NpcInfo.Id = 1;
            agent->NpcId = 1019615;
            return AgentValuesUpdatedHook.Original(agent);
        }

        public static byte LoadNPCDetour(SatisfactionSupplyManager* manager, uint satisfactionNPCID, uint* NPCID)
        {
            Dalamud.Logging.PluginLog.Debug($"{satisfactionNPCID}");
            return LoadNPCHook.Original(manager, satisfactionNPCID, NPCID);
        }

        static SatisfactionManagerHelper()
        {
            AgentUpdateHook ??= Hook<AgentUpdateDelegate>.FromAddress(Svc.SigScanner.ScanText("40 53 48 83 EC ?? 80 79 ?? ?? 48 8B D9 0F 84 ?? ?? ?? ?? 80 79 ?? ?? 75"), AgentUpdateDetour);
            AgentValuesUpdatedHook ??= Hook<AgentValuesUpdated>.FromAddress(Svc.SigScanner.ScanText("48 89 5C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC ?? 8B 51"), AgentValuesUpdatedDetour);
            LoadNPCHook ??= Hook<LoadNPCDelegate>.FromAddress(Svc.SigScanner.ScanText("40 53 55 41 55 41 56 48 83 EC"), LoadNPCDetour);
        }

        public static void UpdateByNPCId(uint NPCId, uint SatisfactionID)
        {
            try
            {
                AgentUpdateDetour(Agent);
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Debug(ex, ex.Message);
            }
        }
        public static void TryEnable()
        {
            if (!AgentUpdateHook.IsEnabled)
                AgentUpdateHook?.Enable();
        }

        public static void TryDisable()
        {
            if (AgentUpdateHook.IsEnabled)
                AgentUpdateHook?.Disable();
        }
        public static void Enable()
        {
            //AgentUpdateHook?.Enable();
            AgentValuesUpdatedHook?.Enable();
            //LoadNPCHook?.Enable();
        }

        public static void Disable()
        {
            AgentUpdateHook?.Disable();
            AgentValuesUpdatedHook?.Disable();
        }

        public static void Dispose()
        {
            AgentUpdateHook?.Dispose();
            AgentValuesUpdatedHook?.Dispose();
            LoadNPCHook?.Dispose();
        }

    }

    public class SatisfactionNPCDetail
    {
        public string Name;

        public uint CraftItemID;

        public uint RecipeID
        {
            get
            {
                if (CharacterInfo.JobID() < 8 || CharacterInfo.JobID() > 15)
                    return 0;

                return LuminaSheets.RecipeSheet.Values.Where(x => x.ItemResult.Value.RowId == CraftItemID && x.CraftType.Value.RowId == CharacterInfo.JobID() - 8).FirstOrDefault().RowId;
            }
        }

        public bool IsBonus;
    }
}
