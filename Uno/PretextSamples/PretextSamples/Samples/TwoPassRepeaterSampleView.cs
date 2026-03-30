using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pretext.LayoutFramework;
using Pretext.Uno;
using Pretext.Uno.Controls;

namespace PretextSamples.Samples;

public sealed class TwoPassRepeaterSampleView : UserControl
{
    private static readonly FeedTemplate[] Templates =
    [
        FeedTemplate.Create("OPS", "Queue pressure is layout pressure", "The repeater keeps native containers alive only for the visible range, but the layout still knows the exact height of every text tile before those containers are realized."),
        FeedTemplate.Create("MAIL", "Prepared models survive resize", "A width change invalidates only the geometric solve. The semantic pass and prepared text handles stay stable until the underlying content itself changes."),
        FeedTemplate.Create("EDITOR", "Viewport-aware exact heights", "This sample uses ItemsRepeater and a custom VirtualizingLayout. Every realized card height still comes from Pretext instead of an after-the-fact measure of the live item tree."),
        FeedTemplate.Create("SYSTEM", "Framework-native virtualization", "The base layout caches PreparedItemsModel, computes a solved extent, queries the vertical occlusion index against the realization rect, and explicitly recycles the off-screen elements."),
        FeedTemplate.Create("ROADMAP", "Shared core model", "The same fingerprint, prepared item model, solved layout, and occlusion index now serve both custom Canvas virtualization and framework-native repeater virtualization."),
    ];

    public TwoPassRepeaterSampleView()
    {
        var items = BuildItems(1600);

        var stack = SampleUi.CreatePageStack();
        stack.Children.Add(SampleUi.CreateHeader(
            "DEMO",
            "Two-pass ItemsRepeater",
            "A reusable VirtualizingLayout adapter prepares item metrics once, solves exact row geometry from width and viewport changes, and lets ItemsRepeater own realization and recycling."));

        var repeater = new ItemsRepeater
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 760,
            ItemsSource = items,
            Layout = new InsightFeedLayout(),
            ItemTemplate = new DataTemplate(() => new FeedItemView()),
        };

