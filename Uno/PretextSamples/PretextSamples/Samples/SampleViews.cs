using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Pretext.Uno;
using Pretext.Uno.Controls;
using SkiaSharp;
using Windows.Foundation;
using Windows.Storage;

namespace PretextSamples.Samples;

public sealed class OverviewSampleView : UserControl
{
    public OverviewSampleView()
    {
        var stack = SampleUi.CreatePageStack();
        stack.Children.Add(SampleUi.CreateHeader(
            "PRETEXT",
            "Uno samples for manual text layout",
            "This port keeps the library-style API shape from the original project and recreates the demo surface in native Uno views. The pages below focus on predicted line counts, shrinkwrap widths, manual line routing, and custom editorial geometry."));

        var cards = new StackPanel { Spacing = 16 };
        cards.Children.Add(BuildFeatureCard("Accordion", "Predicted text heights drive section metadata without measuring the visible layout tree."));
        cards.Children.Add(BuildFeatureCard("Bubbles", "Binary-search shrinkwrap produces tighter multiline chat bubbles than width-to-widest-line sizing."));
        cards.Children.Add(BuildFeatureCard("Masonry", "Card heights come from the layout engine, so the grid can place content before the UI tree measures it."));
        cards.Children.Add(BuildFeatureCard("Virtual Wrap", "A custom 100k-item wrap surface computes every tile box up front, then culls whole rows against the viewport."));
        cards.Children.Add(BuildFeatureCard("Virtual List", "A ListBox-like 100k feed keeps exact non-uniform row heights without ItemsRepeater, ListView, or measured placeholders."));
        cards.Children.Add(BuildFeatureCard("Todo App", "Built-in Uno controls are absolutely positioned into wide, medium, and narrow task-planning shells, while Pretext drives every variable text region."));
        cards.Children.Add(BuildFeatureCard("Mail App", "A responsive inbox surface reflows folders, message rows, and the reader pane with explicit panel rects instead of adaptive Grid heuristics."));
        cards.Children.Add(BuildFeatureCard("Notes App", "A note workspace swaps between stacked, split, and three-pane arrangements while Pretext keeps cards and editor previews geometrically exact."));
        cards.Children.Add(BuildFeatureCard("Rich Text", "Inline text, code spans, and atomic chips share one flow while only the text fragments split across lines."));
        cards.Children.Add(BuildFeatureCard("Dynamic Layout", "Two-column text routes around badges and a headline box by asking the engine for one line at a time."));
        cards.Children.Add(BuildFeatureCard("Editorial Engine", "Animated circular obstacles and a pull quote force live reflow in a magazine-style stage."));
        cards.Children.Add(BuildFeatureCard("Variable ASCII", "A particle field is rendered twice: once with a monospace ramp and once with proportional glyph choices."));

        stack.Children.Add(SampleUi.CreateCard(cards));
        Content = SampleUi.CreatePageRoot(stack);
    }

    private static Border BuildFeatureCard(string title, string body)
    {
        var cardStack = new StackPanel { Spacing = 8 };
        cardStack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = SampleTheme.InkBrush,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
        });
        cardStack.Children.Add(SampleUi.CreateBodyText(body));
        return SampleUi.CreateCard(cardStack, 16);
    }
}

public sealed class AccordionSampleView : UserControl
{
    private const string BodyFont = "16px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const double LineHeight = 26;
    private const double StackBodyPaddingX = 20;
    private const double StackBodyPaddingBottom = 18;

    private readonly (string Title, string Text)[] _items =
    {
        ("Section 1", "Mina cut the release note to three crisp lines, then realized the support caveat still needed one more sentence before it could ship without surprises."),
        ("Section 2", "The handoff doc now reads like a proper morning checklist instead of a diary entry. Restart the worker, verify the queue drains, and only then mark the incident quiet. If the backlog grows again, page the same owner instead of opening a new thread."),
        ("Section 3", "We learned the hard way that a giant native scroll range can dominate everything else. The bug looked like DOM churn, then like pooling, then like rendering pressure, until the repros were stripped down enough to show the real limit. That changed the fix completely: simplify the DOM, keep virtualization honest, and stop hiding the worst-case path behind caches that only make the common frame look cheaper."),
        ("Section 4", "AGI 春天到了. بدأت الرحلة 🚀 and the long URL is https://example.com/reports/q3?lang=ar&mode=full. Nora wrote “please keep 10 000 rows visible,” Mina replied “trans­atlantic labels are still weird.”"),
    };

    private readonly PreparedText[] _prepared;
    private readonly List<TextBlock> _metaBlocks = [];
    private readonly List<Border> _bodyHosts = [];
    private readonly List<RotateTransform> _glyphTransforms = [];
    private readonly List<double> _bodyHeights = [];
    private readonly Border _stackHost;
    private readonly FrameworkElement _pageRoot;
    private readonly UiRenderScheduler _renderScheduler;
    private int _openItemIndex = 0;
    private double _lastMetricWidth = -1;

    public AccordionSampleView()
    {
        _prepared = _items.Select(item => PretextLayout.Prepare(item.Text, BodyFont)).ToArray();
        _renderScheduler = new UiRenderScheduler(DispatcherQueue, UpdateMetrics);

        var stack = SampleUi.CreatePageStack();
        stack.Children.Add(SampleUi.CreateHeader(
            "DEMO",
            "Finally sane accordion",
            "The section heights are predicted by Pretext first, then the accordion opens to those measurements without reading the visible text tree."));

        var accordion = new StackPanel { Spacing = 0 };
        for (var i = 0; i < _items.Length; i++)
        {
            var titleBlock = new TextBlock
            {
                Text = _items[i].Title,
                Foreground = SampleTheme.InkBrush,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.NoWrap,
            };

            var metaBlock = new TextBlock
            {
                Foreground = SampleTheme.MutedBrush,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
            };
            _metaBlocks.Add(metaBlock);

            var glyphTransform = new RotateTransform { Angle = i == _openItemIndex ? 90 : 0, CenterX = 9, CenterY = 9 };
            _glyphTransforms.Add(glyphTransform);

            var glyph = new Grid
            {
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = glyphTransform,
            };
            glyph.Children.Add(new TextBlock
            {
                Text = "▶",
                Foreground = SampleTheme.AccentBrush,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.Children.Add(titleBlock);
            Grid.SetColumn(metaBlock, 1);
            headerGrid.Children.Add(metaBlock);
            Grid.SetColumn(glyph, 2);
            headerGrid.Children.Add(glyph);

            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(20, 18, 20, 18),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Content = headerGrid,
            };
            var itemIndex = i;
            button.Click += (_, _) =>
            {
                _openItemIndex = _openItemIndex == itemIndex ? -1 : itemIndex;
                ApplyAccordionState();
            };

            var copyBlock = new TextBlock
            {
                Text = _items[i].Text,
                Foreground = SampleTheme.InkBrush,
                FontSize = 16,
                LineHeight = 26,
                TextWrapping = TextWrapping.WrapWholeWords,
            };

            var inner = new Border
            {
                Padding = new Thickness(StackBodyPaddingX, 0, StackBodyPaddingX, StackBodyPaddingBottom),
                Child = copyBlock,
            };

            var bodyHost = new Border
            {
                Height = i == _openItemIndex ? double.NaN : 0,
                VerticalAlignment = VerticalAlignment.Top,
                Child = inner,
            };
            _bodyHosts.Add(bodyHost);

            var itemStack = new StackPanel { Spacing = 0 };
            itemStack.Children.Add(button);
            itemStack.Children.Add(bodyHost);

            accordion.Children.Add(new Border
            {
                BorderBrush = SampleTheme.RuleBrush,
                BorderThickness = new Thickness(0, i == 0 ? 0 : 1, 0, 0),
                Child = itemStack,
            });
        }

        _stackHost = new Border
        {
            Background = SampleTheme.PanelBrush,
            BorderBrush = SampleTheme.RuleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = accordion,
        };

        stack.Children.Add(_stackHost);
        _pageRoot = SampleUi.CreatePageRoot(stack);
        Content = _pageRoot;
        Loaded += (_, _) => _renderScheduler.Schedule();
        SizeChanged += (_, _) => _renderScheduler.Schedule();
    }

    private void UpdateMetrics()
    {
        var width = Math.Max(220, _stackHost.ActualWidth - StackBodyPaddingX * 2);
        if (Math.Abs(width - _lastMetricWidth) < 0.5)
        {
            return;
        }

        _lastMetricWidth = width;
        _bodyHeights.Clear();
        for (var i = 0; i < _prepared.Length; i++)
        {
            var metrics = PretextLayout.Layout(_prepared[i], width, LineHeight);
            _metaBlocks[i].Text = $"Measurement: {metrics.LineCount} lines · {Math.Round(metrics.Height)}px";
            _bodyHeights.Add(Math.Ceiling(metrics.Height + StackBodyPaddingBottom));
        }

        ApplyAccordionState();
    }

    private void ApplyAccordionState()
    {
        for (var i = 0; i < _bodyHosts.Count; i++)
        {
            _glyphTransforms[i].Angle = i == _openItemIndex ? 90 : 0;
            _bodyHosts[i].Height = i == _openItemIndex && i < _bodyHeights.Count ? _bodyHeights[i] : 0;
        }
    }
}

public sealed class BubblesSampleView : UserControl
{
    private const string Font = "15px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const double LineHeight = 20;
    private const double PaddingX = 12;
    private const double PaddingY = 8;
    private const double BubbleMaxRatio = 0.8;

    private readonly (bool Sent, string Text, PreparedTextWithSegments Prepared)[] _messages =
    {
        (false, "Yo did you see the new Pretext library?", PretextLayout.PrepareWithSegments("Yo did you see the new Pretext library?", Font)),
        (true, "yeah! It measures text without the DOM. Pure JavaScript arithmetic", PretextLayout.PrepareWithSegments("yeah! It measures text without the DOM. Pure JavaScript arithmetic", Font)),
        (false, "That shrinkwrap demo is wild it finds the exact minimum width for multiline text. CSS can't do that.", PretextLayout.PrepareWithSegments("That shrinkwrap demo is wild it finds the exact minimum width for multiline text. CSS can't do that.", Font)),
        (true, "성능 최적화가 정말 많이 되었더라고요 🎉", PretextLayout.PrepareWithSegments("성능 최적화가 정말 많이 되었더라고요 🎉", Font)),
        (false, "Oh wow it handles CJK and emoji too??", PretextLayout.PrepareWithSegments("Oh wow it handles CJK and emoji too??", Font)),
        (true, "كل شيء! Mixed bidi, grapheme clusters, whatever you want. Try resizing", PretextLayout.PrepareWithSegments("كل شيء! Mixed bidi, grapheme clusters, whatever you want. Try resizing", Font)),
        (true, "the best part: zero layout reflow. You could shrinkwrap 10,000 bubbles and the browser wouldn't even blink", PretextLayout.PrepareWithSegments("the best part: zero layout reflow. You could shrinkwrap 10,000 bubbles and the browser wouldn't even blink", Font)),
    };

    private readonly Slider _slider = new() { Minimum = 220, Maximum = 760, Value = 340 };
    private readonly TextBlock _sliderValue = new() { FontFamily = new FontFamily("Consolas"), Foreground = SampleTheme.MutedBrush };
    private readonly TextBlock _cssWaste = new() { FontFamily = new FontFamily("Consolas"), Foreground = SampleTheme.InkBrush };
    private readonly TextBlock _tightWaste = new() { FontFamily = new FontFamily("Consolas"), Foreground = SampleTheme.InkBrush, Text = "0" };
    private readonly StackPanel _cssChat = new() { Spacing = 8 };
    private readonly StackPanel _tightChat = new() { Spacing = 8 };
    private readonly FrameworkElement _pageRoot;
    private readonly UiRenderScheduler _renderScheduler;
    private double _lastChatWidth = -1;

    public BubblesSampleView()
    {
        _renderScheduler = new UiRenderScheduler(DispatcherQueue, Render);
        var stack = SampleUi.CreatePageStack();
        stack.Children.Add(SampleUi.CreateHeader(
            "DEMO",
            "Shrinkwrap showdown",
            "The left column sizes bubbles like fit-content. The right column binary-searches the tightest width that preserves the same line count."));

        _slider.ValueChanged += (_, _) => _renderScheduler.Schedule();

        var controls = new Grid { ColumnSpacing = 12 };
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.Children.Add(new TextBlock
        {
            Text = "Container width",
            FontFamily = new FontFamily("Consolas"),
            Foreground = SampleTheme.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(_slider, 1);
        controls.Children.Add(_slider);
        Grid.SetColumn(_sliderValue, 2);
        controls.Children.Add(_sliderValue);
        stack.Children.Add(SampleUi.CreateCard(controls, 16));

        var comparisonGrid = new Grid { ColumnSpacing = 16 };
        comparisonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        comparisonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        comparisonGrid.Children.Add(BuildBubblePanel("CSS fit-content", "Sizes to the widest wrapped line, which leaves dead space behind shorter lines.", _cssWaste, _cssChat));
        var tightPanel = BuildBubblePanel("Pretext shrinkwrap", "Walks line ranges and reuses the line count to find the smallest width with the same wraps.", _tightWaste, _tightChat);
        Grid.SetColumn(tightPanel, 1);
        comparisonGrid.Children.Add(tightPanel);
        stack.Children.Add(comparisonGrid);
        stack.Children.Add(BuildWhyCard());

        _pageRoot = SampleUi.CreatePageRoot(stack);
        Content = _pageRoot;
        Loaded += (_, _) => _renderScheduler.Schedule();
        SizeChanged += (_, _) => _renderScheduler.Schedule();
    }

    private static Border BuildBubblePanel(string title, string description, TextBlock metricValue, StackPanel chatPanel)
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = SampleTheme.InkBrush,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
        });
        stack.Children.Add(SampleUi.CreateBodyText(description));

        var metric = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Padding = new Thickness(10, 7, 10, 7),
            Background = SampleTheme.AccentSoftBrush,
        };
        metric.Children.Add(new TextBlock
        {
            Text = "Wasted pixels:",
            FontFamily = new FontFamily("Consolas"),
            Foreground = SampleTheme.InkBrush,
        });
        metric.Children.Add(metricValue);
        stack.Children.Add(metric);

