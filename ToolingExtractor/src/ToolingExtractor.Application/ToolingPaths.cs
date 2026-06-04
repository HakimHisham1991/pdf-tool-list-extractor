using ToolingExtractor.Core.Configuration;

namespace ToolingExtractor.Application;

/// <summary>
/// Resolves content paths so the app works when started from ToolingExtractor.Web or the solution root.
/// </summary>
public static class ToolingPaths
{
    public static void Normalize(ToolingExtractorOptions options)
    {
        options.PaddleOcrModelPath = ResolveModelsRoot(options.PaddleOcrModelPath);
        options.FailedExtractionOutputPath = ResolveWritableContentPath(options.FailedExtractionOutputPath);
        options.AmendedPdfLogPath = ResolveWritableContentPath(options.AmendedPdfLogPath);
        options.UploadStagingPath = ResolveWritableContentPath(options.UploadStagingPath);
    }

    public static string ResolveModelsRoot(string configuredPath)
    {
        return ResolveExistingModelsRoot(configuredPath) ?? Path.GetFullPath(configuredPath);
    }

    public static string? ResolveExistingModelsRoot(string configuredPath)
    {
        foreach (var root in GetSearchRoots())
        {
            var candidate = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(Path.Combine(root, configuredPath));
            if (HasValidModelSet(candidate))
                return candidate;

            var solutionModels = Path.Combine(root, "models", "ppocr_v4");
            if (HasValidModelSet(solutionModels))
                return solutionModels;

            var parentModels = Path.GetFullPath(Path.Combine(root, "..", "models", "ppocr_v4"));
            if (HasValidModelSet(parentModels))
                return parentModels;
        }

        return null;
    }

    public static string ResolveContentPath(string configuredPath) =>
        ResolveWritableContentPath(configuredPath);

    /// <summary>Resolves a folder under the solution/web root and ensures it exists.</summary>
    public static string ResolveWritableContentPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            Directory.CreateDirectory(configuredPath);
            return configuredPath;
        }

        var trimmed = configuredPath.TrimStart('.', '/', '\\');
        foreach (var root in GetSearchRoots())
        {
            foreach (var candidate in new[]
                     {
                         Path.GetFullPath(Path.Combine(root, configuredPath)),
                         Path.GetFullPath(Path.Combine(root, "..", "..", trimmed)),
                         Path.GetFullPath(Path.Combine(root, "..", "..", "..", "..", trimmed)),
                     })
            {
                try
                {
                    Directory.CreateDirectory(candidate);
                    return candidate;
                }
                catch
                {
                    // try next candidate
                }
            }
        }

        var fallback = Path.GetFullPath(configuredPath);
        Directory.CreateDirectory(fallback);
        return fallback;
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
        return roots;
    }

    private static bool HasValidModelSet(string root)
    {
        if (!Directory.Exists(root)) return false;
        foreach (var sub in new[] { "det", "cls", "rec" })
        {
            var dir = Path.Combine(root, sub);
            var hasModel = File.Exists(Path.Combine(dir, "inference.pdmodel")) ||
                           File.Exists(Path.Combine(dir, "inference.json"));
            var hasWeights = File.Exists(Path.Combine(dir, "inference.pdiparams"));
            if (!hasModel || !hasWeights)
                return false;
        }
        return true;
    }
}
