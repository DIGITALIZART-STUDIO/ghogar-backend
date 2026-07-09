namespace GestionHogar.Utils;

/// <summary>
/// Reglas de expiración de leads por días calendario en zona horaria de Lima.
/// Día 1 = día de creación; último día válido = referencia + 7 días; expira al día siguiente.
/// </summary>
public static class LeadExpirationHelper
{
    public const int ValidityCalendarDays = 7;

    /// <summary>
    /// Fecha de referencia del ciclo de 7 días. Si el lead fue reciclado, el ciclo
    /// reinicia desde el último reciclaje; de lo contrario usa entryDate (o createdAt).
    /// </summary>
    public static DateTime GetReferenceDate(
        DateTime entryDate,
        DateTime createdAt,
        DateTime? lastRecycledAt = null
    )
    {
        if (lastRecycledAt is { Year: >= 2000 })
            return lastRecycledAt.Value;

        if (entryDate == default || entryDate.Year < 2000)
            return createdAt;

        return entryDate;
    }

    public static DateOnly GetEntryDateLima(DateTime referenceUtc)
    {
        var utc = EnsureUtc(referenceUtc);
        var lima = TimeZoneInfo.ConvertTimeFromUtc(utc, PeruTimeZone.Instance);
        return DateOnly.FromDateTime(lima.Date);
    }

    public static DateOnly GetLastValidDayLima(DateTime referenceUtc)
    {
        return GetEntryDateLima(referenceUtc).AddDays(ValidityCalendarDays);
    }

    public static DateOnly GetTodayLima()
    {
        var lima = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PeruTimeZone.Instance);
        return DateOnly.FromDateTime(lima.Date);
    }

    /// <summary>
    /// Fin del último día válido en Lima, persistido como UTC.
    /// </summary>
    public static DateTime GetExpirationDateUtc(DateTime referenceUtc)
    {
        var lastValidDay = GetLastValidDayLima(referenceUtc);
        var endOfDayLima = new DateTime(
            lastValidDay.Year,
            lastValidDay.Month,
            lastValidDay.Day,
            23,
            59,
            59,
            999,
            DateTimeKind.Unspecified
        );

        return DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeToUtc(endOfDayLima, PeruTimeZone.Instance),
            DateTimeKind.Utc
        );
    }

    public static bool IsCalendarExpired(DateTime referenceUtc)
    {
        return GetTodayLima() > GetLastValidDayLima(referenceUtc);
    }

    public static int GetDaysUntilExpiration(DateTime referenceUtc)
    {
        if (IsCalendarExpired(referenceUtc))
            return 0;

        return GetLastValidDayLima(referenceUtc).DayNumber - GetTodayLima().DayNumber;
    }

    public static string GetExpirationLabel(DateTime referenceUtc)
    {
        if (IsCalendarExpired(referenceUtc))
            return "Expirado";

        return GetDaysUntilExpiration(referenceUtc) switch
        {
            0 => "Expira hoy",
            1 => "Expira mañana",
            var days => $"Expira en {days} días",
        };
    }

    public static void ApplyExpirationFields(
        DateTime entryDate,
        DateTime createdAt,
        out bool isExpired,
        out int daysUntilExpiration,
        out string expirationLabel,
        DateTime? lastRecycledAt = null
    )
    {
        var reference = GetReferenceDate(entryDate, createdAt, lastRecycledAt);
        isExpired = IsCalendarExpired(reference);
        daysUntilExpiration = GetDaysUntilExpiration(reference);
        expirationLabel = GetExpirationLabel(reference);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }
}
