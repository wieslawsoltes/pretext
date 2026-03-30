using Pretext.Uno;

namespace Pretext.LayoutFramework.Samples;

public interface IPreparedItemsLayoutDefinition
{
    LayoutFingerprint GetLayoutFingerprint();

    PreparedItemsModel Prepare(LayoutFingerprint fingerprint);

    SolvedLayout Solve(PreparedItemsModel prepared, LayoutConstraints constraints);
}

public readonly record struct WrapTileSampleTemplate(
    string Text,
    PreparedTextWithSegments Prepared,
    double BodyWidth,
    double TileWidth,
    double TileHeight,
    int PaletteIndex);

public sealed class WrapItemsSampleDefinition : IPreparedItemsLayoutDefinition
{
    private const string WrapFont = "15px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const double WrapLineHeight = 20;
    private const double WrapMinBodyWidth = 96;
    private const double WrapMaxBodyWidth = 238;
    private const double TilePadding = 14;
    private const double TileBorderThickness = 1;
    private const double SurfacePadding = 18;
    private const double TileGap = 10;

    private static readonly string[] Prefixes =
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

    private static readonly string[] Verbs =
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

    private static readonly string[] Tails =
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

    private readonly WrapTileSampleTemplate[] _templates;
    private readonly int[] _templateIndices;

    public WrapItemsSampleDefinition(IReadOnlyList<WrapTileSampleTemplate> templates, int[] templateIndices)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(templateIndices);

