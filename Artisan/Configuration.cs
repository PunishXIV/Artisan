using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.MacroSystem;
using Artisan.RawInformation;
using Artisan.UI.Tables;
using Dalamud.Configuration;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;

namespace Artisan
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public bool AutoMode
        {
            get => autoMode; 
            set
            {
                if (value)
                {
                    Hotbars.ExecuteRecommended(CurrentCraft.CurrentRecommendation);
                }
                autoMode = value;
            }
        }
        public bool DisableFailurePrediction { get; set; } = false;
        public int MaxPercentage { get; set; } = 100;

        public bool UseTricksGood { get; set; } = false;

        public bool UseTricksExcellent { get; set; } = false;
        public bool UseSpecialist { get; set; } = false;

        public bool ShowEHQ { get; set; } = true;

        public int CurrentSimulated { get; set; } = 0;

        public bool UseSimulatedStartingQuality { get; set; } = false;
        public bool DisableHighlightedAction { get; set; } = false;

        public List<Macro> UserMacros { get; set; } = new();
        public List<CraftingList> CraftingLists { get; set; } = new();
        public Dictionary<uint, int> IRM { get; set; } = new();

        public int AutoDelay { get; set; } = 0;

        public uint Food = 0;
        public uint Potion = 0;
        public uint Manual = 0;
        public uint SquadronManual = 0;
        public bool AbortIfNoFoodPot { get; set; } = false;
        public bool FoodHQ = true;
        public bool PotHQ = true;
        public bool Repair { get; set; } = false;
        public bool QuickSynthMode = false;
        public bool DisableToasts { get; set; } = false;
        public bool ShowOnlyCraftable { get; set; } = false;
        public bool ShowOnlyCraftableRetainers { get;set; } = false;
        public bool DisableMiniMenu { get; set; } = false;
        public bool Materia { get; set; } = false;
        public bool LockMiniMenu { get; set; } = false;
        public bool DelayRecommendation { get; set; }

        public int RecommendationDelay { get; set; } = 0;
        public bool EnduranceStopFail { get; set; } = false;
        public bool EnduranceStopNQ { get; set; } = false;

        public int RepairPercent = 50;

        public Dictionary<ulong, ulong> RetainerIDs = new Dictionary<ulong, ulong>();
        public HashSet<ulong> UnavailableRetainerIDs = new HashSet<ulong>();

        [NonSerialized]
        public bool CraftingX = false;

        public bool MaxQuantityMode = false;
        public bool HideQuestHelper { get; set; } = false;
        public bool DisableTheme { get; set; } = true;
        public bool RequestToStopDuty { get; set; } = false;
        public bool RequestToResumeDuty { get; set; } = false;
        public int RequestToResumeDelay { get; set; } = 5;

        public bool PlaySoundFinishEndurance = false;
        public bool PlaySoundFinishList = false;

        public float SoundVolume = 0.25f;

        public bool DefaultListMateria = false;   
        public bool DefaultListSkip = false;
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
        public float ListCraftThrottle = 1f;

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

        public bool UseUniversalis = false;
        public bool UniversalisDataCenter = false;

        public int SolverCollectibleMode = 3;
        public ItemFilter ShowItemsV1 { get; set; } = ItemFilter.All;

        public bool DontEquipItems = false;

        [NonSerialized]
        public int CraftX = 0;

        [NonSerialized]
        private bool autoMode = false;

        public bool ViewedEnduranceMessage = false;

        public bool DisableSTMessage = false;   

        public void Save()
        {
            Svc.PluginInterface.SavePluginConfig(this);
        }
    }
}
