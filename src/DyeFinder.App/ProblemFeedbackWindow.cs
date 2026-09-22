using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using DyeFinder.Core;

namespace DyeFinder.App;

public sealed class ProblemFeedbackWindow : Window
{
    private readonly TextBox notes = new()
    {
        AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 95, MaxLength = 3000,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };
    private readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Salmon };
    public string Notes => notes.Text;
    public PixelImage? Evidence { get; private set; }

    public ProblemFeedbackWindow(ProblemStage stage)
    {
        Title = "Report a Problem";
        Width = 520;
        MinWidth = 360;
        MinHeight = 320;
        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 24);
        Height = Math.Min(510, MaxHeight);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        DarkTheme.Apply(this);
        var root = new DockPanel { Margin = new(22) };
        Content = root;
        var footer = new StackPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        footer.Children.Add(error);
        footer.Children.Add(new TextBlock
        {
            Text = "Saves a local ZIP with basic app context and your notes. Only a screenshot crop you choose and review is attached. Nothing is uploaded.",
            FontSize = 12, Foreground = Brushes.LightSlateGray, TextWrapping = TextWrapping.Wrap,
            Margin = new(0, 12, 0, 8)
        });
        var actions = new Grid();
        actions.ColumnDefinitions.Add(new());
        actions.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var save = new Button { Content = "Save Problem Report", IsDefault = true, Margin = new(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new(0) };
        actions.Children.Add(save);
        Grid.SetColumn(cancel, 1);
        actions.Children.Add(cancel);
        footer.Children.Add(actions);
        var panel = new StackPanel();
        root.Children.Add(new ScrollViewer
        {
            Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        panel.Children.Add(new TextBlock { Text = "What went wrong?", FontSize = 21, Margin = new(0, 0, 0, 10) });
        panel.Children.Add(new TextBlock
        {
            Text = ProblemFeedbackPackage.StageLabel(stage), Foreground = Brushes.LightGoldenrodYellow,
            FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 18)
        });
        panel.Children.Add(new TextBlock { Text = "Notes (optional)", Margin = new(0, 0, 0, 6) });
        notes.ToolTip = "What did you try, and what did you expect to happen?";
        panel.Children.Add(notes);
        var attach = new Button { Content = "Add Screenshot (optional)", Margin = new(0, 14, 0, 0) };
        panel.Children.Add(attach);
        var evidenceLabel = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new(0, 6, 0, 0) };
        panel.Children.Add(evidenceLabel);
        var remove = new Button { Content = "Remove screenshot", Visibility = Visibility.Collapsed };
        panel.Children.Add(remove);
        attach.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "Choose a screenshot to explain the problem",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp"
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                var source = ImageFiles.Open(dialog.FileName).pixels;
                var review = new EvidenceWindow(source, problemReport: true) { Owner = this };
                if (review.ShowDialog() == true && review.Crop is { } crop)
                {
                    Evidence = crop;
                    evidenceLabel.Text = $"Attached: {crop.Width} × {crop.Height} reviewed crop. Contents are unverified.";
                    remove.Visibility = Visibility.Visible;
                    error.Text = "";
                }
            }
            catch (Exception ex) when (ex is IOException or FileFormatException or NotSupportedException or ArgumentException or UnauthorizedAccessException)
            {
                error.Text = "Could not read that image. Try a PNG screenshot.";
            }
        };
        remove.Click += (_, _) =>
        {
            Evidence = null;
            evidenceLabel.Text = "";
            remove.Visibility = Visibility.Collapsed;
        };
        save.Click += (_, _) => DialogResult = true;
    }
}
