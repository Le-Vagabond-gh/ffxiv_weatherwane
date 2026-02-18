using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace weatherwane
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public HashSet<uint> SelectedZones { get; set; } = new();
        public int ForecastCount { get; set; } = 4;
        public bool LockWindow { get; set; } = false;
        public bool AutoResize { get; set; } = true;
        public bool ShowBackground { get; set; } = true;

        [NonSerialized]
        private IDalamudPluginInterface? PluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.PluginInterface!.SavePluginConfig(this);
        }
    }
}
