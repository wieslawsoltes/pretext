using System.Collections.ObjectModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Pretext.Avalonia.Controls;
using Pretext.LayoutFramework;
using Pretext.LayoutFramework.Samples;

namespace Pretext.Avalonia.Samples;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<SampleDescriptor> _samples;
    private readonly Dictionary<string, Control> _sampleCache = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _diagnosticsTimer;

    private ListBox _sampleList = null!;
    private TextBlock _sampleTitleText = null!;
    private TextBlock _sampleDescriptionText = null!;
    private ScrollViewer _sampleScrollViewer = null!;
    private ContentControl _sampleScrollHost = null!;
    private ContentControl _sampleDirectHost = null!;
    private TextBlock _diagnosticsText = null!;

    public MainWindow()
    {
        _samples =
        [
            new SampleDescriptor("mail-row", "Mail Row", "A shared mail row definition hosted through PretextControlPanel with exact prepared slot metrics.", () => new MailRowSamplePage()),
            new SampleDescriptor("note-card", "Note Card", "A shared note card definition with badge, title, excerpt, metadata, and CTA routed through the same control host.", () => new NoteCardSamplePage()),
            new SampleDescriptor("mail-shell", "Responsive Mail Shell", "A responsive mail shell solved from one shared prepared-control definition and rendered as native Avalonia panes.", () => new MailShellSamplePage()),
            new SampleDescriptor("notes-shell", "Responsive Notes Shell", "A responsive notes workspace that switches pane geometry without nested adaptive layout primitives.", () => new NotesShellSamplePage()),
            new SampleDescriptor("wrap", "Shared Wrap Surface", "A non-uniform wrap surface driven by a shared prepared item definition and Avalonia virtualization.", () => new WrapItemsSamplePage()),
            new SampleDescriptor("masonry", "Shared Masonry Surface", "A shortest-column masonry surface using sparse viewport selection in the shared occlusion model.", () => new MasonryItemsSamplePage()),
        ];

        InitializeComponent();
        AttachControls();

        _sampleList.ItemsSource = _samples;
        _sampleList.SelectedIndex = 0;

        _diagnosticsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _diagnosticsTimer.Tick += (_, _) => RefreshDiagnostics();
        _diagnosticsTimer.Start();

        SelectSample(_samples[0].Key);
    }

    public string? SelectedSampleKey => (_sampleList.SelectedItem as SampleDescriptor)?.Key;

    public void SelectSample(string key)
    {
        var descriptor = _samples.FirstOrDefault(sample => string.Equals(sample.Key, key, StringComparison.Ordinal));
        if (descriptor is null)
        {
            return;
        }

        _sampleList.SelectedItem = descriptor;
        ShowSample(descriptor);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void AttachControls()
    {
        _sampleList = this.FindControl<ListBox>("SampleList") ?? throw new InvalidOperationException("SampleList was not found.");
        _sampleTitleText = this.FindControl<TextBlock>("SampleTitleText") ?? throw new InvalidOperationException("SampleTitleText was not found.");
        _sampleDescriptionText = this.FindControl<TextBlock>("SampleDescriptionText") ?? throw new InvalidOperationException("SampleDescriptionText was not found.");
        _sampleScrollViewer = this.FindControl<ScrollViewer>("SampleScrollViewer") ?? throw new InvalidOperationException("SampleScrollViewer was not found.");
        _sampleScrollHost = this.FindControl<ContentControl>("SampleScrollHost") ?? throw new InvalidOperationException("SampleScrollHost was not found.");
        _sampleDirectHost = this.FindControl<ContentControl>("SampleDirectHost") ?? throw new InvalidOperationException("SampleDirectHost was not found.");
        _diagnosticsText = this.FindControl<TextBlock>("DiagnosticsText") ?? throw new InvalidOperationException("DiagnosticsText was not found.");
    }

    private void OnSampleSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_sampleList.SelectedItem is SampleDescriptor descriptor)
        {
            ShowSample(descriptor);
        }
    }

    private void ShowSample(SampleDescriptor descriptor)
    {
        if (!_sampleCache.TryGetValue(descriptor.Key, out var control))
        {
            control = descriptor.Factory();
            _sampleCache[descriptor.Key] = control;
        }

        var useOuterScrollViewer = control is not ISampleHostMode { UseOuterScrollViewer: false };
        _sampleTitleText.Text = descriptor.Title;
        _sampleDescriptionText.Text = descriptor.Description;
        if (useOuterScrollViewer)
        {
            _sampleScrollHost.Content = control;
            _sampleDirectHost.Content = null;
            _sampleScrollViewer.IsVisible = true;
            _sampleDirectHost.IsVisible = false;
            _sampleScrollViewer.Offset = default;
        }
        else
        {
            _sampleDirectHost.Content = control;
            _sampleScrollHost.Content = null;
            _sampleDirectHost.IsVisible = true;
            _sampleScrollViewer.IsVisible = false;
            _sampleScrollViewer.Offset = default;
        }

        RefreshDiagnostics();
        Dispatcher.UIThread.Post(
            RefreshDiagnostics,
            DispatcherPriority.Render);
    }

    private void RefreshDiagnostics()
    {
        var activeSample = _sampleDirectHost.IsVisible ? _sampleDirectHost.Content : _sampleScrollHost.Content;
        if (activeSample is not IPretextAvaloniaSample sample)
        {
            _diagnosticsText.Text = "No diagnostics available.";
            return;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < sample.DiagnosticsProbes.Count; index++)
        {
            var probe = sample.DiagnosticsProbes[index];
            var snapshot = probe.SnapshotAccessor();

            if (index > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.AppendLine(probe.Name);
            builder.AppendLine($"prepare: {snapshot.PrepareCount} (hit {snapshot.PrepareCacheHits}, miss {snapshot.PrepareCacheMisses})");
            builder.AppendLine($"measure: access {snapshot.MeasureAccessCount}, hit {snapshot.MeasureCacheHits}, solve {snapshot.MeasureSolveCount}");
            builder.AppendLine($"arrange: access {snapshot.ArrangeAccessCount}, hit {snapshot.ArrangeCacheHits}, solve {snapshot.ArrangeSolveCount}");
            builder.AppendLine($"solve hit-rate: {snapshot.SolveCacheHitRate:P0}");
            builder.AppendLine($"viewport updates: {snapshot.ViewportUpdateCount}");
            builder.AppendLine($"realization updates: {snapshot.RealizationUpdateCount}");
            builder.AppendLine($"realized elements: {snapshot.LastRealizedElementCount}");

            if (snapshot.LastViewport is { } viewport)
            {
                builder.AppendLine($"viewport: {viewport.X:0},{viewport.Y:0}  {viewport.Width:0}x{viewport.Height:0}");
            }

            if (snapshot.LastRealizedRange is { } range)
            {
                builder.AppendLine($"range: {range.StartIndex}-{range.EndIndexExclusive} (bands {range.StartBandIndex}-{range.EndBandIndexExclusive})");
            }
        }

        _diagnosticsText.Text = builder.Length == 0 ? "No diagnostics available." : builder.ToString();
    }

}

