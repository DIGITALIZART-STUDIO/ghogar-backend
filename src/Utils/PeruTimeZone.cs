namespace GestionHogar.Utils;

public static class PeruTimeZone
{
    private static readonly Lazy<TimeZoneInfo> LazyInstance = new(GetPeruTimeZone);

    public static TimeZoneInfo Instance => LazyInstance.Value;

    private static TimeZoneInfo GetPeruTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone(
                    "Peru Time",
                    TimeSpan.FromHours(-5),
                    "Peru Time",
                    "Peru Time"
                );
            }
        }
    }
}
