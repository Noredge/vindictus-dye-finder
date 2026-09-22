using System.Windows;
using System.Windows.Controls;
using DyeFinder.Core;

namespace DyeFinder.App;
public sealed class EvidenceWindow : Window
{
    public PixelImage? Crop {get;private set;}
    public EvidenceWindow(PixelImage source, bool problemReport=false)
    {
        Title=problemReport?"Choose the problem area":"Choose the result area";Width=760;Height=Math.Min(680,SystemParameters.WorkArea.Height-24);MinWidth=520;MinHeight=400;WindowStartupLocation=WindowStartupLocation.CenterOwner;DarkTheme.Apply(this);
        var root=new DockPanel{Margin=new(20)};Content=root;
        var instructions=problemReport?"Drag around the area that shows the problem. Only your chosen crop will be saved. Leave out chat, names and other personal details.":"Drag around the dye panel, including its six crosses and RGB labels. Only your chosen crop will be saved. Leave out chat, names and other personal details.";
        var hint=new TextBlock{Text=instructions,TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,12)};
        DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
        var reset=new Button{Content="Select again",Margin=new(0,8,8,0)};var accept=new Button{Content="Use this crop",IsEnabled=false,Margin=new(0,8,0,0)};actions.Children.Add(reset);actions.Children.Add(accept);
        var preview=new Preview{Bitmap=ImageFiles.Bitmap(source),View=new(0,0,source.Width,source.Height),SelectRectangle=true};root.Children.Add(preview);
        preview.RectangleChosen+=rect=>{if(rect.Width<16 || rect.Height<16){hint.Text="Select an area at least 16 × 16 pixels.";return;}Crop=source.Crop(rect);preview.View=rect;preview.SelectRectangle=false;preview.InvalidateVisual();accept.IsEnabled=true;hint.Text="Review this crop. Only this area will be attached. Its contents, RGB and position are not automatically verified.";};
        reset.Click+=(_,_)=>{Crop=null;accept.IsEnabled=false;preview.View=new(0,0,source.Width,source.Height);preview.SelectRectangle=true;preview.InvalidateVisual();hint.Text=instructions;};
        accept.Click+=(_,_)=>{if(Crop is not null)DialogResult=true;};
    }
}
