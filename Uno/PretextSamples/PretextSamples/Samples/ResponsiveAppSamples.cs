using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Pretext.Uno;
using Windows.Foundation;
using Windows.UI.Text;

namespace PretextSamples.Samples;

internal enum ResponsiveAppMode
{
    Narrow,
    Medium,
    Wide,
}

internal static class ResponsiveAppUi
{
    public static ResponsiveAppMode ResolveMode(double width)
    {
        return width >= 1180
            ? ResponsiveAppMode.Wide
            : width >= 820
                ? ResponsiveAppMode.Medium
                : ResponsiveAppMode.Narrow;
    }

    public static Border CreateSurface(Canvas layer, Brush? background = null, Brush? borderBrush = null)
    {
        return new Border
        {
            Background = background ?? SampleTheme.PanelBrush,
            BorderBrush = borderBrush ?? SampleTheme.RuleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Child = layer,
        };
    }

    public static TextBlock CreateLabel(string text, double fontSize, Brush brush, FontWeight? weight = null)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = brush,
            FontSize = fontSize,
            FontWeight = weight ?? FontWeights.Normal,
            TextWrapping = TextWrapping.NoWrap,
        };
    }

    public static TextBox CreateInput(string text, string placeholder)
    {
        return new TextBox
        {
            Text = text,
            PlaceholderText = placeholder,
            Background = SampleTheme.PanelBrush,
            BorderBrush = SampleTheme.RuleBrush,
            BorderThickness = new Thickness(1),
            FontSize = 14,
        };
    }

    public static Button CreateButton(string text, bool filled = false)
    {
        return new Button
        {
            Content = text,
            Background = filled ? SampleTheme.InkBrush : SampleTheme.PanelBrush,
            Foreground = filled ? SampleTheme.WhiteBrush : SampleTheme.InkBrush,
            BorderBrush = filled ? SampleTheme.InkBrush : SampleTheme.RuleBrush,
            BorderThickness = new Thickness(1),
            FontSize = 14,
            Padding = new Thickness(12, 0, 12, 0),
        };
    }

    public static void SetRect(FrameworkElement element, double x, double y, double width, double height)
    {
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
    }

    public static void SetPoint(FrameworkElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
    }

    public static void Show(UIElement element, bool visible)
    {
        element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
}

internal sealed class PretextParagraphView : UserControl
{
    private readonly Canvas _canvas = new();
    private readonly List<TextBlock> _linePool = [];
    private readonly string _fontFamily;
    private readonly double _fontSize;
    private readonly double _lineHeight;
    private readonly Brush _brush;
    private readonly FontWeight _weight;

    public PretextParagraphView(string fontFamily, double fontSize, double lineHeight, Brush brush, FontWeight? weight = null)
    {
        _fontFamily = fontFamily;
        _fontSize = fontSize;
        _lineHeight = lineHeight;
        _brush = brush;
        _weight = weight ?? FontWeights.Normal;
        IsHitTestVisible = false;
        Content = _canvas;
    }

    public double Render(PreparedTextWithSegments prepared, double width, int? maxLines = null)
    {
        var lines = PretextLayout.LayoutWithLines(prepared, Math.Max(1, width), _lineHeight).Lines;
        var lineCount = maxLines.HasValue ? Math.Min(maxLines.Value, lines.Count) : lines.Count;

        while (_linePool.Count < lineCount)
        {
            var line = new TextBlock
            {
                Foreground = _brush,
                FontFamily = new FontFamily(_fontFamily),
                FontSize = _fontSize,
                FontWeight = _weight,
                LineHeight = _lineHeight,
                TextWrapping = TextWrapping.NoWrap,
            };
            _linePool.Add(line);
            _canvas.Children.Add(line);
        }

        for (var index = 0; index < _linePool.Count; index++)
        {
            var visible = index < lineCount;
            _linePool[index].Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (!visible)
            {
                continue;
            }

            _linePool[index].Text = lines[index].Text;
            _linePool[index].Width = Math.Max(1, width);
            Canvas.SetLeft(_linePool[index], 0);
            Canvas.SetTop(_linePool[index], index * _lineHeight);
        }

        Width = Math.Max(1, width);
        Height = lineCount * _lineHeight;
        _canvas.Width = Width;
        _canvas.Height = Height;
        return Height;
    }
}

internal abstract class ResponsiveAppSampleBase : UserControl
{
    protected readonly Canvas Stage = new();
    protected readonly Border Shell;
    protected readonly StretchScrollHost PageRoot;
    protected readonly UiRenderScheduler RenderScheduler;

    protected ResponsiveAppSampleBase(string title, string description)
    {
        Shell = new Border
        {
            Background = SampleTheme.PanelBrush,
            BorderBrush = SampleTheme.RuleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(28),
            Child = Stage,
        };

        var stack = SampleUi.CreatePageStack();
        stack.Children.Add(SampleUi.CreateHeader("DEMO", title, description));
        stack.Children.Add(Shell);

        PageRoot = (StretchScrollHost)SampleUi.CreatePageRoot(stack);
        Content = PageRoot;
        RenderScheduler = new UiRenderScheduler(DispatcherQueue, Render);
        Loaded += (_, _) => RenderScheduler.Schedule();
        SizeChanged += (_, _) => RenderScheduler.Schedule();
    }

    private void Render()
    {
        if (PageRoot.ActualWidth <= 0)
        {
            return;
        }

        var stageWidth = Math.Max(360, PageRoot.ActualWidth - 56);
        Shell.Width = stageWidth;
        Stage.Width = stageWidth;
        var stageHeight = RenderStage(stageWidth);
        Stage.Height = stageHeight;
        Shell.Height = stageHeight;
    }

    protected abstract double RenderStage(double stageWidth);
}

internal sealed class TodoResponsiveSampleView : ResponsiveAppSampleBase
{
    private const string TitleFont = "700 16px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string BodyFont = "15px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string DetailFont = "16px \"Helvetica Neue\", Helvetica, Arial, sans-serif";

    private readonly Canvas _toolbarCanvas = new();
    private readonly Border _toolbarPanel;
    private readonly Canvas _summaryCanvas = new();
    private readonly Border _summaryPanel;
    private readonly Canvas _listCanvas = new();
    private readonly Border _listPanel;
    private readonly Canvas _detailCanvas = new();
    private readonly Border _detailPanel;

    private readonly TextBlock _toolbarTitle = ResponsiveAppUi.CreateLabel("Release Orbit", 24, SampleTheme.InkBrush, FontWeights.Bold);
    private readonly TextBlock _toolbarCaption = ResponsiveAppUi.CreateLabel("Sprint planning without a measured layout tree", 13, SampleTheme.MutedBrush, FontWeights.Normal);
    private readonly TextBox _toolbarSearch = ResponsiveAppUi.CreateInput("Search release notes, QA blockers, handoffs", "Search");
    private readonly Button _newTaskButton = ResponsiveAppUi.CreateButton("New Task", filled: true);
    private readonly Button _focusButton = ResponsiveAppUi.CreateButton("Focus Mode");

    private readonly TextBlock _summaryTitle = ResponsiveAppUi.CreateLabel("Today", 18, SampleTheme.InkBrush, FontWeights.SemiBold);
    private readonly TextBlock _summaryMetricA = ResponsiveAppUi.CreateLabel("18 active", 28, SampleTheme.InkBrush, FontWeights.Bold);
    private readonly TextBlock _summaryMetricB = ResponsiveAppUi.CreateLabel("7 done", 18, SampleTheme.MutedBrush, FontWeights.SemiBold);
    private readonly ProgressBar _summaryProgress = new() { Minimum = 0, Maximum = 100, Value = 72 };
    private readonly Button _summaryButtonA = ResponsiveAppUi.CreateButton("Blocked");
    private readonly Button _summaryButtonB = ResponsiveAppUi.CreateButton("Review");
    private readonly PretextParagraphView _summaryBody = new("Helvetica Neue", 14, 20, SampleTheme.MutedBrush, FontWeights.Normal);

