using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.UI.Tables;
using Dalamud.Configuration;
using ECommons.DalamudServices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Artisan
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 2;
        public bool AutoMode
        {
            get => autoMode;
            set
            {
                if (value)
                {
                    ActionManagerEx.UseSkill(CraftingProcessor.NextRec.Action);
                }
                autoMode = value;
            }
        }
        public bool DisableFailurePrediction = false;
        public int MaxPercentage = 100;
        public bool UseTricksGood = false;
        public int MaxIQPrepTouch = 10;
        public bool UseMaterialMiracle = false;
        public bool MaterialMiracleMulti;
        public bool LowStatsMode = false;
        public bool UseTricksExcellent = false;
        public bool UseSpecialist = false;
        public bool ShowEHQ = true;
        public int CurrentSimulated = 0;
        public bool UseSimulatedStartingQuality = false;
        public bool DisableHighlightedAction = false;

        public ExpertSolverSettings ExpertSolverConfig = new();
        public MacroSolverSettings MacroSolverConfig = new();
        public ScriptSolverSettings ScriptSolverConfig = new();

        public Dictionary<uint, RecipeConfig> RecipeConfigs = new();

        public List<CraftingList> CraftingLists { get; set; } = new();
        public List<NewCraftingList> NewCraftingLists { get; set; } = new();

        public int AutoDelay = 0;
        public bool DelayRecommendation = false;
        public int RecommendationDelay = 0;
        public bool AbortIfNoFoodPot = false;
        public bool Repair = false;
        public bool PrioritizeRepairNPC = false;
        public bool DisableEnduranceNoRepair = false;
        public bool DisableListsNoRepair = false;
        public bool QuickSynthMode = false;
        public bool DisableToasts = false;
        public bool ShowOnlyCraftable = false;
        public bool ShowOnlyCraftableRetainers = false;
        public bool Materia = false;
        public bool LockMiniMenuR = true;

        public bool EnduranceStopFail = false;
        public bool EnduranceStopNQ = false;

        public int RepairPercent = 50;

        public Dictionary<ulong, ulong> RetainerIDs = new Dictionary<ulong, ulong>();
        public HashSet<ulong> UnavailableRetainerIDs = new HashSet<ulong>();

        [NonSerialized]
        public bool CraftingX = false;

        public bool MaxQuantityMode = false;
        public bool HideQuestHelper = false;
        public bool DisableTheme = true;
        public bool RequestToStopDuty = false;
        public bool RequestToResumeDuty = false;
        public int RequestToResumeDelay = 5;

        public bool UseConsumablesTrial = false;
        public bool UseConsumablesQuickSynth = false;

        public bool PlaySoundFinishEndurance = false;
        public bool PlaySoundFinishList = false;

        public float SoundVolume = 0.25f;

        public bool DefaultListMateria = false;
        public bool DefaultListSkip = false;
        public bool DefaultListSkipLiteral = false;
        public bool DefaultListRepair = false;
        public int DefaultListRepairPercent = 50;
        public bool DefaultListQuickSynth = false;
        public bool ResetTimesToAdd = false;
        public bool SkipMacroStepIfUnable = false;
        public bool DisableAllaganTools = false;
        public bool DisableMacroArtisanRecommendation = false;
        public bool UseQualityStarter = false;
        public bool ShowMacroAssignResults = false;
        public bool HideContextMenus = false;
        public int ContextMenuLoops = 1;
        public float ListCraftThrottle2 = 1f;

        public bool DefaultHideInventoryColumn = false;
        public bool DefaultHideRetainerColumn = false;
        public bool DefaultHideRemainingColumn = false;
        public bool DefaultHideCraftableColumn = false;
        public bool DefaultHideCraftableCountColumn = false;
        public bool DefaultHideCraftItemsColumn = false;
        public bool DefaultHideCategoryColumn = false;
        public bool DefaultHideGatherLocationColumn = false;
        public bool DefaultHideIdColumn = false;

        public bool DefaultColourValidation = false;
        public bool DefaultHQCrafts = false;

        public int ListOpacity = 100;

        public bool UseUniversalis = false;
        public bool LimitUnversalisToDC = false;
        public bool UniversalisOnDemand = false;

        public int SolverCollectibleMode = 3;
        public ItemFilter ShowItemsV1 { get; set; } = ItemFilter.All;

        public bool PinMiniMenu = false;

        public bool DontEquipItems = false;

        [NonSerialized]
        public int CraftX = 0;

        [NonSerialized]
        private bool autoMode = false;

        public bool ViewedEnduranceMessage = false;

        public float SimulatorActionSize = 40f;
        public bool SimulatorHoverMode = true;
        public bool HideRecipeWindowSimulator = false;
        public bool DisableSimulatorActionTooltips = false;

        public bool ReplaceSearch = true;
        public bool UsingDiscordHooks;
        public string? DiscordWebhookUrl;
        public RaphaelSolverSettings RaphaelSolverConfig = new();
        public ConcurrentDictionary<string, MacroSolverSettings.Macro> RaphaelSolverCacheV2 = [];

        public void Save()
        {
            Svc.PluginInterface.SavePluginConfig(this);
        }

        public static Configuration Load()
        {
            var fallback = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new();
            try
            {
                var contents = File.ReadAllText(Svc.PluginInterface.ConfigFile.FullName);
                var json = JObject.Parse(contents);
                var version = (int?)json["Version"] ?? 0;
                ConvertConfig(json, version);
                return json.ToObject<Configuration>() ?? new();
            }
            catch (Exception e)
            {
                Svc.Log.Error($"Failed to load config from {Svc.PluginInterface.ConfigFile.FullName}: {e}");
                return fallback;
            }
        }

        private static void ConvertConfig(JObject json, int version)
        {
            if (version <= 0)
            {
                var userMacros = json["UserMacros"] as JArray;
                if (userMacros != null)
                {
                    foreach (var m in userMacros)
                    {
                        m["Options"] = m["MacroOptions"];
                        var actions = m["MacroActions"] as JArray;
                        if (actions != null)
                        {
                            var stepopts = m["MacroStepOptions"] as JArray;
                            var steps = new JArray();
                            for (int i = 0; i < actions.Count; ++i)
                            {
                                var step = stepopts != null && i < stepopts.Count ? stepopts[i] : new JObject();
                                step["Action"] = actions[i];
                                steps.Add(step);
                            }
                            m["Steps"] = steps;
                        }
                    }
                    json["MacroSolverConfig"] = new JObject() { { "Macros", userMacros } };
                }

                var irm = json["IRM"] as JObject;
                if (irm != null)
                {
                    var cvt = new JObject();
                    foreach (var (k, v) in irm)
                    {
                        if (k == "$type")
                            continue;
                        var c = new JObject();
                        c["SolverType"] = typeof(MacroSolverDefinition).FullName;
                        c["SolverFlavour"] = v;
                        cvt[k] = c;
                    }
                    json["RecipeConfigs"] = cvt;
                }
            }
        }
    }
}
