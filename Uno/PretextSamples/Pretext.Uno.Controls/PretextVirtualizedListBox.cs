using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Pretext.Uno;
using Pretext.LayoutFramework;

namespace Pretext.Uno.Controls;

public sealed class PretextVirtualizedListBox : UserControl
{
    private readonly Canvas _canvas = new();
    private readonly List<ListTileVisual> _visualPool = [];
    private readonly UiRenderScheduler _renderScheduler;
    private StretchScrollHost? _scrollHost;
    private ListTileSource? _source;
    private ListLayoutState? _layoutState;
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

    public Brush ItemBackgroundBrush { get; set; } = new SolidColorBrush(Colors.White);

    public Brush ItemBorderBrush { get; set; } = new SolidColorBrush(Colors.LightGray);

    public Brush SelectedItemBackgroundBrush { get; set; } = new SolidColorBrush(Colors.WhiteSmoke);

    public Brush SelectedItemBorderBrush { get; set; } = new SolidColorBrush(Colors.DodgerBlue);

    public Brush StripeBrush { get; set; } = new SolidColorBrush(Colors.LightGray);

    public Brush SelectedStripeBrush { get; set; } = new SolidColorBrush(Colors.DodgerBlue);

    public Brush TitleBrush { get; set; } = new SolidColorBrush(Colors.DodgerBlue);

    public Brush BodyBrush { get; set; } = new SolidColorBrush(Colors.Black);

    public string TitleFontFamily { get; set; } = "Consolas";

    public string BodyFontFamily { get; set; } = "Helvetica Neue";

    public double SurfacePadding { get; set; } = 16;

    public double ItemGap { get; set; } = 8;

    public double ItemPaddingX { get; set; } = 16;

    public double ItemPaddingTop { get; set; } = 14;

    public double ItemPaddingBottom { get; set; } = 16;

    public double ItemBorderThickness { get; set; } = 1;

    public double ItemCornerRadius { get; set; } = 20;

    public double TitleFontSize { get; set; } = 12;

    public double TitleLineHeight { get; set; } = 18;

    public double TitleGap { get; set; } = 10;

    public double BodyFontSize { get; set; } = 15;

    public double BodyLineHeight { get; set; } = 21;

    public double ViewportOverscan { get; set; } = 320;

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

    public void SetSource(ListTileSource source)
    {
        _source = source;
        _layoutState = null;
        _lastAvailableWidth = -1;
        _selectedIndex = 0;
        SelectionChanged?.Invoke(_selectedIndex);
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
            visual.Root.Background = isSelected ? SelectedItemBackgroundBrush : ItemBackgroundBrush;
            visual.Root.BorderBrush = isSelected ? SelectedItemBorderBrush : ItemBorderBrush;
            visual.Root.BorderThickness = new Thickness(ItemBorderThickness);
            visual.Root.CornerRadius = new CornerRadius(ItemCornerRadius);
            visual.Stripe.Fill = isSelected ? SelectedStripeBrush : StripeBrush;
            visual.Stripe.Height = placement.Height;
            visual.Layer.Width = placement.Width;
            visual.Layer.Height = placement.Height;
            visual.Title.Foreground = TitleBrush;
            visual.Title.FontFamily = new FontFamily(TitleFontFamily);
            visual.Title.FontSize = TitleFontSize;
            visual.Title.LineHeight = TitleLineHeight;
            visual.Title.Text = _source.GetTitle(itemIndex);
            visual.Body.Foreground = BodyBrush;
            visual.Body.FontFamily = new FontFamily(BodyFontFamily);
            visual.Body.FontSize = BodyFontSize;
            visual.Body.LineHeight = BodyLineHeight;
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
            var visual = new ListTileVisual(ItemPaddingX, ItemPaddingTop, TitleLineHeight, TitleGap);
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
        if (sender is not Border { Tag: int itemIndex } || itemIndex == _selectedIndex)
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
        public ListTileVisual(double paddingX, double paddingTop, double titleLineHeight, double titleGap)
        {
            Stripe = new Rectangle
            {
                Width = 4,
                RadiusX = 2,
                RadiusY = 2,
            };

            Title = new TextBlock
            {
                FontWeight = FontWeights.SemiBold,
                CharacterSpacing = 40,
                TextWrapping = TextWrapping.NoWrap,
            };

            Body = new TextBlock
            {
                FontWeight = FontWeights.Normal,
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
