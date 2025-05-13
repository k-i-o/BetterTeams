# BetterTeams Plugin Development Guide

This guide explains how to create plugins and themes for BetterTeams, the extension that enhances the Microsoft Teams experience.

## Plugin Structure

A BetterTeams plugin must follow this directory structure:

```
plugin-name/
├── manifest.json    # Plugin metadata
├── main.js          # Main script
├── assets/          # (Optional) Resource files
└── styles/          # (Optional) Additional CSS files
```

### manifest.json File

The `manifest.json` contains the plugin metadata and has the following structure:

```json
{
  "id": "unique-folder-name",
  "name": "Plugin Name",                 // Plugin name
  "description": "Plugin description",   // Plugin description
  "version": "1.0.0",                   // Version in semver format
  "author": "Developer Name",           // Developer/team name
  "repository": "https://github.com/username/repo", // Repository URL
  "main": "main.js"                    // Main plugin file
}
```
### main.js File

The `main.js` file is the entry point of the plugin. We recommend wrapping the code in an IIFE (Immediately Invoked Function Expression) to avoid conflicts with the global scope:

```javascript
(function() {
    // Plugin code here
    
    // Example of how to add styles
    function injectStyles() {
        const style = document.createElement('style');
        style.textContent = `
            .my-plugin-class {
                color: red;
            }
        `;
        document.head.appendChild(style);
    }
    
    // Initialization
    function init() {
        console.log('My plugin has been loaded!');
        injectStyles();
    }
    
    // Call initialization
    init();
})();
```

## Communication with the Backend

BetterTeams provides a global WebSocket object `window.BetterTeamsWS` to communicate with the C# backend. You can use it to send and receive messages:

### Sending Messages

```javascript
// Check that WebSocket is available
if (window.BetterTeamsWS) {
    // Send a message to the backend
    window.BetterTeamsWS.send('action_name', { key: 'value' });
}
```

### Receiving Messages

```javascript
if (window.BetterTeamsWS) {
    // Listen for messages from the backend
    window.BetterTeamsWS.on('event_name', (data) => {
        console.log('Received event:', data);
    });
}
```

## Theme Development

Themes follow the same structure as plugins, but their main purpose is to modify the visual appearance of Teams.

Theme example:

```javascript
(function() {
    // Inject the theme CSS
    const style = document.createElement('style');
    style.textContent = `
        /* Customize Teams colors */
        :root {
            --background-color: #121212;
            --text-color: #ffffff;
        }
        
        /* Apply custom colors */
        body {
            background-color: var(--background-color) !important;
            color: var(--text-color) !important;
        }
    `;
    document.head.appendChild(style);
})();
```

## Packaging and Distribution

To distribute your plugin/theme:

1. Create the directory structure as described above
2. Compress the directory into a ZIP file
3. Upload the file to a public repository (GitHub, GitLab, etc.)
4. Contact the BetterTeams team to include your plugin/theme in the marketplace

## Best Practices

1. **Do not interfere** with essential Teams functionality
2. **Handle errors** gracefully
3. **Optimize performance** by avoiding costly operations and unnecessary DOM observers
4. **Maintain compatibility** with future versions of Teams
5. **Test thoroughly** your plugin with different versions of Teams
6. **Provide clear documentation** on how to use your plugin
7. **Use semantic versioning** for releases

## Available APIs and Hooks

BetterTeams provides some APIs and hooks that you can use in your plugins:

### DOM Observer Helper

```javascript
function observeElement(selector, callback) {
    const observer = new MutationObserver((mutations) => {
        for (const m of mutations) {
            for (const n of m.addedNodes) {
                if (!(n instanceof HTMLElement)) continue;
                
                const el = n.matches(selector) ? n : n.querySelector(selector);
                if (el) callback(el);
            }
        }
    });
    
    observer.observe(document.body, { childList: true, subtree: true });
    
    // Check existing elements
    document.querySelectorAll(selector).forEach(callback);
    
    return observer;
}

// Usage example
observeElement('[data-tid="team-name"]', (element) => {
    // Do something with the element
});
```

## Troubleshooting

- **Plugin not loading**: Verify that the directory structure is correct
- **Plugin not working**: Check the console for any errors
- **Plugin slows down Teams**: Optimize your code, especially DOM observers

## Contact

For support or questions about plugin development, contact the BetterTeams team:

- GitHub: [BetterTeams Repository](https://github.com/username/BetterTeams)
- Email: support@betterteams.example.com 