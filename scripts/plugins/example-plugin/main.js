(function() {
    // Example Plugin for BetterTeams
    // This plugin adds a simple feature to Teams
    
    // Log when the plugin is loaded
    console.log('Example Plugin loaded successfully!');
    
    // Add a custom CSS style
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
    
    // Add a badge to team names in the sidebar
    function addBadgesToTeams() {
        const teamsList = document.querySelectorAll('[data-tid="team-list"] [data-tid="channel-list-team"]');
        teamsList.forEach(team => {
            if (!team.querySelector('.example-plugin-badge')) {
                const teamName = team.querySelector('[data-tid="title-text"]');
                if (teamName) {
                    const badge = document.createElement('span');
                    badge.className = 'example-plugin-badge';
                    badge.textContent = 'Enhanced';
                    teamName.appendChild(badge);
                }
            }
        });
    }
    
    // Watch for changes in the DOM to add badges to newly added teams
    function observeTeamsList() {
        const observer = new MutationObserver((mutations) => {
            for (const mutation of mutations) {
                if (mutation.addedNodes.length > 0) {
                    addBadgesToTeams();
                }
            }
        });
        
        // Start observing the document body for DOM changes
        observer.observe(document.body, { childList: true, subtree: true });
        
        // Initial call to add badges to existing teams
        addBadgesToTeams();
    }
    
    // Call init functions
    injectStyles();
    observeTeamsList();
})(); 