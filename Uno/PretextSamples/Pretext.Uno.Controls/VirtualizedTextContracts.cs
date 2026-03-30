using Microsoft.UI.Xaml.Media;
using Pretext.Uno;

namespace Pretext.Uno.Controls;

public readonly record struct VirtualizedViewportStats(
    int VisibleItemCount,
    int StartIndex,
    int EndIndexExclusive,
    int StartBandIndex,
    int EndBandIndexExclusive);

public readonly record struct WrapTileTemplate(
    string Text,
    double BodyWidth,
    double BodyHeight,
    double TileWidth,
    double TileHeight,
    Brush Background,
    Brush BorderBrush);

public sealed class WrapTileSource
{
    public WrapTileSource(IReadOnlyList<WrapTileTemplate> templates, int[] templateIndices)
    {
        Templates = templates;
        TemplateIndices = templateIndices;
    }

    public IReadOnlyList<WrapTileTemplate> Templates { get; }

    public int[] TemplateIndices { get; }

    public int Count => TemplateIndices.Length;
}

public readonly record struct ListTileTemplate(string Category, string BodyText, PreparedText Prepared);

public sealed class ListTileSource
{
    public ListTileSource(IReadOnlyList<ListTileTemplate> templates, int[] templateIndices)
    {
        Templates = templates;
        TemplateIndices = templateIndices;
    }

    public IReadOnlyList<ListTileTemplate> Templates { get; }

    public int[] TemplateIndices { get; }

    public int Count => TemplateIndices.Length;

    public string GetTitle(int index)
    {
        var template = Templates[TemplateIndices[index]];
        return $"{template.Category} {index + 1:N0}";
    }
}
