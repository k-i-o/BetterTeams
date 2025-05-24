using BetterTeams.Configs;
using System.Text.Json;

namespace BetterTeams
{
    public class PluginManager
    {
        private const string PluginsApiUrl = "https://api.kiocode.com/api/betterteams/plugins";
        private const string ThemesApiUrl = "https://api.kiocode.com/api/betterteams/themes";
        //private const string PluginsApiUrl = "https://localhost:7170/api/betterteams/plugins";
        //private const string ThemesApiUrl = "https://localhost:7170/api/betterteams/themes";
        private static HttpClient _httpClient = new();
        private readonly string _pluginsDirectory;
        private readonly string _themesDirectory;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly PluginConfig _pluginConfig;

        public PluginManager(string scriptsDirectory, PluginConfig pluginConfig)
        {
            _pluginsDirectory = Path.Combine(scriptsDirectory, "plugins");
            _themesDirectory = Path.Combine(scriptsDirectory, "themes");
            _pluginConfig = pluginConfig ?? new PluginConfig();

            Directory.CreateDirectory(_pluginsDirectory);
            Directory.CreateDirectory(_themesDirectory);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
        }

        public List<AddonInfo> GetInstalledPlugins()
        {
            var plugins = new List<AddonInfo>();
            foreach (var pluginDir in Directory.GetDirectories(_pluginsDirectory))
            {
                var dirInfo = new DirectoryInfo(pluginDir);
                var pluginFolderName = dirInfo.Name;

                var manifestPath = Path.Combine(pluginDir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var json = File.ReadAllText(manifestPath);

                        ManifestInfo? manifest = JsonSerializer.Deserialize<ManifestInfo>(json, _jsonOptions);

                        if (manifest != null)
                        {
                            var plugin = manifest.ToAddonInfo(pluginFolderName);
                            plugin.IsActive = _pluginConfig.IsPluginActive(plugin.Id);
                            plugins.Add(plugin);

                            if (string.IsNullOrEmpty(manifest.Id))
                            {
                                Log.Success($"Generated ID {plugin.Id} for plugin {plugin.Name}");
                                UpdateManifestWithId(manifestPath, plugin.Id);
                            }
                            else
                            {
                                Log.Success($"Plugin {plugin.Name} loaded");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error loading plugin manifest {manifestPath}: {ex.Message}");
                        Log.Error($"Stack trace: {ex.StackTrace}");
                    }
                }
            }
            return plugins;
        }

        public List<AddonInfo> GetInstalledThemes()
        {
            var themes = new List<AddonInfo>();
            foreach (var themeDir in Directory.GetDirectories(_themesDirectory))
            {
                var dirInfo = new DirectoryInfo(themeDir);
                var themeFolderName = dirInfo.Name;

                var manifestPath = Path.Combine(themeDir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var json = File.ReadAllText(manifestPath);
                        var manifest = JsonSerializer.Deserialize<ManifestInfo>(json, _jsonOptions);
                        if (manifest != null)
                        {
                            var theme = manifest.ToAddonInfo(themeFolderName);
                            themes.Add(theme);

                            if (string.IsNullOrEmpty(manifest.Id))
                            {
                                Log.Info($"Generated ID {theme.Id} for theme {theme.Name}");
                                UpdateManifestWithId(manifestPath, theme.Id);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error loading theme manifest {manifestPath}: {ex.Message}");
                    }
                }
            }
            return themes;
        }

        private void UpdateManifestWithId(string manifestPath, string id)
        {
            try
            {
                var json = File.ReadAllText(manifestPath);

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var newJson = JsonSerializer.Serialize(
                        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, _jsonOptions), _jsonOptions);

                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(newJson, _jsonOptions);
                    if (dict != null)
                    {
                        dict["id"] = id;
                        var updatedJson = JsonSerializer.Serialize(dict, _jsonOptions);
                        File.WriteAllText(manifestPath, updatedJson);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error updating manifest with generated ID: {ex.Message}");
            }
        }

        public async Task<List<AddonInfo>> GetAvailablePlugins()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(PluginsApiUrl);
                return JsonSerializer.Deserialize<List<AddonInfo>>(response, _jsonOptions) ?? new List<AddonInfo>();
            }
            catch
            {
                return new List<AddonInfo>();
            }
        }

