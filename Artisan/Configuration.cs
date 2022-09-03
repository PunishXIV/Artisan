using Artisan.CraftingLogic;
using Artisan.MacroSystem;
using Artisan.RawInformation;
using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace Artisan
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool AutoCraft { get; set; } = false;
        public bool AutoMode
        {
            get => autoMode; set
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

        public List<Macro> UserMacros { get; set; } = new();
        public bool UseMacroMode { get; set; }
        public Macro? SetMacro { get; set; }

        public uint Food = 0;
        public uint Potion = 0;
        public bool AbortIfNoFoodPot = true;
        public bool FoodHQ = true;
        public bool PotHQ = true;
        public bool Repair = false;
        public int RepairPercent = 50;
        public int CraftX = 0;
        public bool CraftingX = false;



        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;
        private bool autoMode = false;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }
    }
}
