using Microsoft.Win32;
using MsTeamsInjector;
using TeamsInjector.Configs;

namespace TeamsInjector
{
    /// <summary>
    /// Enhanced discovery of the Microsoft Teams executable path by scanning multiple likely locations,
    /// including per-user installs, Program Files, WindowsApps, and registry entries.
    /// </summary>
    public static class TeamsPathHelper
    {
        private static readonly string[] WellKnownPaths = [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Teams", "current", "Teams.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Teams", "current", "Teams.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Teams", "current", "Teams.exe")
        ];

        public static void DiscoverTeamsPathIfMissing(InjectorConfig config)
        {
            if (!string.IsNullOrEmpty(config.TeamsExePath) && File.Exists(config.TeamsExePath))
            {
                return;
            }

            foreach (string candidate in WellKnownPaths)
            {
                if (File.Exists(candidate))
                {
                    config.TeamsExePath = candidate;
                    Log.Success($"Discovered Teams path: {candidate}");
                    return;
                }
            }

            string windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
            try
            {
                if (Directory.Exists(windowsApps))
                {
                    string[] dirs = Directory.GetDirectories(windowsApps, "MSTeams_*", SearchOption.TopDirectoryOnly);
                    foreach (var dir in dirs)
                    {
                        string exe = Path.Combine(dir, "ms-teams.exe");
                        if (File.Exists(exe))
                        {
                            config.TeamsExePath = exe;
                            Log.Success($"Discovered Teams path in WindowsApps: {exe}");
                            return;
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Warning($"Cannot access WindowsApps folder: {ex.Message}");
            }

            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\Teams"))
                {
                    string? installPath = key?.GetValue("InstallPath") as string;
                    if (!string.IsNullOrEmpty(installPath))
                    {
                        string exe = Path.Combine(installPath, "Teams.exe");
                        if (File.Exists(exe))
                        {
                            config.TeamsExePath = exe;
                            Log.Success($"Discovered Teams path via registry: {exe}");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Registry lookup failed: {ex.Message}");
            }

            // 4) Fallback: prompt the user
            Log.Info("Enter full path to Teams.exe:");
            string input = Console.ReadLine()?.Trim('"', ' ') ?? string.Empty;
            if (File.Exists(input) && Path.GetFileName(input).Equals("Teams.exe", StringComparison.OrdinalIgnoreCase))
            {
                config.TeamsExePath = input;
                Log.Success($"Set Teams path from user input: {input}");
            }
            else
            {
                Log.Error("Invalid path, please configure manually.");
            }
        }
    }

}
