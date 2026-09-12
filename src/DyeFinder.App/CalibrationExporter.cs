using System.IO;
using System.Text.Json;
using DyeFinder.Core;

namespace DyeFinder.App;
public static class CalibrationExporter
{
    public static void Save(string jsonPath,PixelImage panel,CalibrationRecord record)
    {
        if(record.SchemaVersion!=1 || record.OriginalMarkers.Length!=6 || record.Suggestion.Slots.Length!=6 || (record.ActualGameRgb is not null && (record.ActualGameRgb.Length!=6 || !record.ActualValuesVerifiedAtSuggestedPosition))) throw new ArgumentException("Incomplete calibration record");
        var directory=Path.GetDirectoryName(Path.GetFullPath(jsonPath))!;
        var imageName=Path.GetFileNameWithoutExtension(jsonPath)+"-"+Guid.NewGuid().ToString("N")[..8]+".png";
        var imagePath=Path.Combine(directory,imageName);var tempPath=jsonPath+"."+Guid.NewGuid().ToString("N")+".tmp";
        record=record with{PanelImage=imageName,PanelSize=new(panel.Width,panel.Height)};
        try
        {
            ImageFiles.SavePng(ImageFiles.Bitmap(panel),imagePath);
            File.WriteAllText(tempPath,JsonSerializer.Serialize(record,new JsonSerializerOptions{WriteIndented=true}));
            File.Move(tempPath,jsonPath,true);
        }
        catch
        {
            // Only files created in this operation, never the user's existing JSON.
            try{if(File.Exists(tempPath))File.Delete(tempPath);if(File.Exists(imagePath))File.Delete(imagePath);}catch(IOException){}
            throw;
        }
    }
}
