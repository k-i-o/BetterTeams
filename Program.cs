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
        private static PluginManager _pluginManager;

        static async Task Main(string[] args)
        {
            Console.Clear();

            Log.Info($"The application is in admin mode: {IsElevated()}");

            _config = LoadOrCreateConfig();
            TeamsPathHelper.DiscoverTeamsPathIfMissing(_config);
            SaveConfig();

            string scriptsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _config.ScriptsDirectory);
            _pluginManager = new PluginManager(scriptsDir);
            
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
                        //Console.WriteLine("  kill - Kill the Teams process");
                        //Console.WriteLine("  launch - Launch Teams with remote debugging");
                        Console.WriteLine("  reinject - Reinject scripts into all pages");
                        Console.WriteLine("  restart - Restart teams reinjecting all");
                        Console.WriteLine("  plugins - List installed plugins");
                        Console.WriteLine("  themes - List installed themes");
                        Console.WriteLine("  install_plugin <id> - Install a plugin");
                        Console.WriteLine("  install_theme <id> - Install a theme");
                        Console.WriteLine("  uninstall_plugin <id> - Uninstall a plugin");
                        Console.WriteLine("  uninstall_theme <id> - Uninstall a theme");
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
                
                await BroadcastToAllPages("theme_activated", new { ThemeId = themeId, ThemeName = theme.Name });
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
            
            // Broadcast to all pages
            await BroadcastToAllPages("theme_deactivated", new { });
            Log.Success("Theme deactivated");
        }

        private static async Task BroadcastToAllPages(string action, object data)
        {
            try
            {
                IPlaywright playwright = await Playwright.CreateAsync();
                string url = "http://127.0.0.1:" + _config.RemoteDebuggingPort;
                IBrowser browser = await playwright.Chromium.ConnectOverCDPAsync(url);
                IBrowserContext context = browser.Contexts.FirstOrDefault() ?? await browser.NewContextAsync();
                
                foreach (IPage page in context.Pages)
                {
                    await SendPageEvent(page, action, data);
                }
                
                await browser.CloseAsync();
            }
            catch (Exception ex)
            {
                Log.Error($"Error broadcasting to pages: {ex.Message}");
            }
        }

        private static void ListInstalledPlugins()
        {
            var plugins = _pluginManager.GetInstalledPlugins();
            if (plugins.Count == 0)
            {
                Log.Info("No plugins installed");
                return;
            }

            Log.Info("Installed plugins:");
            foreach (var plugin in plugins)
            {
                Log.Info($"  {plugin.Name} v{plugin.Version} - {plugin.Description}");
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
                await BroadcastToAllPages("plugin_installed", new { PluginId = pluginId });
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
                await BroadcastToAllPages("theme_installed", new { ThemeId = themeId });
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
                return cfg;
            }
            Directory.CreateDirectory(dir);
            return new InjectorConfig();
        }

        private static void SaveConfig()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterMsTeams");
            string path = Path.Combine(dir, ConfigFileName);
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
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

        private static async Task InjectScriptsIntoAllPages()
        {
            bool websocketServerInjected = true;
            bool mainScriptInjected = true;
            bool themesInjected = true;

            IPlaywright playwright = await Playwright.CreateAsync();
            string url = "http://127.0.0.1:" + _config.RemoteDebuggingPort;
            IBrowser browser = await playwright.Chromium.ConnectOverCDPAsync(url);
            IBrowserContext context = browser.Contexts.FirstOrDefault() ?? await browser.NewContextAsync();

            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _config.ScriptsDirectory);

            string wsConnectorScript = GenerateWebSocketConnectorScript(_config.WebSocketPort);
            foreach (IPage page in context.Pages)
            {
                try
                {
                    await page.EvaluateAsync(wsConnectorScript);

                    await page.ExposeBindingAsync("BetterTeamsActionCallbackAsync", (BindingSource source, string actionJson) =>
                    {
                        try
                        {
                            var action = JsonSerializer.Deserialize<WebSocketMessage>(actionJson);
                            if (action != null)
                            {
                                return HandlePageAction(page, action);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Error processing page action: {ex.Message}");
                        }
                        return Task.CompletedTask;
                    });

                    await page.EvaluateAsync(@"
                            window.BetterTeamsActionHandler = (action, data) => {
                                window.BetterTeamsActionCallbackAsync(JSON.stringify({
                                    Action: action,
                                    Data: data || {}
                                }));
                            };
                        ");

                    await page.ExposeBindingAsync("notifyBridgeReadyAsync", (BindingSource source) =>
                    {
                        Log.Success("BetterTeams bridge connected");
                        return Task.CompletedTask;
                    });

                    await page.EvaluateAsync(@"
                            window.notifyBridgeReady = () => {
                                if (window.notifyBridgeReadyAsync) {
                                    window.notifyBridgeReadyAsync();
                                    console.log('BetterTeams bridge is ready');
                                }
                            };
                        ");


                }
                catch (Exception ex)
                {
                    Log.Error($"Error injecting WebSocket connector: {ex.Message}");
                    websocketServerInjected = false;
                }
            }

            if (websocketServerInjected)
            {
                Log.Success("WebSocket connector injected successfully");
            }
            else
            {
                Log.Error("Failed to inject WebSocket connector");
                return;
            }

            string mainScriptFile = Path.Combine(root, "betterteams-main.js");
            if (!File.Exists(mainScriptFile))
            {
                Log.Error($"Script file not found: {mainScriptFile}");
                return;
            }

            int iPage = 0;
            List<PluginInfo> installedPlugins = _pluginManager.GetInstalledPlugins();
            foreach (IPage page in context.Pages)
            {
                iPage++;
                string contentMain = File.ReadAllText(mainScriptFile);
                try
                {
                    await page.EvaluateAsync(contentMain);
                }
                catch (Exception ex)
                {
                    Log.Error($"Error injecting main script: {ex.Message}");
                    mainScriptInjected = false;
                }

                foreach (var plugin in installedPlugins)
                {
                    string pluginDir = Path.Combine(root, "plugins", plugin.Id);
                    string scriptFile = Path.Combine(pluginDir, "main.js");
                    
                    if (File.Exists(scriptFile))
                    {
                        string content = File.ReadAllText(scriptFile);
                        try
                        {
                            await page.EvaluateAsync(content);
                            if (iPage == 1)
                            {
                                Log.Success($"Injected plugin: {plugin.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Error injecting plugin {plugin.Name}: {ex.Message}");
                        }
                    }
                }
                
                string themeManagerScript = GenerateThemeManagerScript();
                try
                {
                    await page.EvaluateAsync(themeManagerScript);
                    if (iPage == 1)
                    {
                        Log.Success("Theme manager injected");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error injecting theme manager: {ex.Message}");
                    themesInjected = false;
                }
                
                var themesDir = Path.Combine(root, "themes");
                if (Directory.Exists(themesDir))
                {
                    foreach (string themeDir in Directory.GetDirectories(themesDir))
                    {
                        string scriptFile = Path.Combine(themeDir, "main.js");
                        if (File.Exists(scriptFile))
                        {
                            var dirInfo = new DirectoryInfo(themeDir);
                            string themeId = dirInfo.Name;
                            string content = File.ReadAllText(scriptFile);
                            
                            string wrappedScript = $@"
                            (function() {{
                                // Register theme with theme manager
                                if (window.BetterTeamsThemeManager) {{
                                    window.BetterTeamsThemeManager.registerTheme('{themeId}', function() {{
                                        try {{
                                            {content}
                                            console.log('Theme {themeId} applied successfully');
                                            return true;
                                        }} catch(e) {{
                                            console.error('Error applying theme {themeId}:', e);
                                            return false;
                                        }}
                                    }}, function() {{
                                        try {{
                                            // Clean up theme elements
                                            const themeElements = document.querySelectorAll('[data-betterteams-theme=""{themeId}""]');
                                            themeElements.forEach(el => el.remove());
                                            console.log('Theme {themeId} removed successfully');
                                            return true;
                                        }} catch(e) {{
                                            console.error('Error removing theme {themeId}:', e);
                                            return false;
                                        }}
                                    }});
                                }}
                            }})();";
                            
                            try
                            {
                                await page.EvaluateAsync(wrappedScript);

                                if (iPage == 1)
                                {
                                    Log.Success($"Theme {themeId} registered");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"Error registering theme {themeId}: {ex.Message}");
                                themesInjected = false;
                            }
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(_config.ActiveThemeId))
                    {
                        string activateThemeScript = $@"
                        (function() {{
                            if (window.BetterTeamsThemeManager) {{
                                window.BetterTeamsThemeManager.activateTheme('{_config.ActiveThemeId}');
                            }}
                        }})();";
                        
                        try
                        {
                            await page.EvaluateAsync(activateThemeScript);
                            if (iPage == 1)
                            {
                                Log.Success($"Activated theme: {_config.ActiveThemeId}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Error activating theme {_config.ActiveThemeId}: {ex.Message}");
                        }
                    }
                }
            }

            if (mainScriptInjected)
            {
                Log.Success("Main script injected successfully");
            }
            else
            {
                Log.Error("Failed to inject main script");
                return;
            }

            if (themesInjected)
            {
                Log.Success("Themes injected successfully");
            }
            else
            {
                Log.Error("Failed to inject themes");
                return;
            }

            ResizeWindow("Microsoft Teams");
            await browser.CloseAsync();
        }

        private static async Task HandlePageAction(IPage page, WebSocketMessage message)
        {
            Log.Info($"Received action from page: {message.Action}");
            
            switch (message.Action)
            {
                case "get_plugins":
                    var plugins = await _pluginManager.GetAvailablePlugins();
                    await SendPageEvent(page, "available_plugins", new { Plugins = plugins });
                    break;
                case "get_themes":
                    var themes = await _pluginManager.GetAvailableThemes();
                    await SendPageEvent(page, "available_themes", new { Themes = themes });
                    break;
                case "get_installed_plugins":
                    var installedPlugins = _pluginManager.GetInstalledPlugins();
                    await SendPageEvent(page, "installed_plugins", new { Plugins = installedPlugins });
                    break;
                case "get_installed_themes":
                    var installedThemes = _pluginManager.GetInstalledThemes();
                    await SendPageEvent(page, "installed_themes", new { Themes = installedThemes, ActiveThemeId = _config.ActiveThemeId });
                    break;
                case "get_active_theme":
                    await SendActiveTheme(page);
                    break;
                case "install_plugin":
                    if (message.Data.ContainsKey("id") && message.Data["id"] is JsonElement pluginIdElement && pluginIdElement.ValueKind == JsonValueKind.String)
                    {
                        string pluginId = pluginIdElement.GetString();
                        bool success = await _pluginManager.InstallPlugin(pluginId);
                        await SendPageEvent(page, "plugin_installed", new { Success = success, Id = pluginId });
                    }
                    break;
                case "install_theme":
                    if (message.Data.ContainsKey("id") && message.Data["id"] is JsonElement themeIdElement && themeIdElement.ValueKind == JsonValueKind.String)
                    {
                        string themeId = themeIdElement.GetString();
                        bool success = await _pluginManager.InstallTheme(themeId);
                        await SendPageEvent(page, "theme_installed", new { Success = success, Id = themeId });
                    }
                    break;
                case "activate_theme":
                    if (message.Data.ContainsKey("id") && message.Data["id"] is JsonElement activateThemeIdElement && activateThemeIdElement.ValueKind == JsonValueKind.String)
                    {
                        string themeId = activateThemeIdElement.GetString();
                        await ActivateTheme(themeId);
                    }
                    break;
                case "deactivate_theme":
                    await DeactivateTheme();
                    break;
                default:
                    Log.Warning($"Unknown action: {message.Action ?? "None"}");
                    break;
            }
        }
        
        private static async Task SendActiveTheme(IPage page)
        {
            try
            {
                if (!string.IsNullOrEmpty(_config.ActiveThemeId))
                {
                    var themes = _pluginManager.GetInstalledThemes();
                    var theme = themes.Find(t => t.Id == _config.ActiveThemeId);
                    
                    if (theme != null)
                    {
                        await SendPageEvent(page, "active_theme", new { ThemeId = _config.ActiveThemeId, ThemeName = theme.Name });
                    }
                    else
                    {
                        // Theme ID in config doesn't match any installed theme
                        _config.ActiveThemeId = string.Empty;
                        SaveConfig();
                        await SendPageEvent(page, "active_theme", new { ThemeId = string.Empty });
                    }
                }
                else
                {
                    await SendPageEvent(page, "active_theme", new { ThemeId = string.Empty });
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error getting active theme: {ex.Message}");
                await SendPageEvent(page, "active_theme", new { ThemeId = string.Empty, Error = ex.Message });
            }
        }
        
        private static async Task SendPageEvent(IPage page, string action, object data)
        {
            try
            {
                string jsonData = JsonSerializer.Serialize(data);
                string script = $@"
                    (function() {{
                        const event = new CustomEvent('BetterTeamsEvent', {{
                            detail: {{
                                action: '{action}',
                                data: {jsonData}
                            }}
                        }});
                        document.dispatchEvent(event);
                    }})();
                ";
                await page.EvaluateAsync(script);

            }
            catch (Exception ex)
            {
                Log.Error($"Error sending page event: {ex.Message}");
            }
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
            // Deactivate current theme if any
            if (this.activeTheme) {
                this.deactivateTheme();
            }

            // Activate new theme
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

        setupWebSocketListeners() {
            if (!window.BetterTeamsWS) {
                console.error('BetterTeamsWS not available, theme manager will not respond to WebSocket events');
                return;
            }

            // Listen for theme activation
            window.BetterTeamsWS.on('theme_activated', (data) => {
                if (data.ThemeId) {
                    this.activateTheme(data.ThemeId);
                }
            });

            // Listen for theme deactivation
            window.BetterTeamsWS.on('theme_deactivated', () => {
                this.deactivateTheme();
            });

            // Request active theme on connection
            window.BetterTeamsWS.on('connected', () => {
                window.BetterTeamsWS.send('get_active_theme');
            });

            // Handle active theme response
            window.BetterTeamsWS.on('active_theme', (data) => {
                if (data.ThemeId && data.ThemeId !== this.activeTheme) {
                    this.activateTheme(data.ThemeId);
                }
            });
        }
    }

    window.BetterTeamsThemeManager = new ThemeManager();
    console.log('BetterTeams Theme Manager initialized');
})();";
        }

        private static string GenerateWebSocketConnectorScript(int port)
        {
            return @"
(function() {
    if (window.BetterTeamsWS) {
        console.log('BetterTeams Message Bridge already initialized');
        return;
    }

    class BetterTeamsMessageBridge {
        constructor() {
            this.connected = true;
            this.eventListeners = {};
            this.setupMessageListener();
            
            // Notify that we're ready to receive messages
            console.log('BetterTeams Message Bridge initialized');
            this.emit('connected', {});
            
            // Create a hidden div for communication
            this.createBridgeElement();
            
            // Notify the native app that we're ready
            if (window.notifyBridgeReadyAsync) {
                window.notifyBridgeReadyAsync();
            }
        }

        createBridgeElement() {
            // Create a hidden div to mark our presence
            const bridgeElement = document.createElement('div');
            bridgeElement.id = 'betterteams-bridge';
            bridgeElement.style.display = 'none';
            bridgeElement.setAttribute('data-port', '" + port + @"');
            document.body.appendChild(bridgeElement);
        }

        setupMessageListener() {
            // Listen for custom events from the page
            document.addEventListener('BetterTeamsEvent', (e) => {
                if (e.detail && e.detail.action) {
                    this.emit(e.detail.action, e.detail.data || {});
                }
            });
        }

        send(action, data = {}) {
            try {
                // Use the exposed binding to communicate with the native app
                if (window.BetterTeamsActionHandler) {
                    window.BetterTeamsActionHandler(action, data);
                    return true;
                } else {
                    console.error('BetterTeamsActionHandler not available');
                    return false;
                }
            } catch (e) {
                console.error('Error sending message:', e);
                return false;
            }
        }

        on(event, callback) {
            if (!this.eventListeners[event]) {
                this.eventListeners[event] = [];
            }
            this.eventListeners[event].push(callback);
        }

        off(event, callback) {
            if (!this.eventListeners[event]) return;
            this.eventListeners[event] = this.eventListeners[event].filter(cb => cb !== callback);
        }

        emit(event, data) {
            if (!this.eventListeners[event]) return;
            this.eventListeners[event].forEach(callback => callback(data));
        }
    }

    window.BetterTeamsWS = new BetterTeamsMessageBridge();
})();";
        }
    }
}