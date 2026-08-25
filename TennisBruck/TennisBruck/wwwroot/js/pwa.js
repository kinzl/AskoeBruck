if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('/js/sw.js')
            .then(reg => console.log('TennisBruck App-Motor (Service Worker) läuft!'))
            .catch(err => console.error('Fehler beim Starten des Service Workers:', err));
    });
}
