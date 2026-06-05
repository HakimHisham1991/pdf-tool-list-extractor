using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ToolingExtractor.Core;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Application.Services;

public class ExtractionVisualizerStore : IExtractionVisualizerNotifier
{
    private static readonly Regex ToolNoRegex = new(@"\bT(\d{2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ConcurrentDictionary<int, InternalState> _jobs = new();

    public ExtractionVisualizerSnapshot? GetSnapshot(int jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return null;

        lock (state.Lock)
        {
            var file = GetActiveFile(state);
            if (file == null)
                return null;

            return CloneSnapshot(file.Snapshot);
        }
    }

    public ExtractionHighlights? GetHighlightsForPage(int jobId, int pageNumber, string? filePath = null)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return null;

        lock (state.Lock)
        {
            var file = ResolveFile(state, filePath);
            if (file == null || !file.PageHighlights.TryGetValue(pageNumber, out var page))
                return null;

            return new ExtractionHighlights
            {
                PageNumber = pageNumber,
                PageCount = Math.Max(1, file.Snapshot.PageCount),
                ImageWidth = page.ImageWidth,
                ImageHeight = page.ImageHeight,
                Boxes = page.Boxes.Select(CloneBox).ToList()
            };
        }
    }

    public IReadOnlyList<ExtractionHighlights> ExportAllPageHighlights(int jobId, string filePath)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return Array.Empty<ExtractionHighlights>();

