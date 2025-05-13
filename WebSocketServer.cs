using BetterTeams.Configs;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace BetterTeams
{
    public class WebSocketMessage
    {
        public string Action { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }

    public class WebSocketServer
    {
        // Windows API for clipboard operations
        [DllImport("user32.dll")]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);
        
        [DllImport("user32.dll")]
        static extern bool EmptyClipboard();
        
        [DllImport("user32.dll")]
        static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        
        [DllImport("user32.dll")]
        static extern bool CloseClipboard();
        
        [DllImport("gdi32.dll")]
        static extern IntPtr CopyEnhMetaFile(IntPtr hemfSrc, IntPtr hNULL);
        
        [DllImport("gdi32.dll")]
        static extern bool DeleteEnhMetaFile(IntPtr hemf);
        
        // Clipboard format constants
        const uint CF_BITMAP = 2;
        const uint CF_DIB = 8;
        const uint CF_ENHMETAFILE = 14;
        
        private HttpListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly List<WebSocket> _clients = new List<WebSocket>();
        private readonly PluginManager _pluginManager;
        private readonly InjectorConfig _config;
        private readonly int _port;

        public WebSocketServer(PluginManager pluginManager, InjectorConfig config, int port = 0)
        {
            _pluginManager = pluginManager;
            _config = config;
            _port = port > 0 ? port : config.WebSocketPort;
        }

        public async Task Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Start();
            Log.Success($"WebSocket server started on port {_port}");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception ex)
                {
                    Log.Error($"Error getting HTTP context: {ex.Message}");
                    continue;
                }

                if (context.Request.IsWebSocketRequest)
                {
                    ProcessWebSocketRequest(context);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }

        private async void ProcessWebSocketRequest(HttpListenerContext context)
        {
            WebSocketContext webSocketContext;
            try
            {
                webSocketContext = await context.AcceptWebSocketAsync(null);
                Log.Info("WebSocket connection established");
            }
            catch (Exception ex)
            {
                Log.Error($"Error accepting WebSocket: {ex.Message}");
                context.Response.StatusCode = 500;
                context.Response.Close();
                return;
            }

            WebSocket webSocket = webSocketContext.WebSocket;
            _clients.Add(webSocket);

            try
            {
                await HandleWebSocketConnection(webSocket);
            }
            catch (Exception ex)
            {
                Log.Error($"Error handling WebSocket: {ex.Message}");
            }
            finally
            {
                _clients.Remove(webSocket);
                webSocket.Dispose();
            }
        }

        private async Task HandleWebSocketConnection(WebSocket webSocket)
        {
            var buffer = new byte[4096];
            var receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);

            while (!receiveResult.CloseStatus.HasValue)
            {
                string receivedMessage = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                await HandleWebSocketMessage(receivedMessage, webSocket);

                Array.Clear(buffer, 0, buffer.Length);
                receiveResult = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
            }

            await webSocket.CloseAsync(
                receiveResult.CloseStatus.Value,
                receiveResult.CloseStatusDescription,
                _cancellationTokenSource.Token);
        }

        private async Task HandleWebSocketMessage(string message, WebSocket socket)
        {
            try
            {
                var jsonDocument = JsonDocument.Parse(message);
                var root = jsonDocument.RootElement;

                if (root.TryGetProperty("action", out var actionElement))
                {
                    string action = actionElement.GetString() ?? string.Empty;
                    
                    switch (action)
                    {
                        case "get_plugins":
                            await SendAvailablePlugins(socket);
                            break;
                        case "get_themes":
                            await SendAvailableThemes(socket);
                            break;
                        case "install_plugin":
                            if (root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
                            {
                                string pluginId = idElement.GetString() ?? string.Empty;
                                bool success = await _pluginManager.InstallPlugin(pluginId);
                                await SendResponse(socket, "plugin_installed", new { Success = success, Id = pluginId });
                            }
                            break;
                        case "install_theme":
                            if (root.TryGetProperty("id", out var themeIdElement) && themeIdElement.ValueKind == JsonValueKind.String)
                            {
                                string themeId = themeIdElement.GetString() ?? string.Empty;
                                bool success = await _pluginManager.InstallTheme(themeId);
                                await SendResponse(socket, "theme_installed", new { Success = success, Id = themeId });
                            }
                            break;
                        case "uninstall_plugin":
                            if (root.TryGetProperty("id", out var pluginToUninstallElement) && pluginToUninstallElement.ValueKind == JsonValueKind.String)
                            {
                                string pluginToUninstall = pluginToUninstallElement.GetString() ?? string.Empty;
                                bool success = _pluginManager.UninstallPlugin(pluginToUninstall);
                                await SendResponse(socket, "plugin_uninstalled", new { Success = success, Id = pluginToUninstall });
                            }
                            break;
                        case "uninstall_theme":
                            if (root.TryGetProperty("id", out var themeToUninstallElement) && themeToUninstallElement.ValueKind == JsonValueKind.String)
                            {
                                string themeToUninstall = themeToUninstallElement.GetString() ?? string.Empty;
                                bool success = _pluginManager.UninstallTheme(themeToUninstall);
                                await SendResponse(socket, "theme_uninstalled", new { Success = success, Id = themeToUninstall });
                            }
                            break;
                        case "get_installed_plugins":
                            await SendInstalledPlugins(socket);
                            break;
                        case "get_installed_themes":
                            await SendInstalledThemes(socket);
                            break;
                        case "activate_theme":
                            if (root.TryGetProperty("id", out var themeToActivateElement) && themeToActivateElement.ValueKind == JsonValueKind.String)
                            {
                                string themeToActivate = themeToActivateElement.GetString() ?? string.Empty;
                                await ActivateTheme(socket, themeToActivate);
                            }
                            break;
                        case "deactivate_theme":
                            await DeactivateTheme(socket);
                            break;
                        case "get_active_theme":
                            await SendActiveTheme(socket);
                            break;
                        case "copyToClipboard":
                            await HandleCopyToClipboard(root);
                            break;
                        default:
                            Log.Warning($"Unknown action: {action}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error handling WebSocket message: {ex.Message}");
            }
        }

        private async Task ActivateTheme(WebSocket webSocket, string themeId)
        {
            try
            {
                var themes = _pluginManager.GetInstalledThemes();
                var theme = themes.Find(t => t.Id == themeId);
                
                if (theme != null)
                {
                    // Save the active theme in config
                    _config.ActiveThemeId = themeId;
                    SaveConfig();
                    
                    // Notify all clients about theme change
                    await BroadcastMessage("theme_activated", new { ThemeId = themeId, ThemeName = theme.Name });
                    
                    Log.Success($"Theme {theme.Name} activated");
                    await SendResponse(webSocket, "theme_activated", new { Success = true, ThemeId = themeId, ThemeName = theme.Name });
                }
                else
                {
                    Log.Error($"Theme {themeId} not found");
                    await SendResponse(webSocket, "theme_activated", new { Success = false, Error = $"Theme {themeId} not found" });
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error activating theme: {ex.Message}");
                await SendResponse(webSocket, "theme_activated", new { Success = false, Error = ex.Message });
            }
        }

        private async Task DeactivateTheme(WebSocket webSocket)
        {
            try
            {
                // Clear the active theme in config
                _config.ActiveThemeId = string.Empty;
                SaveConfig();
                
                // Notify all clients about theme deactivation
                await BroadcastMessage("theme_deactivated", new { });
                
                Log.Success("Theme deactivated");
                await SendResponse(webSocket, "theme_deactivated", new { Success = true });
            }
            catch (Exception ex)
            {
                Log.Error($"Error deactivating theme: {ex.Message}");
                await SendResponse(webSocket, "theme_deactivated", new { Success = false, Error = ex.Message });
            }
        }

        private async Task SendActiveTheme(WebSocket webSocket)
        {
            try
            {
                if (!string.IsNullOrEmpty(_config.ActiveThemeId))
                {
                    var themes = _pluginManager.GetInstalledThemes();
                    var theme = themes.Find(t => t.Id == _config.ActiveThemeId);
                    
                    if (theme != null)
                    {
                        await SendResponse(webSocket, "active_theme", new { ThemeId = _config.ActiveThemeId, ThemeName = theme.Name });
                    }
                    else
                    {
                        // Theme ID in config doesn't match any installed theme
                        _config.ActiveThemeId = string.Empty;
                        SaveConfig();
                        await SendResponse(webSocket, "active_theme", new { ThemeId = string.Empty });
                    }
                }
                else
                {
                    await SendResponse(webSocket, "active_theme", new { ThemeId = string.Empty });
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error getting active theme: {ex.Message}");
                await SendResponse(webSocket, "active_theme", new { ThemeId = string.Empty, Error = ex.Message });
            }
        }

        private void SaveConfig()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterMsTeams");
                string path = Path.Combine(dir, "BetterMsTeamsConfig.json");
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Log.Error($"Error saving config: {ex.Message}");
            }
        }

        private async Task SendAvailablePlugins(WebSocket webSocket)
        {
            var plugins = await _pluginManager.GetAvailablePlugins();
            await SendResponse(webSocket, "available_plugins", new { Plugins = plugins });
        }

        private async Task SendAvailableThemes(WebSocket webSocket)
        {
            var themes = await _pluginManager.GetAvailableThemes();
            await SendResponse(webSocket, "available_themes", new { Themes = themes });
        }

        private async Task SendInstalledPlugins(WebSocket webSocket)
        {
            var plugins = _pluginManager.GetInstalledPlugins();
            await SendResponse(webSocket, "installed_plugins", new { Plugins = plugins });
        }

        private async Task SendInstalledThemes(WebSocket webSocket)
        {
            var themes = _pluginManager.GetInstalledThemes();
            await SendResponse(webSocket, "installed_themes", new { Themes = themes, ActiveThemeId = _config.ActiveThemeId });
        }

        private async Task SendResponse(WebSocket webSocket, string action, object data)
        {
            var response = new WebSocketMessage
            {
                Action = action,
                Data = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    JsonSerializer.Serialize(data))
            };

            var responseJson = JsonSerializer.Serialize(response);
            var responseBuffer = Encoding.UTF8.GetBytes(responseJson);
            
            await webSocket.SendAsync(
                new ArraySegment<byte>(responseBuffer),
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token);
        }

        public async Task BroadcastMessage(string action, object data)
        {
            var message = new WebSocketMessage
            {
                Action = action,
                Data = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    JsonSerializer.Serialize(data))
            };

            var messageJson = JsonSerializer.Serialize(message);
            var messageBuffer = Encoding.UTF8.GetBytes(messageJson);

            List<WebSocket> disconnectedClients = new List<WebSocket>();

            foreach (var client in _clients)
            {
                if (client.State == WebSocketState.Open)
                {
                    try
                    {
                        await client.SendAsync(
                            new ArraySegment<byte>(messageBuffer),
                            WebSocketMessageType.Text,
                            true,
                            _cancellationTokenSource.Token);
                    }
                    catch
                    {
                        disconnectedClients.Add(client);
                    }
                }
                else
                {
                    disconnectedClients.Add(client);
                }
            }

            foreach (var client in disconnectedClients)
            {
                _clients.Remove(client);
            }
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _listener?.Stop();
            Log.Info("WebSocket server stopped");
        }

        private async Task HandleCopyToClipboard(JsonElement data)
        {
            try
            {
                if (data.TryGetProperty("type", out var typeElement) && 
                    data.TryGetProperty("url", out var urlElement))
                {
                    string type = typeElement.GetString() ?? string.Empty;
                    string url = urlElement.GetString() ?? string.Empty;
                    
                    if (type == "gif" && !string.IsNullOrEmpty(url))
                    {
                        Log.Info($"Copying GIF from URL to clipboard: {url}");
                        
                        // Download the GIF
                        using (HttpClient client = new HttpClient())
                        {
                            var response = await client.GetAsync(url);
                            if (response.IsSuccessStatusCode)
                            {
                                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                                
                                // Save to temporary file and copy to clipboard
                                string tempFilePath = Path.Combine(Path.GetTempPath(), $"betterteams_gif_{Guid.NewGuid()}.gif");
                                try
                                {
                                    // Save the GIF to a temporary file
                                    File.WriteAllBytes(tempFilePath, imageBytes);
                                    Log.Info($"GIF saved to temporary file: {tempFilePath}");
                                    
                                    // Use PowerShell to copy the image to clipboard
                                    string psCommand = $"Add-Type -AssemblyName System.Windows.Forms; " +
                                                      $"[System.Windows.Forms.Clipboard]::SetImage([System.Drawing.Image]::FromFile('{tempFilePath}'))";
                                    
                                    using (var process = new System.Diagnostics.Process())
                                    {
                                        process.StartInfo.FileName = "powershell.exe";
                                        process.StartInfo.Arguments = $"-Command \"{psCommand}\"";
                                        process.StartInfo.UseShellExecute = false;
                                        process.StartInfo.CreateNoWindow = true;
                                        process.StartInfo.RedirectStandardOutput = true;
                                        process.StartInfo.RedirectStandardError = true;
                                        
                                        process.Start();
                                        process.WaitForExit();
                                        
                                        if (process.ExitCode == 0)
                                        {
                                            Log.Success("GIF copied to clipboard successfully");
                                        }
                                        else
                                        {
                                            string error = process.StandardError.ReadToEnd();
                                            Log.Error($"Failed to copy GIF to clipboard: {error}");
                                        }
                                    }
                                    
                                    // Broadcast success message to clients
                                    await BroadcastMessage("clipboard_updated", new { Success = true, Message = "GIF copied to clipboard" });
                                }
                                catch (Exception ex)
                                {
                                    Log.Error($"Error copying GIF to clipboard: {ex.Message}");
                                }
                                finally
                                {
                                    // Clean up the temporary file
                                    try
                                    {
                                        if (File.Exists(tempFilePath))
                                        {
                                            File.Delete(tempFilePath);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Warning($"Failed to delete temporary file: {ex.Message}");
                                    }
                                }
                            }
                            else
                            {
                                Log.Error($"Failed to download GIF: {response.StatusCode}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error handling copy to clipboard: {ex.Message}");
            }
        }
    }
} 