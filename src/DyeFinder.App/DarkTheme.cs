using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DyeFinder.App;
public static class DarkTheme
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    public static void Apply(Window window)
    {
        window.Background=new SolidColorBrush(Color.FromRgb(13,21,32));
        window.Foreground=new SolidColorBrush(Color.FromRgb(223,232,241));
        window.FontFamily=new FontFamily("Segoe UI");window.FontSize=13;
        window.Icon=BitmapFrame.Create(new Uri("pack://application:,,,/VindictusDyeFinder;component/Assets/DyeFinder.ico"));
        window.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/VindictusDyeFinder;component/DarkControls.xaml",UriKind.Relative)});
        window.SourceInitialized+=(_,_)=>ApplyCaption(window);
        window.Loaded+=(_,_)=>ApplyCaption(window);
        window.Activated+=(_,_)=>ApplyCaption(window);
    }
    private static void ApplyCaption(Window window)
    {
        // WPF can initialize native theme attributes after SourceInitialized; apply again when shown.
        // Keep the native frame, resize controls and snap behavior. Older OS builds may decline attributes.
        var hwnd=new WindowInteropHelper(window).Handle;
        int dark=1,caption=0x20150D,text=0xF1E8DF;
        DwmSetWindowAttribute(hwnd,20,ref dark,sizeof(int));
        DwmSetWindowAttribute(hwnd,35,ref caption,sizeof(int));
        DwmSetWindowAttribute(hwnd,36,ref text,sizeof(int));
    }
    public static bool CaptionRequestAccepted(Window window)
    {
        ApplyCaption(window);
        var hwnd=new WindowInteropHelper(window).Handle;
        int expected=0x20150D;return DwmSetWindowAttribute(hwnd,35,ref expected,sizeof(int))==0;
    }
}
