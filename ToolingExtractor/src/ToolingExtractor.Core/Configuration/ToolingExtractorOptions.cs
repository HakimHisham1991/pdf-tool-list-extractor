namespace ToolingExtractor.Core.Configuration;

public class ToolingExtractorOptions
{
    public const string SectionName = "ToolingExtractor";

    public string DefaultFolderPath { get; set; } = string.Empty;
    public int MaxParallelFiles { get; set; } = 4;
    public int NetworkPathThrottleParallelism { get; set; } = 2;
    public float OcrConfidenceThreshold { get; set; } = 0.6f;
    public string FailedExtractionOutputPath { get; set; } = "./failed_extractions";
    public string AmendedPdfLogPath { get; set; } = "./amended_pdf_log";
    /// <summary>Server folder for PDFs uploaded via the Import Files browser picker.</summary>
    public string UploadStagingPath { get; set; } = "./data/uploads";
    public long MaxUploadFileBytes { get; set; } = 200L * 1024 * 1024;
    public string PaddleOcrModelPath { get; set; } = "./models/ppocr_v4";
    public int PdfRenderDpi { get; set; } = 300;
    /// <summary>Cap longest page edge in pixels when rasterizing for OCR (prevents Skia OOM on large sheets).</summary>
    public int MaxPageRenderPixels { get; set; } = 4000;
    /// <summary>Per-PDF processing timeout (OCR on amended/scanned multi-page files can take several minutes).</summary>
    public int PerFileTimeoutSeconds { get; set; } = 600;
    public List<string> AllowedBasePaths { get; set; } = new();
    public AmendmentDetectionOptions AmendmentDetection { get; set; } = new();
    public ImagePreprocessingOptions ImagePreprocessing { get; set; } = new();
}

public class AmendmentDetectionOptions
{
    public bool Enabled { get; set; } = true;
    public double WhiteColorThreshold { get; set; } = 0.94;
    public double CoordProximityPoints { get; set; } = 5.0;
    public double AnnotationOverlapPoints { get; set; } = 8.0;
}

public class ImagePreprocessingOptions
{
    public double BrightnessThreshold { get; set; } = 0.85;
    public double MaxDeskewAngleDegrees { get; set; } = 15.0;
}
