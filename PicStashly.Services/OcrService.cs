using Microsoft.Extensions.Options;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;
using SixLabors.ImageSharp.Formats.Png;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace PicStashly.Services;

/// <summary>
/// Extracts text from images using PaddleOCR (PP-OCRv5) via the Sdcb.PaddleOCR wrapper. Runs fully
/// in-process — no external CLI — using bundled local models, so it works offline and in Docker with
/// no model download at runtime. Replaced the previous tesseract CLI integration, which struggled
/// with meme/screenshot text. Degrades gracefully (returns null) when the native stack can't load.
/// </summary>
public sealed class OcrService : IOcrService, IDisposable
{
    private readonly OcrOptions options;
    private readonly ILogger<OcrService> logger;

    // The queued engine is thread-safe and expensive to build (loads native libs + model), so it's
    // created once, lazily, and shared. A null engine after init means the native stack failed to load.
    private readonly object initLock = new();

    private bool initialized;
    private QueuedPaddleOcrAll? engine;

    public OcrService(IOptions<OcrOptions> ocrOptions, ILogger<OcrService> logger)
    {
        options = ocrOptions.Value;
        this.logger = logger;
    }

    public async Task<string?> ExtractTextAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            return null;
        }

        var ocr = EnsureEngine();
        if (ocr is null)
        {
            return null;
        }

        try
        {
            // Re-encode through ImageSharp to PNG bytes first: it handles every format we ingest
            // (incl. webp/gif) consistently, sidestepping gaps in OpenCV's built-in image codecs.
            byte[] pngBytes;
            using (var image = await ImageSharpImage.LoadAsync(fullPath, cancellationToken))
            using (var buffer = new MemoryStream())
            {
                await image.SaveAsync(buffer, new PngEncoder(), cancellationToken);
                pngBytes = buffer.ToArray();
            }

            using var src = Cv2.ImDecode(pngBytes, ImreadModes.Color);
            if (src.Empty())
            {
                return null;
            }

            var result = await ocr.Run(src);
            string text = result.Text?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(ex, "OCR failed for {Path}", fullPath);
            }

            return null;
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(options.Enabled && EnsureEngine() is not null);

    private QueuedPaddleOcrAll? EnsureEngine()
    {
        if (initialized)
        {
            return engine;
        }

        lock (initLock)
        {
            if (initialized)
            {
                return engine;
            }

            try
            {
                engine = BuildEngine();
            }
            catch (Exception ex)
            {
                engine = null;

                if (logger.IsEnabled(LogLevel.Error))
                {
                    logger.LogError(ex,
                        "Failed to initialise PaddleOCR. Text extraction will be skipped. This usually " +
                        "means a native dependency couldn't load (e.g. missing Visual C++ Redistributable " +
                        "on Windows, or libgomp1 on Linux).");
                }
            }

            initialized = true;
            return engine;
        }
    }

    private QueuedPaddleOcrAll BuildEngine()
    {
        var model = LocalFullModels.ChineseV5; // PP-OCRv5 multilingual (incl. English)
        var device = ResolveDevice(options.Device);
        int? maxSize = options.DetectionMaxSize > 0 ? options.DetectionMaxSize : null;

        return new QueuedPaddleOcrAll(
            () =>
            {
                var all = new PaddleOcrAll(model, device)
                {
                    AllowRotateDetection = options.AllowRotateDetection,
                    Enable180Classification = options.Enable180Classification,
                };
                all.Detector.MaxSize = maxSize;
                return all;
            },
            consumerCount: 1);
    }

    private static Action<PaddleConfig> ResolveDevice(string device) => device?.Trim().ToLowerInvariant() switch
    {
        "mkldnn" => PaddleDevice.Mkldnn(),
        "openblas" or "blas" => PaddleDevice.Blas(),
        _ => PaddleDevice.Onnx(),
    };

    public void Dispose() => engine?.Dispose();
}