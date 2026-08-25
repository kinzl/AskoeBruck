/**
 * Championship Page Scripts - TennisBruck
 */

// Global phases list for bracket duplicate detection
window.existingTournamentPhases = window.existingTournamentPhases || [];

document.addEventListener('DOMContentLoaded', () => {
    const dataEl = document.getElementById('championshipData');
    if (dataEl && dataEl.dataset.phases) {
        try {
            window.existingTournamentPhases = JSON.parse(dataEl.dataset.phases);
        } catch (e) {
            console.error('Could not parse tournament phases:', e);
        }
    }

    // Modal backdrop click listener
    window.addEventListener('click', function (e) {
        const editInfoModal = document.getElementById('editInfoModal');
        if (e.target === editInfoModal) {
            closeInfoModal();
        }
        const editDeadlineModal = document.getElementById('editDeadlineModal');
        if (e.target === editDeadlineModal) {
            closeDateModal();
        }
    });

    // Close modals on Escape key
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            closeInfoModal();
            closeDateModal();
        }
    });
});

/**
 * Validates and prompts if the bracket phase already exists before submitting.
 */
function checkPhaseExists() {
    const existingPhases = window.existingTournamentPhases || [];
    const inputElement = document.getElementById("PhaseName");

    if (!inputElement) return true;

    let inputPhase = inputElement.value.trim();
    if (inputPhase === "") {
        inputPhase = "A-Bewerb"; // Default fallback
    }

    if (existingPhases.includes(inputPhase)) {
        if (!confirm("Der Raster '" + inputPhase + "' existiert bereits. Bisherige Daten in diesem Raster werden überschrieben. Möchtest du wirklich fortfahren?")) {
            return false;
        }
    }

    const btnReal = document.getElementById("btnRealCreateBracket");
    if (btnReal) btnReal.click();
}

/**
 * Edit Info / PDF / Image Modal controls
 */
function openInfoModal() {
    const modal = document.getElementById('editInfoModal');
    if (modal) modal.style.display = 'flex';
}

function closeInfoModal() {
    const modal = document.getElementById('editInfoModal');
    if (modal) modal.style.display = 'none';
}

/**
 * Edit Registration Deadline Modal controls
 */
function openDateModal() {
    const modal = document.getElementById('editDeadlineModal');
    if (modal) modal.style.display = 'flex';
}

function closeDateModal() {
    const modal = document.getElementById('editDeadlineModal');
    if (modal) modal.style.display = 'none';
}

/**
 * Filters the match list by group / phase name and synchronizes dropdown and pill buttons.
 */
function filterAllMatches(groupName, btn) {
    // Sync active state of filter pills
    const pillsContainer = document.querySelector('.match-filter-pills');
    if (pillsContainer) {
        pillsContainer.querySelectorAll('.match-filter-btn').forEach(b => {
            const f = b.getAttribute('data-filter');
            if (f === groupName) {
                b.classList.add('active');
            } else {
                b.classList.remove('active');
            }
        });
    }

    // Sync select dropdown
    const select = document.getElementById('matchGroupFilterSelect');
    if (select && select.value !== groupName) {
        select.value = groupName;
    }

    // Show/hide matches
    const matches = document.querySelectorAll('#allMatchesList .match-scorecard');
    let visibleCount = 0;

    matches.forEach(card => {
        const grp = card.getAttribute('data-group');
        if (groupName === 'all' || grp === groupName) {
            card.style.display = 'flex';
            visibleCount++;
        } else {
            card.style.display = 'none';
        }
    });

    const msg = document.getElementById('noMatchesFilteredMsg');
    if (msg) {
        msg.style.display = visibleCount === 0 ? 'block' : 'none';
    }
}
