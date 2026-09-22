using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using DyeFinder.Core;

namespace DyeFinder.App;
public sealed class FeedbackWindow : Window
{
    private readonly TextBox actual=new(){AcceptsReturn=true,Height=105,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private readonly CheckBox verified=new(){Content=new TextBlock{Text="All six RGB values were checked at the original suggested position.",TextWrapping=TextWrapping.Wrap},Margin=new(0,8,0,12)};
    private readonly TextBox settings=new(){TextWrapping=TextWrapping.Wrap,Height=50};
    private readonly TextBox notes=new(){AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=60,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxLength=3000};
    private readonly TextBlock error=new(){TextWrapping=TextWrapping.Wrap,Foreground=System.Windows.Media.Brushes.Salmon};
    public Rgb[]? ActualColors {get;private set;}
    public TrialOutcome? Outcome {get;private set;}
    public PixelImage? Evidence {get;private set;}
    public string Settings=>settings.Text;
    public string Notes=>notes.Text;
    public FeedbackWindow(Suggestion suggestion)
    {
        Title="Record Result";Width=560;Height=Math.Min(650,SystemParameters.WorkArea.Height-24);MinWidth=500;MinHeight=450;WindowStartupLocation=WindowStartupLocation.CenterOwner;DarkTheme.Apply(this);
        var root=new DockPanel{Margin=new(22)};Content=root;
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        footer.Children.Add(error);
        footer.Children.Add(new TextBlock{Text="Saves a local ZIP with this recommendation and your feedback. Nothing is uploaded. Review the ZIP before sharing.",TextWrapping=TextWrapping.Wrap,Margin=new(0,10,0,5),Foreground=System.Windows.Media.Brushes.LightSlateGray,FontSize=12});
        var save=new Button{Content="Save Feedback",HorizontalContentAlignment=HorizontalAlignment.Center};footer.Children.Add(save);
        var panel=new StackPanel();root.Children.Add(new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        void Label(string text,int size=13)=>panel.Children.Add(new TextBlock{Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,6)});
        Label("How did this spot work?",20);
        Label($"Suggested Point 1: {suggestion.Anchor.X}, {suggestion.Anchor.Y}");
        foreach(var value in Enum.GetValues<TrialOutcome>())
        {
            var text=value switch{TrialOutcome.MatchedHere=>"Matched here — found a useful color at this spot",TrialOutcome.FoundNearby=>"Found nearby — needed a small adjustment",_=>"Not useful — no useful color found"};
            var radio=new RadioButton{Content=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap},GroupName="Outcome"};
            radio.Checked+=(_,_)=>{Outcome=value;actual.IsEnabled=verified.IsEnabled=value==TrialOutcome.MatchedHere;if(value!=TrialOutcome.MatchedHere){actual.Clear();verified.IsChecked=false;}};panel.Children.Add(radio);
        }
        Label("Notes (optional)");panel.Children.Add(notes);
        var attach=new Button{Content="Add Result Screenshot (optional)"};panel.Children.Add(attach);
        var evidenceLabel=new TextBlock{TextWrapping=TextWrapping.Wrap,FontSize=12};panel.Children.Add(evidenceLabel);
        var remove=new Button{Content="Remove screenshot",Visibility=Visibility.Collapsed};panel.Children.Add(remove);
        attach.Click+=(_,_)=>
        {
            var dialog=new OpenFileDialog{Title="Choose a screenshot of your game result",Filter="Images|*.png;*.jpg;*.jpeg;*.bmp"};if(dialog.ShowDialog(this)!=true)return;
            try{var source=ImageFiles.Open(dialog.FileName).pixels;var crop=new EvidenceWindow(source){Owner=this};if(crop.ShowDialog()==true){Evidence=crop.Crop;evidenceLabel.Text=$"Attached: {Evidence!.Width} × {Evidence.Height} crop. Position and RGB are unverified.";remove.Visibility=Visibility.Visible;}}
            catch(Exception ex) when(ex is IOException or NotSupportedException or ArgumentException){error.Text="Could not read that image. Try a PNG screenshot.";}
        };
        remove.Click+=(_,_)=>{Evidence=null;evidenceLabel.Text="";remove.Visibility=Visibility.Collapsed;};
        var details=new StackPanel{Margin=new(0,10,0,0)};
        details.Children.Add(new TextBlock{Text="Exact game RGB is optional. Only enter values checked at the suggested position; nearby results belong in the screenshot or notes.",TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,8)});
        actual.IsEnabled=verified.IsEnabled=false;actual.ToolTip="Six RGB lines: top row left to right, then bottom row.";details.Children.Add(actual);details.Children.Add(verified);
        details.Children.Add(new TextBlock{Text="Game settings (optional)",Margin=new(0,4,0,6)});settings.MaxLength=1500;details.Children.Add(settings);
        panel.Children.Add(new Expander{Header="More Details (optional)",Content=details,Margin=new(0,12,0,12)});
        save.Click+=(_,_)=>{if(TryValidate())DialogResult=true;};
    }
    public bool TryValidate()
    {
        error.Text="";ActualColors=null;
        if(Outcome is null){error.Text="Choose a result before saving.";return false;}
        if(!string.IsNullOrWhiteSpace(actual.Text))
        {
            var lines=actual.Text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries);var parsed=new List<Rgb>();
            foreach(var line in lines){if(!Rgb.TryParse(line,out var rgb)){error.Text="Use three whole numbers from 0 to 255 on each line.";return false;}parsed.Add(rgb);}
            if(parsed.Count!=6 || verified.IsChecked!=true || Outcome!=TrialOutcome.MatchedHere){error.Text="Enter six RGB lines and confirm the original suggested position.";return false;}ActualColors=parsed.ToArray();
        }
        return true;
    }
}
