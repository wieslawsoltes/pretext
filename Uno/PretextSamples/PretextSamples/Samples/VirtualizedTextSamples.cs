using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Pretext.Uno;

namespace PretextSamples.Samples;

public sealed class VirtualWrapTilesSampleView : UserControl
{
    private readonly TextBlock _status = SampleUi.CreateBodyText("Preparing 100,000 text tiles…");
    private readonly PretextVirtualizedWrapPanel _wrapPanel = new();
    private readonly StretchScrollHost _pageRoot;

    public VirtualWrapTilesSampleView()
    {
        _wrapPanel.ViewportChanged += stats =>
        {
            _status.Text = $"100,000 tiles • {stats.VisibleItemCount} visible • rows {stats.StartBandIndex + 1:N0}-{stats.EndBandIndexExclusive:N0}";
        };

        var stack = SampleUi.CreatePageStack();
        stack.Children.Add(SampleUi.CreateHeader(
            "DEMO",
            "100k non-uniform wrap panel",
            "This page uses the same explicit placement idea as masonry: precompute each tile box with Pretext, wrap them row by row with arithmetic, and realize only the rows crossing the viewport. No WrapPanel, ItemsRepeater, or measured placeholders."));
        stack.Children.Add(_status);
        stack.Children.Add(SampleUi.CreateCard(_wrapPanel, 0));

        _pageRoot = (StretchScrollHost)SampleUi.CreatePageRoot(stack);
        Content = _pageRoot;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _wrapPanel.AttachScrollHost(_pageRoot);
        _wrapPanel.SetSource(VirtualizedTextTileFactory.GetWrapSource());
    }
}

public sealed class VirtualListTilesSampleView : UserControl
{
    private readonly TextBlock _status = SampleUi.CreateBodyText("Preparing 100,000 virtual rows…");
    private readonly TextBlock _selection = SampleUi.CreateBodyText("Click a row to pin its content.", 14);
    private readonly PretextVirtualizedListBox _listBox = new();
    private readonly StretchScrollHost _pageRoot;

    public VirtualListTilesSampleView()
    {
        _listBox.ViewportChanged += stats =>
        {
            _status.Text = $"100,000 rows • {stats.VisibleItemCount} visible • rows {stats.StartIndex + 1:N0}-{stats.EndIndexExclusive:N0}";
        };

        _listBox.SelectionChanged += index =>
        {
            if (_listBox.Source is null || index < 0 || index >= _listBox.Source.Count)
            {
                _selection.Text = "Click a row to pin its content.";
                return;
            }

            var template = _listBox.Source.Templates[_listBox.Source.TemplateIndices[index]];
            _selection.Text = $"Selected row {index + 1:N0} • {template.Category} • {template.BodyText}";
        };

        var stack = SampleUi.CreatePageStack();
        stack.Children.Add(SampleUi.CreateHeader(
            "DEMO",
            "100k non-uniform list box",
            "This list keeps exact variable row heights without ListView. The same precomputed-geometry + occlusion pattern from masonry drives a custom Canvas-based list surface with selection and zero measurement reads."));
        stack.Children.Add(_status);
        stack.Children.Add(_selection);
        stack.Children.Add(SampleUi.CreateCard(_listBox, 0));

        _pageRoot = (StretchScrollHost)SampleUi.CreatePageRoot(stack);
        Content = _pageRoot;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _listBox.AttachScrollHost(_pageRoot);
        _listBox.SetSource(VirtualizedTextTileFactory.GetListSource());
    }
}

internal readonly record struct VirtualizedViewportStats(
    int VisibleItemCount,
    int StartIndex,
    int EndIndexExclusive,
    int StartBandIndex,
    int EndBandIndexExclusive);

internal readonly record struct WrapTileTemplate(
    string Text,
    double BodyWidth,
    double BodyHeight,
    double TileWidth,
    double TileHeight,
    SolidColorBrush Background,
    SolidColorBrush BorderBrush);

