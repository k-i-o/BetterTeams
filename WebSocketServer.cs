using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BetterTeams.Configs;

namespace BetterTeams
{
    public class WebSocketMessage
    {
        public string Action { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }

    public class WebSocketServer
    {
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
                await ProcessMessage(webSocket, receivedMessage);

                Array.Clear(buffer, 0, buffer.Length);
                receiveResult = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
            }

            await webSocket.CloseAsync(
                receiveResult.CloseStatus.Value,
                receiveResult.CloseStatusDescription,
                _cancellationTokenSource.Token);
        }

        private async Task ProcessMessage(WebSocket webSocket, string message)
        {
            try
            {
                var msg = JsonSerializer.Deserialize<WebSocketMessage>(message);
                if (msg == null)
                {
                    Log.Error("Received null message");
                    return;
                }

                Log.Info($"Received action: {msg.Action}");

                switch (msg.Action)
                {
                    case "get_plugins":
                        await SendAvailablePlugins(webSocket);
                        break;
                    case "get_themes":
                        await SendAvailableThemes(webSocket);
                        break;
                    case "install_plugin":
                        if (msg.Data.ContainsKey("id") && msg.Data["id"] is string pluginId)
                        {
                            bool success = await _pluginManager.InstallPlugin(pluginId);
                            await SendResponse(webSocket, "plugin_installed", new { Success = success, Id = pluginId });
                        }
                        break;
                    case "install_theme":
                        if (msg.Data.ContainsKey("id") && msg.Data["id"] is string themeId)
                        {
                            bool success = await _pluginManager.InstallTheme(themeId);
                            await SendResponse(webSocket, "theme_installed", new { Success = success, Id = themeId });
                        }
                        break;
                    case "uninstall_plugin":
                        if (msg.Data.ContainsKey("id") && msg.Data["id"] is string pluginToUninstall)
                        {
                            bool success = _pluginManager.UninstallPlugin(pluginToUninstall);
                            await SendResponse(webSocket, "plugin_uninstalled", new { Success = success, Id = pluginToUninstall });
                        }
                        break;
                    case "uninstall_theme":
                        if (msg.Data.ContainsKey("id") && msg.Data["id"] is string themeToUninstall)
                        {
                            bool success = _pluginManager.UninstallTheme(themeToUninstall);
                            await SendResponse(webSocket, "theme_uninstalled", new { Success = success, Id = themeToUninstall });
                        }
                        break;
                    case "get_installed_plugins":
                        await SendInstalledPlugins(webSocket);
                        break;
                    case "get_installed_themes":
                        await SendInstalledThemes(webSocket);
                        break;
                    case "activate_theme":
                        if (msg.Data.ContainsKey("id") && msg.Data["id"] is string themeToActivate)
                        {
                            await ActivateTheme(webSocket, themeToActivate);
                        }
                        break;
                    case "deactivate_theme":
                        await DeactivateTheme(webSocket);
                        break;
                    case "get_active_theme":
                        await SendActiveTheme(webSocket);
                        break;
                    default:
                        Log.Warning($"Unknown action: {msg.Action}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error processing message: {ex.Message}");
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
    }
} 