    private readonly TextBlock _listTitle = ResponsiveAppUi.CreateLabel("This sprint", 18, SampleTheme.InkBrush, FontWeights.SemiBold);
    private readonly TextBlock _listMeta = ResponsiveAppUi.CreateLabel("6 selected tasks positioned by exact text height", 13, SampleTheme.MutedBrush, FontWeights.Normal);

    private readonly TextBlock _detailEyebrow = ResponsiveAppUi.CreateLabel("Selected task", 12, SampleTheme.AccentBrush, FontWeights.SemiBold);
    private readonly TextBlock _detailOwner = ResponsiveAppUi.CreateLabel(string.Empty, 13, SampleTheme.MutedBrush, FontWeights.Normal);
    private readonly Button _completeButton = ResponsiveAppUi.CreateButton("Mark Done", filled: true);
    private readonly Button _deferButton = ResponsiveAppUi.CreateButton("Defer");
    private readonly TextBox _nextStepInput = ResponsiveAppUi.CreateInput(string.Empty, "Capture the next step");
    private readonly PretextParagraphView _detailTitleView = new("Helvetica Neue", 22, 28, SampleTheme.InkBrush, FontWeights.Bold);
    private readonly PretextParagraphView _detailBodyView = new("Helvetica Neue", 16, 24, SampleTheme.InkBrush, FontWeights.Normal);

    private readonly TodoTaskData[] _tasks;
    private readonly List<TodoTaskVisual> _taskVisuals = [];
    private readonly PreparedTextWithSegments _summaryPrepared;
    private int _selectedIndex = 1;

    public TodoResponsiveSampleView()
        : base(
            "Responsive todo planner",
            "A task workspace rendered with normal Uno controls, but laid out by explicit panel rects and Pretext-driven text measurement. Drag the window narrower and the same controls reflow from a three-pane board into stacked mobile sections.")
    {
        _toolbarPanel = ResponsiveAppUi.CreateSurface(_toolbarCanvas, SampleTheme.Brush(0xFB, 0xF7, 0xF0));
        _summaryPanel = ResponsiveAppUi.CreateSurface(_summaryCanvas);
        _listPanel = ResponsiveAppUi.CreateSurface(_listCanvas);
        _detailPanel = ResponsiveAppUi.CreateSurface(_detailCanvas, SampleTheme.Brush(0xFC, 0xF9, 0xF4));

        Stage.Children.Add(_toolbarPanel);
        Stage.Children.Add(_summaryPanel);
        Stage.Children.Add(_listPanel);
        Stage.Children.Add(_detailPanel);

        _toolbarCanvas.Children.Add(_toolbarTitle);
        _toolbarCanvas.Children.Add(_toolbarCaption);
        _toolbarCanvas.Children.Add(_toolbarSearch);
        _toolbarCanvas.Children.Add(_newTaskButton);
        _toolbarCanvas.Children.Add(_focusButton);

        _summaryCanvas.Children.Add(_summaryTitle);
        _summaryCanvas.Children.Add(_summaryMetricA);
        _summaryCanvas.Children.Add(_summaryMetricB);
        _summaryCanvas.Children.Add(_summaryProgress);
        _summaryCanvas.Children.Add(_summaryButtonA);
        _summaryCanvas.Children.Add(_summaryButtonB);
        _summaryCanvas.Children.Add(_summaryBody);

        _listCanvas.Children.Add(_listTitle);
        _listCanvas.Children.Add(_listMeta);

        _detailCanvas.Children.Add(_detailEyebrow);
        _detailCanvas.Children.Add(_detailOwner);
        _detailCanvas.Children.Add(_completeButton);
        _detailCanvas.Children.Add(_deferButton);
        _detailCanvas.Children.Add(_nextStepInput);
        _detailCanvas.Children.Add(_detailTitleView);
        _detailCanvas.Children.Add(_detailBodyView);

        _summaryPrepared = PretextLayout.PrepareWithSegments(
            "The sprint shell itself is panel-free. Pretext predicts every title and note preview first, then the surface swaps between board, split, and stacked arrangements without a measure pass over the live controls.",
            BodyFont);

        _tasks =
        [
            new TodoTaskData("Stabilize the Uno sample host", "Fold the new demo pages into the shell and keep the navigation clean at narrow widths.", "Platform", "Today", false,
                "The shell should stay snappy when the viewport crosses wide, medium, and narrow breakpoints. The task board should not depend on Grid re-measurement to decide where the detail pane lands."),
            new TodoTaskData("Rewrite the mail triage copy", "Keep the wide reader pane and condensed mobile stack aligned to the same interaction model.", "Content", "Today", true,
                "The desktop composition reads like a normal productivity surface, but every preview and detail block is still measured by Pretext before controls are positioned."),
            new TodoTaskData("Audit long multilingual labels", "Mixed punctuation, CJK names, and Arabic notes should fit the same responsive shells.", "Localization", "Tomorrow", false,
                "The point of the sample is not just to resize chrome. It is to show that exact line counts and exact heights remain stable while the app frame responds fluidly."),
            new TodoTaskData("Tighten the note-card density", "Use tighter widths without clipping the editorial excerpts in the card stack.", "Design", "Tomorrow", false,
                "The cards should wrap earlier on medium widths, then flip into a single-column reading mode on narrow screens while keeping the same selected note."),
            new TodoTaskData("Queue final accessibility pass", "Label the buttons, inputs, and selection affordances before shipping the Uno port.", "QA", "Friday", false,
                "This sample intentionally uses built-in controls so the layout story does not trade away platform behavior, focus, or semantics."),
            new TodoTaskData("Collect hot-path timings", "Track the reflow cost while the stage width changes to demonstrate the arithmetic-first path.", "Perf", "Friday", false,
                "A quick resize should only recompute panel rects and text lines. No hidden placeholder controls should be created to discover how tall content became."),
        ];

        for (var index = 0; index < _tasks.Length; index++)
        {
            var visual = new TodoTaskVisual();
            visual.Root.Tag = index;
            visual.Root.Tapped += OnTaskTapped;
            _taskVisuals.Add(visual);
            _listCanvas.Children.Add(visual.Root);
        }
    }

    protected override double RenderStage(double stageWidth)
    {
        var mode = ResponsiveAppUi.ResolveMode(stageWidth);
        var gutter = mode == ResponsiveAppMode.Narrow ? 18 : 24;
        var toolbarHeight = mode == ResponsiveAppMode.Narrow ? 118 : 76;

        RenderToolbar(stageWidth, gutter, toolbarHeight, mode);
        RenderSummary(stageWidth, gutter, toolbarHeight, mode, out var summaryRect);
        RenderTodoPanels(stageWidth, gutter, toolbarHeight, summaryRect, mode, out var listRect, out var detailRect, out var stageHeight);
        RenderTaskList(listRect.Width, listRect.Height);
        RenderDetail(detailRect.Width, detailRect.Height);

        return stageHeight;
    }