internal sealed class WrapTileSource(IReadOnlyList<WrapTileTemplate> templates, int[] templateIndices)
{
    public IReadOnlyList<WrapTileTemplate> Templates { get; } = templates;

    public int[] TemplateIndices { get; } = templateIndices;

    public int Count => TemplateIndices.Length;
}

internal readonly record struct ListTileTemplate(string Category, string BodyText, PreparedText Prepared);

internal sealed class ListTileSource(IReadOnlyList<ListTileTemplate> templates, int[] templateIndices)
{
    public IReadOnlyList<ListTileTemplate> Templates { get; } = templates;

    public int[] TemplateIndices { get; } = templateIndices;

    public int Count => TemplateIndices.Length;

    public string GetTitle(int index)
    {
        var template = Templates[TemplateIndices[index]];
        return $"{template.Category} {index + 1:N0}";
    }
}

internal static class VirtualizedTextTileFactory
{
    private const string WrapFont = "15px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const double WrapLineHeight = 20;
    private const double WrapMinBodyWidth = 96;
    private const double WrapMaxBodyWidth = 238;
    private const double WrapTilePadding = 14;
    private const double WrapTileBorderThickness = 1;

    private const string ListBodyFont = "15px \"Helvetica Neue\", Helvetica, Arial, sans-serif";

    private const int TotalItemCount = 100_000;

    private static WrapTileSource? _wrapSource;
    private static ListTileSource? _listSource;

    private static readonly SolidColorBrush[] WrapBackgrounds =
    [
        SampleTheme.PanelBrush,
        SampleTheme.AccentSoftBrush,
        SampleTheme.Brush(0xF0, 0xEE, 0xE8),
        SampleTheme.Brush(0xEE, 0xF1, 0xEA),
        SampleTheme.Brush(0xEA, 0xEC, 0xF4),
    ];

    private static readonly SolidColorBrush[] WrapBorders =
    [
        SampleTheme.RuleBrush,
        SampleTheme.Brush(0xCF, 0xBF, 0xB0),
        SampleTheme.Brush(0xC8, 0xD4, 0xC2),
        SampleTheme.Brush(0xC8, 0xC9, 0xDA),
        SampleTheme.Brush(0xD9, 0xC8, 0xBE),
    ];

    private static readonly string[] WrapPrefixes =
    [
        "Zero-reflow labels",
        "Viewport tiles",
        "Mixed-script notes",
        "Adaptive chips",
        "Pinned summaries",
        "Warm cache rows",
        "Editorial snippets",
        "Shrink-wrapped cards",
        "Masonry probes",
        "Live preview tiles",
        "Cursor-safe excerpts",
        "Selection-ready cells",
    ];

    private static readonly string[] WrapVerbs =
    [
        "keep their predicted width",
        "reuse measured segments",
        "skip live tree reads",
        "hold line counts steady",
        "avoid placeholder jumps",
        "survive rapid resize",
        "stay dense in motion",
        "preserve exact wraps",
        "cull cleanly off-screen",
        "fit before paint",
    ];

    private static readonly string[] WrapTails =
    [
        "while the viewport slides",
        "under a 100k tile load",
        "through mixed punctuation",
        "during binary-search shrinkwrap",
        "without any built-in panel",
        "with explicit row occlusion",
        "inside a pooled Canvas surface",
        "as the scroll range shifts",
        "with cache hits already warm",
        "before a single UI element measures",
    ];

    private static readonly string[] ListCategories =
    [
        "Queue",
        "Alert",
        "Memo",
        "Probe",
        "Draft",
        "Review",
        "Thread",
        "Note",
    ];

