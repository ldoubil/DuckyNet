using System.Collections.Generic;

namespace DuckyNet.Server.Plugin
{
    public class PluginEntry
    {
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }

    public class PluginConfiguration
    {
        public List<PluginEntry> CorePlugins { get; set; } = new List<PluginEntry>();
        public List<PluginEntry> ModulePlugins { get; set; } = new List<PluginEntry>();
        public List<PluginEntry> WebPlugins { get; set; } = new List<PluginEntry>();

        public static PluginConfiguration CreateDefault()
        {
            return new PluginConfiguration
            {
                CorePlugins = new List<PluginEntry>
                {
                    new PluginEntry { Name = "CorePlugin", Enabled = true }
                },
                ModulePlugins = new List<PluginEntry>
                {
                    new PluginEntry { Name = "PlayerModule", Enabled = true },
                    new PluginEntry { Name = "RoomModule", Enabled = true },
                    new PluginEntry { Name = "SceneModule", Enabled = true },
                    new PluginEntry { Name = "NpcModule", Enabled = true },
                    new PluginEntry { Name = "SyncModule", Enabled = true }
                },
                WebPlugins = new List<PluginEntry>
                {
                    new PluginEntry { Name = "WebPlugin", Enabled = true }
                }
            };
        }
    }
}
