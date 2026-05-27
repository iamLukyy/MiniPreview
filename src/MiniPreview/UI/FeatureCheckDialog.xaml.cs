using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MiniPreview.Bootstrap;
using MiniPreview.Localization;

namespace MiniPreview.UI;

public partial class FeatureCheckDialog : Window
{
    private ProbeResult[] _probes;

    public FeatureCheckDialog(ProbeResult[] probes)
    {
        InitializeComponent();
        _probes = probes;
        Title              = Strings.T("featurecheck.title");
        IntroText.Text     = Strings.T("featurecheck.intro");
        RecheckBtn.Content = Strings.T("featurecheck.recheck");
        TryAnywayBtn.Content = Strings.T("featurecheck.tryAnyway");
        ExitBtn.Content    = Strings.T("featurecheck.exit");
        Render();
    }

    private void Render()
    {
        ProbesList.Items.Clear();
        foreach (var p in _probes)
        {
            var border = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 6),
                Background = p.Ok ? Brushes.Honeydew : Brushes.MistyRose
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = $"{(p.Ok ? "✓" : "✗")}  {p.Name}", FontWeight = FontWeights.Bold });
            stack.Children.Add(new TextBlock { Text = p.Detail, TextWrapping = TextWrapping.Wrap });
            if (!p.Ok && !string.IsNullOrEmpty(p.FixCommand))
            {
                var fixPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
                var box = new TextBox
                {
                    Text = p.FixCommand,
                    IsReadOnly = true,
                    FontFamily = new FontFamily("Consolas"),
                    Background = Brushes.White,
                    MinWidth = 400
                };
                var copyBtn = new Button { Content = Strings.T("featurecheck.copy"), Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2) };
                copyBtn.Click += (_, _) => { Clipboard.SetText(p.FixCommand); copyBtn.Content = Strings.T("featurecheck.copied"); };
                fixPanel.Children.Add(box);
                fixPanel.Children.Add(copyBtn);
                stack.Children.Add(fixPanel);
            }
            border.Child = stack;
            ProbesList.Items.Add(border);
        }

        // Disable TryAnyway only when the WGC probe fails (capture is required)
        var wgcProbe = _probes.FirstOrDefault(p => p.Name == "WGC");
        TryAnywayBtn.IsEnabled = wgcProbe?.Ok ?? true;
    }

    private void OnRecheck(object sender, RoutedEventArgs e)
    {
        _probes = FeatureChecker.CreateDefault().RunProbes();
        if (FeatureChecker.AllOk(_probes)) { DialogResult = true; Close(); return; }
        Render();
    }

    private void OnTryAnyway(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void OnExit(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
