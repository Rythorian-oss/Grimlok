namespace Grimlok.Models
{
    public sealed record MotionAnalysisResult(
        bool MotionDetected,
        double MotionRatio,
        IReadOnlyList<MotionRegion> Regions);

    public sealed record MotionRegion(
        int X,
        int Y,
        int Width,
        int Height,
        double Area);

    public sealed record DetectedObject(
        string Label,
        double Confidence,
        int X,
        int Y,
        int Width,
        int Height);

    public sealed record SecurityEvent(
        Guid Id,
        DateTimeOffset StartedAt,
        DateTimeOffset EndedAt,
        double MotionRatio,
        IReadOnlyList<MotionRegion> Regions,
        IReadOnlyList<DetectedObject> Objects,
        string? SnapshotFileName)
    {
        // Added for UI Binding
        public string DisplayName => $"{StartedAt:HH:mm:ss} - {MotionRatio:P2}";
    }

    public sealed record SecurityAlert(
        DateTimeOffset DetectedAt,
        double MotionRatio,
        IReadOnlyList<MotionRegion> Regions,
        IReadOnlyList<DetectedObject> Objects,
        byte[]? SnapshotJpeg);

    public sealed record MonitorStatus(
        string State,
        string CameraSource,
        bool ObjectDetectionEnabled,
        DateTimeOffset? StartedAt,
        DateTimeOffset? LastFrameAt,
        DateTimeOffset? LastMotionAt,
        DateTimeOffset? LastAlertAt,
        DateTimeOffset? LastSnapshotAt,
        long FramesProcessed,
        long MotionEvents,
        string? LastError);
}
