namespace ToolingExtractor.Core;

/// <summary>AsyncLocal job context for visualizer updates from extractors.</summary>
public sealed class ExtractionVisualizerScope : IDisposable
{
    private static readonly AsyncLocal<ScopeData?> Current = new();

    public static int? JobId => Current.Value?.JobId;
    public static string? FilePath => Current.Value?.FilePath;

    public static IDisposable Begin(int jobId, string filePath)
    {
        var prior = Current.Value;
        Current.Value = new ScopeData(jobId, filePath, prior);
        return new ExtractionVisualizerScope();
    }

    public void Dispose()
    {
        if (Current.Value != null)
            Current.Value = Current.Value.Parent;
    }

    private sealed class ScopeData(int jobId, string filePath, ScopeData? parent)
    {
        public int JobId { get; } = jobId;
        public string FilePath { get; } = filePath;
        public ScopeData? Parent { get; } = parent;
    }
}
