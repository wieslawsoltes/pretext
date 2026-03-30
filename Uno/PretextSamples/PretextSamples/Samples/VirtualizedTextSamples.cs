using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pretext.Uno;
using Pretext.Uno.Controls;

namespace PretextSamples.Samples;

public sealed class VirtualWrapTilesSampleView : UserControl
{
    private readonly TextBlock _status = SampleUi.CreateBodyText("Preparing 100,000 text tiles…");
    private readonly PretextVirtualizedWrapPanel _wrapPanel = new()
    {
        TileTextBrush = SampleTheme.InkBrush,
    };
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
    private readonly PretextVirtualizedListBox _listBox = new()
    {
        ItemBackgroundBrush = SampleTheme.PanelBrush,
        ItemBorderBrush = SampleTheme.RuleBrush,
        SelectedItemBackgroundBrush = SampleTheme.AccentSoftBrush,
        SelectedItemBorderBrush = SampleTheme.AccentBrush,
        StripeBrush = SampleTheme.RuleBrush,
        SelectedStripeBrush = SampleTheme.AccentBrush,
        TitleBrush = SampleTheme.AccentBrush,
        BodyBrush = SampleTheme.InkBrush,
    };
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