    private void RenderToolbar(double stageWidth, double gutter, double toolbarHeight, ResponsiveAppMode mode)
    {
        ResponsiveAppUi.SetRect(_toolbarPanel, gutter, gutter, stageWidth - gutter * 2, toolbarHeight);
        _toolbarCanvas.Width = _toolbarPanel.Width;
        _toolbarCanvas.Height = toolbarHeight;

        ResponsiveAppUi.SetPoint(_toolbarTitle, 20, 14);
        ResponsiveAppUi.SetPoint(_toolbarCaption, 20, 46);

        if (mode == ResponsiveAppMode.Narrow)
        {
            ResponsiveAppUi.SetRect(_toolbarSearch, 20, 74, _toolbarPanel.Width - 152, 36);
            ResponsiveAppUi.SetRect(_newTaskButton, _toolbarPanel.Width - 120, 74, 100, 36);
            ResponsiveAppUi.Show(_focusButton, false);
        }
        else
        {
            ResponsiveAppUi.SetRect(_toolbarSearch, _toolbarPanel.Width - 432, 20, 252, 36);
            ResponsiveAppUi.SetRect(_focusButton, _toolbarPanel.Width - 168, 20, 72, 36);
            ResponsiveAppUi.Show(_focusButton, true);
            ResponsiveAppUi.SetRect(_newTaskButton, _toolbarPanel.Width - 88, 20, 68, 36);
        }
    }

    private void RenderSummary(double stageWidth, double gutter, double toolbarHeight, ResponsiveAppMode mode, out Rect summaryRect)
    {
        if (mode == ResponsiveAppMode.Wide)
        {
            summaryRect = new Rect(gutter, gutter + toolbarHeight + 18, 238, 246);
        }
        else
        {
            summaryRect = new Rect(gutter, gutter + toolbarHeight + 18, stageWidth - gutter * 2, mode == ResponsiveAppMode.Medium ? 150 : 172);
        }

        ResponsiveAppUi.SetRect(_summaryPanel, summaryRect.X, summaryRect.Y, summaryRect.Width, summaryRect.Height);
        _summaryCanvas.Width = summaryRect.Width;
        _summaryCanvas.Height = summaryRect.Height;

        ResponsiveAppUi.SetPoint(_summaryTitle, 20, 18);
        ResponsiveAppUi.SetPoint(_summaryMetricA, 20, 48);
        ResponsiveAppUi.SetPoint(_summaryMetricB, 20, 84);
        ResponsiveAppUi.SetRect(_summaryProgress, 20, 116, summaryRect.Width - 40, 8);
        var summaryBodyWidth = summaryRect.Width - 40;
        var summaryBodyY = 136;
        _summaryBody.Render(_summaryPrepared, summaryBodyWidth, mode == ResponsiveAppMode.Narrow ? 4 : 3);
        ResponsiveAppUi.SetRect(_summaryBody, 20, summaryBodyY, summaryBodyWidth, _summaryBody.Height);

        if (mode == ResponsiveAppMode.Wide)
        {
            ResponsiveAppUi.SetRect(_summaryButtonA, 20, summaryRect.Height - 54, 92, 34);
            ResponsiveAppUi.SetRect(_summaryButtonB, 120, summaryRect.Height - 54, 92, 34);
        }
        else
        {
            ResponsiveAppUi.SetRect(_summaryButtonA, 20, summaryRect.Height - 52, 92, 34);
            ResponsiveAppUi.SetRect(_summaryButtonB, 120, summaryRect.Height - 52, 92, 34);
        }
    }

    private void RenderTodoPanels(double stageWidth, double gutter, double toolbarHeight, Rect summaryRect, ResponsiveAppMode mode, out Rect listRect, out Rect detailRect, out double stageHeight)
    {
        if (mode == ResponsiveAppMode.Wide)
        {
            listRect = new Rect(summaryRect.X + summaryRect.Width + 18, summaryRect.Y, 368, 616);
            detailRect = new Rect(listRect.X + listRect.Width + 18, summaryRect.Y, stageWidth - (listRect.X + listRect.Width) - gutter - 18, 616);
            stageHeight = Math.Max(detailRect.Y + detailRect.Height, summaryRect.Y + summaryRect.Height) + gutter;
        }
        else if (mode == ResponsiveAppMode.Medium)
        {
            listRect = new Rect(gutter, summaryRect.Y + summaryRect.Height + 18, 340, 592);
            detailRect = new Rect(listRect.X + listRect.Width + 18, summaryRect.Y + summaryRect.Height + 18, stageWidth - gutter - (listRect.X + listRect.Width) - 18, 592);
            stageHeight = detailRect.Y + detailRect.Height + gutter;
        }
        else
        {
            listRect = new Rect(gutter, summaryRect.Y + summaryRect.Height + 18, stageWidth - gutter * 2, 508);
            detailRect = new Rect(gutter, listRect.Y + listRect.Height + 18, stageWidth - gutter * 2, 520);
            stageHeight = detailRect.Y + detailRect.Height + gutter;
        }

        ResponsiveAppUi.SetRect(_listPanel, listRect.X, listRect.Y, listRect.Width, listRect.Height);
        _listCanvas.Width = listRect.Width;
        _listCanvas.Height = listRect.Height;

        ResponsiveAppUi.SetRect(_detailPanel, detailRect.X, detailRect.Y, detailRect.Width, detailRect.Height);
        _detailCanvas.Width = detailRect.Width;
        _detailCanvas.Height = detailRect.Height;
    }

    private void RenderTaskList(double panelWidth, double panelHeight)
    {
        ResponsiveAppUi.SetPoint(_listTitle, 20, 18);
        ResponsiveAppUi.SetPoint(_listMeta, 20, 48);

        var rowWidth = panelWidth - 32;
        var y = 80d;
        for (var index = 0; index < _taskVisuals.Count; index++)
        {
            var height = _taskVisuals[index].Render(_tasks[index], rowWidth, index == _selectedIndex);
            ResponsiveAppUi.SetRect(_taskVisuals[index].Root, 16, y, rowWidth, height);
            y += height + 10;
        }

        _listCanvas.Height = Math.Max(panelHeight, y + 12);
        _listPanel.Height = _listCanvas.Height;
    }

    private void RenderDetail(double panelWidth, double panelHeight)
    {
        var task = _tasks[_selectedIndex];
        ResponsiveAppUi.SetPoint(_detailEyebrow, 22, 20);
        ResponsiveAppUi.SetPoint(_detailOwner, 22, 46);
        _detailOwner.Text = $"{task.Bucket} • {task.Due} • owner {task.Owner}";

        ResponsiveAppUi.SetRect(_completeButton, panelWidth - 210, 18, 94, 34);
        ResponsiveAppUi.SetRect(_deferButton, panelWidth - 108, 18, 86, 34);

        var titleWidth = Math.Max(160, panelWidth - 44);
        var titleHeight = _detailTitleView.Render(task.TitlePrepared, titleWidth, maxLines: 3);
        ResponsiveAppUi.SetRect(_detailTitleView, 22, 82, titleWidth, titleHeight);

        ResponsiveAppUi.SetRect(_nextStepInput, 22, 98 + titleHeight, panelWidth - 44, 38);

        var bodyHeight = _detailBodyView.Render(task.DetailPrepared, panelWidth - 44);
        ResponsiveAppUi.SetRect(_detailBodyView, 22, 150 + titleHeight, panelWidth - 44, bodyHeight);

        _detailCanvas.Height = Math.Max(panelHeight, 180 + titleHeight + bodyHeight);
        _detailPanel.Height = _detailCanvas.Height;
    }

