using GestionHogar.Services;
using Xunit;

namespace GestionHogar.Tests.Services;

public class ContractHtmlServiceTests
{
    private readonly ContractHtmlService _service = new();

    [Fact]
    public void Sanitize_RemovesScriptTagsButKeepsInnerText()
    {
        var html = "<p>Hola <script>alert(1)</script>mundo</p>";

        var result = _service.Sanitize(html);

        Assert.DoesNotContain("<script", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hola", result);
        Assert.Contains("mundo", result);
    }

    [Fact]
    public void Sanitize_RemovesJavascriptHref()
    {
        var html = "<p><a href=\"javascript:alert(1)\">link</a></p>";

        var result = _service.Sanitize(html);

        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("link", result);
    }

    [Fact]
    public void Sanitize_RemovesOnEventAttributes()
    {
        var html = "<p onclick=\"alert(1)\" class=\"body\">Texto</p>";

        var result = _service.Sanitize(html);

        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Texto", result);
        Assert.Contains("class=\"body\"", result);
    }

    [Fact]
    public void Sanitize_PreservesContractVariables()
    {
        var html =
            "<p>Contrato <span class=\"contract-variable\" data-variable=\"{nro_contrato}\">{nro_contrato}</span></p>";

        var result = _service.Sanitize(html);

        Assert.Contains("{nro_contrato}", result);
        Assert.Contains("data-variable", result);
    }

    [Fact]
    public void Sanitize_ReturnsEmptyForBlankInput()
    {
        Assert.Equal(string.Empty, _service.Sanitize("   "));
        Assert.Equal(string.Empty, _service.Sanitize(null!));
    }

    [Fact]
    public void ExtractBody_KeepsStylesAndBodyContent()
    {
        var html =
            "<html><head><style>p{color:red}</style></head><body><p>Contrato</p></body></html>";

        var result = _service.ExtractBody(html);

        Assert.Contains("<style>p{color:red}</style>", result);
        Assert.Contains("<p>Contrato</p>", result);
        Assert.DoesNotContain("<html", result);
    }

    [Fact]
    public void ReplaceVariables_ReplacesLongerKeysBeforeShorterOnes()
    {
        var html =
            "<p>Total {precio_total_dolares} ({precio_total_dolares_letras})</p>";
        var placeholders = new Dictionary<string, string>
        {
            ["{precio_total_dolares}"] = "$ 100.00",
            ["{precio_total_dolares_letras}"] = "cien dolares",
        };

        var result = _service.ReplaceVariables(html, placeholders);

        Assert.Equal("<p>Total $ 100.00 (cien dolares)</p>", result);
        Assert.DoesNotContain("{precio_total", result);
        Assert.DoesNotContain("_letras", result);
    }

    [Fact]
    public void ReplaceVariables_HtmlEncodesValues()
    {
        var html = "<p>{nombre_cliente}</p>";
        var placeholders = new Dictionary<string, string>
        {
            ["{nombre_cliente}"] = "Juan <script> & Maria",
        };

        var result = _service.ReplaceVariables(html, placeholders);

        Assert.Equal("<p>Juan &lt;script&gt; &amp; Maria</p>", result);
        Assert.DoesNotContain("<script>", result);
    }

    [Fact]
    public void WrapForConversion_EmbedsBodyInHtmlDocument()
    {
        var wrapped = _service.WrapForConversion("<p>Hola</p>");

        Assert.StartsWith("<!DOCTYPE html>", wrapped);
        Assert.Contains("<p>Hola</p>", wrapped);
        Assert.Contains("</body></html>", wrapped);
    }
}
