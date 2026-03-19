document.addEventListener("DOMContentLoaded", () => {
    const draggablePlayers = document.querySelectorAll(".draggable-player");
    let draggedPlayer = null;

    draggablePlayers.forEach(player => {
        // Enable dragging
        player.addEventListener("dragstart", event => {
            draggedPlayer = player;
            event.dataTransfer.setData("text/plain", player.dataset.playerId);
            event.dataTransfer.effectAllowed = "move";
            player.classList.add("opacity-50", "ring-2", "ring-emerald-400");
        });

        player.addEventListener("dragend", () => {
            player.classList.remove("opacity-50", "ring-2", "ring-emerald-400");
            draggedPlayer = null;
        });

        // Allow drop
        player.addEventListener("dragover", event => {
            event.preventDefault();
            event.dataTransfer.dropEffect = "move";
            if (player !== draggedPlayer) {
                player.classList.add("border", "border-2", "border-dashed", "border-emerald-400");
            }
        });

        player.addEventListener("dragleave", () => {
            player.classList.remove("border", "border-2", "border-dashed", "border-emerald-400");
        });

        player.addEventListener("drop", async event => {
            event.preventDefault();

            const target = event.currentTarget;
            player.classList.remove("border", "border-2", "border-dashed", "border-emerald-400");

            if (draggedPlayer && target !== draggedPlayer) {
                const player1Id = draggedPlayer.dataset.playerId;
                const court1Id = draggedPlayer.dataset.courtId;

                const player2Id = target.dataset.playerId;
                const court2Id = target.dataset.courtId;

                // Swap visuals
                const tempContent = draggedPlayer.innerHTML;
                draggedPlayer.innerHTML = target.innerHTML;
                target.innerHTML = tempContent;

                // Swap data attributes
                const tempPlayerId = draggedPlayer.dataset.playerId;
                const tempCourtId = draggedPlayer.dataset.courtId;

                draggedPlayer.dataset.playerId = target.dataset.playerId;
                draggedPlayer.dataset.courtId = target.dataset.courtId;

                target.dataset.playerId = tempPlayerId;
                target.dataset.courtId = tempCourtId;

                try {
                    const response = await fetch("/SwapPlayer/OnPostSwapPlayers", {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                        },
                        body: JSON.stringify({
                            player1Id,
                            player2Id,
                            court1Id,
                            court2Id,
                        }),
                    });

                    if (!response.ok) {
                        throw new Error("Failed to swap players");
                    }
                } catch (error) {
                    console.error(error);

                    // Revert on failure
                    target.innerHTML = draggedPlayer.innerHTML;
                    draggedPlayer.innerHTML = tempContent;

                    draggedPlayer.dataset.playerId = player1Id;
                    draggedPlayer.dataset.courtId = court1Id;

                    target.dataset.playerId = player2Id;
                    target.dataset.courtId = court2Id;
                }
            }
        });
    });
});
