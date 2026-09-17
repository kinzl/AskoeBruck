// TennisBruck Web Push Notifications Client Helper

function urlB64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
    const base64 = (base64String + padding)
        .replace(/\-/g, '+')
        .replace(/_/g, '/');

    const rawData = window.atob(base64);
    const outputArray = new Uint8Array(rawData.length);

    for (let i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}

window.TennisPush = {
    isSupported: function() {
        return ('serviceWorker' in navigator) && ('PushManager' in window) && ('Notification' in window);
    },

    getRegistration: async function() {
        if (!navigator.serviceWorker) return null;
        let reg = await navigator.serviceWorker.getRegistration();
        if (!reg) {
            reg = await navigator.serviceWorker.register('/sw.js');
        }
        return reg;
    },

    getSubscription: async function() {
        const reg = await this.getRegistration();
        if (!reg || !reg.pushManager) return null;
        return await reg.pushManager.getSubscription();
    },

    subscribe: async function() {
        if (!this.isSupported()) {
            throw new Error("Push-Benachrichtigungen werden von diesem Browser leider nicht unterstützt.");
        }

        const permission = await Notification.requestPermission();
        if (permission !== 'granted') {
            throw new Error("Benachrichtigungs-Berechtigung wurde nicht erteilt.");
        }

        const keyRes = await fetch('/api/push/public-key');
        if (!keyRes.ok) throw new Error("VAPID Public Key konnte nicht geladen werden.");
        const keyData = await keyRes.json();
        const applicationServerKey = urlB64ToUint8Array(keyData.publicKey);

        const reg = await this.getRegistration();
        if (!reg) throw new Error("Service Worker konnte nicht registriert werden.");

        let subscription = await reg.pushManager.getSubscription();
        if (!subscription) {
            subscription = await reg.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: applicationServerKey
            });
        }

        const subJson = subscription.toJSON();
        const saveRes = await fetch('/api/push/subscribe', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                endpoint: subJson.endpoint,
                p256dh: subJson.keys ? subJson.keys.p256dh : '',
                auth: subJson.keys ? subJson.keys.auth : ''
            })
        });

        if (!saveRes.ok) {
            throw new Error("Abonnement konnte am Server nicht gespeichert werden.");
        }

        return subscription;
    },

    unsubscribe: async function() {
        const subscription = await this.getSubscription();
        if (subscription) {
            const endpoint = subscription.endpoint;
            await subscription.unsubscribe();

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
            throw new Error("Test-Benachrichtigung konnte nicht ausgelöst werden.");
        }
        const data = await res.json();
        return data.success;
    }
};
