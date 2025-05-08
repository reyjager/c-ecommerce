// Hot reload implementation using WebSockets
(function () {
    // Only run in development environment
    if (!window.location.hostname.includes('localhost') && !window.location.hostname.includes('127.0.0.1')) {
        return;
    }

    console.log('Hot reload script initialized');

    // Create a function to handle the reload
    function setupHotReload() {
        // Create WebSocket connection
        const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
        const host = window.location.host;
        const socket = new WebSocket(`${protocol}//${host}/hot-reload`);

        // Connection opened
        socket.addEventListener('open', (event) => {
            console.log('Hot reload connection established');
        });

        // Listen for messages
        socket.addEventListener('message', (event) => {
            if (event.data === 'reload') {
                console.log('Hot reload triggered, refreshing page...');
                window.location.reload();
            }
        });

        // Connection closed, try to reconnect
        socket.addEventListener('close', (event) => {
            console.log('Hot reload connection closed, reconnecting in 3 seconds...');
            setTimeout(setupHotReload, 3000);
        });

        // Connection error, try to reconnect
        socket.addEventListener('error', (event) => {
            console.log('Hot reload connection error, reconnecting in 3 seconds...');
            socket.close();
        });
    }

    // Alternative approach using Server-Sent Events
    function setupSSE() {
        try {
            const evtSource = new EventSource('/hot-reload-sse');

            evtSource.onopen = function () {
                console.log('SSE hot reload connection established');
            };

            evtSource.onmessage = function (event) {
                if (event.data === 'reload') {
                    console.log('SSE hot reload triggered, refreshing page...');
                    window.location.reload();
                }
            };

            evtSource.onerror = function () {
                console.log('SSE hot reload connection error, reconnecting...');
                evtSource.close();
                setTimeout(setupSSE, 3000);
            };
        } catch (e) {
            console.error('SSE hot reload setup failed:', e);
            // Fallback to polling
            setupPolling();
        }
    }

    // Fallback polling approach
    function setupPolling() {
        console.log('Using polling for hot reload');
        let lastModified = new Date().toISOString();

        setInterval(() => {
            fetch('/hot-reload-poll?lastModified=' + encodeURIComponent(lastModified), {
                method: 'GET',
                headers: {
                    'Cache-Control': 'no-cache'
                }
            })
                .then(response => response.text())
                .then(data => {
                    if (data === 'reload') {
                        console.log('Poll detected change, refreshing page...');
                        window.location.reload();
                    } else if (data !== 'no-change') {
                        lastModified = data;
                    }
                })
                .catch(error => {
                    console.error('Hot reload polling error:', error);
                });
        }, 1000); // Check every second
    }

    // Try WebSocket first, then SSE, then polling
    try {
        if ('WebSocket' in window) {
            setupHotReload();
        } else if ('EventSource' in window) {
            setupSSE();
        } else {
            setupPolling();
        }
    } catch (e) {
        console.error('Hot reload initialization failed:', e);
        setupPolling(); // Fallback to polling
    }

    // Also add a manual refresh button
    const refreshButton = document.createElement('button');
    refreshButton.textContent = '🔄';
    refreshButton.title = 'Refresh page';
    refreshButton.style.position = 'fixed';
    refreshButton.style.bottom = '20px';
    refreshButton.style.right = '20px';
    refreshButton.style.zIndex = '9999';
    refreshButton.style.borderRadius = '50%';
    refreshButton.style.width = '40px';
    refreshButton.style.height = '40px';
    refreshButton.style.fontSize = '20px';
    refreshButton.style.backgroundColor = '#007bff';
    refreshButton.style.color = 'white';
    refreshButton.style.border = 'none';
    refreshButton.style.cursor = 'pointer';
    refreshButton.style.boxShadow = '0 2px 5px rgba(0,0,0,0.2)';
    refreshButton.style.display = 'flex';
    refreshButton.style.alignItems = 'center';
    refreshButton.style.justifyContent = 'center';

    refreshButton.addEventListener('click', () => {
        window.location.reload();
    });

    document.body.appendChild(refreshButton);
})();