        stack.Children.Add(SampleUi.CreateCard(repeater, 0));
        Content = SampleUi.CreatePageRoot(stack);
    }

    private static IReadOnlyList<FeedItem> BuildItems(int count)
    {
        var items = new List<FeedItem>(count);
        for (var index = 0; index < count; index++)
        {
            var template = Templates[index % Templates.Length];
            items.Add(new FeedItem(
                index,
                $"{template.Channel} {(index + 1):D4}",
                template.Subject,
                template.Body,
                template.PreparedBody,
                template.Fingerprint));
        }

        return items;
    }

    private sealed record FeedTemplate(
        string Channel,
        string Subject,
        string Body,
        PreparedTextWithSegments PreparedBody,
        LayoutFingerprint Fingerprint)
    {
        private const string BodyFont = "15px \"Helvetica Neue\", Helvetica, Arial, sans-serif";

        public static FeedTemplate Create(string channel, string subject, string body)
        {
            var builder = LayoutFingerprintBuilder.Create();
            builder.Add(channel);
            builder.Add(subject);
            builder.Add(body);

            return new FeedTemplate(
                channel,
                subject,
                body,
                PretextLayout.PrepareWithSegments(body, BodyFont),
                builder.ToFingerprint());
        }
    }

    private sealed record FeedItem(
        int Index,
        string ChannelLabel,
        string Subject,
        string Body,
        PreparedTextWithSegments PreparedBody,
        LayoutFingerprint TemplateFingerprint);

    private sealed class FeedItemView : Border
    {
        private FeedItem? _item;
        private readonly TextBlock _channel;
        private readonly TextBlock _subject;
        private readonly PretextParagraphView _body;

        public FeedItemView()
        {
            Background = SampleTheme.PanelBrush;
            BorderBrush = SampleTheme.RuleBrush;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(18);
            Padding = new Thickness(16, 14, 16, 16);

            var stack = new StackPanel { Spacing = 10 };
            _channel = new TextBlock
            {
                Foreground = SampleTheme.AccentBrush,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                CharacterSpacing = 90,
                TextWrapping = TextWrapping.NoWrap,
            };
            _subject = new TextBlock
            {
                Foreground = SampleTheme.InkBrush,
                FontFamily = new FontFamily("Georgia"),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.NoWrap,
            };
            _body = new PretextParagraphView("Helvetica Neue", 15, 21, SampleTheme.MutedBrush)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            stack.Children.Add(_channel);
            stack.Children.Add(_subject);
            stack.Children.Add(_body);
            Child = stack;

            DataContextChanged += OnDataContextChanged;
            Loaded += (_, _) => RenderBody();
            SizeChanged += (_, _) => RenderBody();
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            _item = DataContext as FeedItem;
            if (_item is null)
            {
                return;
            }

            _channel.Text = _item.ChannelLabel;
            _subject.Text = _item.Subject;
            RenderBody();
        }

        private void RenderBody()
        {
            if (_item is null)
            {
                return;
            }

            var width = Math.Max(1, ActualWidth - Padding.Left - Padding.Right - BorderThickness.Left - BorderThickness.Right);
            _body.Render(_item.PreparedBody, width);
        }
    }

    private sealed class InsightFeedLayout : PretextVirtualizingLayout
    {
        private const double OuterPadding = 18;
        private const double ItemGap = 12;
        private const double ItemPaddingX = 16;
        private const double ItemChromeHeight = 84;
        private const double BodyLineHeight = 21;

        public InsightFeedLayout()
        {
            ViewportOverscan = 360;
        }

        protected override LayoutFingerprint GetLayoutFingerprint(VirtualizingLayoutContext context)
        {
            var builder = LayoutFingerprintBuilder.Create();
            builder.Add(context.ItemCount);
            for (var index = 0; index < context.ItemCount; index++)
            {
                var item = (FeedItem)context.GetItemAt(index);
                builder.Add(item.ChannelLabel);
                builder.Add(item.TemplateFingerprint);
            }

            return builder.ToFingerprint();
        }

        protected override PreparedItemsModel Prepare(VirtualizingLayoutContext context, LayoutFingerprint fingerprint)
        {
            var items = new PreparedItemMetrics[context.ItemCount];
            var texts = new PreparedText?[context.ItemCount];

            for (var index = 0; index < context.ItemCount; index++)
            {
                var item = (FeedItem)context.GetItemAt(index);
                items[index] = new PreparedItemMetrics(PreparedItemKind.Text, 280, 120);
                texts[index] = item.PreparedBody;
            }

            return new PreparedItemsModel(fingerprint, items, textHandles: texts);
        }

        protected override SolvedLayout Solve(VirtualizingLayoutContext context, PreparedItemsModel prepared, LayoutConstraints constraints)
        {
            var availableWidth = Math.Max(320, constraints.AvailableWidth);
            var itemWidth = Math.Max(260, availableWidth - OuterPadding * 2);
            var bodyWidth = Math.Max(120, itemWidth - ItemPaddingX * 2 - 2);

            var placements = new List<LayoutPlacement>(prepared.Count);
            var bands = new List<VerticalBand>(prepared.Count);
            var y = OuterPadding;

            for (var index = 0; index < prepared.Count; index++)
            {
                var bodyPrepared = prepared.GetTextHandleOrDefault(index) ?? throw new InvalidOperationException("Prepared body is required.");
                var bodyLayout = PretextLayout.Layout(bodyPrepared, bodyWidth, BodyLineHeight);
                var height = ItemChromeHeight + bodyLayout.Height;

                placements.Add(new LayoutPlacement(index, $"feed-{index}", new LayoutRect(OuterPadding, y, itemWidth, height)));
                bands.Add(new VerticalBand(index, index + 1, y, y + height));
                y += height + ItemGap;
            }

            var contentHeight = prepared.Count == 0 ? OuterPadding * 2 : y - ItemGap + OuterPadding;
            return new SolvedLayout(
                prepared.Fingerprint,
                constraints,
                new LayoutSize(availableWidth, contentHeight),
                placements,
                new VerticalOcclusionIndex(bands));
        }
    }
}
