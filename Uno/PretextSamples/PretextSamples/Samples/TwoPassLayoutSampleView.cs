using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pretext.LayoutFramework;
using Pretext.Uno;
using Pretext.Uno.Controls;
using Windows.Foundation;

namespace PretextSamples.Samples;

public sealed class TwoPassLayoutSampleView : UserControl
{
    private static readonly InsightCardModel[] Cards =
    [
        InsightCardModel.Create("ROADMAP", "Prepare once", "Prepared models keep copy, metrics, and role metadata stable while the viewport width changes dozens of times during an active resize."),
        InsightCardModel.Create("ENGINE", "Solve often", "The panel only recomputes rectangles from cached prepared handles. It does not rediscover text height by probing the live visual tree on every pass."),
        InsightCardModel.Create("RESPONSIVE", "Column shifts", "At narrow widths the cards stack. At medium widths they flow into two columns. At wide widths the same prepared content redistributes into three lanes."),
        InsightCardModel.Create("UI", "Native controls", "Each card is still a retained Uno control with real focus, accessibility, and input behavior. Pretext only owns the geometry."),
        InsightCardModel.Create("VIRTUALIZATION", "Same architecture", "The same fingerprint, prepare, and solve structure scales into wrap panels, masonry feeds, and repeater layouts with viewport culling."),
        InsightCardModel.Create("NEXT", "Framework path", "This sample uses LayoutPanel today. The same core model can now back future Uno VirtualizingLayout and Avalonia panel adapters."),
    ];