    private void OnTaskTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Border { Tag: int index })
        {
            _selectedIndex = index;
            RenderScheduler.Schedule();
        }
    }

    private sealed class TodoTaskVisual
    {
        public TodoTaskVisual()
        {
            Layer = new Canvas();
            Root = ResponsiveAppUi.CreateSurface(Layer);
            Toggle = new CheckBox { IsEnabled = false };
            Due = ResponsiveAppUi.CreateLabel(string.Empty, 12, SampleTheme.MutedBrush, FontWeights.SemiBold);
            Meta = ResponsiveAppUi.CreateLabel(string.Empty, 12, SampleTheme.AccentBrush, FontWeights.SemiBold);
            TitleView = new PretextParagraphView("Helvetica Neue", 16, 22, SampleTheme.InkBrush, FontWeights.SemiBold);
            BodyView = new PretextParagraphView("Helvetica Neue", 14, 19, SampleTheme.MutedBrush, FontWeights.Normal);

            Layer.Children.Add(Toggle);
            Layer.Children.Add(Due);
            Layer.Children.Add(Meta);
            Layer.Children.Add(TitleView);
            Layer.Children.Add(BodyView);
        }

        public Border Root { get; }

        private Canvas Layer { get; }

        private CheckBox Toggle { get; }

        private TextBlock Due { get; }

        private TextBlock Meta { get; }

        private PretextParagraphView TitleView { get; }

        private PretextParagraphView BodyView { get; }

        public double Render(TodoTaskData task, double width, bool selected)
        {
            Root.Background = selected ? SampleTheme.AccentSoftBrush : SampleTheme.PanelBrush;
            Root.BorderBrush = selected ? SampleTheme.AccentBrush : SampleTheme.RuleBrush;
            Toggle.IsChecked = task.Done;
            Toggle.Opacity = task.Done ? 0.72 : 1;
            Due.Text = task.Due;
            Meta.Text = task.Bucket;

            ResponsiveAppUi.SetRect(Toggle, 14, 16, 24, 24);
            ResponsiveAppUi.SetRect(Due, width - 86, 16, 70, 18);

            var titleWidth = Math.Max(120, width - 132);
            var titleHeight = TitleView.Render(task.TitlePrepared, titleWidth, maxLines: 2);
            ResponsiveAppUi.SetRect(TitleView, 48, 14, titleWidth, titleHeight);

            var bodyHeight = BodyView.Render(task.PreviewPrepared, width - 64, maxLines: 2);
            ResponsiveAppUi.SetRect(BodyView, 48, 18 + titleHeight, width - 64, bodyHeight);

            ResponsiveAppUi.SetRect(Meta, 48, 26 + titleHeight + bodyHeight, 120, 18);

            var height = 52 + titleHeight + bodyHeight;
            Layer.Width = width;
            Layer.Height = height;
            return height;
        }
    }

    private sealed class TodoTaskData
    {
        public TodoTaskData(string title, string preview, string bucket, string due, bool done, string detail)
        {
            TitlePrepared = PretextLayout.PrepareWithSegments(title, TitleFont);
            PreviewPrepared = PretextLayout.PrepareWithSegments(preview, BodyFont);
            DetailPrepared = PretextLayout.PrepareWithSegments(detail, DetailFont);
            Bucket = bucket;
            Due = due;
            Done = done;
            Owner = bucket switch
            {
                "Platform" => "Anya",
                "Content" => "Mina",
                "Localization" => "Karim",
                "Design" => "Lena",
                "QA" => "Theo",
                _ => "Nora",
            };
        }

        public PreparedTextWithSegments TitlePrepared { get; }

        public PreparedTextWithSegments PreviewPrepared { get; }

        public PreparedTextWithSegments DetailPrepared { get; }

        public string Bucket { get; }

        public string Due { get; }

        public bool Done { get; }

        public string Owner { get; }
    }
}

internal sealed class MailResponsiveSampleView : ResponsiveAppSampleBase
{
    private const string SubjectFont = "700 16px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string PreviewFont = "15px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string ReaderTitleFont = "700 24px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string ReaderBodyFont = "16px \"Helvetica Neue\", Helvetica, Arial, sans-serif";

    private readonly Canvas _toolbarCanvas = new();
    private readonly Border _toolbarPanel;
    private readonly Canvas _foldersCanvas = new();
    private readonly Border _foldersPanel;
    private readonly Canvas _messagesCanvas = new();
    private readonly Border _messagesPanel;
    private readonly Canvas _readerCanvas = new();
    private readonly Border _readerPanel;

    private readonly TextBlock _toolbarTitle = ResponsiveAppUi.CreateLabel("Northwind Mail", 24, SampleTheme.InkBrush, FontWeights.Bold);
    private readonly TextBlock _toolbarCaption = ResponsiveAppUi.CreateLabel("Inbox geometry rendered without adaptive Grids", 13, SampleTheme.MutedBrush, FontWeights.Normal);
    private readonly TextBox _searchBox = ResponsiveAppUi.CreateInput("customer brief, demo replay, shipping copy", "Search mail");
    private readonly Button _composeButton = ResponsiveAppUi.CreateButton("Compose", filled: true);
    private readonly Button _archiveButton = ResponsiveAppUi.CreateButton("Archive");

    private readonly TextBlock _foldersTitle = ResponsiveAppUi.CreateLabel("Folders", 18, SampleTheme.InkBrush, FontWeights.SemiBold);
    private readonly Button[] _folderButtons =
    [
        ResponsiveAppUi.CreateButton("Inbox", filled: true),
        ResponsiveAppUi.CreateButton("Design"),
        ResponsiveAppUi.CreateButton("Launch"),
        ResponsiveAppUi.CreateButton("Contracts"),
        ResponsiveAppUi.CreateButton("Archive"),
    ];

    private readonly TextBlock _messagesTitle = ResponsiveAppUi.CreateLabel("Inbox", 18, SampleTheme.InkBrush, FontWeights.SemiBold);
    private readonly TextBlock _messagesMeta = ResponsiveAppUi.CreateLabel("18 unread • preview heights are predicted up front", 13, SampleTheme.MutedBrush, FontWeights.Normal);
    private readonly List<MailRowVisual> _messageVisuals = [];

    private readonly TextBlock _readerMeta = ResponsiveAppUi.CreateLabel(string.Empty, 13, SampleTheme.MutedBrush, FontWeights.Normal);
    private readonly Button _replyButton = ResponsiveAppUi.CreateButton("Reply", filled: true);
    private readonly Button _forwardButton = ResponsiveAppUi.CreateButton("Forward");
    private readonly Button _readerArchiveButton = ResponsiveAppUi.CreateButton("Archive");
    private readonly PretextParagraphView _readerTitleView = new("Helvetica Neue", 24, 30, SampleTheme.InkBrush, FontWeights.Bold);
    private readonly PretextParagraphView _readerBodyView = new("Helvetica Neue", 16, 24, SampleTheme.InkBrush, FontWeights.Normal);

    private readonly MailMessageData[] _messages;
    private int _selectedIndex;

