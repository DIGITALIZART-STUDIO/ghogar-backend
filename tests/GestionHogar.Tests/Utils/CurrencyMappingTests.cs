using GestionHogar.Model;
using GestionHogar.Utils;
using Xunit;

namespace GestionHogar.Tests.Utils;

public class CurrencyMappingTests
{
    [Theory]
    [InlineData("PEN", Currency.SOLES)]
    [InlineData("USD", Currency.DOLARES)]
    [InlineData("soles", Currency.SOLES)]
    [InlineData("dolares", Currency.DOLARES)]
    public void FromIsoCode_MapsProjectAndDocumentCurrencies(string code, Currency expected)
    {
        Assert.Equal(expected, CurrencyMapping.FromIsoCode(code));
    }

    [Fact]
    public void FormatAmount_UsesProjectCurrencySymbol()
    {
        Assert.StartsWith("S/", CurrencyMapping.FormatAmount(14741.55m, Currency.SOLES));
        Assert.Contains("14,741.55", CurrencyMapping.FormatAmount(14741.55m, Currency.SOLES));
        Assert.StartsWith("US$", CurrencyMapping.FormatAmount(37166.25m, Currency.DOLARES));
    }

    [Fact]
    public void Convert_UsesExchangeRateInBothDirections()
    {
        Assert.Equal(37000m, CurrencyMapping.Convert(10000m, Currency.DOLARES, 3.7m));
        Assert.Equal(10000m, CurrencyMapping.Convert(37000m, Currency.SOLES, 3.7m));
        Assert.Equal(0m, CurrencyMapping.Convert(10000m, Currency.DOLARES, 0m));
    }
}
