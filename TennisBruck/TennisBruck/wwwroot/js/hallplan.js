MobileDragDrop.polyfill({
    dragImageTranslateOverride: MobileDragDrop.scrollBehaviourDragImageTranslateOverride,
    holdToDrag: 350 // Erfordert langes Drücken (350ms) vor dem Start des Drag & Drop Events auf Touch-Geräten
});
window.addEventListener('touchmove', function () {
}, {passive: false});

document.addEventListener("DOMContentLoaded", () => {
    const draggablePlayers = document.querySelectorAll(".draggable-player, .draggable-substitute");
    let draggedPlayer = null;

    // Wir holen uns deine eigene ID aus dem HTML
    const myPlayerId = document.getElementById("main-content")?.dataset.meId;

    // ==========================================
    // AUTO-SCROLL LOGIK (EDGE SCROLLING)
    // ==========================================
    let isDragging = false;
    let scrollDirection = 0;
    const scrollSpeed = 15;
    const scrollThreshold = 100;

    function performAutoScroll() {
        if (!isDragging) return;
        if (scrollDirection !== 0) window.scrollBy(0, scrollDirection * scrollSpeed);
        requestAnimationFrame(performAutoScroll);
    }

    document.addEventListener("dragover", (e) => {
        if (!isDragging) return;
        const y = e.clientY;
        const windowHeight = window.innerHeight;

        if (y < scrollThreshold) scrollDirection = -1;
        else if (windowHeight - y < scrollThreshold) scrollDirection = 1;
        else scrollDirection = 0;
    });
    // ==========================================

    draggablePlayers.forEach(player => {
        player.addEventListener("dragstart", event => {
            draggedPlayer = player;
            event.dataTransfer.setData("text/plain", player.dataset.playerId);
            event.dataTransfer.effectAllowed = "move";
            player.style.opacity = "0.5";
            player.style.boxShadow = "0 0 0 2px var(--accent-color)";

            isDragging = true;
            requestAnimationFrame(performAutoScroll);
        });

        player.addEventListener("dragend", () => {
            player.style.opacity = "1";
            player.style.boxShadow = "none";
            draggedPlayer = null;

            isDragging = false;
            scrollDirection = 0;
        });

        player.addEventListener("dragover", event => {
            event.preventDefault();
            event.dataTransfer.dropEffect = "move";
            if (player !== draggedPlayer) {
                player.style.boxShadow = "0 0 0 2px var(--nav-bg)";
            }
        });

        player.addEventListener("dragleave", () => {
            player.style.boxShadow = "none";
        });

        player.addEventListener("drop", async event => {
            event.preventDefault();

            const target = event.currentTarget;
            target.style.boxShadow = "none";

            if (draggedPlayer && target !== draggedPlayer) {
                const isFromBench = draggedPlayer.classList.contains("draggable-substitute");
                const isTargetBench = target.classList.contains("draggable-substitute");

                if (isTargetBench) return;

                const player1Id = draggedPlayer.dataset.playerId;
                const court1Id = draggedPlayer.dataset.courtId ? draggedPlayer.dataset.courtId : null;
                const player2Id = target.dataset.playerId;
                const court2Id = target.dataset.courtId ? target.dataset.courtId : null;

                // VALIDATION: Verhindern, dass ein Spieler am selben Tag doppelt eingeteilt wird
                if (court1Id !== court2Id) {
                    if (court2Id) {
                        const court2Slots = document.querySelectorAll(`.player-slot[data-court-id="${court2Id}"]`);
                        for (const slot of court2Slots) {
                            if (slot !== target && slot.dataset.playerId === player1Id) {
                                alert("Aktion abgebrochen: Der Spieler ist an diesem Tag / in dieser Partie bereits eingeteilt!");
                                return;
                            }
                        }
                    }

                    if (court1Id && !isFromBench) {
                        const court1Slots = document.querySelectorAll(`.player-slot[data-court-id="${court1Id}"]`);
                        for (const slot of court1Slots) {
                            if (slot !== draggedPlayer && slot.dataset.playerId === player2Id) {
                                alert("Aktion abgebrochen: Der getauschte Spieler ist an dem anderen Tag bereits eingeteilt!");
                                return;
                            }
                        }
                    }
                }

                if (!confirm("Möchtest du den Spieler wirklich hier eintragen/tauschen?")) {
                    return;
                }

                const oldTargetHTML = target.innerHTML;
                const oldTargetPlayerId = target.dataset.playerId;

                // 1. VISUELLES UPDATE (Optimiert)
                if (isFromBench) {
                    target.innerHTML = draggedPlayer.innerHTML;
                    target.dataset.playerId = draggedPlayer.dataset.playerId;
                } else {
                    target.innerHTML = draggedPlayer.innerHTML;
                    target.dataset.playerId = draggedPlayer.dataset.playerId;
                    draggedPlayer.innerHTML = oldTargetHTML;
                    draggedPlayer.dataset.playerId = oldTargetPlayerId;
                }

                // 2. FARBE KORRIGIEREN (Auf die neue CSS-Klasse 'slot-me' angepasst!)
                if (myPlayerId) {
                    if (target.dataset.playerId === myPlayerId) target.classList.add("slot-me");
                    else target.classList.remove("slot-me");

                    if (!isFromBench) {
                        if (draggedPlayer.dataset.playerId === myPlayerId) draggedPlayer.classList.add("slot-me");
                        else draggedPlayer.classList.remove("slot-me");
                    }
                }

                // 3. BACKEND ANFRAGE (URL Repariert!)
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

                if (!token) {
                    alert("Sicherheitstoken fehlt. Bitte lade die Seite neu.");
                    return;
                }

                try {
                    // WICHTIG: Baut die URL sauber zusammen, damit Parameter (wie HallPlanId) nicht verloren gehen!
                    const targetUrl = new URL(window.location.href);
                    targetUrl.searchParams.set("handler", "SwapPlayers");

                    const response = await fetch(targetUrl.toString(), {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                            "RequestVerificationToken": token
                        },
                        body: JSON.stringify({
                            player1Id: parseInt(player1Id),
                            player2Id: parseInt(player2Id),
                            court1Id: court1Id ? parseInt(court1Id) : null,
                            court2Id: court2Id ? parseInt(court2Id) : null
                        }),
                    });

                    if (!response.ok) {
                        throw new Error(`Server antwortete mit Status ${response.status}`);
                    }

                } catch (error) {
                    console.error("Fetch-Fehler:", error);
                    alert("Es gab einen Fehler beim Speichern. Die Seite wird neu geladen.");
                    window.location.reload();
                }
            }
        });
    });
});