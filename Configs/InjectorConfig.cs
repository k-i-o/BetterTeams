namespace BetterTeams.Configs
{
    public class InjectorConfig
    {
        public string TeamsExePath { get; set; } = string.Empty;
        public int RemoteDebuggingPort { get; set; } = 9222;
        public bool EnableLogging { get; set; } = true;
        public int InitialDelayMs { get; set; } = 10000;
        public int ReinjectDelayMs { get; set; } = 2000;
        public string ScriptsDirectory { get; set; } = "scripts";
    }
}
