using System.Collections.Generic;

namespace BetterTeams.Configs
{
    public class PluginConfig
    {
        public List<string> DeactivatedPluginIds { get; set; } = new List<string>();
        
        public bool IsPluginActive(string pluginId)
        {
            return !DeactivatedPluginIds.Contains(pluginId);
        }
        
        public void DeactivatePlugin(string pluginId)
        {
            if (!DeactivatedPluginIds.Contains(pluginId))
            {
                DeactivatedPluginIds.Add(pluginId);
            }
        }
        
        public void ActivatePlugin(string pluginId)
        {
            DeactivatedPluginIds.Remove(pluginId);
        }
    }
} 