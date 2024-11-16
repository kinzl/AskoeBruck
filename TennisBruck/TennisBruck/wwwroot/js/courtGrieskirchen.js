document.addEventListener("DOMContentLoaded", () => {
    let draggedPlayerId = null;
    let draggedCourtId = null;
    let draggedCell = null;

    // Set draggable attribute and add event listeners for each player cell
    document.querySelectorAll(".draggable-player").forEach(cell => {
        cell.draggable = true;

        // Drag start event
        cell.addEventListener("dragstart", event => {
            draggedCell = event.target; // Store reference to the dragged cell
            draggedPlayerId = draggedCell.dataset.playerId;
            draggedCourtId = draggedCell.dataset.courtId;

            // Store the text in the dataTransfer object to allow it to be accessed during drop
            event.dataTransfer.setData("playerId", draggedPlayerId);
            event.dataTransfer.setData("courtId", draggedCourtId);
            event.dataTransfer.setData("playerText", draggedCell.innerText);
            console.log("Drag started:", draggedPlayerId, draggedCourtId);
        });

        // Drag over event (allow drop on any target cell)
        cell.addEventListener("dragover", event => {
            event.preventDefault(); // Allow drop
        });

        // Drop event
        cell.addEventListener("drop", async event => {
            event.preventDefault();
            const targetCell = event.target;
            const targetPlayerId = targetCell.dataset.playerId;
            const targetCourtId = targetCell.dataset.courtId;

            console.log("Dropped on:", targetPlayerId, targetCourtId);

            // Check if drop target is different (to avoid unnecessary swap)
            if (draggedPlayerId !== targetPlayerId || draggedCourtId !== targetCourtId) {
                // Send swap request to backend
                try {
                    const response = await fetch("/CourtGrieskirchen?handler=SwapPlayers", {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json",
                            "RequestVerificationToken": document.querySelector("input[name='__RequestVerificationToken']").value
                        },
                        body: JSON.stringify({
                            player1Id: draggedPlayerId,
                            court1Id: draggedCourtId,
                            player2Id: targetPlayerId,
                            court2Id: targetCourtId
                        })
                    });

                    if (response.ok) {
                        // Swap the player names and data attributes
                        const draggedText = event.dataTransfer.getData("playerText");
                        const targetText = targetCell.innerText;

                        // Swap the player names (innerText)
                        targetCell.innerText = draggedText;
                        draggedCell.innerText = targetText;

                        // Swap the player IDs and court IDs (data attributes)
                        targetCell.dataset.playerId = draggedPlayerId;
                        targetCell.dataset.courtId = draggedCourtId;

                        draggedCell.dataset.playerId = targetPlayerId;
                        draggedCell.dataset.courtId = targetCourtId;

                    } else {
                        alert("Failed to swap players.");
                    }
                } catch (error) {
                    console.error("Error:", error);
                }
            }

            // Clear dragged data
            draggedPlayerId = null;
            draggedCourtId = null;
            draggedCell = null;
        });
    });
});
