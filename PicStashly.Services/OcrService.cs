using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace PicStashly.Services;

/// <summary>
/// Extracts text from images by shelling out to the tesseract CLI. Chosen over native bindings
/// because it works identically on Windows and Linux with a plain <c>tesseract</c> install and
/// has no native-library versioning headaches. Degrades gracefully when tesseract is missing.
/// </summary>
public class OcrService(IOptions<OcrOptions> ocrOptions, ILogger<OcrService> logger) : IOcrService
{
    private bool? isTesseractAvailable;

    public async Task<string?> ExtractTextAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        var opts = ocrOptions.Value;

        if (!opts.Enabled || !await IsAvailableAsync(cancellationToken))
        {
            return null;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = opts.TesseractPath,
                    ArgumentList = { fullPath, "stdout", "-l", opts.Languages },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(opts.TimeoutSeconds));

            string output = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);

            if (process.ExitCode != 0)
            {
                return null;
            }

            string text = output.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || cancellationToken.IsCancellationRequested == false)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(ex, "OCR failed for {Path}", fullPath);
            }

            return null;
        }
    }

    private async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        if (isTesseractAvailable.HasValue)
        {
            return isTesseractAvailable.Value;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ocrOptions.Value.TesseractPath,
                    ArgumentList = { "--version" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);
            isTesseractAvailable = process.ExitCode == 0;
        }
        catch
        {
            isTesseractAvailable = false;
        }

        if (isTesseractAvailable == false && logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "Tesseract not found at '{Path}'. OCR is skipped — install tesseract or set Ocr:TesseractPath.",
                ocrOptions.Value.TesseractPath);
        }

        return isTesseractAvailable.Value;
    }
}