(function() {
    console.log('Site Pet plugin loading...');
    
    // Function to load the site-pet script from GitHub
    function loadSitePetScript() {
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'https://derdere.github.io/site-pet/site-pet.js';
            script.onload = () => {
                console.log('Site Pet script loaded successfully');
                resolve();
            };
            script.onerror = (error) => {
                console.error('Failed to load Site Pet script:', error);
                reject(error);
            };
            document.head.appendChild(script);
        });
    }
    
    // Function to create and setup the pet
    function setupPet() {
        try {
            // Make sure we don't add multiple pets
            if (document.querySelector('[data-site-pet]')) {
                return;
            }
            
            // Detect which sprite to use from available options
            const availableSprites = ['example']; // Default from the repository
            const selectedSprite = availableSprites[Math.floor(Math.random() * availableSprites.length)];
            
            // Create the pet
            const pet = window.createSitePet(selectedSprite);
            pet.setAttribute('data-site-pet', 'true');
            
            console.log(`Site Pet created with sprite: ${selectedSprite}`);
        } catch (error) {
            console.error('Error setting up Site Pet:', error);
        }
    }
    
    // Initialize the plugin
    async function initialize() {
        try {
            await loadSitePetScript();
            
            // Wait a short time for the page to fully load
            setTimeout(() => {
                setupPet();
            }, 2000);
            
            // Watch for navigation changes to recreate pet if needed
            const observer = new MutationObserver((mutations) => {
                if (!document.querySelector('[data-site-pet]')) {
                    setupPet();
                }
            });
            
            observer.observe(document.body, { childList: true, subtree: true });
            
        } catch (error) {
            console.error('Failed to initialize Site Pet plugin:', error);
        }
    }
    
    initialize();
})(); 