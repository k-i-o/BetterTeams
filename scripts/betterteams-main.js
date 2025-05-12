(function () {



    let policy = null;
    if (window.trustedTypes) {
        try {
            policy = trustedTypes.createPolicy(
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
    
    function injectHtmlWithPolicy(html, eventListenerType = 'click', eventListener = null) {
        const tempDiv = document.createElement('div');
        if (policy) {
            tempDiv.innerHTML = policy.createHTML(html);
        } else {
            tempDiv.innerHTML = html;
        }
        const element = tempDiv.firstElementChild;
        if (eventListener) {
            element.addEventListener(eventListenerType, eventListener);
        }
        return element;
    }

    async function searchGifs(searchTerm = 'funny', limit = 8) {
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
        await navigator.clipboard.write([gifUrl]);
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

    async function loadGifs(gifPopup, searchText) {
        const gridContainerExisting = document.querySelector('[data-tid="betterteams_gif_samples_grid"]');
        if (gridContainerExisting) {
            gridContainerExisting.remove();
        }

        const gridContainer = gifPopup.appendChild(injectHtmlWithPolicy(`
            <div data-tid="betterteams_gif_samples_grid" 
                 class="fui-Grid ___1zqzfu0 f13qh94s figf6al fi0ouf2 ffmv8ov fgfbwa2 fo7qwa0 fbuepbf"
                 style="display: grid; grid-template-columns: repeat(2, 130px); gap: 6px; padding: 15px; padding-top: 0; justify-content: center;">
            </div>
        `));

        const gifs = await searchGifs(searchText, 20);
        
        if (gifs.length > 0) {
            for (const gif of gifs) {
                gridContainer.appendChild(injectHtmlWithPolicy(`
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
                }));
            }
        } else {
            gridContainer.appendChild(injectHtmlWithPolicy(`
                <div class="fui-Text">No GIFs found</div>
            `));
        }
    }

    async function addGifSection(gifPopup) {
        if (!gifPopup.querySelector('[data-tid="betterteams_gif_section"]')) {
            try {
                gifPopup.appendChild(injectHtmlWithPolicy(`
                    <div data-tid="betterteams_gif_section" style="padding-top: 10px; padding-left: 15px">
                        <span id="betterteams_gif_section_title">BetterTeams GIF</span>
                    </div>
                `));

                gifPopup.appendChild(injectHtmlWithPolicy(`
                    <div data-tid="betterteams_gif_search_input" class="fui-Primitive f122n59 f1869bpl fxugw4r f1063pyq f4akndk f1voxoxi frgqz4e">
                        <span class="fui-Input r1oeeo9n ___opxuz00 f1sgzk6v f16xq7d1 ftmjh5b f17blpuu f1tpwn32 fsrcdbj fghlq4f f1gn591s fb073pr fjscplz fly5x3f">
                            <input type="text" 
                                   aria-label="Cerca emoji, GIF o adesivi" 
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
                    console.log('searchInputElement', e.target.value);
                    await loadGifs(gifPopup, e.target.value);
                }));

                await loadGifs(gifPopup);
            } catch (e) {
                console.error('Error adding GIF section:', e);
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

                const gifPopup = n.matches('[data-tid="sendMessageCommands-popup-UnifiedFunPicker-content"] [data-tid="unified-picker-giphys-content"]')
                    ? n
                    : n.querySelector('[data-tid="sendMessageCommands-popup-UnifiedFunPicker-content"] [data-tid="unified-picker-giphys-content"]');
                if (gifPopup) {
                    console.log('Found GIF popup, adding section');
                    addGifSection(gifPopup);
                }
            }
        }
    });

    observer.observe(document.body, { childList: true, subtree: true });
})();
