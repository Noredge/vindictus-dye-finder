using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DyeFinder.Core;

namespace DyeFinder.App;
public static class ImageFiles
{
    public static (BitmapSource bitmap,PixelImage pixels) Open(string path)
    {
        using var stream=File.OpenRead(path);
        var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        return Convert(decoder.Frames[0]);
    }
    public static (BitmapSource bitmap,PixelImage pixels) Convert(BitmapSource source)
    {
        if(source.PixelWidth<16 || source.PixelHeight<16 || (long)source.PixelWidth*source.PixelHeight>24_000_000) throw new InvalidDataException("Use an image at least 16 x 16 and no larger than 24 megapixels.");
        var bitmap=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0); bitmap.Freeze();
        var bytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(bytes,bitmap.PixelWidth*4,0);
        var pixels=new Rgb[bytes.Length/4];
        for(int i=0;i<pixels.Length;i++)
        {
            // Composite transparent imports onto black consistently for display and sampling.
            var a=bytes[i*4+3]/255.0;
            pixels[i]=new((byte)Math.Round(bytes[i*4+2]*a),(byte)Math.Round(bytes[i*4+1]*a),(byte)Math.Round(bytes[i*4]*a));
        }
        var image=new PixelImage(bitmap.PixelWidth,bitmap.PixelHeight,pixels);
        return(Bitmap(image),image);
    }
    public static BitmapSource Bitmap(PixelImage image)
    {
        var data=new byte[image.Width*image.Height*3];for(int i=0;i<image.Pixels.Length;i++){data[3*i]=image.Pixels[i].R;data[3*i+1]=image.Pixels[i].G;data[3*i+2]=image.Pixels[i].B;}
        var bitmap=BitmapSource.Create(image.Width,image.Height,96,96,PixelFormats.Rgb24,null,data,image.Width*3);bitmap.Freeze();return bitmap;
    }
    public static void SavePng(BitmapSource image,string path)
    { var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);encoder.Save(file); }
}
