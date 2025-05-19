using BetterTeams;
using BetterTeams.Configs;
using Microsoft.Playwright;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace MsTeamsInjector
{
    class Program
    {
        private const string ConfigFileName = "BetterMsTeamsConfig.json";
        private const string PluginConfigFileName = "BetterMsTeamsPluginConfig.json";

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOMOVE = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        private static InjectorConfig _config = new();
        private static PluginConfig _pluginConfig = new();
        private static PluginManager _pluginManager;
        private static WebSocketServer _webSocketServer;

        static async Task Main(string[] args)
        {
            Console.Clear();

            Log.Info($"The application is in admin mode: {IsElevated()}");

            _config = LoadOrCreateConfig();
            _pluginConfig = LoadOrCreatePluginConfig();
            TeamsPathHelper.DiscoverTeamsPathIfMissing(_config);
            SaveConfig();

            string scriptsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _config.ScriptsDirectory);
            _pluginManager = new PluginManager(scriptsDir, _pluginConfig);
            
            _webSocketServer = new WebSocketServer(_pluginManager, _config);
            
            _ = Task.Run(async () => {
                try {
                    await _webSocketServer.Start();
                } catch (Exception ex) {
                    Log.Error($"WebSocket server error: {ex.Message}");
                }
            });
            
            KillTeamsProcess();
            LaunchTeams();

            await Task.Delay(_config.InitialDelayMs);
            await InjectScriptsIntoAllPages();

            while (true)
            {
                Console.Write("> ");
                string input = Console.ReadLine()?.Trim() ?? string.Empty;
                string[] parts = input.Split(' ', 2);
                switch (parts[0].ToLower())
                {
                    case "help":
                        Console.WriteLine("Available commands:");
                        Console.WriteLine("  help - Show this help message");
                        Console.WriteLine("  reinject - Reinject scripts into all pages");
                        Console.WriteLine("  restart - Restart teams reinjecting all");
                        Console.WriteLine("  marketplace - List plugins and themes from marketplace");
                        Console.WriteLine("  plugins - List installed plugins");
                        Console.WriteLine("  install_plugin <id> - Install a plugin");
                        Console.WriteLine("  uninstall_plugin <id> - Uninstall a plugin");
                        Console.WriteLine("  activate_plugin <id> - Activate a plugin");
                        Console.WriteLine("  themes - List installed themes");
                        Console.WriteLine("  install_theme <id> - Install a theme");
                        Console.WriteLine("  uninstall_theme <id> - Uninstall a theme");
                        Console.WriteLine("  deactivate_plugin <id> - Deactivate a plugin");
                        Console.WriteLine("  activate_theme <id> - Activate a theme");
                        Console.WriteLine("  deactivate_theme - Deactivate the current theme");
                        Console.WriteLine("  exit - Exit the program");
                        break;
                    case "reinject":
                        Log.Info("Reinjecting scripts...");
                        await Task.Delay(_config.ReInjectDelayMs);
                        await InjectScriptsIntoAllPages();
                        break;
                    case "restart":
                        Log.Info("Restarting Teams and reinjecting scripts...");
                        KillTeamsProcess();
                        LaunchTeams();
                        await Task.Delay(_config.InitialDelayMs);
                        await InjectScriptsIntoAllPages();
                        break;
                    case "kill":
                        KillTeamsProcess();
                        break;
                    case "launch":
                        LaunchTeams();
                        break;
                    case "marketplace":
                        var plugins = await _pluginManager.GetAvailablePlugins();
                        if (plugins.Count == 0)
                        {
                            Log.Info("No plugins found in the marketplace.");
                        }
                        else
                        {
                            Log.Info("Marketplace Plugins:");
                            foreach (var plugin in plugins)
                            {
                                Log.Info($"  [{plugin.Id}] {plugin.Name} v{plugin.Version} - {plugin.Description}");
                            }
                        }

                        var themes = await _pluginManager.GetAvailableThemes();
                        if (themes.Count == 0)
                        {
                            Log.Info("No themes found in the marketplace.");
                        }
                        else
                        {
                            Log.Info("Marketplace Themes:");
                            foreach (var theme in themes)
                            {
                                Log.Info($"  [{theme.Id}] {theme.Name} v{theme.Version} - {theme.Description}");
                            }
                        }
                        break;
                    case "plugins":
                        ListInstalledPlugins();
                        break;
                    case "themes":
                        ListInstalledThemes();
                        break;
                    case "install_plugin":
                        if (parts.Length > 1)
                        {
                            await InstallPlugin(parts[1]);
                            await Task.Delay(_config.ReInjectDelayMs);
                            await InjectScriptsIntoAllPages();
                        }
                        else
                        {
                            Log.Error("Plugin ID required");
                        }
                        break;
                    case "install_theme":
                        if (parts.Length > 1)
                        {
                            await InstallTheme(parts[1]);
                            await Task.Delay(_config.ReInjectDelayMs);
                            await InjectScriptsIntoAllPages();
                        }
                        else
                        {
                            Log.Error("Theme ID required");
                        }
                        break;
                    case "uninstall_plugin":
                        if (parts.Length > 1)
                        {
                            UninstallPlugin(parts[1]);
                            await Task.Delay(_config.ReInjectDelayMs);
                            await InjectScriptsIntoAllPages();
                        }
                        else
                        {
                            Log.Error("Plugin ID required");
                        }
                        break;
                    case "uninstall_theme":
                        if (parts.Length > 1)
                        {
                            UninstallTheme(parts[1]);
                            await Task.Delay(_config.ReInjectDelayMs);
                            await InjectScriptsIntoAllPages();
                        }
                        else
                        {
                            Log.Error("Theme ID required");
                        }
                        break;
                    case "activate_plugin":
                        if (parts.Length > 1)
                        {
                            await ActivatePlugin(parts[1]);
                            await Task.Delay(_config.ReInjectDelayMs);
                            await InjectScriptsIntoAllPages();
                        }
                        else
                        {
                            Log.Error("Plugin ID required");
                        }
                        break;
                    case "deactivate_plugin":
                        if (parts.Length > 1)
                        {
                            await DeactivatePlugin(parts[1]);
                            await Task.Delay(_config.ReInjectDelayMs);
                            await InjectScriptsIntoAllPages();
                        }
                        else
                        {
                            Log.Error("Plugin ID required");
                        }
                        break;
                    case "activate_theme":
                        if (parts.Length > 1)
                        {
                            await ActivateTheme(parts[1]);
                        }
                        else
                        {
                            Log.Error("Theme ID required");
                        }
                        break;
                    case "deactivate_theme":
                        await DeactivateTheme();
                        break;
                    case "exit":
                        Log.Info("Exiting...");

                        if (_webSocketServer != null)
                        {
                            _webSocketServer.Stop();
                        }
                        return;
                    default:
                        Log.Warning($"Unknown command: {parts[0]}");
                        break;
                }
            }
        }

        private static async Task ActivateTheme(string themeId)
        {
            var themes = _pluginManager.GetInstalledThemes();
            var theme = themes.Find(t => t.Id == themeId);
            
            if (theme != null)
            {
                _config.ActiveThemeId = themeId;
                SaveConfig();
                
                Log.Success($"Theme {theme.Name} activated");
            }
            else
            {
                Log.Error($"Theme {themeId} not found");
            }
        }

        private static async Task DeactivateTheme()
        {
            _config.ActiveThemeId = string.Empty;
            SaveConfig();
            
            Log.Success("Theme deactivated");
        }

        private static void ListInstalledPlugins()
        {
            var plugins = _pluginManager.GetInstalledPlugins();
            
            if (plugins.Count == 0)
            {
                Log.Info("No plugins installed");
                return;
            }
            
            Console.WriteLine("\nInstalled Plugins:");
            Console.WriteLine("------------------");
            
            foreach (var plugin in plugins)
            {
                string status = plugin.IsActive ? "Active" : "Inactive";
                Console.WriteLine($"ID: {plugin.Id}");
                Console.WriteLine($"Name: {plugin.Name}");
                Console.WriteLine($"Description: {plugin.Description}");
                Console.WriteLine($"Version: {plugin.Version}");
                Console.WriteLine($"Author: {plugin.Author}");
                Console.WriteLine($"Status: {status}");
                Console.WriteLine("------------------");
            }
        }

        private static void ListInstalledThemes()
        {
            var themes = _pluginManager.GetInstalledThemes();
            if (themes.Count == 0)
            {
                Log.Info("No themes installed");
                return;
            }

            Log.Info("Installed themes:");
            foreach (var theme in themes)
            {
                string activeMarker = theme.Id == _config.ActiveThemeId ? " ~ACTIVE~" : "";
                Log.Info($"  {activeMarker} [{theme.Id}] {theme.Name} v{theme.Version} - {theme.Description}");
            }
        }

        private static async Task InstallPlugin(string pluginId)
        {
            Log.Info($"Installing plugin: {pluginId}");
            bool success = await _pluginManager.InstallPlugin(pluginId);
            if (success)
            {
                Log.Success($"Plugin {pluginId} installed successfully");
            }
            else
            {
                Log.Error($"Failed to install plugin {pluginId}");
            }
        }

        private static async Task InstallTheme(string themeId)
        {
            Log.Info($"Installing theme: {themeId}");
            bool success = await _pluginManager.InstallTheme(themeId);
            if (success)
            {
                Log.Success($"Theme {themeId} installed successfully");
            }
            else
            {
                Log.Error($"Failed to install theme {themeId}");
            }
        }

        private static void UninstallPlugin(string pluginId)
        {
            Log.Info($"Uninstalling plugin: {pluginId}");
            bool success = _pluginManager.UninstallPlugin(pluginId);
            if (success)
            {
                Log.Success($"Plugin {pluginId} uninstalled successfully");
            }
            else
            {
                Log.Error($"Failed to uninstall plugin {pluginId}");
            }
        }

        private static void UninstallTheme(string themeId)
        {
            Log.Info($"Uninstalling theme: {themeId}");
            bool success = _pluginManager.UninstallTheme(themeId);
            if (success)
            {
                if (_config.ActiveThemeId == themeId)
                {
                    _config.ActiveThemeId = string.Empty;
                    SaveConfig();
                }
                
                Log.Success($"Theme {themeId} uninstalled successfully");
            }
            else
            {
                Log.Error($"Failed to uninstall theme {themeId}");
            }
        }

        public static bool IsElevated()
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static InjectorConfig LoadOrCreateConfig()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterMsTeams");
            string path = Path.Combine(dir, ConfigFileName);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                InjectorConfig cfg = JsonSerializer.Deserialize<InjectorConfig>(json) ?? new InjectorConfig();
                cfg.FirstTime = false;
                SaveConfig();
                return cfg;
            }
            Directory.CreateDirectory(dir);
            return new InjectorConfig();
        }

        private static PluginConfig LoadOrCreatePluginConfig()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PluginConfigFileName);
            
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<PluginConfig>(json);
                    
                    if (config != null)
                    {
                        Log.Success("Plugin config loaded successfully");
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error loading plugin config: {ex.Message}");
                }
            }
            
            Log.Warning("Creating new plugin config");
            return new PluginConfig();
        }

        private static void SaveConfig()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterMsTeams");
            string path = Path.Combine(dir, ConfigFileName);
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private static void SavePluginConfig()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PluginConfigFileName);
            
            try
            {
                JsonSerializerOptions options = new()
                {
                    WriteIndented = true
                };
                
                string json = JsonSerializer.Serialize(_pluginConfig, options);
                File.WriteAllText(configPath, json);
                
                Log.Success("Plugin config saved successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"Error saving plugin config: {ex.Message}");
            }
        }

        private static void KillTeamsProcess()
        {
            Process[] procs = Process.GetProcessesByName("ms-teams");
            if (procs.Length == 0)
            {
                procs = Process.GetProcessesByName("Teams");
                if (procs.Length == 0)
                {
                    Log.Info("No running Teams process found.");
                }
            }

            foreach (Process proc in procs)
            {
                Log.Warning($"Killing Teams process (ID: {proc.Id})");
                proc.Kill();
                proc.WaitForExit();
            }

            if(procs.Length != 0)
            {
                Log.Success("Teams processes terminated.");
            }
        }

        private static void LaunchTeams()
        {
            string args = "--remote-debugging-port=" + _config.RemoteDebuggingPort;
            if (_config.EnableLogging)
            {
                args += " --enable-logging";
            }

            ProcessStartInfo psi = new(_config.TeamsExePath, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            try
            {
                Process.Start(psi);
                Log.Success("Launched Teams with remote debugging.");
            }
            catch (Exception ex)
            {
                Log.Error("Launch failed: " + ex.Message);
            }
        }

        private static IntPtr GetWindowHandleByTitle(string title)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((IntPtr hWnd, IntPtr lParam) =>
            {
                StringBuilder sb = new(256);
                GetWindowText(hWnd, sb, sb.Capacity);
                if (sb.ToString().Contains(title, StringComparison.OrdinalIgnoreCase))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static void ResizeWindow(string title)
        {
            IntPtr hwnd = GetWindowHandleByTitle(title);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }
            if (GetWindowRect(hwnd, out RECT rect))
            {
                int width = rect.Right - rect.Left + 1;
                int height = rect.Bottom - rect.Top;
                SetWindowPos(hwnd, IntPtr.Zero, rect.Left, rect.Top, width, height, SWP_NOZORDER | SWP_NOMOVE);
            }
        }

        static int iPages;
        private static async Task InjectScriptsIntoAllPages()
        {
            IPlaywright playwright = await Playwright.CreateAsync();
            string cdpUrl = $"http://127.0.0.1:{_config.RemoteDebuggingPort}";
            IBrowser browser = await playwright.Chromium.ConnectOverCDPAsync(cdpUrl);
            IBrowserContext context = browser.Contexts.ToList().FirstOrDefault() ?? await browser.NewContextAsync();

            List<IPage> pagesSnapshot = [.. context.Pages];

            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _config.ScriptsDirectory);
            string mainScript = Path.Combine(root, "betterteams-main.js");

            List<AddonInfo> installedPlugins = _pluginManager.GetInstalledPlugins();
            AddonInfo? activeTheme = _pluginManager.GetInstalledThemes().Where(t => t.Id.Equals(_config.ActiveThemeId, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            foreach (IPage page in pagesSnapshot)
            {
                await InjectMainAndExtensionsAsync(page, mainScript, installedPlugins, activeTheme, root);
            }

            ResizeWindow("Microsoft Teams");
            await browser.CloseAsync();
        }

        private static async Task InjectMainAndExtensionsAsync(IPage page, string mainScriptPath, List<AddonInfo> plugins, AddonInfo? activeTheme, string rootScriptsDir)
        {
            if (!File.Exists(mainScriptPath))
            {
                if (iPages == 1)
                {
                    Log.Error($"Main script missing: {mainScriptPath}");
                }
                return;
            }

            try
            {
                string mainCode = File.ReadAllText(mainScriptPath);
                await page.EvaluateAsync(mainCode);
            }
            catch (Exception ex)
            {
                if (iPages == 1)
                {
                    Log.Error($"Main script injection failed: {ex.Message}");
                }
            }

            foreach (AddonInfo? plugin in plugins.Where(p => p.IsActive))
            {
                string pluginDir = Path.Combine(rootScriptsDir, "plugins", plugin.Id);
                string mainJsPath = Path.Combine(pluginDir, "main.js");

                if (File.Exists(mainJsPath))
                {
                    try
                    {
                        string scriptContent = File.ReadAllText(mainJsPath);
                        await page.EvaluateAsync(scriptContent);
                        if (iPages == 1)
                        {
                            Log.Success($"Injected plugin: {plugin.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (iPages == 1)
                        {
                            Log.Error($"Failed to inject plugin {plugin.Name}: {ex.Message}");
                        }
                    }
                }
            }

            if (activeTheme is null) return;


            string themeDir = Path.Combine(rootScriptsDir, "themes", activeTheme.Id);
            string mainJsPathTheme = Path.Combine(themeDir, "main.js");

            if (File.Exists(mainJsPathTheme))
            {
                try
                {
                    string scriptContent = File.ReadAllText(mainJsPathTheme);
                    await page.EvaluateAsync(scriptContent);
                    if (iPages == 1)
                    {
                        Log.Success($"Injected theme: {activeTheme.Name}");
                    }
                }
                catch (Exception ex)
                {
                    if (iPages == 1)
                    {
                        Log.Error($"Failed to inject theme {activeTheme.Name}: {ex.Message}");
                    }
                }
            }

            //try
            //{
            //    string themeMgrCode = GenerateThemeManagerScript();
            //    await page.EvaluateAsync(themeMgrCode);

            //    if (iPages == 1)
            //    {
            //        Log.Success("Theme manager injected");
            //    }
            //}
            //catch (Exception ex)
            //{
            //    if (iPages == 1)
            //    {
            //        Log.Error($"Theme manager injection failed: {ex.Message}");
            //    }
            //}

            //var themesDir = Path.Combine(rootScriptsDir, "themes");
            //if (!Directory.Exists(themesDir)) return;

            //foreach (var themeDir in Directory.GetDirectories(themesDir))
            //{
            //    string themeId = new DirectoryInfo(themeDir).Name;
            //    string themeMainJs = Path.Combine(themeDir, "main.js");
            //    if (!File.Exists(themeMainJs)) continue;

            //    string themeCode = File.ReadAllText(themeMainJs);
            //    string wrapped = $@"
            //        (function() {{
            //            if (window.BetterTeamsThemeManager) {{
            //                window.BetterTeamsThemeManager.registerTheme(
            //                    '{themeId}',
            //                    () => {{ {themeCode} return true; }},
            //                    () => {{
            //                        document
            //                          .querySelectorAll('[data-betterteams-theme=""{themeId}""]')
            //                          .forEach(el => el.remove());
            //                        return true;
            //                    }}
            //                );
            //            }}
            //        }})();
            //    ";

            //    try
            //    {
            //        await page.EvaluateAsync(wrapped);
            //        if (iPages == 1)
            //        {
            //            Log.Success($"Theme registered: {themeId}");
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        if (iPages == 1)
            //        {
            //            Log.Error($"Registering theme {themeId} failed: {ex.Message}");
            //        }
            //    }
            //}

            //if (!string.IsNullOrEmpty(_config.ActiveThemeId))
            //{
            //    try
            //    {
            //        await page.EvaluateAsync($@"window.BetterTeamsThemeManager?.activateTheme('{_config.ActiveThemeId}');");

            //        if (iPages == 1)
            //        {
            //            Log.Success($"Activated theme: {_config.ActiveThemeId}");
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        if (iPages == 1)
            //        {
            //            Log.Error($"Activating theme {_config.ActiveThemeId} failed: {ex.Message}");
            //        }
            //    }
            //}
        }

        private static string GenerateThemeManagerScript()
        {
            return @"
(function() {
    if (window.BetterTeamsThemeManager) {
        console.log('BetterTeams Theme Manager already initialized');
        return;
    }

    class ThemeManager {
        constructor() {
            this.themes = {};
            this.activeTheme = null;
            this.setupWebSocketListeners();
        }

        registerTheme(themeId, applyFn, removeFn) {
            this.themes[themeId] = {
                apply: applyFn,
                remove: removeFn
            };
            console.log(`Theme ${themeId} registered`);
        }

        activateTheme(themeId) {
            if (this.activeTheme) {
                this.deactivateTheme();
            }

            const theme = this.themes[themeId];
            if (theme) {
                console.log(`Activating theme: ${themeId}`);
                const success = theme.apply();
                if (success) {
                    this.activeTheme = themeId;
                    console.log(`Theme ${themeId} activated successfully`);
                    return true;
                } else {
                    console.error(`Failed to activate theme ${themeId}`);
                }
            } else {
                console.error(`Theme ${themeId} not found`);
            }
            return false;
        }

        deactivateTheme() {
            if (!this.activeTheme) {
                return true;
            }

            const theme = this.themes[this.activeTheme];
            if (theme) {
                console.log(`Deactivating theme: ${this.activeTheme}`);
                const success = theme.remove();
                if (success) {
                    console.log(`Theme ${this.activeTheme} deactivated successfully`);
                    this.activeTheme = null;
                    return true;
                } else {
                    console.error(`Failed to deactivate theme ${this.activeTheme}`);
                }
            }
            return false;
        }

    }

    window.BetterTeamsThemeManager = new ThemeManager();
    console.log('BetterTeams Theme Manager initialized');
})();";
        }

        private static async Task ActivatePlugin(string pluginId)
        {
            bool success = _pluginManager.ActivatePlugin(pluginId);
            if (success)
            {
                Log.Success($"Plugin {pluginId} activated successfully");
                SavePluginConfig();
                Log.Info("Reinjecting scripts for changes to take effect...");
                await InjectScriptsIntoAllPages();
            }
            else
            {
                Log.Error($"Failed to activate plugin {pluginId}");
            }
        }

        private static async Task DeactivatePlugin(string pluginId)
        {
            bool success = _pluginManager.DeactivatePlugin(pluginId);
            if (success)
            {
                Log.Success($"Plugin {pluginId} deactivated successfully");
                SavePluginConfig();
                Log.Info("Reinjecting scripts for changes to take effect...");
                await InjectScriptsIntoAllPages();
            }
            else
            {
                Log.Error($"Failed to deactivate plugin {pluginId}");
            }
        }
    }
}