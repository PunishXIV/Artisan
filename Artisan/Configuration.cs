using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace Artisan
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool AutoCraft { get; set; } = false;
        public bool AutoMode { get; set; } = false;

        public bool DisableFailurePrediction { get; set; } = false;
        public int MaxPercentage { get; internal set; } = 100;

        // the below exist just to make saving less cumbersome

        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

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
