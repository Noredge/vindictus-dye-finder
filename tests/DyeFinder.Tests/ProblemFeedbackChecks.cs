using System.IO;
using System.IO.Compression;
using System.Text.Json;
using DyeFinder.App;
using DyeFinder.Core;

internal static class ProblemFeedbackChecks
{
    public static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "DyeFinderProblemTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var targets = new[] { new Rgb(153, 153, 255) };
            var report = ProblemFeedbackPackage.Create(ProblemStage.NoMatches, Guid.NewGuid(), "0.3.4",
                targets, 5, "user-image", new(2560, 1440), "No lavender matches.");
            targets[0] = new(0, 0, 0);
            var path = Path.Combine(directory, "problem.zip");
            ProblemFeedbackPackage.Save(path, report);
            using (var zip = ZipFile.OpenRead(path))
            {
                check(zip.Entries.Select(e => e.FullName).Order().SequenceEqual(new[] { "feedback.json", "summary.txt" }),
                    "problem report includes no screenshot automatically");
                using var reader = new StreamReader(zip.GetEntry("feedback.json")!.Open());
                var json = reader.ReadToEnd();
                var saved = JsonSerializer.Deserialize<ProblemFeedback>(json, ProblemFeedbackPackage.JsonOptions)!;
                check(saved.RecordType == "problem-feedback" && saved.Stage == ProblemStage.NoMatches &&
                    saved.Targets[0] == new Rgb(153, 153, 255) && saved.ColorTolerance == 5 && saved.SessionId == report.SessionId,
                    "problem report preserves failure context and copies selected targets");
                check(!json.Contains("Recommendation") && !json.Contains("ActualGameRgb") && !json.Contains(directory) && saved.ResultImage is null,
                    "problem report has no invented recommendation, labels or source path");
            }
            var before = File.ReadAllBytes(path);
            var rejected = false;
            try { ProblemFeedbackPackage.Save(path, report); } catch (IOException) { rejected = true; }
            check(rejected && before.SequenceEqual(File.ReadAllBytes(path)) && !Directory.GetFiles(directory, "*.tmp").Any(),
                "problem report never overwrites an existing ZIP and cleans temporary output");
            var crop = new PixelImage(20, 24, Enumerable.Repeat(new Rgb(70, 98, 45), 20 * 24).ToArray());
            var withCrop = Path.Combine(directory, "crop.zip");
            ProblemFeedbackPackage.Save(withCrop, report with { SourceKind = "bundled-real-demo" }, crop);
            using (var zip = ZipFile.OpenRead(withCrop))
            {
                check(zip.Entries.Select(e => e.FullName).Order().SequenceEqual(new[] { "feedback.json", "screenshot-crop.png", "summary.txt" }),
                    "problem report only adds the supplied reviewed crop");
                using var stream = zip.GetEntry("screenshot-crop.png")!.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer); buffer.Position = 0;
                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(buffer,
                    System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                check(decoder.Frames[0].PixelWidth == 20 && decoder.Frames[0].PixelHeight == 24,
                    "problem report preserves selected crop dimensions");
                using var jsonReader = new StreamReader(zip.GetEntry("feedback.json")!.Open());
                var saved = JsonSerializer.Deserialize<ProblemFeedback>(jsonReader.ReadToEnd(), ProblemFeedbackPackage.JsonOptions)!;
                using var summary = new StreamReader(zip.GetEntry("summary.txt")!.Open());
                check(saved.ResultImageRelationship.Contains("not automatically verified") &&
                    summary.ReadToEnd().Contains("do not count this as an independent live-game trial"),
                    "problem screenshot remains unverified and demo reports remain identified");
            }
            var noImage = ProblemFeedbackPackage.Create(ProblemStage.NoScreenshot, Guid.NewGuid(), "0.3.4", [], 5, "none", null, "");
            ProblemFeedbackPackage.Save(Path.Combine(directory, "no-image.zip"), noImage);
            check(File.Exists(Path.Combine(directory, "no-image.zip")), "problem report can be saved without image or optional notes");
            foreach (var invalid in new[]
            {
                report with { Stage = (ProblemStage)100 }, report with { SourceKind = @"C:\private\screenshot.png" },
                report with { ColorTolerance = double.NaN }, report with { SourceImageSize = new(0, 1440) },
                report with { RecordType = "trial-feedback" }
            })
            {
                rejected = false;
                try { ProblemFeedbackPackage.Save(Path.Combine(directory, "invalid.zip"), invalid); }
                catch (ArgumentException) { rejected = true; }
                check(rejected && !File.Exists(Path.Combine(directory, "invalid.zip")), "invalid problem context rejected before writing");
            }
        }
        finally { Directory.Delete(directory, true); }
    }
}
