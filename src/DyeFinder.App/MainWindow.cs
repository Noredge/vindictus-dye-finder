using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using DyeFinder.Core;

namespace DyeFinder.App;
public sealed class MainWindow : Window
{
    private readonly PreferenceStore? store;
    private readonly StackPanel targetList=new();
    private readonly List<(Rgb color,CheckBox check)> targets=[];
    private readonly Slider threshold=new(){Minimum=1,Maximum=40,TickFrequency=1,IsSnapToTickEnabled=true,Value=15,Margin=new(0,8,0,8)};
    private readonly TextBlock thresholdLabel=new();
    private readonly CheckBox preferStable=new(){Content="Prefer easier alignment",Margin=new(0,8,0,12)};
    private readonly Button analyze=new(){Content="Find Matches",MinHeight=44};
    private readonly Button cancel=new(){Content="Cancel",IsEnabled=false};
    private readonly Button feedback=new(){Content="Record Result",IsEnabled=false};
    private readonly string feedbackDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VindictusDyeFinder","feedback");
    private readonly Guid sessionId=Guid.NewGuid();
    private Guid analysisId=Guid.NewGuid();
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock imageInfo=new(){Text="Open or drop a screenshot here. You can also paste with Ctrl+V.",TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock selectionInfo=new(){Text="Your suggested points will appear on the image.",TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock detailInfo=new(){Text="Select a spot to inspect its colors.",TextWrapping=TextWrapping.Wrap};
    private readonly StackPanel results=new();
    private readonly Preview preview=new();
    private readonly Preview detail=new(){Height=145,IsDetail=true};
    private BitmapSource? bitmap;
    private PixelImage? pixels;
    private Geometry? geometry;
    private IReadOnlyList<Suggestion> suggestions=[];
    private Suggestion? selected;
    private CancellationTokenSource? work;
    private int revision;
    private bool initialized;
    private bool busy;
    private string sourceName="";
    public int ResultCount=>suggestions.Count;
    public Geometry? CurrentGeometry=>geometry;
    public string Status=>status.Text;

    public MainWindow(bool smoke=false)
    {
        Title="Vindictus Dye Finder · Preview 0.3.2";Width=1280;Height=850;MinWidth=1100;MinHeight=720;
        DarkTheme.Apply(this);
        Background=Brush("#0D1520");Foreground=Brush("#DFE8F1");FontFamily=new FontFamily("Segoe UI");FontSize=13;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;AllowDrop=true;
        if(!smoke) store=new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VindictusDyeFinder","preferences.json"));
        var pref=store?.Load() ?? Preferences.Default;
        SetupStyles(); Content=Layout();
        foreach(var target in pref.Targets) AddTarget(target.Color,target.Enabled,target.Name);
        threshold.Value=pref.Threshold;preferStable.IsChecked=pref.PreferStable;
        threshold.ValueChanged+=(_,_)=>RulesChanged();preferStable.Checked+=(_,_)=>RulesChanged();preferStable.Unchecked+=(_,_)=>RulesChanged();
        analyze.Click+=async(_,_)=>await AnalyzeAsync();cancel.Click+=(_,_)=>{InvalidateResults();SetStatus("Search canceled. Change your colors or try again.");};
        feedback.Click+=(_,_)=>ExportFeedback();
        preview.SuggestedPointChosen+=InspectPoint;preview.RectangleChosen+=ManualPanel;preview.PointChosen+=ManualPoint;
        DragOver+=(_,e)=>{e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
        Drop+=async(_,e)=>{if(e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length>0) await ImportAsync(files[0]);};
        PreviewKeyDown+=async(_,e)=>{if(e.Key==Key.V && Keyboard.Modifiers==ModifierKeys.Control && Keyboard.FocusedElement is not TextBox){e.Handled=true;await PasteAsync();}};
        Closed+=(_,_)=>{work?.Cancel();SavePreferences();};
        initialized=true;InvalidateResults();UpdateThreshold();UpdateEnabled();SetStatus(store?.Warning ?? "Start with Open Screenshot or Try Demo. Everything stays on your computer.");
    }
    private static SolidColorBrush Brush(string hex)=>(SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    private void SetupStyles()
    {
        var button=new Style(typeof(Button));button.Setters.Add(new Setter(Control.BackgroundProperty,Brush("#20364C")));button.Setters.Add(new Setter(Control.ForegroundProperty,Brush("#F4F8FC")));button.Setters.Add(new Setter(Control.BorderBrushProperty,Brush("#3C5A73")));button.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(12,8,12,8)));button.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,3,6,3)));
        var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(4));border.SetValue(Border.BorderThicknessProperty,new Thickness(1));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(presenter);
        var template=new ControlTemplate(typeof(Button)){VisualTree=border};var hover=new Trigger{Property=IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,Brush("#2D4C66")));template.Triggers.Add(hover);var disabled=new Trigger{Property=IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(Control.BackgroundProperty,Brush("#182532")));disabled.Setters.Add(new Setter(Control.ForegroundProperty,Brush("#728697")));template.Triggers.Add(disabled);button.Setters.Add(new Setter(Control.TemplateProperty,template));Resources.Add(typeof(Button),button);
        var text=new Style(typeof(TextBox));text.Setters.Add(new Setter(Control.BackgroundProperty,Brush("#101E2C")));text.Setters.Add(new Setter(Control.ForegroundProperty,Brush("#F4F8FC")));text.Setters.Add(new Setter(Control.BorderBrushProperty,Brush("#3C5A73")));text.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(7)));text.Setters.Add(new Setter(TextBox.CaretBrushProperty,Brushes.White));Resources.Add(typeof(TextBox),text);
        var check=new Style(typeof(CheckBox));check.Setters.Add(new Setter(Control.ForegroundProperty,Brush("#DFE8F1")));Resources.Add(typeof(CheckBox),check);
    }
    private static TextBlock Label(string text,int size=13,string color="#DFE8F1")=>new(){Text=text,FontSize=size,Foreground=Brush(color),TextWrapping=TextWrapping.Wrap,Margin=new(0,5,0,5)};
    private static Border Card(UIElement content)=>new(){Background=Brush("#152332"),CornerRadius=new(8),Padding=new(16),Margin=new(6),Child=content};
    private Button ActionButton(string text,Action action){var b=new Button{Content=text};b.Click+=(_,_)=>action();return b;}
    private Button AsyncButton(string text,Func<Task> action){var b=new Button{Content=text};b.Click+=async(_,_)=>await action();return b;}
    private readonly Expander advanced = new() { Header="Advanced", IsExpanded=false, Margin=new(0,16,0,0), Foreground=Brush("#AAC0D2") };
    private readonly TextBlock technicalInfo = new() { TextWrapping=TextWrapping.Wrap, FontSize=12 };
    private readonly TextBlock zoomTitle = new() { Text="Point 1", FontSize=18, Margin=new(0,6,0,8) };
    public int SelectedPointIndex => preview.ActivePoint;
    public RectI ZoomView => detail.View;
    public Suggestion? CurrentSuggestion => selected;
    public Preview MainPreview => preview;
    private UIElement Layout()
    {
        var root=new Grid{Margin=new(18)};
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new());root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new DockPanel{Margin=new(6,0,6,14)};
        var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(actions,Dock.Right);
        actions.Children.Add(AsyncButton("Open Screenshot",OpenAsync));actions.Children.Add(ActionButton("Try Demo",LoadDemo));header.Children.Add(actions);
        var title=new StackPanel();title.Children.Add(Label("Dye Finder",26));title.Children.Add(Label("Open a screenshot. Pick your colors. Find a spot.",13,"#8CAAC3"));header.Children.Add(title);root.Children.Add(header);
        var body=new Grid();Grid.SetRow(body,1);body.ColumnDefinitions.Add(new(){Width=new GridLength(254)});body.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});body.ColumnDefinitions.Add(new(){Width=new GridLength(285)});root.Children.Add(body);
        var controls=new StackPanel();controls.Children.Add(Label("Choose Colors",18));controls.Children.Add(Label("Pick one or more.",12,"#91A8BA"));controls.Children.Add(targetList);
        var addPanel=new StackPanel();var customName=new TextBox{MaxLength=40,ToolTip="Optional color name",Margin=new(0,0,0,4)};
        addPanel.Children.Add(Label("Name (optional)",11,"#91A8BA"));addPanel.Children.Add(customName);
        var custom=new TextBox{ToolTip="Three numbers, each 0–255",Margin=new(0,8,0,4)};
        addPanel.Children.Add(Label("RGB, for example: 200 130 35",11,"#91A8BA"));addPanel.Children.Add(custom);
        addPanel.Children.Add(ActionButton("Add",()=>{if(!Rgb.TryParse(custom.Text,out var color)){SetStatus("Enter three numbers from 0 to 255, such as 200 130 35.");return;}if(targets.Count>=30){SetStatus("You can save up to 30 colors.");return;}if(targets.Any(t=>t.color==color)){SetStatus("That color is already in your list. Click its name to rename it.");return;}AddTarget(color,true,customName.Text);custom.Clear();customName.Clear();RulesChanged();}));
        controls.Children.Add(new Expander{Header="Add Color",Foreground=Foreground,Content=addPanel,Margin=new(0,12,0,14)});
        var extra=new StackPanel{Margin=new(0,10,0,0)};
        extra.Children.Add(preferStable);
        extra.Children.Add(Label("Fix detection",15));extra.Children.Add(Label("The yellow rings should cover the original six crosses.",12,"#91A8BA"));
        extra.Children.Add(ActionButton("Select Color Board",StartManualPanel));extra.Children.Add(ActionButton("Mark Six Points",StartManualPoints));
        extra.Children.Add(ActionButton("Full Image / Board",()=>{if(bitmap is null)return;preview.View=preview.View.Width==bitmap.PixelWidth && geometry is not null?geometry.Panel:new(0,0,bitmap.PixelWidth,bitmap.PixelHeight);preview.InvalidateVisual();}));
        extra.Children.Add(Label("Selected suggestion",15));extra.Children.Add(technicalInfo);advanced.Content=extra;controls.Children.Add(advanced);
        var leftBody=new DockPanel();
        var quickControls=new StackPanel{Margin=new(0,12,0,0)};DockPanel.SetDock(quickControls,Dock.Bottom);
        quickControls.Children.Add(thresholdLabel);quickControls.Children.Add(threshold);quickControls.Children.Add(Label("Higher = more color variation",11,"#91A8BA"));quickControls.Children.Add(analyze);quickControls.Children.Add(cancel);leftBody.Children.Add(quickControls);
        leftBody.Children.Add(new ScrollViewer{Content=controls,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        var left=Card(leftBody);body.Children.Add(left);
        var middle=new Grid();middle.RowDefinitions.Add(new(){Height=GridLength.Auto});middle.RowDefinitions.Add(new());middle.RowDefinitions.Add(new(){Height=GridLength.Auto});middle.RowDefinitions.Add(new(){Height=GridLength.Auto});
        imageInfo.FontSize=12;imageInfo.Foreground=Brush("#91A8BA");middle.Children.Add(imageInfo);
        Grid.SetRow(preview,1);preview.Margin=new(0,10,0,10);middle.Children.Add(preview);
        Grid.SetRow(selectionInfo,2);selectionInfo.Margin=new(0,0,0,12);middle.Children.Add(selectionInfo);
        var zoom=new DockPanel();Grid.SetRow(zoom,3);detail.Width=145;detail.Height=145;detail.MinHeight=145;detail.Margin=new(0,0,18,0);DockPanel.SetDock(detail,Dock.Left);zoom.Children.Add(detail);
        var zoomText=new StackPanel{VerticalAlignment=VerticalAlignment.Center};zoomText.Children.Add(zoomTitle);zoomText.Children.Add(detailInfo);zoomText.Children.Add(Label("Click any numbered point to inspect it here.",12,"#91A8BA"));zoom.Children.Add(zoomText);middle.Children.Add(zoom);
        var center=Card(middle);Grid.SetColumn(center,1);body.Children.Add(center);
        var right=new DockPanel();var rh=new StackPanel();DockPanel.SetDock(rh,Dock.Top);rh.Children.Add(Label("Suggested Spots",18));rh.Children.Add(Label("Choose a spot to see its six points.",12,"#91A8BA"));right.Children.Add(rh);
        var foot=new StackPanel();foot.Children.Add(Label("Estimated colors. Preview in game.",12,"#91A8BA"));foot.Children.Add(feedback);foot.Children.Add(ActionButton("Open Feedback Folder",OpenFeedbackFolder));DockPanel.SetDock(foot,Dock.Bottom);right.Children.Add(foot);right.Children.Add(new ScrollViewer{Content=results,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});var rc=Card(right);Grid.SetColumn(rc,2);body.Children.Add(rc);
        var footer=Card(status);Grid.SetRow(footer,2);root.Children.Add(footer);return root;
    }
    private void AddTarget(Rgb color,bool enabled,string? name=null)
    {
        var line=new DockPanel{Margin=new(0,7,0,7)};var remove=ActionButton("×",()=>{var t=targets.First(t=>t.color==color);targets.Remove(t);targetList.Children.Remove(line);RulesChanged();});remove.Padding=new(6,1,6,1);remove.ToolTip="Remove this color";DockPanel.SetDock(remove,Dock.Right);line.Children.Add(remove);
        var check=new CheckBox{IsChecked=enabled,Tag=string.IsNullOrWhiteSpace(name)?null:name.Trim(),VerticalContentAlignment=VerticalAlignment.Center};
        var content=new StackPanel{Orientation=Orientation.Horizontal};content.Children.Add(new Border{Width=22,Height=22,Background=new SolidColorBrush(Color.FromRgb(color.R,color.G,color.B)),BorderBrush=Brush("#7490A6"),BorderThickness=new(1),Margin=new(0,0,9,0)});
        var caption=new StackPanel{Width=106};var nameText=new TextBlock{Text=check.Tag as string ?? ColorName(color),FontSize=13,TextTrimming=TextTrimming.CharacterEllipsis};
        var rename=new Button{Content=nameText,Padding=new(0),Margin=new(0),BorderThickness=new(0),Background=Brushes.Transparent,HorizontalContentAlignment=HorizontalAlignment.Left,ToolTip=$"{nameText.Text}\nClick to rename"};
        rename.Click+=(_,_)=>{var editor=new ColorNameWindow(check.Tag as string ?? ColorName(color)){Owner=this};if(editor.ShowDialog()!=true)return;check.Tag=editor.ColorName;nameText.Text=editor.ColorName ?? ColorName(color);rename.ToolTip=$"{nameText.Text}\nClick to rename";SavePreferences();};
        caption.Children.Add(rename);caption.Children.Add(new TextBlock{Text=color.ToString(),FontSize=11,Foreground=Brush("#91A8BA")});content.Children.Add(caption);check.Content=content;line.Children.Add(check);targetList.Children.Add(line);targets.Add((color,check));check.Checked+=(_,_)=>RulesChanged();check.Unchecked+=(_,_)=>RulesChanged();
    }
    private static string ColorName(Rgb c) => c switch { {R:230,G:230,B:230} => "White", {R:25,G:25,B:25} => "Black", {R:200,G:130,B:35} => "Gold", {R:140,G:36,B:35} => "Deep Red", {R:184,G:125,B:46} => "Amber", {R:171,G:40,B:38} => "Red", _ => "Custom Color" };
    private void SetStatus(string text,bool warning=false){status.Text=text;status.Foreground=warning?Brush("#FFE3A0"):Brush("#DFE8F1");status.FontWeight=warning?FontWeights.SemiBold:FontWeights.Normal;}
    private void UpdateThreshold()=>thresholdLabel.Text=$"Color Tolerance: {threshold.Value:0} (ΔE00)";
    private void SavePreferences(){if(initialized)store?.Save(new(1,targets.Select(t=>new TargetSetting(t.color,t.check.IsChecked==true,t.check.Tag as string)).ToArray(),threshold.Value,preferStable.IsChecked==true));}
    private void RulesChanged(){if(!initialized)return;UpdateThreshold();InvalidateResults();SavePreferences();SetStatus("Colors or options changed. Click Find Matches to update."+(store?.Warning is {} w?" "+w:""));}
    public void InvalidateResults()
    {
        revision++;work?.Cancel();work=null;busy=false;suggestions=[];selected=null;results.Children.Clear();results.Children.Add(Label("Your suggestions will appear here.",13,"#91A8BA"));preview.ActivePoint=1;detail.ActivePoint=1;zoomTitle.Text="Point 1";technicalInfo.Text="No spot selected.";preview.Selected=null;detail.Selected=null;detail.Bitmap=null;preview.InvalidateVisual();detail.InvalidateVisual();selectionInfo.Text="Your suggested points will appear on the image.";detailInfo.Text="Select a spot to inspect its colors.";UpdateEnabled();
    }
    private void UpdateEnabled(){analyze.IsEnabled=!busy && pixels is not null && geometry?.Markers.Length==6 && targets.Any(t=>t.check.IsChecked==true);cancel.IsEnabled=busy;cancel.Visibility=busy?Visibility.Visible:Visibility.Collapsed;feedback.IsEnabled=selected is not null;}
    private async Task OpenAsync()
    {var dialog=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*",Title="Open a game screenshot"};if(dialog.ShowDialog(this)==true)await ImportAsync(dialog.FileName);}
    public async Task ImportAsync(string path)
    {
        InvalidateResults();geometry=null;pixels=null;bitmap=null;preview.Bitmap=null;preview.Geometry=null;preview.ManualPoints.Clear();preview.SelectRectangle=false;preview.SelectPoints=false;preview.InvalidateVisual();UpdateEnabled();SetStatus("Opening image…");
        var current=revision;var cancellation=new CancellationTokenSource();work=cancellation;busy=true;UpdateEnabled();
        try
        {
            var loaded=await Task.Run(()=>{var image=ImageFiles.Open(path);var g=GeometryDetector.Detect(image.pixels,cancellation.Token);return(image,g);},cancellation.Token);
            if(current!=revision)return;AcceptImage(loaded.image.bitmap,loaded.image.pixels,loaded.g,Path.GetFileName(path));
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) when(ex is IOException or FileFormatException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException or OverflowException){if(current==revision)SetStatus("Could not open image: "+ex.Message);}
        finally{if(current==revision){busy=false;work=null;UpdateEnabled();}cancellation.Dispose();}
    }
    private void AcceptImage(BitmapSource bmp,PixelImage data,Geometry? g,string name)
    {
        bitmap=bmp;pixels=data;geometry=g;sourceName=name;preview.Bitmap=bmp;preview.Geometry=g;preview.View=g?.Panel ?? new(0,0,data.Width,data.Height);preview.SelectPoints=false;preview.SelectRectangle=false;preview.InvalidateVisual();
        imageInfo.Text=$"{name}  ·  {data.Width} × {data.Height}\n"+(g is null?"Board not found. Use Advanced to select it.":$"{g.Source}");
        if(g is null)advanced.IsExpanded=true;SetStatus(g is null?"Board not found. Use Advanced > Select Color Board, then mark the six crosses.":"Check the yellow rings, choose colors, then click Find Matches.");UpdateEnabled();
    }
    private async Task PasteAsync()
    {
        try
        {
            var source=Clipboard.GetImage();if(source is null){SetStatus("There is no image on the clipboard.");return;}
            var loaded=ImageFiles.Convert(source);InvalidateResults();geometry=null;pixels=null;bitmap=null;preview.Bitmap=null;preview.Geometry=null;preview.ManualPoints.Clear();preview.SelectPoints=false;preview.SelectRectangle=false;preview.InvalidateVisual();
            var current=revision;var cancellation=new CancellationTokenSource();work=cancellation;busy=true;UpdateEnabled();SetStatus("Reading pasted image…");
            try{var g=await Task.Run(()=>GeometryDetector.Detect(loaded.pixels,cancellation.Token),cancellation.Token);if(current==revision)AcceptImage(loaded.bitmap,loaded.pixels,g,"Pasted image");}
            catch(OperationCanceledException){}
            finally{if(current==revision){busy=false;work=null;UpdateEnabled();}cancellation.Dispose();}
        }
        catch(Exception ex) when(ex is System.Runtime.InteropServices.COMException or InvalidDataException or NotSupportedException or ArgumentException){SetStatus("Could not paste image: "+ex.Message);}
    }
    public async Task AnalyzeAsync()
    {
        if(pixels is null || geometry is null || geometry.Markers.Length!=6)return;
        var options=new SearchOptions(targets.Where(t=>t.check.IsChecked==true).Select(t=>t.color).ToArray(),threshold.Value,preferStable.IsChecked==true,MaxResults:3);if(options.Targets.Length==0)return;
        InvalidateResults();var current=revision;var data=pixels.Crop(geometry.Panel);var g=geometry;var cancellation=new CancellationTokenSource();work=cancellation;busy=true;UpdateEnabled();SetStatus("Finding matching spots…");
        try
        {
            var found=await Task.Run(()=>SearchEngine.Analyze(data,g,options,cancellation.Token),cancellation.Token);
            if(current!=revision)return;analysisId=Guid.NewGuid();suggestions=found;ShowResults();
            if(found.Count==0){results.Children.Add(Label("No matches found",17,"#FFE3A0"));results.Children.Add(Label("No suggested spot meets your selected colors and current tolerance. Try increasing Color Tolerance or choosing another color.",13,"#FFE3A0"));}
            SetStatus(found.Count==0?"No matches found at this tolerance. Increase Color Tolerance or choose another color.":$"Found {found.Count} spots. Try one in game, then use Record Result.",warning:found.Count==0);
        }
        catch(OperationCanceledException){}
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException){if(current==revision)SetStatus("Search could not finish: "+ex.Message);}
        finally{if(current==revision){busy=false;work=null;UpdateEnabled();}cancellation.Dispose();}
    }
    private void ShowResults()
    {
        results.Children.Clear();for(int i=0;i<suggestions.Count;i++)
        {
            var result=suggestions[i];var index=i;var card=new StackPanel();card.Children.Add(Label($"{(i==0 ? "Try this first" : $"Alternative {i}")}",15));
            card.Children.Add(Label($"{result.Matches} of 6 slots close to your colors",11,"#A6C0D3"));
            card.Children.Add(Label("Estimated RGB",11,"#91A8BA"));
            var swatches=new System.Windows.Controls.Primitives.UniformGrid{Columns=3,Rows=2};
            foreach(var slot in result.Slots)
            {
                var cell=new StackPanel{Margin=new(2,0,2,5),ToolTip=$"Point {slot.Index}\nEstimate: {slot.Color}\nClosest target: {slot.NearestTarget}\nΔE00 {slot.Difference:0.0}"};
                var strip=new DockPanel();
                var number=new TextBlock{Text=slot.Index.ToString(),FontSize=11,Foreground=Brush("#DFE8F1"),Width=14,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(number,Dock.Left);strip.Children.Add(number);
                strip.Children.Add(new Border{Background=new SolidColorBrush(Color.FromRgb(slot.Color.R,slot.Color.G,slot.Color.B)),Height=17,BorderBrush=slot.Difference<=threshold.Value?Brushes.Cyan:Brush("#758190"),BorderThickness=new(1)});
                cell.Children.Add(strip);
                cell.Children.Add(new TextBlock{Text=$"{slot.Color.R},{slot.Color.G},{slot.Color.B}",FontSize=11,Foreground=Brush("#DFE8F1"),Margin=new(0,3,0,0)});
                swatches.Children.Add(cell);
            }
            card.Children.Add(swatches);
            var b=new Button{Content=card,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new(9),Margin=new(0,4,0,5)};b.Click+=(_,_)=>SelectSuggestion(index);results.Children.Add(b);
        }
        if(suggestions.Count>0)SelectSuggestion(0);
    }
    public void SelectSuggestion(int index)
    {
        if(geometry is null || bitmap is null || index<0 || index>=suggestions.Count)return;selected=suggestions[index];preview.Selected=selected;preview.View=geometry.Panel;preview.InvalidateVisual();
        for(int i=0;i<results.Children.Count;i++)if(results.Children[i] is Button button)button.BorderBrush=i==index?Brushes.Cyan:Brush("#3C5A73");
        var p=selected.Anchor;var old=geometry.Markers[0];
        selectionInfo.Text=$"Spot {index+1}  ·  White = selected  ·  Cyan = suggested  ·  Yellow = original";
        technicalInfo.Text=$"Point 1: ({p.X}, {p.Y}) from the board top-left\nMove from original Point 1: X {p.X-old.X:+0;-0;0}, Y {p.Y-old.Y:+0;-0;0} px\nNearby matching slots: {selected.Stability*100:0}% (not a probability)";
        InspectPoint(1);UpdateEnabled();
    }
    public void InspectPoint(int number)
    {
        if(selected is null || geometry is null || bitmap is null || number<1 || number>6)return;
        var slot=selected.Slots.First(s=>s.Index==number);var p=slot.Point;
        preview.ActivePoint=number;preview.InvalidateVisual();
        detail.Bitmap=bitmap;detail.Geometry=geometry;detail.Selected=selected;detail.ActivePoint=number;
        // Keep the selected pixel centered even near an edge; the preview clips image bounds.
        detail.View=new(geometry.Panel.X+p.X-18,geometry.Panel.Y+p.Y-18,37,37);detail.InvalidateVisual();
        zoomTitle.Text=$"Point {number}";
        detailInfo.Text=$"Estimated RGB\n{slot.Color}\n\nBoard position: {p.X}, {p.Y}";
    }
    private void StartManualPanel()
    {
        if(bitmap is null){SetStatus("Open a screenshot first.");return;}InvalidateResults();geometry=null;preview.Geometry=null;preview.ManualPoints.Clear();preview.SelectPoints=false;preview.SelectRectangle=true;preview.View=new(0,0,bitmap.PixelWidth,bitmap.PixelHeight);preview.InvalidateVisual();UpdateEnabled();SetStatus("Drag around the colorful square only. Leave out its border and the six color swatches below.");
    }
    public void ManualPanel(RectI rect)
    {
        if(pixels is null)return;
        if(rect.Width<64 || rect.Height<64 || rect.Width>768 || rect.Height>768 || rect.X<0 || rect.Y<0 || rect.X+rect.Width>pixels.Width || rect.Y+rect.Height>pixels.Height){SetStatus("Choose a board inside the image, 64–768 pixels wide and tall.");return;}
        InvalidateResults();geometry=new(rect,[],"Manual calibration",1);preview.Geometry=geometry;preview.View=rect;preview.SelectRectangle=false;StartManualPoints();
    }
    private void StartManualPoints()
    {
        if(geometry is null){SetStatus("Select the color board first.");return;}InvalidateResults();geometry=geometry with{Markers=[]};preview.Geometry=geometry;preview.View=geometry.Panel;preview.ManualPoints.Clear();preview.SelectRectangle=false;preview.SelectPoints=true;preview.InvalidateVisual();UpdateEnabled();SetStatus("Click the six original crosses: top row left to right, then bottom row (1 of 6).");
    }
    public void ManualPoint(PointI point)
    {
        if(geometry is null || !preview.SelectPoints || point.X<0 || point.Y<0 || point.X>=geometry.Panel.Width || point.Y>=geometry.Panel.Height)return;
        if(preview.ManualPoints.Contains(point)){SetStatus("That point is already marked. Click the next cross.");return;}preview.ManualPoints.Add(point);preview.InvalidateVisual();
        if(preview.ManualPoints.Count==6)
        {geometry=geometry with{Markers=preview.ManualPoints.ToArray(),Source="Manual calibration",Confidence=1};preview.Geometry=geometry;preview.ManualPoints.Clear();preview.SelectPoints=false;imageInfo.Text=$"{sourceName} · Manually selected board";SetStatus("Six points marked. Check the yellow rings, then click Find Matches.");UpdateEnabled();}
        else SetStatus($"Click original cross {preview.ManualPoints.Count+1} of 6.");
    }
    public void LoadDemo()
    {
        InvalidateResults();
        var resource=Application.GetResourceStream(new Uri("pack://application:,,,/VindictusDyeFinder;component/Assets/DemoScreenshot.png"))!;
        using var stream=resource.Stream;
        var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        var loaded=ImageFiles.Convert(decoder.Frames[0]);var g=GeometryDetector.Detect(loaded.pixels);
        if(g is null)throw new InvalidDataException("The built-in example could not be detected.");
        preview.ManualPoints.Clear();AcceptImage(loaded.bitmap,loaded.pixels,g,"Real example");
        SetStatus("Real game example loaded. Choose colors and click Find Matches. Other dye boards need their own screenshot.");
    }
    private void ExportFeedback()
    {
        if(selected is null || pixels is null || geometry is null)return;
        var editor=new FeedbackWindow(selected){Owner=this};if(editor.ShowDialog()!=true)return;
        try
        {
            var record=new CalibrationRecord(1,DateTimeOffset.UtcNow,new ScreenshotSampler().Version,"",new(geometry.Panel.Width,geometry.Panel.Height),geometry.Markers,geometry.Source,targets.Where(t=>t.check.IsChecked==true).Select(t=>t.color).ToArray(),threshold.Value,preferStable.IsChecked==true,selected,editor.ActualColors,editor.ActualColors is not null,FeedbackPackage.OutcomeLabel(editor.Outcome!.Value),editor.Settings,editor.Notes,sourceName=="Real example"?"bundled-real-demo":"user-image");
            var id=Guid.NewGuid();var trial=new TrialFeedback(2,id,analysisId,sessionId,"0.3.2","ranker-v1",editor.Outcome.Value,suggestions.ToList().IndexOf(selected)+1,preview.ActivePoint,new(pixels.Width,pixels.Height),"",null,"",record);
            var path=Path.Combine(feedbackDirectory,$"dye-feedback-{DateTime.Now:yyyyMMdd-HHmmss}-{id.ToString("N")[..8]}.zip");
            FeedbackPackage.Save(path,pixels.Crop(geometry.Panel),trial,editor.Evidence);SetStatus("Feedback saved locally. Use Open Feedback Folder to review or share the ZIP.");
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){SetStatus("Could not export feedback: "+ex.Message);}
    }
    private void OpenFeedbackFolder()
    {
        try{Directory.CreateDirectory(feedbackDirectory);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(feedbackDirectory){UseShellExecute=true});}
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException){SetStatus("Could not open the feedback folder: "+feedbackDirectory);}
    }
    public void ChangeThresholdForTest(double value)=>threshold.Value=value;
    public void SetOnlyTargetForTest(Rgb color){foreach(var target in targets)target.check.IsChecked=false;AddTarget(color,true,"Test color");RulesChanged();}
    public Color StatusColorForTest=>((SolidColorBrush)status.Foreground).Color;
    private sealed class UniformGridEx : System.Windows.Controls.Primitives.UniformGrid {public UniformGridEx(){Columns=6;Rows=1;}}
}
