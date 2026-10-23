using System.Net;
using HtmlAgilityPack;

namespace GestionHogar.Services;

public class ContractHtmlService
{
    public const int MaxHtmlLength = 2 * 1024 * 1024;
    public const int MaxDocxBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "html",
        "head",
        "body",
        "meta",
        "title",
        "style",
        "p",
        "br",
        "div",
        "span",
        "strong",
        "b",
        "em",
        "i",
        "u",
        "s",
        "strike",
        "h1",
        "h2",
        "h3",
        "h4",
        "h5",
        "h6",
        "ul",
        "ol",
        "li",
        "table",
        "thead",
        "tbody",
        "tfoot",
        "tr",
        "th",
        "td",
        "colgroup",
        "col",
        "blockquote",
        "hr",
        "a",
        "img",
        "sup",
        "sub",
        "font",
        "pre",
        "section",
        "article",
        "header",
        "footer",
        "figure",
        "figcaption",
        "center",
    };

    private static readonly HashSet<string> AllowedAttributes = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "style",
        "class",
        "href",
        "src",
        "alt",
        "title",
        "colspan",
        "rowspan",
        "width",
        "height",
        "align",
        "valign",
        "border",
        "cellpadding",
        "cellspacing",
        "data-variable",
        "charset",
        "lang",
        "dir",
        "face",
        "size",
        "color",
        "type",
        "start",
        "data-list-style",
    };

    public string ExtractBody(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var body = doc.DocumentNode.SelectSingleNode("//body");
        var styleNodes = doc.DocumentNode.SelectNodes("//style");
        var styles = string.Empty;
        if (styleNodes != null)
        {
            foreach (var styleNode in styleNodes)
                styles += styleNode.OuterHtml;
        }

        var content = body != null ? body.InnerHtml : html;
        return styles + content;
    }

    public string Sanitize(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        SanitizeNode(doc.DocumentNode);

        var sanitized = ExtractBody(doc.DocumentNode.OuterHtml).Trim();
        if (sanitized.Length > MaxHtmlLength)
            throw new InvalidOperationException(
                "El contenido del contrato supera el tamaño máximo permitido"
            );

        return sanitized;
    }

    public string ReplaceVariables(string html, Dictionary<string, string> placeholders)
    {
        var result = html ?? string.Empty;

        // Longer keys first so `{precio_total_dolares}` does not eat
        // `{precio_total_dolares_letras}`.
        foreach (var placeholder in placeholders.OrderByDescending(p => p.Key.Length))
        {
            var encoded = WebUtility.HtmlEncode(placeholder.Value ?? string.Empty);
            result = result.Replace(placeholder.Key, encoded);
        }

        return result;
    }

    public string WrapForConversion(string bodyHtml)
    {
        return "<!DOCTYPE html><html><head><meta charset=\"UTF-8\" /><style>"
            + "body { font-family: \"Times New Roman\", serif; font-size: 12pt; line-height: 1.4; }"
            + "p { margin: 0 0 8pt 0; }"
            + "table { border-collapse: collapse; width: 100%; }"
            + "td, th { border: 1px solid #000; padding: 4px; vertical-align: top; }"
            + "ul, ol { margin: 0 0 8pt 24pt; padding-left: 24pt; }"
            + "ul { list-style-type: disc; }"
            + "ol { list-style-type: decimal; }"
            + "ul[data-list-style=\"dash\"] { list-style-type: \"- \"; }"
            + "li { display: list-item; }"
            + ".contract-variable { background: transparent; }"
            + "</style></head><body>"
            + bodyHtml
            + "</body></html>";
    }

    private static void SanitizeNode(HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Element)
        {
            if (!AllowedTags.Contains(node.Name))
            {
                var parent = node.ParentNode;
                if (parent != null)
                {
                    var children = node.ChildNodes.ToList();
                    foreach (var child in children)
                    {
                        parent.InsertBefore(child, node);
                        SanitizeNode(child);
                    }
                    parent.RemoveChild(node);
                }
                return;
            }

            var attributes = node.Attributes.ToList();
            foreach (var attribute in attributes)
            {
                if (
                    !AllowedAttributes.Contains(attribute.Name)
                    || attribute.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                )
                {
                    attribute.Remove();
                    continue;
                }

                if (
                    attribute.Name.Equals("href", StringComparison.OrdinalIgnoreCase)
                    || attribute.Name.Equals("src", StringComparison.OrdinalIgnoreCase)
                )
                {
                    var value = attribute.Value?.Trim() ?? string.Empty;
                    if (
                        value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
                        || value.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        attribute.Remove();
                    }
                }
            }
        }

        foreach (var child in node.ChildNodes.ToList())
            SanitizeNode(child);
    }
}
