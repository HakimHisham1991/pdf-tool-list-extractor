using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolingExtractor.Core.Configuration;

namespace ToolingExtractor.Infrastructure.Parsing;

public class PythonTableExtractionBridge
{
    private readonly PythonTableExtractionOptions _options;
    private readonly ILogger<PythonTableExtractionBridge> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public PythonTableExtractionBridge(
        IOptions<ToolingExtractorOptions> options,
        ILogger<PythonTableExtractionBridge> logger)
    {
        _options = options.Value.PythonTableExtraction;
        _logger = logger;
    }

    public async Task<PythonTableExtractionPayload?> TryExtractAsync(
        string pdfPath,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return null;

        var pythonRoot = ResolvePythonRoot();
        if (pythonRoot == null)
        {
            _logger.LogDebug("Python table_extractor folder not found — skipping.");
            return null;
        }

        var pdfFull = Path.GetFullPath(pdfPath);
        if (!File.Exists(pdfFull))
            return null;

        var pythonExe = ResolvePythonExecutable();
        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = $"-m table_extractor --pdf \"{pdfFull}\"",
            WorkingDirectory = pythonRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUTF8"] = "1";

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
                return null;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(30, _options.TimeoutSeconds)));

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0 && process.ExitCode != 2)
            {
                _logger.LogWarning(
                    "Python table extraction exit {Code} for {File}. stderr: {Err}",
                    process.ExitCode, Path.GetFileName(pdfPath), Trim(stderr, 500));
                return null;
            }

            if (string.IsNullOrWhiteSpace(stdout))
                return null;

            var payload = JsonSerializer.Deserialize<PythonTableExtractionPayload>(stdout, JsonOptions);
            if (payload?.Cleaned == null || payload.Cleaned.Count == 0)
            {
                _logger.LogInformation(
                    "Python table extraction returned no cleaned rows for {File} (method: {Method})",
                    Path.GetFileName(pdfPath), payload?.Method ?? "none");
                return null;
            }

            _logger.LogInformation(
                "Python table extraction: {Count} rows via {Method} for {File}",
                payload.Cleaned.Count, payload.Method, Path.GetFileName(pdfPath));
            return payload;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Python table extraction timed out for {File}", Path.GetFileName(pdfPath));
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Python table extraction failed for {File}", Path.GetFileName(pdfPath));
            return null;
        }
    }

    private string ResolvePythonExecutable()
    {
        var configured = _options.PythonExecutable;
        if (!string.Equals(configured, "python", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(configured, "python3", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(configured))
            return configured;

        foreach (var root in GetSearchRoots())
        {
            var venvPython = Path.Combine(root, "python", ".venv", "Scripts", "python.exe");
            if (File.Exists(venvPython))
                return venvPython;
        }

        return configured;
    }

    private static string? ResolvePythonRoot()
    {
        foreach (var root in GetSearchRoots())
        {
            var candidate = Path.Combine(root, "python");
            var module = Path.Combine(candidate, "table_extractor", "pipeline.py");
            if (File.Exists(module))
                return candidate;
        }
        return null;
    }

    private static List<string> GetSearchRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<string>();
        void TryAdd(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return;
            try
            {
                var full = Path.GetFullPath(p);
                if (seen.Add(full))
                    roots.Add(full);
            }
            catch { /* ignore */ }
        }

        TryAdd(Directory.GetCurrentDirectory());
        TryAdd(AppContext.BaseDirectory);
        TryAdd(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        TryAdd(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        TryAdd(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        return roots;
    }

    private static string Trim(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}

public class PythonTableExtractionPayload
{
    public bool Success { get; set; }
    public string Method { get; set; } = string.Empty;
    public List<PythonToolTableRow> Raw { get; set; } = new();
    public List<PythonToolTableRow> Cleaned { get; set; } = new();
    public PythonValidationMeta? Validation { get; set; }
    public List<PythonPageHighlights> Highlights { get; set; } = new();
}

public class PythonPageHighlights
{
    public int PageNumber { get; set; }
    public List<PythonHighlightBox> Boxes { get; set; } = new();
}

public class PythonHighlightBox
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public string Type { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class PythonValidationMeta
{
    public int RowCountRaw { get; set; }
    public int RowCountCleaned { get; set; }
    public List<string> ToolNumbers { get; set; } = new();
}

public class PythonToolTableRow
{
    public string Tool_No { get; set; } = string.Empty;
    public string Tool_Name { get; set; } = string.Empty;
    public string Consumable_Tool_Description { get; set; } = string.Empty;
    public string Tool_Supplier { get; set; } = string.Empty;
    public string Tool_Identifier { get; set; } = string.Empty;
    public string Total_Diameter { get; set; } = string.Empty;
    public string Flute_Length { get; set; } = string.Empty;
    public string Total_Length { get; set; } = string.Empty;
    public string Total_Corner_Radius { get; set; } = string.Empty;
    public string Anchor_Description { get; set; } = string.Empty;
    public string Tool_Path_Time_In_Minutes { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
}