        public async Task<List<AddonInfo>> GetAvailableThemes()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(ThemesApiUrl);
                return JsonSerializer.Deserialize<List<AddonInfo>>(response, _jsonOptions) ?? new List<AddonInfo>();
            }
            catch
            {
                return new List<AddonInfo>();
            }
        }

        public async Task<bool> InstallPlugin(string pluginId)
        {
            const int MaxRetries = 2;
            var plugins = await GetAvailablePlugins();
            var plugin = plugins.Find(p => p.Id == pluginId);
            if (plugin == null) return false;

            var pluginDir = Path.Combine(_pluginsDirectory, pluginId);

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    if (Directory.Exists(pluginDir))
                        Directory.Delete(pluginDir, true);
                    Directory.CreateDirectory(pluginDir);

                    var manifestUrl = $"{PluginsApiUrl}/download/{pluginId}/manifest.json";
                    var scriptUrl = $"{PluginsApiUrl}/download/{pluginId}/main.js";

                    using (var ms = await _httpClient.GetStreamAsync(manifestUrl))
                    using (var fs = new FileStream(Path.Combine(pluginDir, "manifest.json"), FileMode.Create))
                        await ms.CopyToAsync(fs);

                    using (var ss = await _httpClient.GetStreamAsync(scriptUrl))
                    using (var fs = new FileStream(Path.Combine(pluginDir, $"main.js"), FileMode.Create))
                        await ss.CopyToAsync(fs);

                    return true;
                }
                catch when (attempt < MaxRetries)
                {
                    if (Directory.Exists(pluginDir))
                        Directory.Delete(pluginDir, true);
                }
            }

            return false;
        }

        public async Task<bool> InstallTheme(string themeId)
        {
            var themes = await GetAvailableThemes();
            var theme = themes.Find(t => t.Id == themeId);
            if (theme == null) return false;

            var themeDir = Path.Combine(_themesDirectory, themeId);
            if (Directory.Exists(themeDir))
                Directory.Delete(themeDir, true);
            Directory.CreateDirectory(themeDir);

            var manifestUrl = $"{ThemesApiUrl}/download/{themeId}/manifest.json";
            var scriptUrl = $"{ThemesApiUrl}/download/{themeId}/main.js";

            using (var ms = await _httpClient.GetStreamAsync(manifestUrl))
            using (var fs = new FileStream(Path.Combine(themeDir, "manifest.json"), FileMode.Create))
                await ms.CopyToAsync(fs);

            using (var ss = await _httpClient.GetStreamAsync(scriptUrl))
            using (var fs = new FileStream(Path.Combine(themeDir, $"main.js"), FileMode.Create))
                await ss.CopyToAsync(fs);

            return true;
        }

        public bool UninstallPlugin(string pluginId)
        {
            var dir = Path.Combine(_pluginsDirectory, pluginId);
            if (!Directory.Exists(dir)) return false;
            Directory.Delete(dir, true);
            return true;
        }

        public bool UninstallTheme(string themeId)
        {
            var dir = Path.Combine(_themesDirectory, themeId);
            if (!Directory.Exists(dir)) return false;
            Directory.Delete(dir, true);
            return true;
        }

        public bool DeactivatePlugin(string pluginId)
        {
            var dir = Path.Combine(_pluginsDirectory, pluginId);
            if (!Directory.Exists(dir)) return false;
            _pluginConfig.DeactivatePlugin(pluginId);
            return true;
        }

        public bool ActivatePlugin(string pluginId)
        {
            var dir = Path.Combine(_pluginsDirectory, pluginId);
            if (!Directory.Exists(dir)) return false;
            _pluginConfig.ActivatePlugin(pluginId);
            return true;
        }

        public List<AddonInfo> GetActivePlugins()
        {
            return GetInstalledPlugins().FindAll(p => p.IsActive);
        }

    }
}
