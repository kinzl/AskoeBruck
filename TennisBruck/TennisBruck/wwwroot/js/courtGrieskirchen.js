document.addEventListener("DOMContentLoaded", () => {
    const draggablePlayers = document.querySelectorAll(".draggable-player");
    const table = document.querySelector(".table-wrapper table");

    let draggedPlayer = null;

    draggablePlayers.forEach(player => {
        // Allow dragging
        player.addEventListener("dragstart", event => {
            draggedPlayer = player;
            event.dataTransfer.setData("text/plain", player.dataset.playerId);
        });

        player.addEventListener("dragend", () => {
            draggedPlayer = null;
        });
    });

    table.addEventListener("dragover", event => {
        // Allow dropping
        event.preventDefault();
    });

    table.addEventListener("drop", async event => {
        event.preventDefault();

        const target = event.target;
        if (target.classList.contains("draggable-player") && draggedPlayer) {
            const player1Id = draggedPlayer.dataset.playerId;
            const court1Id = draggedPlayer.dataset.courtId;

            const player2Id = target.dataset.playerId;
            const court2Id = target.dataset.courtId;

            // Visual feedback for swapping
            const tempContent = draggedPlayer.innerHTML;
            draggedPlayer.innerHTML = target.innerHTML;
            target.innerHTML = tempContent;

            try {
                // Send swap request to the server
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
                console.log(JSON.stringify({
                    player1Id,
                    player2Id,
                    court1Id,
                    court2Id,
                }), response);
                if (!response.ok) {
                    console.log(response);
                    throw new Error("Failed to swap players");
                }
            } catch (error) {
                console.error(error);
                // Revert visual change on failure
                target.innerHTML = draggedPlayer.innerHTML;
                draggedPlayer.innerHTML = tempContent;
            }
        }
    });
});
