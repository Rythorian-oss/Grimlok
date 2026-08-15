using Emgu.CV;
using Emgu.CV.CvEnum;
using Grimlok.Configuration;
using Grimlok.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Grimlok.Services;

public sealed class MonitorService : IDisposable
{
    private readonly GrimlokOptions _options;
    private readonly MotionAnalyzer _motionAnalyzer;
    private readonly IObjectDetector _objectDetector;
    private readonly IAlertDispatcher _alertDispatcher;
    private readonly MotionEventStore _events;
    private readonly SnapshotStore _snapshots;
    private readonly ILogger<MonitorService> _logger;
    private readonly object _statusSync = new();
    private readonly object _optionsSync = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);

    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;
    private MonitorStatus _status;

    public event EventHandler<Mat>? FrameProcessed;
    public event EventHandler<MotionAnalysisResult>? MotionDetected;
    public event EventHandler<MonitorStatus>? StatusChanged;

    public MonitorService(
        IOptions<GrimlokOptions> options,
        MotionAnalyzer motionAnalyzer,
        IObjectDetector objectDetector,
        IAlertDispatcher alertDispatcher,
        MotionEventStore events,
        SnapshotStore snapshots,
        ILogger<MonitorService> logger)
    {
        _options = options.Value;
        _motionAnalyzer = motionAnalyzer;
        _objectDetector = objectDetector;
        _alertDispatcher = alertDispatcher;
        _events = events;
        _snapshots = snapshots;
        _logger = logger;
        _status = CreateStatus("Stopped");
    }

    public MonitorStatus GetStatus()
    {
        lock (_statusSync)
            return _status;
    }

    public void UpdateMotionThreshold(int threshold)
    {
        lock (_optionsSync)
            _options.Motion.PixelThreshold = Math.Clamp(threshold, 1, 100);
        _logger.LogInformation("Motion threshold updated to {Threshold}", _options.Motion.PixelThreshold);
    }

    public void UpdateEndAfterSeconds(int seconds)
    {
        lock (_optionsSync)
            _options.Motion.EndAfterSeconds = Math.Clamp(seconds, 1, 60);
        _logger.LogInformation("End after seconds updated to {Seconds}", _options.Motion.EndAfterSeconds);
    }

    public void UpdateHumanConfirmedOnly(bool value)
    {
        lock (_optionsSync)
            _options.ObjectDetection.HumanConfirmedOnly = value;
        _logger.LogInformation("Human confirmed only set to {Value}", value);
    }

    public void UpdateObjectDetectionEnabled(bool value)
    {
        lock (_optionsSync)
            _options.ObjectDetection.Enabled = value;
        UpdateStatus(status => status with { ObjectDetectionEnabled = value });
        _logger.LogInformation("Object detection enabled set to {Value}", value);
    }

    public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (_monitorTask is { IsCompleted: false })
                return;

            _monitorCts?.Dispose();
            _monitorCts = new CancellationTokenSource();
            var monitorToken = _monitorCts.Token;
            _monitorTask = Task.Run(() => MonitorLoopAsync(monitorToken), CancellationToken.None);
            UpdateStatus(status => status with
            {
                State = "Starting",
                StartedAt = DateTimeOffset.UtcNow,
                LastError = null
            });
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task StopMonitoringAsync(CancellationToken cancellationToken = default)
    {
        Task? monitorTask;
        CancellationTokenSource? monitorCts;
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            monitorCts = _monitorCts;
            monitorCts?.Cancel();
            monitorTask = _monitorTask;
            _monitorTask = null;
            _monitorCts = null;
        }
        finally
        {
            _lifecycle.Release();
        }

        if (monitorTask is not null)
        {
            try
            {
                await monitorTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        monitorCts?.Dispose();
        _motionAnalyzer.Reset();
        UpdateStatus(status => status with { State = "Stopped" });
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        var activeEventId = Guid.Empty;
        var activeEventStartedAt = DateTimeOffset.MinValue;
        var activeMotionLastSeen = DateTimeOffset.MinValue;
        var activeMotionRatio = 0d;
        var activeRegions = Array.Empty<MotionRegion>();
        var activeObjects = Array.Empty<DetectedObject>();
        string? activeSnapshotFileName = null;
        byte[]? activeSnapshotJpeg = null;
        DateTimeOffset lastAlertAt = DateTimeOffset.MinValue;
        DateTimeOffset lastSnapshotAt = DateTimeOffset.MinValue;

        try
        {
            using var capture = CreateCapture();
            if (!capture.IsOpened)
                throw new InvalidOperationException($"Unable to open camera source '{_options.Camera.Source}'");

            ConfigureCapture(capture);
            UpdateStatus(status => status with { State = "Running" });
            _logger.LogInformation("Monitoring started for camera source {Source}", _options.Camera.Source);

            using var frame = new Mat();
            var stopwatch = Stopwatch.StartNew();
            var frameInterval = TimeSpan.FromMilliseconds(_options.Motion.AnalysisIntervalMilliseconds);

            while (!cancellationToken.IsCancellationRequested)
            {
                var loopStarted = stopwatch.Elapsed;
                if (!capture.Read(frame) || frame.IsEmpty)
                {
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                // Raise frame event – clone disposed after event
                using var frameClone = frame.Clone();
                FrameProcessed?.Invoke(this, frameClone);

                var now = DateTimeOffset.UtcNow;
                UpdateStatus(status => status with
                {
                    LastFrameAt = now,
                    FramesProcessed = status.FramesProcessed + 1
                });

                MotionAnalysisResult analysis;
                lock (_optionsSync)
                    analysis = _motionAnalyzer.Analyze(frame);

                if (analysis.MotionDetected)
                {
                    MotionDetected?.Invoke(this, analysis);

                    activeMotionLastSeen = now;
                    activeMotionRatio = Math.Max(activeMotionRatio, analysis.MotionRatio);
                    activeRegions = [.. analysis.Regions];

                    // Encode JPEG once for all uses
                    var jpeg = CvInvoke.Imencode(".jpg", frame);

                    bool objectDetectionEnabled, humanConfirmedOnly;
                    lock (_optionsSync)
                    {
                        objectDetectionEnabled = _options.ObjectDetection.Enabled;
                        humanConfirmedOnly = _options.ObjectDetection.HumanConfirmedOnly;
                    }

                    var objects = (objectDetectionEnabled && _objectDetector.IsEnabled)
                        ? await _objectDetector.DetectAsync(jpeg, cancellationToken)
                        : [];

                    if (humanConfirmedOnly && _objectDetector.IsEnabled && objects.Count == 0)
                    {
                        await DelayForFrameAsync(loopStarted, stopwatch, frameInterval, cancellationToken);
                        continue;
                    }

                    activeObjects = [.. objects];
                    if (activeEventId == Guid.Empty)
                    {
                        activeEventId = Guid.NewGuid();
                        activeEventStartedAt = now;
                        UpdateStatus(status => status with
                        {
                            LastMotionAt = now,
                            MotionEvents = status.MotionEvents + 1
                        });
                        _logger.LogWarning(
                            "Motion detected: ratio={MotionRatio:P2}, regions={RegionCount}, objects={ObjectCount}",
                            analysis.MotionRatio,
                            analysis.Regions.Count,
                            objects.Count);
                    }

                    int snapshotCooldownSeconds, alertCooldownSeconds;
                    lock (_optionsSync)
                    {
                        snapshotCooldownSeconds = _options.Motion.SnapshotCooldownSeconds;
                        alertCooldownSeconds = _options.Motion.AlertCooldownSeconds;
                    }

                    if (now - lastSnapshotAt >= TimeSpan.FromSeconds(snapshotCooldownSeconds))
                    {
                        // Save snapshot using JPEG bytes
                        var snapshotFileName = _snapshots.SaveJpeg(jpeg, now);
                        activeSnapshotFileName ??= snapshotFileName;
                        activeSnapshotJpeg ??= [.. jpeg]; // keep copy for alert
                        lastSnapshotAt = now;
                        UpdateStatus(status => status with { LastSnapshotAt = now });

                        if (now - lastAlertAt >= TimeSpan.FromSeconds(alertCooldownSeconds))
                        {
                            lastAlertAt = now;
                            UpdateStatus(status => status with { LastAlertAt = now });
                            var alert = new SecurityAlert(
                                now,
                                analysis.MotionRatio,
                                analysis.Regions,
                                objects,
                                activeSnapshotJpeg);
                            await DispatchAlertSafelyAsync(alert, cancellationToken);
                        }
                    }
                }
                else if (activeEventId != Guid.Empty)
                {
                    int endAfterSeconds;
                    lock (_optionsSync)
                        endAfterSeconds = _options.Motion.EndAfterSeconds;

                    if (now - activeMotionLastSeen >= TimeSpan.FromSeconds(endAfterSeconds))
                    {
                        var completed = new SecurityEvent(
                            activeEventId,
                            activeEventStartedAt,
                            activeMotionLastSeen,
                            activeMotionRatio,
                            activeRegions,
                            activeObjects,
                            activeSnapshotFileName);
                        _events.AddEvent(completed);
                        _logger.LogInformation(
                            "Motion event {EventId} ended after {Duration:F1}s",
                            completed.Id,
                            (completed.EndedAt - completed.StartedAt).TotalSeconds);
                        activeEventId = Guid.Empty;
                        activeMotionRatio = 0;
                        activeRegions = [];
                        activeObjects = [];
                        activeSnapshotFileName = null;
                        activeSnapshotJpeg = null;
                    }
                }

                await DelayForFrameAsync(loopStarted, stopwatch, frameInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Monitoring cancelled");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Monitoring loop failed");
            UpdateStatus(status => status with
            {
                State = "Faulted",
                LastError = exception.Message
            });
        }
        finally
        {
            if (activeEventId != Guid.Empty)
            {
                _events.AddEvent(new SecurityEvent(
                    activeEventId,
                    activeEventStartedAt,
                    DateTimeOffset.UtcNow,
                    activeMotionRatio,
                    activeRegions,
                    activeObjects,
                    activeSnapshotFileName));
            }

            if (GetStatus().State != "Faulted")
                UpdateStatus(status => status with { State = "Stopped" });
        }
    }

    private async Task DispatchAlertSafelyAsync(SecurityAlert alert, CancellationToken cancellationToken)
    {
        try
        {
            await _alertDispatcher.DispatchAsync(alert, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Unable to dispatch motion alert");
            UpdateStatus(status => status with { LastError = exception.Message });
        }
    }

    private VideoCapture CreateCapture()
    {
        return int.TryParse(_options.Camera.Source, out var cameraIndex)
            ? new VideoCapture(cameraIndex)
            : new VideoCapture(_options.Camera.Source);
    }

    private void ConfigureCapture(VideoCapture capture)
    {
        if (_options.Camera.Width > 0) capture.Set(CapProp.FrameWidth, _options.Camera.Width);
        if (_options.Camera.Height > 0) capture.Set(CapProp.FrameHeight, _options.Camera.Height);
        if (_options.Camera.Fps > 0) capture.Set(CapProp.Fps, _options.Camera.Fps);
    }

    private static async Task DelayForFrameAsync(
        TimeSpan loopStarted,
        Stopwatch stopwatch,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        var remaining = interval - (stopwatch.Elapsed - loopStarted);
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining, cancellationToken);
    }

    private MonitorStatus CreateStatus(string state) => new(
        state,
        _options.Camera.Source,
        _options.ObjectDetection.Enabled,
        null,
        null,
        null,
        null,
        null,
        0,
        0,
        null);

    private void UpdateStatus(Func<MonitorStatus, MonitorStatus> update)
    {
        MonitorStatus newStatus;
        lock (_statusSync)
        {
            _status = update(_status);
            newStatus = _status;
        }
        StatusChanged?.Invoke(this, newStatus);
    }

    public void Dispose()
    {
        _monitorCts?.Cancel();
        // Wait for the loop to exit with a bounded timeout
        if (_monitorTask != null)
        {
            try
            {
                _monitorTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ae) when (ae.InnerExceptions.All(e => e is OperationCanceledException)) { }
            catch (Exception) { }
        }
        _monitorCts?.Dispose();
        _lifecycle.Dispose();
        _motionAnalyzer.Dispose();
        if (_objectDetector is IDisposable disposable)
            disposable.Dispose();
    }
}