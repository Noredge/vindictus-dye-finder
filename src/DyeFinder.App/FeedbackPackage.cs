using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using DyeFinder.Core;

namespace DyeFinder.App;
public enum TrialOutcome { MatchedHere, FoundNearby, NotUseful }
public sealed record TrialFeedback(
    int SchemaVersion,Guid RecordId,Guid AnalysisId,Guid SessionId,string AppVersion,string RankerVersion,
    TrialOutcome Outcome,int SuggestedSpot,int InspectedPoint,PointI SourceImageSize,
    string BoardSha256,string? ResultImage,string ResultImageRelationship,CalibrationRecord Recommendation);

public static class FeedbackPackage
{
    public static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true,Converters={new JsonStringEnumConverter()}};
    public static string OutcomeLabel(TrialOutcome outcome)=>outcome switch{
        TrialOutcome.MatchedHere=>"Matched here",TrialOutcome.FoundNearby=>"Found nearby",_=>"Not useful"};
    public static void Validate(TrialFeedback record)
    {
        var data=record.Recommendation;
        if(record.SchemaVersion!=2 || !Enum.IsDefined(record.Outcome) || record.RecordId==Guid.Empty || record.AnalysisId==Guid.Empty || record.SessionId==Guid.Empty
            || record.SuggestedSpot is <1 or >3 || record.InspectedPoint is <1 or >6 || data.OriginalMarkers.Length!=6 || data.Suggestion.Slots.Length!=6)
            throw new ArgumentException("Incomplete feedback record.");
        if(data.ActualGameRgb is not null && (record.Outcome!=TrialOutcome.MatchedHere || data.ActualGameRgb.Length!=6 || !data.ActualValuesVerifiedAtSuggestedPosition))
            throw new ArgumentException("Actual RGB labels need six verified values from the suggested position. Nearby results cannot label that position.");
    }
    private static byte[] Png(PixelImage image)
    {
        using var stream=new MemoryStream();var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(ImageFiles.Bitmap(image)));encoder.Save(stream);return stream.ToArray();
    }
    public static void Save(string path,PixelImage board,TrialFeedback record,PixelImage? evidence=null)
    {
        Validate(record);
        if(record.SourceImageSize.X<board.Width || record.SourceImageSize.Y<board.Height)throw new ArgumentException("Invalid source dimensions.");
        var bytes=Png(board);var actual=record.Recommendation.ActualGameRgb;
        record=record with{BoardSha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),ResultImage=evidence is null?null:"result-crop.png",
            ResultImageRelationship=evidence is null?"not-provided":"user-selected-final-state; coordinates and RGB not automatically verified",
            Recommendation=record.Recommendation with{PanelImage="board.png",PanelSize=new(board.Width,board.Height),Usefulness=OutcomeLabel(record.Outcome)}};
        var full=Path.GetFullPath(path);var directory=Path.GetDirectoryName(full)!;Directory.CreateDirectory(directory);
        var temp=Path.Combine(directory,"."+Guid.NewGuid().ToString("N")+".tmp");
        try
        {
            using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write))
            using(var zip=new ZipArchive(file,ZipArchiveMode.Create))
            {
                void Write(string name,byte[] data){using var entry=zip.CreateEntry(name).Open();entry.Write(data);}
                Write("board.png",bytes);
                Write("feedback.json",Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record,JsonOptions)));
                if(evidence is not null)Write("result-crop.png",Png(evidence));
                var sb=new StringBuilder();sb.AppendLine("Vindictus Dye Finder - trial feedback");
                sb.AppendLine($"App: {record.AppVersion} | Sampler: {record.Recommendation.Sampler} | Ranker: {record.RankerVersion}");
                sb.AppendLine($"Source: {record.Recommendation.SourceKind}");
                if(record.Recommendation.SourceKind=="bundled-real-demo")sb.AppendLine("Built-in example: do not count this as an independent live-game trial.");
                sb.AppendLine($"Outcome: {OutcomeLabel(record.Outcome)}");sb.AppendLine($"Suggested spot: {record.SuggestedSpot} | Point inspected: {record.InspectedPoint}");
                sb.AppendLine($"Targets: {string.Join(" / ",record.Recommendation.Targets)}");sb.AppendLine($"Color tolerance: {record.Recommendation.Threshold:0}");
                sb.AppendLine("\nPoint | Estimated RGB | Verified game RGB at suggested position");
                foreach(var slot in record.Recommendation.Suggestion.Slots)sb.AppendLine($"{slot.Index} | {slot.Color} | {(actual is null?"not supplied":actual[slot.Index-1].ToString())}");
                sb.AppendLine($"\nFinal screenshot: {record.ResultImageRelationship}");
                sb.AppendLine("Matched here is a user usefulness report, not proof of exact RGB equality. Nearby results never become labels for the original recommendation.");
                sb.AppendLine("\nSettings: "+record.Recommendation.RenderingSettings);sb.AppendLine("Notes: "+record.Recommendation.MaterialAndAppearanceNotes);
                sb.AppendLine("\nReview this ZIP before sharing. Nothing is uploaded automatically. Included images are only the board and any crop you chose; notes are included as entered.");
                Write("summary.txt",Encoding.UTF8.GetBytes(sb.ToString()));
            }
            File.Move(temp,full,false);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
