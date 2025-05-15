namespace BetterTeams.Configs
{
    public class InjectorConfig
    {
        public bool FirstTime = true;
        public string TeamsExePath { get; set; } = string.Empty;
        public int RemoteDebuggingPort { get; set; } = 9222;
        public bool EnableLogging { get; set; } = true;
        public int InitialDelayMs { get; set; } = 5000;
        public int ReInjectDelayMs { get; set; } = 2000;
        public string ScriptsDirectory { get; set; } = "scripts";
        public int WebSocketPort { get; set; } = 8097;
        public string MarketplaceApiUrl { get; set; } = "https://api.kiocode.com/api/betterteams";
        public bool AutoCheckUpdates { get; set; } = true;
        public string ActiveThemeId { get; set; } = string.Empty;
    }
}