    public MailResponsiveSampleView()
        : base(
            "Responsive mail triage",
            "Folders, message rows, and the reading pane are all built from normal Uno controls, but the app shell itself is an explicit absolute layout. Resize the stage and the same controls rearrange from three panes into a stacked handheld reading flow.")
    {
        _toolbarPanel = ResponsiveAppUi.CreateSurface(_toolbarCanvas, SampleTheme.Brush(0xF3, 0xF5, 0xF9), SampleTheme.Brush(0xC9, 0xD2, 0xDF));
        _foldersPanel = ResponsiveAppUi.CreateSurface(_foldersCanvas, SampleTheme.PanelBrush, SampleTheme.Brush(0xC9, 0xD2, 0xDF));
        _messagesPanel = ResponsiveAppUi.CreateSurface(_messagesCanvas);
        _readerPanel = ResponsiveAppUi.CreateSurface(_readerCanvas, SampleTheme.Brush(0xFC, 0xFD, 0xFF), SampleTheme.Brush(0xC9, 0xD2, 0xDF));

        Stage.Children.Add(_toolbarPanel);
        Stage.Children.Add(_foldersPanel);
        Stage.Children.Add(_messagesPanel);
        Stage.Children.Add(_readerPanel);

        _toolbarCanvas.Children.Add(_toolbarTitle);
        _toolbarCanvas.Children.Add(_toolbarCaption);
        _toolbarCanvas.Children.Add(_searchBox);
        _toolbarCanvas.Children.Add(_composeButton);
        _toolbarCanvas.Children.Add(_archiveButton);

        _foldersCanvas.Children.Add(_foldersTitle);
        foreach (var button in _folderButtons)
        {
            _foldersCanvas.Children.Add(button);
        }

        _messagesCanvas.Children.Add(_messagesTitle);
        _messagesCanvas.Children.Add(_messagesMeta);

        _readerCanvas.Children.Add(_readerMeta);
        _readerCanvas.Children.Add(_replyButton);
        _readerCanvas.Children.Add(_forwardButton);
        _readerCanvas.Children.Add(_readerArchiveButton);
        _readerCanvas.Children.Add(_readerTitleView);
        _readerCanvas.Children.Add(_readerBodyView);

        _messages =
        [
            new MailMessageData("Mina", "Re: landing page typography freeze", "The narrow stack still needs better spacing around the action row and footer copy before we lock the demo.", "Design • 09:12", true,
                "We finally have a clean responsive story for the stage. On desktop the reader pane can stay wide and comfortable, but on narrow screens the same message collapses into a single-column article without any chrome fighting the content."),
            new MailMessageData("Theo", "CI canary is green on the Uno branch", "Desktop builds passed after the symbol-name cleanup and the new sample registration.", "Build • 08:41", false,
                "The key win is that the mail surface never asks a list control how tall its rows became. We compute those heights from Pretext, then position the message cards directly."),
            new MailMessageData("Nora", "Need one more pass on localized subjects", "Arabic punctuation and long German compounds still push a few preview rows taller than expected.", "Localization • Yesterday", true,
                "Because the preview heights are explicit, we can watch them change as widths tighten and still keep the list stable. There is no post-layout jump when the row becomes visible."),
            new MailMessageData("Lena", "Editor wants the notes app in the keynote", "Can you keep the masonry-like note cards but make the editor column calmer on tablet widths?", "Design • Yesterday", false,
                "The mail page is a good benchmark because it mixes short sender lines with long preview copy, action buttons, and panel rearrangement across breakpoints."),
            new MailMessageData("Karim", "Subject line cleanup for the inbox demo", "Let’s trim the sample subjects so the narrow shell still feels product-shaped instead of synthetic.", "Content • Fri", false,
                "If we keep the underlying text metrics exact, we can dial the copy up or down without rewriting the container logic every time the viewport changes."),
            new MailMessageData("Anya", "Perf trace from the resize session", "The shell only touched our layout pass and the pooled line elements while the viewport moved.", "Perf • Fri", false,
                "This is the behavior we want to demonstrate: absolute panel positioning with built-in controls, but without losing responsive adaptability or paying for UI-tree measurement."),
        ];

        for (var index = 0; index < _messages.Length; index++)
        {
            var visual = new MailRowVisual();
            visual.Root.Tag = index;
            visual.Root.Tapped += OnMessageTapped;
            _messageVisuals.Add(visual);
            _messagesCanvas.Children.Add(visual.Root);
        }
    }

    protected override double RenderStage(double stageWidth)
    {
        var mode = ResponsiveAppUi.ResolveMode(stageWidth);
        var gutter = mode == ResponsiveAppMode.Narrow ? 18 : 24;
        var toolbarHeight = mode == ResponsiveAppMode.Narrow ? 118 : 76;

        RenderMailToolbar(stageWidth, gutter, toolbarHeight, mode);

        Rect foldersRect;
        Rect messagesRect;
        Rect readerRect;
        if (mode == ResponsiveAppMode.Wide)
        {
            foldersRect = new Rect(gutter, gutter + toolbarHeight + 18, 186, 636);
            messagesRect = new Rect(foldersRect.X + foldersRect.Width + 16, foldersRect.Y, 336, 636);
            readerRect = new Rect(messagesRect.X + messagesRect.Width + 16, foldersRect.Y, stageWidth - (messagesRect.X + messagesRect.Width) - gutter - 16, 636);
        }
        else if (mode == ResponsiveAppMode.Medium)
        {
            foldersRect = new Rect(gutter, gutter + toolbarHeight + 18, stageWidth - gutter * 2, 84);
            messagesRect = new Rect(gutter, foldersRect.Y + foldersRect.Height + 18, 332, 610);
            readerRect = new Rect(messagesRect.X + messagesRect.Width + 16, messagesRect.Y, stageWidth - (messagesRect.X + messagesRect.Width) - gutter - 16, 610);
        }
        else
        {
            foldersRect = new Rect(gutter, gutter + toolbarHeight + 18, stageWidth - gutter * 2, 84);
            messagesRect = new Rect(gutter, foldersRect.Y + foldersRect.Height + 18, stageWidth - gutter * 2, 482);
            readerRect = new Rect(gutter, messagesRect.Y + messagesRect.Height + 18, stageWidth - gutter * 2, 568);
        }

        RenderFolders(foldersRect, mode);
        RenderMessages(messagesRect);
        RenderReader(readerRect);
        return readerRect.Y + readerRect.Height + gutter;
    }

    private void RenderMailToolbar(double stageWidth, double gutter, double toolbarHeight, ResponsiveAppMode mode)
    {
        ResponsiveAppUi.SetRect(_toolbarPanel, gutter, gutter, stageWidth - gutter * 2, toolbarHeight);
        _toolbarCanvas.Width = _toolbarPanel.Width;
        _toolbarCanvas.Height = _toolbarPanel.Height;

        ResponsiveAppUi.SetPoint(_toolbarTitle, 20, 14);
        ResponsiveAppUi.SetPoint(_toolbarCaption, 20, 46);

        if (mode == ResponsiveAppMode.Narrow)
        {
            ResponsiveAppUi.SetRect(_searchBox, 20, 74, _toolbarPanel.Width - 152, 36);
            ResponsiveAppUi.SetRect(_composeButton, _toolbarPanel.Width - 120, 74, 100, 36);
            ResponsiveAppUi.Show(_archiveButton, false);
        }
        else
        {
            ResponsiveAppUi.SetRect(_searchBox, _toolbarPanel.Width - 420, 20, 216, 36);
            ResponsiveAppUi.SetRect(_archiveButton, _toolbarPanel.Width - 188, 20, 76, 36);
            ResponsiveAppUi.Show(_archiveButton, true);
            ResponsiveAppUi.SetRect(_composeButton, _toolbarPanel.Width - 100, 20, 80, 36);
        }
    }

    private void RenderFolders(Rect rect, ResponsiveAppMode mode)
    {
        ResponsiveAppUi.SetRect(_foldersPanel, rect.X, rect.Y, rect.Width, rect.Height);
        _foldersCanvas.Width = rect.Width;
        _foldersCanvas.Height = rect.Height;
        ResponsiveAppUi.SetPoint(_foldersTitle, 18, 16);

        if (mode == ResponsiveAppMode.Wide)
        {
            var y = 52d;
            foreach (var button in _folderButtons)
            {
                ResponsiveAppUi.SetRect(button, 16, y, rect.Width - 32, 36);
                y += 44;
            }
        }
        else
        {
            var x = 16d;
            foreach (var button in _folderButtons)
            {
                ResponsiveAppUi.SetRect(button, x, 42, 96, 34);
                x += 104;
            }
        }
    }

    private void RenderMessages(Rect rect)
    {
        ResponsiveAppUi.SetRect(_messagesPanel, rect.X, rect.Y, rect.Width, rect.Height);
        _messagesCanvas.Width = rect.Width;
        _messagesCanvas.Height = rect.Height;
        ResponsiveAppUi.SetPoint(_messagesTitle, 18, 18);
        ResponsiveAppUi.SetPoint(_messagesMeta, 18, 46);

        var rowWidth = rect.Width - 28;
        var y = 78d;
        foreach (var visual in _messageVisuals.Select((value, index) => (value, index)))
        {
            var height = visual.value.Render(_messages[visual.index], rowWidth, visual.index == _selectedIndex);
            ResponsiveAppUi.SetRect(visual.value.Root, 14, y, rowWidth, height);
            y += height + 10;
        }

        _messagesPanel.Height = Math.Max(rect.Height, y + 10);
        _messagesCanvas.Height = _messagesPanel.Height;
    }

