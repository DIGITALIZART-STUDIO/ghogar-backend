namespace GestionHogar.Services;

public class SofficeConverterService(ILogger<SofficeConverterService> logger)
{
    // writes to a temp file, invokes soffice on it, returns the
    // converted bytes, and cleans up
    public (byte[], string?) ConvertToPdf(byte[] inputBytes, string inputExtension)
    {
        var unixms = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var tempDir = Path.Combine(Path.GetTempPath(), "gen_files");
        Directory.CreateDirectory(tempDir);

        var tempFilePath = Path.Combine(tempDir, $"file_{unixms}.{inputExtension}");
        var pdfFilePath = Path.Combine(tempDir, $"file_{unixms}.pdf");
        try
        {
            File.WriteAllBytes(tempFilePath, inputBytes);

            // Call LibreOffice to convert to PDF
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "soffice",
                    Arguments =
                        $"--headless --convert-to \"pdf:calc_pdf_Export:PageSize=1:ColumnScaling=100\" --outdir \"{tempDir}\" \"{tempFilePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                logger.LogError(process.ExitCode, "Error generating PDF");
                logger.LogError(process.StandardError.ReadToEnd(), "Error generating PDF (stderr)");
                var error = process.StandardError.ReadToEnd();
                return ([], $"Error generating PDF: {error}");
            }

            // read pdf file
            var pdfBytes = File.ReadAllBytes(pdfFilePath);

            return (pdfBytes, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception during conversion");
            return ([], $"Exception during conversion: {ex.Message}");
        }
        finally
        {
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
            if (File.Exists(pdfFilePath))
                File.Delete(pdfFilePath);
        }
    }

    public (byte[], string?) convertTo(
        byte[] inputBytes,
        string inputExtension,
        string outputExtension = "pdf"
    )
    {
        var unixms = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var tempDir = Path.Combine(Path.GetTempPath(), "gen_files");
        Directory.CreateDirectory(tempDir);

        var tempFilePath = Path.Combine(tempDir, $"file_{unixms}.{inputExtension}");
        var outputFilePath = Path.Combine(tempDir, $"file_{unixms}.{outputExtension}");
        try
        {
            System.IO.File.WriteAllBytes(tempFilePath, inputBytes);

            // Call LibreOffice to convert to whatever format
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "soffice",
                    Arguments =
                        $"--headless --convert-to {outputExtension} --outdir \"{tempDir}\" \"{tempFilePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                return ([], $"Error converting to {outputExtension}: {error}");
            }

            // Read the converted file
            var outputBytes = System.IO.File.ReadAllBytes(outputFilePath);

            return (outputBytes, null);
        }
        catch (Exception ex)
        {
            return ([], $"Exception during conversion: {ex.Message}");
        }
        finally
        {
            // Clean up temp files
            if (System.IO.File.Exists(tempFilePath))
                System.IO.File.Delete(tempFilePath);
            if (System.IO.File.Exists(outputFilePath))
                System.IO.File.Delete(outputFilePath);
        }
    }
}