        _templates = templates.ToArray();
        _templateIndices = templateIndices;
    }

    public int Count => _templateIndices.Length;

    public IReadOnlyList<WrapTileSampleTemplate> Templates => _templates;

    public static WrapItemsSampleDefinition CreateDemo(int itemCount = 20_000, int templateCount = 240)
    {
        var templates = new WrapTileSampleTemplate[templateCount];
        for (var index = 0; index < templateCount; index++)
        {
            var text = BuildWrapText(index);
            var prepared = PretextLayout.PrepareWithSegments(text, WrapFont);
            var bodyWidth = Math.Clamp(
                Math.Ceiling(Math.Max(WrapMinBodyWidth, SampleDefinitionTextMetrics.MeasureMaxLineWidth(prepared))),
                WrapMinBodyWidth,
                WrapMaxBodyWidth);
            var metrics = PretextLayout.Layout(prepared, bodyWidth, WrapLineHeight);
            var tileWidth = bodyWidth + (TilePadding + TileBorderThickness) * 2;
            var tileHeight = metrics.Height + (TilePadding + TileBorderThickness) * 2;
            templates[index] = new WrapTileSampleTemplate(text, prepared, bodyWidth, tileWidth, tileHeight, index % 5);
        }

        return new WrapItemsSampleDefinition(templates, BuildTemplateIndices(itemCount, templateCount, 73, 197));
    }

    public WrapTileSampleTemplate GetTemplateForItem(int itemIndex)
    {
        return _templates[_templateIndices[itemIndex]];
    }

    public LayoutFingerprint GetLayoutFingerprint()
    {
        var builder = LayoutFingerprintBuilder.Create();
        builder.Add("wrap-items-sample");
        builder.Add(_templateIndices.Length);
        for (var index = 0; index < _templates.Length; index++)
        {
            builder.Add(_templateIndices.Length > index ? _templateIndices[index] : index);
            builder.Add(_templates[index].Text);
        }

        return builder.ToFingerprint();
    }

    public PreparedItemsModel Prepare(LayoutFingerprint fingerprint)
    {
        var items = new PreparedItemMetrics[_templateIndices.Length];
        var richTextHandles = new PreparedTextWithSegments?[_templateIndices.Length];
        for (var index = 0; index < _templateIndices.Length; index++)
        {
            var templateIndex = _templateIndices[index];
            var template = _templates[templateIndex];
            items[index] = new PreparedItemMetrics(PreparedItemKind.Text, template.TileWidth, template.TileHeight, templateIndex);
            richTextHandles[index] = template.Prepared;
        }

        return new PreparedItemsModel(fingerprint, items, richTextHandles: richTextHandles, derivedState: this);
    }

    public SolvedLayout Solve(PreparedItemsModel prepared, LayoutConstraints constraints)
    {
        var availableWidth = Math.Max(280, constraints.AvailableWidth);
        var innerWidth = Math.Max(160, availableWidth - SurfacePadding * 2);
        var placements = new List<LayoutPlacement>(prepared.Count);
        var bands = new List<VerticalBand>();

        var rowStartIndex = 0;
        var x = 0d;
        var y = SurfacePadding;
        var rowHeight = 0d;

        for (var itemIndex = 0; itemIndex < prepared.Count; itemIndex++)
        {
            var template = GetTemplateForItem(itemIndex);
            if (x > 0 && x + template.TileWidth > innerWidth)
            {
                bands.Add(new VerticalBand(rowStartIndex, itemIndex, y, y + rowHeight));
                y += rowHeight + TileGap;
                x = 0;
                rowHeight = 0;
                rowStartIndex = itemIndex;
            }

            placements.Add(new LayoutPlacement(itemIndex, $"wrap-{itemIndex}", new LayoutRect(SurfacePadding + x, y, template.TileWidth, template.TileHeight)));
            x += template.TileWidth + TileGap;
            rowHeight = Math.Max(rowHeight, template.TileHeight);
        }

        if (prepared.Count > 0)
        {
            bands.Add(new VerticalBand(rowStartIndex, prepared.Count, y, y + rowHeight));
            y += rowHeight;
        }

        return new SolvedLayout(
            prepared.Fingerprint,
            constraints,
            new LayoutSize(availableWidth, y + SurfacePadding),
            placements,
            new VerticalOcclusionIndex(bands),
            this);
    }

    private static string BuildWrapText(int index)
    {
        var prefix = Prefixes[index % Prefixes.Length];
        var verb = Verbs[(index / Prefixes.Length) % Verbs.Length];
        var tail = Tails[(index / (Prefixes.Length * Verbs.Length)) % Tails.Length];

        return (index % 4) switch
        {
            0 => $"{prefix} {verb} {tail}.",
            1 => $"{prefix} {verb}.",
            2 => $"{prefix} {tail}.",
            _ => $"{prefix} {verb} {tail} with exact text math.",
        };
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

public sealed record MasonryCardSampleItem(
    string Text,
    PreparedTextWithSegments Prepared,
    int PaletteIndex);

public sealed class MasonryItemsSampleDefinition : IPreparedItemsLayoutDefinition
{
    private const string CardFont = "15px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const double LineHeight = 22;
    private const double CardBorderThickness = 1;
    private const double CardPadding = 16;
    private const double Gap = 12;
    private const double MaxColumnWidth = 400;
    private const double BucketHeight = 220;

    private static readonly string[] Openers =
    [
        "If your release note reads like a diary,",
        "Most dashboards would calm down if",
        "People call it artificial intelligence, but",
        "By the time a flaky test reaches prod,",
        "Every architecture review secretly asks whether",
        "The fastest feature flag cleanup is usually",
        "A healthy backlog looks suspiciously like",
        "Once the app shell stops guessing heights,",
        "The weirdest performance bug is often",
        "Nobody notices a layout engine until",
    ];

    private static readonly string[] Middles =
    [
        "the layout system already knew the answer",
        "someone is still measuring the live tree",
        "the scroll range is paying for old assumptions",
        "the cache has become the real product surface",
        "virtualization started depending on optimistic math",
        "the row chrome is hiding a text problem",
        "the placeholder is teaching the wrong geometry",
        "the selection model survived more churn than the DOM",
        "an innocent width change invalidated everything",
        "the same card was solved three times before paint",
    ];

    private static readonly string[] Endings =
    [
        "and nobody wrote that down.",
        "which is why the resize path still feels expensive.",
        "so the next fix should probably start with preparation instead of repaint.",
        "but the samples now make that cost visible.",
        "and the viewport ends up debugging the panel.",
        "which is how a simple note card becomes a systems problem.",
        "until the content itself changes.",
        "and that is a better default than hoping the tree can keep up.",
        "before the user even scrolls there.",
        "once the geometry is explicit enough to inspect.",
    ];

    private readonly MasonryCardSampleItem[] _items;

    public MasonryItemsSampleDefinition(IReadOnlyList<MasonryCardSampleItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToArray();
    }

    public int Count => _items.Length;

    public IReadOnlyList<MasonryCardSampleItem> Items => _items;

    public static MasonryItemsSampleDefinition CreateDemo(int itemCount = 1_904)
    {
        var items = new MasonryCardSampleItem[itemCount];
        for (var index = 0; index < itemCount; index++)
        {
            var text = BuildMasonryText(index);
            items[index] = new MasonryCardSampleItem(
                text,
                PretextLayout.PrepareWithSegments(text, CardFont),
                index % 5);
        }

        return new MasonryItemsSampleDefinition(items);
    }

    public MasonryCardSampleItem GetItem(int index)
    {
        return _items[index];
    }

    public LayoutFingerprint GetLayoutFingerprint()
    {
        var builder = LayoutFingerprintBuilder.Create();
        builder.Add("masonry-items-sample");
        builder.Add(_items.Length);
        for (var index = 0; index < _items.Length; index++)
        {
            builder.Add(_items[index].Text);
        }

        return builder.ToFingerprint();
    }

    public PreparedItemsModel Prepare(LayoutFingerprint fingerprint)
    {
        var items = new PreparedItemMetrics[_items.Length];
        var richTextHandles = new PreparedTextWithSegments?[_items.Length];

        for (var index = 0; index < _items.Length; index++)
        {
            var prepared = _items[index].Prepared;
            var probe = PretextLayout.Layout(prepared, 240, LineHeight);
            items[index] = new PreparedItemMetrics(
                PreparedItemKind.Text,
                280,
                probe.Height + (CardPadding + CardBorderThickness) * 2,
                _items[index].PaletteIndex);
            richTextHandles[index] = prepared;
        }

        return new PreparedItemsModel(fingerprint, items, richTextHandles: richTextHandles, derivedState: this);
    }

    public SolvedLayout Solve(PreparedItemsModel prepared, LayoutConstraints constraints)
    {
        var availableWidth = Math.Max(360, constraints.AvailableWidth);
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

        var textWidth = Math.Max(80, columnWidth - (CardPadding + CardBorderThickness) * 2);
        var contentWidth = columnCount * columnWidth + (columnCount - 1) * Gap;
        var offsetLeft = (availableWidth - contentWidth) / 2;
        var columnHeights = Enumerable.Repeat(Gap, columnCount).ToArray();
        var placements = new List<LayoutPlacement>(prepared.Count);

        for (var index = 0; index < prepared.Count; index++)
        {
            var targetColumn = 0;
            for (var column = 1; column < columnCount; column++)
            {
                if (columnHeights[column] < columnHeights[targetColumn])
                {
                    targetColumn = column;
                }
            }

            var richText = prepared.GetRichTextHandleOrDefault(index) ?? throw new InvalidOperationException("Rich text handle is required for masonry samples.");
            var metrics = PretextLayout.Layout(richText, textWidth, LineHeight);
            var totalHeight = metrics.Height + (CardPadding + CardBorderThickness) * 2;
            var x = offsetLeft + targetColumn * (columnWidth + Gap);
            var y = columnHeights[targetColumn];
            columnHeights[targetColumn] += totalHeight + Gap;
            placements.Add(new LayoutPlacement(index, $"masonry-{index}", new LayoutRect(x, y, columnWidth, totalHeight)));
        }

        var contentHeight = prepared.Count == 0 ? Gap * 2 : columnHeights.Max() + Gap;
        return new SolvedLayout(
            prepared.Fingerprint,
            constraints,
            new LayoutSize(availableWidth, contentHeight),
            placements,
            BuildBucketedOcclusionIndex(placements, contentHeight, BucketHeight),
            this);
    }

    private static string BuildMasonryText(int index)
    {
        var opener = Openers[index % Openers.Length];
        var middle = Middles[(index / Openers.Length) % Middles.Length];
        var ending = Endings[(index / (Openers.Length * Middles.Length)) % Endings.Length];

        return (index % 3) switch
        {
            0 => $"{opener} {middle}, {ending}",
            1 => $"{opener} {middle}. {ending}",
            _ => $"{opener} {middle}, and that stays true even when the viewport is moving. {ending}",
        };
    }

    private static VerticalOcclusionIndex BuildBucketedOcclusionIndex(
        IReadOnlyList<LayoutPlacement> placements,
        double contentHeight,
        double bucketHeight)
    {
        if (placements.Count == 0)
        {
            return new VerticalOcclusionIndex([]);
        }

        var bucketCount = Math.Max(1, (int)Math.Ceiling(contentHeight / bucketHeight));
        var buckets = new HashSet<int>[bucketCount];
        for (var i = 0; i < bucketCount; i++)
        {
            buckets[i] = [];
        }

        for (var i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            var firstBucket = Math.Clamp((int)Math.Floor(placement.Bounds.Top / bucketHeight), 0, bucketCount - 1);
            var lastBucket = Math.Clamp((int)Math.Floor(Math.Max(placement.Bounds.Top, placement.Bounds.Bottom - 0.001) / bucketHeight), firstBucket, bucketCount - 1);
            for (var bucketIndex = firstBucket; bucketIndex <= lastBucket; bucketIndex++)
            {
                buckets[bucketIndex].Add(placement.Index);
            }
        }

        var bands = new List<VerticalBand>(bucketCount);
        for (var bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
        {
            if (buckets[bucketIndex].Count == 0)
            {
                continue;
            }

            var indices = buckets[bucketIndex].OrderBy(index => index).ToArray();
            var top = bucketIndex * bucketHeight;
            var bottom = Math.Min(contentHeight, top + bucketHeight);
            bands.Add(new VerticalBand(indices[0], indices[^1] + 1, top, bottom, indices));
        }

        return new VerticalOcclusionIndex(bands);
    }
}
