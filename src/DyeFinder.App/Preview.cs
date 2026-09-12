using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DyeFinder.Core;

namespace DyeFinder.App;
public sealed class Preview : FrameworkElement
{
    public BitmapSource? Bitmap {get;set;}
    public RectI View {get;set;}
    public Geometry? Geometry {get;set;}
    public Suggestion? Selected {get;set;}
    public int ActivePoint {get;set;}=1;
    public bool IsDetail {get;set;}
    public event Action<int>? SuggestedPointChosen;
    public List<PointI> ManualPoints {get;}=[];
    public bool SelectRectangle {get;set;}
    public bool SelectPoints {get;set;}
    public event Action<RectI>? RectangleChosen;
    public event Action<PointI>? PointChosen;
    private Point? drag;
    private Point? end;
    public Preview(){MinHeight=150;ClipToBounds=true;RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.NearestNeighbor);}
    public (double scale,double left,double top) Transform()
    {
        if(View.Width<1 || View.Height<1) return(1,0,0);
        var scale=Math.Min(ActualWidth/View.Width,ActualHeight/View.Height);
        return(scale,(ActualWidth-View.Width*scale)/2,(ActualHeight-View.Height*scale)/2);
    }
    public Point ToImage(Point local)
    {var (s,x,y)=Transform();return new((local.X-x)/s+View.X,(local.Y-y)/s+View.Y);}
    private Point ToLocal(double x,double y){var(s,l,t)=Transform();return new(l+(x-View.X)*s,t+(y-View.Y)*s);}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(12,19,28)),null,new Rect(RenderSize));
        if(Bitmap is null || View.Width<1)
        {
            var text=new FormattedText(IsDetail ? "Point preview" : "Drop a screenshot here",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),IsDetail?12:18,Brushes.SlateGray,VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text,new Point(Math.Max(4,(ActualWidth-text.Width)/2),Math.Max(4,(ActualHeight-text.Height)/2)));return;
        }
        var(s,l,t)=Transform();dc.PushClip(new RectangleGeometry(new Rect(l,t,View.Width*s,View.Height*s)));
        if(IsDetail && Geometry is {} board){var corner=ToLocal(board.Panel.X,board.Panel.Y);dc.PushClip(new RectangleGeometry(new Rect(corner,new Size(board.Panel.Width*s,board.Panel.Height*s))));}
        dc.DrawImage(Bitmap,new Rect(l-View.X*s,t-View.Y*s,Bitmap.PixelWidth*s,Bitmap.PixelHeight*s));
        if(Geometry is { } g)
        {
            var a=ToLocal(g.Panel.X,g.Panel.Y);dc.DrawRectangle(null,new Pen(Brushes.Gold,1),new Rect(a,new Size(g.Panel.Width*s,g.Panel.Height*s)));
            foreach(var p in g.Markers) DrawPoint(dc,p,g.Panel,Brushes.Gold,null,4);
            if(Selected is { } result) foreach(var slot in result.Slots)
            {
                DrawPoint(dc,slot.Point,g.Panel,slot.Index==ActivePoint?Brushes.White:Brushes.Cyan,slot.Index.ToString(),5);
            }
            for(int i=0;i<ManualPoints.Count;i++) DrawPoint(dc,ManualPoints[i],g.Panel,Brushes.Magenta,(i+1).ToString(),5);
        }
        if(drag.HasValue && end.HasValue)
        {var a=ToLocal(drag.Value.X,drag.Value.Y);var b=ToLocal(end.Value.X,end.Value.Y);dc.DrawRectangle(null,new Pen(Brushes.Magenta,2),new Rect(a,b));}
        if(IsDetail && Geometry is not null)dc.Pop();
        dc.Pop();
    }
    private void DrawPoint(DrawingContext dc,PointI p,RectI panel,Brush brush,string? text,int radius)
    {
        var center=ToLocal(panel.X+p.X+.5,panel.Y+p.Y+.5);
        dc.DrawEllipse(null,new Pen(Brushes.Black,4),center,radius,radius);dc.DrawEllipse(null,new Pen(brush,2),center,radius,radius);
        if(text is not null)
        {
            var ft=new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),14,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip);
            var origin=LabelBounds(center).TopLeft;dc.DrawRectangle(Brushes.Black,null,new Rect(origin,new Size(ft.Width+4,ft.Height)));dc.DrawText(ft,origin);
        }
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);if(Bitmap is null) return;var p=ToImage(e.GetPosition(this));
        if(SelectRectangle){drag=p;end=p;CaptureMouse();}
        else if(SelectPoints && Geometry is {} g && g.Panel.Contains((int)p.X,(int)p.Y)) PointChosen?.Invoke(new((int)Math.Floor(p.X)-g.Panel.X,(int)Math.Floor(p.Y)-g.Panel.Y));
        else if(!SelectPoints)TryInspectAt(e.GetPosition(this));
    }
    public bool TryInspectAt(Point local)
    {
        var number=SuggestedPointAt(local);
        if(number is null)return false;
        SuggestedPointChosen?.Invoke(number.Value);return true;
    }
    public Rect LabelBounds(Point center)
    {
        var(s,l,t)=Transform();
        return new Rect(Math.Clamp(center.X+8,l,Math.Max(l,l+View.Width*s-22)),Math.Clamp(center.Y-19,t,Math.Max(t,t+View.Height*s-20)),22,20);
    }
    public int? SuggestedPointAt(Point local)
    {
        if(IsDetail || SelectRectangle || SelectPoints || Selected is null || Geometry is null || Bitmap is null)return null;
        if(local.X<0 || local.Y<0 || local.X>ActualWidth || local.Y>ActualHeight)return null;
        var hits=Selected.Slots.Select(slot=>{
            var center=ToLocal(Geometry.Panel.X+slot.Point.X+.5,Geometry.Panel.Y+slot.Point.Y+.5);
            var distance=(local-center).Length;
            var labelHit=LabelBounds(center).Contains(local);
            return(slot.Index,distance,hit:distance<=15 || labelHit);
        });
        return hits.Where(h=>h.hit).OrderBy(h=>h.distance).Select(h=>(int?)h.Index).FirstOrDefault();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {if(drag.HasValue){end=ToImage(e.GetPosition(this));InvalidateVisual();}else Cursor=SuggestedPointAt(e.GetPosition(this)) is not null?Cursors.Hand:SelectRectangle||SelectPoints?Cursors.Cross:Cursors.Arrow;}
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if(!drag.HasValue || Bitmap is null) return;
        var p=ToImage(e.GetPosition(this));var a=drag.Value;drag=null;end=null;ReleaseMouseCapture();
        int left=Math.Clamp((int)Math.Floor(Math.Min(a.X,p.X)),0,Bitmap.PixelWidth-1),top=Math.Clamp((int)Math.Floor(Math.Min(a.Y,p.Y)),0,Bitmap.PixelHeight-1);
        int right=Math.Clamp((int)Math.Ceiling(Math.Max(a.X,p.X)),left+1,Bitmap.PixelWidth),bottom=Math.Clamp((int)Math.Ceiling(Math.Max(a.Y,p.Y)),top+1,Bitmap.PixelHeight);
        InvalidateVisual();RectangleChosen?.Invoke(new(left,top,right-left,bottom-top));
    }
}
