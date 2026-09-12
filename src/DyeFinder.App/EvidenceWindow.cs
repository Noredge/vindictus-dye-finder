using System.Windows;
using System.Windows.Controls;
using DyeFinder.Core;

namespace DyeFinder.App;
public sealed class EvidenceWindow : Window
{
    public PixelImage? Crop {get;private set;}
    public EvidenceWindow(PixelImage source)
    {
        Title="Choose the result area";Width=760;Height=680;MinWidth=520;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;DarkTheme.Apply(this);
        var root=new DockPanel{Margin=new(20)};Content=root;
        var hint=new TextBlock{Text="Drag around the dye panel, including its six crosses and RGB labels. Only your chosen crop will be saved. Leave out chat, names and other personal details.",TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,12)};
        DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
        var reset=new Button{Content="Select again",Margin=new(0,8,8,0)};var accept=new Button{Content="Use this crop",IsEnabled=false,Margin=new(0,8,0,0)};actions.Children.Add(reset);actions.Children.Add(accept);
        var preview=new Preview{Bitmap=ImageFiles.Bitmap(source),View=new(0,0,source.Width,source.Height),SelectRectangle=true};root.Children.Add(preview);
        preview.RectangleChosen+=rect=>{if(rect.Width<16 || rect.Height<16){hint.Text="Select an area at least 16 × 16 pixels.";return;}Crop=source.Crop(rect);preview.View=rect;preview.SelectRectangle=false;preview.InvalidateVisual();accept.IsEnabled=true;hint.Text="Review this crop. It will be attached as a final game screenshot, without automatic RGB or position verification.";};
        reset.Click+=(_,_)=>{Crop=null;accept.IsEnabled=false;preview.View=new(0,0,source.Width,source.Height);preview.SelectRectangle=true;preview.InvalidateVisual();hint.Text="Drag around the dye panel and its RGB labels.";};
        accept.Click+=(_,_)=>{if(Crop is not null)DialogResult=true;};
    }
}
