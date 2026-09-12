namespace DyeFinder.Core;

public readonly record struct PointI(int X,int Y);
public readonly record struct RectI(int X,int Y,int Width,int Height)
{
    public bool Contains(int x,int y) => x>=X && y>=Y && x<X+Width && y<Y+Height;
}
public sealed class PixelImage(int width,int height,Rgb[] pixels)
{
    public int Width { get; }=width;
    public int Height { get; }=height;
    public Rgb[] Pixels { get; }=pixels.Length==checked(width*height) ? pixels : throw new ArgumentException("Pixel count");
    public Rgb this[int x,int y] => Pixels[y*Width+x];
    public PixelImage Crop(RectI rect)
    {
        if(rect.Width<1 || rect.Height<1 || rect.X<0 || rect.Y<0 || rect.X+rect.Width>Width || rect.Y+rect.Height>Height) throw new ArgumentException("The selected area is outside the image");
        var data=new Rgb[rect.Width*rect.Height];
        for(int y=0;y<rect.Height;y++) Array.Copy(Pixels,(rect.Y+y)*Width+rect.X,data,y*rect.Width,rect.Width);
        return new(rect.Width,rect.Height,data);
    }
}
public sealed record Geometry(RectI Panel,PointI[] Markers,string Source,double Confidence)
{
    public PointI[] Offsets => Markers.Select(p=>new PointI(p.X-Markers[0].X,p.Y-Markers[0].Y)).ToArray();
    public bool[] BuildMask(int width,int height)
    {
        var mask=new bool[width*height]; var radius=Math.Max(5,(int)Math.Round(5*Panel.Width/256.0));
        foreach(var p in Markers)
            for(int y=Math.Max(0,p.Y-radius);y<=Math.Min(height-1,p.Y+radius);y++)
                for(int x=Math.Max(0,p.X-radius);x<=Math.Min(width-1,p.X+radius);x++) mask[y*width+x]=true;
        return mask;
    }
}
public static class GeometryDetector
{
    private sealed record Box(int X,int Y,int W,int H,Rgb Color);
    public static Geometry? Detect(PixelImage image,CancellationToken token=default)
    {
        // Discover flat swatch interiors, independent of the dialog's screen position.
        var active=new Dictionary<(int,int,Rgb),Box>(); var boxes=new List<Box>();
        for(int y=0;y<image.Height;y++)
        {
            token.ThrowIfCancellationRequested(); var next=new Dictionary<(int,int,Rgb),Box>();
            for(int x=0;x<image.Width;)
            {
                var start=x; var color=image[x,y]; while(++x<image.Width && image[x,y]==color) { }
                var w=x-start; if(w<18 || w>100) continue;
                var key=(start,w,color);
                next[key]=active.TryGetValue(key,out var previous) ? previous with {H=previous.H+1} : new(start,y,w,1,color);
            }
            foreach(var pair in active) if(!next.ContainsKey(pair.Key) && Math.Abs(pair.Value.H-pair.Value.W)<=3) boxes.Add(pair.Value);
            active=next;
        }
        boxes.AddRange(active.Values.Where(b=>Math.Abs(b.H-b.W)<=3));
        foreach(var first in boxes)
        {
            var s=first.W/33.0; var dx=91*s; var dy=53*s; var tol=Math.Max(3,3*s);
            bool Near(double x,double y) => boxes.Any(b=>Math.Abs(b.W-first.W)<=2 && Math.Abs(b.X-x)<=tol && Math.Abs(b.Y-y)<=tol);
            if(!Near(first.X+dx,first.Y) || !Near(first.X+2*dx,first.Y) || !Near(first.X,first.Y+dy) || !Near(first.X+dx,first.Y+dy) || !Near(first.X+2*dx,first.Y+dy)) continue;
            var panel=new RectI((int)Math.Round(first.X-s),(int)Math.Round(first.Y-280*s),(int)Math.Round(256*s),(int)Math.Round(256*s));
            if(panel.X<0 || panel.Y<0 || panel.X+panel.Width>image.Width || panel.Y+panel.Height>image.Height) continue;
            var detected=FindMarkers(image.Crop(panel),s,token);
            if(detected is not null) return new(panel,detected.Value.points,"Board detected · check yellow rings",detected.Value.confidence);
        }
        return null;
    }
    private static (PointI[] points,double confidence)? FindMarkers(PixelImage image,double scale,CancellationToken token)
    {
        var step=(int)Math.Round(40*scale); var size=Math.Max(4,(int)Math.Round(8*scale));
        var score=new double[image.Width*image.Height];
        // A yellow cross raises R/G relative to B on its arms, compared with its four corners.
        for(int y=0;y<=image.Height-size;y++) for(int x=0;x<=image.Width-size;x++)
        {
            double arms=0,corners=0; int na=0,nc=0;
            for(int j=0;j<size;j++) for(int i=0;i<size;i++)
            {
                var c=image[x+i,y+j]; var yellow=Math.Min(c.R,c.G)-c.B;
                var cross=(i>=size*3/8 && i<size*5/8)||(j>=size*3/8 && j<size*5/8);
                if(cross){arms+=yellow;na++;} else {corners+=yellow;nc++;}
            }
            score[y*image.Width+x]=arms/na-corners/nc;
        }
        double best=double.NegativeInfinity; int bx=0,bestY=0;
        for(int y=-size/2;y<image.Height-step;y++)
        {
            token.ThrowIfCancellationRequested();
            for(int x=-size/2;x<image.Width-2*step;x++)
            {
                double sum=0;int count=0;
                for(int row=0;row<2;row++) for(int col=0;col<3;col++)
                { int px=x+col*step,py=y+row*step; if(px<0 || py<0 || px>image.Width-size || py>image.Height-size) continue;sum+=score[py*image.Width+px];count++; }
                if(count<2) continue; var value=sum/count;
                if(value>best){best=value;bx=x;bestY=y;}
            }
        }
        if(best<8) return null;
        var points=(from row in Enumerable.Range(0,2) from col in Enumerable.Range(0,3) select new PointI(bx+size/2+col*step,bestY+size/2+row*step)).ToArray();
        return (points,Math.Clamp(best/50,0,1));
    }
}
