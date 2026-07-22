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
    private string? resolvedExecutablePath;

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
                    FileName = resolvedExecutablePath,
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

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (isTesseractAvailable.HasValue)
        {
            return isTesseractAvailable.Value;
        }

        resolvedExecutablePath = ResolveExecutablePath(ocrOptions.Value.TesseractPath);

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = resolvedExecutablePath,
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
        catch (Exception ex)
        {
            isTesseractAvailable = false;

            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    ex,
                    "Tesseract not found at '{Path}' (resolved to '{ResolvedPath}'). OCR is skipped — " +
                    "install tesseract, or set Ocr:TesseractPath to the tesseract executable itself " +
                    "(not just its folder). Restart the app after changing PATH — running processes " +
                    "don't pick up PATH changes made after they started.",
                    ocrOptions.Value.TesseractPath,
                    resolvedExecutablePath);
            }

            return false;
        }

        if (isTesseractAvailable == false && logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "Tesseract at '{ResolvedPath}' exited with a non-zero code on --version. OCR is skipped.",
                resolvedExecutablePath);
        }

        return isTesseractAvailable.Value;
    }

    /// <summary>
    /// Accepts either a bare command ("tesseract", resolved via PATH), a full path to the
    /// executable, or — the easy mistake — the install *folder* (e.g. what you'd copy from
    /// "where tesseract" minus the filename, or the default Windows install directory). In the
    /// last case, appends the platform-appropriate executable name so it still works.
    /// </summary>
    private static string ResolveExecutablePath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return "tesseract";
        }

        if (!Directory.Exists(configuredPath))
        {
            return configuredPath;
        }

        string exeName = OperatingSystem.IsWindows() ? "tesseract.exe" : "tesseract";
        return Path.Combine(configuredPath, exeName);
    }
}