using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pretext.LayoutFramework;
using Pretext.LayoutFramework.Samples;
using Pretext.Uno.Controls;

namespace PretextSamples.Samples;

public sealed class SharedItemSamplesView : UserControl
{
    public SharedItemSamplesView()
    {
        var stack = SampleUi.CreatePageStack();
        stack.Children.Add(SampleUi.CreateHeader(
            "DEMO",
            "Shared item-surface definitions",
            "These wrap and masonry surfaces are driven by framework-neutral prepared item definitions in the core Pretext project. Uno only supplies the repeater host, native controls, and retained visuals."));

        stack.Children.Add(BuildSection(
            "Shared wrap surface",
            "A row-wrapped tile field solved from one prepared item model and rendered through ItemsRepeater.",
            new WrapItemsDemo()));

        stack.Children.Add(BuildSection(
            "Shared masonry surface",
            "A shortest-column masonry layout that now uses sparse viewport selection in the shared occlusion model instead of assuming one contiguous item range.",
            new MasonryItemsDemo()));

        Content = SampleUi.CreatePageRoot(stack);
    }

    private static Border BuildSection(string title, string body, FrameworkElement content)
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = SampleTheme.InkBrush,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        stack.Children.Add(SampleUi.CreateBodyText(body));
        stack.Children.Add(content);
        return SampleUi.CreateCard(stack, 18);
    }

    private sealed class WrapItemsDemo : Border
    {
        private readonly WrapItemsSampleDefinition _definition = WrapItemsSampleDefinition.CreateDemo(itemCount: 12_000, templateCount: 120);

        public WrapItemsDemo()
        {
            BorderBrush = SampleTheme.RuleBrush;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(18);
            Height = 560;
            HorizontalAlignment = HorizontalAlignment.Stretch;

            var indices = Enumerable.Range(0, _definition.Count).ToArray();
            var repeater = new ItemsRepeater
            {
                ItemsSource = indices,
                Layout = new WrapItemsLayout(_definition),
                ItemTemplate = new DataTemplate(() => new WrapTileView(_definition)),
            };

            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = repeater,
            };
        }
    }

    private sealed class MasonryItemsDemo : Border
    {
        private readonly MasonryItemsSampleDefinition _definition = MasonryItemsSampleDefinition.CreateDemo();

        public MasonryItemsDemo()
        {
            BorderBrush = SampleTheme.RuleBrush;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(18);
            Height = 640;
            HorizontalAlignment = HorizontalAlignment.Stretch;

            var indices = Enumerable.Range(0, _definition.Count).ToArray();
            var repeater = new ItemsRepeater
            {
                ItemsSource = indices,
                Layout = new MasonryItemsLayout(_definition),
                ItemTemplate = new DataTemplate(() => new MasonryCardView(_definition)),
            };

            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = repeater,
            };
        }
    }

    private sealed class WrapItemsLayout : PretextVirtualizingLayout
    {
        private readonly WrapItemsSampleDefinition _definition;

        public WrapItemsLayout(WrapItemsSampleDefinition definition)
        {
            _definition = definition;
            ViewportOverscan = 240;
        }

        protected override LayoutFingerprint GetLayoutFingerprint(VirtualizingLayoutContext context)
        {
            return _definition.GetLayoutFingerprint();
        }

        protected override PreparedItemsModel Prepare(VirtualizingLayoutContext context, LayoutFingerprint fingerprint)
        {
            return _definition.Prepare(fingerprint);
        }

        protected override SolvedLayout Solve(VirtualizingLayoutContext context, PreparedItemsModel prepared, LayoutConstraints constraints)
        {
            return _definition.Solve(prepared, constraints);
        }
    }

    private sealed class MasonryItemsLayout : PretextVirtualizingLayout
    {
        private readonly MasonryItemsSampleDefinition _definition;

        public MasonryItemsLayout(MasonryItemsSampleDefinition definition)
        {
            _definition = definition;
            ViewportOverscan = 260;
        }

        protected override LayoutFingerprint GetLayoutFingerprint(VirtualizingLayoutContext context)
        {
            return _definition.GetLayoutFingerprint();
        }

        protected override PreparedItemsModel Prepare(VirtualizingLayoutContext context, LayoutFingerprint fingerprint)
        {
            return _definition.Prepare(fingerprint);
        }

        protected override SolvedLayout Solve(VirtualizingLayoutContext context, PreparedItemsModel prepared, LayoutConstraints constraints)
        {
            return _definition.Solve(prepared, constraints);
        }
    }

    private sealed class WrapTileView : Border
    {
        private static readonly Brush[] Backgrounds =
        [
            SampleTheme.PanelBrush,
            SampleTheme.AccentSoftBrush,
            SampleTheme.Brush(0xF0, 0xEE, 0xE8),
            SampleTheme.Brush(0xEE, 0xF1, 0xEA),
            SampleTheme.Brush(0xEA, 0xEC, 0xF4),
        ];

        private static readonly Brush[] Borders =
        [
            SampleTheme.RuleBrush,
            SampleTheme.Brush(0xCF, 0xBF, 0xB0),
            SampleTheme.Brush(0xC8, 0xD4, 0xC2),
            SampleTheme.Brush(0xC8, 0xC9, 0xDA),
            SampleTheme.Brush(0xD9, 0xC8, 0xBE),
        ];

        private readonly WrapItemsSampleDefinition _definition;
        private readonly PretextParagraphView _body;
        private int _index = -1;
        private double _lastWidth = -1;

        public WrapTileView(WrapItemsSampleDefinition definition)
        {
            _definition = definition;
            Padding = new Thickness(14);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(18);
            _body = new PretextParagraphView("Helvetica Neue", 15, 20, SampleTheme.InkBrush);
            Child = _body;

            DataContextChanged += OnDataContextChanged;
            Loaded += (_, _) => Render();
            SizeChanged += (_, _) => Render();
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (DataContext is not int index)
            {
                return;
            }

            _index = index;
            var template = _definition.GetTemplateForItem(index);
            Background = Backgrounds[template.PaletteIndex % Backgrounds.Length];
            BorderBrush = Borders[template.PaletteIndex % Borders.Length];
            _lastWidth = -1;
            Render();
        }

        private void Render()
        {
            if (_index < 0 || ActualWidth <= 0)
            {
                return;
            }

            var width = Math.Max(1, ActualWidth - Padding.Left - Padding.Right - BorderThickness.Left - BorderThickness.Right);
            if (Math.Abs(width - _lastWidth) <= 0.5)
            {
                return;
            }

            _lastWidth = width;
            _body.Render(_definition.GetTemplateForItem(_index).Prepared, width);
        }
    }

    private sealed class MasonryCardView : Border
    {
        private static readonly Brush[] Borders =
        [
            SampleTheme.RuleBrush,
            SampleTheme.Brush(0xCF, 0xBF, 0xB0),
            SampleTheme.Brush(0xC8, 0xD4, 0xC2),
            SampleTheme.Brush(0xC8, 0xC9, 0xDA),
            SampleTheme.Brush(0xD9, 0xC8, 0xBE),
        ];

        private readonly MasonryItemsSampleDefinition _definition;
        private readonly PretextParagraphView _body;
        private int _index = -1;
        private double _lastWidth = -1;

        public MasonryCardView(MasonryItemsSampleDefinition definition)
        {
            _definition = definition;
            Background = SampleTheme.PanelBrush;
            Padding = new Thickness(16);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(18);
            _body = new PretextParagraphView("Helvetica Neue", 15, 22, SampleTheme.InkBrush);
            Child = _body;

            DataContextChanged += OnDataContextChanged;
            Loaded += (_, _) => Render();
            SizeChanged += (_, _) => Render();
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (DataContext is not int index)
            {
                return;
            }

            _index = index;
            BorderBrush = Borders[_definition.GetItem(index).PaletteIndex % Borders.Length];
            _lastWidth = -1;
            Render();
        }

        private void Render()
        {
            if (_index < 0 || ActualWidth <= 0)
            {
                return;
            }

            var width = Math.Max(1, ActualWidth - Padding.Left - Padding.Right - BorderThickness.Left - BorderThickness.Right);
            if (Math.Abs(width - _lastWidth) <= 0.5)
            {
                return;
            }

            _lastWidth = width;
            _body.Render(_definition.GetItem(_index).Prepared, width);
        }
    }
}
