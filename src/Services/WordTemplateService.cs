using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace GestionHogar.Services;

public class WordTemplateService
{
    public (byte[], string?) ReplacePlaceholders(
        byte[] docxBytes,
        Dictionary<string, string> placeholders
    )
    {
        // FIXME: move to a proper place
        var provisionalPlaceholders = new Dictionary<string, string>()
        {
            { "{nro_contrato}", "" },
            { "{honorifico_cliente}", "" },
            { "{nombre_cliente}", "" },
            { "{dni_cliente}", "" },
            { "{estado_civil_cliente}", "" },
            { "{ocupacion_cliente}", "" },
            { "{domicilio_cliente}", "" },
            { "{distrito_cliente}", "" },
            { "{provincia_cliente}", "" },
            { "{departamento_cliente}", "" },
            { "{nombre_proyecto}", "" },
            { "{precio_dolares_metro_cuadrado}", "" },
            { "{area_terreno}", "" },
            { "{precio_departamento_dolares}", "" },
            { "{precio_departamento_dolares_letras}", "" },
            { "{precio_cochera_dolares}", "" },
            { "{precio_cochera_dolares_letras}", "" },
            { "{area_cochera}", "" },
            { "{nro_signada_cochera}", "" },
            { "{precio_total_dolares}", "" },
            { "{precio_total_dolares_letras}", "" },
            { "{precio_inicial_dolares}", "" },
            { "{precio_inicial_dolares_letras}", "" },
            { "{fecha_suscripcion_contrato_letras}", "" },
            { "{}", "" },
        };

        try
        {
            using var inputStream = new MemoryStream(docxBytes);
            using var outputStream = new MemoryStream();

            // Copy the original document to the output stream
            inputStream.CopyTo(outputStream);
            outputStream.Position = 0;

            // Open the document for editing
            using var document = WordprocessingDocument.Open(outputStream, true);

            if (document.MainDocumentPart == null)
            {
                return ([], "Document does not contain a main document part");
            }

            var body = document.MainDocumentPart.Document.Body;
            if (body == null)
            {
                return ([], "Document does not contain a body");
            }

            // Replace placeholders in all text runs
            foreach (var textElement in body.Descendants<Text>())
            {
                if (string.IsNullOrEmpty(textElement.Text))
                    continue;

                string originalText = textElement.Text;
                string replacedText = originalText;

                // Replace all placeholders in this text run
                foreach (var placeholder in placeholders)
                {
                    replacedText = replacedText.Replace(placeholder.Key, placeholder.Value);
                }

                // Only update if there were changes
                if (replacedText != originalText)
                {
                    textElement.Text = replacedText;
                }
            }

            // Save changes
            document.MainDocumentPart.Document.Save();

            return (outputStream.ToArray(), null);
        }
        catch (Exception ex)
        {
            return ([], $"Error processing document: {ex.Message}");
        }
    }
}
