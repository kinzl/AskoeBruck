document.addEventListener('DOMContentLoaded', async () => {
    const dateInput = document.querySelector('input[name="date"]');
    const currentViewDate = window.currentViewDate || (dateInput ? dateInput.value : '');
    if (!currentViewDate) return;

    const latitude = 48.25;
    const longitude = 13.78;
    const url = `https://api.open-meteo.com/v1/forecast?latitude=${latitude}&longitude=${longitude}&hourly=temperature_2m,weathercode&timezone=Europe%2FBerlin&start_date=${currentViewDate}&end_date=${currentViewDate}`;

    try {
        const response = await fetch(url);
        if (!response.ok) throw new Error('Weather data fetch failed');
        
        const data = await response.json();
        const hourlyTimes = data.hourly.time;
        const temperatures = data.hourly.temperature_2m;
        const weatherCodes = data.hourly.weathercode;

        const weatherElements = document.querySelectorAll('.weather-info');

        weatherElements.forEach(el => {
            const timeStr = el.getAttribute('data-time');
            const index = hourlyTimes.indexOf(timeStr);
            
            if (index !== -1) {
                const temp = Math.round(temperatures[index]);
                const code = weatherCodes[index];
                const emoji = getWeatherEmoji(code);

                el.innerHTML = `${emoji} ${temp}°C`;
            }
        });

    } catch (error) {
        console.error("Error loading weather data:", error);
    }
});

function getWeatherEmoji(code) {
    switch (code) {
        case 0:
            return '☀️'; // Clear sky
        case 1:
            return '🌤️'; // Mainly clear
        case 2:
            return '⛅'; // Partly cloudy
        case 3:
            return '☁️'; // Overcast
        case 45:
        case 48:
            return '🌫️'; // Fog
        case 51:
        case 53:
        case 55:
        case 56:
        case 57:
            return '🌧️'; // Drizzle
        case 61:
        case 63:
        case 65:
        case 66:
        case 67:
            return '🌧️'; // Rain
        case 71:
        case 73:
        case 75:
        case 77:
            return '❄️'; // Snow
        case 80:
        case 81:
        case 82:
            return '🌦️'; // Rain showers
        case 85:
        case 86:
            return '🌨️'; // Snow showers
        case 95:
        case 96:
        case 99:
            return '⛈️'; // Thunderstorm
        default:
            return '❓';
    }
}
