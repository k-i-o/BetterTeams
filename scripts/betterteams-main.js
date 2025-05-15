(function () {

    window.policy = null;
    if (window.trustedTypes) {
        try {
            window.policy = trustedTypes.createPolicy(
                '@msteams/frameworks-loader#dompurify',
                { 
                    createHTML: (input) => input,
                    createScriptURL: (input) => input
                }
            );
        } catch (e) {
            console.error('Error creating policy:', e);
        }
    }
    
    window.injectHtmlWithPolicy = (html, eventListenerType = 'click', eventListener = null) => {
        const tempDiv = document.createElement('div');
        if (window.policy) {
            tempDiv.innerHTML = window.policy.createHTML(html);
        } else {
            tempDiv.innerHTML = html;
        }
        const element = tempDiv.firstElementChild;
        if (eventListener) {
            element.addEventListener(eventListenerType, eventListener);
        }
        return element;
    }



    function injectBetterTeamsSettings(container) {
        container.appendChild(injectHtmlWithPolicy(`
            <div data-testid="betterteams-section">
                <h2>BetterTeams Core Settings</h2>
                <label>
                    <input type="checkbox" id="bt-feature-x">
                    Enable feature X
                </label>
            </div>
        `));
    }

    function addSettingsButton(list) {

        if (!list.querySelector('[data-tid="betterteams_settings"]')) {
            try {
                const btn = injectHtmlWithPolicy(`
                    <button data-tid="betterteams_settings" style="color:white; padding-top: 10px; padding-left: 15px" class="fui-Tab ___156ub8c f122n59 faev5xe fgaxf95 f1k6fduh f13qh94s fi64zpg f1u07yai frn2hmy f1olsevy fk6fouc f1i3iumi f1s6fcnf f10pi13n f1a3p1vp f1cxpek8 f1s9ku6b f1rjii52 fk9x6wc f8hki3x f1d2448m f1bjia2o ffh67wi f1p7hgxw f1way5bb f9znhxp fqa318h f1vjpng2 f1c21dwh f9rvdkv f1051ucx fmmjozx fqhzt5g f7l5cgy fpkze5g f1iywnoi f9n45c4 fhw179n fg9j5n4 f1kmhr4c fl1ydde f1y7maxz fceyvr4 f16cxu0 f1nwgacf f15ovonk fvje46l f17jracn f1fzr1x6 f117lcb2 f1aij3q f6sp5hn fh08h3f fodoguf f11ysow2 f1g2edtw f1rgs53l f1pgmu2d fjdgwu1 fzpis3h f1k8kk0z f1wv0b92 f1crroip f1b7nj7s f1mhi33d f1ei0m12 f1145swg f4fr5yb f1i88shb f1qaicya fr3lfoh fhr3a7c f1b20q4f f1o9jjeg f16i1l7t f1bcxn2w frcd6pn">
                        BetterTeams Settings
                    </button>`, 'click', () => {
                    const main = document.querySelector(
                        '[data-tid="app-layout-area--main"] [data-tid="slot-measurer"] .fui-Flex'
                    );
                    if (main) injectBetterTeamsSettings(main);
                });
                const aboutButton = list.querySelector('[data-tid="about"]');
                if (aboutButton) {
                    list.insertBefore(btn, aboutButton);
                } else {
                    list.appendChild(btn);
                }
            } catch (e) {
                console.error('Error adding settings button:', e);
            }
        }
    }

    const observer = new MutationObserver((mutations) => {
        for (const m of mutations) {
            for (const n of m.addedNodes) {
                if (!(n instanceof HTMLElement)) continue;

                const settingsList = n.matches('[data-tid="settings-list"]')
                    ? n
                    : n.querySelector('[data-tid="settings-list"]');
                if (settingsList) {
                    console.log('Found settings list, adding button');
                    addSettingsButton(settingsList);
                }
            }
        }
    });

    observer.observe(document.body, { childList: true, subtree: true });


    
    console.log('WebSocket Client plugin initializing...');
    
    /**
     * Native WebSocket client for BetterTeams
     * This client uses direct WebSocket connection instead of Playwright bindings
     */
    class BetterTeamsDirectClient {
        constructor() {
            this.wsPort = 8097; // Default port for BetterTeams WebSocket
            this.socket = null;
            this.connected = false;
            this.reconnectAttempts = 0;
            this.maxReconnectAttempts = 5;
            this.reconnectDelay = 2000;
            this.eventListeners = {};
            this.pendingMessages = [];
            
            // Create the global API
            this.createGlobalAPI();
            
            // Connect to the WebSocket server
            this.connect();
        }
        
        createGlobalAPI() {
            if (window.BetterTeamsWS) {
                console.log('BetterTeamsWS already exists, not replacing existing client');
                return;
            }
            
            window.BetterTeamsWS = {
                isConnected: () => this.connected,
                send: (action, data = {}) => this.send(action, data),
                on: (event, callback) => this.on(event, callback),
                off: (event, callback) => this.off(event, callback),
                copyToClipboard: (url, type = "text") => {
                    return this.send("copyToClipboard", { url, type });
                },
                registerPlugin: (pluginId, features = []) => {
                    console.log(`Plugin ${pluginId} registered for WebSocket access`);
                    document.dispatchEvent(new CustomEvent('betterteams:websocket-ready', {
                        detail: { pluginId }
                    }));
                    return true;
                }
            };
            
            console.log('BetterTeamsWS global API created');
        }
        
        connect() {
            if (this.socket && (this.socket.readyState === WebSocket.CONNECTING || this.socket.readyState === WebSocket.OPEN)) {
                return;
            }
            
            try {
                const url = `ws://localhost:${this.wsPort}`;
                console.log(`Connecting to BetterTeams WebSocket server at ${url}...`);
                
                this.socket = new WebSocket(url);
                
                this.socket.onopen = () => {
                    this.connected = true;
                    this.reconnectAttempts = 0;
                    console.log('Connected to BetterTeams WebSocket server');
                    
                    // Send any pending messages
                    while (this.pendingMessages.length > 0) {
                        const msg = this.pendingMessages.shift();
                        this.doSend(msg);
                    }
                    
                    // Emit connected event
                    this.emit('connected', {});
                };
                
                this.socket.onclose = (event) => {
                    this.connected = false;
                    console.log(`WebSocket connection closed (${event.code}: ${event.reason || 'No reason provided'})`);
                    
                    if (this.reconnectAttempts < this.maxReconnectAttempts) {
                        this.reconnectAttempts++;
                        console.log(`Reconnecting (attempt ${this.reconnectAttempts}/${this.maxReconnectAttempts})...`);
                        
                        setTimeout(() => this.connect(), this.reconnectDelay);
                    } else {
                        console.warn('Max reconnect attempts reached');
                    }
                };
                
                this.socket.onerror = (error) => {
                    console.error('WebSocket error:', error);
                };
                
                this.socket.onmessage = (event) => {
                    try {
                        const message = JSON.parse(event.data);
                        
                        if (message.Action && message.Action !== 'pong') {
                            console.log(`Received WebSocket message: ${message.Action}`, message);
                        }
                        
                        if (message.Action) {
                            this.emit(message.Action, message.Data || {});
                        }
                    } catch (e) {
                        console.error('Error parsing WebSocket message:', e, event.data);
                    }
                };
            } catch (error) {
                console.error('Error connecting to WebSocket:', error);
            }
        }
        
        send(action, data = {}) {
            const message = {
                action: action,
                ...data
            };
            
            if (action !== 'ping') {
                console.log(`Sending WebSocket message: ${action}`, message);
            }
            
            if (!this.connected) {
                console.warn('Not connected to WebSocket server, queuing message');
                this.pendingMessages.push(message);
                return false;
            }
            
            return this.doSend(message);
        }
        
        doSend(message) {
            try {
                this.socket.send(JSON.stringify(message));
                return true;
            } catch (e) {
                console.error('Error sending WebSocket message:', e);
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
                } catch (e) {
                    console.error(`Error in event listener for ${event}:`, e);
                }
            });
        }
    }
    
    const client = new BetterTeamsDirectClient();
    
    client.on('reinject_required', () => {
        console.log('Server requested reinjection - this may cause the page to reload');
    });
    
    client.on('error', (data) => {
        console.error('Server error:', data.message);
    });
    
    client.on('active_theme', (data) => {
        
    });
    
    setInterval(() => {
        if (client.connected) {
            client.send('ping');
        }
    }, 30000);
        
    console.log('WebSocket Client plugin initialized');
})();
