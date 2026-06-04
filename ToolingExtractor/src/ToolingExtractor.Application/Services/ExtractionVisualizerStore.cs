using System.Collections.Concurrent;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Application.Services;

public class ExtractionVisualizerStore : IExtractionVisualizerNotifier
{
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

    private sealed class InternalState
    {
        public object Lock { get; } = new();
        public string FilePath { get; set; } = string.Empty;
        public ExtractionVisualizerSnapshot Snapshot { get; set; } = new();
    }
}