    private void RenderReader(Rect rect)
    {
        var selected = _messages[_selectedIndex];
        ResponsiveAppUi.SetRect(_readerPanel, rect.X, rect.Y, rect.Width, rect.Height);
        _readerCanvas.Width = rect.Width;
        _readerCanvas.Height = rect.Height;

        _readerMeta.Text = selected.MetaLine;
        ResponsiveAppUi.SetPoint(_readerMeta, 22, 22);
        ResponsiveAppUi.SetRect(_replyButton, rect.Width - 256, 18, 74, 34);
        ResponsiveAppUi.SetRect(_forwardButton, rect.Width - 172, 18, 74, 34);
        ResponsiveAppUi.SetRect(_readerArchiveButton, rect.Width - 88, 18, 66, 34);

        var titleHeight = _readerTitleView.Render(selected.SubjectPrepared, rect.Width - 44, maxLines: 3);
        ResponsiveAppUi.SetRect(_readerTitleView, 22, 68, rect.Width - 44, titleHeight);

        var bodyHeight = _readerBodyView.Render(selected.BodyPrepared, rect.Width - 44);
        ResponsiveAppUi.SetRect(_readerBodyView, 22, 92 + titleHeight, rect.Width - 44, bodyHeight);

        _readerPanel.Height = Math.Max(rect.Height, 124 + titleHeight + bodyHeight);
        _readerCanvas.Height = _readerPanel.Height;
    }

    private void OnMessageTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Border { Tag: int index })
        {
            _selectedIndex = index;
            RenderScheduler.Schedule();
        }
    }

    private sealed class MailRowVisual
    {
        public MailRowVisual()
        {
            Layer = new Canvas();
            Root = ResponsiveAppUi.CreateSurface(Layer, SampleTheme.PanelBrush, SampleTheme.RuleBrush);
            Avatar = new Ellipse { Fill = SampleTheme.Brush(0xD9, 0xE3, 0xF2) };
            UnreadDot = new Ellipse { Fill = SampleTheme.AccentBrush };
            Sender = ResponsiveAppUi.CreateLabel(string.Empty, 14, SampleTheme.InkBrush, FontWeights.SemiBold);
            Meta = ResponsiveAppUi.CreateLabel(string.Empty, 12, SampleTheme.MutedBrush, FontWeights.Normal);
            SubjectView = new PretextParagraphView("Helvetica Neue", 16, 22, SampleTheme.InkBrush, FontWeights.SemiBold);
            PreviewView = new PretextParagraphView("Helvetica Neue", 14, 19, SampleTheme.MutedBrush, FontWeights.Normal);

            Layer.Children.Add(Avatar);
            Layer.Children.Add(UnreadDot);
            Layer.Children.Add(Sender);
            Layer.Children.Add(Meta);
            Layer.Children.Add(SubjectView);
            Layer.Children.Add(PreviewView);
        }

        public Border Root { get; }

        private Canvas Layer { get; }

        private Ellipse Avatar { get; }

        private Ellipse UnreadDot { get; }

        private TextBlock Sender { get; }

        private TextBlock Meta { get; }

        private PretextParagraphView SubjectView { get; }

        private PretextParagraphView PreviewView { get; }

        public double Render(MailMessageData data, double width, bool selected)
        {
            Root.Background = selected ? SampleTheme.Brush(0xEE, 0xF3, 0xFA) : SampleTheme.PanelBrush;
            Root.BorderBrush = selected ? SampleTheme.Brush(0x88, 0xA8, 0xCF) : SampleTheme.RuleBrush;

            ResponsiveAppUi.SetRect(Avatar, 14, 16, 32, 32);
            ResponsiveAppUi.SetRect(UnreadDot, 24, 26, 12, 12);
            UnreadDot.Visibility = data.Unread ? Visibility.Visible : Visibility.Collapsed;
            Sender.Text = data.Sender;
            ResponsiveAppUi.SetRect(Sender, 56, 16, width - 132, 18);
            Meta.Text = data.MetaLine;
            ResponsiveAppUi.SetRect(Meta, width - 110, 16, 90, 18);

            var titleHeight = SubjectView.Render(data.SubjectPrepared, width - 76, maxLines: 2);
            ResponsiveAppUi.SetRect(SubjectView, 56, 40, width - 76, titleHeight);

            var previewHeight = PreviewView.Render(data.PreviewPrepared, width - 76, maxLines: 2);
            ResponsiveAppUi.SetRect(PreviewView, 56, 44 + titleHeight, width - 76, previewHeight);

            var height = 60 + titleHeight + previewHeight;
            Layer.Width = width;
            Layer.Height = height;
            return height;
        }
    }

    private sealed class MailMessageData
    {
        public MailMessageData(string sender, string subject, string preview, string metaLine, bool unread, string body)
        {
            Sender = sender;
            MetaLine = metaLine;
            Unread = unread;
            SubjectPrepared = PretextLayout.PrepareWithSegments(subject, SubjectFont);
            PreviewPrepared = PretextLayout.PrepareWithSegments(preview, PreviewFont);
            BodyPrepared = PretextLayout.PrepareWithSegments(body, ReaderBodyFont);
        }

        public string Sender { get; }

        public string MetaLine { get; }

        public bool Unread { get; }

        public PreparedTextWithSegments SubjectPrepared { get; }

        public PreparedTextWithSegments PreviewPrepared { get; }

        public PreparedTextWithSegments BodyPrepared { get; }
    }
}

internal sealed class NotesResponsiveSampleView : ResponsiveAppSampleBase
{
    private const string CardTitleFont = "700 17px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string CardExcerptFont = "15px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string EditorBodyFont = "16px \"Helvetica Neue\", Helvetica, Arial, sans-serif";

    private readonly Canvas _toolbarCanvas = new();
    private readonly Border _toolbarPanel;
    private readonly Canvas _collectionCanvas = new();
    private readonly Border _collectionPanel;
    private readonly Canvas _editorCanvas = new();
    private readonly Border _editorPanel;
    private readonly Canvas _insightCanvas = new();
    private readonly Border _insightPanel;

    private readonly TextBlock _toolbarTitle = ResponsiveAppUi.CreateLabel("Field Notes", 24, SampleTheme.InkBrush, FontWeights.Bold);
    private readonly TextBlock _toolbarCaption = ResponsiveAppUi.CreateLabel("Absolute layout with note cards and a reading editor", 13, SampleTheme.MutedBrush, FontWeights.Normal);
    private readonly TextBox _searchBox = ResponsiveAppUi.CreateInput("reflow, notebooks, launch, excerpts", "Search notes");
    private readonly Button _newNoteButton = ResponsiveAppUi.CreateButton("New Note", filled: true);
    private readonly Button _shareButton = ResponsiveAppUi.CreateButton("Share");

    private readonly TextBlock _collectionTitle = ResponsiveAppUi.CreateLabel("Recent notes", 18, SampleTheme.InkBrush, FontWeights.SemiBold);
    private readonly TextBlock _collectionMeta = ResponsiveAppUi.CreateLabel("Cards repack when the viewport changes", 13, SampleTheme.MutedBrush, FontWeights.Normal);
    private readonly List<NoteCardVisual> _cardVisuals = [];

    private readonly TextBlock _editorEyebrow = ResponsiveAppUi.CreateLabel("Selected note", 12, SampleTheme.AccentBrush, FontWeights.SemiBold);
    private readonly TextBox _editorTitleInput = ResponsiveAppUi.CreateInput(string.Empty, "Note title");
    private readonly TextBox _quickCaptureInput = ResponsiveAppUi.CreateInput(string.Empty, "Quick capture");
    private readonly CheckBox _pinCheck = new() { Content = "Pinned" };
    private readonly Button _publishButton = ResponsiveAppUi.CreateButton("Publish", filled: true);
    private readonly PretextParagraphView _editorBodyView = new("Helvetica Neue", 16, 24, SampleTheme.InkBrush, FontWeights.Normal);

