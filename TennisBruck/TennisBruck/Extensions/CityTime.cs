namespace TennisBruck.Services;

public static class CityTime
{
    public static DateTime GetViennaTimeZone()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna"));
    }
}