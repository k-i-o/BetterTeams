using BetterTeams.Configs;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace BetterTeams
{
    public class PluginInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
        
        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;
        
        [JsonPropertyName("repository")]
        public string Repository { get; set; } = string.Empty;
        
        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = string.Empty;
    }

    public class PluginManager
    {
        private const string PluginsApiUrl = "https://api.kiocode.com/api/betterteams/plugins";
        private const string ThemesApiUrl = "https://api.kiocode.com/api/betterteams/themes";
        private static HttpClient _httpClient = new HttpClient();
        private readonly string _pluginsDirectory;
        private readonly string _themesDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        public PluginManager(string scriptsDirectory)
        {
            _pluginsDirectory = Path.Combine(scriptsDirectory, "plugins");
            _themesDirectory = Path.Combine(scriptsDirectory, "themes");
            
            Directory.CreateDirectory(_pluginsDirectory);
            Directory.CreateDirectory(_themesDirectory);
            
            // Configure JSON options to be case-insensitive
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
        }

        public async Task<List<PluginInfo>> GetAvailablePlugins()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(PluginsApiUrl);
                return JsonSerializer.Deserialize<List<PluginInfo>>(response, _jsonOptions) ?? new List<PluginInfo>();
            }
            catch (Exception ex)
            {
                Log.Error($"Error fetching available plugins: {ex.Message}");
                return new List<PluginInfo>();
            }
        }

        public async Task<List<PluginInfo>> GetAvailableThemes()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(ThemesApiUrl);
                return JsonSerializer.Deserialize<List<PluginInfo>>(response, _jsonOptions) ?? new List<PluginInfo>();
            }
            catch (Exception ex)
            {
                Log.Error($"Error fetching available themes: {ex.Message}");
                return new List<PluginInfo>();
            }
        }

        private class PluginManifest
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }
            
            [JsonPropertyName("name")]
            public string? Name { get; set; }
            
            [JsonPropertyName("description")]
            public string? Description { get; set; }
            
            [JsonPropertyName("version")]
            public string? Version { get; set; }
            
            [JsonPropertyName("author")]
            public string? Author { get; set; }
            
            [JsonPropertyName("repository")]
            public string? Repository { get; set; }
            
            public PluginInfo ToPluginInfo(string pluginFolderName)
            {
                return new PluginInfo
                {
                    Id = string.IsNullOrEmpty(Id) ? GeneratePluginId(Name, pluginFolderName) : Id,
                    Name = Name ?? "Unknown Plugin",
                    Description = Description ?? "",
                    Version = Version ?? "1.0.0",
                    Author = Author ?? "Unknown Author",
                    Repository = Repository ?? ""
                };
            }
            
            private string GeneratePluginId(string? name, string folderName)
            {
                string baseString = (name ?? "unknown") + "-" + folderName;
                
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(baseString);
                    byte[] hashBytes = md5.ComputeHash(inputBytes);
                    
                    return new Guid(hashBytes).ToString();
                }
            }
        }

        public List<PluginInfo> GetInstalledPlugins()
        {
            var plugins = new List<PluginInfo>();
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
                        
                        PluginManifest? manifest = JsonSerializer.Deserialize<PluginManifest>(json, _jsonOptions);
                        
                        if (manifest != null)
                        {
                            var plugin = manifest.ToPluginInfo(pluginFolderName);
                            plugins.Add(plugin);
                            
                            if (string.IsNullOrEmpty(manifest.Id))
                            {
                                Log.Success($"Generated ID {plugin.Id} for plugin {plugin.Name}");
                                UpdateManifestWithId(manifestPath, plugin.Id);
                            } else
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

        public List<PluginInfo> GetInstalledThemes()
        {
            var themes = new List<PluginInfo>();
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
                        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, _jsonOptions);
                        if (manifest != null)
                        {
                            var theme = manifest.ToPluginInfo(themeFolderName);
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
                
                // Parse to JsonDocument to manipulate the JSON
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var newJson = JsonSerializer.Serialize(
                        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, _jsonOptions), _jsonOptions);
                    
                    // Add the ID to the deserialized dictionary and reserialize
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

        public async Task<bool> InstallPlugin(string pluginId)
        {
            try
            {
                var plugins = await GetAvailablePlugins();
                var plugin = plugins.Find(p => p.Id == pluginId);
                if (plugin == null)
                {
                    Log.Error($"Plugin {pluginId} not found");
                    return false;
                }

                var pluginDir = Path.Combine(_pluginsDirectory, pluginId);
                Directory.CreateDirectory(pluginDir);

                // Download the plugin package
                var zipPath = Path.Combine(Path.GetTempPath(), $"{pluginId}.zip");
                using (var response = await _httpClient.GetAsync(plugin.DownloadUrl))
                {
                    using (var fs = new FileStream(zipPath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                // Extract the package
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, pluginDir, true);
                File.Delete(zipPath);

                Log.Success($"Plugin {plugin.Name} installed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Error installing plugin: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> InstallTheme(string themeId)
        {
            try
            {
                var themes = await GetAvailableThemes();
                var theme = themes.Find(t => t.Id == themeId);
                if (theme == null)
                {
                    Log.Error($"Theme {themeId} not found");
                    return false;
                }

                var themeDir = Path.Combine(_themesDirectory, themeId);
                Directory.CreateDirectory(themeDir);

                // Download the theme package
                var zipPath = Path.Combine(Path.GetTempPath(), $"{themeId}.zip");
                using (var response = await _httpClient.GetAsync(theme.DownloadUrl))
                {
                    using (var fs = new FileStream(zipPath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                // Extract the package
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, themeDir, true);
                File.Delete(zipPath);

                Log.Success($"Theme {theme.Name} installed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Error installing theme: {ex.Message}");
                return false;
            }
        }

        public bool UninstallPlugin(string pluginId)
        {
            try
            {
                var pluginDir = Path.Combine(_pluginsDirectory, pluginId);
                if (Directory.Exists(pluginDir))
                {
                    Directory.Delete(pluginDir, true);
                    Log.Success($"Plugin {pluginId} uninstalled successfully");
                    return true;
                }
                else
                {
                    Log.Warning($"Plugin {pluginId} not found");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error uninstalling plugin: {ex.Message}");
                return false;
            }
        }

        public bool UninstallTheme(string themeId)
        {
            try
            {
                var themeDir = Path.Combine(_themesDirectory, themeId);
                if (Directory.Exists(themeDir))
                {
                    Directory.Delete(themeDir, true);
                    Log.Success($"Theme {themeId} uninstalled successfully");
                    return true;
                }
                else
                {
                    Log.Warning($"Theme {themeId} not found");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error uninstalling theme: {ex.Message}");
                return false;
            }
        }
    }
} 