    private readonly TextBlock _insightTitle = ResponsiveAppUi.CreateLabel("Context", 18, SampleTheme.InkBrush, FontWeights.SemiBold);
    private readonly TextBlock _insightMeta = ResponsiveAppUi.CreateLabel("Tags and backlinks keep the layout busy", 13, SampleTheme.MutedBrush, FontWeights.Normal);
    private readonly Button[] _tagButtons =
    [
        ResponsiveAppUi.CreateButton("launch"),
        ResponsiveAppUi.CreateButton("ui"),
        ResponsiveAppUi.CreateButton("copy"),
        ResponsiveAppUi.CreateButton("perf"),
    ];
    private readonly PretextParagraphView _insightBody = new("Helvetica Neue", 14, 20, SampleTheme.MutedBrush, FontWeights.Normal);

    private readonly NoteData[] _notes;
    private readonly PreparedTextWithSegments _insightPrepared;
    private int _selectedIndex = 2;

    public NotesResponsiveSampleView()
        : base(
            "Responsive notes workspace",
            "This sample uses built-in controls for search, buttons, and editor affordances, but all note cards and preview text are packed by explicit geometry. The shell flips between three-pane, split, and stacked arrangements without changing the underlying control set.")
    {
        _toolbarPanel = ResponsiveAppUi.CreateSurface(_toolbarCanvas, SampleTheme.Brush(0xF8, 0xF3, 0xEA));
        _collectionPanel = ResponsiveAppUi.CreateSurface(_collectionCanvas);
        _editorPanel = ResponsiveAppUi.CreateSurface(_editorCanvas, SampleTheme.Brush(0xFF, 0xFD, 0xF8));
        _insightPanel = ResponsiveAppUi.CreateSurface(_insightCanvas, SampleTheme.Brush(0xF8, 0xF1, 0xE7));

        Stage.Children.Add(_toolbarPanel);
        Stage.Children.Add(_collectionPanel);
        Stage.Children.Add(_editorPanel);
        Stage.Children.Add(_insightPanel);

        _toolbarCanvas.Children.Add(_toolbarTitle);
        _toolbarCanvas.Children.Add(_toolbarCaption);
        _toolbarCanvas.Children.Add(_searchBox);
        _toolbarCanvas.Children.Add(_newNoteButton);
        _toolbarCanvas.Children.Add(_shareButton);

        _collectionCanvas.Children.Add(_collectionTitle);
        _collectionCanvas.Children.Add(_collectionMeta);

        _editorCanvas.Children.Add(_editorEyebrow);
        _editorCanvas.Children.Add(_editorTitleInput);
        _editorCanvas.Children.Add(_quickCaptureInput);
        _editorCanvas.Children.Add(_pinCheck);
        _editorCanvas.Children.Add(_publishButton);
        _editorCanvas.Children.Add(_editorBodyView);

        _insightCanvas.Children.Add(_insightTitle);
        _insightCanvas.Children.Add(_insightMeta);
        foreach (var tag in _tagButtons)
        {
            _insightCanvas.Children.Add(tag);
        }

        _insightCanvas.Children.Add(_insightBody);

        _insightPrepared = PretextLayout.PrepareWithSegments(
            "The note cards on the left are a small masonry-like collection. Their heights come from Pretext, then the cards repack between one and two columns as the available width changes. The editor and context rail keep their built-in controls, but their panel rects are still driven by our own layout pass.",
            CardExcerptFont);

        _notes =
        [
            new NoteData("Window-size canary", "Track the exact moment the shell leaves the wide three-pane composition and enters the tablet split.", "This note is mostly about watching layout change without any control measuring its own content.", "Today"),
            new NoteData("Mail reader polish", "Keep the reply row stable while the title wraps from one line to two on medium widths.", "Reader actions should stay aligned to the top edge of the panel even while the subject block grows.", "Today"),
            new NoteData("Todo shell principles", "Use normal Buttons and TextBoxes, but place them with explicit geometry so the demo stays honest.", "The sample should feel like an actual product surface, not like a generic layout experiment wearing app colors.", "Yesterday"),
            new NoteData("Backlink inventory", "Collect the editorial, masonry, and virtual-list references in one searchable workspace.", "This is a good note to keep selected on narrow widths because it is long enough to force the editor body to reflow.", "Yesterday"),
            new NoteData("Launch copy trims", "Remove filler words so the note cards stay dense in the two-column collection.", "Tighter copy gives the collection more rhythm and makes the repacking behavior easier to see when the window changes size.", "Fri"),
            new NoteData("Perf screenshot plan", "Capture three widths and annotate the same control tree in each state.", "The point is to prove we are reflowing one surface, not swapping in a second implementation for mobile.", "Fri"),
        ];

        for (var index = 0; index < _notes.Length; index++)
        {
            var visual = new NoteCardVisual();
            visual.Root.Tag = index;
            visual.Root.Tapped += OnCardTapped;
            _cardVisuals.Add(visual);
            _collectionCanvas.Children.Add(visual.Root);
        }
    }

    protected override double RenderStage(double stageWidth)
    {
        var mode = ResponsiveAppUi.ResolveMode(stageWidth);
        var gutter = mode == ResponsiveAppMode.Narrow ? 18 : 24;
        var toolbarHeight = mode == ResponsiveAppMode.Narrow ? 118 : 76;

        RenderNotesToolbar(stageWidth, gutter, toolbarHeight, mode);

        Rect collectionRect;
        Rect editorRect;
        Rect insightRect;
        if (mode == ResponsiveAppMode.Wide)
        {
            collectionRect = new Rect(gutter, gutter + toolbarHeight + 18, 320, 660);
            editorRect = new Rect(collectionRect.X + collectionRect.Width + 16, collectionRect.Y, stageWidth - (collectionRect.X + collectionRect.Width) - gutter - 252, 660);
            insightRect = new Rect(editorRect.X + editorRect.Width + 16, collectionRect.Y, 220, 660);
        }
        else if (mode == ResponsiveAppMode.Medium)
        {
            collectionRect = new Rect(gutter, gutter + toolbarHeight + 18, 324, 600);
            editorRect = new Rect(collectionRect.X + collectionRect.Width + 16, collectionRect.Y, stageWidth - (collectionRect.X + collectionRect.Width) - gutter - 16, 420);
            insightRect = new Rect(collectionRect.X + collectionRect.Width + 16, editorRect.Y + editorRect.Height + 18, editorRect.Width, 162);
        }
        else
        {
            collectionRect = new Rect(gutter, gutter + toolbarHeight + 18, stageWidth - gutter * 2, 420);
            editorRect = new Rect(gutter, collectionRect.Y + collectionRect.Height + 18, stageWidth - gutter * 2, 470);
            insightRect = new Rect(gutter, editorRect.Y + editorRect.Height + 18, stageWidth - gutter * 2, 182);
        }

        RenderCollection(collectionRect);
        RenderEditor(editorRect);
        RenderInsights(insightRect);
        return insightRect.Y + insightRect.Height + gutter;
    }

