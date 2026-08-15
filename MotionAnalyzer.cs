using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Grimlok.Configuration;
using Grimlok.Models;
using Microsoft.Extensions.Options;
using System.Drawing;

namespace Grimlok.Services;

public sealed class MotionAnalyzer(IOptions<GrimlokOptions> options) : IDisposable
{
    private readonly MotionOptions _options = options.Value.Motion;
    private readonly object _sync = new();
    private Mat? _previousGray;

    public MotionAnalysisResult Analyze(Mat frame)
    {
        lock (_sync)
        {
            using var gray = new Mat();
            using var blurred = new Mat();
            using var difference = new Mat();
            using var binary = new Mat();

            CvInvoke.CvtColor(frame, gray, ColorConversion.Bgr2Gray);
            CvInvoke.GaussianBlur(gray, blurred, new Size(5, 5), 0);

            if (_previousGray is not null &&
                _previousGray.Width == blurred.Width &&
                _previousGray.Height == blurred.Height)
            {
                CvInvoke.AbsDiff(blurred, _previousGray, difference);
                CvInvoke.Threshold(
                    difference,
                    binary,
                    _options.PixelThreshold,
                    255,
                    ThresholdType.Binary);

                using var kernel = CvInvoke.GetStructuringElement(
                    shape: ElementShape.Rectangle,  // Here we add an argument >>> shape:
                    new Size(3, 3),
                    new Point(-1, -1));
                // Wrong usage, you have to give Emgu CV the exact terminology it requires by Explicitly Replacing MorphShapes.Rectange
                //  MorphShapes.Rectangle,
                //  new Size(3, 3),
                //  new Point(-1, -1));
                CvInvoke.MorphologyEx(
                    binary,
                    binary,
                    MorphOp.Open,
                    kernel,
                    new Point(-1, -1),
                    1,
                    BorderType.Reflect,
                    new Emgu.CV.Structure.MCvScalar());

                var changedPixels = CvInvoke.CountNonZero(binary);
                var totalPixels = Math.Max(1L, binary.Width * (long)binary.Height);
                var motionRatio = changedPixels / (double)totalPixels;

                using var contours = new VectorOfVectorOfPoint();
                CvInvoke.FindContours(
                    binary,
                    contours,
                    null,
                    RetrType.External,
                    ChainApproxMethod.ChainApproxSimple);

                var regions = new List<MotionRegion>();
                for (var index = 0; index < contours.Size; index++)
                {
                    var area = CvInvoke.ContourArea(contours[index]);
                    if (area < _options.MinimumContourArea)
                    {
                        continue;
                    }

                    var bounds = CvInvoke.BoundingRectangle(contours[index]);
                    regions.Add(new MotionRegion(
                        bounds.X,
                        bounds.Y,
                        bounds.Width,
                        bounds.Height,
                        area));
                }

                blurred.CopyTo(_previousGray);
                return new MotionAnalysisResult(
                    regions.Count > 0 && motionRatio >= _options.MinimumMotionRatio,
                    motionRatio,
                    regions);
            }

            _previousGray?.Dispose();
            _previousGray = blurred.Clone();
            return new MotionAnalysisResult(false, 0, []);
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _previousGray?.Dispose();
            _previousGray = null;
        }
    }

    public void Dispose() => Reset();
}
