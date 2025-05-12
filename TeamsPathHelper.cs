using Microsoft.Win32;
using System.Diagnostics;
using System.Text.Json;
using BetterTeams.Configs;
using System.Security.AccessControl;
using System.Security.Principal;

namespace BetterTeams
{

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
            {
                TryEnableDevMenu(config.TeamsExePath);
                return;
            }

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

            string windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
            try
            {
                PrivilegeHelper.EnableTakeOwnership();
                Log.Info("Privilege SeTakeOwnership enabled before scanning WindowsApps");

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


            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\Teams"))
                {
                    string? installPath = key?.GetValue("InstallPath") as string;
                    if (!string.IsNullOrEmpty(installPath))
                    {
                        string exe = Path.Combine(installPath, "ms-teams.exe");
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

            Log.Info("Enter full path to ms-teams.exe:");
            string input = Console.ReadLine() ?? string.Empty;
            if (File.Exists(input))
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

        private static string? GetInstallFolder(string exePath)
        {
            if (string.IsNullOrEmpty(exePath))
                return null;

            if (Directory.Exists(exePath))
                return exePath;

            if (File.Exists(exePath))
                return Path.GetDirectoryName(exePath);

            return null;
        }

        private static void TryEnableDevMenu(string exe)
        {
            string? installFolder = GetInstallFolder(exe);
            if (string.IsNullOrEmpty(installFolder)) throw new Exception("Failed to get install folder");

            string configJson = Path.Combine(installFolder, "configuration.json");
            if (!File.Exists(configJson))
            {
                Log.Warning($"configuration.json not found at: {configJson}");
                return;
            }

            try
            {
                // 1) Enable take-ownership and security privileges
                PrivilegeHelper.EnableTakeOwnership();
                Log.Success("SeTakeOwnershipPrivilege enabled on token");

                // 2) Now use FileSecurity to set owner (Administrators) and grant yourself FullControl
                var fileInfo = new FileInfo(configJson);
                var security = fileInfo.GetAccessControl(AccessControlSections.All);

                var adminsSid = new SecurityIdentifier(
                    WellKnownSidType.BuiltinAdministratorsSid, null);
                security.SetOwner(adminsSid);

                var currentUser = new NTAccount(
                    Environment.UserDomainName + "\\" + Environment.UserName)
                    .Translate(typeof(SecurityIdentifier)) as SecurityIdentifier;

                var rule = new FileSystemAccessRule(
                    currentUser!,
                    FileSystemRights.FullControl,
                    InheritanceFlags.None,
                    PropagationFlags.None,
                    AccessControlType.Allow);

                security.AddAccessRule(rule);
                fileInfo.SetAccessControl(security);

                Log.Success("Ownership and permissions applied via FileSecurity API");

                // 3) Modify JSON as before
                string jsonText = File.ReadAllText(configJson);
                using var doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement.Clone();
                using var ms = new MemoryStream();
                using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    foreach (var prop in root.EnumerateObject())
                        prop.WriteTo(writer);
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

            Log.Success($"{exe} dev menu enabled. Restart Teams to apply changes.");
        }

    }
}