public sealed record SampleDescriptor(string Key, string Title, string Description, Func<Control> Factory)
{
    public string ShortDescription => Description;
}

public sealed record DiagnosticsProbe(string Name, Func<LayoutDiagnosticsSnapshot> SnapshotAccessor);

public interface IPretextAvaloniaSample
{
    IReadOnlyList<DiagnosticsProbe> DiagnosticsProbes { get; }
}

public interface ISampleHostMode
{
    bool UseOuterScrollViewer { get; }
}

internal static class SamplePalette
{
    public static readonly SolidColorBrush PageBrush = Brush(0xF7, 0xF2, 0xEA);
    public static readonly SolidColorBrush SurfaceBrush = Brush(0xFF, 0xFD, 0xF9);
    public static readonly SolidColorBrush PanelBrush = Brush(0xFB, 0xF6, 0xEF);
    public static readonly SolidColorBrush AccentSoftBrush = Brush(0xF1, 0xE4, 0xD4);
    public static readonly SolidColorBrush AccentBrush = Brush(0x9F, 0x67, 0x36);
    public static readonly SolidColorBrush InkBrush = Brush(0x24, 0x1D, 0x16);
    public static readonly SolidColorBrush MutedBrush = Brush(0x6B, 0x62, 0x58);
    public static readonly SolidColorBrush RuleBrush = Brush(0xD8, 0xC9, 0xB9);
    public static readonly SolidColorBrush SoftRuleBrush = Brush(0xE7, 0xDC, 0xCF);
    public static readonly SolidColorBrush WhiteBrush = Brush(0xFF, 0xFF, 0xFF);
    public static readonly SolidColorBrush SuccessBrush = Brush(0x2E, 0x69, 0x4A);
    public static readonly SolidColorBrush InfoBrush = Brush(0x3A, 0x5D, 0x86);
    public static readonly SolidColorBrush PlumBrush = Brush(0x58, 0x4C, 0x7B);