    public TwoPassLayoutSampleView()
    {
        var stack = SampleUi.CreatePageStack();
        stack.Children.Add(SampleUi.CreateHeader(
            "DEMO",
            "Two-pass LayoutPanel",
            "A reusable Uno LayoutPanel adapter caches a prepared panel model, solves card geometry from width changes, and arranges retained controls from solved placements."));

        var panel = new LayoutPanel
        {
            Layout = new ResponsiveInsightsLayout(Cards),
            MinHeight = 520,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        foreach (var card in Cards)
        {
            panel.Children.Add(new InsightCardView(card));
        }

        stack.Children.Add(SampleUi.CreateCard(panel, 0));
        Content = SampleUi.CreatePageRoot(stack);
    }

    private sealed record InsightCardModel(
        string Eyebrow,
        string Title,
        string Body,
        PreparedTextWithSegments PreparedBody)
    {
        private const string BodyFont = "16px \"Helvetica Neue\", Helvetica, Arial, sans-serif";

        public static InsightCardModel Create(string eyebrow, string title, string body)
        {
            return new InsightCardModel(
                eyebrow,
                title,
                body,
                PretextLayout.PrepareWithSegments(body, BodyFont));
        }
    }

    private sealed class InsightCardView : Border
    {
        private readonly InsightCardModel _model;
        private readonly PretextParagraphView _paragraphView;

        public InsightCardView(InsightCardModel model)
        {
            _model = model;
            Background = SampleTheme.PanelBrush;
            BorderBrush = SampleTheme.RuleBrush;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(20);
            Padding = new Thickness(18);

            var stack = new StackPanel { Spacing = 10 };
            stack.Children.Add(new TextBlock
            {
                Text = model.Eyebrow,
                Foreground = SampleTheme.AccentBrush,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                CharacterSpacing = 100,
                TextWrapping = TextWrapping.NoWrap,
            });
            stack.Children.Add(new TextBlock
            {
                Text = model.Title,
                Foreground = SampleTheme.InkBrush,
                FontFamily = new FontFamily("Georgia"),
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.NoWrap,
            });

            _paragraphView = new PretextParagraphView("Helvetica Neue", 16, 22, SampleTheme.MutedBrush)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            stack.Children.Add(_paragraphView);

            stack.Children.Add(new TextBlock
            {
                Text = "Prepared fingerprint + solved geometry",
                Foreground = SampleTheme.MutedBrush,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                TextWrapping = TextWrapping.NoWrap,
            });

            Child = stack;
            Loaded += (_, _) => RenderBody();
            SizeChanged += (_, _) => RenderBody();
        }

        private void RenderBody()
        {
            var width = Math.Max(1, ActualWidth - Padding.Left - Padding.Right - BorderThickness.Left - BorderThickness.Right);
            _paragraphView.Render(_model.PreparedBody, width);
        }
    }

    private sealed class ResponsiveInsightsLayout : PretextPanelLayout
    {
        private readonly InsightCardModel[] _cards;

        public ResponsiveInsightsLayout(InsightCardModel[] cards)
        {
            _cards = cards;
        }

        protected override LayoutFingerprint GetLayoutFingerprint(NonVirtualizingLayoutContext context)
        {
            var builder = LayoutFingerprintBuilder.Create();
            builder.Add(_cards.Length);
            foreach (var card in _cards)
            {
                builder.Add(card.Eyebrow);
                builder.Add(card.Title);
                builder.Add(card.Body);
            }

            return builder.ToFingerprint();
        }

        protected override PreparedPanelModel Prepare(NonVirtualizingLayoutContext context, LayoutFingerprint fingerprint)
        {
            var nodes = _cards.Select((card, index) =>
                new PreparedNode(
                    Key: $"card-{index}",
                    Kind: PreparedNodeKind.Text,
                    Metrics: PreparedNodeMetrics.Flexible(new LayoutSize(260, 180), new LayoutSize(220, 160)),
                    Text: card.PreparedBody,
                    RichText: card.PreparedBody,
                    DerivedState: card));

            return new PreparedPanelModel(
                fingerprint,
                nodes,
                chromeSize: LayoutSize.Empty,
                derivedState: _cards);
        }

        protected override SolvedLayout Solve(NonVirtualizingLayoutContext context, PreparedPanelModel prepared, LayoutConstraints constraints)
        {
            const double outerPadding = 20;
            const double columnGap = 16;
            const double cardPadding = 18;
            const double cardChromeHeight = 110;
            const double bodyLineHeight = 22;

            var availableWidth = Math.Max(320, constraints.AvailableWidth);
            var innerWidth = Math.Max(220, availableWidth - outerPadding * 2);
            var columns = innerWidth >= 1080 ? 3 : innerWidth >= 720 ? 2 : 1;
            var columnWidth = Math.Max(220, (innerWidth - columnGap * (columns - 1)) / columns);
            var bodyWidth = Math.Max(120, columnWidth - cardPadding * 2 - 2);

            var columnHeights = new double[columns];
            for (var index = 0; index < columns; index++)
            {
                columnHeights[index] = outerPadding;
            }

            var placements = new List<LayoutPlacement>(prepared.Nodes.Count);
            for (var index = 0; index < prepared.Nodes.Count; index++)
            {
                var node = prepared.Nodes[index];
                var preparedText = node.Text ?? throw new InvalidOperationException("Prepared text handle is required.");
                var bodyLayout = PretextLayout.Layout(preparedText, bodyWidth, bodyLineHeight);
                var cardHeight = cardChromeHeight + bodyLayout.Height;

                var column = 0;
                for (var candidate = 1; candidate < columnHeights.Length; candidate++)
                {
                    if (columnHeights[candidate] < columnHeights[column])
                    {
                        column = candidate;
                    }
                }

                var x = outerPadding + column * (columnWidth + columnGap);
                var y = columnHeights[column];
                placements.Add(new LayoutPlacement(index, node.Key, new LayoutRect(x, y, columnWidth, cardHeight)));
                columnHeights[column] += cardHeight + columnGap;
            }

            var contentHeight = columnHeights.Length == 0 ? outerPadding * 2 : columnHeights.Max() - columnGap + outerPadding;

            return new SolvedLayout(
                prepared.Fingerprint,
                constraints,
                new LayoutSize(availableWidth, contentHeight),
                placements);
        }
    }
}
