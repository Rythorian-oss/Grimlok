using Emgu.CV;
using Grimlok.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;

namespace Grimlok.Services;

public sealed class SnapshotStore(IOptions<GrimlokOptions> options, ILogger<SnapshotStore> logger)
{
    private readonly SnapshotOptions _options = options.Value.Snapshots;
    private readonly ILogger<SnapshotStore> _logger = logger;

    public string? SaveSnapshot(Mat frame, DateTimeOffset timestamp)
    {
        if (!_options.Enabled) return null;
        try
        {
            var jpeg = CvInvoke.Imencode(".jpg", frame);
            return SaveJpeg(jpeg, timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to save motion snapshot");
            return null;
        }
    }

    public string? SaveJpeg(byte[] jpegData, DateTimeOffset timestamp)
    {
        if (!_options.Enabled) return null;
        try
        {
            var directory = ResolveDirectory(_options.Directory);
            Directory.CreateDirectory(directory);
            var fileName = $"motion-{timestamp:yyyyMMdd-HHmmss-fff}.jpg";
            var fullPath = Path.Combine(directory, fileName);
            File.WriteAllBytes(fullPath, jpegData);
            _logger.LogInformation("Motion snapshot saved to {SnapshotPath}", fullPath);
            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to save motion snapshot");
            return null;
        }
    }

    public string? Save(Mat frame, DateTimeOffset timestamp) => SaveSnapshot(frame, timestamp);

    public (byte[] Content, string FileName)? OpenRead(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        if (!string.Equals(safeName, fileName, StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(safeName), ".jpg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = Path.Combine(ResolveDirectory(_options.Directory), safeName);
        return File.Exists(path) ? (File.ReadAllBytes(path), safeName) : null;
    }

    private static string ResolveDirectory(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
}