        var shell = new Border
        {
            Background = SampleTheme.ChatBackgroundBrush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16),
            Child = chatPanel,
        };
        stack.Children.Add(shell);

        return SampleUi.CreateCard(stack);
    }

    private static Border BuildWhyCard()
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = "Why can't CSS do this?",
            Foreground = SampleTheme.InkBrush,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Georgia"),
        });
        stack.Children.Add(SampleUi.CreateBodyText(
            "CSS can give you fit-content, which is the width of the widest wrapped line after layout. It cannot search for the narrowest width that preserves the same line count. Pretext can, because it measures the prepared text at multiple candidate widths and compares the resulting wraps without reading the visible tree.",
            15));
        return SampleUi.CreateCard(stack);
    }

    private void Render()
    {
        if (_pageRoot.ActualWidth <= 0)
        {
            return;
        }

        var maxChatWidth = Math.Max(220, Math.Min(760, _pageRoot.ActualWidth - 160));
        var chatWidth = Math.Min(_slider.Value, maxChatWidth);
        if (Math.Abs(chatWidth - _lastChatWidth) < 0.5 && _cssChat.Children.Count == _messages.Length)
        {
            _slider.Maximum = maxChatWidth;
            _sliderValue.Text = $"{Math.Round(chatWidth)}px";
            return;
        }

        _lastChatWidth = chatWidth;
        _slider.Maximum = maxChatWidth;
        _sliderValue.Text = $"{Math.Round(chatWidth)}px";

        var bubbleMaxWidth = Math.Floor(chatWidth * BubbleMaxRatio);
        var contentMaxWidth = Math.Max(1, bubbleMaxWidth - PaddingX * 2);
        var totalWaste = 0d;

        _cssChat.Width = chatWidth;
        _tightChat.Width = chatWidth;
        _cssChat.Children.Clear();
        _tightChat.Children.Clear();

        foreach (var message in _messages)
        {
            var cssMetrics = SampleTextMetrics.CollectWrapMetrics(message.Prepared, contentMaxWidth, LineHeight);
            var tightMetrics = SampleTextMetrics.FindTightWrapMetrics(message.Prepared, contentMaxWidth, LineHeight);

            var cssWidth = Math.Ceiling(cssMetrics.MaxLineWidth) + PaddingX * 2;
            var tightWidth = Math.Ceiling(tightMetrics.MaxLineWidth) + PaddingX * 2;
            totalWaste += Math.Max(0, cssWidth - tightWidth) * (cssMetrics.Height + PaddingY * 2);

            _cssChat.Children.Add(CreateBubble(message.Sent, message.Text, cssWidth, bubbleMaxWidth, explicitWidth: false));
            _tightChat.Children.Add(CreateBubble(message.Sent, message.Text, tightWidth, bubbleMaxWidth, explicitWidth: true));
        }

        _cssWaste.Text = Math.Round(totalWaste).ToString("N0");
    }

    private static Border CreateBubble(bool sent, string text, double desiredWidth, double maxWidth, bool explicitWidth)
    {
        var textWidth = Math.Max(40, desiredWidth - PaddingX * 2);
        var border = new Border
        {
            HorizontalAlignment = sent ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Background = sent ? SampleTheme.SentBubbleBrush : SampleTheme.ReceiveBubbleBrush,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(PaddingX, PaddingY, PaddingX, PaddingY),
            MaxWidth = maxWidth,
        };

        if (explicitWidth)
        {
            border.Width = desiredWidth;
        }

        border.Child = new TextBlock
        {
            Text = text,
            Foreground = SampleTheme.WhiteBrush,
            FontSize = 15,
            Width = explicitWidth ? textWidth : double.NaN,
            MaxWidth = explicitWidth ? double.PositiveInfinity : maxWidth - PaddingX * 2,
            TextWrapping = TextWrapping.WrapWholeWords,
        };
        return border;
    }
}

public sealed class MasonrySampleView : UserControl
{
    private const string CardFont = "15px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const double LineHeight = 22;
    private const double CardBorderThickness = 1;
    private const double CardPadding = 16;
    private const double Gap = 12;
    private const double MaxColumnWidth = 400;
    private const double ViewportOverscan = 200;

    private readonly Canvas _canvas = new();
    private readonly TextBlock _status = SampleUi.CreateBodyText("Loading cards…");
    private List<(string Text, PreparedText Prepared)> _cards = [];
    private readonly List<Border> _cardPool = [];
    private readonly StretchScrollHost _pageRoot;
    private readonly UiRenderScheduler _renderScheduler;
    private double _lastAvailableWidth = -1;
    private MasonryLayoutState? _layoutState;
    private bool _scrollViewerHooked;

    public MasonrySampleView()
    {
        _renderScheduler = new UiRenderScheduler(DispatcherQueue, Render);
        var stack = SampleUi.CreatePageStack();
        stack.Children.Add(SampleUi.CreateHeader(
            "DEMO",
            "Masonry without DOM reads",
            "The grid places cards using predicted heights from the shared layout engine. There is no measurement pass over the live card tree."));
        stack.Children.Add(_status);
        stack.Children.Add(SampleUi.CreateCard(_canvas, 0));

        _pageRoot = (StretchScrollHost)SampleUi.CreatePageRoot(stack);
        Content = _pageRoot;
        Loaded += async (_, _) =>
        {
            await EnsureCardsAsync();
            HookScrollViewer();
            _renderScheduler.Schedule();
        };
        SizeChanged += (_, _) => _renderScheduler.Schedule();
    }

    private async Task EnsureCardsAsync()
    {
        if (_cards.Count > 0)
        {
            return;
        }

        var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/shower_thoughts.json"));
        var raw = await FileIO.ReadTextAsync(file);
        var texts = JsonSerializer.Deserialize<string[]>(raw) ?? [];
        _cards = texts
            .Select(text => (text, PretextLayout.Prepare(text, CardFont)))
            .ToList();
        _status.Text = $"Showing {_cards.Count} cards";
    }

    private void HookScrollViewer()
    {
        if (_scrollViewerHooked)
        {
            return;
        }

        _pageRoot.ScrollViewer.ViewChanged += (_, _) => _renderScheduler.Schedule();
        _scrollViewerHooked = true;
    }

    private void Render()
    {
        if (_cards.Count == 0 || _pageRoot.ActualWidth <= 0)
        {
            return;
        }

        var availableWidth = Math.Max(360, _pageRoot.ActualWidth - 72);
        if (_layoutState is null || Math.Abs(availableWidth - _lastAvailableWidth) >= 0.5)
        {
            _lastAvailableWidth = availableWidth;
            _layoutState = ComputeLayout(availableWidth);
            _canvas.Width = availableWidth;
            _canvas.Height = _layoutState.ContentHeight;
        }

        var scrollViewer = _pageRoot.ScrollViewer;
        var viewportHeight = scrollViewer.ActualHeight > 0 ? scrollViewer.ActualHeight : _pageRoot.ActualHeight;
        var viewportTop = viewportHeight > 0 ? Math.Max(0, scrollViewer.VerticalOffset - ViewportOverscan) : 0;
        var viewportBottom = viewportHeight > 0
            ? scrollViewer.VerticalOffset + viewportHeight + ViewportOverscan
            : double.PositiveInfinity;
        var visibleCards = new List<PositionedCard>(_layoutState.PositionedCards.Count);
        foreach (var card in _layoutState.PositionedCards)
        {
            if (card.Y > viewportBottom || card.Y + card.Height < viewportTop)
            {
                continue;
            }

            visibleCards.Add(card);
        }

        _status.Text = $"Showing {_cards.Count} cards • {visibleCards.Count} visible";
        EnsureCardPool(visibleCards.Count);

        for (var index = 0; index < visibleCards.Count; index++)
        {
            var positioned = visibleCards[index];
            var (text, _) = _cards[positioned.CardIndex];
            var card = _cardPool[index];
            card.Width = _layoutState.ColumnWidth;
            card.Height = positioned.Height;
            if (card.Child is TextBlock textBlock)
            {
                textBlock.Text = text;
                textBlock.Width = GetTextWidth(_layoutState.ColumnWidth);
            }

            Canvas.SetLeft(card, positioned.X);
            Canvas.SetTop(card, positioned.Y);
        }
    }

    private MasonryLayoutState ComputeLayout(double availableWidth)
    {
        int columnCount;
        double columnWidth;
        if (availableWidth <= 520)
        {
            columnCount = 1;
            columnWidth = Math.Min(MaxColumnWidth, availableWidth - Gap * 2);
        }
        else
        {
            var minColumnWidth = 100 + availableWidth * 0.1;
            columnCount = Math.Max(2, (int)Math.Floor((availableWidth + Gap) / (minColumnWidth + Gap)));
            columnWidth = Math.Min(MaxColumnWidth, (availableWidth - (columnCount + 1) * Gap) / columnCount);
        }

        var textWidth = GetTextWidth(columnWidth);
        var contentWidth = columnCount * columnWidth + (columnCount - 1) * Gap;
        var offsetLeft = (availableWidth - contentWidth) / 2;
        var columnHeights = Enumerable.Repeat(Gap, columnCount).ToArray();
        var positionedCards = new List<PositionedCard>(_cards.Count);

        for (var index = 0; index < _cards.Count; index++)
        {
            var (text, prepared) = _cards[index];
            var targetColumn = 0;
            for (var c = 1; c < columnCount; c++)
            {
                if (columnHeights[c] < columnHeights[targetColumn])
                {
                    targetColumn = c;
                }
            }

            var metrics = PretextLayout.Layout(prepared, textWidth, LineHeight);
            var totalHeight = metrics.Height + (CardPadding + CardBorderThickness) * 2;
            var x = offsetLeft + targetColumn * (columnWidth + Gap);
            var y = columnHeights[targetColumn];
            columnHeights[targetColumn] += totalHeight + Gap;
            positionedCards.Add(new PositionedCard(index, x, y, totalHeight));
        }

        return new MasonryLayoutState(columnWidth, columnHeights.Max() + Gap, positionedCards);
    }

