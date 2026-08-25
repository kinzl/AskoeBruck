/**
 * Members Page Scripts - TennisBruck
 */

let currentSortCol = -1;
let currentSortDir = "asc";

/**
 * Filter members in the table by firstname and lastname
 */
function filterMembers() {
    const input = document.getElementById("memberSearch");
    if (!input) return;

    const filter = input.value.toLowerCase().trim();
    const table = document.querySelector(".members-table");
    if (!table) return;

    const trs = table.querySelectorAll("tbody tr");

    trs.forEach(tr => {
        const firstnameTd = tr.querySelector("td:nth-child(1)");
        const lastnameTd = tr.querySelector("td:nth-child(2)");
        if (firstnameTd && lastnameTd) {
            const firstnameText = firstnameTd.getAttribute("data-val") || "";
            const lastnameText = lastnameTd.getAttribute("data-val") || "";
            const combined = (firstnameText + " " + lastnameText).toLowerCase();
            if (combined.includes(filter)) {
                tr.style.display = "";
            } else {
                tr.style.display = "none";
            }
        }
    });
}

/**
 * Sort table rows by column index (0 = Vorname, 1 = Nachname)
 */
function sortTable(colIndex) {
    const table = document.querySelector(".members-table");
    if (!table) return;

    const tbody = table.querySelector("tbody");
    if (!tbody) return;

    const rows = Array.from(tbody.querySelectorAll("tr"));

    if (currentSortCol === colIndex) {
        currentSortDir = currentSortDir === "asc" ? "desc" : "asc";
    } else {
        currentSortCol = colIndex;
        currentSortDir = "asc";
    }

    rows.sort((a, b) => {
        const cellA = (a.querySelector(`td:nth-child(${colIndex + 1})`)?.getAttribute("data-val") || "").toLowerCase();
        const cellB = (b.querySelector(`td:nth-child(${colIndex + 1})`)?.getAttribute("data-val") || "").toLowerCase();

        return currentSortDir === "asc"
            ? cellA.localeCompare(cellB, 'de', { sensitivity: 'base' })
            : cellB.localeCompare(cellA, 'de', { sensitivity: 'base' });
    });

    tbody.innerHTML = "";
    rows.forEach(row => tbody.appendChild(row));

    const firstnameTh = document.getElementById("th-firstname");
    const lastnameTh = document.getElementById("th-lastname");
    const firstnameIcon = firstnameTh?.querySelector(".sort-icon");
    const lastnameIcon = lastnameTh?.querySelector(".sort-icon");

    if (colIndex === 0 && firstnameIcon && lastnameIcon) {
        firstnameIcon.textContent = currentSortDir === "asc" ? "▲" : "▼";
        lastnameIcon.textContent = "↕";
    } else if (colIndex === 1 && firstnameIcon && lastnameIcon) {
        lastnameIcon.textContent = currentSortDir === "asc" ? "▲" : "▼";
        firstnameIcon.textContent = "↕";
    }
}

/**
 * Opens edit modal with player data
 */
function openEditModal(id, firstname, lastname, itn, email, isOffline, hasNuLiga) {
    const modal = document.getElementById('editModal');
    const editPlayerId = document.getElementById('editPlayerId');
    const editFirstname = document.getElementById('editFirstname');
    const editLastname = document.getElementById('editLastname');
    const editEmail = document.getElementById('editEmail');
    const emailHint = document.getElementById('editEmailHint');
    const offlineNote = document.getElementById('editOfflineNote');
    const itnInput = document.getElementById('editItn');
    const itnHint = document.getElementById('editItnHint');

    if (editPlayerId) editPlayerId.value = id;
    if (editFirstname) editFirstname.value = firstname;
    if (editLastname) editLastname.value = lastname;
    if (editEmail) editEmail.value = email;

    // Email field: hint text changes for offline players
    if (emailHint && offlineNote) {
        if (isOffline) {
            emailHint.textContent = '(optional — wird Login-Account erstellen)';
            offlineNote.classList.remove('hidden-element');
            offlineNote.style.display = 'block';
        } else {
            emailHint.textContent = '(optional)';
            offlineNote.classList.add('hidden-element');
            offlineNote.style.display = 'none';
        }
    }

    // ITN field: disabled when ÖTV/NuLiga URL exists (auto-synced)
    if (itnInput && itnHint) {
        if (hasNuLiga) {
            itnInput.value = itn;
            itnInput.disabled = true;
            itnInput.title = 'Wird automatisch vom ÖTV synchronisiert';
            itnHint.textContent = '(automatisch vom ÖTV — nicht editierbar)';
        } else {
            itnInput.value = itn;
            itnInput.disabled = false;
            itnInput.title = '';
            itnHint.textContent = '(optional)';
        }
    }

    if (modal) {
        modal.classList.remove('hidden-element');
        modal.style.display = 'flex';
    }
    document.body.style.overflow = 'hidden';
}

/**
 * Closes edit modal
 */
function closeEditModal() {
    const modal = document.getElementById('editModal');
    if (modal) {
        modal.classList.add('hidden-element');
        modal.style.display = 'none';
    }
    document.body.style.overflow = '';
}

// Event Listeners for Backdrop click & Escape key
document.addEventListener('DOMContentLoaded', () => {
    const modal = document.getElementById('editModal');
    if (modal) {
        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                closeEditModal();
            }
        });
    }

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closeEditModal();
        }
    });
});