        lock (state.Lock)
        {
            var file = ResolveFile(state, filePath);
            if (file == null || file.PageHighlights.Count == 0)
                return Array.Empty<ExtractionHighlights>();

            var pageCount = Math.Max(1, file.Snapshot.PageCount);
            return file.PageHighlights
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new ExtractionHighlights
                {
                    PageNumber = kvp.Key,
                    PageCount = pageCount,
                    ImageWidth = kvp.Value.ImageWidth,
                    ImageHeight = kvp.Value.ImageHeight,
                    Boxes = kvp.Value.Boxes.Select(CloneBox).ToList()
                })
                .ToList();
        }
    }

    public string? GetPreviewFilePath(int jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return null;
        lock (state.Lock)
            return string.IsNullOrEmpty(state.ActiveFilePath) ? null : state.ActiveFilePath;
    }

    public void BeginFile(int jobId, string filePath, string displayName)
    {
        var key = NormalizePath(filePath);
        var state = _jobs.GetOrAdd(jobId, _ => new InternalState());
        lock (state.Lock)
        {
            state.ActiveFilePath = key;
            var file = GetOrCreateFile(state, key, displayName);
            file.Snapshot = new ExtractionVisualizerSnapshot
            {
                JobId = jobId,
                FileName = displayName,
                Stage = "open",
                Message = "Opening PDF…",
                UpdatedAtUtc = DateTime.UtcNow
            };
        }
    }

    public void SetStage(
        int jobId,
        string stage,
        string message,
        int pageIndex,
        int pageCount,
        int imageWidth,
        int imageHeight,
        IReadOnlyList<VisualizerHighlight>? highlights = null,
        int activeHighlightIndex = -1)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return;

        lock (state.Lock)
        {
            var file = GetActiveFile(state);
            if (file == null)
                return;

            var prev = file.Snapshot;
            file.Snapshot.Stage = stage;
            file.Snapshot.Message = message;
            file.Snapshot.PageIndex = pageIndex >= 0 ? pageIndex : prev.PageIndex;
            file.Snapshot.PageCount = pageCount > 0 ? pageCount : prev.PageCount;
            file.Snapshot.ImageWidth = imageWidth > 0 ? imageWidth : prev.ImageWidth;
            file.Snapshot.ImageHeight = imageHeight > 0 ? imageHeight : prev.ImageHeight;
            file.Snapshot.ActiveHighlightIndex = activeHighlightIndex;
            file.Snapshot.Highlights = highlights is { Count: > 0 }
                ? highlights.ToList()
                : prev.Highlights;
            file.Snapshot.HasPreview = file.Snapshot.ImageWidth > 0 && file.Snapshot.ImageHeight > 0;
            file.Snapshot.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public void AddPageHighlights(int jobId, int pageNumber, IReadOnlyList<HighlightBox> boxes)
    {
        if (boxes.Count == 0 || !_jobs.TryGetValue(jobId, out var state))
            return;

        var filePath = ExtractionVisualizerScope.FilePath;
        if (string.IsNullOrEmpty(filePath))
            return;

        var key = NormalizePath(filePath);
        lock (state.Lock)
        {
            var file = GetOrCreateFile(state, key, Path.GetFileName(filePath));
            var page = file.PageHighlights.GetValueOrDefault(pageNumber);
            if (page == null)
            {
                page = new PageHighlightState
                {
                    ImageWidth = file.Snapshot.ImageWidth,
                    ImageHeight = file.Snapshot.ImageHeight
                };
                file.PageHighlights[pageNumber] = page;
            }

            if (file.Snapshot.ImageWidth > 0)
                page.ImageWidth = file.Snapshot.ImageWidth;
            if (file.Snapshot.ImageHeight > 0)
                page.ImageHeight = file.Snapshot.ImageHeight;

            foreach (var box in boxes)
                page.Boxes.Add(CloneBox(box));

            SyncLegacyHighlights(file, pageNumber);
        }
    }

    public void MarkExtractedHighlights(int jobId, IReadOnlyCollection<string> toolNumbers)
    {
        if (toolNumbers.Count == 0 || !_jobs.TryGetValue(jobId, out var state))
            return;

        var filePath = ExtractionVisualizerScope.FilePath ?? state.ActiveFilePath;
        if (string.IsNullOrEmpty(filePath))
            return;

        var normalized = new HashSet<string>(
            toolNumbers.Select(NormalizeToolNo).Where(t => t.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        lock (state.Lock)
        {
            var file = ResolveFile(state, filePath);
            if (file == null)
                return;

            foreach (var (pageNumber, page) in file.PageHighlights)
            {
                var added = new List<HighlightBox>();
                foreach (var box in page.Boxes)
                {
                    if (box.Type is not ("ocr" or "lowconf"))
                        continue;

                    var toolNo = ExtractToolNo(box.Label);
                    if (toolNo == null || !normalized.Contains(toolNo))
                        continue;

                    if (page.Boxes.Any(b =>
                            b.Type == "extracted" &&
                            Math.Abs(b.X - box.X) < 0.001 &&
                            Math.Abs(b.Y - box.Y) < 0.001))
                        continue;

                    added.Add(new HighlightBox
                    {
                        X = box.X,
                        Y = box.Y,
                        Width = box.Width,
                        Height = box.Height,
                        Type = "extracted",
                        Confidence = box.Confidence,
                        Label = box.Label
                    });
                }

                if (added.Count > 0)
                {
                    page.Boxes.AddRange(added);
                    SyncLegacyHighlights(file, pageNumber);
                }
            }
        }
    }

    public void EndFile(int jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return;
        lock (state.Lock)
        {
            var file = GetActiveFile(state);
            if (file == null)
                return;

            file.Snapshot.Message = "File complete";
            file.Snapshot.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public void ClearJob(int jobId) => _jobs.TryRemove(jobId, out _);

    private static FileHighlightState? GetActiveFile(InternalState state)
    {
        if (string.IsNullOrEmpty(state.ActiveFilePath))
            return null;
        return state.Files.GetValueOrDefault(state.ActiveFilePath);
    }

    private static FileHighlightState? ResolveFile(InternalState state, string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            var key = NormalizePath(filePath);
            return state.Files.GetValueOrDefault(key);
        }

        return GetActiveFile(state);
    }

    private static FileHighlightState GetOrCreateFile(InternalState state, string key, string displayName)
    {
        if (!state.Files.TryGetValue(key, out var file))
        {
            file = new FileHighlightState { DisplayName = displayName };
            state.Files[key] = file;
        }

        if (!string.IsNullOrWhiteSpace(displayName))
            file.DisplayName = displayName;

        return file;
    }

    private static void SyncLegacyHighlights(FileHighlightState file, int pageNumber)
    {
        if (file.Snapshot.PageIndex + 1 != pageNumber)
            return;

        if (!file.PageHighlights.TryGetValue(pageNumber, out var page))
            return;

        file.Snapshot.Highlights = page.Boxes
            .Select(b => new VisualizerHighlight
            {
                X = b.X,
                Y = b.Y,
                W = b.Width,
                H = b.Height,
                Label = b.Label,
                Kind = b.Type
            })
            .Take(120)
            .ToList();
    }

    private static ExtractionVisualizerSnapshot CloneSnapshot(ExtractionVisualizerSnapshot s) => new()
    {
        JobId = s.JobId,
        FileName = s.FileName,
        Stage = s.Stage,
        Message = s.Message,
        PageIndex = s.PageIndex,
        PageCount = s.PageCount,
        ImageWidth = s.ImageWidth,
        ImageHeight = s.ImageHeight,
        ActiveHighlightIndex = s.ActiveHighlightIndex,
        Highlights = s.Highlights.Select(h => new VisualizerHighlight
        {
            X = h.X, Y = h.Y, W = h.W, H = h.H, Label = h.Label, Kind = h.Kind
        }).ToList(),
        UpdatedAtUtc = s.UpdatedAtUtc,
        HasPreview = s.HasPreview
    };

    private static string NormalizePath(string filePath) =>
        Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static HighlightBox CloneBox(HighlightBox b) => new()
    {
        X = b.X,
        Y = b.Y,
        Width = b.Width,
        Height = b.Height,
        Type = b.Type,
        Confidence = b.Confidence,
        Label = b.Label
    };

    private static string NormalizeToolNo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var m = ToolNoRegex.Match(value.Trim());
        return m.Success ? m.Value.ToUpperInvariant() : string.Empty;
    }

    private static string? ExtractToolNo(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;
        var m = ToolNoRegex.Match(label);
        return m.Success ? m.Value.ToUpperInvariant() : null;
    }

    private sealed class InternalState
    {
        public object Lock { get; } = new();
        public string ActiveFilePath { get; set; } = string.Empty;
        public Dictionary<string, FileHighlightState> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FileHighlightState
    {
        public string DisplayName { get; set; } = string.Empty;
        public ExtractionVisualizerSnapshot Snapshot { get; set; } = new();
        public Dictionary<int, PageHighlightState> PageHighlights { get; } = new();
    }

    private sealed class PageHighlightState
    {
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public List<HighlightBox> Boxes { get; } = new();
    }
}