    private void EnsureCardPool(int count)
    {
        while (_cardPool.Count < count)
        {
            var card = new Border
            {
                Background = SampleTheme.PanelBrush,
                BorderBrush = SampleTheme.RuleBrush,
                BorderThickness = new Thickness(CardBorderThickness),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(CardPadding),
                Child = new TextBlock
                {
                    Foreground = SampleTheme.InkBrush,
                    FontSize = 15,
                    FontFamily = new FontFamily("Helvetica Neue"),
                    LineHeight = LineHeight,
                    TextWrapping = TextWrapping.WrapWholeWords,
                },
            };
            _cardPool.Add(card);
            _canvas.Children.Add(card);
        }

        for (var index = 0; index < _cardPool.Count; index++)
        {
            _cardPool[index].Visibility = index < count ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static double GetTextWidth(double columnWidth)
    {
        return Math.Max(80, columnWidth - (CardPadding + CardBorderThickness) * 2);
    }

    private readonly record struct PositionedCard(int CardIndex, double X, double Y, double Height);

    private sealed record MasonryLayoutState(double ColumnWidth, double ContentHeight, IReadOnlyList<PositionedCard> PositionedCards);
}

public sealed class RichNoteSampleView : UserControl
{
    private const string BodyFont = "500 17px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string LinkFont = "600 17px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string CodeFont = "600 14px \"SF Mono\", ui-monospace, Menlo, Monaco, monospace";
    private const string ChipFont = "700 12px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const double ToolbarCornerRadius = 18;
    private const double NoteShellCornerRadius = 20;
    private const double LineHeight = 34;
    private const double LastLineBlockHeight = 24;
    private const double NoteChrome = 40;
    private const double BodyMinWidth = 260;
    private const double BodyMaxWidth = 760;
    private const double UnboundedWidth = 100_000;
    private static readonly LayoutCursor LineStartCursor = new(0, 0);
    private static readonly double InlineBoundaryGap = MeasureCollapsedSpaceWidth(BodyFont);

    private readonly Slider _slider = new() { Minimum = BodyMinWidth, Maximum = BodyMaxWidth, Value = 516 };
    private readonly TextBlock _sliderValue = new() { Foreground = SampleTheme.MutedBrush };
    private readonly Canvas _noteCanvas = new();
    private readonly Border _shell;
    private readonly List<InlineItem> _items;
    private readonly FrameworkElement _pageRoot;
    private readonly UiRenderScheduler _renderScheduler;
    private double _lastWidth = -1;

    public RichNoteSampleView()
    {
        _items = BuildInlineItems();
        _renderScheduler = new UiRenderScheduler(DispatcherQueue, Render);

        var stack = SampleUi.CreatePageStack();
        var header = SampleUi.CreateHeader(
            "DEMO",
            "Rich text fragments that still wrap",
            "Text, links, and code spans keep wrapping naturally while atomic chips stay whole and reserve their own inline chrome.");
        header.MaxWidth = 720;
        header.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(header);

        _slider.ValueChanged += (_, _) => _renderScheduler.Schedule();
        var controls = new Grid { ColumnSpacing = 12 };
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.Children.Add(new TextBlock
        {
            Text = "Text width",
            Foreground = SampleTheme.MutedBrush,
            FontFamily = new FontFamily("Consolas"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(_slider, 1);
        controls.Children.Add(_slider);
        Grid.SetColumn(_sliderValue, 2);
        controls.Children.Add(_sliderValue);
        var controlsCard = SampleUi.CreateCard(controls, 16);
        controlsCard.MaxWidth = 720;
        controlsCard.Width = 720;
        controlsCard.CornerRadius = new CornerRadius(ToolbarCornerRadius);
        controlsCard.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(controlsCard);

        _shell = new Border
        {
            Background = SampleTheme.PanelBrush,
            BorderBrush = SampleTheme.RuleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(NoteShellCornerRadius),
            Padding = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = _noteCanvas,
        };

        var preview = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        preview.Children.Add(_shell);
        stack.Children.Add(preview);
        _pageRoot = SampleUi.CreatePageRoot(stack);
        Content = _pageRoot;
        Loaded += (_, _) => _renderScheduler.Schedule();
        SizeChanged += (_, _) => _renderScheduler.Schedule();
    }

    private void Render()
    {
        if (_pageRoot.ActualWidth <= 0)
        {
            return;
        }

        var maxWidth = Math.Max(BodyMinWidth, Math.Min(BodyMaxWidth, _pageRoot.ActualWidth - NoteChrome - 40));
        var width = Math.Max(BodyMinWidth, Math.Min(maxWidth, _slider.Value));
        if (Math.Abs(width - _lastWidth) < 0.5 && Math.Abs(_slider.Maximum - maxWidth) < 0.5)
        {
            return;
        }

        _lastWidth = width;
        _slider.Maximum = maxWidth;
        _sliderValue.Text = $"{Math.Round(width)}px";

        var lines = LayoutInlineItems(width);
        var noteBodyHeight = lines.Count == 0
            ? LastLineBlockHeight
            : (lines.Count - 1) * LineHeight + LastLineBlockHeight;

        _noteCanvas.Width = width;
        _noteCanvas.Height = noteBodyHeight;
        _noteCanvas.Children.Clear();

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 0,
            };

            foreach (var fragment in lines[lineIndex].Fragments)
            {
                var element = BuildFragment(fragment);
                if (fragment.LeadingGap > 0)
                {
                    element.Margin = new Thickness(fragment.LeadingGap, 0, 0, 0);
                }

                row.Children.Add(element);
            }

            Canvas.SetTop(row, lineIndex * LineHeight);
            _noteCanvas.Children.Add(row);
        }

        _shell.Width = width + NoteChrome;
    }

    private static FrameworkElement BuildFragment(InlineFragment fragment)
    {
        if (fragment is ChipFragment chip)
        {
            return new Border
            {
                Background = chip.Background,
                BorderBrush = chip.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(10, 4, 10, 4),
                RenderTransform = new TranslateTransform { Y = -1 },
                Child = new TextBlock
                {
                    Text = chip.Text,
                    Foreground = chip.Foreground,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                },
            };
        }

        var text = (TextFragment)fragment;
        if (text.Kind == TextKind.Code)
        {
            return new Border
            {
                Background = SampleTheme.AccentSoftBrush,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(7, 2, 7, 3),
                Child = new TextBlock
                {
                    Text = text.Text,
                    Foreground = SampleTheme.InkBrush,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                },
            };
        }

        return new TextBlock
        {
            Text = text.Text,
            Foreground = text.Kind == TextKind.Link ? SampleTheme.AccentBrush : SampleTheme.InkBrush,
            FontFamily = new FontFamily("Helvetica Neue"),
            FontSize = 17,
            FontWeight = text.Kind == TextKind.Link ? FontWeights.SemiBold : FontWeights.Normal,
            TextDecorations = text.Kind == TextKind.Link
                ? Windows.UI.Text.TextDecorations.Underline
                : Windows.UI.Text.TextDecorations.None,
        };
    }

    private static List<InlineItem> BuildInlineItems()
    {
        var specs = new InlineSpec[]
        {
            new TextInlineSpec("Ship ", BodyFont, TextKind.Body),
            new ChipInlineSpec("@maya", SampleTheme.Brush(0x15, 0x5A, 0x88), SampleTheme.Brush(0x15, 0x5A, 0x88), SampleTheme.Brush(0xE8, 0xF1, 0xF6)),
            new TextInlineSpec("'s ", BodyFont, TextKind.Body),
            new TextInlineSpec("rich-note", CodeFont, TextKind.Code, 14),
            new TextInlineSpec(" card once ", BodyFont, TextKind.Body),
            new TextInlineSpec("pre-wrap", CodeFont, TextKind.Code, 14),
            new TextInlineSpec(" lands. Status ", BodyFont, TextKind.Body),
            new ChipInlineSpec("blocked", SampleTheme.Brush(0x91, 0x62, 0x07), SampleTheme.Brush(0xC4, 0x81, 0x14), SampleTheme.Brush(0xF8, 0xEF, 0xDE)),
            new TextInlineSpec(" by ", BodyFont, TextKind.Body),
            new TextInlineSpec("vertical text", LinkFont, TextKind.Link),
            new TextInlineSpec(" research, but 北京 copy and Arabic QA are both green ✅. Keep ", BodyFont, TextKind.Body),
            new ChipInlineSpec("جاهز", SampleTheme.Brush(0x35, 0x5F, 0x38), SampleTheme.Brush(0x46, 0x76, 0x4D), SampleTheme.Brush(0xEB, 0xF2, 0xEB)),
            new TextInlineSpec(" for ", BodyFont, TextKind.Body),
            new TextInlineSpec("Cmd+K", CodeFont, TextKind.Code, 14),
            new TextInlineSpec(" docs; the review bundle now includes 中文 labels, عربي fallback, and one more launch pass 🚀 for ", BodyFont, TextKind.Body),
            new ChipInlineSpec("Fri 2:30 PM", SampleTheme.Brush(0x48, 0x3E, 0x83), SampleTheme.Brush(0x43, 0x39, 0x7A), SampleTheme.Brush(0xEF, 0xED, 0xF8)),
            new TextInlineSpec(". Keep ", BodyFont, TextKind.Body),
            new TextInlineSpec("layoutNextLine()", CodeFont, TextKind.Code, 14),
            new TextInlineSpec(" public, tag this ", BodyFont, TextKind.Body),
            new ChipInlineSpec("P1", SampleTheme.Brush(0x8E, 0x23, 0x23), SampleTheme.Brush(0xB0, 0x2C, 0x2C), SampleTheme.Brush(0xF6, 0xE7, 0xE7)),
            new TextInlineSpec(", keep ", BodyFont, TextKind.Body),
            new ChipInlineSpec("3 reviewers", SampleTheme.Brush(0x48, 0x3E, 0x83), SampleTheme.Brush(0x43, 0x39, 0x7A), SampleTheme.Brush(0xEF, 0xED, 0xF8)),
            new TextInlineSpec(", and route feedback to ", BodyFont, TextKind.Body),
            new TextInlineSpec("design sync", LinkFont, TextKind.Link),
            new TextInlineSpec(".", BodyFont, TextKind.Body),
        };

        var items = new List<InlineItem>(specs.Length);
        var pendingGap = 0d;

        foreach (var spec in specs)
        {
            switch (spec)
            {
                case ChipInlineSpec chip:
                    items.Add(new ChipInlineItem(
                        chip.Label,
                        Math.Ceiling(MeasureSingleLineWidth(PretextLayout.PrepareWithSegments(chip.Label, ChipFont))) + 22,
                        pendingGap,
                        chip.Foreground,
                        chip.Border,
                        chip.Background));
                    pendingGap = 0;
                    break;

                case TextInlineSpec text:
                    var carryGap = pendingGap;
                    var hasLeadingWhitespace = !string.IsNullOrEmpty(text.Text) && char.IsWhiteSpace(text.Text[0]);
                    var hasTrailingWhitespace = !string.IsNullOrEmpty(text.Text) && char.IsWhiteSpace(text.Text[^1]);
                    var trimmedText = text.Text.Trim();
                    pendingGap = hasTrailingWhitespace ? InlineBoundaryGap : 0;
                    if (trimmedText.Length == 0)
                    {
                        break;
                    }

                    var prepared = PretextLayout.PrepareWithSegments(trimmedText, text.Font);
                    var wholeLine = PretextLayout.LayoutNextLine(prepared, LineStartCursor, UnboundedWidth);
                    if (wholeLine is null)
                    {
                        break;
                    }

                    items.Add(new TextInlineItem(
                        text.Kind,
                        text.ChromeWidth,
                        carryGap > 0 || hasLeadingWhitespace ? InlineBoundaryGap : 0,
                        wholeLine.End,
                        wholeLine.Text,
                        wholeLine.Width,
                        prepared));
                    break;
            }
        }

        return items;
    }

    private static double MeasureSingleLineWidth(PreparedTextWithSegments prepared)
    {
        var maxWidth = 0d;
        PretextLayout.WalkLineRanges(prepared, UnboundedWidth, line =>
        {
            if (line.Width > maxWidth)
            {
                maxWidth = line.Width;
            }
        });
        return maxWidth;
    }

    private static double MeasureCollapsedSpaceWidth(string font)
    {
        var joinedWidth = MeasureSingleLineWidth(PretextLayout.PrepareWithSegments("A A", font));
        var compactWidth = MeasureSingleLineWidth(PretextLayout.PrepareWithSegments("AA", font));
        return Math.Max(0, joinedWidth - compactWidth);
    }

    private List<RichLine> LayoutInlineItems(double maxWidth)
    {
        var lines = new List<RichLine>();
        var safeWidth = Math.Max(1, maxWidth);
        var itemIndex = 0;
        LayoutCursor? textCursor = null;

        while (itemIndex < _items.Count)
        {
            var fragments = new List<InlineFragment>();
            var lineWidth = 0d;
            var remainingWidth = safeWidth;

            while (itemIndex < _items.Count)
            {
                switch (_items[itemIndex])
                {
                    case ChipInlineItem chip:
                    {
                        var leadingGap = fragments.Count == 0 ? 0 : chip.LeadingGap;
                        if (fragments.Count > 0 && leadingGap + chip.Width > remainingWidth)
                        {
                            goto FinishLine;
                        }

                        fragments.Add(new ChipFragment(leadingGap, chip.Text, chip.Foreground, chip.Border, chip.Background));
                        lineWidth += leadingGap + chip.Width;
                        remainingWidth = Math.Max(0, safeWidth - lineWidth);
                        itemIndex++;
                        textCursor = null;
                        continue;
                    }

                    case TextInlineItem text:
                    {
                        if (textCursor is not null && CursorsMatch(textCursor.Value, text.EndCursor))
                        {
                            itemIndex++;
                            textCursor = null;
                            continue;
                        }

                        var leadingGap = fragments.Count == 0 ? 0 : text.LeadingGap;
                        var reservedWidth = leadingGap + text.ChromeWidth;
                        if (fragments.Count > 0 && reservedWidth >= remainingWidth)
                        {
                            goto FinishLine;
                        }

                        if (textCursor is null)
                        {
                            var fullWidth = leadingGap + text.FullWidth + text.ChromeWidth;
                            if (fullWidth <= remainingWidth)
                            {
                                fragments.Add(new TextFragment(text.Kind, leadingGap, text.FullText));
                                lineWidth += fullWidth;
                                remainingWidth = Math.Max(0, safeWidth - lineWidth);
                                itemIndex++;
                                continue;
                            }
                        }

                        var startCursor = textCursor ?? LineStartCursor;
                        var line = PretextLayout.LayoutNextLine(text.Prepared, startCursor, Math.Max(1, remainingWidth - reservedWidth));
                        if (line is null || CursorsMatch(startCursor, line.End))
                        {
                            itemIndex++;
                            textCursor = null;
                            continue;
                        }

                        fragments.Add(new TextFragment(text.Kind, leadingGap, line.Text));
                        lineWidth += leadingGap + line.Width + text.ChromeWidth;
                        remainingWidth = Math.Max(0, safeWidth - lineWidth);

                        if (CursorsMatch(line.End, text.EndCursor))
                        {
                            itemIndex++;
                            textCursor = null;
                            continue;
                        }

                        textCursor = line.End;
                        goto FinishLine;
                    }
                }
            }

        FinishLine:
            if (fragments.Count == 0)
            {
                break;
            }

            lines.Add(new RichLine(fragments));
        }

        return lines;
    }

    private static bool CursorsMatch(LayoutCursor a, LayoutCursor b)
    {
        return a.SegmentIndex == b.SegmentIndex && a.GraphemeIndex == b.GraphemeIndex;
    }

    private abstract record InlineSpec;

    private sealed record TextInlineSpec(string Text, string Font, TextKind Kind, double ChromeWidth = 0) : InlineSpec;

    private sealed record ChipInlineSpec(string Label, Brush Foreground, Brush Border, Brush Background) : InlineSpec;

    private abstract record InlineItem;

    private sealed record TextInlineItem(
        TextKind Kind,
        double ChromeWidth,
        double LeadingGap,
        LayoutCursor EndCursor,
        string FullText,
        double FullWidth,
        PreparedTextWithSegments Prepared) : InlineItem;

    private sealed record ChipInlineItem(
        string Text,
        double Width,
        double LeadingGap,
        Brush Foreground,
        Brush Border,
        Brush Background) : InlineItem;

    private abstract record InlineFragment(double LeadingGap);

    private sealed record TextFragment(TextKind Kind, double LeadingGap, string Text) : InlineFragment(LeadingGap);

    private sealed record ChipFragment(double LeadingGap, string Text, Brush Foreground, Brush Border, Brush Background) : InlineFragment(LeadingGap);

    private sealed record RichLine(IReadOnlyList<InlineFragment> Fragments);

    private enum TextKind
    {
        Body,
        Link,
        Code,
    }
}

public sealed class DynamicLayoutSampleView : UserControl
{
    private const string TitleText = "SITUATIONAL AWARENESS: THE DECADE AHEAD";
    private const string TitleFontFamily = "\"Iowan Old Style\", \"Palatino Linotype\", \"Book Antiqua\", Palatino, serif";
    private const string BodyFont = "20px \"Iowan Old Style\", \"Palatino Linotype\", \"Book Antiqua\", Palatino, serif";
    private const string CreditText = "LEOPOLD ASCHENBRENNER";
    private const string CreditFont = "12px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const int CreditLineHeight = 16;
    private const int BodyLineHeight = 32;
    private const int NarrowBreakpoint = 760;
    private const int NarrowColumnMaxWidth = 430;

    private readonly Grid _root;
    private readonly Canvas _stage = new();
    private readonly Canvas _headlineLayer = new();
    private readonly Canvas _bodyLayer = new();
    private readonly Canvas _overlayLayer = new();
    private readonly List<TextBlock> _headlinePool = [];
    private readonly List<TextBlock> _bodyPool = [];
    private readonly TextBlock _creditBlock;
    private readonly Border _hintPill;
    private readonly Border _openAiLogoHost;
    private readonly Border _claudeLogoHost;
    private readonly UiRenderScheduler _renderScheduler;
    private readonly DispatcherTimer _spinTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly PreparedTextWithSegments _bodyPrepared = PretextLayout.PrepareWithSegments(SampleTextData.DynamicBodyCopy, BodyFont);
    private readonly Dictionary<string, PreparedTextWithSegments> _preparedCache = new(StringComparer.Ordinal);
    private readonly LogoAnimationState _openAiLogo = new();
    private readonly LogoAnimationState _claudeLogo = new();

    public DynamicLayoutSampleView()
    {
        _renderScheduler = new UiRenderScheduler(DispatcherQueue, Render);
        _root = CreateRoot();
        _root.Children.Add(BuildAtmosphereLeft());
        _root.Children.Add(BuildAtmosphereRight());
        _root.Children.Add(_stage);

        _stage.Children.Add(_bodyLayer);
        _stage.Children.Add(_headlineLayer);
        _stage.Children.Add(_overlayLayer);

        _creditBlock = new TextBlock
        {
            Text = CreditText,
            Foreground = SampleTheme.Brush(148, 17, 16, 13),
            FontFamily = new FontFamily("Helvetica Neue"),
            FontSize = 12,
            CharacterSpacing = 140,
        };

        _hintPill = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 16, 0, 0),
            Background = SampleTheme.Brush(240, 17, 16, 13),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(16, 10, 16, 11),
            Child = new TextBlock
            {
                Text = "Everything laid out in C#. Resize horizontally and vertically, then click the logos.",
                Foreground = SampleTheme.Brush(245, 246, 240, 230),
                FontFamily = new FontFamily("Helvetica Neue"),
                FontSize = 12,
                TextWrapping = TextWrapping.NoWrap,
            },
        };

        _openAiLogoHost = CreateLogoHost("ms-appx:///Assets/openai_symbol.png", "OpenAI", () => StartLogoSpin(_openAiLogo, 1));
        _claudeLogoHost = CreateLogoHost("ms-appx:///Assets/claude_symbol.png", "Claude", () => StartLogoSpin(_claudeLogo, -1));

        _overlayLayer.Children.Add(_creditBlock);
        _overlayLayer.Children.Add(_openAiLogoHost);
        _overlayLayer.Children.Add(_claudeLogoHost);
        _root.Children.Add(_hintPill);

        Content = _root;

        Loaded += (_, _) => _renderScheduler.Schedule();
        SizeChanged += (_, _) => _renderScheduler.Schedule();
        Unloaded += (_, _) => _spinTimer.Stop();
        _spinTimer.Tick += (_, _) =>
        {
            if (!UpdateSpinState())
            {
                _spinTimer.Stop();
            }

            _renderScheduler.Schedule();
        };
    }

    private void Render()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var pageWidth = ActualWidth;
        var pageHeight = ActualHeight;
        var layout = BuildLayout(pageWidth, pageHeight, BodyLineHeight);
        var evaluation = EvaluateLayout(layout);

        _stage.Width = pageWidth;
        _stage.Height = pageHeight;

        _hintPill.Visibility = layout.IsNarrow ? Visibility.Collapsed : Visibility.Visible;

        SampleUi.EnsurePool(_headlineLayer, _headlinePool, evaluation.HeadlineLines.Count, () =>
            SampleUi.CreateCanvasLine(string.Empty, "Iowan Old Style", 48, SampleTheme.InkBrush, FontWeights.Bold));

        for (var index = 0; index < evaluation.HeadlineLines.Count; index++)
        {
            var line = evaluation.HeadlineLines[index];
            var block = _headlinePool[index];
            block.Text = line.Text;
            block.FontSize = layout.HeadlineFontSize;
            block.FontFamily = new FontFamily("Iowan Old Style");
            block.FontWeight = FontWeights.Bold;
            Canvas.SetLeft(block, line.X);
            Canvas.SetTop(block, line.Y);
        }

        var bodyLines = evaluation.LeftLines.Count + evaluation.RightLines.Count;
        SampleUi.EnsurePool(_bodyLayer, _bodyPool, bodyLines, () =>
            SampleUi.CreateCanvasLine(string.Empty, "Iowan Old Style", 20, SampleTheme.InkBrush, FontWeights.Normal));

        var bodyIndex = 0;
        foreach (var line in evaluation.LeftLines)
        {
            var block = _bodyPool[bodyIndex++];
            block.Text = line.Text;
            block.FontSize = 20;
            block.FontFamily = new FontFamily("Iowan Old Style");
            Canvas.SetLeft(block, line.X);
            Canvas.SetTop(block, line.Y);
        }

        foreach (var line in evaluation.RightLines)
        {
            var block = _bodyPool[bodyIndex++];
            block.Text = line.Text;
            block.FontSize = 20;
            block.FontFamily = new FontFamily("Iowan Old Style");
            Canvas.SetLeft(block, line.X);
            Canvas.SetTop(block, line.Y);
        }

        Canvas.SetLeft(_creditBlock, evaluation.CreditLeft);
        Canvas.SetTop(_creditBlock, evaluation.CreditTop);

        ApplyLogoLayout(_openAiLogoHost, layout.OpenAiRect, _openAiLogo.Angle);
        ApplyLogoLayout(_claudeLogoHost, layout.ClaudeRect, _claudeLogo.Angle);
    }

