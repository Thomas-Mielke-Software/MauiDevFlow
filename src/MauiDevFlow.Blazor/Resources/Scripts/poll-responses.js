(function() {
    var queue = window.__chobitsuOutgoingQueue || [];
    window.__chobitsuOutgoingQueue = [];
    return JSON.stringify(queue);
})();
