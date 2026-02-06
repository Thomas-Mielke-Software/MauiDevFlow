(function() {
    if (typeof chobitsu === 'undefined') {
        return 'chobitsu_not_loaded';
    }
    if (!window.__chobitsuOutgoingQueue) {
        window.__chobitsuOutgoingQueue = [];
    }
    return 'ready';
})();
