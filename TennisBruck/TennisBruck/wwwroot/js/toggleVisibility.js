function toggleCreateUser() {
    const createUserSection = document.getElementById('createUser');
    if (createUserSection.style.display === 'none' || createUserSection.style.display === '') {
        createUserSection.style.display = 'block';
    } else {
        createUserSection.style.display = 'none';
    }
}

function toggleForgotPassword() {
    const createUserSection = document.getElementById('forgotPassword');
    const loginSection = document.getElementById('login-section');
    if (createUserSection.style.display === 'none' || createUserSection.style.display === '') {
        createUserSection.style.display = 'block';
        loginSection.style.display = 'none';
    } else {
        createUserSection.style.display = 'none';
        loginSection.style.display = 'block';
    }
}
