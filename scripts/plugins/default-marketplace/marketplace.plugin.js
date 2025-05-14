(function() {
    // Plugin Marketplace for BetterTeams
    // Author: BetterTeams Community
    // Version: 1.0.0
    
    if (!window.BetterTeamsWS) {
        console.error('BetterTeamsWS not initialized');
        return;
    }
    
    const ws = window.BetterTeamsWS;
    
    // Create UI elements for injecting
    function createMarketplaceUI() {
        return `
        <div data-tid="betterteams-marketplace" class="marketplace-container">
            <div class="marketplace-header">
                <h2>BetterTeams Marketplace</h2>
                <div class="marketplace-tabs">
                    <button class="marketplace-tab active" data-tab="plugins">Plugins</button>
                    <button class="marketplace-tab" data-tab="themes">Themes</button>
                </div>
            </div>
            
            <div class="marketplace-content">
                <div class="marketplace-tab-content active" data-tab-content="plugins">
                    <div class="marketplace-search">
                        <input type="text" placeholder="Search plugins..." class="marketplace-search-input">
                    </div>
                    <div class="marketplace-items plugins-list">
                        <div class="marketplace-loading">Loading plugins...</div>
                    </div>
                </div>
                
                <div class="marketplace-tab-content" data-tab-content="themes">
                    <div class="marketplace-search">
                        <input type="text" placeholder="Search themes..." class="marketplace-search-input">
                    </div>
                    <div class="marketplace-items themes-list">
                        <div class="marketplace-loading">Loading themes...</div>
                    </div>
                </div>
            </div>
        </div>
        `;
    }
    
    // CSS styles for the marketplace
    function injectMarketplaceStyles() {
        const style = document.createElement('style');
        style.textContent = `
            .marketplace-container {
                padding: 20px;
                height: 100%;
                overflow-y: auto;
                color: var(--colorNeutralForeground1);
            }
            
            .marketplace-header {
                margin-bottom: 20px;
            }
            
            .marketplace-tabs {
                display: flex;
                border-bottom: 1px solid var(--colorNeutralStroke1);
                margin-top: 15px;
            }
            
            .marketplace-tab {
                padding: 8px 16px;
                background: none;
                border: none;
                color: var(--colorNeutralForeground1);
                cursor: pointer;
                font-size: 14px;
                border-bottom: 2px solid transparent;
            }
            
            .marketplace-tab.active {
                border-bottom: 2px solid var(--colorBrandBackground);
                color: var(--colorBrandBackground);
            }
            
            .marketplace-tab-content {
                display: none;
                padding-top: 15px;
            }
            
            .marketplace-tab-content.active {
                display: block;
            }
            
            .marketplace-search {
                margin-bottom: 15px;
            }
            
            .marketplace-search-input {
                width: 100%;
                padding: 8px 12px;
                border: 1px solid var(--colorNeutralStroke1);
                border-radius: 4px;
                background: var(--colorNeutralBackground1);
                color: var(--colorNeutralForeground1);
            }
            
            .marketplace-items {
                display: grid;
                grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
                gap: 16px;
            }
            
            .marketplace-item {
                border: 1px solid var(--colorNeutralStroke1);
                border-radius: 6px;
                padding: 16px;
                background: var(--colorNeutralBackground1);
                position: relative;
            }
            
            .marketplace-item-name {
                font-weight: bold;
                margin-bottom: 4px;
                font-size: 16px;
            }
            
            .marketplace-item-author {
                font-size: 12px;
                color: var(--colorNeutralForeground3);
                margin-bottom: 8px;
            }
            
            .marketplace-item-description {
                font-size: 14px;
                margin-bottom: 12px;
            }
            
            .marketplace-item-actions {
                display: flex;
                justify-content: space-between;
                align-items: center;
            }
            
            .marketplace-item-version {
                font-size: 12px;
                color: var(--colorNeutralForeground3);
            }
            
            .marketplace-item-buttons {
                display: flex;
                gap: 8px;
            }
            
            .marketplace-item-button {
                padding: 6px 12px;
                border: none;
                border-radius: 4px;
                cursor: pointer;
                background: var(--colorBrandBackground);
                color: var(--colorNeutralBackground1);
                font-size: 12px;
            }
            
            .marketplace-item-button.uninstall {
                background: var(--colorStatusDangerBackground);
            }
            
            .marketplace-item-button.deactivate {
                background: var(--colorNeutralForeground3);
            }
            
            .marketplace-item-button.activate {
                background: var(--colorStatusSuccessBackground);
            }
            
            .marketplace-item-button:disabled {
                opacity: 0.5;
                cursor: not-allowed;
            }
            
            .marketplace-item-installed {
                position: absolute;
                top: 10px;
                right: 10px;
                background: var(--colorStatusSuccessBackground);
                color: var(--colorStatusSuccessForeground1);
                font-size: 10px;
                padding: 2px 6px;
                border-radius: 10px;
            }
            
            .marketplace-loading {
                grid-column: 1 / -1;
                text-align: center;
                padding: 20px;
                color: var(--colorNeutralForeground3);
            }
        `;
        document.head.appendChild(style);
    }
    
    function renderPluginItem(plugin, isInstalled) {
        const isActive = isInstalled && plugin.isActive !== false;
        
        return `
        <div class="marketplace-item" data-id="${plugin.id}">
            ${isInstalled ? '<div class="marketplace-item-installed">Installed</div>' : ''}
            <div class="marketplace-item-name">${plugin.name}</div>
            <div class="marketplace-item-author">by ${plugin.author}</div>
            <div class="marketplace-item-description">${plugin.description}</div>
            <div class="marketplace-item-actions">
                <div class="marketplace-item-version">v${plugin.version}</div>
                <div class="marketplace-item-buttons">
                    ${isInstalled 
                        ? `<button class="marketplace-item-button uninstall">Uninstall</button>
                           ${isActive 
                              ? '<button class="marketplace-item-button deactivate">Deactivate</button>' 
                              : '<button class="marketplace-item-button activate">Activate</button>'}`
                        : '<button class="marketplace-item-button">Install</button>'}
                </div>
            </div>
        </div>
        `;
    }
    
    function renderThemeItem(theme, isInstalled) {
        return `
        <div class="marketplace-item" data-id="${theme.id}">
            ${isInstalled ? '<div class="marketplace-item-installed">Installed</div>' : ''}
            <div class="marketplace-item-name">${theme.name}</div>
            <div class="marketplace-item-author">by ${theme.author}</div>
            <div class="marketplace-item-description">${theme.description}</div>
            <div class="marketplace-item-actions">
                <div class="marketplace-item-version">v${theme.version}</div>
                ${isInstalled 
                    ? '<button class="marketplace-item-button uninstall">Uninstall</button>' 
                    : '<button class="marketplace-item-button">Install</button>'}
            </div>
        </div>
        `;
    }
    
    // Load plugins from websocket
    function loadPlugins() {
        const pluginsList = document.querySelector('.plugins-list');
        if (!pluginsList) return;
        
        pluginsList.innerHTML = '<div class="marketplace-loading">Loading plugins...</div>';
        
        // Get installed plugins
        ws.send('get_installed_plugins');
        
        // Listen for installed plugins response
        ws.on('installed_plugins', (data) => {
            // Get available plugins
            ws.send('get_plugins');
            
            // Store installed plugins
            window.installedPlugins = data.plugins || [];
        });
        
        // Listen for available plugins response
        ws.on('available_plugins', (data) => {
            const plugins = data.plugins || [];
            
            if (plugins.length === 0) {
                pluginsList.innerHTML = '<div class="marketplace-loading">No plugins available</div>';
                return;
            }
            
            pluginsList.innerHTML = '';
            
            plugins.forEach(plugin => {
                const isInstalled = window.installedPlugins?.some(p => p.id === plugin.id) || false;
                const pluginHtml = renderPluginItem(plugin, isInstalled);
                pluginsList.insertAdjacentHTML('beforeend', pluginHtml);
            });
            
            // Add event listeners to install/uninstall buttons
            pluginsList.querySelectorAll('.marketplace-item-button').forEach(button => {
                button.addEventListener('click', (e) => {
                    const pluginId = e.target.closest('.marketplace-item').dataset.id;
                    const isUninstall = e.target.classList.contains('uninstall');
                    
                    if (isUninstall) {
                        ws.send('uninstall_plugin', { id: pluginId });
                        e.target.disabled = true;
                        e.target.textContent = 'Uninstalling...';
                    } else {
                        ws.send('install_plugin', { id: pluginId });
                        e.target.disabled = true;
                        e.target.textContent = 'Installing...';
                    }
                });
            });
        });
    }
    
    // Load themes from websocket
    function loadThemes() {
        const themesList = document.querySelector('.themes-list');
        if (!themesList) return;
        
        themesList.innerHTML = '<div class="marketplace-loading">Loading themes...</div>';
        
        // Get installed themes
        ws.send('get_installed_themes');
        
        // Listen for installed themes response
        ws.on('installed_themes', (data) => {
            // Get available themes
            ws.send('get_themes');
            
            // Store installed themes
            window.installedThemes = data.themes || [];
        });
        
        // Listen for available themes response
        ws.on('available_themes', (data) => {
            const themes = data.themes || [];
            
            if (themes.length === 0) {
                themesList.innerHTML = '<div class="marketplace-loading">No themes available</div>';
                return;
            }
            
            themesList.innerHTML = '';
            
            themes.forEach(theme => {
                const isInstalled = window.installedThemes?.some(t => t.id === theme.id) || false;
                const themeHtml = renderThemeItem(theme, isInstalled);
                themesList.insertAdjacentHTML('beforeend', themeHtml);
            });
            
            // Add event listeners to install/uninstall buttons
            themesList.querySelectorAll('.marketplace-item-button').forEach(button => {
                button.addEventListener('click', (e) => {
                    const themeId = e.target.closest('.marketplace-item').dataset.id;
                    const isUninstall = e.target.classList.contains('uninstall');
                    
                    if (isUninstall) {
                        ws.send('uninstall_theme', { id: themeId });
                        e.target.disabled = true;
                        e.target.textContent = 'Uninstalling...';
                    } else {
                        ws.send('install_theme', { id: themeId });
                        e.target.disabled = true;
                        e.target.textContent = 'Installing...';
                    }
                });
            });
        });
    }
    
    // Listen for installation events
    function setupEventListeners() {
        ws.on('plugin_installed', (data) => {
            loadPlugins(); // Reload plugins list
        });
        
        ws.on('theme_installed', (data) => {
            loadThemes(); // Reload themes list
        });
        
        ws.on('plugin_uninstalled', (data) => {
            loadPlugins(); // Reload plugins list
        });
        
        ws.on('theme_uninstalled', (data) => {
            loadThemes(); // Reload themes list
        });
        
        // Listen for connected event to reload data
        ws.on('connected', () => {
            loadPlugins();
            loadThemes();
        });
        
        // Plugin installation and uninstallation
        document.addEventListener('click', (e) => {
            if (e.target.matches('.marketplace-item-button:not(.uninstall):not(.activate):not(.deactivate)')) {
                const item = e.target.closest('.marketplace-item');
                if (!item) return;
                
                const id = item.dataset.id;
                if (!id) return;
                
                e.target.disabled = true;
                
                ws.send('install_plugin', { id });
            }
            
            if (e.target.matches('.marketplace-item-button.uninstall')) {
                const item = e.target.closest('.marketplace-item');
                if (!item) return;
                
                const id = item.dataset.id;
                if (!id) return;
                
                if (confirm(`Are you sure you want to uninstall this plugin?`)) {
                    e.target.disabled = true;
                    
                    ws.send('uninstall_plugin', { id });
                }
            }
            
            if (e.target.matches('.marketplace-item-button.activate')) {
                const item = e.target.closest('.marketplace-item');
                if (!item) return;
                
                const id = item.dataset.id;
                if (!id) return;
                
                e.target.disabled = true;
                
                ws.send('activatePlugin', { id });
            }
            
            if (e.target.matches('.marketplace-item-button.deactivate')) {
                const item = e.target.closest('.marketplace-item');
                if (!item) return;
                
                const id = item.dataset.id;
                if (!id) return;
                
                e.target.disabled = true;
                
                ws.send('deactivatePlugin', { id });
            }
        });
    }
    
    // Add marketplace button to settings
    function addMarketplaceButton(list) {
        if (!list.querySelector('[data-tid="betterteams_marketplace"]')) {
            try {
                const btn = document.createElement('button');
                btn.dataset.tid = "betterteams_marketplace";
                btn.className = "fui-Tab ___156ub8c f122n59 faev5xe fgaxf95 f1k6fduh f13qh94s fi64zpg f1u07yai frn2hmy f1olsevy fk6fouc f1i3iumi f1s6fcnf f10pi13n f1a3p1vp f1cxpek8 f1s9ku6b f1rjii52 fk9x6wc f8hki3x f1d2448m f1bjia2o ffh67wi f1p7hgxw f1way5bb f9znhxp fqa318h f1vjpng2 f1c21dwh f9rvdkv f1051ucx fmmjozx fqhzt5g f7l5cgy fpkze5g f1iywnoi f9n45c4 fhw179n fg9j5n4 f1kmhr4c fl1ydde f1y7maxz fceyvr4 f16cxu0 f1nwgacf f15ovonk fvje46l f17jracn f1fzr1x6 f117lcb2 f1aij3q f6sp5hn fh08h3f fodoguf f11ysow2 f1g2edtw f1rgs53l f1pgmu2d fjdgwu1 fzpis3h f1k8kk0z f1wv0b92 f1crroip f1b7nj7s f1mhi33d f1ei0m12 f1145swg f4fr5yb f1i88shb f1qaicya fr3lfoh fhr3a7c f1b20q4f f1o9jjeg f16i1l7t f1bcxn2w frcd6pn";
                btn.style.color = "white";
                btn.style.paddingTop = "10px";
                btn.style.paddingLeft = "15px";
                btn.textContent = "BetterTeams Marketplace";
                
                btn.addEventListener('click', () => {
                    const main = document.querySelector(
                        '[data-tid="app-layout-area--main"] [data-tid="slot-measurer"] .fui-Flex'
                    );
                    
                    if (main) {
                        // Remove existing marketplace UI if any
                        const existingMarketplace = document.querySelector('[data-tid="betterteams-marketplace"]');
                        if (existingMarketplace) {
                            existingMarketplace.remove();
                        }
                        
                        // Inject marketplace UI
                        const marketplaceDiv = document.createElement('div');
                        marketplaceDiv.innerHTML = createMarketplaceUI();
                        main.appendChild(marketplaceDiv.firstElementChild);
                        
                        // Setup tabs
                        const tabs = document.querySelectorAll('.marketplace-tab');
                        tabs.forEach(tab => {
                            tab.addEventListener('click', () => {
                                // Deactivate all tabs
                                tabs.forEach(t => t.classList.remove('active'));
                                // Activate the clicked tab
                                tab.classList.add('active');
                                
                                // Hide all tab content
                                document.querySelectorAll('.marketplace-tab-content').forEach(content => {
                                    content.classList.remove('active');
                                });
                                
                                // Show the corresponding tab content
                                const tabName = tab.dataset.tab;
                                document.querySelector(`[data-tab-content="${tabName}"]`).classList.add('active');
                            });
                        });
                        
                        // Load plugins and themes
                        loadPlugins();
                        loadThemes();
                        
                        // Setup search
                        document.querySelectorAll('.marketplace-search-input').forEach(input => {
                            input.addEventListener('input', (e) => {
                                const searchTerm = e.target.value.toLowerCase();
                                const tabContent = e.target.closest('.marketplace-tab-content');
                                const items = tabContent.querySelectorAll('.marketplace-item');
                                
                                items.forEach(item => {
                                    const name = item.querySelector('.marketplace-item-name').textContent.toLowerCase();
                                    const description = item.querySelector('.marketplace-item-description').textContent.toLowerCase();
                                    const author = item.querySelector('.marketplace-item-author').textContent.toLowerCase();
                                    
                                    if (name.includes(searchTerm) || description.includes(searchTerm) || author.includes(searchTerm)) {
                                        item.style.display = 'block';
                                    } else {
                                        item.style.display = 'none';
                                    }
                                });
                            });
                        });
                    }
                });
                
                const aboutButton = list.querySelector('[data-tid="about"]');
                if (aboutButton) {
                    list.insertBefore(btn, aboutButton);
                } else {
                    list.appendChild(btn);
                }
            } catch (e) {
                console.error('Error adding marketplace button:', e);
            }
        }
    }
    
    // Inject styles
    injectMarketplaceStyles();
    
    // Setup WebSocket event listeners
    setupEventListeners();
    
    // Observer to watch for settings list and add button
    const observer = new MutationObserver((mutations) => {
        for (const m of mutations) {
            for (const n of m.addedNodes) {
                if (!(n instanceof HTMLElement)) continue;

                const settingsList = n.matches('[data-tid="settings-list"]')
                    ? n
                    : n.querySelector('[data-tid="settings-list"]');
                if (settingsList) {
                    console.log('Found settings list, adding marketplace button');
                    addMarketplaceButton(settingsList);
                }
            }
        }
    });

    observer.observe(document.body, { childList: true, subtree: true });
})(); 