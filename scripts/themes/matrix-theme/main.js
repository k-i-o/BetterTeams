const canvas = document.createElement('canvas');
document.body.appendChild(canvas);

Object.assign(canvas.style, {
    position: 'absolute',
    top: '0',
    left: '0',
    width: '100%',
    height: '100%',
    display: 'block',
    zIndex: '-1'
});

const ctx = canvas.getContext('2d');
function resizeCanvas() {
    canvas.width = window.innerWidth;
    canvas.height = window.innerHeight;
}
resizeCanvas();
window.addEventListener('resize', resizeCanvas);

const characters = '0123456789ABCDEF'; 
const fontSize = 16;                  
const columns = Math.floor(window.innerWidth / fontSize); 
const drops = Array(columns).fill(0);  

function drawMatrix() {
    ctx.fillStyle = 'rgba(0, 0, 0, 0.05)';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    ctx.fillStyle = '#00FF00';
    ctx.font = `${fontSize}px monospace`;

    drops.forEach((y, i) => {
        const text = characters[Math.floor(Math.random() * characters.length)];
        const x = i * fontSize;

        ctx.fillText(text, x, y * fontSize);

        if (y * fontSize > canvas.height && Math.random() > 0.975) {
            drops[i] = 0;
        }

        drops[i]++;
    });

    requestAnimationFrame(drawMatrix);
}

drawMatrix();
