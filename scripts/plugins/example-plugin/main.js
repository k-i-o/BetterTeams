(function () {

    console.log('Example Plugin loaded successfully!');
    
    function injectStyles() {
        const style = document.createElement('style');
        style.textContent = `
            .example-plugin-badge {
                background-color: #ff5722;
                color: white;
                padding: 2px 5px;
                border-radius: 3px;
                font-size: 10px;
                margin-left: 5px;
            }
        `;
        document.head.appendChild(style);
    }
    
    function addBadgesToTeams() {
        const teamsList = document.querySelectorAll('.virtual-tree-list-scroll-container');
        teamsList.forEach(team => {
            if (!team.querySelector('.example-plugin-badge')) {
                const teamName = team.querySelector('[data-tid="chat-list-item-title"]');
                if (teamName) {
                    const badge = document.createElement('span');
                    badge.className = 'example-plugin-badge';
                    badge.textContent = 'Enhanced';
                    teamName.appendChild(badge);
                }
            }
        });
    }
    
    function observeTeamsList() {
        const observer = new MutationObserver((mutations) => {
            for (const mutation of mutations) {
                if (mutation.addedNodes.length > 0) {
                    addBadgesToTeams();
                }
            }
        });
        
        observer.observe(document.body, { childList: true, subtree: true });
        
        addBadgesToTeams();
    }
    
    injectStyles();
    observeTeamsList();
})(); 