    private Grid CreateRoot()
    {
        return new Grid
        {
            Background = SampleTheme.PageBrush,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
    }

    private static Rectangle BuildAtmosphereLeft()
    {
        return new Rectangle
        {
            IsHitTestVisible = false,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.16, 0.82),
                RadiusX = 0.62,
                RadiusY = 0.54,
                GradientStops =
                {
                    new GradientStop { Offset = 0, Color = ColorHelper.FromArgb(41, 45, 88, 128) },
                    new GradientStop { Offset = 0.69, Color = ColorHelper.FromArgb(0, 45, 88, 128) },
                },
            },
        };
    }

    private static Rectangle BuildAtmosphereRight()
    {
        return new Rectangle
        {
            IsHitTestVisible = false,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.86, 0.16),
                RadiusX = 0.58,
                RadiusY = 0.48,
                GradientStops =
                {
                    new GradientStop { Offset = 0, Color = ColorHelper.FromArgb(46, 217, 119, 87) },
                    new GradientStop { Offset = 0.7, Color = ColorHelper.FromArgb(0, 217, 119, 87) },
                },
            },
        };
    }

    private Border CreateLogoHost(string assetUri, string label, Action onClick)
    {
        var host = new Border
        {
            Background = new SolidColorBrush(Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Child = new Image
            {
                Source = new BitmapImage(new Uri(assetUri)),
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            },
        };

        ToolTipService.SetToolTip(host, label);
        host.Tapped += (_, _) => onClick();
        return host;
    }

    private void StartLogoSpin(LogoAnimationState logo, int direction)
    {
        var delta = direction * Math.PI;
        var now = Environment.TickCount64;
        logo.Spin = new SpinState(logo.Angle, logo.Angle + delta, now, 900);
        _spinTimer.Start();
        _renderScheduler.Schedule();
    }

    private bool UpdateSpinState()
    {
        var now = Environment.TickCount64;
        var openAiAnimating = UpdateSpin(_openAiLogo, now);
        var claudeAnimating = UpdateSpin(_claudeLogo, now);
        return openAiAnimating || claudeAnimating;
    }

    private static bool UpdateSpin(LogoAnimationState logo, long now)
    {
        if (logo.Spin is null)
        {
            return false;
        }

        var spin = logo.Spin;
        var progress = Math.Clamp((now - spin.StartMs) / spin.DurationMs, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);
        logo.Angle = spin.From + (spin.To - spin.From) * eased;
        if (progress >= 1)
        {
            logo.Angle = spin.To;
            logo.Spin = null;
            return false;
        }

        return true;
    }

    private PreparedTextWithSegments GetPrepared(string text, string font)
    {
        var key = $"{font}::{text}";
        if (_preparedCache.TryGetValue(key, out var prepared))
        {
            return prepared;
        }

        prepared = PretextLayout.PrepareWithSegments(text, font);
        _preparedCache[key] = prepared;
        return prepared;
    }

    private bool HeadlineBreaksInsideWord(PreparedTextWithSegments prepared, double maxWidth)
    {
        var breaksInsideWord = false;
        PretextLayout.WalkLineRanges(prepared, maxWidth, line =>
        {
            if (line.End.GraphemeIndex != 0)
            {
                breaksInsideWord = true;
            }
        });
        return breaksInsideWord;
    }

    private double GetPreparedSingleLineWidth(PreparedTextWithSegments prepared)
    {
        var width = 0d;
        PretextLayout.WalkLineRanges(prepared, 100_000, line => width = line.Width);
        return width;
    }

    private double FitHeadlineFontSize(double headlineWidth, double pageWidth)
    {
        var low = Math.Ceiling(Math.Max(22, pageWidth * 0.026));
        var high = Math.Floor(Math.Min(94.4, Math.Max(55.2, pageWidth * 0.055)));
        var best = low;

        while (low <= high)
        {
            var size = Math.Floor((low + high) / 2);
            var font = $"700 {size}px {TitleFontFamily}";
            var headlinePrepared = GetPrepared(TitleText, font);
            if (!HeadlineBreaksInsideWord(headlinePrepared, headlineWidth))
            {
                best = size;
                low = size + 1;
            }
            else
            {
                high = size - 1;
            }
        }

        return best;
    }

    private PageLayout BuildLayout(double pageWidth, double pageHeight, double lineHeight)
    {
        var isNarrow = pageWidth < NarrowBreakpoint;
        if (isNarrow)
        {
            var gutter = Math.Round(Math.Max(18, Math.Min(28, pageWidth * 0.06)));
            var columnWidth = Math.Round(Math.Min(pageWidth - gutter * 2, NarrowColumnMaxWidth));
            var headlineWidth = pageWidth - gutter * 2;
            var headlineFontSize = Math.Min(48, FitHeadlineFontSize(headlineWidth, pageWidth));
            var headlineLineHeight = Math.Round(headlineFontSize * 0.92);
            var headlineFont = $"700 {headlineFontSize}px {TitleFontFamily}";
            var creditGap = Math.Round(Math.Max(12, lineHeight * 0.5));
            var copyGap = Math.Round(Math.Max(18, lineHeight * 0.7));
            var claudeSize = Math.Round(Math.Min(92, Math.Min(pageWidth * 0.23, pageHeight * 0.11)));
            var openAiSize = Math.Round(Math.Min(138, pageWidth * 0.34));

            return new PageLayout(
                true,
                gutter,
                pageWidth,
                pageHeight,
                0,
                columnWidth,
                new RectObstacle(gutter, 28, headlineWidth, Math.Max(320, pageHeight - 28 - gutter)),
                headlineFont,
                headlineFontSize,
                headlineLineHeight,
                creditGap,
                copyGap,
                new RectObstacle(gutter - Math.Round(openAiSize * 0.22), pageHeight - gutter - openAiSize + Math.Round(openAiSize * 0.08), openAiSize, openAiSize),
                new RectObstacle(pageWidth - gutter - Math.Round(claudeSize * 0.88), 4, claudeSize, claudeSize));
        }

        var wideGutter = Math.Round(Math.Max(52, pageWidth * 0.048));
        var centerGap = Math.Round(Math.Max(28, pageWidth * 0.025));
        var wideColumnWidth = Math.Round((pageWidth - wideGutter * 2 - centerGap) / 2);
        var headlineTop = Math.Round(Math.Max(42, Math.Max(pageWidth * 0.04, 72)));
        var wideHeadlineWidth = Math.Round(Math.Min(pageWidth - wideGutter * 2, Math.Max(wideColumnWidth, pageWidth * 0.5)));
        var wideHeadlineFontSize = FitHeadlineFontSize(wideHeadlineWidth, pageWidth);
        var wideHeadlineLineHeight = Math.Round(wideHeadlineFontSize * 0.92);
        var wideHeadlineFont = $"700 {wideHeadlineFontSize}px {TitleFontFamily}";
        var wideCreditGap = Math.Round(Math.Max(14, lineHeight * 0.6));
        var wideCopyGap = Math.Round(Math.Max(20, lineHeight * 0.9));
        var openAiShrinkT = Math.Max(0, Math.Min(1, (960 - pageWidth) / 260));
        var openAiSizeWide = Math.Round(Math.Min(400 - openAiShrinkT * 56, pageHeight * 0.43));
        var claudeSizeWide = Math.Round(Math.Max(276, Math.Min(500, Math.Min(pageWidth * 0.355, pageHeight * 0.45))));

        return new PageLayout(
            false,
            wideGutter,
            pageWidth,
            pageHeight,
            centerGap,
            wideColumnWidth,
            new RectObstacle(wideGutter, headlineTop, wideHeadlineWidth, pageHeight - headlineTop - wideGutter),
            wideHeadlineFont,
            wideHeadlineFontSize,
            wideHeadlineLineHeight,
            wideCreditGap,
            wideCopyGap,
            new RectObstacle(wideGutter - Math.Round(openAiSizeWide * 0.3), pageHeight - wideGutter - openAiSizeWide + Math.Round(openAiSizeWide * 0.2), openAiSizeWide, openAiSizeWide),
            new RectObstacle(pageWidth - Math.Round(claudeSizeWide * 0.69), -Math.Round(claudeSizeWide * 0.22), claudeSizeWide, claudeSizeWide));
    }

    private LayoutEvaluation EvaluateLayout(PageLayout layout)
    {
        var headlinePrepared = GetPrepared(TitleText, layout.HeadlineFont);
        var openAiObstacle = new EllipseBandObstacle(
            layout.OpenAiRect.X + layout.OpenAiRect.Width / 2,
            layout.OpenAiRect.Y + layout.OpenAiRect.Height / 2,
            layout.OpenAiRect.Width / 2,
            layout.OpenAiRect.Height / 2,
            Math.Round(BodyLineHeight * 0.82),
            Math.Round(BodyLineHeight * 0.26));
        var claudeObstacle = new RectBandObstacle(
            [layout.ClaudeRect],
            Math.Round(BodyLineHeight * 0.28),
            Math.Round(BodyLineHeight * 0.12));

        var headlineResult = LayoutColumn(
            headlinePrepared,
            new LayoutCursor(0, 0),
            layout.HeadlineRegion,
            layout.HeadlineLineHeight,
            [openAiObstacle],
            preferRightOnTie: false);
        var headlineLines = headlineResult.Lines;
        var headlineRects = headlineLines
            .Select(line => new RectObstacle(line.X, line.Y, Math.Ceiling(line.Width), layout.HeadlineLineHeight))
            .ToArray();
        var headlineBottom = headlineLines.Count == 0
            ? layout.HeadlineRegion.Y
            : headlineLines.Max(line => line.Y + layout.HeadlineLineHeight);
        var creditTop = headlineBottom + layout.CreditGap;
        var creditRegion = new RectObstacle(layout.Gutter + 4, creditTop, layout.HeadlineRegion.Width, CreditLineHeight);
        var copyTop = creditTop + CreditLineHeight + layout.CopyGap;
        var titleObstacle = new RectBandObstacle(headlineRects, Math.Round(BodyLineHeight * 0.95), Math.Round(BodyLineHeight * 0.3));
        var creditBlocked = GetObstacleIntervals(openAiObstacle, creditRegion.Y, creditRegion.Bottom);
        var claudeCreditBlocked = GetObstacleIntervals(claudeObstacle, creditRegion.Y, creditRegion.Bottom);
        var creditSlots = ObstacleLayoutHelper.CarveTextLineSlots(
            new Interval(creditRegion.X, creditRegion.Right),
            layout.IsNarrow ? creditBlocked.Concat(claudeCreditBlocked) : creditBlocked);
        var creditWidth = GetPreparedSingleLineWidth(GetPrepared(CreditText, CreditFont));
        var creditLeft = creditRegion.X;
        foreach (var slot in creditSlots)
        {
            if (slot.Width >= creditWidth)
            {
                creditLeft = Math.Round(slot.Left);
                break;
            }
        }

        if (layout.IsNarrow)
        {
            var bodyRegion = new RectObstacle(
                Math.Round((layout.PageWidth - layout.ColumnWidth) / 2),
                copyTop,
                layout.ColumnWidth,
                Math.Max(0, layout.PageHeight - copyTop - layout.Gutter));
            var bodyResult = LayoutColumn(_bodyPrepared, new LayoutCursor(0, 0), bodyRegion, BodyLineHeight, [claudeObstacle, openAiObstacle], false);
            return new LayoutEvaluation(headlineLines, creditLeft, creditTop, bodyResult.Lines, [], layout.PageHeight);
        }

        var leftRegion = new RectObstacle(layout.Gutter, copyTop, layout.ColumnWidth, layout.PageHeight - copyTop - layout.Gutter);
        var rightRegion = new RectObstacle(layout.Gutter + layout.ColumnWidth + layout.CenterGap, layout.HeadlineRegion.Y, layout.ColumnWidth, layout.PageHeight - layout.HeadlineRegion.Y - layout.Gutter);
        var leftResult = LayoutColumn(_bodyPrepared, new LayoutCursor(0, 0), leftRegion, BodyLineHeight, [openAiObstacle], false);
        var rightResult = LayoutColumn(_bodyPrepared, leftResult.Cursor, rightRegion, BodyLineHeight, [titleObstacle, claudeObstacle, openAiObstacle], true);
        return new LayoutEvaluation(headlineLines, creditLeft, creditTop, leftResult.Lines, rightResult.Lines, layout.PageHeight);
    }

    private static (List<PositionedLine> Lines, LayoutCursor Cursor) LayoutColumn(
        PreparedTextWithSegments prepared,
        LayoutCursor startCursor,
        RectObstacle region,
        double lineHeight,
        IReadOnlyList<BandObstacle> obstacles,
        bool preferRightOnTie)
    {
        var cursor = startCursor;
        var lineTop = region.Y;
        var lines = new List<PositionedLine>();
        while (lineTop + lineHeight <= region.Bottom)
        {
            var blocked = new List<Interval>();
            foreach (var obstacle in obstacles)
            {
                blocked.AddRange(GetObstacleIntervals(obstacle, lineTop, lineTop + lineHeight));
            }

            var slots = ObstacleLayoutHelper.CarveTextLineSlots(new Interval(region.X, region.Right), blocked);
            if (slots.Count == 0)
            {
                lineTop += lineHeight;
                continue;
            }

            var slot = ObstacleLayoutHelper.PickSlot(slots, preferRightOnTie);
            var width = slot.Width;
            var line = PretextLayout.LayoutNextLine(prepared, cursor, width);
            if (line is null)
            {
                break;
            }

            lines.Add(new PositionedLine(line.Text, Math.Round(slot.Left), Math.Round(lineTop), line.Width));
            cursor = line.End;
            lineTop += lineHeight;
        }

        return (lines, cursor);
    }

    private static List<Interval> GetObstacleIntervals(BandObstacle obstacle, double bandTop, double bandBottom)
    {
        return obstacle switch
        {
            EllipseBandObstacle ellipse => ObstacleLayoutHelper.EllipseIntervalForBand(
                ellipse.CenterX,
                ellipse.CenterY,
                ellipse.RadiusX,
                ellipse.RadiusY,
                bandTop,
                bandBottom,
                ellipse.HorizontalPadding,
                ellipse.VerticalPadding) is { } interval
                ? [interval]
                : [],
            RectBandObstacle rects => ObstacleLayoutHelper.GetRectIntervalsForBand(
                rects.Rects,
                bandTop,
                bandBottom,
                rects.HorizontalPadding,
                rects.VerticalPadding),
            _ => [],
        };
    }

    private static void ApplyLogoLayout(Border logoHost, RectObstacle rect, double angle)
    {
        logoHost.Width = rect.Width;
        logoHost.Height = rect.Height;
        logoHost.RenderTransform = new RotateTransform { Angle = angle * 180 / Math.PI };
        Canvas.SetLeft(logoHost, rect.X);
        Canvas.SetTop(logoHost, rect.Y);
    }

    private sealed record PageLayout(
        bool IsNarrow,
        double Gutter,
        double PageWidth,
        double PageHeight,
        double CenterGap,
        double ColumnWidth,
        RectObstacle HeadlineRegion,
        string HeadlineFont,
        double HeadlineFontSize,
        double HeadlineLineHeight,
        double CreditGap,
        double CopyGap,
        RectObstacle OpenAiRect,
        RectObstacle ClaudeRect);

    private sealed record LayoutEvaluation(
        IReadOnlyList<PositionedLine> HeadlineLines,
        double CreditLeft,
        double CreditTop,
        IReadOnlyList<PositionedLine> LeftLines,
        IReadOnlyList<PositionedLine> RightLines,
        double ContentHeight);

    private abstract record BandObstacle(double HorizontalPadding, double VerticalPadding);

    private sealed record RectBandObstacle(IReadOnlyList<RectObstacle> Rects, double HorizontalPadding, double VerticalPadding)
        : BandObstacle(HorizontalPadding, VerticalPadding);

    private sealed record EllipseBandObstacle(double CenterX, double CenterY, double RadiusX, double RadiusY, double HorizontalPadding, double VerticalPadding)
        : BandObstacle(HorizontalPadding, VerticalPadding);

    private sealed class LogoAnimationState
    {
        public double Angle { get; set; }

        public SpinState? Spin { get; set; }
    }

    private sealed record SpinState(double From, double To, long StartMs, double DurationMs);
}

