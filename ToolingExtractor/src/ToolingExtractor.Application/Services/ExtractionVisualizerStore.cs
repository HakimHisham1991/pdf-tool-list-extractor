using System.Collections.Concurrent;
using System.Text.RegularExpressions;
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
            var s = state.Snapshot;
            return new ExtractionVisualizerSnapshot
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
        }
    }

    public ExtractionHighlights? GetHighlightsForPage(int jobId, int pageNumber)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return null;

        lock (state.Lock)
        {
            if (!state.PageHighlights.TryGetValue(pageNumber, out var page))
                return null;

            return new ExtractionHighlights
            {
                PageNumber = pageNumber,
                PageCount = state.Snapshot.PageCount,
                ImageWidth = page.ImageWidth,
                ImageHeight = page.ImageHeight,
                Boxes = page.Boxes.Select(CloneBox).ToList()
            };
        }
    }

    public string? GetPreviewFilePath(int jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return null;
        lock (state.Lock)
            return state.FilePath;
    }

    public void BeginFile(int jobId, string filePath, string displayName)
    {
        var state = _jobs.GetOrAdd(jobId, _ => new InternalState());
        lock (state.Lock)
        {
            state.FilePath = filePath;
            state.PageHighlights.Clear();
            state.Snapshot = new ExtractionVisualizerSnapshot
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
            var prev = state.Snapshot;
            state.Snapshot.Stage = stage;
            state.Snapshot.Message = message;
            state.Snapshot.PageIndex = pageIndex >= 0 ? pageIndex : prev.PageIndex;
            state.Snapshot.PageCount = pageCount > 0 ? pageCount : prev.PageCount;
            state.Snapshot.ImageWidth = imageWidth > 0 ? imageWidth : prev.ImageWidth;
            state.Snapshot.ImageHeight = imageHeight > 0 ? imageHeight : prev.ImageHeight;
            state.Snapshot.ActiveHighlightIndex = activeHighlightIndex;
            state.Snapshot.Highlights = highlights is { Count: > 0 }
                ? highlights.ToList()
                : prev.Highlights;
            state.Snapshot.HasPreview = state.Snapshot.ImageWidth > 0 && state.Snapshot.ImageHeight > 0;
            state.Snapshot.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public void AddPageHighlights(int jobId, int pageNumber, IReadOnlyList<HighlightBox> boxes)
    {
        if (boxes.Count == 0 || !_jobs.TryGetValue(jobId, out var state))
            return;

        lock (state.Lock)
        {
            var page = state.PageHighlights.GetValueOrDefault(pageNumber);
            if (page == null)
            {
                page = new PageHighlightState
                {
                    ImageWidth = state.Snapshot.ImageWidth,
                    ImageHeight = state.Snapshot.ImageHeight
                };
                state.PageHighlights[pageNumber] = page;
            }

            if (state.Snapshot.ImageWidth > 0)
                page.ImageWidth = state.Snapshot.ImageWidth;
            if (state.Snapshot.ImageHeight > 0)
                page.ImageHeight = state.Snapshot.ImageHeight;

            foreach (var box in boxes)
                page.Boxes.Add(CloneBox(box));

            SyncLegacyHighlights(state, pageNumber);
        }
    }

    public void MarkExtractedHighlights(int jobId, IReadOnlyCollection<string> toolNumbers)
    {
        if (toolNumbers.Count == 0 || !_jobs.TryGetValue(jobId, out var state))
            return;

        var normalized = new HashSet<string>(
            toolNumbers.Select(NormalizeToolNo).Where(t => t.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        lock (state.Lock)
        {
            foreach (var (pageNumber, page) in state.PageHighlights)
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
                    SyncLegacyHighlights(state, pageNumber);
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
            state.Snapshot.Message = "File complete";
            state.Snapshot.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public void ClearJob(int jobId) => _jobs.TryRemove(jobId, out _);

    private static void SyncLegacyHighlights(InternalState state, int pageNumber)
    {
        if (state.Snapshot.PageIndex + 1 != pageNumber)
            return;

        if (!state.PageHighlights.TryGetValue(pageNumber, out var page))
            return;

        state.Snapshot.Highlights = page.Boxes
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
        public string FilePath { get; set; } = string.Empty;
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