    private void RenderNotesToolbar(double stageWidth, double gutter, double toolbarHeight, ResponsiveAppMode mode)
    {
        ResponsiveAppUi.SetRect(_toolbarPanel, gutter, gutter, stageWidth - gutter * 2, toolbarHeight);
        _toolbarCanvas.Width = _toolbarPanel.Width;
        _toolbarCanvas.Height = _toolbarPanel.Height;
        ResponsiveAppUi.SetPoint(_toolbarTitle, 20, 14);
        ResponsiveAppUi.SetPoint(_toolbarCaption, 20, 46);

        if (mode == ResponsiveAppMode.Narrow)
        {
            ResponsiveAppUi.SetRect(_searchBox, 20, 74, _toolbarPanel.Width - 152, 36);
            ResponsiveAppUi.SetRect(_newNoteButton, _toolbarPanel.Width - 120, 74, 100, 36);
            ResponsiveAppUi.Show(_shareButton, false);
        }
        else
        {
            ResponsiveAppUi.SetRect(_searchBox, _toolbarPanel.Width - 420, 20, 220, 36);
            ResponsiveAppUi.SetRect(_shareButton, _toolbarPanel.Width - 188, 20, 76, 36);
            ResponsiveAppUi.Show(_shareButton, true);
            ResponsiveAppUi.SetRect(_newNoteButton, _toolbarPanel.Width - 100, 20, 80, 36);
        }
    }

    private void RenderCollection(Rect rect)
    {
        ResponsiveAppUi.SetRect(_collectionPanel, rect.X, rect.Y, rect.Width, rect.Height);
        _collectionCanvas.Width = rect.Width;
        _collectionCanvas.Height = rect.Height;
        ResponsiveAppUi.SetPoint(_collectionTitle, 18, 18);
        ResponsiveAppUi.SetPoint(_collectionMeta, 18, 46);

        var innerWidth = rect.Width - 28;
        var columns = innerWidth >= 440 ? 2 : 1;
        var gap = 12d;
        var cardWidth = columns == 1
            ? innerWidth
            : (innerWidth - gap) / 2;
        var columnHeights = Enumerable.Repeat(82d, columns).ToArray();

        for (var index = 0; index < _cardVisuals.Count; index++)
        {
            var targetColumn = 0;
            for (var c = 1; c < columns; c++)
            {
                if (columnHeights[c] < columnHeights[targetColumn])
                {
                    targetColumn = c;
                }
            }

            var height = _cardVisuals[index].Render(_notes[index], cardWidth, index == _selectedIndex);
            var x = 14 + targetColumn * (cardWidth + gap);
            var y = columnHeights[targetColumn];
            ResponsiveAppUi.SetRect(_cardVisuals[index].Root, x, y, cardWidth, height);
            columnHeights[targetColumn] += height + gap;
        }

        var contentHeight = columnHeights.Max() + 8;
        _collectionPanel.Height = Math.Max(rect.Height, contentHeight);
        _collectionCanvas.Height = _collectionPanel.Height;
    }

    private void RenderEditor(Rect rect)
    {
        var selected = _notes[_selectedIndex];
        ResponsiveAppUi.SetRect(_editorPanel, rect.X, rect.Y, rect.Width, rect.Height);
        _editorCanvas.Width = rect.Width;
        _editorCanvas.Height = rect.Height;

        ResponsiveAppUi.SetPoint(_editorEyebrow, 22, 20);
        _editorTitleInput.Text = selected.Title;
        ResponsiveAppUi.SetRect(_editorTitleInput, 22, 48, rect.Width - 144, 38);
        _pinCheck.IsChecked = _selectedIndex % 2 == 0;
        ResponsiveAppUi.SetRect(_pinCheck, rect.Width - 110, 48, 88, 24);
        ResponsiveAppUi.SetRect(_quickCaptureInput, 22, 98, rect.Width - 138, 38);
        ResponsiveAppUi.SetRect(_publishButton, rect.Width - 108, 98, 86, 38);

        var bodyHeight = _editorBodyView.Render(selected.BodyPrepared, rect.Width - 44);
        ResponsiveAppUi.SetRect(_editorBodyView, 22, 152, rect.Width - 44, bodyHeight);

        _editorPanel.Height = Math.Max(rect.Height, 184 + bodyHeight);
        _editorCanvas.Height = _editorPanel.Height;
    }

    private void RenderInsights(Rect rect)
    {
        ResponsiveAppUi.SetRect(_insightPanel, rect.X, rect.Y, rect.Width, rect.Height);
        _insightCanvas.Width = rect.Width;
        _insightCanvas.Height = rect.Height;
        ResponsiveAppUi.SetPoint(_insightTitle, 18, 18);
        ResponsiveAppUi.SetPoint(_insightMeta, 18, 46);

        var x = 18d;
        for (var index = 0; index < _tagButtons.Length; index++)
        {
            ResponsiveAppUi.SetRect(_tagButtons[index], x, 76, 78, 32);
            x += 86;
        }

        var bodyHeight = _insightBody.Render(_insightPrepared, rect.Width - 36, maxLines: 5);
        ResponsiveAppUi.SetRect(_insightBody, 18, 120, rect.Width - 36, bodyHeight);
    }

    private void OnCardTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Border { Tag: int index })
        {
            _selectedIndex = index;
            RenderScheduler.Schedule();
        }
    }

    private sealed class NoteCardVisual
    {
        public NoteCardVisual()
        {
            Layer = new Canvas();
            Root = ResponsiveAppUi.CreateSurface(Layer);
            Category = ResponsiveAppUi.CreateLabel(string.Empty, 12, SampleTheme.AccentBrush, FontWeights.SemiBold);
            Updated = ResponsiveAppUi.CreateLabel(string.Empty, 12, SampleTheme.MutedBrush, FontWeights.Normal);
            TitleView = new PretextParagraphView("Helvetica Neue", 17, 23, SampleTheme.InkBrush, FontWeights.SemiBold);
            ExcerptView = new PretextParagraphView("Helvetica Neue", 15, 20, SampleTheme.MutedBrush, FontWeights.Normal);

            Layer.Children.Add(Category);
            Layer.Children.Add(Updated);
            Layer.Children.Add(TitleView);
            Layer.Children.Add(ExcerptView);
        }

        public Border Root { get; }

        private Canvas Layer { get; }

        private TextBlock Category { get; }

        private TextBlock Updated { get; }

        private PretextParagraphView TitleView { get; }

        private PretextParagraphView ExcerptView { get; }

        public double Render(NoteData note, double width, bool selected)
        {
            Root.Background = selected ? SampleTheme.AccentSoftBrush : SampleTheme.PanelBrush;
            Root.BorderBrush = selected ? SampleTheme.AccentBrush : SampleTheme.RuleBrush;
            Category.Text = "Notebook";
            Updated.Text = note.Updated;
            ResponsiveAppUi.SetRect(Category, 14, 14, width - 88, 18);
            ResponsiveAppUi.SetRect(Updated, width - 74, 14, 60, 18);

            var titleHeight = TitleView.Render(note.TitlePrepared, width - 28, maxLines: 3);
            ResponsiveAppUi.SetRect(TitleView, 14, 40, width - 28, titleHeight);

            var excerptHeight = ExcerptView.Render(note.ExcerptPrepared, width - 28, maxLines: 4);
            ResponsiveAppUi.SetRect(ExcerptView, 14, 48 + titleHeight, width - 28, excerptHeight);

            var height = 68 + titleHeight + excerptHeight;
            Layer.Width = width;
            Layer.Height = height;
            return height;
        }
    }

    private sealed class NoteData
    {
        public NoteData(string title, string excerpt, string body, string updated)
        {
            Title = title;
            Updated = updated;
            TitlePrepared = PretextLayout.PrepareWithSegments(title, CardTitleFont);
            ExcerptPrepared = PretextLayout.PrepareWithSegments(excerpt, CardExcerptFont);
            BodyPrepared = PretextLayout.PrepareWithSegments(body, EditorBodyFont);
        }

        public string Title { get; }

        public string Updated { get; }

        public PreparedTextWithSegments TitlePrepared { get; }

        public PreparedTextWithSegments ExcerptPrepared { get; }

        public PreparedTextWithSegments BodyPrepared { get; }
    }
}
