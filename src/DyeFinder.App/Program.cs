using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DyeFinder.App;
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        bool smoke=args.Length>=2 && args[0]=="--smoke-test";int code=0;
        var app=new Application();var window=new MainWindow(smoke);
        if(smoke)window.Loaded+=async(_,_)=>
        {
            try
            {
                var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
                if(!window.FeedbackEnabled || window.FeedbackAction!="Report a Problem" || window.CurrentProblemStage!=ProblemStage.NoScreenshot)throw new Exception("Smoke: startup problem reporting");
                if(args.Length>=3)await window.ImportAsync(args[2]);else window.LoadDemo();
                if(window.CurrentGeometry is null)throw new Exception("Smoke: geometry missing");
                await window.AnalyzeAsync();if(window.ResultCount==0)throw new Exception("Smoke: no results");
                if(window.ResultCount>3 || window.SelectedPointIndex!=1)throw new Exception("Smoke: simple result defaults");
                if(window.FeedbackAction!="Record Result" || !window.ClosestTargetText.StartsWith("Closest to "))throw new Exception("Smoke: result feedback and target identification");
                await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                void Snapshot(string name,Window? target=null)
                {target ??=window;target.UpdateLayout();var dpi=VisualTreeHelper.GetDpi(target);var bitmap=new RenderTargetBitmap((int)(target.ActualWidth*dpi.DpiScaleX),(int)(target.ActualHeight*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);bitmap.Render(target);ImageFiles.SavePng(bitmap,Path.Combine(output,name));}
                Snapshot("prototype.png");
                if(OperatingSystem.IsWindowsVersionAtLeast(10,0,22000) && !DarkTheme.CaptionRequestAccepted(window))throw new Exception("Smoke: native dark caption request rejected");
                var feedback=new FeedbackWindow(window.CurrentSuggestion!){Owner=window};feedback.Show();feedback.UpdateLayout();
                if(feedback.TryValidate())throw new Exception("Smoke: feedback requires an outcome");
                static IEnumerable<T> FindVisuals<T>(DependencyObject root) where T:DependencyObject
                {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var nested in FindVisuals<T>(child))yield return nested;}}
                var radios=FindVisuals<System.Windows.Controls.RadioButton>(feedback).ToArray();
                if(radios.Length!=3)throw new Exception("Smoke: three feedback outcomes");
                foreach(var radio in radios){radio.IsChecked=true;if(!feedback.TryValidate() || feedback.ActualColors is not null)throw new Exception("Smoke: quick feedback should need no RGB");}
                Snapshot("feedback.png",feedback);feedback.Close();
                var scroller=FindVisuals<System.Windows.Controls.ScrollViewer>(window).First(s=>s.ScrollableHeight>0);scroller.ScrollToEnd();window.UpdateLayout();
                if(scroller.VerticalOffset<=0)throw new Exception("Smoke: dark scroller navigation");scroller.ScrollToHome();window.UpdateLayout();
                void CheckPointClicks()
                {
                    var original=window.CurrentSuggestion!;var preview=window.MainPreview;var g=window.CurrentGeometry!;
                    foreach(var slot in original.Slots)
                    {
                        var (scale,left,top)=preview.Transform();
                        var click=new Point(left+(g.Panel.X+slot.Point.X+.5-preview.View.X)*scale,top+(g.Panel.Y+slot.Point.Y+.5-preview.View.Y)*scale);
                        if(!preview.TryInspectAt(click) || window.SelectedPointIndex!=slot.Index)throw new Exception("Smoke: clicking point "+slot.Index);
                        var zoom=window.ZoomView;
                        if(zoom.X+18!=g.Panel.X+slot.Point.X || zoom.Y+18!=g.Panel.Y+slot.Point.Y)throw new Exception("Smoke: zoom center "+slot.Index);
                        if(!ReferenceEquals(original,window.CurrentSuggestion))throw new Exception("Smoke: inspecting changed recommendation");
                        var label=preview.LabelBounds(click);
                        if(!preview.TryInspectAt(new Point(label.X+4,label.Y+9)) || window.SelectedPointIndex!=slot.Index)throw new Exception("Smoke: number label hit target "+slot.Index);
                    }
                    if(preview.TryInspectAt(new Point(-10,-10)))throw new Exception("Smoke: outside click changed selection");
                    preview.SelectPoints=true;if(preview.TryInspectAt(new Point(20,20)))throw new Exception("Smoke: calibration mode priority");preview.SelectPoints=false;
                }
                CheckPointClicks();Snapshot("point-6.png");
                window.Width=1100;window.Height=680;window.UpdateLayout();CheckPointClicks();Snapshot("compact.png");
                window.Width=980;window.Height=580;window.UpdateLayout();CheckPointClicks();Snapshot("minimum-size.png");
                window.Width=1280;window.Height=850;window.UpdateLayout();
                window.MainPreview.View=new(0,0,window.MainPreview.Bitmap!.PixelWidth,window.MainPreview.Bitmap.PixelHeight);CheckPointClicks();
                window.SelectSuggestion(0);if(window.SelectedPointIndex!=1)throw new Exception("Smoke: suggestion resets inspection to point 1");
                window.ChangeThresholdForTest(16);if(window.ResultCount!=0)throw new Exception("Smoke: stale results after rule edit");
                var originalGeometry=window.CurrentGeometry!;window.ManualPanel(originalGeometry.Panel);foreach(var point in originalGeometry.Markers)window.ManualPoint(point);
                if(window.CurrentGeometry?.Markers.Length!=6 || window.CurrentGeometry.Source!="Manual calibration")throw new Exception("Smoke: manual calibration");
                var pending=window.AnalyzeAsync();window.LoadDemo();await pending;if(window.ResultCount!=0)throw new Exception("Smoke: stale results after image replacement");
                if(window.CurrentGeometry?.Panel!=new DyeFinder.Core.RectI(0,0,256,256) || !window.Status.StartsWith("Real game example"))throw new Exception("Smoke: real example detection");
                await window.AnalyzeAsync();if(window.ResultCount==0)throw new Exception("Smoke: demo search");
                var testColor=window.CurrentSuggestion!.Slots.First(s=>s.Color!=new DyeFinder.Core.Rgb(25,25,25) && ColorLabels.Name(s.Color) is null).Color;
                window.SetOnlyTargetForTest(testColor);await window.AnalyzeAsync();
                if(window.ResultCount==0 || window.ClosestTargetText!=$"Closest to RGB {testColor}")throw new Exception("Smoke: unnamed target RGB fallback");
                Snapshot("unnamed-color.png");
                const string longName="Lavender armor with a long custom name";
                window.SetOnlyTargetForTest(new DyeFinder.Core.Rgb(153,153,255),longName);window.ChangeThresholdForTest(40);await window.AnalyzeAsync();
                if(window.ResultCount==0 || window.ClosestTargetText!="Closest to "+longName)throw new Exception("Smoke: custom target name");
                window.Width=980;window.Height=580;window.UpdateLayout();Snapshot("long-name-compact.png");window.Width=1280;window.Height=850;window.UpdateLayout();
                window.SetOnlyTargetForTest(new DyeFinder.Core.Rgb(255,0,255));window.ChangeThresholdForTest(1);await window.AnalyzeAsync();
                if(window.ResultCount!=0 || window.StatusColorForTest!=Color.FromRgb(255,227,160))throw new Exception("Smoke: no-match warning");
                if(!window.FeedbackEnabled || window.FeedbackAction!="Report a Problem" || window.CurrentProblemStage!=ProblemStage.NoMatches || window.ClosestTargetText!="")throw new Exception("Smoke: no-match problem reporting");
                var problem=new ProblemFeedbackWindow(window.CurrentProblemStage){Owner=window};problem.Show();Snapshot("problem-report.png",problem);problem.Close();
                Snapshot("no-matches.png");window.ChangeThresholdForTest(2);
                if(window.StatusColorForTest==Color.FromRgb(255,227,160))throw new Exception("Smoke: stale warning color");
                await window.ImportAsync(Path.Combine(output,"missing-image.png"));
                if(!window.FeedbackEnabled || window.CurrentProblemStage!=ProblemStage.ScreenshotUnreadable)throw new Exception("Smoke: unreadable screenshot reporting");
                window.ChangeThresholdForTest(4);if(window.CurrentProblemStage!=ProblemStage.ScreenshotUnreadable)throw new Exception("Smoke: unreadable image state survives rule edits");
                var plain=Path.Combine(output,"plain-image.png");ImageFiles.SavePng(ImageFiles.Bitmap(new DyeFinder.Core.PixelImage(100,100,Enumerable.Repeat(new DyeFinder.Core.Rgb(80,80,80),10000).ToArray())),plain);await window.ImportAsync(plain);
                if(window.CurrentGeometry is not null || window.CurrentProblemStage!=ProblemStage.BoardNotDetected || !window.FeedbackEnabled)throw new Exception("Smoke: undetected board reporting");
                window.ChangeThresholdForTest(3);if(window.CurrentProblemStage!=ProblemStage.BoardNotDetected)throw new Exception("Smoke: board failure survives rule edits");
                File.WriteAllText(Path.Combine(output,"smoke-test.txt"),"PASS: import, geometry, search, maximum three suggestions, all six point/label hit targets, zoom centers, unchanged recommendation coordinates, compact/full-image transforms, point reset, WPF render at 1280x850 / 1100x680 / 980x580, native dark caption (Windows 11), dark scrollbar navigation, three feedback outcomes with no required RGB, required outcome validation, rule invalidation, manual six-point calibration, image-replacement cancellation, demo search, closest target labels, unnamed RGB fallback, startup/no-match/unreadable/undetected problem reporting.");
            }
            catch(Exception ex){code=1;Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"smoke-test-error.txt"),ex.ToString());}
            finally{window.Close();}
        };
        app.Run(window);return code;
    }
}
