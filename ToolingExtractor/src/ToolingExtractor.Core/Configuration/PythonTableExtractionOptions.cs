namespace ToolingExtractor.Core.Configuration;

public class PythonTableExtractionOptions
{
    public const string SectionName = "PythonTableExtraction";

    public bool Enabled { get; set; } = true;
    public string PythonExecutable { get; set; } = "python";
    public int TimeoutSeconds { get; set; } = 180;
    /// <summary>Minimum valid T## rows required to accept Python output over text parsing.</summary>
    public int MinimumToolRows { get; set; } = 3;
}