    public static Border CreateCard(Control child, Thickness? padding = null)
    {
        return new Border
        {
            Background = SurfaceBrush,
            BorderBrush = RuleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Padding = padding ?? new Thickness(18),
            Child = child,
        };
    }

    public static Border CreatePane(string title, string body, IBrush background)
    {
        return new Border
        {
            Background = background,
            BorderBrush = RuleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Foreground = InkBrush,
                        FontSize = 18,
                        FontWeight = FontWeight.SemiBold,
                    },
                    new TextBlock
                    {
                        Text = body,
                        Foreground = MutedBrush,
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
        };
    }

    public static SolidColorBrush Brush(byte r, byte g, byte b, byte a = 0xFF)
    {
        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }
}

internal abstract class SamplePageBase : UserControl, IPretextAvaloniaSample, ISampleHostMode
{
    protected SamplePageBase()
    {
        DiagnosticsProbes = [];
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    public IReadOnlyList<DiagnosticsProbe> DiagnosticsProbes { get; protected set; }

    public virtual bool UseOuterScrollViewer => true;

    protected static Border CreateDemoFrame(string eyebrow, string title, string description, Control content)
    {
        var stack = new StackPanel
        {
            Spacing = 18,
            Children =
            {
                new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = eyebrow,
                            Foreground = SamplePalette.AccentBrush,
                            FontFamily = new FontFamily("Consolas"),
                            FontSize = 12,
                            FontWeight = FontWeight.SemiBold,
                        },
                        new TextBlock
                        {
                            Text = title,
                            Foreground = SamplePalette.InkBrush,
                            FontFamily = new FontFamily("Georgia"),
                            FontSize = 32,
                            FontWeight = FontWeight.Bold,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock
                        {
                            Text = description,
                            Foreground = SamplePalette.MutedBrush,
                            FontSize = 15,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 860,
                        },
                    },
                },
                content,
            },
        };

        return SamplePalette.CreateCard(stack, new Thickness(22));
    }

    protected static Border CreateStretchDemoFrame(string eyebrow, string title, string description, Control content)
    {
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.VerticalAlignment = VerticalAlignment.Stretch;

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 18,
            Children =
            {
                new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = eyebrow,
                            Foreground = SamplePalette.AccentBrush,
                            FontFamily = new FontFamily("Consolas"),
                            FontSize = 12,
                            FontWeight = FontWeight.SemiBold,
                        },
                        new TextBlock
                        {
                            Text = title,
                            Foreground = SamplePalette.InkBrush,
                            FontFamily = new FontFamily("Georgia"),
                            FontSize = 32,
                            FontWeight = FontWeight.Bold,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock
                        {
                            Text = description,
                            Foreground = SamplePalette.MutedBrush,
                            FontSize = 15,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 860,
                        },
                    },
                },
                content,
            },
        };

        Grid.SetRow(content, 1);

        var frame = SamplePalette.CreateCard(grid, new Thickness(22));
        frame.HorizontalAlignment = HorizontalAlignment.Stretch;
        frame.VerticalAlignment = VerticalAlignment.Stretch;
        return frame;
    }
}

