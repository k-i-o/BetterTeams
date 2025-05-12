using Microsoft.Win32;
using MsTeamsInjector;
using System.Diagnostics;
using System.Text.Json;
using TeamsInjector.Configs;

namespace TeamsInjector
{
    /// <summary>
    /// Enhanced discovery of the Microsoft Teams executable path by scanning multiple likely locations,
    /// including per-user installs, Program Files, WindowsApps, and registry entries.
    /// Also ensures the Teams configuration.json is writable and enables the dev menu flag.
    /// </summary>
    public static class TeamsPathHelper
    {
        private static readonly string[] WellKnownPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Teams", "current", "Teams.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Teams", "current", "Teams.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Teams", "current", "Teams.exe")
        };

        public static void DiscoverTeamsPathIfMissing(InjectorConfig config)
        {
            if (!string.IsNullOrEmpty(config.TeamsExePath) && File.Exists(config.TeamsExePath))
                return;

            // 1) Check well-known install locations
            //foreach (string candidate in WellKnownPaths)
            //{
            //    if (File.Exists(candidate))
            //    {
            //        config.TeamsExePath = candidate;
            //        Log.Success($"Discovered Teams path: {candidate}");
            //        TryEnableDevMenu(candidate);
            //        return;
            //    }
            //}

            // 2) Search in WindowsApps
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
                            TryEnableDevMenu(dir);
                            return;
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Warning($"Cannot access WindowsApps folder: {ex.Message}");
            }

            // 3) Registry lookup
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
                            TryEnableDevMenu(installPath);
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
                TryEnableDevMenu(Path.GetDirectoryName(input)!);
            }
            else
            {
                Log.Error("Invalid path, please configure manually.");
            }
        }

        private static void TryEnableDevMenu(string installFolder)
        {
            try
            {
                // Locate configuration.json under the install folder
                string configJson = Path.Combine(installFolder, "configuration.json");
                if (!File.Exists(configJson))
                {
                    Log.Warning($"configuration.json not found at: {configJson}");
                    return;
                }

                // Take ownership and grant full control
                Process.Start(new ProcessStartInfo("takeown", $"/F \"{configJson}\"")
                {
                    UseShellExecute = false,
                    Verb = "runas",
                    CreateNoWindow = true
                })?.WaitForExit();

                Process.Start(new ProcessStartInfo("icacls", $"\"{configJson}\" /grant %USERNAME%:F")
                {
                    UseShellExecute = false,
                    Verb = "runas",
                    CreateNoWindow = true
                })?.WaitForExit();

                // Load, modify JSON
                string jsonText = File.ReadAllText(configJson);
                using JsonDocument doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement.Clone();
                using var ms = new MemoryStream();
                using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    foreach (var property in root.EnumerateObject())
                    {
                        property.WriteTo(writer);
                    }
                    // Add devMenu flag if missing
                    if (!root.TryGetProperty("core/devMenuEnabled", out _))
                    {
                        writer.WriteBoolean("core/devMenuEnabled", true);
                        Log.Success("Enabled core/devMenuEnabled in configuration.json");
                    }
                    writer.WriteEndObject();
                }

                File.WriteAllBytes(configJson, ms.ToArray());
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to enable dev menu: {ex.Message}");
            }
        }
    }
}
