namespace PicStashly.Services;

public class OcrOptions
{
    /// <summary>When false, ingest skips OCR entirely (text can still be added/edited manually).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Path to the tesseract executable; "tesseract" resolves from PATH.</summary>
    public string TesseractPath { get; set; } = "tesseract";

    /// <summary>Tesseract language(s), e.g. "eng" or "eng+deu".</summary>
    public string Languages { get; set; } = "eng";

    public int TimeoutSeconds { get; set; } = 30;
}