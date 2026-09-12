using System.Windows;
using System.Windows.Controls;

namespace DyeFinder.App;
public sealed class ColorNameWindow : Window
{
    public string? ColorName {get;private set;}
    public ColorNameWindow(string name)
    {
        Title="Rename Color";Width=390;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;DarkTheme.Apply(this);
        var panel=new StackPanel{Margin=new(20)};Content=panel;
        panel.Children.Add(new TextBlock{Text="Color name",Margin=new(0,0,0,8)});
        var input=new TextBox{Text=name,MaxLength=40};panel.Children.Add(input);
        panel.Children.Add(new TextBlock{Text="Up to 40 characters. Leave blank to use the default name.",TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new(0,8,0,10)});
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};panel.Children.Add(buttons);
        var cancel=new Button{Content="Cancel",IsCancel=true,Margin=new(0,0,8,0)};var save=new Button{Content="Save",IsDefault=true};buttons.Children.Add(cancel);buttons.Children.Add(save);
        save.Click+=(_,_)=>{ColorName=string.IsNullOrWhiteSpace(input.Text)?null:input.Text.Trim();DialogResult=true;};
        Loaded+=(_,_)=>{input.Focus();input.SelectAll();};
    }
}
