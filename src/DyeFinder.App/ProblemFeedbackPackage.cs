using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using DyeFinder.Core;

namespace DyeFinder.App;

public enum ProblemStage { NoScreenshot, ScreenshotUnreadable, BoardNotDetected, NoMatches, Other }

// A problem report is not a recommendation trial and never supplies calibration labels.
public sealed record ProblemFeedback(
    int SchemaVersion, string RecordType, Guid RecordId, DateTimeOffset CreatedUtc,
    Guid SessionId, string AppVersion, ProblemStage Stage, Rgb[] Targets, double ColorTolerance,
    string SourceKind, PointI? SourceImageSize, string Notes,
    string? ResultImage, string ResultImageRelationship);

public static class ProblemFeedbackPackage
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string StageLabel(ProblemStage stage) => stage switch
    {
        ProblemStage.NoScreenshot => "No screenshot loaded",
        ProblemStage.ScreenshotUnreadable => "Screenshot could not be opened",
        ProblemStage.BoardNotDetected => "Dye board could not be detected",
        ProblemStage.NoMatches => "No matching spots found",
        _ => "Another problem"
    };

    public static ProblemFeedback Create(ProblemStage stage, Guid sessionId, string appVersion,
        Rgb[] targets, double tolerance, string sourceKind, PointI? imageSize, string notes) =>
        new(1, "problem-feedback", Guid.NewGuid(), DateTimeOffset.UtcNow, sessionId, appVersion,
            stage, targets.ToArray(), tolerance, sourceKind, imageSize, notes, null, "not-provided");

    public static void Validate(ProblemFeedback report)
    {
        if (report.SchemaVersion != 1 || report.RecordType != "problem-feedback" ||
            report.RecordId == Guid.Empty || report.SessionId == Guid.Empty ||
            !Enum.IsDefined(report.Stage) || !Version.TryParse(report.AppVersion, out _) ||
            report.Targets is null || !double.IsFinite(report.ColorTolerance) ||
            report.ColorTolerance is < 0 or > 100 || report.Notes is null || report.Notes.Length > 3000 ||
            report.SourceKind is not ("none" or "user-image" or "bundled-real-demo") ||
            (report.SourceImageSize is { } size && (size.X <= 0 || size.Y <= 0)) ||
            (report.SourceKind == "none" && report.SourceImageSize is not null))
            throw new ArgumentException("Incomplete problem report.");
    }

    public static void Save(string path, ProblemFeedback report, PixelImage? reviewedCrop = null)
    {
        Validate(report);
        report = report with
        {
            ResultImage = reviewedCrop is null ? null : "screenshot-crop.png",
            ResultImageRelationship = reviewedCrop is null ? "not-provided" :
                "user-selected and reviewed crop; contents and RGB not automatically verified"
        };
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                void Write(string name, byte[] bytes)
                {
                    using var entry = zip.CreateEntry(name).Open();
                    entry.Write(bytes);
                }
                Write("feedback.json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(report, JsonOptions)));
                if (reviewedCrop is not null)
                {
                    using var buffer = new MemoryStream();
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(ImageFiles.Bitmap(reviewedCrop)));
                    encoder.Save(buffer);
                    Write("screenshot-crop.png", buffer.ToArray());
                }
                var summary = new StringBuilder();
                summary.AppendLine("Vindictus Dye Finder - problem report");
                summary.AppendLine($"App: {report.AppVersion}");
                summary.AppendLine($"Problem: {StageLabel(report.Stage)}");
                summary.AppendLine($"Source: {report.SourceKind}");
                if (report.SourceKind == "bundled-real-demo")
                    summary.AppendLine("Built-in example: do not count this as an independent live-game trial.");
                if (report.SourceImageSize is { } size)
                    summary.AppendLine($"Screenshot size: {size.X} x {size.Y}");
                summary.AppendLine($"Chosen targets: {(report.Targets.Length == 0 ? "none" : string.Join(" / ", report.Targets))}");
                summary.AppendLine($"Color tolerance: {report.ColorTolerance:0.##}");
                summary.AppendLine("\nNotes: " + report.Notes);
                summary.AppendLine("\nScreenshot: " + report.ResultImageRelationship);
                summary.AppendLine("This report contains no suggested position or verified game RGB. It is not a calibration sample.");
                summary.AppendLine("\nReview this ZIP before sharing. Nothing is uploaded automatically. No source file path or full screenshot is included automatically; notes are included as entered.");
                Write("summary.txt", Encoding.UTF8.GetBytes(summary.ToString()));
            }
            File.Move(temp, full, false);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
