namespace PicStashly.Services;

public class OcrOptions
{
    /// <summary>When false, ingest skips OCR entirely (text can still be added/edited manually).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// PaddleOCR inference backend:
    /// <list type="bullet">
    /// <item>"Mkldnn" (oneDNN — the default; fast and accurate with the bundled PP-OCRv5 models).</item>
    /// <item>"Openblas" (smaller/slower — use on old CPUs that lack AVX).</item>
    /// <item>"Onnx" (onnxruntime — lower memory, BUT Paddle2ONNX cannot convert the PP-OCRv5
    /// detection model, so it produces empty/garbled results with the bundled models; only useful
    /// with older V3/V4 models). Not recommended.</item>
    /// </list>
    /// Memory is kept in check by <see cref="DetectionMaxSize"/> regardless of backend, so Mkldnn
    /// does not trigger the PaddleOCR 3.x memory blow-up here.
    /// </summary>
    public string Device { get; set; } = "Mkldnn";

    /// <summary>
    /// Caps the long side of the image fed into the text-detection step (PaddleOCR default: 960).
    /// This is the exact mitigation for the PaddleOCR 3.x CPU memory regression (issue #17955):
    /// large inputs are scaled down before detection so RAM stays bounded. Set to 0 (or negative)
    /// to disable the cap for maximum accuracy at the cost of much higher memory on big images.
    /// </summary>
    public int DetectionMaxSize { get; set; } = 960;

    /// <summary>
    /// Detect rotated (angled) text. Off by default: memes/screenshots are virtually always upright,
    /// and enabling it makes the detector wrap wide horizontal lines as 90°-rotated strips, which the
    /// recognizer then fails to read. Only turn this on if your images genuinely contain angled text.
    /// </summary>
    public bool AllowRotateDetection { get; set; } = false;

    /// <summary>Detect text rotated 180°. Off by default (most text is upright); enabling it costs ~10%.</summary>
    public bool Enable180Classification { get; set; } = false;
}