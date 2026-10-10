using System.IO;
using System.Text.Json;
using DyeFinder.Core;
using DyeFinder.App;

var options = FeedbackPackage.JsonOptions;
if (args[0] == "validate-feedback") {
    foreach (var path in args.Skip(1)) {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("RecordType", out _))
            ProblemFeedbackPackage.Validate(JsonSerializer.Deserialize<ProblemFeedback>(json, options)!);
        else FeedbackPackage.Validate(JsonSerializer.Deserialize<TrialFeedback>(json, options)!);
        Console.WriteLine("Compatible: " + Path.GetFileName(path));
    }
    return;
}
var destination = args[1]; Directory.CreateDirectory(destination);
var colors = new Rgb[] { new(0,0,0), new(255,255,255), new(230,230,230), new(25,25,25), new(200,130,35), new(140,36,35), new(184,125,46), new(171,40,38), new(1,10,128), new(153,153,255) };
var labs = colors.Select(c => new { Color=c, Lab=ColorMath.ToLab(c) }).ToArray();
var distances = colors.SelectMany(a => colors.Select(b => new { A=a, B=b, Difference=ColorMath.Distance(ColorMath.ToLab(a),ColorMath.ToLab(b)) })).ToArray();
var cases = new List<object>();
void Add(string name, PixelImage source, Geometry geometry, SearchOptions search) {
    var bytes = source.Pixels.SelectMany(c => new byte[]{c.R,c.G,c.B}).ToArray();
    File.WriteAllBytes(Path.Combine(destination,name+".rgb"),bytes);
    var detected = GeometryDetector.Detect(source);
    var board = source.Crop(geometry.Panel);
    cases.Add(new { Name=name, Width=source.Width, Height=source.Height, PixelFile=name+".rgb", Geometry=geometry, Detected=detected, Options=search, Suggestions=SearchEngine.Analyze(board,geometry,search) });
}
var demo = ImageFiles.Open(args[2]).pixels;
var g = GeometryDetector.Detect(demo) ?? throw new Exception("Demo was not detected");
var defaults = colors.Skip(2).Take(6).ToArray();
ImageFiles.SavePng(ImageFiles.Bitmap(demo.Crop(g.Panel)),Path.Combine(destination,"demo-board.png"));
Add("demo-default",demo,g,new(defaults,15,false,3));
Add("demo-stable",demo,g,new(defaults,15,true,3));
Add("demo-strict",demo,g,new([new(25,25,25),new(230,230,230)],5,false,3));
Add("demo-no-match",demo,g,new([new(1,10,128)],1,false,3));
var thirty = Enumerable.Range(0,30).Select(i=>new Rgb((byte)(i*7%256),(byte)(i*13%256),(byte)(i*19%256))).ToArray();
Add("demo-thirty-targets",demo,g,new(thirty,15,false,3));
const double scale=.75;
int sw=(int)Math.Round(demo.Width*scale),sh=(int)Math.Round(demo.Height*scale);
var scaledPixels = Enumerable.Range(0,sw*sh).Select(i=>demo[(int)((i%sw)/scale),(int)((i/sw)/scale)]).ToArray();
var scaledGeometry = new Geometry(new(0,0,(int)Math.Round(g.Panel.Width*scale),(int)Math.Round(g.Panel.Height*scale)),g.Markers.Select(p=>new PointI((int)Math.Round(p.X*scale),(int)Math.Round(p.Y*scale))).ToArray(),"generated scaled demo / manual",1);
Add("scaled-demo",new(sw,sh,scaledPixels),scaledGeometry,new(defaults,15,false,3));
var offsets = new PointI[]{new(0,0),new(16,0),new(32,0),new(0,16),new(16,16),new(32,16)};
var points = offsets.Select(p=>new PointI(p.X+10,p.Y+10)).ToArray();
var synthetic = Enumerable.Repeat(new Rgb(40,80,170),128*128).ToArray();
foreach(var p in offsets) synthetic[(70+p.Y)*128+50+p.X]=new(25,25,25);
var sg = new Geometry(new(0,0,128,128),points,"test",1);
Add("six-slot-optimum",new(128,128,synthetic),sg,new([new(25,25,25),new(25,25,25)],1,false,3));
Add("synthetic-no-match",new(128,128,synthetic),sg,new([new(230,230,230)],1,false,3));
Add("coordinate-ties",new(128,128,Enumerable.Repeat(new Rgb(25,25,25),128*128).ToArray()),sg,new([new(25,25,25)],15,false,3));
var gradient = Enumerable.Range(0,101*93).Select(i=>new Rgb((byte)(i%101*2),(byte)(i/101*2),(byte)(i%19*11))).ToArray();
var gg = new Geometry(new(0,0,101,93),points,"manual",1);
Add("gradient-stable",new(101,93,gradient),gg,new(defaults,40,true,3));
File.WriteAllText(Path.Combine(destination,"golden.json"),JsonSerializer.Serialize(new { BaselineCommit="76301cac3481f4b433eec9fc81cbf2ff416c4391", Labs=labs, Distances=distances, Cases=cases },options));
Console.WriteLine($"Exported {labs.Length} Lab values, {distances.Length} distances, {cases.Count} search cases from unchanged C# source.");
