/**
 * PartnerBoard Page Scripts - TennisBruck
 */

document.addEventListener('DOMContentLoaded', () => {
    const dateFrom = document.getElementById('filterDateFrom');
    const dateTo = document.getElementById('filterDateTo');
    const timeFrom = document.getElementById('filterTimeFrom');
    const timeTo = document.getElementById('filterTimeTo');

    // Keep "Datum bis" min in sync with "Datum von"
    if (dateFrom && dateTo) {
        const today = new Date().toISOString().split('T')[0];
        dateFrom.addEventListener('change', () => {
            dateTo.min = dateFrom.value || today;
            if (dateTo.value && dateTo.value < dateFrom.value) {
                dateTo.value = dateFrom.value;
            }
        });
        if (dateFrom.value) {
            dateTo.min = dateFrom.value;
        }
    }

    // Keep "Zeit bis" min in sync with "Zeit von"
    if (timeFrom && timeTo) {
        timeFrom.addEventListener('change', () => {
            timeTo.min = timeFrom.value;
            if (timeTo.value && timeTo.value <= timeFrom.value) {
                timeTo.value = '';
            }
        });
        if (timeFrom.value) {
            timeTo.min = timeFrom.value;
        }
    }
});

/**
 * Opens the edit modal for a PartnerBoard slot
 */
function openEditModal(id, date, startTime, endTime, neededPlayers, message) {
    const editSlotId = document.getElementById('editSlotId');
    const editDate = document.getElementById('editDate');
    const editStartTime = document.getElementById('editStartTime');
    const editEndTime = document.getElementById('editEndTime');
    const editNeededPlayers = document.getElementById('editNeededPlayers');
    const editMessage = document.getElementById('editMessage');

    if (editSlotId) editSlotId.value = id;
    if (editDate) editDate.value = date;
    if (editStartTime) editStartTime.value = startTime;
    if (editEndTime) editEndTime.value = endTime;
    if (editNeededPlayers) editNeededPlayers.value = neededPlayers;
    if (editMessage) editMessage.value = message;

    const modalEl = document.getElementById('editSlotModal');
    if (modalEl && window.bootstrap && typeof window.bootstrap.Modal === 'function') {
        const editModal = bootstrap.Modal.getOrCreateInstance(modalEl);
        editModal.show();
    }
}
