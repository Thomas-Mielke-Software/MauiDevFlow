(function() {
    if (!window.__chobitsuConnectionCallbacks) {
        window.__chobitsuConnectionCallbacks = {};
    }
    window.__chobitsuConnectionCallbacks['%CONNECTION_ID%'] = function(message) {
        // This will be called by native code to send messages back
        window.external.sendMessage('%CONNECTION_ID%', message);
    };
})();
