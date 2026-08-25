/**
 * Pyramid Page Scripts - TennisBruck
 */

/**
 * Opens the match result modal with challenger & defender options
 */
function openResultModal(challengeId, challengerName, challengerId, defenderName, defenderId) {
    const challengeInput = document.getElementById('modalChallengeId');
    if (challengeInput) challengeInput.value = challengeId;

    const select = document.getElementById('modalWinnerSelect');
    if (select) {
        select.innerHTML = '';

        const opt1 = document.createElement('option');
        opt1.value = challengerId;
        opt1.textContent = challengerName + ' (Forderer - übernimmt Rang bei Sieg)';

        const opt2 = document.createElement('option');
        opt2.value = defenderId;
        opt2.textContent = defenderName + ' (Geforderter - verteidigt Rang)';

        select.appendChild(opt1);
        select.appendChild(opt2);
    }

    const modal = document.getElementById('resultModal');
    if (modal) modal.style.display = 'flex';
}

/**
 * Closes the match result modal
 */
function closeResultModal() {
    const modal = document.getElementById('resultModal');
    if (modal) modal.style.display = 'none';
}

document.addEventListener('DOMContentLoaded', () => {
    const resultModal = document.getElementById('resultModal');
    if (resultModal) {
        resultModal.addEventListener('click', (e) => {
            if (e.target === resultModal) {
                closeResultModal();
            }
        });
    }

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closeResultModal();
        }
    });
});
