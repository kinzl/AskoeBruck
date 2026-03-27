MobileDragDrop.polyfill({
    dragImageTranslateOverride: MobileDragDrop.scrollBehaviourDragImageTranslateOverride,
});
window.addEventListener('touchmove', function () {
}, {passive: false});
document.addEventListener("DOMContentLoaded", () => {
    const draggablePlayers = document.querySelectorAll(".draggable-player, .draggable-substitute");
    let draggedPlayer = null;

    // Wir holen uns deine eigene ID aus dem HTML
    const myPlayerId = document.getElementById("main-content")?.dataset.meId;

    // ==========================================
    // NEU: AUTO-SCROLL LOGIK (EDGE SCROLLING)
    // ==========================================
    let isDragging = false;
    let scrollDirection = 0; // -1 = hoch, 1 = runter, 0 = stopp
    const scrollSpeed = 15;  // Wie schnell gescrollt wird (Pixel pro Frame)
    const scrollThreshold = 100; // Ab wie vielen Pixeln vor dem Rand der Scroll startet

    function performAutoScroll() {
        if (!isDragging) return;

        if (scrollDirection !== 0) {
            window.scrollBy(0, scrollDirection * scrollSpeed);
        }

        // requestAnimationFrame sorgt für flüssiges Scrollen passend zur Framerate deines Monitors
        requestAnimationFrame(performAutoScroll);
    }

    // Wir überwachen das gesamte Fenster, um zu wissen, wo die Maus ist
    document.addEventListener("dragover", (e) => {
        if (!isDragging) return;

        const y = e.clientY;
        const windowHeight = window.innerHeight;

        if (y < scrollThreshold) {
            scrollDirection = -1; // Maus ist oben -> Nach oben scrollen
        } else if (windowHeight - y < scrollThreshold) {
            scrollDirection = 1;  // Maus ist unten -> Nach unten scrollen
        } else {
            scrollDirection = 0;  // Maus ist in der Mitte -> Stehen bleiben
        }
    });
    // ==========================================

    draggablePlayers.forEach(player => {
        player.addEventListener("dragstart", event => {
            draggedPlayer = player;
            event.dataTransfer.setData("text/plain", player.dataset.playerId);
            event.dataTransfer.effectAllowed = "move";
            player.style.opacity = "0.5";
            player.style.boxShadow = "0 0 0 2px var(--accent-color)";

            // Start Auto-Scroll
            isDragging = true;
            requestAnimationFrame(performAutoScroll);
        });

        player.addEventListener("dragend", () => {
            player.style.opacity = "1";
            player.style.boxShadow = "none";
            draggedPlayer = null;

            // Stopp Auto-Scroll
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

                const oldTargetHTML = target.innerHTML;
                const oldTargetPlayerId = target.dataset.playerId;
                const oldDraggedHTML = draggedPlayer.innerHTML;
                const oldDraggedPlayerId = draggedPlayer.dataset.playerId;

                // 1. VISUELLES UPDATE
                if (isFromBench) {
                    target.innerHTML = draggedPlayer.innerHTML;
                    target.dataset.playerId = draggedPlayer.dataset.playerId;
                } else {
                    target.innerHTML = draggedPlayer.innerHTML;
                    target.dataset.playerId = draggedPlayer.dataset.playerId;
                    draggedPlayer.innerHTML = oldTargetHTML;
                    draggedPlayer.dataset.playerId = oldTargetPlayerId;
                }

                // 2. FARBE KORRIGIEREN
                if (myPlayerId) {
                    if (target.dataset.playerId === myPlayerId) target.classList.add("player-slot-me");
                    else target.classList.remove("player-slot-me");

                    if (!isFromBench) {
                        if (draggedPlayer.dataset.playerId === myPlayerId) draggedPlayer.classList.add("player-slot-me");
                        else draggedPlayer.classList.remove("player-slot-me");
                    }
                }

                // 3. BACKEND ANFRAGE
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

                try {
                    const response = await fetch("?handler=SwapPlayers", {
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

                    if (!response.ok) throw new Error("Fehler beim Speichern in der DB");

                } catch (error) {
                    console.error(error);
                    alert("Es gab einen Fehler beim Speichern. Die Seite wird neu geladen.");
                    window.location.reload();
                }
            }
        });
    });
});