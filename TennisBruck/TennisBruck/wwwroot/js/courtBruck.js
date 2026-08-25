/**
 * CourtBruck - Interactive Reservation and Booking Modal Scripts
 */

function openBookingModal(court, startTime, endTime) {
    const modalTitle = document.getElementById('modalTitle');
    const modalForm = document.getElementById('modalForm');
    const modalReservationId = document.getElementById('modalReservationId');
    const modalCourtNumber = document.getElementById('modalCourtNumber');
    const modalStartTime = document.getElementById('modalStartTime');
    const modalEndTime = document.getElementById('modalEndTime');
    const modalPartnerId = document.getElementById('modalPartnerId');
    const eventNameInput = document.getElementById('modalEventName');
    const btnModalDelete = document.getElementById('btnModalDelete');
    const btnModalSubmit = document.getElementById('btnModalSubmit');
    const reservationModal = document.getElementById('reservationModal');

    if (modalTitle) modalTitle.innerText = 'Platz ' + court + ' reservieren';
    if (modalForm) modalForm.action = '?handler=CreateReservation';
    if (modalReservationId) modalReservationId.value = '0';
    if (modalCourtNumber) modalCourtNumber.value = court;
    if (modalStartTime) modalStartTime.value = startTime;
    if (modalEndTime) modalEndTime.value = endTime;
    if (modalPartnerId) modalPartnerId.value = '';
    if (eventNameInput) eventNameInput.value = '';

    if (btnModalDelete) btnModalDelete.style.display = 'none';
    if (btnModalSubmit) btnModalSubmit.innerText = 'Reservieren';

    if (reservationModal) reservationModal.style.display = 'flex';
}

function openEditModal(reservationId, court, startTime, endTime, partnerId, eventName) {
    const modalTitle = document.getElementById('modalTitle');
    const modalForm = document.getElementById('modalForm');
    const modalReservationId = document.getElementById('modalReservationId');
    const modalCourtNumber = document.getElementById('modalCourtNumber');
    const modalStartTime = document.getElementById('modalStartTime');
    const modalEndTime = document.getElementById('modalEndTime');
    const modalPartnerId = document.getElementById('modalPartnerId');
    const eventNameInput = document.getElementById('modalEventName');
    const btnModalDelete = document.getElementById('btnModalDelete');
    const deleteReservationId = document.getElementById('deleteReservationId');
    const btnModalSubmit = document.getElementById('btnModalSubmit');
    const reservationModal = document.getElementById('reservationModal');

    if (modalTitle) modalTitle.innerText = 'Reservierung bearbeiten';
    if (modalForm) modalForm.action = '?handler=UpdateReservation';
    if (modalReservationId) modalReservationId.value = reservationId;
    if (modalCourtNumber) modalCourtNumber.value = court;
    if (modalStartTime) modalStartTime.value = startTime;
    if (modalEndTime) modalEndTime.value = endTime;
    if (modalPartnerId) modalPartnerId.value = partnerId > 0 ? partnerId : '';
    if (eventNameInput) eventNameInput.value = eventName || '';

    if (btnModalDelete) btnModalDelete.style.display = 'inline-block';
    if (deleteReservationId) deleteReservationId.value = reservationId;
    if (btnModalSubmit) btnModalSubmit.innerText = 'Speichern';

    if (reservationModal) reservationModal.style.display = 'flex';
}

function closeReservationModal() {
    const reservationModal = document.getElementById('reservationModal');
    if (reservationModal) reservationModal.style.display = 'none';
}

function onStartTimeChanged() {
    const startTimeEl = document.getElementById('modalStartTime');
    const endTimeEl = document.getElementById('modalEndTime');
    if (!startTimeEl || !endTimeEl || !startTimeEl.value) return;

    const parts = startTimeEl.value.split(':').map(Number);
    let endHours = parts[0] + 2;
    if (endHours > 22) endHours = 22;

    const pad = n => n.toString().padStart(2, '0');
    endTimeEl.value = `${pad(endHours)}:${pad(parts[1])}`;
}

function deleteCurrentReservation() {
    if (confirm('Möchtest du diese Reservierung wirklich stornieren/löschen?')) {
        const deleteForm = document.getElementById('deleteForm');
        if (deleteForm) deleteForm.submit();
    }
}

window.addEventListener('click', function (event) {
    const modal = document.getElementById('reservationModal');
    if (event.target === modal) {
        closeReservationModal();
    }
});
