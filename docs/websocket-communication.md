# WebSocket Communication in BetterTeams

This document explains how communication between BetterTeams and Microsoft Teams is handled using WebSocket, as an alternative to Playwright's `ExposeBindingAsync`, which has compatibility issues.

## Problem Description

The original communication between BetterTeams and the Microsoft Teams WebView relied on Playwright’s `ExposeBindingAsync`, which exposes a C# function to JavaScript. However, this method has several limitations:

1. It does not work properly when called directly from the Teams JavaScript console
2. It only works when invoked from JavaScript injected using `page.EvaluateAsync()`
3. It limits the ability to debug and test functionality

## Implemented Solution

A bidirectional communication system based on WebSocket was implemented:

1. **C# Server**: `WebSocketServer.cs` handles WebSocket connections and message responses
2. **JavaScript Client**: A `websocket-client` plugin implements a native WebSocket client in JavaScript
3. **Test Client**: A testing utility verifies WebSocket communication

## Structure

### WebSocket Server (C#)

The WebSocket server starts automatically with BetterTeams:

```csharp
_webSocketServer = new WebSocketServer(_pluginManager, _config);
_ = Task.Run(async () => {
    try {
        await _webSocketServer.Start();
    } catch (Exception ex) {
        Log.Error($"WebSocket server error: {ex.Message}");
    }
});
```

The server listens on the port defined in `_config.WebSocketPort` (default: 8097).

### WebSocket Client (JavaScript)

The WebSocket client is implemented as a BetterTeams plugin that can be enabled/disabled:

* Plugin: `websocket-client`
* Main file: `scripts/plugins/websocket-client/main.js`

This client exposes the global API `window.BetterTeamsWS`, offering an interface compatible with the original API but using WebSocket instead of Playwright binding.

### Test Client

For debugging and testing WebSocket communication, a test client is available:

* File: `scripts/betterteams-websocket-test.js`
* Functionality: UI for testing WebSocket communication directly from the Teams webview

## API Usage

### In Third-Party Plugins

Plugins can use the WebSocket API via the global object `window.BetterTeamsWS`:

```javascript
// Register a plugin
document.dispatchEvent(new CustomEvent('betterteams:request-websocket', {
    detail: { plugin: 'my-plugin-id' }
}));

// Send messages
window.BetterTeamsWS.send('my_action', { key: 'value' });

// Listen to events
window.BetterTeamsWS.on('some_event', (data) => {
    console.log('Received event:', data);
});
```

### Available API Methods

#### Sending Messages

* `send(action, data)`: Sends a message to the WebSocket server

  * `action`: String - Action to perform
  * `data`: Object - Associated data

#### Receiving Events

* `on(event, callback)`: Registers a callback for an event

  * `event`: String - Event name
  * `callback`: Function - Function to call when the event is received
* `off(event, callback)`: Removes a callback for an event

#### Utility Methods

* `copyToClipboard(url, type)`: Copies an image to the clipboard

  * `url`: String - Image URL
  * `type`: String - Image type (default: `'text'`)

## Testing and Debugging

### Injecting the Test Client

To test WebSocket communication:

1. Launch BetterTeams
2. Open Teams
3. Open the developer console (F12)
4. Inject and run `scripts/betterteams-websocket-test.js` in the console

A test interface will appear, allowing you to send commands and view responses.

### Example WebSocket Requests

```javascript
// Ping
window.BetterTeamsWS.send('ping');

// Get list of installed plugins
window.BetterTeamsWS.send('get_installed_plugins');

// Get list of available themes
window.BetterTeamsWS.send('get_themes');
```

## Supported WebSocket Server Actions

| Action                  | Description              | Parameters                      |
| ----------------------- | ------------------------ | ------------------------------- |
| `ping`                  | Connection test          | -                               |
| `test_call`             | Test with custom data    | `{ data: string }`              |
| `get_plugins`           | Get available plugins    | -                               |
| `get_themes`            | Get available themes     | -                               |
| `get_installed_plugins` | Get installed plugins    | -                               |
| `get_installed_themes`  | Get installed themes     | -                               |
| `install_plugin`        | Install a plugin         | `{ id: string }`                |
| `install_theme`         | Install a theme          | `{ id: string }`                |
| `uninstall_plugin`      | Uninstall a plugin       | `{ id: string }`                |
| `uninstall_theme`       | Uninstall a theme        | `{ id: string }`                |
| `activate_plugin`       | Activate a plugin        | `{ id: string }`                |
| `deactivate_plugin`     | Deactivate a plugin      | `{ id: string }`                |
| `activate_theme`        | Activate a theme         | `{ id: string }`                |
| `deactivate_theme`      | Deactivate current theme | -                               |
| `get_active_theme`      | Get the active theme     | -                               |
| `copyToClipboard`       | Copy to clipboard        | `{ url: string, type: string }` |

## Server Events

| Event                | Description               | Data                                                       |
| -------------------- | ------------------------- | ---------------------------------------------------------- |
| `connected`          | Connection established    | `{}`                                                       |
| `pong`               | Ping response             | `{ timestamp: string }`                                    |
| `test_response`      | Test response             | `{ message: string, timestamp: string }`                   |
| `available_plugins`  | List of available plugins | `{ Plugins: PluginInfo[] }`                                |
| `available_themes`   | List of available themes  | `{ Themes: PluginInfo[] }`                                 |
| `installed_plugins`  | List of installed plugins | `{ Plugins: PluginInfo[] }`                                |
| `installed_themes`   | List of installed themes  | `{ Themes: PluginInfo[], ActiveThemeId: string }`          |
| `plugin_installed`   | Plugin installed          | `{ Success: boolean, Id: string }`                         |
| `theme_installed`    | Theme installed           | `{ Success: boolean, Id: string }`                         |
| `plugin_uninstalled` | Plugin uninstalled        | `{ Success: boolean, Id: string }`                         |
| `theme_uninstalled`  | Theme uninstalled         | `{ Success: boolean, Id: string }`                         |
| `pluginActivated`    | Plugin activated          | `{ success: boolean }`                                     |
| `pluginDeactivated`  | Plugin deactivated        | `{ success: boolean }`                                     |
| `theme_activated`    | Theme activated           | `{ Success: boolean, ThemeId: string, ThemeName: string }` |
| `theme_deactivated`  | Theme deactivated         | `{ Success: boolean }`                                     |
| `active_theme`       | Active theme information  | `{ ThemeId: string, ThemeName?: string }`                  |
| `reinject_required`  | Reinjection required      | `{}`                                                       |
| `error`              | Error occurred            | `{ message: string }`                                      |

## Advantages of This Approach

1. **Greater reliability**: WebSocket works consistently, regardless of how it is triggered
2. **Easier debugging**: Enables direct testing and debugging from the browser console
3. **Performance**: Asynchronous and lightweight communication
4. **Robustness**: Built-in error handling and auto-reconnection
5. **Extensibility**: Easy to add new features while maintaining compatibility with the old system

