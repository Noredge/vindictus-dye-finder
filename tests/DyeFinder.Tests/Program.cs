using System.IO;
using System.Text.Json;
using DyeFinder.Core;
using DyeFinder.App;

internal static class Program
{
    private static int passed,failed;
    private static void Check(bool condition,string name){if(condition){passed++;Console.WriteLine("PASS "+name);}else{failed++;Console.WriteLine("FAIL "+name);}}
    [STAThread]
    public static int Main(string[] args)
    {
        var pairs=new (Lab a,Lab b,double expected)[]{
            (new(50,2.6772,-79.7751),new(50,0,-82.7485),2.0425),
            (new(50,3.1571,-77.2803),new(50,0,-82.7485),2.8615),
            (new(50,2.8361,-74.0200),new(50,0,-82.7485),3.4412),
            (new(50,-1.3802,-84.2814),new(50,0,-82.7485),1.0000),
            (new(50,0,0),new(50,-1,2),2.3669),
            (new(50,2.49,-.001),new(50,-2.49,.001),7.1792)};
        foreach(var p in pairs)Check(Math.Abs(ColorMath.Distance(p.a,p.b)-p.expected)<.0001,"CIEDE2000 reference "+p.expected);
        Check(Math.Abs(ColorMath.ToLab(new(255,255,255)).L-100)<.0001,"sRGB D65 white");
        Check(Rgb.TryParse("200，130，35",out var rgb)&&rgb==new Rgb(200,130,35)&&!Rgb.TryParse("256 0 0",out _),"RGB input validation");
        var black=new Rgb(25,25,25);var white=new Rgb(230,230,230);var pixels=Enumerable.Repeat(new Rgb(40,80,170),128*128).ToArray();
        var offsets=new PointI[]{new(0,0),new(16,0),new(32,0),new(0,16),new(16,16),new(32,16)};
        foreach(var p in offsets)pixels[(70+p.Y)*128+50+p.X]=black;
        var image=new PixelImage(128,128,pixels);var geometry=new Geometry(new(0,0,128,128),offsets.Select(p=>new PointI(p.X+10,p.Y+10)).ToArray(),"test",1);
        var result=SearchEngine.Analyze(image,geometry,new([black,black],1));
        Check(result.Count>0 && result[0].Anchor==new PointI(50,70) && result[0].Matches==6,"six-slot optimum / duplicate targets do not inflate count");
        Check(result.All(r=>r.Slots.All(s=>s.Point.X>=1 && s.Point.X<127 && s.Point.Y>=1 && s.Point.Y<127)),"all six positions stay inside panel");
        Check(result.All(r=>r.Slots.All(s=>!geometry.BuildMask(128,128)[s.Point.Y*128+s.Point.X])),"original markers excluded");
        Check(result.Select(r=>r.Anchor).SequenceEqual(SearchEngine.Analyze(image,geometry,new([black],1)).Select(r=>r.Anchor)),"deterministic ranking");
        Check(result.SelectMany((a,i)=>result.Skip(i+1).Select(b=>Math.Pow(a.Anchor.X-b.Anchor.X,2)+Math.Pow(a.Anchor.Y-b.Anchor.Y,2)>=100)).All(v=>v),"candidate spatial diversity");
        Check(SearchEngine.Analyze(image,geometry,new([white],1)).Count==0,"no artificial fallback when nothing matches");
        var cancellation=new CancellationTokenSource();cancellation.Cancel();bool canceled=false;try{SearchEngine.Analyze(image,geometry,new([black]),cancellation.Token);}catch(OperationCanceledException){canceled=true;}Check(canceled,"cancellation honored");
        var dir=Path.Combine(Path.GetTempPath(),"DyeFinderTest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);var prefPath=Path.Combine(dir,"preferences.json");
        try
        {
            var store=new PreferenceStore(prefPath);var empty=new Preferences(1,[],12,true);store.Save(empty);Check(store.Load().Targets.Length==0,"deleted targets stay deleted");
            var namedStore=new PreferenceStore(Path.Combine(dir,"named.json"));namedStore.Save(new(1,[new(new(153,153,255),true,"Lavender / 淡紫")],5,false));
            Check(namedStore.Load().Targets[0].Name=="Lavender / 淡紫" && namedStore.Load().Targets[0].Color==new Rgb(153,153,255),"custom color names survive preference roundtrip");
            var legacy=JsonSerializer.Deserialize<Preferences>("{\"SchemaVersion\":1,\"Targets\":[{\"Color\":{\"R\":153,\"G\":153,\"B\":255},\"Enabled\":true}],\"Threshold\":5,\"PreferStable\":false}")!;
            Check(legacy.Valid && legacy.Targets[0].Name is null && legacy.Threshold==5,"existing unnamed custom colors remain compatible");
            File.WriteAllText(prefPath,"{broken");store=new(prefPath);store.Load();store.Save(Preferences.Default);Check(File.ReadAllText(prefPath)=="{broken" && store.Warning is not null,"corrupt preference file preserved");
            Check(!new Preferences(2,[],12,false).Valid && !new Preferences(1,[],double.NaN,false).Valid,"preference schema and numeric checks");
            Check(!JsonSerializer.Deserialize<Preferences>("{\"SchemaVersion\":1,\"Targets\":[null],\"Threshold\":15,\"PreferStable\":false}")!.Valid,"null target entry is safely rejected");
            var png=Path.Combine(dir,"roundtrip.png");ImageFiles.SavePng(ImageFiles.Bitmap(image),png);Check(ImageFiles.Open(png).pixels.Pixels.SequenceEqual(pixels),"PNG preserves raw sample values");
            var record=new CalibrationRecord(1,DateTimeOffset.UtcNow,"screenshot-srgb-v1","",new(128,128),geometry.Markers,"manual",[black],15,false,result[0],Enumerable.Repeat(white,6).ToArray(),true,"有帮助","test settings","test material","synthetic");
            var export=Path.Combine(dir,"sample.json");CalibrationExporter.Save(export,image,record);var saved=JsonSerializer.Deserialize<CalibrationRecord>(File.ReadAllText(export))!;
            Check(saved.ActualGameRgb![0]==white && saved.Suggestion.Slots[0].Color==black && saved.PanelSize==new PointI(128,128),"calibration preserves actual and predicted colors independently");
            Check(ImageFiles.Open(Path.Combine(dir,saved.PanelImage)).pixels.Pixels.SequenceEqual(pixels) && !Path.IsPathRooted(saved.PanelImage),"calibration includes only supplied panel with relative image link");
            bool rejected=false;try{CalibrationExporter.Save(export,image,record with{ActualValuesVerifiedAtSuggestedPosition=false});}catch(ArgumentException){rejected=true;}Check(rejected,"unconfirmed game values cannot become calibration labels");
            var trial=new TrialFeedback(2,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"0.3.0","ranker-v1",TrialOutcome.MatchedHere,1,2,new(2560,1440),"",null,"",record);
            var package=Path.Combine(dir,"feedback.zip");FeedbackPackage.Save(package,image,trial,image.Crop(new(5,7,20,24)));
            using(var archive=System.IO.Compression.ZipFile.OpenRead(package))
            {
                Check(archive.Entries.Select(e=>e.FullName).Order().SequenceEqual(new[]{"board.png","feedback.json","result-crop.png","summary.txt"}),"feedback ZIP contains only named crops, JSON and summary");
                using var reader=new StreamReader(archive.GetEntry("feedback.json")!.Open());var json=reader.ReadToEnd();var feedback=JsonSerializer.Deserialize<TrialFeedback>(json,FeedbackPackage.JsonOptions)!;
                Check(feedback.Outcome==TrialOutcome.MatchedHere && feedback.InspectedPoint==2 && feedback.AnalysisId==trial.AnalysisId,"feedback preserves outcome, inspected point and analysis grouping");
                Check(feedback.Recommendation.ActualGameRgb![0]==white && feedback.Recommendation.Suggestion.Slots[0].Color==black,"feedback keeps game RGB distinct from estimates");
                Check(feedback.BoardSha256.Length==64 && feedback.ResultImageRelationship.Contains("not automatically verified") && !json.Contains(dir),"feedback identifies board, labels final crop unverified, excludes local path");
                using var cropStream=archive.GetEntry("result-crop.png")!.Open();using var memory=new MemoryStream();cropStream.CopyTo(memory);memory.Position=0;
                var decoded=System.Windows.Media.Imaging.BitmapDecoder.Create(memory,System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                Check(decoded.Frames[0].PixelWidth==20 && decoded.Frames[0].PixelHeight==24,"attached image is selected crop only");
            }
            var before=File.ReadAllBytes(package);bool overwriteRejected=false;try{FeedbackPackage.Save(package,image,trial);}catch(IOException){overwriteRejected=true;}
            Check(overwriteRejected && before.SequenceEqual(File.ReadAllBytes(package)) && !Directory.GetFiles(dir,"*.tmp").Any(),"feedback never overwrites existing ZIP and cleans temporary output");
            foreach(var outcome in new[]{TrialOutcome.FoundNearby,TrialOutcome.NotUseful})
            {
                bool labelsRejected=false;try{FeedbackPackage.Save(Path.Combine(dir,"invalid.zip"),image,trial with{Outcome=outcome});}catch(ArgumentException){labelsRejected=true;}
                Check(labelsRejected && !File.Exists(Path.Combine(dir,"invalid.zip")),outcome+" cannot label original position with actual RGB");
                var simplePath=Path.Combine(dir,outcome+".zip");FeedbackPackage.Save(simplePath,image,trial with{Outcome=outcome,Recommendation=record with{ActualGameRgb=null,ActualValuesVerifiedAtSuggestedPosition=false}});
                using var simple=System.IO.Compression.ZipFile.OpenRead(simplePath);Check(simple.Entries.Count==3 && simple.GetEntry("result-crop.png")==null,outcome+" needs no screenshot or RGB");
            }
        }
        finally{foreach(var file in Directory.EnumerateFiles(dir))File.Delete(file);Directory.Delete(dir);}
        Check(GeometryDetector.Detect(image) is null,"non-dialog image has no automatic geometry");
        if(args.Length>0)
        {
            var anchors=new Dictionary<int,PointI>{[0]=new(106,216),[1]=new(1,216),[2]=new(176,216),[3]=new(176,108),[4]=new(1,102),[5]=new(88,97),[6]=new(1,1),[7]=new(86,1),[8]=new(176,1),[9]=new(53,162),[10]=new(46,153),[11]=new(53,213),[12]=new(71,106),[13]=new(73,105),[14]=new(111,155),[15]=new(91,171),[16]=new(92,171),[17]=new(88,165),[18]=new(130,83),[21]=new(112,151),[22]=new(113,151),[23]=new(115,151),[24]=new(117,151),[25]=new(119,152)};
            foreach(var pair in anchors)
            {
                var path=Path.Combine(args[0],$"2026_09_12_{pair.Key:0000}.png");var loaded=ImageFiles.Open(path);var g=GeometryDetector.Detect(loaded.pixels);
                Check(g is not null && g.Panel==new RectI(1127,512,256,256),$"sample {pair.Key:0000} panel: {g?.Panel}");
                Check(g is not null && Math.Abs(g.Markers[0].X-pair.Value.X)<=1 && Math.Abs(g.Markers[0].Y-pair.Value.Y)<=1,$"sample {pair.Key:0000} six-point anchor: {g?.Markers[0]}");
                if(g is not null)
                {
                    var found=SearchEngine.Analyze(loaded.pixels.Crop(g.Panel),g,new(Preferences.Default.Targets.Select(t=>t.Color).ToArray()));
                    Check(found.Count is >0 and <=5 && found.All(r=>r.Matches<=6),$"sample {pair.Key:0000} search");
                }
            }
        }
        if(args.Length>0)
        {
            foreach(var id in Enumerable.Range(32,5))
            {
                var path=Path.Combine(args[0],$"2026_09_12_{id:0000}.png");
                if(!File.Exists(path)){Console.WriteLine($"SKIP optional new-board sample {id}");continue;}
                var loaded=ImageFiles.Open(path);var g=GeometryDetector.Detect(loaded.pixels);
                Check(g?.Panel==new RectI(1127,512,256,256),$"new board {id}: detected panel");
                Check(g is not null && g.Markers.Length==6 && g.Offsets.SequenceEqual(new PointI[]{new(0,0),new(40,0),new(80,0),new(0,40),new(40,40),new(80,40)}),$"new board {id}: complete six-point geometry");
                Check(loaded.pixels[1236,807]==black,$"new board {id}: observed top-middle game swatch is 25,25,25");
                if(g is not null)
                {
                    var matches=SearchEngine.Analyze(loaded.pixels.Crop(g.Panel),g,new([black,white],5,MaxResults:3));
                    Check(matches.Count is >0 and <=3 && matches.All(m=>m.Slots.Length==6),$"new board {id}: close black/white search");
                }
            }
        }
        Console.WriteLine($"RESULT: {passed} passed, {failed} failed");return failed==0?0:1;
    }
}
