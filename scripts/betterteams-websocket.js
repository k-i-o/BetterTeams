(function() {
    // Check if the WebSocket client is already initialized
    if (window.BetterTeamsWebSocket) {
        console.log('BetterTeams WebSocket client already initialized');
        return;
    }

    class BetterTeamsWebSocketClient {
        constructor() {
            this.socket = null;
            this.connected = false;
            this.reconnectAttempts = 0;
            this.maxReconnectAttempts = 5;
            this.reconnectDelay = 2000; // Start with 2 seconds
            this.eventListeners = {};
            this.pendingMessages = [];
            this.plugins = {};
            
            // Find the WebSocket port from the bridge element or use default
            this.port = this.findWebSocketPort() || 8080;
            
            // Initialize the WebSocket connection
            this.connect();
            
            // Create a debug UI element (hidden by default)
            this.createDebugUI();
            
            console.log(`BetterTeams WebSocket client initialized on port ${this.port}`);
        }
        
        findWebSocketPort() {
            const bridgeElement = document.getElementById('betterteams-bridge');
            return bridgeElement ? parseInt(bridgeElement.getAttribute('data-port')) : null;
        }
        
        connect() {
            try {
                this.socket = new WebSocket(`ws://localhost:${this.port}`);
                
                this.socket.onopen = () => {
                    console.log('WebSocket connection established');
                    this.connected = true;
                    this.reconnectAttempts = 0;
                    this.reconnectDelay = 2000;
                    
                    // Send any pending messages
                    while (this.pendingMessages.length > 0) {
                        const message = this.pendingMessages.shift();
                        this.sendRaw(message);
                    }
                    
                    // Emit connected event
                    this.emit('connected', {});
                    
                    // Update debug UI
                    this.updateDebugUI('Connected');
                };
                
                this.socket.onmessage = (event) => {
                    try {
                        const message = JSON.parse(event.data);
                        if (message.action) {
                            console.log(`Received WebSocket message: ${message.action}`);
                            this.emit(message.action, message.data || {});
                            
                            // Update debug UI
                            this.updateDebugUI(`Received: ${message.action}`);
                        }
                    } catch (error) {
                        console.error('Error parsing WebSocket message:', error);
                    }
                };
                
                this.socket.onclose = () => {
                    console.log('WebSocket connection closed');
                    this.connected = false;
                    
                    // Update debug UI
                    this.updateDebugUI('Disconnected');
                    
                    // Attempt to reconnect
                    this.reconnect();
                };
                
                this.socket.onerror = (error) => {
                    console.error('WebSocket error:', error);
                    
                    // Update debug UI
                    this.updateDebugUI('Error: ' + (error.message || 'Unknown error'));
                };
            } catch (error) {
                console.error('Error creating WebSocket:', error);
                
                // Update debug UI
                this.updateDebugUI('Connection Error');
                
                // Attempt to reconnect
                this.reconnect();
            }
        }
        
        reconnect() {
            if (this.reconnectAttempts >= this.maxReconnectAttempts) {
                console.error('Max reconnect attempts reached, giving up');
                this.updateDebugUI('Failed to connect after multiple attempts');
                return;
            }
            
            this.reconnectAttempts++;
            const delay = this.reconnectDelay * Math.pow(1.5, this.reconnectAttempts - 1);
            
            console.log(`Attempting to reconnect in ${delay}ms (attempt ${this.reconnectAttempts}/${this.maxReconnectAttempts})`);
            this.updateDebugUI(`Reconnecting in ${delay}ms...`);
            
            setTimeout(() => {
                this.connect();
            }, delay);
        }
        
        send(action, data = {}) {
            const message = {
                action: action,
                data: data
            };
            
            return this.sendRaw(message);
        }
        
        sendRaw(message) {
            if (!this.connected) {
                console.warn('WebSocket not connected, queueing message');
                this.pendingMessages.push(message);
                return false;
            }
            
            try {
                this.socket.send(JSON.stringify(message));
                
                // Update debug UI
                this.updateDebugUI(`Sent: ${message.action || 'unknown'}`);
                return true;
            } catch (error) {
                console.error('Error sending WebSocket message:', error);
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
            this.eventListeners[event].forEach(callback => {
                try {
                    callback(data);
                } catch (error) {
                    console.error(`Error in event listener for ${event}:`, error);
                }
            });
        }
        
        // Register a plugin to use WebSocket functionality
        registerPlugin(pluginId, features = []) {
            this.plugins[pluginId] = { features };
            console.log(`Plugin ${pluginId} registered for WebSocket access`);
            
            // Notify the plugin that WebSocket is ready
            document.dispatchEvent(new CustomEvent('betterteams:websocket-ready', {
                detail: { pluginId }
            }));
            
            return true;
        }
        
        // Handle clipboard operations for plugins
        copyToClipboard(url, type = 'gif') {
            return this.send('copyToClipboard', { url, type });
        }
        
        createDebugUI() {
            const debugDiv = document.createElement('div');
            debugDiv.id = 'betterteams-websocket-debug';
            debugDiv.style.cssText = `
                position: fixed;
                bottom: 10px;
                right: 10px;
                background-color: rgba(0, 0, 0, 0.7);
                color: white;
                padding: 10px;
                border-radius: 5px;
                font-family: monospace;
                font-size: 12px;
                z-index: 9999;
                max-width: 300px;
                max-height: 200px;
                overflow: auto;
                display: none;
            `;
            
            const statusDiv = document.createElement('div');
            statusDiv.id = 'betterteams-websocket-status';
            statusDiv.textContent = 'Initializing...';
            debugDiv.appendChild(statusDiv);
            
            const logDiv = document.createElement('div');
            logDiv.id = 'betterteams-websocket-log';
            debugDiv.appendChild(logDiv);
            
            const toggleButton = document.createElement('button');
            toggleButton.textContent = 'Debug';
            toggleButton.style.cssText = `
                position: fixed;
                bottom: 10px;
                right: 10px;
                background-color: #0078d4;
                color: white;
                border: none;
                border-radius: 3px;
                padding: 5px 10px;
                font-size: 12px;
                cursor: pointer;
                z-index: 10000;
                opacity: 0.5;
            `;
            toggleButton.onclick = () => {
                const isVisible = debugDiv.style.display === 'block';
                debugDiv.style.display = isVisible ? 'none' : 'block';
                toggleButton.style.opacity = isVisible ? '0.5' : '1';
            };
            
            document.body.appendChild(debugDiv);
            document.body.appendChild(toggleButton);
            
            this.debugUI = {
                container: debugDiv,
                status: statusDiv,
                log: logDiv,
                toggle: toggleButton
            };
        }
        
        updateDebugUI(status) {
            if (!this.debugUI) return;
            
            this.debugUI.status.textContent = status;
            
            const logEntry = document.createElement('div');
            logEntry.textContent = `[${new Date().toLocaleTimeString()}] ${status}`;
            this.debugUI.log.appendChild(logEntry);
            
            // Keep only the last 10 entries
            while (this.debugUI.log.children.length > 10) {
                this.debugUI.log.removeChild(this.debugUI.log.firstChild);
            }
            
            // Auto-scroll to bottom
            this.debugUI.log.scrollTop = this.debugUI.log.scrollHeight;
        }
    }
    
    // Initialize the WebSocket client
    window.BetterTeamsWebSocket = new BetterTeamsWebSocketClient();
    
    // Make it available for plugins
    window.betterTeams = {
        sendWebSocketMessage: (message) => {
            return window.BetterTeamsWebSocket.sendRaw(message);
        },
        copyToClipboard: (url, type) => {
            return window.BetterTeamsWebSocket.copyToClipboard(url, type);
        }
    };
    
    // Listen for plugin registration requests
    document.addEventListener('betterteams:request-websocket', (event) => {
        if (event.detail && event.detail.plugin) {
            window.BetterTeamsWebSocket.registerPlugin(
                event.detail.plugin,
                event.detail.features || []
            );
        }
    });
})(); 