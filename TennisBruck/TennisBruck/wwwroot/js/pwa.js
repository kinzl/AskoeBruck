if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('/sw.js')
            .then(reg => console.log('TennisBruck Service Worker läuft (Scope: ' + reg.scope + ')'))
            .catch(err => console.error('Fehler beim Starten des Service Workers:', err));
    });
}
