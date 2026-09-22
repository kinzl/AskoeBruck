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

    const recurringGroup = document.getElementById('modalRecurringGroup');
    const modalRepeatWeeks = document.getElementById('modalRepeatWeeks');
    if (recurringGroup) recurringGroup.style.display = 'block';
    if (modalRepeatWeeks) modalRepeatWeeks.value = '1';

    if (btnModalDelete) {
        btnModalDelete.classList.add('hidden-element');
        btnModalDelete.style.display = 'none';
    }
    if (btnModalSubmit) btnModalSubmit.innerText = 'Reservieren';

    if (reservationModal) {
        reservationModal.classList.remove('hidden-element');
        reservationModal.classList.add('active');
        reservationModal.style.display = 'flex';
    }
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

    const recurringGroup = document.getElementById('modalRecurringGroup');
    const modalRepeatWeeks = document.getElementById('modalRepeatWeeks');
    if (recurringGroup) recurringGroup.style.display = 'none';
    if (modalRepeatWeeks) modalRepeatWeeks.value = '1';

    if (btnModalDelete) {
        btnModalDelete.classList.remove('hidden-element');
        btnModalDelete.style.display = 'inline-block';
    }
    if (deleteReservationId) deleteReservationId.value = reservationId;
    if (btnModalSubmit) btnModalSubmit.innerText = 'Speichern';

    if (reservationModal) {
        reservationModal.classList.remove('hidden-element');
        reservationModal.classList.add('active');
        reservationModal.style.display = 'flex';
    }
}

function toggleRecurringOptions(isChecked) {
    const recurringOptions = document.getElementById('recurringOptions');
    const modalRepeatWeeks = document.getElementById('modalRepeatWeeks');
    if (recurringOptions) {
        recurringOptions.style.display = isChecked ? 'block' : 'none';
    }
    if (modalRepeatWeeks) {
        modalRepeatWeeks.value = isChecked ? '4' : '1';
    }
}

function closeReservationModal() {
    const reservationModal = document.getElementById('reservationModal');
    if (reservationModal) {
        reservationModal.classList.add('hidden-element');
        reservationModal.classList.remove('active');
        reservationModal.style.display = 'none';
    }
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
