using System.Collections.ObjectModel;

namespace Pretext.LayoutFramework;

public readonly record struct LayoutPlacement(int Index, string? Key, LayoutRect Bounds);

public sealed class SolvedLayout
{
    private readonly LayoutPlacement[] _placements;

    public SolvedLayout(
        LayoutFingerprint preparedFingerprint,
        LayoutConstraints constraints,
        LayoutSize extent,
        IEnumerable<LayoutPlacement> placements,
        VerticalOcclusionIndex? verticalOcclusion = null,
        object? derivedState = null)
    {
        ArgumentNullException.ThrowIfNull(placements);

        PreparedFingerprint = preparedFingerprint;
        Constraints = constraints;
        Extent = extent;
        _placements = placements.ToArray();
        Placements = Array.AsReadOnly(_placements);
        VerticalOcclusion = verticalOcclusion;
        DerivedState = derivedState;
    }

    public LayoutFingerprint PreparedFingerprint { get; }

    public LayoutConstraints Constraints { get; }

    public LayoutSize Extent { get; }

    public IReadOnlyList<LayoutPlacement> Placements { get; }

    public VerticalOcclusionIndex? VerticalOcclusion { get; }

    public object? DerivedState { get; }

    public bool TryGetPlacement(string key, out LayoutPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(key);

        for (var index = 0; index < _placements.Length; index++)
        {
            if (string.Equals(_placements[index].Key, key, StringComparison.Ordinal))
            {
                placement = _placements[index];
                return true;
            }
        }

        placement = default;
        return false;
    }

    public bool TryQueryViewport(LayoutViewport viewport, out VerticalOcclusionRange range)
    {
        if (VerticalOcclusion is null)
        {
            range = default;
            return false;
        }

        return VerticalOcclusion.TryQuery(viewport.Top, viewport.Bottom, out range);
    }

    public bool TryQueryViewportSelection(LayoutViewport viewport, out VerticalOcclusionSelection selection)
    {
        if (VerticalOcclusion is null)
        {
            selection = default;
            return false;
        }

        return VerticalOcclusion.TryQuerySelection(viewport.Top, viewport.Bottom, out selection);
    }
}
