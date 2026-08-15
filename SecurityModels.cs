#region SYSTEM INITIALIZATION : BLACK STAR PROJECT
// ========================================================================
//   ____  _        _    ____ _  __  ____ _____  _    ____  
//  | __ )| |      / \  / ___| |/ / / ___|_   _|/ \  |  _ \ 
//  |  _ \| |     / _ \| |   | ' /  \___ \ | | / _ \ | |_) |
//  | |_) | |___ / ___ \ |___| . \   ___) || |/ ___ \|  _ < 
//  |____/|_____/_/   \_\____|_|\_\ |____/ |_/_/   \_\_| \_\
//                                                          
//              R E S E A R C H   F A C I L I T Y           
//                                                          
//             [ LOCATION: ICELAND ]            
// ========================================================================
#endregion
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
