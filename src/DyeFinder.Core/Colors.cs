namespace DyeFinder.Core;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public override string ToString() => $"{R}, {G}, {B}";
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";
    public static bool TryParse(string text, out Rgb rgb)
    {
        rgb = default;
        var p = text.Split([' ', ',', '，', ';', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (p.Length != 3 || !byte.TryParse(p[0], out var r) || !byte.TryParse(p[1], out var g) || !byte.TryParse(p[2], out var b)) return false;
        rgb = new(r, g, b); return true;
    }
}
public readonly record struct Lab(double L, double A, double B);
public static class ColorMath
{
    public static Lab ToLab(Rgb c)
    {
        static double Linear(byte n) { var v=n/255.0; return v<=.04045 ? v/12.92 : Math.Pow((v+.055)/1.055,2.4); }
        static double F(double t) => t>216.0/24389 ? Math.Cbrt(t) : (24389.0/27*t+16)/116;
        var r=Linear(c.R); var g=Linear(c.G); var b=Linear(c.B);
        var x=F((r*.4124564+g*.3575761+b*.1804375)/.95047);
        var y=F(r*.2126729+g*.7151522+b*.0721750);
        var z=F((r*.0193339+g*.1191920+b*.9503041)/1.08883);
        return new(116*y-16,500*(x-y),200*(y-z));
    }
    // CIEDE2000, kL=kC=kH=1; validated against Sharma et al. reference pairs.
    public static double Distance(Lab p, Lab q)
    {
        const double rad=Math.PI/180;
        var c1=Math.Sqrt(p.A*p.A+p.B*p.B); var c2=Math.Sqrt(q.A*q.A+q.B*q.B);
        var cb=(c1+c2)/2; var pow=Math.Pow(cb,7);
        var g=.5*(1-Math.Sqrt(pow/(pow+Math.Pow(25,7))));
        var a1=(1+g)*p.A; var a2=(1+g)*q.A;
        c1=Math.Sqrt(a1*a1+p.B*p.B); c2=Math.Sqrt(a2*a2+q.B*q.B);
        static double Hue(double a,double b) { var h=Math.Atan2(b,a)*180/Math.PI; return h<0 ? h+360 : h; }
        var h1=Hue(a1,p.B); var h2=Hue(a2,q.B);
        var dl=q.L-p.L; var dc=c2-c1; var dh=h2-h1;
        if (c1*c2==0) dh=0; else if (dh>180) dh-=360; else if(dh< -180) dh+=360;
        var dH=2*Math.Sqrt(c1*c2)*Math.Sin(dh/2*rad);
        var lb=(p.L+q.L)/2; cb=(c1+c2)/2;
        var hb=c1*c2==0 ? h1+h2 : Math.Abs(h1-h2)<=180 ? (h1+h2)/2 : (h1+h2<360 ? (h1+h2+360)/2 : (h1+h2-360)/2);
        var t=1-.17*Math.Cos((hb-30)*rad)+.24*Math.Cos(2*hb*rad)+.32*Math.Cos((3*hb+6)*rad)-.20*Math.Cos((4*hb-63)*rad);
        var sl=1+.015*(lb-50)*(lb-50)/Math.Sqrt(20+(lb-50)*(lb-50));
        var sc=1+.045*cb; var sh=1+.015*cb*t;
        var rc=2*Math.Sqrt(Math.Pow(cb,7)/(Math.Pow(cb,7)+Math.Pow(25,7)));
        var rt=-rc*Math.Sin(60*Math.Exp(-Math.Pow((hb-275)/25,2))*rad);
        return Math.Sqrt(Math.Max(0,Math.Pow(dl/sl,2)+Math.Pow(dc/sc,2)+Math.Pow(dH/sh,2)+rt*(dc/sc)*(dH/sh)));
    }
}
