(function() {
    console.log('Site Pet plugin loading...');
    
    // Define the site-pet script directly instead of loading from external source
    function setupSitePet() {
        try {
            // Make sure we don't add multiple pets
            if (document.querySelector('[data-site-pet]')) {
                return;
            }
            
            // Create the pet element
            const pet = document.createElement('div');
            pet.setAttribute('data-site-pet', 'true');
            pet.style.position = 'fixed';
            pet.style.bottom = '10px';
            pet.style.right = '10px';
            pet.style.width = '50px';
            pet.style.height = '50px';
            pet.style.backgroundColor = '#ffcc00';
            pet.style.borderRadius = '50%';
            pet.style.zIndex = '9999';
            pet.style.cursor = 'pointer';
            pet.style.boxShadow = '0 2px 5px rgba(0,0,0,0.2)';
            pet.style.transition = 'all 0.3s ease';
            
            // Add eyes to make it look like a pet
            const leftEye = document.createElement('div');
            leftEye.style.position = 'absolute';
            leftEye.style.top = '15px';
            leftEye.style.left = '12px';
            leftEye.style.width = '8px';
            leftEye.style.height = '8px';
            leftEye.style.backgroundColor = '#000';
            leftEye.style.borderRadius = '50%';
            pet.appendChild(leftEye);
            
            const rightEye = document.createElement('div');
            rightEye.style.position = 'absolute';
            rightEye.style.top = '15px';
            rightEye.style.right = '12px';
            rightEye.style.width = '8px';
            rightEye.style.height = '8px';
            rightEye.style.backgroundColor = '#000';
            rightEye.style.borderRadius = '50%';
            pet.appendChild(rightEye);
            
            // Add mouth
            const mouth = document.createElement('div');
            mouth.style.position = 'absolute';
            mouth.style.bottom = '15px';
            mouth.style.left = '20px';
            mouth.style.width = '10px';
            mouth.style.height = '5px';
            mouth.style.borderBottomLeftRadius = '10px';
            mouth.style.borderBottomRightRadius = '10px';
            mouth.style.backgroundColor = '#000';
            pet.appendChild(mouth);

            // Add speech bubble for interactions
            const speechBubble = document.createElement('div');
            speechBubble.style.position = 'absolute';
            speechBubble.style.bottom = '60px';
            speechBubble.style.left = '50%';
            speechBubble.style.transform = 'translateX(-50%)';
            speechBubble.style.backgroundColor = 'white';
            speechBubble.style.padding = '8px 12px';
            speechBubble.style.borderRadius = '15px';
            speechBubble.style.boxShadow = '0 2px 5px rgba(0,0,0,0.2)';
            speechBubble.style.fontSize = '12px';
            speechBubble.style.fontWeight = 'bold';
            speechBubble.style.textAlign = 'center';
            speechBubble.style.minWidth = '80px';
            speechBubble.style.display = 'none';
            speechBubble.style.zIndex = '10000';
            pet.appendChild(speechBubble);

            // Function to show speech bubble with message
            function speak(message, duration = 3000) {
                speechBubble.textContent = message;
                speechBubble.style.display = 'block';
                
                setTimeout(() => {
                    speechBubble.style.display = 'none';
                }, duration);
            }
            
            // Add interactivity
            let isDragging = false;
            let offsetX, offsetY;
            let autoMoving = false;
            
            // Messages for interactions
            const messages = [
                "Ciao!",
                "Come stai?",
                "Mi piace Teams!",
                "Sono carino, vero?",
                "Clicca ancora!",
                "Che bello essere qui!",
                "Ti aiuto io!",
                "Buona giornata!",
                "Sono il tuo assistente!"
            ];
            
            // Click interaction
            pet.addEventListener('click', (e) => {
                if (!isDragging) {
                    // Random color change on click
                    const colors = ['#ffcc00', '#ff6b6b', '#48dbfb', '#1dd1a1', '#f368e0', '#ff9f43'];
                    const randomColor = colors[Math.floor(Math.random() * colors.length)];
                    pet.style.backgroundColor = randomColor;
                    
                    // Show random message
                    const randomMessage = messages[Math.floor(Math.random() * messages.length)];
                    speak(randomMessage);
                    
                    // Jump animation
                    pet.style.transform = 'translateY(-20px)';
                    setTimeout(() => {
                        pet.style.transform = 'translateY(0)';
                    }, 300);
                }
            });
            
            // Drag and drop
            pet.addEventListener('mousedown', (e) => {
                isDragging = true;
                offsetX = e.clientX - pet.getBoundingClientRect().left;
                offsetY = e.clientY - pet.getBoundingClientRect().top;
                pet.style.transform = 'scale(0.95)';
                autoMoving = false; // Stop auto movement when dragging
            });
            
            document.addEventListener('mousemove', (e) => {
                if (isDragging) {
                    pet.style.right = 'auto';
                    pet.style.bottom = 'auto';
                    pet.style.left = (e.clientX - offsetX) + 'px';
                    pet.style.top = (e.clientY - offsetY) + 'px';
                }
            });
            
            document.addEventListener('mouseup', () => {
                if (isDragging) {
                    isDragging = false;
                    pet.style.transform = 'scale(1)';
                }
            });
            
            // Random movement function
            function moveRandomly() {
                if (isDragging || autoMoving) return;
                
                autoMoving = true;
                
                // Get viewport dimensions
                const viewportWidth = window.innerWidth;
                const viewportHeight = window.innerHeight;
                
                // Calculate random position within viewport boundaries (with padding)
                const padding = 60;
                const randomX = Math.max(padding, Math.min(viewportWidth - padding, Math.random() * viewportWidth));
                const randomY = Math.max(padding, Math.min(viewportHeight - padding, Math.random() * viewportHeight));
                
                // Get current position
                const rect = pet.getBoundingClientRect();
                const currentX = rect.left;
                const currentY = rect.top;
                
                // Calculate distance
                const distance = Math.sqrt(Math.pow(randomX - currentX, 2) + Math.pow(randomY - currentY, 2));
                
                // Set transition duration based on distance (faster for short distances)
                const duration = Math.min(3, Math.max(1, distance / 300));
                pet.style.transition = `all ${duration}s ease`;
                
                // Move pet to new position
                pet.style.right = 'auto';
                pet.style.bottom = 'auto';
                pet.style.left = randomX + 'px';
                pet.style.top = randomY + 'px';
                
                // Reset transition after movement
                setTimeout(() => {
                    pet.style.transition = 'all 0.3s ease';
                    autoMoving = false;
                }, duration * 1000);
            }
            
            // Animate the pet occasionally
            setInterval(() => {
                if (!isDragging && !autoMoving) {
                    // 30% chance to move randomly
                    if (Math.random() < 0.3) {
                        moveRandomly();
                    } else {
                        // Otherwise just do a small animation
                        pet.style.transform = 'scale(1.1)';
                        setTimeout(() => {
                            pet.style.transform = 'scale(1)';
                        }, 300);
                    }
                }
            }, 5000);
            
            // Initial greeting
            setTimeout(() => {
                speak("Ciao! Sono qui per aiutarti!");
            }, 1000);
            
            // Add to document
            document.body.appendChild(pet);
            
            console.log('Site Pet created successfully');
        } catch (error) {
            console.error('Error setting up Site Pet:', error);
        }
    }
    
    // Initialize the plugin
    function initialize() {
        try {
            // Wait a short time for the page to fully load
            setTimeout(() => {
                setupSitePet();
            }, 2000);
            
            // Watch for navigation changes to recreate pet if needed
            const observer = new MutationObserver((mutations) => {
                if (!document.querySelector('[data-site-pet]')) {
                    setupSitePet();
                }
            });
            
            observer.observe(document.body, { childList: true, subtree: true });
            
        } catch (error) {
            console.error('Failed to initialize Site Pet plugin:', error);
        }
    }
    
    initialize();
})(); 