internal sealed class MailRowSamplePage : SamplePageBase
{
    public MailRowSamplePage()
    {
        var row = new MailRowCardControl();
        DiagnosticsProbes = [new DiagnosticsProbe("mail-row", () => row.Diagnostics)];
        Content = CreateDemoFrame(
            "CONTROL",
            "Shared Mail Row",
            "The row chrome is native Avalonia, but the slot geometry, prepared text metrics, and layout caching come from the shared Pretext control definition.",
            row);
    }

}

internal sealed class NoteCardSamplePage : SamplePageBase
{
    public NoteCardSamplePage()
    {
        var card = new NoteCardControl();
        DiagnosticsProbes = [new DiagnosticsProbe("note-card", () => card.Diagnostics)];
        Content = CreateDemoFrame(
            "CONTROL",
            "Shared Note Card",
            "The same note-card sample definition now renders through Avalonia controls and the shared Pretext control host without reauthoring the geometry.",
            card);
    }

}

internal sealed class MailShellSamplePage : SamplePageBase
{
    public MailShellSamplePage()
    {
        var shell = new MailShellControl();
        DiagnosticsProbes = [new DiagnosticsProbe("mail-shell", () => shell.Diagnostics)];
        Content = CreateDemoFrame(
            "SHELL",
            "Responsive Mail Shell",
            "Resize the host and the mail shell switches between stacked, split, and three-pane geometry from one shared prepared-control definition.",
            shell);
    }

}

internal sealed class NotesShellSamplePage : SamplePageBase
{
    public NotesShellSamplePage()
    {
        var shell = new NotesShellControl();
        DiagnosticsProbes = [new DiagnosticsProbe("notes-shell", () => shell.Diagnostics)];
        Content = CreateDemoFrame(
            "SHELL",
            "Responsive Notes Shell",
            "The notes workspace uses the same staged layout model: prepare once, solve pane geometry on width changes, and keep the live controls stable.",
            shell);
    }

}

internal sealed class WrapItemsSamplePage : SamplePageBase
{
    public override bool UseOuterScrollViewer => false;

    public WrapItemsSamplePage()
    {
        var surface = new WrapItemsSurface();
        DiagnosticsProbes = [new DiagnosticsProbe("wrap-surface", () => surface.Diagnostics)];
        Content = CreateStretchDemoFrame(
            "ITEMS",
            "Shared Wrap Surface",
            "A 12k tile field uses the shared wrap item definition through the Avalonia virtualizing panel adapter, with viewport-aware realization and no built-in wrap panel.",
            surface);
    }

}

internal sealed class MasonryItemsSamplePage : SamplePageBase
{
    public override bool UseOuterScrollViewer => false;

    public MasonryItemsSamplePage()
    {
        var surface = new MasonryItemsSurface();
        DiagnosticsProbes = [new DiagnosticsProbe("masonry-surface", () => surface.Diagnostics)];
        Content = CreateStretchDemoFrame(
            "ITEMS",
            "Shared Masonry Surface",
            "The shortest-column masonry layout is solved by the shared item definition and realized through Avalonia virtualization with sparse viewport selection.",
            surface);
    }

}

internal sealed class MailRowCardControl : Border, IPretextControlLayoutSource
{
    private readonly MailRowSampleDefinition _definition = new(
        new MailRowSampleData(
            "Leopold Aschenbrenner",
            "11:08",
            "Situational awareness for the decade ahead",
            "This inbox row is solved from a shared sample definition, then rendered by native Avalonia controls through the reusable PretextControlPanel.",
            "Pinned",
            true));

    private readonly PretextControlPanel _panel;

