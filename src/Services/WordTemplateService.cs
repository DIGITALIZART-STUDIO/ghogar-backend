using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace GestionHogar.Services;

public class WordTemplateService
{
    public (byte[], string?) ReplacePlaceholders(
        Stream docxStream,
        Dictionary<string, string> placeholders
    )
    {
        try
        {
            using var inputStream = docxStream;
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
            document.Save();

            return (outputStream.ToArray(), null);
        }
        catch (Exception ex)
        {
            return ([], $"Error processing document: {ex.Message}");
        }
    }
}