    private static readonly string[] ListClauses =
    [
        "Pretext computes the exact row height before the item is realized.",
        "The visible pool only tracks rows that intersect the viewport band.",
        "Selection state stays outside the layout path so reuse stays cheap.",
        "No ListView or ItemsRepeater is involved in the scrolling surface.",
        "The line count is predicted once and reused through the hot resize path.",
        "Mixed punctuation and long labels are still measured with the same engine.",
        "This row exists mainly to stress non-uniform virtualization under load.",
        "Occlusion happens at the row band, not by scanning every realized element.",
        "The row can be rebound to another index without asking the UI tree for size.",
        "Exact text geometry keeps the scroll extent stable during fast movement.",
        "Only the visible canvas children exist, even though the dataset has 100,000 rows.",
        "This sample uses the same arithmetic-first idea as the masonry page.",
    ];

    public static WrapTileSource GetWrapSource()
    {
        return _wrapSource ??= BuildWrapSource();
    }

    public static ListTileSource GetListSource()
    {
        return _listSource ??= BuildListSource();
    }

    private static WrapTileSource BuildWrapSource()
    {
        const int templateCount = 480;
        var templates = new List<WrapTileTemplate>(templateCount);
        for (var index = 0; index < templateCount; index++)
        {
            var text = BuildWrapText(index);
            var prepared = PretextLayout.PrepareWithSegments(text, WrapFont);
            var tightMetrics = SampleTextMetrics.FindTightWrapMetrics(prepared, WrapMaxBodyWidth, WrapLineHeight);
            var bodyWidth = Math.Clamp(Math.Ceiling(Math.Max(WrapMinBodyWidth, tightMetrics.MaxLineWidth)), WrapMinBodyWidth, WrapMaxBodyWidth);
            var bodyMetrics = SampleTextMetrics.CollectWrapMetrics(prepared, bodyWidth, WrapLineHeight);
            var tileWidth = bodyWidth + (WrapTilePadding + WrapTileBorderThickness) * 2;
            var tileHeight = bodyMetrics.Height + (WrapTilePadding + WrapTileBorderThickness) * 2;
            templates.Add(new WrapTileTemplate(
                text,
                bodyWidth,
                bodyMetrics.Height,
                tileWidth,
                tileHeight,
                WrapBackgrounds[index % WrapBackgrounds.Length],
                WrapBorders[index % WrapBorders.Length]));
        }

        return new WrapTileSource(templates, BuildTemplateIndices(TotalItemCount, templates.Count, 73, 197));
    }

    private static ListTileSource BuildListSource()
    {
        const int templateCount = 320;
        var templates = new List<ListTileTemplate>(templateCount);
        for (var index = 0; index < templateCount; index++)
        {
            var category = ListCategories[index % ListCategories.Length];
            var body = BuildListBody(index);
            templates.Add(new ListTileTemplate(category, body, PretextLayout.Prepare(body, ListBodyFont)));
        }

        return new ListTileSource(templates, BuildTemplateIndices(TotalItemCount, templates.Count, 89, 431));
    }

    private static string BuildWrapText(int index)
    {
        var prefix = WrapPrefixes[index % WrapPrefixes.Length];
        var verb = WrapVerbs[(index / WrapPrefixes.Length) % WrapVerbs.Length];
        var tail = WrapTails[(index / (WrapPrefixes.Length * WrapVerbs.Length)) % WrapTails.Length];

        return (index % 4) switch
        {
            0 => $"{prefix} {verb} {tail}.",
            1 => $"{prefix} {verb}.",
            2 => $"{prefix} {tail}.",
            _ => $"{prefix} {verb} {tail} with exact text math.",
        };
    }

    private static string BuildListBody(int index)
    {
        var clauseCount = 2 + index % 3;
        var parts = new string[clauseCount];
        for (var i = 0; i < clauseCount; i++)
        {
            parts[i] = ListClauses[(index * 3 + i * 5) % ListClauses.Length];
        }

        return string.Join(' ', parts);
    }

    private static int[] BuildTemplateIndices(int itemCount, int templateCount, int multiplier, int divisor)
    {
        var indices = new int[itemCount];
        for (var index = 0; index < itemCount; index++)
        {
            indices[index] = (int)(((long)index * multiplier + index / divisor + (index % 17) * 11) % templateCount);
        }

        return indices;
    }
}

