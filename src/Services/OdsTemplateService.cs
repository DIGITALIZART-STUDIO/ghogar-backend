using System.IO.Compression;
using System.Text;
using System.Xml;

namespace GestionHogar.Services;

public class OdsTemplateService
{
    public (byte[], string?) ReplacePlaceholders(
        byte[] odsBytes,
        Dictionary<string, string> placeholders
    )
    {
        try
        {
            using var inputStream = new MemoryStream(odsBytes);
            using var outputStream = new MemoryStream();

            // Copy the original ODS to output stream
            inputStream.CopyTo(outputStream);
            outputStream.Position = 0;

            // Open as ZIP archive for modification
            using var archive = new ZipArchive(outputStream, ZipArchiveMode.Update, true);

            // Find the content.xml file (where the actual spreadsheet data is)
            var contentEntry = archive.GetEntry("content.xml");
            if (contentEntry == null)
            {
                return (Array.Empty<byte>(), "ODS file does not contain content.xml");
            }

            // Read the content.xml
            string contentXml;
            using (var contentStream = contentEntry.Open())
            using (var reader = new StreamReader(contentStream, Encoding.UTF8))
            {
                contentXml = reader.ReadToEnd();
            }

            // Replace placeholders in the XML content
            string modifiedXml = contentXml;
            foreach (var placeholder in placeholders)
            {
                modifiedXml = modifiedXml.Replace(placeholder.Key, placeholder.Value);
            }

            // Only update if there were changes
            if (modifiedXml != contentXml)
            {
                // Validate the modified XML is still valid
                try
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(modifiedXml);
                }
                catch (XmlException ex)
                {
                    return (
                        Array.Empty<byte>(),
                        $"Placeholder replacement resulted in invalid XML: {ex.Message}"
                    );
                }

                // Delete the old entry and create a new one
                contentEntry.Delete();
                var newContentEntry = archive.CreateEntry("content.xml");

                using var newContentStream = newContentEntry.Open();
                using var writer = new StreamWriter(newContentStream, Encoding.UTF8);
                writer.Write(modifiedXml);
            }

            // Close archive to finalize changes
            archive.Dispose();

            return (outputStream.ToArray(), null);
        }
        catch (Exception ex)
        {
            return (Array.Empty<byte>(), $"Error processing ODS document: {ex.Message}");
        }
    }
}
