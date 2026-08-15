using Grimlok.Configuration;
using Grimlok.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;

using System.IO;
using YoloDotNet;

namespace Grimlok.Services;

public interface IObjectDetector
{
    bool IsEnabled { get; }
    Task<IReadOnlyList<DetectedObject>> DetectAsync(
        ReadOnlyMemory<byte> jpeg,
        CancellationToken cancellationToken = default);
}

public sealed class YoloObjectDetector : IObjectDetector, IDisposable
{
    private readonly ObjectDetectionOptions _options;
    private readonly ILogger<YoloObjectDetector> _logger;
    private readonly Yolo? _yolo;
    private readonly HashSet<string> _humanLabels;
    private bool _disposed;

    public YoloObjectDetector(
        IOptions<GrimlokOptions> options,
        ILogger<YoloObjectDetector> logger)
    {
        _options = options.Value.ObjectDetection;
        _logger = logger;
        _humanLabels = (_options.HumanLabels ?? ["person"])
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (_options.Enabled)
        {
            var modelPath = Path.IsPathRooted(_options.ModelPath)
                ? _options.ModelPath
                : Path.Combine(AppContext.BaseDirectory, _options.ModelPath);

            if (File.Exists(modelPath))
            {
                try
                {
                    _yolo = new Yolo(modelPath, false, 0);
                    _logger.LogInformation("YOLO object detector loaded from {ModelPath}", modelPath);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Unable to initialize YOLO object detector");
                }
            }
            else
            {
                _logger.LogWarning(
                    "Object detection is enabled but the YOLO model was not found at {ModelPath}; continuing without YOLO",
                    modelPath);
                return;
            }
        }
    }

    public bool IsEnabled => _yolo is not null;

    public Task<IReadOnlyList<DetectedObject>> DetectAsync(
        ReadOnlyMemory<byte> jpeg,
        CancellationToken cancellationToken = default)
    {
        if (_yolo is null)
        {
            return Task.FromResult<IReadOnlyList<DetectedObject>>([]);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // YoloDotNet Changed Policies when its updated to a new version, the creators changed the rules: It no longer accepts-
        // raw byte Arrays, it strictly requires an ImageSharp.
        // Here's the work around:
        using var image = Image.Load(jpeg.ToArray()); //By using: >> using var image also gets properly disposed of from memory so your app doesn't eat RAM!!!
        var detections = _yolo.RunObjectDetection(image, _options.Confidence);

        IReadOnlyList<DetectedObject> result = [.. detections
            .Select(detection => new DetectedObject(
                detection.Label?.Name ?? "unknown",
                detection.Confidence,
                detection.BoundingBox.X,
                detection.BoundingBox.Y,
                detection.BoundingBox.Width,
                detection.BoundingBox.Height))
            .Where(detection => !_options.HumanConfirmedOnly ||
                                _humanLabels.Contains(detection.Label))];

        return Task.FromResult(result);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _yolo?.Dispose();
        }
    }
}