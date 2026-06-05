using Sdcb.PaddleInference.Native;

namespace ToolingExtractor.Web;

/// <summary>
/// PaddleSharp resolves native DLLs from the app directory; NuGet places them under
/// runtimes/win-x64/native. Add that folder to PATH before the OCR singleton is created.
/// </summary>
internal static class PaddleNativeBootstrap
{
    public static void Configure()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // Paddle native (glog) emits deprecation noise on stderr; show errors only.
        Environment.SetEnvironmentVariable("GLOG_minloglevel", "2");

        foreach (var dir in GetCandidateDirectories())
            PrependPath(dir);

        PaddleInferenceLibLoader.Init();
    }

    private static IEnumerable<string> GetCandidateDirectories()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "runtimes", "win-x64", "native");
        yield return Path.Combine(baseDir, "dll", "x64");
        yield return baseDir;
    }

    private static void PrependPath(string dir)
    {
        if (!Directory.Exists(dir))
            return;

        var normalized = Path.GetFullPath(dir);
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var segments = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(p => string.Equals(Path.GetFullPath(p), normalized, StringComparison.OrdinalIgnoreCase)))
            return;

        Environment.SetEnvironmentVariable("PATH", normalized + Path.PathSeparator + path);
    }
}