    public MailRowCardControl()
    {
        Background = SamplePalette.SurfaceBrush;
        BorderBrush = SamplePalette.RuleBrush;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(20);
        Padding = new Thickness(0);
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _panel = new PretextControlPanel
        {
            Source = this,
            MinHeight = 152,
        };

        var avatar = new Border
        {
            Background = SamplePalette.AccentSoftBrush,
            CornerRadius = new CornerRadius(22),
            Child = new TextBlock
            {
                Text = "LA",
                Foreground = SamplePalette.AccentBrush,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            },
        };
        PretextControlPanel.SetLayoutKey(avatar, "avatar");

        var sender = new PretextParagraphControl("Helvetica Neue", 16, 20, SamplePalette.InkBrush, FontWeight.SemiBold)
        {
            Prepared = _definition.PreparedSender,
        };
        PretextControlPanel.SetLayoutKey(sender, "sender");

        var time = new TextBlock
        {
            Text = _definition.Data.TimeLabel,
            Foreground = SamplePalette.MutedBrush,
            FontFamily = new FontFamily("Helvetica Neue"),
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
        };
        PretextControlPanel.SetLayoutKey(time, "time");

        var subject = new PretextParagraphControl("Helvetica Neue", 17, 22, SamplePalette.InkBrush, FontWeight.Bold)
        {
            Prepared = _definition.PreparedSubject,
        };
        PretextControlPanel.SetLayoutKey(subject, "subject");

        var preview = new PretextParagraphControl("Helvetica Neue", 14, 20, SamplePalette.MutedBrush)
        {
            Prepared = _definition.PreparedPreview,
        };
        PretextControlPanel.SetLayoutKey(preview, "preview");

        var status = new Border
        {
            Background = SamplePalette.AccentSoftBrush,
            BorderBrush = SamplePalette.RuleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Child = new TextBlock
            {
                Text = _definition.Data.StatusLabel,
                Foreground = SamplePalette.AccentBrush,
                FontFamily = new FontFamily("Helvetica Neue"),
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            },
        };
        PretextControlPanel.SetLayoutKey(status, "status");

        _panel.Children.Add(avatar);
        _panel.Children.Add(sender);
        _panel.Children.Add(time);
        _panel.Children.Add(subject);
        _panel.Children.Add(preview);
        _panel.Children.Add(status);
        Child = _panel;
    }

    public LayoutDiagnosticsSnapshot Diagnostics => _panel.Diagnostics;

    public LayoutFingerprint GetLayoutFingerprint() => _definition.GetLayoutFingerprint();

    public PreparedControlModel Prepare(LayoutFingerprint fingerprint) => _definition.Prepare(fingerprint);

    public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints) => _definition.Solve(prepared, constraints);
}

internal sealed class NoteCardControl : Border, IPretextControlLayoutSource
{
    private readonly NoteCardSampleDefinition _definition = new(
        new NoteCardSampleData(
            "Editorial",
            "Shared sample classes can drive retained note cards",
            "The title, excerpt, metadata, badge, and action all route through a shared prepared-control definition instead of being reauthored separately for each UI framework.",
            "Updated 6m ago · 3 comments",
            "Open note"));

    private readonly PretextControlPanel _panel;

