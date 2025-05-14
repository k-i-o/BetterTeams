(function () {



    async function searchGifs(searchTerm = 'funny', limit = 20) {
        try {
            const response = await fetch(`https://tenor.googleapis.com/v2/search?q=${encodeURIComponent(searchTerm)}&key=${TENOR_API_KEY}&client_key=${TENOR_CLIENT_KEY}&limit=${limit}`);
            const data = await response.json();
            return data.results || [];
        } catch (e) {
            console.error('Error fetching GIFs:', e);
            return [];
        }
    }

    async function copyGifToClipboard(gifUrl) {
        try {
            if (window.betterTeams && window.betterTeams.sendWebSocketMessage) {
                window.betterTeams.sendWebSocketMessage({
                    action: "copyToClipboard",
                    type: "gif",
                    url: gifUrl
                });
                
                showNotification("GIF copied to clipboard. You can now paste it in the chat.");
                return true;
            }
            
            try {
                const response = await fetch(gifUrl);
                const blob = await response.blob();
                const item = new ClipboardItem({ 'image/gif': blob });
                await navigator.clipboard.write([item]);
                console.log('GIF copied to clipboard using ClipboardItem');
                return true;
            } catch (clipboardError) {
                console.warn('Standard clipboard API failed:', clipboardError);
            }
            
            showNotification("Unable to copy GIF to clipboard. Please try pasting manually.", 10000);
            return false;
        } catch (e) {
            console.error('Error copying GIF to clipboard:', e);
            return false;
        }
    }
    
    function showNotification(message, duration = 3000) {
        const existingNotification = document.querySelector('#betterteams-notification');
        if (existingNotification) {
            document.body.removeChild(existingNotification);
        }
        
        const notificationDiv = document.createElement('div');
        notificationDiv.id = 'betterteams-notification';
        notificationDiv.style.cssText = `
            position: fixed;
            bottom: 20px;
            right: 20px;
            background-color: #0078d4;
            color: white;
            padding: 15px;
            border-radius: 5px;
            z-index: 10000;
            box-shadow: 0 2px 10px rgba(0,0,0,0.2);
            font-family: 'Segoe UI', sans-serif;
            max-width: 300px;
        `;
        
        notificationDiv.textContent = message;
        document.body.appendChild(notificationDiv);
        
        setTimeout(() => {
            if (document.body.contains(notificationDiv)) {
                document.body.removeChild(notificationDiv);
            }
        }, duration);
    }

    async function loadGifs(gifPopup, searchText) {
        const gridContainerExisting = document.querySelector('[data-tid="betterteams_gif_samples_grid"]');
        if (gridContainerExisting) {
            gridContainerExisting.remove();
        }

        const gridContainer = gifPopup.appendChild(window.injectHtmlWithPolicy(`
            <div data-tid="betterteams_gif_samples_grid" 
                 class="fui-Grid ___1zqzfu0 f13qh94s figf6al fi0ouf2 ffmv8ov fgfbwa2 fo7qwa0 fbuepbf"
                 style="display: grid; grid-template-columns: repeat(2, 130px); gap: 6px; padding: 15px; padding-top: 0; justify-content: center;">
            </div>
        `));

        let gifs = [];
        if(!searchText || searchText.length == 0) {
            gifs = await searchGifs();
        } else {
            gifs = await searchGifs(searchText);
        }

        if (gifs.length > 0) {
            for (const gif of gifs) {
                gridContainer.appendChild(window.injectHtmlWithPolicy(`
                    <div class="fui-GridItem" style="width: 130px; height: 75px; cursor: pointer;">
                        <img src="${gif.media_formats.tinygif.url}"
                            alt="${gif.tags.map(tag => `#${tag}`).join(', ') || 'GIF'}"
                            title="${gif.tags.map(tag => `#${tag}`).join(', ') || 'GIF'}"
                            loading="lazy"
                            style="width: 100%; height: 100%; object-fit: cover;"
                            data-orig-src="${gif.media_formats.gif.url}"
                            data-inline-image="true"
                            data-image-type="standard"
                            data-image-mode="single">
                    </div>
                `, 'click', () => {
                    copyGifToClipboard(gif.media_formats.gif.url);
                    
                    // Close the popup after click
                    const closeButton = document.querySelector('[data-tid="sendMessageCommands-popup-close"]');
                    if (closeButton) {
                        closeButton.click();
                    }
                    
                    // Focus the message input
                    setTimeout(() => {
                        const messageInput = document.querySelector('[data-tid="newMessageInput"]');
                        if (messageInput) {
                            messageInput.focus();
                        }
                    }, 300);
                }));
            }
        } else {
            gridContainer.appendChild(window.injectHtmlWithPolicy(`
                <div class="fui-Text">No GIFs found</div>
            `));
        }
    }

    async function addGifSection(gifPopup) {
        if (gifPopup.querySelector('[data-tid="betterteams_gif_section"]')) {
            return;
        }

        try {
            gifPopup.appendChild(window.injectHtmlWithPolicy(`
                <div data-tid="betterteams_gif_section" style="padding-top: 10px; padding-left: 15px">
                    <span id="betterteams_gif_section_title">BetterTeams GIF</span>
                </div>
            `));

            gifPopup.appendChild(window.injectHtmlWithPolicy(`
                <div data-tid="betterteams_gif_search_input" class="fui-Primitive f122n59 f1869bpl fxugw4r f1063pyq f4akndk f1voxoxi frgqz4e">
                    <span class="fui-Input r1oeeo9n ___opxuz00 f1sgzk6v f16xq7d1 ftmjh5b f17blpuu f1tpwn32 fsrcdbj fghlq4f f1gn591s fb073pr fjscplz fly5x3f">
                        <input type="text"
                                placeholder="Search for custom GIFs" 
                                data-tid="unified-picker-search-bar" 
                                data-tabster="{&quot;restorer&quot;:{&quot;type&quot;:1}}" 
                                class="fui-Input__input r12stul0 ___1q6bera ffczdla" 
                                value="">
                        <span class="fui-Input__contentAfter r1572tok">
                            <svg class="fui-Icon-filled ___1vjqft9 fjseox fez10in fg4l7m0" fill="currentColor" aria-hidden="true" width="1em" height="1em" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg"><path d="M8.5 3a5.5 5.5 0 0 1 4.38 8.82l4.15 4.15a.75.75 0 0 1-.98 1.13l-.08-.07-4.15-4.15A5.5 5.5 0 1 1 8.5 3Zm0 1.5a4 4 0 1 0 0 8 4 4 0 0 0 0-8Z" fill="currentColor"></path></svg>
                            <svg class="fui-Icon-regular ___12fm75w f1w7gpdv fez10in fg4l7m0" fill="currentColor" aria-hidden="true" width="1em" height="1em" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg"><path d="M8.5 3a5.5 5.5 0 0 1 4.23 9.02l4.12 4.13a.5.5 0 0 1-.63.76l-.07-.06-4.13-4.12A5.5 5.5 0 1 1 8.5 3Zm0 1a4.5 4.5 0 1 0 0 9 4.5 4.5 0 0 0 0-9Z" fill="currentColor"></path></svg>
                        </span>
                    </span>
                </div>
            `, 'input', async (e) => {
                await loadGifs(gifPopup, e.target.value);
            }));

            await loadGifs(gifPopup);
        } catch (e) {
            console.error('Error adding GIF section:', JSON.stringify(e));
        }
    }

    // Initialize WebSocket connection if not already established
    function ensureWebSocketConnection() {
        // Check if BetterTeams WebSocket functionality is already available
        if (!window.betterTeams) {
            window.betterTeams = {};
        }
        
        if (!window.betterTeams.sendWebSocketMessage) {
            // Create a custom event to request WebSocket access from the C# application
            const event = new CustomEvent('betterteams:request-websocket', {
                detail: {
                    plugin: 'tenor-gifs',
                    features: ['clipboard']
                }
            });
            
            document.dispatchEvent(event);
            
            console.log('Requested WebSocket access for tenor-gifs plugin');
        }
    }

    const observer = new MutationObserver((mutations) => {
        for (const m of mutations) {
            for (const n of m.addedNodes) {
                if (!(n instanceof HTMLElement)) continue;

                const gifPopup = n.matches('[data-tid="sendMessageCommands-popup-UnifiedFunPicker-content"] [data-tid="unified-picker-giphys-content"]')
                    ? n
                    : n.querySelector('[data-tid="sendMessageCommands-popup-UnifiedFunPicker-content"] [data-tid="unified-picker-giphys-content"]');
                if (gifPopup) {
                    console.log('Found GIF popup, adding section');
                    addGifSection(gifPopup).catch(e => {
                        console.error('Error adding GIF section:', e);
                    });
                }
            }
        }
    });

    // Initialize the plugin
    ensureWebSocketConnection();
    observer.observe(document.body, { childList: true, subtree: true });
    
    // Listen for WebSocket ready event from C# application
    document.addEventListener('betterteams:websocket-ready', (event) => {
        console.log('WebSocket connection is ready for tenor-gifs plugin');
    });
})(); 