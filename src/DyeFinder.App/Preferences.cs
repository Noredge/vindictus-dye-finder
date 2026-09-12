using System.IO;
using System.Text.Json;
using DyeFinder.Core;

namespace DyeFinder.App;
public sealed record TargetSetting(Rgb Color,bool Enabled=true,string? Name=null);
public sealed record Preferences(int SchemaVersion,TargetSetting[] Targets,double Threshold,bool PreferStable)
{
    public static Preferences Default => new(1,[new(new(230,230,230)),new(new(25,25,25)),new(new(200,130,35)),new(new(140,36,35)),new(new(184,125,46)),new(new(171,40,38))],15,false);
    public bool Valid => SchemaVersion==1 && Targets is not null && Targets.Length<=30 && Targets.All(t=>t is not null && (t.Name is null || t.Name.Length<=40)) && Targets.Select(t=>t.Color).Distinct().Count()==Targets.Length && double.IsFinite(Threshold) && Threshold>=1 && Threshold<=40;
}
public sealed class PreferenceStore(string path)
{
    private bool writable=true;
    public string? Warning {get;private set;}
    public Preferences Load()
    {
        if(!File.Exists(path)) return Preferences.Default;
        try
        {
            var value=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path));
            if(value is null || !value.Valid) throw new InvalidDataException("Unsupported preferences format");return value;
        }
        catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException or InvalidDataException)
        { writable=false;Warning="Could not load preferences. Defaults are in use; the original file has been preserved.";return Preferences.Default; }
    }
    public void Save(Preferences value)
    {
        if(!writable || !value.Valid) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
            File.Move(path+".tmp",path,true);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { Warning="Could not save preferences."; }
    }
}