    public NoteCardControl()
    {
        Background = SamplePalette.SurfaceBrush;
        BorderBrush = SamplePalette.RuleBrush;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(20);
        Padding = new Thickness(0);
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _panel = new PretextControlPanel
        {
            Source = this,
            MinHeight = 290,
        };

        var badge = new Border
        {
            Background = SamplePalette.AccentSoftBrush,
            BorderBrush = SamplePalette.RuleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(15),
            Child = new TextBlock
            {
                Text = _definition.Data.Category,
                Foreground = SamplePalette.AccentBrush,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            },
        };
        PretextControlPanel.SetLayoutKey(badge, "badge");

        var title = new PretextParagraphControl("Georgia", 26, 30, SamplePalette.InkBrush, FontWeight.Bold)
        {
            Prepared = _definition.PreparedTitle,
        };
        PretextControlPanel.SetLayoutKey(title, "title");

        var excerpt = new PretextParagraphControl("Helvetica Neue", 15, 22, SamplePalette.MutedBrush)
        {
            Prepared = _definition.PreparedExcerpt,
        };
        PretextControlPanel.SetLayoutKey(excerpt, "excerpt");

        var meta = new TextBlock
        {
            Text = _definition.Data.MetaLabel,
            Foreground = SamplePalette.MutedBrush,
            FontFamily = new FontFamily("Helvetica Neue"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        };
        PretextControlPanel.SetLayoutKey(meta, "meta");

        var action = new Button
        {
            Content = _definition.Data.ActionLabel,
            Background = SamplePalette.InkBrush,
            Foreground = SamplePalette.WhiteBrush,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(18, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        PretextControlPanel.SetLayoutKey(action, "action");

        _panel.Children.Add(badge);
        _panel.Children.Add(title);
        _panel.Children.Add(excerpt);
        _panel.Children.Add(meta);
        _panel.Children.Add(action);
        Child = _panel;
    }

    public LayoutDiagnosticsSnapshot Diagnostics => _panel.Diagnostics;

    public LayoutFingerprint GetLayoutFingerprint() => _definition.GetLayoutFingerprint();

    public PreparedControlModel Prepare(LayoutFingerprint fingerprint) => _definition.Prepare(fingerprint);

    public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints) => _definition.Solve(prepared, constraints);
}

internal sealed class MailShellControl : Border, IPretextControlLayoutSource
{
    private readonly MailShellSampleDefinition _definition = new(
        new MailShellSampleData(
            "Northwind Mail",
            "Inbox geometry rendered without adaptive Grids",
            5,
            18,
            "Prepared geometry keeps the reader pane stable"));

    private readonly PretextControlPanel _panel;

    public MailShellControl()
    {
        Background = SamplePalette.SurfaceBrush;
        BorderBrush = SamplePalette.RuleBrush;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(22);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Height = 720;

        _panel = new PretextControlPanel
        {
            Source = this,
        };

        _panel.Children.Add(SamplePalette.CreatePane("Mail Toolbar", "Search, compose, archive, and route alerts.", SamplePalette.AccentSoftBrush));
        PretextControlPanel.SetLayoutKey(_panel.Children[^1], "toolbar");
        _panel.Children.Add(SamplePalette.CreatePane("Folders", "Inbox, Design, Ops, Unread, Starred", SamplePalette.PanelBrush));
        PretextControlPanel.SetLayoutKey(_panel.Children[^1], "folders");
        _panel.Children.Add(SamplePalette.CreatePane("Messages", "Exact preview heights, stable row positions, and no placeholder jumps.", SamplePalette.Brush(0xEF, 0xF4, 0xFB)));
        PretextControlPanel.SetLayoutKey(_panel.Children[^1], "messages");
        _panel.Children.Add(SamplePalette.CreatePane("Reader", "The selected message stays pinned while pane geometry changes.", SamplePalette.Brush(0xF3, 0xEE, 0xF8)));
        PretextControlPanel.SetLayoutKey(_panel.Children[^1], "reader");
        Child = _panel;
    }

    public LayoutDiagnosticsSnapshot Diagnostics => _panel.Diagnostics;

    public LayoutFingerprint GetLayoutFingerprint() => _definition.GetLayoutFingerprint();

    public PreparedControlModel Prepare(LayoutFingerprint fingerprint) => _definition.Prepare(fingerprint);

    public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints) => _definition.Solve(prepared, constraints);
}

internal sealed class NotesShellControl : Border, IPretextControlLayoutSource
{
    private readonly NotesShellSampleDefinition _definition = new(
        new NotesShellSampleData(
            "Field Notes",
            "Absolute layout with note cards and a reading editor",
            6,
            "Shared shell geometry keeps the selected note stable"));

    private readonly PretextControlPanel _panel;

