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
        var profileDir = Path.Combine(tempDir, $"lo_profile_{unixms}");
        try
        {
            System.IO.File.WriteAllBytes(tempFilePath, inputBytes);

            // Writer/Web (HTML) has no default "docx" filter; the OOXML filter must be named.
            var convertTarget = outputExtension.Equals("docx", StringComparison.OrdinalIgnoreCase)
                ? "docx:MS Word 2007 XML"
                : outputExtension;
            Directory.CreateDirectory(profileDir);
            var profileUri = new Uri(profileDir).AbsoluteUri;

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "soffice",
                    Arguments =
                        $"--headless --norestore --nolockcheck -env:UserInstallation={profileUri} --convert-to \"{convertTarget}\" --outdir \"{tempDir}\" \"{tempFilePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            if (!process.WaitForExit(120_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignored
                }
                return ([], $"Timeout converting to {outputExtension}");
            }

            var stderr = process.StandardError.ReadToEnd();
            if (process.ExitCode != 0)
            {
                logger.LogError(
                    "LibreOffice convert failed. Exit={ExitCode} Stderr={Stderr}",
                    process.ExitCode,
                    stderr
                );
                return ([], $"Error converting to {outputExtension}: {stderr}");
            }

            // Read the converted file (LibreOffice keeps the input base name)
            if (!System.IO.File.Exists(outputFilePath))
            {
                var inputBaseName = Path.GetFileNameWithoutExtension(tempFilePath);
                var fallback = Directory
                    .GetFiles(tempDir, $"{inputBaseName}*.{outputExtension}")
                    .OrderByDescending(System.IO.File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (fallback == null)
                    return ([], $"Converted {outputExtension} file was not generated");

                outputFilePath = fallback;
            }

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
            if (Directory.Exists(profileDir))
                Directory.Delete(profileDir, recursive: true);
        }
    }
}