internal sealed class PretextVirtualizedWrapPanel : UserControl
{
    private const double TileGap = 10;
    private const double SurfacePadding = 18;
    private const double TilePadding = 14;
    private const double TileBorderThickness = 1;
    private const double ViewportOverscan = 240;
    private const double BodyLineHeight = 20;

    private readonly Canvas _canvas = new();
    private readonly List<WrapTileVisual> _visualPool = [];
    private readonly UiRenderScheduler _renderScheduler;
    private StretchScrollHost? _scrollHost;
    private WrapTileSource? _source;
    private WrapLayoutState? _layoutState;
    private bool _scrollHooked;
    private double _lastAvailableWidth = -1;

    public PretextVirtualizedWrapPanel()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        MinHeight = 520;
        Content = _canvas;
        _renderScheduler = new UiRenderScheduler(DispatcherQueue, Render);
        Loaded += (_, _) => _renderScheduler.Schedule();
        SizeChanged += (_, _) => _renderScheduler.Schedule();
    }

    public event Action<VirtualizedViewportStats>? ViewportChanged;

    public void AttachScrollHost(StretchScrollHost scrollHost)
    {
        if (_scrollHost == scrollHost && _scrollHooked)
        {
            return;
        }

        _scrollHost = scrollHost;
        if (_scrollHooked)
        {
            return;
        }

        _scrollHost.ScrollViewer.ViewChanged += (_, _) => _renderScheduler.Schedule();
        _scrollHooked = true;
        _renderScheduler.Schedule();
    }

    public void SetSource(WrapTileSource source)
    {
        _source = source;
        _layoutState = null;
        _lastAvailableWidth = -1;
        _renderScheduler.Schedule();
    }

    private void Render()
    {
        if (_source is null || _scrollHost is null || ActualWidth <= 0)
        {
            return;
        }

        var availableWidth = Math.Max(280, ActualWidth);
        if (_layoutState is null || Math.Abs(availableWidth - _lastAvailableWidth) >= 0.5)
        {
            _lastAvailableWidth = availableWidth;
            _layoutState = ComputeLayout(_source, availableWidth);
            _canvas.Width = availableWidth;
            _canvas.Height = _layoutState.ContentHeight;
            Height = _layoutState.ContentHeight;
        }

        var viewportTop = 0d;
        var viewportBottom = Math.Min(_layoutState.ContentHeight, 1200);
        _scrollHost.TryGetLocalViewportBounds(this, ViewportOverscan, out viewportTop, out viewportBottom);

        if (!_layoutState.Occlusion.TryQuery(viewportTop, viewportBottom, out var range))
        {
            EnsureVisualPool(0);
            ViewportChanged?.Invoke(new VirtualizedViewportStats(0, 0, 0, 0, 0));
            return;
        }

        var visibleCount = range.EndIndexExclusive - range.StartIndex;
        EnsureVisualPool(visibleCount);

        var poolIndex = 0;
        for (var itemIndex = range.StartIndex; itemIndex < range.EndIndexExclusive; itemIndex++, poolIndex++)
        {
            var placement = _layoutState.Placements[itemIndex];
            var template = _source.Templates[_source.TemplateIndices[itemIndex]];
            var visual = _visualPool[poolIndex];
            visual.Root.Width = placement.Width;
            visual.Root.Height = placement.Height;
            visual.Root.Background = template.Background;
            visual.Root.BorderBrush = template.BorderBrush;
            visual.Body.Width = template.BodyWidth;
            visual.Body.Text = template.Text;
            Canvas.SetLeft(visual.Root, placement.X);
            Canvas.SetTop(visual.Root, placement.Y);
        }

        ViewportChanged?.Invoke(new VirtualizedViewportStats(
            visibleCount,
            range.StartIndex,
            range.EndIndexExclusive,
            range.StartBandIndex,
            range.EndBandIndexExclusive));
    }

    private WrapLayoutState ComputeLayout(WrapTileSource source, double availableWidth)
    {
        var innerWidth = Math.Max(160, availableWidth - SurfacePadding * 2);
        var placements = new List<WrapPlacement>(source.Count);
        var bands = new List<VerticalBand>();

        var rowStartIndex = 0;
        var x = 0d;
        var y = SurfacePadding;
        var rowHeight = 0d;

        for (var itemIndex = 0; itemIndex < source.Count; itemIndex++)
        {
            var template = source.Templates[source.TemplateIndices[itemIndex]];
            if (x > 0 && x + template.TileWidth > innerWidth)
            {
                bands.Add(new VerticalBand(rowStartIndex, itemIndex, y, y + rowHeight));
                y += rowHeight + TileGap;
                x = 0;
                rowHeight = 0;
                rowStartIndex = itemIndex;
            }

            placements.Add(new WrapPlacement(itemIndex, SurfacePadding + x, y, template.TileWidth, template.TileHeight));
            x += template.TileWidth + TileGap;
            rowHeight = Math.Max(rowHeight, template.TileHeight);
        }

        if (source.Count > 0)
        {
            bands.Add(new VerticalBand(rowStartIndex, source.Count, y, y + rowHeight));
            y += rowHeight;
        }

        var contentHeight = y + SurfacePadding;
        return new WrapLayoutState(placements, new VerticalOcclusionIndex(bands), contentHeight);
    }

    private void EnsureVisualPool(int count)
    {
        while (_visualPool.Count < count)
        {
            var visual = new WrapTileVisual(TilePadding, TileBorderThickness, BodyLineHeight);
            _visualPool.Add(visual);
            _canvas.Children.Add(visual.Root);
        }

        for (var index = 0; index < _visualPool.Count; index++)
        {
            _visualPool[index].Root.Visibility = index < count ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private readonly record struct WrapPlacement(int ItemIndex, double X, double Y, double Width, double Height);

    private sealed record WrapLayoutState(
        IReadOnlyList<WrapPlacement> Placements,
        VerticalOcclusionIndex Occlusion,
        double ContentHeight);

    private sealed class WrapTileVisual
    {
        public WrapTileVisual(double padding, double borderThickness, double lineHeight)
        {
            Body = new TextBlock
            {
                Foreground = SampleTheme.InkBrush,
                FontSize = 15,
                FontFamily = new FontFamily("Helvetica Neue"),
                LineHeight = lineHeight,
                TextWrapping = TextWrapping.WrapWholeWords,
            };

            Root = new Border
            {
                BorderThickness = new Thickness(borderThickness),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(padding),
                Child = Body,
            };
        }

        public Border Root { get; }

        public TextBlock Body { get; }
    }
}

internal sealed class PretextVirtualizedListBox : UserControl
{
    private const double SurfacePadding = 16;
    private const double ItemGap = 8;
    private const double ItemPaddingX = 16;
    private const double ItemPaddingTop = 14;
    private const double ItemPaddingBottom = 16;
    private const double ItemBorderThickness = 1;
    private const double TitleLineHeight = 18;
    private const double TitleGap = 10;
    private const double BodyLineHeight = 21;
    private const double ViewportOverscan = 320;

    private readonly Canvas _canvas = new();
    private readonly List<ListTileVisual> _visualPool = [];
    private readonly UiRenderScheduler _renderScheduler;
    private StretchScrollHost? _scrollHost;
    private ListTileSource? _source;
    private ListLayoutState? _layoutState;
    private bool _scrollHooked;
    private double _lastAvailableWidth = -1;
    private int _selectedIndex;

    public PretextVirtualizedListBox()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        MinHeight = 620;
        Content = _canvas;
        _renderScheduler = new UiRenderScheduler(DispatcherQueue, Render);
        Loaded += (_, _) => _renderScheduler.Schedule();
        SizeChanged += (_, _) => _renderScheduler.Schedule();
    }

    public event Action<VirtualizedViewportStats>? ViewportChanged;

    public event Action<int>? SelectionChanged;

    public ListTileSource? Source => _source;

    public void AttachScrollHost(StretchScrollHost scrollHost)
    {
        if (_scrollHost == scrollHost && _scrollHooked)
        {
            return;
        }

        _scrollHost = scrollHost;
        if (_scrollHooked)
        {
            return;
        }

        _scrollHost.ScrollViewer.ViewChanged += (_, _) => _renderScheduler.Schedule();
        _scrollHooked = true;
        _renderScheduler.Schedule();
    }

    public void SetSource(ListTileSource source)
    {
        _source = source;
        _layoutState = null;
        _lastAvailableWidth = -1;
        _selectedIndex = 0;
        SelectionChanged?.Invoke(_selectedIndex);
        _renderScheduler.Schedule();
    }

    private void Render()
    {
        if (_source is null || _scrollHost is null || ActualWidth <= 0)
        {
            return;
        }

        var availableWidth = Math.Max(320, ActualWidth);
        if (_layoutState is null || Math.Abs(availableWidth - _lastAvailableWidth) >= 0.5)
        {
            _lastAvailableWidth = availableWidth;
            _layoutState = ComputeLayout(_source, availableWidth);
            _canvas.Width = availableWidth;
            _canvas.Height = _layoutState.ContentHeight;
            Height = _layoutState.ContentHeight;
        }

        var viewportTop = 0d;
        var viewportBottom = Math.Min(_layoutState.ContentHeight, 1200);
        _scrollHost.TryGetLocalViewportBounds(this, ViewportOverscan, out viewportTop, out viewportBottom);

        if (!_layoutState.Occlusion.TryQuery(viewportTop, viewportBottom, out var range))
        {
            EnsureVisualPool(0);
            ViewportChanged?.Invoke(new VirtualizedViewportStats(0, 0, 0, 0, 0));
            return;
        }

        var visibleCount = range.EndIndexExclusive - range.StartIndex;
        EnsureVisualPool(visibleCount);

        var poolIndex = 0;
        for (var itemIndex = range.StartIndex; itemIndex < range.EndIndexExclusive; itemIndex++, poolIndex++)
        {
            var placement = _layoutState.Placements[itemIndex];
            var templateIndex = _source.TemplateIndices[itemIndex];
            var template = _source.Templates[templateIndex];
            var visual = _visualPool[poolIndex];
            var isSelected = itemIndex == _selectedIndex;
            visual.Root.Tag = itemIndex;
            visual.Root.Width = placement.Width;
            visual.Root.Height = placement.Height;
            visual.Root.Background = isSelected ? SampleTheme.AccentSoftBrush : SampleTheme.PanelBrush;
            visual.Root.BorderBrush = isSelected ? SampleTheme.AccentBrush : SampleTheme.RuleBrush;
            visual.Stripe.Fill = isSelected ? SampleTheme.AccentBrush : SampleTheme.RuleBrush;
            visual.Stripe.Height = placement.Height;
            visual.Layer.Width = placement.Width;
            visual.Layer.Height = placement.Height;
            visual.Title.Text = _source.GetTitle(itemIndex);
            visual.Body.Text = template.BodyText;
            visual.Body.Width = _layoutState.BodyWidth;
            Canvas.SetLeft(visual.Root, placement.X);
            Canvas.SetTop(visual.Root, placement.Y);
        }

        ViewportChanged?.Invoke(new VirtualizedViewportStats(
            visibleCount,
            range.StartIndex,
            range.EndIndexExclusive,
            range.StartBandIndex,
            range.EndBandIndexExclusive));
    }

    private ListLayoutState ComputeLayout(ListTileSource source, double availableWidth)
    {
        var itemWidth = Math.Max(280, availableWidth - SurfacePadding * 2);
        var bodyWidth = Math.Max(120, itemWidth - (ItemPaddingX + ItemBorderThickness) * 2);
        var templateHeights = new double[source.Templates.Count];

        for (var index = 0; index < source.Templates.Count; index++)
        {
            var bodyMetrics = PretextLayout.Layout(source.Templates[index].Prepared, bodyWidth, BodyLineHeight);
            templateHeights[index] = ItemPaddingTop + TitleLineHeight + TitleGap + bodyMetrics.Height + ItemPaddingBottom + ItemBorderThickness * 2;
        }

        var placements = new List<ListPlacement>(source.Count);
        var bands = new List<VerticalBand>(source.Count);
        var y = SurfacePadding;
        for (var itemIndex = 0; itemIndex < source.Count; itemIndex++)
        {
            var height = templateHeights[source.TemplateIndices[itemIndex]];
            placements.Add(new ListPlacement(itemIndex, SurfacePadding, y, itemWidth, height));
            bands.Add(new VerticalBand(itemIndex, itemIndex + 1, y, y + height));
            y += height + ItemGap;
        }

        var contentHeight = y - ItemGap + SurfacePadding;
        return new ListLayoutState(placements, new VerticalOcclusionIndex(bands), bodyWidth, contentHeight);
    }

    private void EnsureVisualPool(int count)
    {
        while (_visualPool.Count < count)
        {
            var visual = new ListTileVisual(ItemPaddingX, ItemPaddingTop, TitleLineHeight, TitleGap, BodyLineHeight);
            visual.Root.Tapped += OnItemTapped;
            _visualPool.Add(visual);
            _canvas.Children.Add(visual.Root);
        }

        for (var index = 0; index < _visualPool.Count; index++)
        {
            _visualPool[index].Root.Visibility = index < count ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Border { Tag: int itemIndex })
        {
            return;
        }

        if (itemIndex == _selectedIndex)
        {
            return;
        }

        _selectedIndex = itemIndex;
        SelectionChanged?.Invoke(itemIndex);
        _renderScheduler.Schedule();
    }

    private readonly record struct ListPlacement(int ItemIndex, double X, double Y, double Width, double Height);

    private sealed record ListLayoutState(
        IReadOnlyList<ListPlacement> Placements,
        VerticalOcclusionIndex Occlusion,
        double BodyWidth,
        double ContentHeight);

    private sealed class ListTileVisual
    {
        public ListTileVisual(double paddingX, double paddingTop, double titleLineHeight, double titleGap, double bodyLineHeight)
        {
            Stripe = new Rectangle
            {
                Width = 4,
                RadiusX = 2,
                RadiusY = 2,
            };

            Title = new TextBlock
            {
                Foreground = SampleTheme.AccentBrush,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                LineHeight = titleLineHeight,
                FontWeight = FontWeights.SemiBold,
                CharacterSpacing = 40,
                TextWrapping = TextWrapping.NoWrap,
            };

            Body = new TextBlock
            {
                Foreground = SampleTheme.InkBrush,
                FontSize = 15,
                FontFamily = new FontFamily("Helvetica Neue"),
                LineHeight = bodyLineHeight,
                TextWrapping = TextWrapping.WrapWholeWords,
            };

            Layer = new Canvas();
            Canvas.SetLeft(Stripe, 0);
            Canvas.SetTop(Stripe, 0);
            Canvas.SetLeft(Title, paddingX);
            Canvas.SetTop(Title, paddingTop);
            Canvas.SetLeft(Body, paddingX);
            Canvas.SetTop(Body, paddingTop + titleLineHeight + titleGap);
            Layer.Children.Add(Stripe);
            Layer.Children.Add(Title);
            Layer.Children.Add(Body);

            Root = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Child = Layer,
            };
        }

        public Border Root { get; }

        public Canvas Layer { get; }

        public Rectangle Stripe { get; }

        public TextBlock Title { get; }

        public TextBlock Body { get; }
    }
}