    public NotesShellControl()
    {
        Background = SamplePalette.SurfaceBrush;
        BorderBrush = SamplePalette.RuleBrush;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(22);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Height = 760;

        _panel = new PretextControlPanel
        {
            Source = this,
        };

        _panel.Children.Add(SamplePalette.CreatePane("Toolbar", "Search, capture, tag, pin, and branch notes.", SamplePalette.AccentSoftBrush));
        PretextControlPanel.SetLayoutKey(_panel.Children[^1], "toolbar");
        _panel.Children.Add(SamplePalette.CreatePane("Collection", "Card stack with prepared preview geometry.", SamplePalette.PanelBrush));
        PretextControlPanel.SetLayoutKey(_panel.Children[^1], "collection");
        _panel.Children.Add(SamplePalette.CreatePane("Editor", "The selected note body is stable through shell reflow.", SamplePalette.Brush(0xEF, 0xF4, 0xFB)));
        PretextControlPanel.SetLayoutKey(_panel.Children[^1], "editor");
        _panel.Children.Add(SamplePalette.CreatePane("Insight", "Metadata, comments, and links stay in a separate solved pane.", SamplePalette.Brush(0xF3, 0xEE, 0xF8)));
        PretextControlPanel.SetLayoutKey(_panel.Children[^1], "insight");
        Child = _panel;
    }

    public LayoutDiagnosticsSnapshot Diagnostics => _panel.Diagnostics;

    public LayoutFingerprint GetLayoutFingerprint() => _definition.GetLayoutFingerprint();

    public PreparedControlModel Prepare(LayoutFingerprint fingerprint) => _definition.Prepare(fingerprint);

    public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints) => _definition.Solve(prepared, constraints);
}

internal sealed class WrapItemsSurface : Border
{
    private readonly WrapItemsSampleDefinition _definition = WrapItemsSampleDefinition.CreateDemo(itemCount: 12_000, templateCount: 120);
    private readonly WrapItemsPanel _panel;

    public WrapItemsSurface()
    {
        Background = SamplePalette.SurfaceBrush;
        BorderBrush = SamplePalette.RuleBrush;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(20);
        MinHeight = 420;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _panel = new WrapItemsPanel(_definition);
        var items = Enumerable.Range(0, _definition.Count).ToArray();
        var control = ItemsHostFactory.CreateItemsHost(items, _panel, index => new WrapTileView(_definition));
        Child = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = control,
        };
    }

    public LayoutDiagnosticsSnapshot Diagnostics => _panel.Diagnostics;
}

internal sealed class MasonryItemsSurface : Border
{
    private readonly MasonryItemsSampleDefinition _definition = MasonryItemsSampleDefinition.CreateDemo();
    private readonly MasonryItemsPanel _panel;

    public MasonryItemsSurface()
    {
        Background = SamplePalette.SurfaceBrush;
        BorderBrush = SamplePalette.RuleBrush;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(20);
        MinHeight = 420;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _panel = new MasonryItemsPanel(_definition);
        var items = Enumerable.Range(0, _definition.Count).ToArray();
        var control = ItemsHostFactory.CreateItemsHost(items, _panel, index => new MasonryTileView(_definition));
        Child = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = control,
        };
    }

    public LayoutDiagnosticsSnapshot Diagnostics => _panel.Diagnostics;
}

internal sealed class WrapItemsPanel : PretextVirtualizingPanelBase
{
    private readonly WrapItemsSampleDefinition _definition;

    public WrapItemsPanel(WrapItemsSampleDefinition definition)
    {
        _definition = definition;
        ViewportOverscan = 240;
    }

    protected override LayoutFingerprint GetLayoutFingerprint() => _definition.GetLayoutFingerprint();

    protected override PreparedItemsModel Prepare(LayoutFingerprint fingerprint) => _definition.Prepare(fingerprint);

    protected override SolvedLayout Solve(PreparedItemsModel prepared, LayoutConstraints constraints) => _definition.Solve(prepared, constraints);
}

internal sealed class MasonryItemsPanel : PretextVirtualizingPanelBase
{
    private readonly MasonryItemsSampleDefinition _definition;

    public MasonryItemsPanel(MasonryItemsSampleDefinition definition)
    {
        _definition = definition;
        ViewportOverscan = 260;
    }

    protected override LayoutFingerprint GetLayoutFingerprint() => _definition.GetLayoutFingerprint();

