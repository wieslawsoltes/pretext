using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pretext.LayoutFramework;

namespace Pretext.Uno.Controls;

public sealed class PretextVirtualizedWrapPanel : UserControl
{
    private readonly Canvas _canvas = new();
    private readonly List<WrapTileVisual> _visualPool = [];
    private readonly UiRenderScheduler _renderScheduler;
    private StretchScrollHost? _scrollHost;
    private WrapTileSource? _source;
    private WrapLayoutState? _layoutState;
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

    public Brush TileTextBrush { get; set; } = new SolidColorBrush(Colors.Black);

    public string TileFontFamily { get; set; } = "Helvetica Neue";

    public double TileFontSize { get; set; } = 15;

    public double TileLineHeight { get; set; } = 20;

    public double SurfacePadding { get; set; } = 18;

    public double TileGap { get; set; } = 10;

    public double TilePadding { get; set; } = 14;

    public double TileBorderThickness { get; set; } = 1;

    public double TileCornerRadius { get; set; } = 18;

    public double ViewportOverscan { get; set; } = 240;

    public void AttachScrollHost(StretchScrollHost scrollHost)
    {
        if (ReferenceEquals(_scrollHost, scrollHost))
        {
            return;
        }

        if (_scrollHost is not null)
        {
            _scrollHost.ScrollViewer.ViewChanged -= OnScrollViewChanged;
        }

        _scrollHost = scrollHost;
        _scrollHost.ScrollViewer.ViewChanged += OnScrollViewChanged;
        _renderScheduler.Schedule();
    }

    public void SetSource(WrapTileSource source)
    {
        _source = source;
        _layoutState = null;
        _lastAvailableWidth = -1;
        _renderScheduler.Schedule();
    }

    public void Refresh()
    {
        _layoutState = null;
        _lastAvailableWidth = -1;
        _renderScheduler.Schedule();
    }

    private void OnScrollViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
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
            visual.Root.BorderThickness = new Thickness(TileBorderThickness);
            visual.Root.CornerRadius = new CornerRadius(TileCornerRadius);
            visual.Root.Padding = new Thickness(TilePadding);
            visual.Body.Foreground = TileTextBrush;
            visual.Body.FontFamily = new FontFamily(TileFontFamily);
            visual.Body.FontSize = TileFontSize;
            visual.Body.LineHeight = TileLineHeight;
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
            var visual = new WrapTileVisual();
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
        public WrapTileVisual()
        {
            Body = new TextBlock
            {
                FontWeight = FontWeights.Normal,
                TextWrapping = TextWrapping.WrapWholeWords,
            };

            Root = new Border
            {
                Child = Body,
            };
        }

        public Border Root { get; }

        public TextBlock Body { get; }
    }
}
