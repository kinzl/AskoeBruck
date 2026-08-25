// Ein winziger Service Worker, der dem Browser sagt: "Ich bin eine moderne PWA!"
self.addEventListener('install', (event) => {
    // Installiert den Worker sofort, ohne auf einen Neustart zu warten
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    // Übernimmt sofort die Kontrolle über alle geöffneten Tabs der App
    event.waitUntil(clients.claim());
});

self.addEventListener('fetch', (event) => {
    // Hier könnten wir später Offline-Seiten bauen. 
    // Aktuell lassen wir einfach alle Internet-Anfragen ganz normal durch:
    event.respondWith(fetch(event.request));
});