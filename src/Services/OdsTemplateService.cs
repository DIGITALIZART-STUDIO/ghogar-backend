using System.IO.Compression;
using System.Text;
using System.Xml;

namespace GestionHogar.Services;

public class OdsTemplateService(ILogger<OdsTemplateService> logger)
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

    /// <summary>
    /// Replaces placeholders in ODS template with support for dynamic row generation
    /// </summary>
    public (byte[], string?) ReplacePlaceholdersWithDynamicRows(
        byte[] odsBytes,
        Dictionary<string, string> staticPlaceholders,
        List<Dictionary<string, string>> dynamicRowsData,
        int templateRowNumber
    )
    {
        try
        {
            using var outputStream = new MemoryStream();

            // Copy the original ODS to output stream
            outputStream.Write(odsBytes, 0, odsBytes.Length);
            outputStream.Position = 0;

            // Open as ZIP archive for modification
            using var archive = new ZipArchive(
                outputStream,
                ZipArchiveMode.Update,
                leaveOpen: true
            );

            // Find the content.xml file
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

            // Load as XML document for easier manipulation
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(contentXml);

            // Find the template row (should contain the dynamic placeholders)
            var rows = xmlDoc.GetElementsByTagName("table:table-row");
            XmlNode? templateRow = null;

            // Find row by index (templateRowNumber is 1-based)
            if (templateRowNumber > 0 && templateRowNumber <= rows.Count)
            {
                templateRow = rows[templateRowNumber - 1];
            }

            if (templateRow == null)
            {
                return (Array.Empty<byte>(), $"Template row {templateRowNumber} not found");
            }

            // Get the parent table
            var parentTable = templateRow.ParentNode;
            if (parentTable == null)
            {
                return (Array.Empty<byte>(), "Could not find parent table for template row");
            }

            // Create new rows based on dynamic data
            var newRows = new List<XmlNode>();
            foreach (var rowData in dynamicRowsData)
            {
                // Clone the template row
                var newRow = templateRow.CloneNode(true);

                // Replace placeholders in the new row
                foreach (var placeholder in rowData)
                {
                    ReplaceInXmlNode(newRow, placeholder.Key, placeholder.Value);
                }

                newRows.Add(newRow);
            }

            // Remove the original template row
            parentTable.RemoveChild(templateRow);

            // Insert the new rows at the same position
            XmlNode? nextSibling = null;
            if (templateRowNumber < rows.Count)
            {
                nextSibling = rows[templateRowNumber]; // Next row after template
            }

            foreach (var newRow in newRows)
            {
                if (nextSibling != null)
                {
                    parentTable.InsertBefore(newRow, nextSibling);
                }
                else
                {
                    parentTable.AppendChild(newRow);
                }
            }

            // Replace static placeholders in the entire document
            string modifiedXml = xmlDoc.OuterXml;
            foreach (var placeholder in staticPlaceholders)
            {
                modifiedXml = modifiedXml.Replace(placeholder.Key, placeholder.Value);
            }

            // Validate the modified XML
            try
            {
                var validationDoc = new XmlDocument();
                validationDoc.LoadXml(modifiedXml);
            }
            catch (XmlException ex)
            {
                return (Array.Empty<byte>(), $"Generated XML is invalid: {ex.Message}");
            }

            // Update the content.xml in the archive
            contentEntry.Delete();
            var newContentEntry = archive.CreateEntry("content.xml");

            // Write the modified XML to the new entry
            using (var newContentStream = newContentEntry.Open())
            {
                var xmlBytes = Encoding.UTF8.GetBytes(modifiedXml);
                newContentStream.Write(xmlBytes, 0, xmlBytes.Length);
            }

            // Close the archive to finalize changes but keep the stream open
            archive.Dispose();

            // Now get the bytes from the output stream
            var resultBytes = outputStream.ToArray();
            return (resultBytes, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing ODS document with dynamic rows");
            return (
                Array.Empty<byte>(),
                $"Error processing ODS document with dynamic rows: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Helper method to replace text in XML nodes recursively
    /// </summary>
    private void ReplaceInXmlNode(XmlNode node, string placeholder, string value)
    {
        if (node.NodeType == XmlNodeType.Text)
        {
            if (node.Value != null && node.Value.Contains(placeholder))
            {
                node.Value = node.Value.Replace(placeholder, value);
            }
        }
        else
        {
            // Check attributes
            if (node.Attributes != null)
            {
                foreach (XmlAttribute attr in node.Attributes)
                {
                    if (attr.Value.Contains(placeholder))
                    {
                        attr.Value = attr.Value.Replace(placeholder, value);
                    }
                }
            }

            // Recursively process child nodes
            foreach (XmlNode child in node.ChildNodes)
            {
                ReplaceInXmlNode(child, placeholder, value);
            }
        }
    }
}
