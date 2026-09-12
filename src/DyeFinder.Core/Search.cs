namespace DyeFinder.Core;

public interface IColorSampler
{
    string Version { get; }
    Rgb Sample(PixelImage image,int x,int y);
}
public sealed class ScreenshotSampler : IColorSampler
{
    public string Version => "screenshot-srgb-v1";
    public Rgb Sample(PixelImage image,int x,int y) => image[x,y];
}
public sealed record SearchOptions(Rgb[] Targets,double Threshold=15,bool PreferStable=false,int MaxResults=5);
public sealed record SlotEstimate(int Index,PointI Point,Rgb Color,Rgb NearestTarget,double Difference);
public sealed record Suggestion(PointI Anchor,SlotEstimate[] Slots,int Matches,double Quality,double Stability);
public static class SearchEngine
{
    public static IReadOnlyList<Suggestion> Analyze(PixelImage image,Geometry geometry,SearchOptions options,CancellationToken token=default,IColorSampler? sampler=null)
    {
        if(options.Targets.Length==0 || options.Threshold<=0 || options.Threshold>100 || options.MaxResults<1 || options.MaxResults>20) throw new ArgumentException("Select a target and a valid tolerance");
        if(geometry.Markers.Length!=6 || geometry.Markers.Distinct().Count()!=6) throw new ArgumentException("Six different sampling points are required");
        sampler ??=new ScreenshotSampler(); var offsets=geometry.Offsets; var mask=geometry.BuildMask(image.Width,image.Height);
        var labs=options.Targets.Select(ColorMath.ToLab).ToArray();
        var values=new (double distance,int target)[image.Pixels.Length]; var colors=new Rgb[image.Pixels.Length];
        var cache=new Dictionary<Rgb,(double,int)>();
        for(int y=0;y<image.Height;y++)
        {
            token.ThrowIfCancellationRequested();
            for(int x=0;x<image.Width;x++)
            {
                var color=sampler.Sample(image,x,y); colors[y*image.Width+x]=color;
                if(!cache.TryGetValue(color,out var value))
                {
                    var lab=ColorMath.ToLab(color); double min=double.MaxValue; int nearest=0;
                    for(int i=0;i<labs.Length;i++){var d=ColorMath.Distance(lab,labs[i]);if(d<min){min=d;nearest=i;}}
                    value=(min,nearest); cache[color]=value;
                }
                values[y*image.Width+x]=value;
            }
        }
        (int matches,double quality)? At(int x,int y)
        {
            int hits=0;double quality=0;
            foreach(var p in offsets)
            {
                int px=x+p.X,py=y+p.Y;
                if(px<1 || py<1 || px>=image.Width-1 || py>=image.Height-1 || mask[py*image.Width+px]) return null;
                var d=values[py*image.Width+px].distance; if(d<=options.Threshold) hits++;
                quality+=Math.Exp(-Math.Pow(d/options.Threshold,2));
            }
            return (hits,quality/6);
        }
        var candidates=new List<(int x,int y,int hits,double quality,double stable)>();
        for(int y=1;y<image.Height-1;y++)
        {
            token.ThrowIfCancellationRequested();
            for(int x=1;x<image.Width-1;x++)
            {
                var v=At(x,y); if(v is null || v.Value.matches==0) continue;
                double stable=0;
                foreach(var n in new PointI[]{new(-1,0),new(1,0),new(0,-1),new(0,1)}) stable+=(At(x+n.X,y+n.Y)?.matches ?? 0)/6.0;
                candidates.Add((x,y,v.Value.matches,v.Value.quality,stable/4));
            }
        }
        var ordered=candidates.OrderByDescending(c=>c.hits).ThenByDescending(c=>options.PreferStable ? .6*c.stable+.4*c.quality : .85*c.quality+.15*c.stable).ThenBy(c=>c.y).ThenBy(c=>c.x);
        var result=new List<Suggestion>(); int separation=Math.Max(10,image.Width/20);
        foreach(var c in ordered)
        {
            if(result.Any(r=>Math.Pow(r.Anchor.X-c.x,2)+Math.Pow(r.Anchor.Y-c.y,2)<separation*separation)) continue;
            var slots=offsets.Select((p,i)=>{
                int x=c.x+p.X,y=c.y+p.Y; var v=values[y*image.Width+x];
                return new SlotEstimate(i+1,new(x,y),colors[y*image.Width+x],options.Targets[v.target],v.distance);
            }).ToArray();
            result.Add(new(new(c.x,c.y),slots,c.hits,c.quality,c.stable)); if(result.Count>=options.MaxResults) break;
        }
        return result;
    }
}