    protected override PreparedItemsModel Prepare(LayoutFingerprint fingerprint) => _definition.Prepare(fingerprint);

    protected override SolvedLayout Solve(PreparedItemsModel prepared, LayoutConstraints constraints) => _definition.Solve(prepared, constraints);
}

internal sealed class WrapTileView : Border
{
    private static readonly IBrush[] Backgrounds =
    [
        SamplePalette.SurfaceBrush,
        SamplePalette.AccentSoftBrush,
        SamplePalette.Brush(0xF0, 0xEE, 0xE8),
        SamplePalette.Brush(0xEE, 0xF1, 0xEA),
        SamplePalette.Brush(0xEA, 0xEC, 0xF4),
    ];

    private static readonly IBrush[] Borders =
    [
        SamplePalette.RuleBrush,
        SamplePalette.Brush(0xCF, 0xBF, 0xB0),
        SamplePalette.Brush(0xC8, 0xD4, 0xC2),
        SamplePalette.Brush(0xC8, 0xC9, 0xDA),
        SamplePalette.Brush(0xD9, 0xC8, 0xBE),
    ];

    private readonly WrapItemsSampleDefinition _definition;
    private readonly PretextParagraphControl _body;

    public WrapTileView(WrapItemsSampleDefinition definition)
    {
        _definition = definition;
        Padding = new Thickness(14);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(18);
        _body = new PretextParagraphControl("Helvetica Neue", 15, 20, SamplePalette.InkBrush);
        Child = _body;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not int index)
        {
            return;
        }

        var template = _definition.GetTemplateForItem(index);
        Background = Backgrounds[template.PaletteIndex % Backgrounds.Length];
        BorderBrush = Borders[template.PaletteIndex % Borders.Length];
        _body.Prepared = template.Prepared;
    }
}

internal sealed class MasonryTileView : Border
{
    private static readonly IBrush[] Borders =
    [
        SamplePalette.RuleBrush,
        SamplePalette.Brush(0xCF, 0xBF, 0xB0),
        SamplePalette.Brush(0xC8, 0xD4, 0xC2),
        SamplePalette.Brush(0xC8, 0xC9, 0xDA),
        SamplePalette.Brush(0xD9, 0xC8, 0xBE),
    ];

    private readonly MasonryItemsSampleDefinition _definition;
    private readonly PretextParagraphControl _body;

    public MasonryTileView(MasonryItemsSampleDefinition definition)
    {
        _definition = definition;
        Background = SamplePalette.SurfaceBrush;
        Padding = new Thickness(16);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(18);
        _body = new PretextParagraphControl("Helvetica Neue", 15, 22, SamplePalette.InkBrush);
        Child = _body;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not int index)
        {
            return;
        }

        var item = _definition.GetItem(index);
        BorderBrush = Borders[item.PaletteIndex % Borders.Length];
        _body.Prepared = item.Prepared;
    }
}

internal static class ItemsHostFactory
{
    public static PretextVirtualizingItemsControl CreateItemsHost<TPanel>(
        IEnumerable<int> items,
        TPanel panel,
        Func<int, Control> createControl)
        where TPanel : Panel
    {
        return new PretextVirtualizingItemsControl
        {
            ItemsSource = items.ToArray(),
            Template = CreateItemsControlTemplate<PretextVirtualizingItemsControl>(),
            ItemsPanel = new FuncTemplate<Panel?>(() => panel),
            ItemTemplate = new FuncDataTemplate<int>((index, _) => createControl(index)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }

    private static FuncControlTemplate<TItemsControl> CreateItemsControlTemplate<TItemsControl>()
        where TItemsControl : ItemsControl
    {
        return new FuncControlTemplate<TItemsControl>((parent, scope) =>
            new ItemsPresenter
            {
                Name = "PART_ItemsPresenter",
                [~ItemsPresenter.ItemsPanelProperty] = parent[~ItemsControl.ItemsPanelProperty],
            }.RegisterInNameScope(scope));
    }
}
