using System.ComponentModel.DataAnnotations;

namespace Grimlok.Configuration;

public sealed class GrimlokOptions
{
    public const string SectionName = "Grimlok";

    public CameraOptions Camera { get; set; } = new();
    public MotionOptions Motion { get; set; } = new();
    public ObjectDetectionOptions ObjectDetection { get; set; } = new();
    public SnapshotOptions Snapshots { get; set; } = new();
    public SmtpOptions Smtp { get; set; } = new();
    public AlertOptions Alerts { get; set; } = new();
    public bool AutoStart { get; set; }
}

public sealed class CameraOptions
{
    [Required]
    public string Source { get; set; } = "0";

    [Range(0, 7680)]
    public int Width { get; set; } = 1280;

    [Range(0, 4320)]
    public int Height { get; set; } = 720;

    [Range(1, 120)]
    public int Fps { get; set; } = 15;
}

public sealed class MotionOptions
{
    [Range(1, 255)]
    public int PixelThreshold { get; set; } = 22;

    [Range(1, 1_000_000)]
    public double MinimumContourArea { get; set; } = 900;

    [Range(0.000001, 1)]
    public double MinimumMotionRatio { get; set; } = 0.004;

    [Range(1, 60)]
    public int EndAfterSeconds { get; set; } = 2;

    [Range(10, 10_000)]
    public int AnalysisIntervalMilliseconds { get; set; } = 100;

    [Range(0, 86_400)]
    public int AlertCooldownSeconds { get; set; } = 5;

    [Range(0, 86_400)]
    public int SnapshotCooldownSeconds { get; set; } = 3;
}

public sealed class ObjectDetectionOptions
{
    public bool Enabled { get; set; } = true;

    public string ModelPath { get; set; } = "Models/yolov8n.onnx";

    public string LabelsPath { get; set; } = "Models/coco.txt";

    [Range(0.01, 1)]
    public double Confidence { get; set; } = 0.45;

    public bool HumanConfirmedOnly { get; set; } = true;

    public string[] HumanLabels { get; set; } = ["person"];
}

public sealed class SnapshotOptions
{
    [Required]
    public string Directory { get; set; } = "Storage/Snapshots";

    [Range(1, 100)]
    public int JpegQuality { get; set; } = 88;

    public bool Enabled { get; set; } = true;
}

public sealed class SmtpOptions
{
    public bool EmailEnabled { get; set; }
    public string Host { get; set; } = string.Empty;

    public string SmtpHost
    {
        get => Host;
        set => Host = value;
    }

    [Range(1, 65_535)]
    public int Port { get; set; } = 587;

    public int SmtpPort
    {
        get => Port;
        set => Port = value;
    }

    public bool EnableSsl { get; set; } = true;

    public bool UseSsl
    {
        get => EnableSsl;
        set => EnableSsl = value;
    }

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string PasswordEnvironmentVariable { get; set; } = "GRIMLOK_ALERTS_PASSWORD";
}

public sealed class AlertOptions
{
    public bool EmailEnabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65_535)]
    public int SmtpPort { get; set; } = 587;

    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string PasswordEnvironmentVariable { get; set; } = "GRIMLOK_ALERTS_PASSWORD";
}