public sealed class EditorialEngineSampleView : UserControl
{
    private const string TitleText = "THE FUTURE OF TEXT LAYOUT IS NOT CSS";
    private const string HeadlineFontFamily = "\"Iowan Old Style\", \"Palatino Linotype\", \"Book Antiqua\", Palatino, serif";
    private const string BodyFont = "18px \"Iowan Old Style\", \"Palatino Linotype\", \"Book Antiqua\", Palatino, serif";
    private const string PullquoteFont = "italic 19px \"Iowan Old Style\", \"Palatino Linotype\", \"Book Antiqua\", Palatino, serif";
    private const double BodyLineHeight = 30;
    private const int Gutter = 48;
    private const int ColGap = 40;
    private const int BottomGap = 20;
    private const int DropCapLines = 3;
    private const int MinSlotWidth = 50;
    private const int NarrowBreakpoint = 760;
    private const int NarrowGutter = 20;
    private const int NarrowColGap = 20;
    private const int NarrowBottomGap = 16;
    private const double NarrowOrbScale = 0.58;
    private const int NarrowActiveOrbs = 3;
    private const int PullquoteLineHeight = 27;

    private readonly Grid _root;
    private readonly Canvas _stage = new();
    private readonly Canvas _headlineLayer = new();
    private readonly Canvas _bodyLayer = new();
    private readonly Canvas _pullquoteLayer = new();
    private readonly Canvas _orbLayer = new();
    private readonly List<TextBlock> _headlinePool = [];
    private readonly List<TextBlock> _bodyPool = [];
    private readonly List<TextBlock> _pullquoteLinePool = [];
    private readonly List<Border> _pullquoteBoxPool = [];
    private readonly List<OrbVisual> _orbPool = [];
    private readonly TextBlock _dropCapBlock;
    private readonly Border _hintPill;
    private readonly TextBlock _creditBlock;
    private readonly PreparedTextWithSegments _bodyPrepared = PretextLayout.PrepareWithSegments(SampleTextData.EditorialBodyText, BodyFont);
    private readonly PreparedTextWithSegments[] _pullquotes = SampleTextData.EditorialPullquotes.Select(text => PretextLayout.PrepareWithSegments(text, PullquoteFont)).ToArray();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(24) };
    private readonly UiRenderScheduler _renderScheduler;
    private readonly List<OrbState> _orbs;
    private readonly DragState _dragState = new();
    private long _lastTickMs;
    private double _dropCapWidth;
    private double _cachedHeadlineWidth = -1;
    private double _cachedHeadlineHeight = -1;
    private double _cachedHeadlineMaxSize = -1;
    private double _cachedHeadlineFontSize = 24;
    private List<PositionedLine> _cachedHeadlineLines = [];

    public EditorialEngineSampleView()
    {
        _renderScheduler = new UiRenderScheduler(DispatcherQueue, Render);
        _root = new Grid
        {
            Background = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.4),
                RadiusX = 0.8,
                RadiusY = 0.8,
                GradientStops =
                {
                    new GradientStop { Offset = 0, Color = ColorHelper.FromArgb(255, 15, 15, 20) },
                    new GradientStop { Offset = 1, Color = ColorHelper.FromArgb(255, 10, 10, 12) },
                },
            },
        };
        _root.Children.Add(_stage);

        _stage.Children.Add(_bodyLayer);
        _stage.Children.Add(_pullquoteLayer);
        _stage.Children.Add(_headlineLayer);
        _stage.Children.Add(_orbLayer);

        _dropCapBlock = SampleUi.CreateCanvasLine("T", "Iowan Old Style", BodyLineHeight * DropCapLines - 4, SampleTheme.Brush(0xC4, 0xA3, 0x5A), FontWeights.Bold);
        _dropCapBlock.Text = "T";
        _dropCapBlock.Visibility = Visibility.Visible;
        _stage.Children.Add(_dropCapBlock);

        _hintPill = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 16, 0, 0),
            Background = SampleTheme.Brush(115, 0, 0, 0),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(18, 8, 18, 8),
            Child = new TextBlock
            {
                Text = "Drag the orbs · Click to pause · Zero UI-tree reads",
                Foreground = SampleTheme.Brush(56, 255, 255, 255),
                FontFamily = new FontFamily("Helvetica Neue"),
                FontSize = 13,
                TextWrapping = TextWrapping.NoWrap,
            },
        };

        _creditBlock = new TextBlock
        {
            Text = "Made by @somnai_dreams",
            Foreground = SampleTheme.Brush(72, 255, 255, 255),
            FontFamily = new FontFamily("Helvetica Neue"),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 16, 12),
        };

        _root.Children.Add(_hintPill);
        _root.Children.Add(_creditBlock);
        Content = _root;

        _orbs =
        [
            new OrbState(0.52, 0.22, 110, 24, 16, 196, 163, 90),
            new OrbState(0.18, 0.48, 85, -19, 26, 100, 140, 255),
            new OrbState(0.74, 0.58, 95, 16, -21, 232, 100, 130),
            new OrbState(0.38, 0.72, 75, -26, -14, 80, 200, 140),
            new OrbState(0.86, 0.18, 65, -13, 19, 150, 100, 220),
        ];

        var dropCapPrepared = PretextLayout.PrepareWithSegments("T", $"700 {BodyLineHeight * DropCapLines - 4}px {HeadlineFontFamily}");
        PretextLayout.WalkLineRanges(dropCapPrepared, 9999, line => _dropCapWidth = line.Width);

        _root.PointerPressed += OnPointerPressed;
        _root.PointerMoved += OnPointerMoved;
        _root.PointerReleased += OnPointerReleased;
        _root.PointerCanceled += OnPointerReleased;
        _root.PointerCaptureLost += OnPointerCaptureLost;
        _root.PointerExited += OnPointerExited;
        _timer.Tick += OnTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => _renderScheduler.Schedule();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SeedOrbPositions();
        _lastTickMs = Environment.TickCount64;
        _renderScheduler.Schedule();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
    }

    private void SeedOrbPositions()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        foreach (var orb in _orbs)
        {
            orb.X = orb.Fx * ActualWidth;
            orb.Y = orb.Fy * ActualHeight;
        }
    }

    private void OnTick(object? sender, object e)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var now = Environment.TickCount64;
        var dt = Math.Min((now - _lastTickMs) / 1000d, 0.05);
        _lastTickMs = now;
        AdvanceOrbs(dt);
        _renderScheduler.Schedule();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_root).Position;
        var orbIndex = HitTestOrbs(point.X, point.Y);
        if (orbIndex < 0)
        {
            return;
        }

        _dragState.Active = true;
        _dragState.OrbIndex = orbIndex;
        _dragState.StartPointerX = point.X;
        _dragState.StartPointerY = point.Y;
        _dragState.StartOrbX = _orbs[orbIndex].X;
        _dragState.StartOrbY = _orbs[orbIndex].Y;
        _dragState.Moved = false;
        _root.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragState.Active || _dragState.OrbIndex < 0)
        {
            return;
        }

        var point = e.GetCurrentPoint(_root).Position;
        var orb = _orbs[_dragState.OrbIndex];
        var dx = point.X - _dragState.StartPointerX;
        var dy = point.Y - _dragState.StartPointerY;
        orb.X = _dragState.StartOrbX + dx;
        orb.Y = _dragState.StartOrbY + dy;
        _dragState.Moved = _dragState.Moved || dx * dx + dy * dy > 16;
        _renderScheduler.Schedule();
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragState.Active || _dragState.OrbIndex < 0)
        {
            return;
        }

        var point = e.GetCurrentPoint(_root).Position;
        var orb = _orbs[_dragState.OrbIndex];
        var dx = point.X - _dragState.StartPointerX;
        var dy = point.Y - _dragState.StartPointerY;
        if (!_dragState.Moved && dx * dx + dy * dy < 16)
        {
            orb.Paused = !orb.Paused;
        }
        else
        {
            orb.X = _dragState.StartOrbX + dx;
            orb.Y = _dragState.StartOrbY + dy;
        }

        _root.ReleasePointerCaptures();
        _dragState.Reset();
        _renderScheduler.Schedule();
        e.Handled = true;
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragState.Active)
        {
            return;
        }

        if (!e.GetCurrentPoint(_root).Properties.IsLeftButtonPressed)
        {
            _root.ReleasePointerCaptures();
            _dragState.Reset();
            _renderScheduler.Schedule();
        }
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragState.Active)
        {
            return;
        }

        _dragState.Reset();
        _renderScheduler.Schedule();
    }

    private int HitTestOrbs(double x, double y)
    {
        var isNarrow = ActualWidth < NarrowBreakpoint;
        var activeCount = isNarrow ? Math.Min(NarrowActiveOrbs, _orbs.Count) : _orbs.Count;
        var radiusScale = isNarrow ? NarrowOrbScale : 1;
        for (var index = activeCount - 1; index >= 0; index--)
        {
            var orb = _orbs[index];
            var radius = orb.Radius * radiusScale;
            var dx = x - orb.X;
            var dy = y - orb.Y;
            if (dx * dx + dy * dy <= radius * radius)
            {
                return index;
            }
        }

        return -1;
    }

    private void AdvanceOrbs(double dt)
    {
        var pageWidth = ActualWidth;
        var pageHeight = ActualHeight;
        if (pageWidth <= 0 || pageHeight <= 0)
        {
            return;
        }

        var isNarrow = pageWidth < NarrowBreakpoint;
        var gutter = isNarrow ? NarrowGutter : Gutter;
        var bottomGap = isNarrow ? NarrowBottomGap : BottomGap;
        var radiusScale = isNarrow ? NarrowOrbScale : 1;
        var activeCount = isNarrow ? Math.Min(NarrowActiveOrbs, _orbs.Count) : _orbs.Count;
        var draggedIndex = _dragState.Active ? _dragState.OrbIndex : -1;

        for (var index = 0; index < activeCount; index++)
        {
            var orb = _orbs[index];
            var radius = orb.Radius * radiusScale;
            if (orb.Paused || index == draggedIndex)
            {
                continue;
            }

            orb.X += orb.Vx * dt;
            orb.Y += orb.Vy * dt;

            if (orb.X - radius < 0)
            {
                orb.X = radius;
                orb.Vx = Math.Abs(orb.Vx);
            }

            if (orb.X + radius > pageWidth)
            {
                orb.X = pageWidth - radius;
                orb.Vx = -Math.Abs(orb.Vx);
            }

            if (orb.Y - radius < gutter * 0.5)
            {
                orb.Y = radius + gutter * 0.5;
                orb.Vy = Math.Abs(orb.Vy);
            }

            if (orb.Y + radius > pageHeight - bottomGap)
            {
                orb.Y = pageHeight - bottomGap - radius;
                orb.Vy = -Math.Abs(orb.Vy);
            }
        }

        for (var index = 0; index < activeCount; index++)
        {
            var a = _orbs[index];
            var aRadius = a.Radius * radiusScale;
            for (var otherIndex = index + 1; otherIndex < activeCount; otherIndex++)
            {
                var b = _orbs[otherIndex];
                var bRadius = b.Radius * radiusScale;
                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                var dist = Math.Sqrt(dx * dx + dy * dy);
                var minDist = aRadius + bRadius + (isNarrow ? 12 : 20);
                if (dist >= minDist || dist <= 0.1)
                {
                    continue;
                }

                var force = (minDist - dist) * 0.8;
                var nx = dx / dist;
                var ny = dy / dist;
                if (!a.Paused && index != draggedIndex)
                {
                    a.Vx -= nx * force * dt;
                    a.Vy -= ny * force * dt;
                }

                if (!b.Paused && otherIndex != draggedIndex)
                {
                    b.Vx += nx * force * dt;
                    b.Vy += ny * force * dt;
                }
            }
        }
    }

    private void Render()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var pageWidth = ActualWidth;
        var pageHeight = ActualHeight;
        var isNarrow = pageWidth < NarrowBreakpoint;
        var gutter = isNarrow ? NarrowGutter : Gutter;
        var colGap = isNarrow ? NarrowColGap : ColGap;
        var bottomGap = isNarrow ? NarrowBottomGap : BottomGap;
        var orbRadiusScale = isNarrow ? NarrowOrbScale : 1;
        var activeOrbCount = isNarrow ? Math.Min(NarrowActiveOrbs, _orbs.Count) : _orbs.Count;

        _stage.Width = pageWidth;
        _stage.Height = pageHeight;
        _hintPill.Visibility = isNarrow ? Visibility.Collapsed : Visibility.Visible;
        _creditBlock.Visibility = isNarrow ? Visibility.Collapsed : Visibility.Visible;

        var headlineWidth = Math.Min(pageWidth - gutter * 2 - (isNarrow ? 12 : 0), 1000);
        var maxHeadlineHeight = Math.Floor(pageHeight * (isNarrow ? 0.2 : 0.24));
        var headlineFit = FitHeadline(headlineWidth, maxHeadlineHeight, isNarrow ? 38 : 92);
        var headlineSize = headlineFit.FontSize;
        var headlineLines = headlineFit.Lines;
        var headlineLineHeight = Math.Round(headlineSize * 0.93);
        var bodyTop = gutter + headlineLines.Count * headlineLineHeight + (isNarrow ? 14 : 20);
        var bodyHeight = pageHeight - bodyTop - bottomGap;
        var columnCount = pageWidth > 1000 ? 3 : pageWidth > 640 ? 2 : 1;
        var maxContentWidth = Math.Min(pageWidth, 1500);
        var columnWidth = Math.Floor((maxContentWidth - gutter * 2 - colGap * (columnCount - 1)) / columnCount);
        var contentLeft = Math.Round((pageWidth - (columnCount * columnWidth + (columnCount - 1) * colGap)) / 2);
        var dropCapRect = new RectObstacle(contentLeft - 2, bodyTop - 2, Math.Ceiling(_dropCapWidth) + 10, DropCapLines * BodyLineHeight + 2);

        var pullquotePlacements = new[]
        {
            new PullquotePlacement(0, 0.48, 0.52, false),
            new PullquotePlacement(1, 0.32, 0.5, true),
        };
        var pullquoteRects = new List<PullquoteRect>();
        for (var index = 0; index < _pullquotes.Length; index++)
        {
            if (isNarrow)
            {
                break;
            }

            var placement = pullquotePlacements[index];
            if (placement.ColumnIndex >= columnCount)
            {
                continue;
            }

            var pullquoteWidth = Math.Round(columnWidth * placement.WidthFraction);
            var pullquoteLines = PretextLayout.LayoutWithLines(_pullquotes[index], pullquoteWidth - 20, PullquoteLineHeight).Lines;
            var pullquoteHeight = pullquoteLines.Count * PullquoteLineHeight + 16;
            var columnX = contentLeft + placement.ColumnIndex * (columnWidth + colGap);
            var pullquoteX = placement.AlignLeft ? columnX : columnX + columnWidth - pullquoteWidth;
            var pullquoteY = Math.Round(bodyTop + bodyHeight * placement.YFraction);
            var positionedLines = pullquoteLines
                .Select((line, lineIndex) => new PositionedLine(line.Text, pullquoteX + 20, pullquoteY + 8 + lineIndex * PullquoteLineHeight, line.Width))
                .ToList();

            pullquoteRects.Add(new PullquoteRect(new RectObstacle(pullquoteX, pullquoteY, pullquoteWidth, pullquoteHeight), positionedLines, placement.ColumnIndex));
        }

        var circleObstacles = new List<EditorialCircleObstacle>();
        for (var index = 0; index < activeOrbCount; index++)
        {
            var orb = _orbs[index];
            circleObstacles.Add(new EditorialCircleObstacle(orb.X, orb.Y, orb.Radius * orbRadiusScale, isNarrow ? 10 : 14, isNarrow ? 2 : 4));
        }

        var allBodyLines = new List<PositionedLine>();
        var cursor = new LayoutCursor(0, 1);
        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            var columnX = contentLeft + columnIndex * (columnWidth + colGap);
            var rects = new List<RectObstacle>();
            if (columnIndex == 0)
            {
                rects.Add(dropCapRect);
            }

            foreach (var pullquote in pullquoteRects.Where(p => p.ColumnIndex == columnIndex))
            {
                rects.Add(pullquote.Rect);
            }

            var result = LayoutEditorialColumn(
                _bodyPrepared,
                cursor,
                columnX,
                bodyTop,
                columnWidth,
                bodyHeight,
                BodyLineHeight,
                circleObstacles,
                rects,
                isNarrow);
            allBodyLines.AddRange(result.Lines);
            cursor = result.Cursor;
        }

        SampleUi.EnsurePool(_headlineLayer, _headlinePool, headlineLines.Count, () =>
            SampleUi.CreateCanvasLine(string.Empty, "Iowan Old Style", 64, SampleTheme.WhiteBrush, FontWeights.Bold));
        for (var index = 0; index < headlineLines.Count; index++)
        {
            var line = headlineLines[index];
            var block = _headlinePool[index];
            block.Text = line.Text;
            block.FontFamily = new FontFamily("Iowan Old Style");
            block.FontSize = headlineSize;
            block.FontWeight = FontWeights.Bold;
            block.Foreground = SampleTheme.WhiteBrush;
            Canvas.SetLeft(block, gutter);
            Canvas.SetTop(block, gutter + line.Y);
        }

        Canvas.SetLeft(_dropCapBlock, contentLeft);
        Canvas.SetTop(_dropCapBlock, bodyTop);

        SampleUi.EnsurePool(_bodyLayer, _bodyPool, allBodyLines.Count, () =>
            SampleUi.CreateCanvasLine(string.Empty, "Iowan Old Style", 18, SampleTheme.Brush(0xE8, 0xE4, 0xDC), FontWeights.Normal));
        for (var index = 0; index < allBodyLines.Count; index++)
        {
            var line = allBodyLines[index];
            var block = _bodyPool[index];
            block.Text = line.Text;
            block.FontSize = 18;
            block.FontFamily = new FontFamily("Iowan Old Style");
            block.Foreground = SampleTheme.Brush(0xE8, 0xE4, 0xDC);
            Canvas.SetLeft(block, line.X);
            Canvas.SetTop(block, line.Y);
        }

        SampleUi.EnsurePool(_pullquoteLayer, _pullquoteBoxPool, pullquoteRects.Count, CreatePullquoteBox);
        var totalPullquoteLines = pullquoteRects.Sum(pullquote => pullquote.Lines.Count);
        SampleUi.EnsurePool(_pullquoteLayer, _pullquoteLinePool, totalPullquoteLines, () =>
            SampleUi.CreateCanvasLine(string.Empty, "Iowan Old Style", 19, SampleTheme.Brush(0xB8, 0xA0, 0x70), FontWeights.Normal));

        var pullquoteLineIndex = 0;
        for (var index = 0; index < pullquoteRects.Count; index++)
        {
            var pullquote = pullquoteRects[index];
            var box = _pullquoteBoxPool[index];
            box.Width = pullquote.Rect.Width;
            box.Height = pullquote.Rect.Height;
            Canvas.SetLeft(box, pullquote.Rect.X);
            Canvas.SetTop(box, pullquote.Rect.Y);

            foreach (var line in pullquote.Lines)
            {
                var lineBlock = _pullquoteLinePool[pullquoteLineIndex++];
                lineBlock.Text = line.Text;
                lineBlock.FontFamily = new FontFamily("Iowan Old Style");
                lineBlock.FontSize = 19;
                lineBlock.FontStyle = Windows.UI.Text.FontStyle.Italic;
                lineBlock.Foreground = SampleTheme.Brush(0xB8, 0xA0, 0x70);
                Canvas.SetLeft(lineBlock, line.X);
                Canvas.SetTop(lineBlock, line.Y);
            }
        }

        EnsureOrbPool(activeOrbCount);
        for (var index = 0; index < activeOrbCount; index++)
        {
            var orb = _orbs[index];
            var visual = _orbPool[index];
            var radius = orb.Radius * orbRadiusScale;
            visual.SetColor(orb.R, orb.G, orb.B);
            visual.SetSize(radius);
            visual.Root.Opacity = orb.Paused ? 0.45 : 1;
            Canvas.SetLeft(visual.Root, orb.X - visual.OuterRadius);
            Canvas.SetTop(visual.Root, orb.Y - visual.OuterRadius);
        }
    }

    private Border CreatePullquoteBox()
    {
        return new Border
        {
            BorderBrush = SampleTheme.Brush(0x6B, 0x5A, 0x3D),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(14, 0, 0, 0),
            Background = null,
            IsHitTestVisible = false,
        };
    }

    private void EnsureOrbPool(int count)
    {
        while (_orbPool.Count < count)
        {
            var visual = new OrbVisual();
            _orbPool.Add(visual);
            _orbLayer.Children.Add(visual.Root);
        }

        for (var index = 0; index < _orbPool.Count; index++)
        {
            _orbPool[index].Root.Visibility = index < count ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static Brush CreateOrbCoreBrush(byte r, byte g, byte b)
    {
        return new RadialGradientBrush
        {
            Center = new Point(0.35, 0.35),
            RadiusX = 0.72,
            RadiusY = 0.72,
            GradientStops =
            {
                new GradientStop { Offset = 0, Color = ColorHelper.FromArgb(89, r, g, b) },
                new GradientStop { Offset = 0.55, Color = ColorHelper.FromArgb(31, r, g, b) },
                new GradientStop { Offset = 1, Color = ColorHelper.FromArgb(0, r, g, b) },
            },
        };
    }

    private static Brush CreateOrbNearGlowBrush(byte r, byte g, byte b)
    {
        return new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5,
            GradientStops =
            {
                new GradientStop { Offset = 0, Color = ColorHelper.FromArgb(46, r, g, b) },
                new GradientStop { Offset = 0.42, Color = ColorHelper.FromArgb(18, r, g, b) },
                new GradientStop { Offset = 1, Color = ColorHelper.FromArgb(0, r, g, b) },
            },
        };
    }

    private static Brush CreateOrbFarGlowBrush(byte r, byte g, byte b)
    {
        return new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5,
            GradientStops =
            {
                new GradientStop { Offset = 0, Color = ColorHelper.FromArgb(18, r, g, b) },
                new GradientStop { Offset = 0.38, Color = ColorHelper.FromArgb(10, r, g, b) },
                new GradientStop { Offset = 1, Color = ColorHelper.FromArgb(0, r, g, b) },
            },
        };
    }

    private HeadlineFit FitHeadline(double maxWidth, double maxHeight, double maxSize)
    {
        if (Math.Abs(maxWidth - _cachedHeadlineWidth) < 0.5 &&
            Math.Abs(maxHeight - _cachedHeadlineHeight) < 0.5 &&
            Math.Abs(maxSize - _cachedHeadlineMaxSize) < 0.5)
        {
            return new HeadlineFit(_cachedHeadlineFontSize, _cachedHeadlineLines);
        }

        _cachedHeadlineWidth = maxWidth;
        _cachedHeadlineHeight = maxHeight;
        _cachedHeadlineMaxSize = maxSize;
        var low = 20d;
        var high = maxSize;
        var best = low;
        var bestLines = new List<PositionedLine>();

        while (low <= high)
        {
            var size = Math.Floor((low + high) / 2);
            var font = $"700 {size}px {HeadlineFontFamily}";
            var lineHeight = Math.Round(size * 0.93);
            var prepared = PretextLayout.PrepareWithSegments(TitleText, font);
            var breaksWord = false;
            var lineCount = 0;

            PretextLayout.WalkLineRanges(prepared, maxWidth, line =>
            {
                lineCount++;
                if (line.End.GraphemeIndex != 0)
                {
                    breaksWord = true;
                }
            });

            var totalHeight = lineCount * lineHeight;
            if (!breaksWord && totalHeight <= maxHeight)
            {
                best = size;
                bestLines = PretextLayout.LayoutWithLines(prepared, maxWidth, lineHeight).Lines
                    .Select((line, index) => new PositionedLine(line.Text, 0, index * lineHeight, line.Width))
                    .ToList();
                low = size + 1;
            }
            else
            {
                high = size - 1;
            }
        }

        _cachedHeadlineFontSize = best;
        _cachedHeadlineLines = bestLines;
        return new HeadlineFit(best, bestLines);
    }

    private static (List<PositionedLine> Lines, LayoutCursor Cursor) LayoutEditorialColumn(
        PreparedTextWithSegments prepared,
        LayoutCursor startCursor,
        double regionX,
        double regionY,
        double regionWidth,
        double regionHeight,
        double lineHeight,
        IReadOnlyList<EditorialCircleObstacle> circleObstacles,
        IReadOnlyList<RectObstacle> rectObstacles,
        bool singleSlotOnly)
    {
        var cursor = startCursor;
        var lineTop = regionY;
        var lines = new List<PositionedLine>();
        var textExhausted = false;

        while (lineTop + lineHeight <= regionY + regionHeight && !textExhausted)
        {
            var blocked = new List<Interval>();
            foreach (var obstacle in circleObstacles)
            {
                var interval = ObstacleLayoutHelper.CircleIntervalForBand(
                    obstacle.CenterX,
                    obstacle.CenterY,
                    obstacle.Radius,
                    lineTop,
                    lineTop + lineHeight,
                    obstacle.HorizontalPadding,
                    obstacle.VerticalPadding);
                if (interval is not null)
                {
                    blocked.Add(interval.Value);
                }
            }

            blocked.AddRange(ObstacleLayoutHelper.GetRectIntervalsForBand(rectObstacles, lineTop, lineTop + lineHeight));
            var slots = ObstacleLayoutHelper.CarveTextLineSlots(new Interval(regionX, regionX + regionWidth), blocked, MinSlotWidth);
            if (slots.Count == 0)
            {
                lineTop += lineHeight;
                continue;
            }

            var orderedSlots = singleSlotOnly
                ? new[] { ObstacleLayoutHelper.PickSlot(slots, false) }
                : slots.OrderBy(slot => slot.Left).ToArray();

            foreach (var slot in orderedSlots)
            {
                var line = PretextLayout.LayoutNextLine(prepared, cursor, slot.Width);
                if (line is null)
                {
                    textExhausted = true;
                    break;
                }

                lines.Add(new PositionedLine(line.Text, Math.Round(slot.Left), Math.Round(lineTop), line.Width));
                cursor = line.End;
            }

            lineTop += lineHeight;
        }

        return (lines, cursor);
    }

    private sealed record EditorialCircleObstacle(double CenterX, double CenterY, double Radius, double HorizontalPadding, double VerticalPadding);

    private sealed record PullquotePlacement(int ColumnIndex, double YFraction, double WidthFraction, bool AlignLeft);

    private sealed record PullquoteRect(RectObstacle Rect, IReadOnlyList<PositionedLine> Lines, int ColumnIndex);

    private sealed record HeadlineFit(double FontSize, IReadOnlyList<PositionedLine> Lines);

    private sealed class OrbVisual
    {
        private readonly Ellipse _farGlow;
        private readonly Ellipse _nearGlow;
        private readonly Ellipse _core;
        private byte _r;
        private byte _g;
        private byte _b;

        public OrbVisual()
        {
            _farGlow = CreateLayerEllipse();
            _nearGlow = CreateLayerEllipse();
            _core = CreateLayerEllipse();

            Root = new Grid
            {
                IsHitTestVisible = false,
                Children =
                {
                    _farGlow,
                    _nearGlow,
                    _core,
                },
            };
        }

        public Grid Root { get; }

        public double OuterRadius { get; private set; }

        public void SetColor(byte r, byte g, byte b)
        {
            if (_r == r && _g == g && _b == b)
            {
                return;
            }

            _r = r;
            _g = g;
            _b = b;
            _farGlow.Fill = CreateOrbFarGlowBrush(r, g, b);
            _nearGlow.Fill = CreateOrbNearGlowBrush(r, g, b);
            _core.Fill = CreateOrbCoreBrush(r, g, b);
        }

        public void SetSize(double radius)
        {
            var nearGlowRadius = radius * 1.42;
            var farGlowRadius = radius * 1.92;
            OuterRadius = farGlowRadius;

            Root.Width = farGlowRadius * 2;
            Root.Height = farGlowRadius * 2;
            SetEllipseRadius(_farGlow, farGlowRadius);
            SetEllipseRadius(_nearGlow, nearGlowRadius);
            SetEllipseRadius(_core, radius);
        }

        private static Ellipse CreateLayerEllipse()
        {
            return new Ellipse
            {
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        private static void SetEllipseRadius(Ellipse ellipse, double radius)
        {
            ellipse.Width = radius * 2;
            ellipse.Height = radius * 2;
        }
    }

    private sealed class OrbState(double fx, double fy, double radius, double vx, double vy, byte r, byte g, byte b)
    {
        public double Fx { get; } = fx;

        public double Fy { get; } = fy;

        public double Radius { get; } = radius;

        public double X { get; set; }

        public double Y { get; set; }

        public double Vx { get; set; } = vx;

        public double Vy { get; set; } = vy;

        public byte R { get; } = r;

        public byte G { get; } = g;

        public byte B { get; } = b;

        public bool Paused { get; set; }
    }

    private sealed class DragState
    {
        public bool Active { get; set; }

        public int OrbIndex { get; set; } = -1;

        public double StartPointerX { get; set; }

        public double StartPointerY { get; set; }

        public double StartOrbX { get; set; }

        public double StartOrbY { get; set; }

        public bool Moved { get; set; }

        public void Reset()
        {
            Active = false;
            OrbIndex = -1;
            StartPointerX = 0;
            StartPointerY = 0;
            StartOrbX = 0;
            StartOrbY = 0;
            Moved = false;
        }
    }
}

public sealed class VariableAsciiSampleView : UserControl
{
    private const int Columns = 50;
    private const int Rows = 28;
    private const int AsciiFontSize = 14;
    private const int LineHeight = 16;
    private const int TargetRowWidth = 440;
    private const int PanelWidth = 468;
    private const string PropFamilyCss = "Georgia, Palatino, \"Times New Roman\", serif";
    private const string PropFamilyDisplay = "Georgia";
    private const int FieldOversample = 2;
    private const int FieldCols = Columns * FieldOversample;
    private const int FieldRows = Rows * FieldOversample;
    private const int CanvasWidth = 220;
    private const int CanvasHeight = 224;
    private const double FieldScaleX = (double)FieldCols / CanvasWidth;
    private const double FieldScaleY = (double)FieldRows / CanvasHeight;
    private const double TargetCellWidth = (double)TargetRowWidth / Columns;
    private const int ParticleCount = 120;
    private const int SpriteRadius = 14;
    private const int AttractorRadius = 12;
    private const int LargeAttractorRadius = 30;
    private const double AttractorForceNear = 0.22;
    private const double AttractorForceFar = 0.05;
    private const double FieldDecay = 0.82;
    private const string Charset = " .,:;!+-=*#@%&abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string MonoRamp = " .`-_:,;^=+/|)\\!?0oOQ#%@";
    private static readonly int[] Weights = [300, 500, 800];
    private static readonly bool[] ItalicOptions = [false, true];
    private static readonly SolidColorBrush[] PropAlphaBrushes = CreatePropAlphaBrushes();

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly UiRenderScheduler _renderScheduler;
    private readonly Stopwatch _clock = new();
    private readonly Image _sourceImage;
    private readonly WriteableBitmap _sourceBitmap = new(CanvasWidth, CanvasHeight);
    private readonly Stream _sourcePixelStream;
    private readonly byte[] _sourcePixels = new byte[CanvasWidth * CanvasHeight * 4];
    private readonly SKBitmap _sourceSurface = new(CanvasWidth, CanvasHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
    private readonly SKCanvas _sourceSurfaceCanvas;
    private readonly Dictionary<int, SKImage> _spriteCache = [];
    private readonly SKPaint _sourceFadePaint = new()
    {
        Color = new SKColor(0, 0, 0, 46),
        BlendMode = SKBlendMode.SrcOver,
        IsAntialias = true,
    };
    private readonly SKPaint _sourceSpritePaint = new()
    {
        BlendMode = SKBlendMode.Plus,
        IsAntialias = true,
    };
    private readonly StackPanel _propRowsHost = new()
    {
        Spacing = 0,
        Width = TargetRowWidth,
        Height = Rows * LineHeight,
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    private readonly StackPanel _monoRowsHost = new()
    {
        Spacing = 0,
        Width = TargetRowWidth,
        Height = Rows * LineHeight,
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    private readonly List<TextBlock> _propRowPool = [];
    private readonly List<TextBlock> _monoRowPool = [];
    private readonly List<Particle> _particles = [];
    private readonly float[] _brightnessField = new float[FieldCols * FieldRows];
    private readonly BrightnessEntry[] _brightnessLookup = new BrightnessEntry[256];
    private readonly FieldStamp _particleFieldStamp;
    private readonly FieldStamp _largeAttractorFieldStamp;
    private readonly FieldStamp _smallAttractorFieldStamp;
    private double _attractor1X;
    private double _attractor1Y;
    private double _attractor2X;
    private double _attractor2Y;

    public VariableAsciiSampleView()
    {
        _renderScheduler = new UiRenderScheduler(DispatcherQueue, Render);
        _sourceSurfaceCanvas = new SKCanvas(_sourceSurface);
        _sourceSurfaceCanvas.Clear(SKColors.Black);
        _sourcePixelStream = _sourceBitmap.PixelBuffer.AsStream();
        _particleFieldStamp = CreateFieldStamp(SpriteRadius);
        _largeAttractorFieldStamp = CreateFieldStamp(LargeAttractorRadius);
        _smallAttractorFieldStamp = CreateFieldStamp(AttractorRadius);
        _sourceImage = new Image
        {
            Source = _sourceBitmap,
            Width = TargetRowWidth,
            Height = Rows * LineHeight,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        SeedParticles();
        BuildBrightnessLookup();
        BuildRowPools();
        UpdateSourceBitmap();

        var stack = new StackPanel
        {
            Spacing = 28,
            Padding = new Thickness(20, 32, 20, 60),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(new TextBlock
        {
            Text = "Variable Typographic ASCII",
            FontFamily = new FontFamily("Helvetica Neue"),
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = SampleTheme.Brush(230, 255, 255, 255),
            HorizontalTextAlignment = TextAlignment.Center,
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Proportional font (Georgia) rendered at 3 font-weights × normal/italic — each variant measured by pretext for precise width. A shared particle-and-attractor brightness field drives all three panels, then characters are chosen by brightness AND width to preserve the shape in proportional type.",
            FontFamily = new FontFamily("Helvetica Neue"),
            FontSize = 13,
            Foreground = SampleTheme.Brush(102, 255, 255, 255),
            MaxWidth = 720,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        });

        var grid = new Grid
        {
            ColumnSpacing = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        for (var i = 0; i < 3; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        }

        var sourcePanel = BuildAsciiPanel("SOURCE FIELD", new Border
        {
            Width = TargetRowWidth,
            Height = Rows * LineHeight,
            Background = SampleTheme.Brush(255, 0, 0, 0),
            Child = _sourceImage,
        }, PanelWidth);
        var propPanel = BuildAsciiPanel("PROPORTIONAL × 3 WEIGHTS × ITALIC", _propRowsHost, PanelWidth);
        var monoPanel = BuildAsciiPanel("MONOSPACE × SINGLE WEIGHT", _monoRowsHost, PanelWidth);
        grid.Children.Add(sourcePanel);
        Grid.SetColumn(propPanel, 1);
        grid.Children.Add(propPanel);
        Grid.SetColumn(monoPanel, 2);
        grid.Children.Add(monoPanel);
        stack.Children.Add(grid);

        Content = new Grid
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops =
                {
                    new GradientStop { Offset = 0, Color = ColorHelper.FromArgb(255, 10, 10, 18) },
                    new GradientStop { Offset = 1, Color = ColorHelper.FromArgb(255, 6, 6, 10) },
                },
            },
            Children =
            {
                new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = stack,
                },
            },
        };

        Loaded += (_, _) =>
        {
            _clock.Restart();
            AdvanceParticles(0);
            Render();
            _timer.Start();
        };
        Unloaded += (_, _) => _timer.Stop();
        _timer.Tick += (_, _) =>
        {
            AdvanceParticles(_clock.Elapsed.TotalMilliseconds);
            _renderScheduler.Schedule();
        };
    }

    private static Border BuildAsciiPanel(string title, UIElement content, double width)
    {
        var stack = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = new FontFamily("Helvetica Neue"),
            FontSize = 10,
            CharacterSpacing = 150,
            Foreground = SampleTheme.Brush(76, 255, 255, 255),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        });
        stack.Children.Add(new Border
        {
            Width = width,
            BorderBrush = SampleTheme.Brush(24, 255, 255, 255),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Background = SampleTheme.Brush(102, 0, 0, 0),
            Child = content,
        });
        return new Border
        {
            Background = null,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = stack,
        };
    }

    private void SeedParticles()
    {
        for (var index = 0; index < ParticleCount; index++)
        {
            var angle = Random.Shared.NextDouble() * Math.PI * 2;
            var radius = Random.Shared.NextDouble() * 40 + 20;
            _particles.Add(new Particle(
                CanvasWidth / 2 + Math.Cos(angle) * radius,
                CanvasHeight / 2 + Math.Sin(angle) * radius,
                (Random.Shared.NextDouble() - 0.5) * 0.8,
                (Random.Shared.NextDouble() - 0.5) * 0.8));
        }
    }

    private void BuildBrightnessLookup()
    {
        var palette = new List<PaletteEntry>();
        foreach (var italic in ItalicOptions)
        {
            foreach (var weight in Weights)
            {
                var font = $"{(italic ? "italic " : string.Empty)}{weight} {AsciiFontSize}px {PropFamilyCss}";
                foreach (var ch in Charset)
                {
                    if (ch == ' ')
                    {
                        continue;
                    }

                    var width = MeasureWidth(ch, font);
                    if (width <= 0)
                    {
                        continue;
                    }

                    var brightness = EstimateBrightness(ch, weight, italic);
                    palette.Add(new PaletteEntry(ch, weight, italic, width, brightness));
                }
            }
        }

        var maxBrightness = palette.Count == 0 ? 1d : palette.Max(entry => entry.Brightness);
        if (maxBrightness > 0)
        {
            for (var index = 0; index < palette.Count; index++)
            {
                palette[index] = palette[index] with { Brightness = palette[index].Brightness / maxBrightness };
            }
        }

        palette.Sort((a, b) => a.Brightness.CompareTo(b.Brightness));
        for (var brightnessByte = 0; brightnessByte < 256; brightnessByte++)
        {
            var brightness = brightnessByte / 255d;
            var monoChar = MonoRamp[Math.Min(MonoRamp.Length - 1, (int)(brightness * MonoRamp.Length))];

            if (brightness < 0.03)
            {
                _brightnessLookup[brightnessByte] = new BrightnessEntry(monoChar, null);
                continue;
            }

            var best = FindBestPaletteEntry(palette, brightness);
            var alphaLevel = Math.Max(1, Math.Min(10, (int)Math.Round(brightness * 10)));
            _brightnessLookup[brightnessByte] = new BrightnessEntry(monoChar, new GlyphVariant(best.Character, best.Weight, best.Italic, alphaLevel));
        }
    }

    private void BuildRowPools()
    {
        for (var row = 0; row < Rows; row++)
        {
            var propRow = new TextBlock
            {
                FontFamily = new FontFamily(PropFamilyDisplay),
                FontSize = AsciiFontSize,
                Foreground = PropAlphaBrushes[10],
                LineHeight = LineHeight,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _propRowsHost.Children.Add(propRow);
            _propRowPool.Add(propRow);

            var monoRow = new TextBlock
            {
                FontFamily = new FontFamily("Courier New"),
                FontSize = AsciiFontSize,
                Foreground = SampleTheme.Brush(179, 130, 155, 210),
                TextWrapping = TextWrapping.NoWrap,
                LineHeight = LineHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _monoRowsHost.Children.Add(monoRow);
            _monoRowPool.Add(monoRow);
        }
    }

    private static SolidColorBrush[] CreatePropAlphaBrushes()
    {
        var brushes = new SolidColorBrush[11];
        for (var level = 1; level <= 10; level++)
        {
            brushes[level] = SampleTheme.Brush((byte)Math.Round(level * 25.5), 196, 163, 90);
        }

        return brushes;
    }

    private static PaletteEntry FindBestPaletteEntry(IReadOnlyList<PaletteEntry> palette, double targetBrightness)
    {
        var lo = 0;
        var hi = palette.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) >> 1;
            if (palette[mid].Brightness < targetBrightness)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        var best = palette[lo];
        var bestScore = double.MaxValue;
        var start = Math.Max(0, lo - 15);
        var end = Math.Min(palette.Count, lo + 15);
        for (var index = start; index < end; index++)
        {
            var entry = palette[index];
            var brightnessError = Math.Abs(entry.Brightness - targetBrightness) * 2.5;
            var widthError = Math.Abs(entry.Width - TargetCellWidth) / TargetCellWidth;
            var score = brightnessError + widthError;
            if (score < bestScore)
            {
                best = entry;
                bestScore = score;
            }
        }

        return best;
    }

    private static double MeasureWidth(char ch, string font)
    {
        var prepared = PretextLayout.PrepareWithSegments(ch.ToString(), font);
        return prepared.Widths.Count > 0 ? prepared.Widths[0] : 0;
    }

    private static double EstimateBrightness(char ch, int weight, bool italic)
    {
        const int size = 28;
        using var bitmap = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        using var typeface = CreateTypeface(weight, italic);
        using var font = new SKFont(typeface, size)
        {
            Subpixel = true,
        };
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
        };

        var metrics = font.Metrics;
        var baseline = size / 2f - ((metrics.Ascent + metrics.Descent) / 2f);
        canvas.DrawText(ch.ToString(), 1, baseline, SKTextAlign.Left, font, paint);

        double sum = 0;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                sum += bitmap.GetPixel(x, y).Alpha;
            }
        }

        return sum / (255d * size * size);
    }

    private static SKTypeface CreateTypeface(int weight, bool italic)
    {
        var style = new SKFontStyle(weight, (int)SKFontStyleWidth.Normal, italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        return SKTypeface.FromFamilyName(PropFamilyDisplay, style) ?? SKTypeface.Default;
    }

    private void AdvanceParticles(double nowMs)
    {
        _attractor1X = Math.Cos(nowMs * 0.0007) * CanvasWidth * 0.25 + CanvasWidth / 2d;
        _attractor1Y = Math.Sin(nowMs * 0.0011) * CanvasHeight * 0.3 + CanvasHeight / 2d;
        _attractor2X = Math.Cos(nowMs * 0.0013 + Math.PI) * CanvasWidth * 0.2 + CanvasWidth / 2d;
        _attractor2Y = Math.Sin(nowMs * 0.0009 + Math.PI) * CanvasHeight * 0.25 + CanvasHeight / 2d;

        for (var index = 0; index < _particles.Count; index++)
        {
            var particle = _particles[index];
            var d1x = _attractor1X - particle.X;
            var d1y = _attractor1Y - particle.Y;
            var d2x = _attractor2X - particle.X;
            var d2y = _attractor2Y - particle.Y;
            var dist1 = d1x * d1x + d1y * d1y;
            var dist2 = d2x * d2x + d2y * d2y;
            var ax = dist1 < dist2 ? d1x : d2x;
            var ay = dist1 < dist2 ? d1y : d2y;
            var dist = Math.Sqrt(Math.Min(dist1, dist2)) + 1;
            var force = dist1 < dist2 ? AttractorForceNear : AttractorForceFar;

            particle.Vx += ax / dist * force;
            particle.Vy += ay / dist * force;
            particle.Vx += (Random.Shared.NextDouble() - 0.5) * 0.25;
            particle.Vy += (Random.Shared.NextDouble() - 0.5) * 0.25;
            particle.Vx *= 0.97;
            particle.Vy *= 0.97;
            particle.X += particle.Vx;
            particle.Y += particle.Vy;

            if (particle.X < -SpriteRadius) particle.X += CanvasWidth + SpriteRadius * 2;
            if (particle.X > CanvasWidth + SpriteRadius) particle.X -= CanvasWidth + SpriteRadius * 2;
            if (particle.Y < -SpriteRadius) particle.Y += CanvasHeight + SpriteRadius * 2;
            if (particle.Y > CanvasHeight + SpriteRadius) particle.Y -= CanvasHeight + SpriteRadius * 2;
        }
    }

    private void Render()
    {
        for (var index = 0; index < _brightnessField.Length; index++)
        {
            _brightnessField[index] *= (float)FieldDecay;
        }

        RenderSourceField();

        foreach (var particle in _particles)
        {
            SplatFieldStamp(particle.X, particle.Y, _particleFieldStamp);
        }
        SplatFieldStamp(_attractor1X, _attractor1Y, _largeAttractorFieldStamp);
        SplatFieldStamp(_attractor2X, _attractor2Y, _smallAttractorFieldStamp);

        var mono = new StringBuilder(Columns);
        var prop = new StringBuilder(Columns);
        for (var row = 0; row < Rows; row++)
        {
            mono.Clear();
            prop.Clear();
            var propRow = _propRowPool[row];
            propRow.Inlines.Clear();
            GlyphVariant? currentVariant = null;
            var fieldRowStart = row * FieldOversample * FieldCols;
            for (var col = 0; col < Columns; col++)
            {
                var fieldColStart = col * FieldOversample;
                float brightness = 0;
                for (var sampleY = 0; sampleY < FieldOversample; sampleY++)
                {
                    var sampleRowOffset = fieldRowStart + sampleY * FieldCols + fieldColStart;
                    for (var sampleX = 0; sampleX < FieldOversample; sampleX++)
                    {
                        brightness += _brightnessField[sampleRowOffset + sampleX];
                    }
                }

                var brightnessByte = Math.Min(255, (int)((brightness / (FieldOversample * FieldOversample)) * 255));
                var entry = _brightnessLookup[brightnessByte];
                mono.Append(entry.MonoChar);

                if (entry.PropVariant != currentVariant)
                {
                    FlushPropRun(propRow, prop, currentVariant);
                    currentVariant = entry.PropVariant;
                }

                prop.Append(entry.PropVariant?.Character ?? ' ');
            }

            FlushPropRun(propRow, prop, currentVariant);
            _monoRowPool[row].Text = mono.ToString();
        }
    }

    private void FlushPropRun(TextBlock rowBlock, StringBuilder buffer, GlyphVariant? variant)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        var run = new Run
        {
            Text = buffer.ToString(),
        };
        if (variant is GlyphVariant glyph)
        {
            run.FontWeight = glyph.Weight switch
            {
                300 => FontWeights.Light,
                500 => FontWeights.Medium,
                _ => FontWeights.ExtraBold,
            };
            run.FontStyle = glyph.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
            run.Foreground = PropAlphaBrushes[glyph.AlphaLevel];
        }

        rowBlock.Inlines.Add(run);
        buffer.Clear();
    }

    private void RenderSourceField()
    {
        _sourceSurfaceCanvas.DrawRect(SKRect.Create(CanvasWidth, CanvasHeight), _sourceFadePaint);
        var particleSprite = GetSpriteImage(SpriteRadius);
        for (var index = 0; index < _particles.Count; index++)
        {
            var particle = _particles[index];
            _sourceSurfaceCanvas.DrawImage(particleSprite, (float)(particle.X - SpriteRadius), (float)(particle.Y - SpriteRadius), _sourceSpritePaint);
        }

        _sourceSurfaceCanvas.DrawImage(GetSpriteImage(LargeAttractorRadius), (float)(_attractor1X - LargeAttractorRadius), (float)(_attractor1Y - LargeAttractorRadius), _sourceSpritePaint);
        _sourceSurfaceCanvas.DrawImage(GetSpriteImage(AttractorRadius), (float)(_attractor2X - AttractorRadius), (float)(_attractor2Y - AttractorRadius), _sourceSpritePaint);
        UpdateSourceBitmap();
    }

    private SKImage GetSpriteImage(int radius)
    {
        if (_spriteCache.TryGetValue(radius, out var image))
        {
            return image;
        }

        using var bitmap = new SKBitmap(radius * 2, radius * 2, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(radius, radius),
                radius,
                [new SKColor(255, 255, 255, 115), new SKColor(255, 255, 255, 38), new SKColor(255, 255, 255, 0)],
                [0f, 0.35f, 1f],
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(SKRect.Create(radius * 2, radius * 2), paint);

        image = SKImage.FromBitmap(bitmap);
        _spriteCache[radius] = image;
        return image;
    }

    private void UpdateSourceBitmap()
    {
        Marshal.Copy(_sourceSurface.GetPixels(), _sourcePixels, 0, _sourcePixels.Length);
        _sourcePixelStream.Position = 0;
        _sourcePixelStream.Write(_sourcePixels, 0, _sourcePixels.Length);
        _sourceBitmap.Invalidate();
    }

    private static float SpriteAlphaAt(double normalizedDistance)
    {
        if (normalizedDistance >= 1)
        {
            return 0;
        }

        if (normalizedDistance <= 0.35)
        {
            return 0.45f + (float)((0.15 - 0.45) * (normalizedDistance / 0.35));
        }

        return 0.15f * (float)(1 - ((normalizedDistance - 0.35) / 0.65));
    }

    private static FieldStamp CreateFieldStamp(int radiusPx)
    {
        var fieldRadiusX = radiusPx * FieldScaleX;
        var fieldRadiusY = radiusPx * FieldScaleY;
        var radiusX = (int)Math.Ceiling(fieldRadiusX);
        var radiusY = (int)Math.Ceiling(fieldRadiusY);
        var sizeX = radiusX * 2 + 1;
        var sizeY = radiusY * 2 + 1;
        var values = new float[sizeX * sizeY];

        for (var y = -radiusY; y <= radiusY; y++)
        {
            for (var x = -radiusX; x <= radiusX; x++)
            {
                var normalizedDistance = Math.Sqrt(Math.Pow(x / fieldRadiusX, 2) + Math.Pow(y / fieldRadiusY, 2));
                values[(y + radiusY) * sizeX + x + radiusX] = SpriteAlphaAt(normalizedDistance);
            }
        }

        return new FieldStamp(radiusX, radiusY, sizeX, sizeY, values);
    }

    private void SplatFieldStamp(double centerX, double centerY, FieldStamp stamp)
    {
        var gridCenterX = (int)Math.Round(centerX * FieldScaleX);
        var gridCenterY = (int)Math.Round(centerY * FieldScaleY);
        for (var y = -stamp.RadiusY; y <= stamp.RadiusY; y++)
        {
            var gridY = gridCenterY + y;
            if (gridY < 0 || gridY >= FieldRows)
            {
                continue;
            }

            var fieldRowOffset = gridY * FieldCols;
            var stampRowOffset = (y + stamp.RadiusY) * stamp.SizeX;
            for (var x = -stamp.RadiusX; x <= stamp.RadiusX; x++)
            {
                var gridX = gridCenterX + x;
                if (gridX < 0 || gridX >= FieldCols)
                {
                    continue;
                }

                var stampValue = stamp.Values[stampRowOffset + x + stamp.RadiusX];
                if (stampValue == 0)
                {
                    continue;
                }

                var fieldIndex = fieldRowOffset + gridX;
                _brightnessField[fieldIndex] = Math.Min(1, _brightnessField[fieldIndex] + stampValue);
            }
        }
    }

    private readonly record struct PaletteEntry(char Character, int Weight, bool Italic, double Width, double Brightness);

    private readonly record struct GlyphVariant(char Character, int Weight, bool Italic, int AlphaLevel);

    private readonly record struct BrightnessEntry(char MonoChar, GlyphVariant? PropVariant);

    private readonly record struct FieldStamp(int RadiusX, int RadiusY, int SizeX, int SizeY, float[] Values);

    private sealed class Particle(double x, double y, double vx, double vy)
    {
        public double X { get; set; } = x;

        public double Y { get; set; } = y;

        public double Vx { get; set; } = vx;

        public double Vy { get; set; } = vy;
    }
}

internal static class SharedText
{
    public const string LongExcerpt =
        "You can see the future first in San Francisco. Over the past year, the talk of the town has shifted from $10 billion compute clusters to $100 billion clusters to trillion-dollar clusters. Every six months another zero is added to the boardroom plans. Behind the scenes, there’s a fierce scramble to secure every power contract still available for the rest of the decade, every voltage transformer that can possibly be procured. American industrial might is being reorganized around AI infrastructure. Everyone is now talking about AI, but few have the faintest glimmer of what is about to hit them. There are perhaps a few hundred people, most of them in San Francisco and the AI labs, that have situational awareness. Whether these people are right about the next few years remains to be seen, but they are the ones building this technology. If they are seeing the future even close to correctly, we are in for a wild ride. The pace of deep learning progress in the last decade has simply been extraordinary. Over and over again, skeptics have claimed deep learning won’t be able to do X and have been quickly proven wrong. If there’s one lesson we’ve learned, it’s that you should never bet against the trendlines. We are rapidly racing through the orders of magnitude, and the numbers suggest we should expect another GPT-2-to-GPT-4 sized qualitative jump on top of GPT-4 this decade.";

    public const string EditorialExcerpt =
        "Manual text layout becomes interesting once the page stops being a rectangle. The moment you place a pull quote, a floating image, or an animated ornament into the reading space, the line boxes need to respond to geometry the same frame it changes. That is awkward if your system can only ask a UI framework how tall the text ended up after layout. It becomes much more tractable if you can prepare the text once and then request the next line for whatever slot remains. That is the core idea this sample is trying to dogfood. The orbs move, the pull quote stays fixed, and the paragraphs route around both without a measurement pass over the live text tree. That is not a browser-perfect magazine engine, but it is enough to show why the API surface matters. Once the line walker is reusable, you can build shrinkwrap, editorial spreads, masonry, notes with chips, and any other layout where text is a participant in the geometry instead of an opaque block.";
}
