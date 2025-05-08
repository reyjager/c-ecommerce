// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Hot reload helper
(function() {
    // Check if we're in development mode
    if (document.querySelector('meta[name="environment"][content="Development"]')) {
        // Create a hidden iframe to check for changes
        const checkIframe = document.createElement('iframe');
        checkIframe.style.display = 'none';
        checkIframe.src = '/_framework/aspnetcore-browser-refresh.js';
        document.body.appendChild(checkIframe);
        
        // Listen for messages from the server
        window.addEventListener('message', function(e) {
            if (e.data === 'reload') {
                console.log('Hot reload triggered, refreshing page...');
                location.reload();
            }
        });
    }
})();