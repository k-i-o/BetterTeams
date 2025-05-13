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
})();
