// TennisBruck Web Push Notifications Client Helper

function urlB64ToUint8Array(base64String) {
    if (!base64String) return new Uint8Array(0);
    const cleanBase64 = base64String.trim().replace(/-/g, '+').replace(/_/g, '/');
    const padding = '='.repeat((4 - (cleanBase64.length % 4)) % 4);
    const rawData = window.atob(cleanBase64 + padding);
    const outputArray = new Uint8Array(rawData.length);

    for (let i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}

window.TennisPush = {
    getSupportDetails: function() {
        const isSecure = window.isSecureContext === true;
        const hasServiceWorker = 'serviceWorker' in navigator;
        const hasPushManager = 'PushManager' in window;
        const hasNotification = 'Notification' in window;
        const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) || 
                      (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
        const isStandalone = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;

        if (!isSecure) {
            return {
                supported: false,
                reason: "Push-Benachrichtigungen erfordern eine sichere HTTPS-Verbindung (oder localhost). Wenn du über dein Smartphone im lokalen Netzwerk zugreifst, muss HTTPS verwendet werden."
            };
        }

        if (isIOS && !isStandalone) {
            return {
                supported: false,
                isIOSNonStandalone: true,
                reason: "Auf dem iPhone/iPad: Bitte öffne die Seite in Safari, tippe unten auf 'Teilen' (⎋) und wähle 'Zum Home-Bildschirm'. Öffne die Tennis-App danach über deinen Home-Bildschirm, um Mitteilungen zu aktivieren."
            };
        }

        if (!hasServiceWorker || !hasPushManager || !hasNotification) {
            return {
                supported: false,
                reason: "Push-Benachrichtigungen werden von diesem Browser leider nicht unterstützt."
            };
        }

        return { supported: true };
    },

    isSupported: function() {
        return this.getSupportDetails().supported;
    },

    getRegistration: async function() {
        if (!navigator.serviceWorker) return null;
        let reg = await navigator.serviceWorker.getRegistration();
        if (!reg) {
            reg = await navigator.serviceWorker.register('/sw.js');
        }
        await navigator.serviceWorker.ready;
        return reg;
    },

    getSubscription: async function() {
        try {
            const reg = await this.getRegistration();
            if (!reg || !reg.pushManager) return null;
            return await reg.pushManager.getSubscription();
        } catch (e) {
            console.warn("Could not get existing push subscription:", e);
            return null;
        }
    },

    subscribe: async function() {
        const support = this.getSupportDetails();
        if (!support.supported) {
            throw new Error(support.reason);
        }

        let permission = Notification.permission;
        if (permission === 'default') {
            permission = await Notification.requestPermission();
        }

        if (permission !== 'granted') {
            throw new Error("Benachrichtigungs-Berechtigung wurde im Browser nicht erteilt oder abgelehnt. Bitte prüfe deine Browser-Einstellungen.");
        }

        const keyRes = await fetch('/api/push/public-key');
        if (!keyRes.ok) {
            throw new Error("VAPID Public Key konnte vom Server nicht geladen werden.");
        }
        const keyData = await keyRes.json();
        if (!keyData || !keyData.publicKey) {
            throw new Error("Ungültiger VAPID Public Key empfangen.");
        }

        const applicationServerKey = urlB64ToUint8Array(keyData.publicKey);

        await this.getRegistration();
        const reg = await navigator.serviceWorker.ready;
        if (!reg || !reg.pushManager) {
            throw new Error("Service Worker oder PushManager ist nicht bereit.");
        }

        // Clean up any stale subscription created with old or different keys
        let existingSubscription = await reg.pushManager.getSubscription();
        if (existingSubscription) {
            try {
                await existingSubscription.unsubscribe();
            } catch (e) {
                console.warn("Altes Abonnement konnte nicht entfernt werden:", e);
            }
        }

        const subscription = await reg.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: applicationServerKey
        });

        const subJson = subscription.toJSON();
        let p256dh = (subJson.keys && subJson.keys.p256dh) ? subJson.keys.p256dh : '';
        let auth = (subJson.keys && subJson.keys.auth) ? subJson.keys.auth : '';

        // Fallback for browsers that don't serialize keys in toJSON()
        if (!p256dh && subscription.getKey) {
            const rawKey = subscription.getKey('p256dh');
            if (rawKey) {
                p256dh = btoa(String.fromCharCode.apply(null, new Uint8Array(rawKey)))
                    .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
            }
        }
        if (!auth && subscription.getKey) {
            const rawAuth = subscription.getKey('auth');
            if (rawAuth) {
                auth = btoa(String.fromCharCode.apply(null, new Uint8Array(rawAuth)))
                    .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
            }
        }

        const saveRes = await fetch('/api/push/subscribe', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                endpoint: subJson.endpoint,
                p256dh: p256dh,
                auth: auth
            })
        });

        if (!saveRes.ok) {
            if (saveRes.status === 401) {
                throw new Error("Du bist aktuell nicht angemeldet. Bitte melde dich erneut an.");
            }
            const errData = await saveRes.json().catch(() => null);
            throw new Error(errData?.message || `Abonnement konnte am Server nicht gespeichert werden (HTTP ${saveRes.status}).`);
        }

        return subscription;
    },

    unsubscribe: async function() {
        const subscription = await this.getSubscription();
        if (subscription) {
            const endpoint = subscription.endpoint;
            try {
                await subscription.unsubscribe();
            } catch (e) {
                console.warn("Fehler beim lokalen Unsubscribe:", e);
            }

            await fetch('/api/push/unsubscribe', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ endpoint: endpoint })
            });
        }
        return true;
    },

    sendTestNotification: async function() {
        const res = await fetch('/api/push/test', { method: 'POST' });
        if (!res.ok) {
            if (res.status === 401) {
                throw new Error("Du bist nicht angemeldet. Bitte melde dich erneut an.");
            }
            throw new Error("Test-Benachrichtigung konnte nicht ausgelöst werden.");
        }
        const data = await res.json();
        return data.success;
    }
};
