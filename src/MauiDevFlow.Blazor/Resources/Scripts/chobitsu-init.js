(function() {
    // Prevent double-injection
    if (window.__chobitsuDebugEnabled) {
        console.log('[ChobitsuDebug] Already initialized');
        return;
    }
    window.__chobitsuDebugEnabled = true;
    
    console.log('[ChobitsuDebug] Initializing debug server on port %PORT%...');
    
    // Initialize the outgoing message queue immediately
    window.__chobitsuOutgoingQueue = [];
    
    if (typeof chobitsu === 'undefined') {
        console.error('[ChobitsuDebug] chobitsu not found after injection attempt');
        return;
    }
    
    console.log('[ChobitsuDebug] Chobitsu found, setting up message handler...');
    
    // Set up Chobitsu message handler - queue messages for native polling
    chobitsu.setOnMessage(function(message) {
        window.__chobitsuOutgoingQueue.push(message);
    });
    
    // Signal that we're ready for connections
    window.__chobitsuReady = true;
    window.__chobitsuPort = %PORT%;
    
    console.log('[ChobitsuDebug] Ready! Messages will be queued for native polling.');
})();
