using GestionHogar.Model;

namespace GestionHogar.Utils;

public static class CurrencyMapping
{
    public static Currency FromIsoCode(string? code, string source = "proyecto")
    {
        return (code ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "PEN" or "SOLES" => Currency.SOLES,
            "USD" or "DOLARES" or "DÓLARES" => Currency.DOLARES,
            _ => throw new ArgumentException(
                $"La moneda del {source} ('{code}') no está soportada. Valores soportados: PEN, USD."
            ),
        };
    }

    public static string Symbol(Currency currency) => currency == Currency.SOLES ? "S/" : "US$";

    public static string NameInWords(Currency currency) =>
        currency == Currency.SOLES ? "SOLES" : "DÓLARES";

    public static string FormatAmount(decimal amount, Currency currency) =>
        $"{Symbol(currency)} {amount:N2}";

    public static Currency Counterpart(Currency currency) =>
        currency == Currency.SOLES ? Currency.DOLARES : Currency.SOLES;

    /// <summary>
    /// Convierte un monto a la moneda contrapuesta usando el tipo de cambio
    /// (USD → PEN = monto * TC, PEN → USD = monto / TC).
    /// </summary>
    public static decimal Convert(decimal amount, Currency from, decimal exchangeRate)
    {
        if (exchangeRate <= 0)
        {
            return 0;
        }

        return from == Currency.DOLARES ? amount * exchangeRate : amount / exchangeRate;
    }
}
