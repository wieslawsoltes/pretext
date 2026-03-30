namespace Pretext.LayoutFramework;

public readonly record struct VerticalBand(
    int StartIndex,
    int EndIndexExclusive,
    double Top,
    double Bottom,
    int[]? ItemIndices = null);

public readonly record struct VerticalOcclusionRange(
    int StartIndex,
    int EndIndexExclusive,
    int StartBandIndex,
    int EndBandIndexExclusive);

public readonly record struct VerticalOcclusionSelection(
    int StartIndex,
    int EndIndexExclusive,
    int StartBandIndex,
    int EndBandIndexExclusive,
    int[]? ItemIndices = null)
{
    public int VisibleItemCount => ItemIndices?.Length ?? Math.Max(0, EndIndexExclusive - StartIndex);

    public VerticalOcclusionRange ToRange()
    {
        return new VerticalOcclusionRange(StartIndex, EndIndexExclusive, StartBandIndex, EndBandIndexExclusive);
    }
}

public sealed class VerticalOcclusionIndex
{
    private readonly VerticalBand[] _bands;

    public VerticalOcclusionIndex(IReadOnlyList<VerticalBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);
        _bands = bands.ToArray();
    }

    public int BandCount => _bands.Length;

    public IReadOnlyList<VerticalBand> Bands => _bands;

    public bool TryQuery(double top, double bottom, out VerticalOcclusionRange range)
    {
        range = default;
        if (!TryQuerySelection(top, bottom, out var selection))
        {
            return false;
        }

        range = selection.ToRange();
        return true;
    }

    public bool TryQuerySelection(double top, double bottom, out VerticalOcclusionSelection selection)
    {
        selection = default;
        if (_bands.Length == 0 || bottom < 0)
        {
            return false;
        }

        var firstBand = FindFirstBandEndingAfter(top);
        if (firstBand >= _bands.Length)
        {
            return false;
        }

        var endBandExclusive = FindFirstBandStartingAfter(bottom);
        if (endBandExclusive <= firstBand)
        {
            return false;
        }

        var usesExplicitIndices = false;
        for (var bandIndex = firstBand; bandIndex < endBandExclusive; bandIndex++)
        {
            if (_bands[bandIndex].ItemIndices is { Length: > 0 })
            {
                usesExplicitIndices = true;
                break;
            }
        }

        if (!usesExplicitIndices)
        {
            selection = new VerticalOcclusionSelection(
                _bands[firstBand].StartIndex,
                _bands[endBandExclusive - 1].EndIndexExclusive,
                firstBand,
                endBandExclusive);
            return true;
        }

        var unique = new HashSet<int>();
        var ordered = new List<int>();
        var startIndex = int.MaxValue;
        var endIndexExclusive = 0;

        for (var bandIndex = firstBand; bandIndex < endBandExclusive; bandIndex++)
        {
            var band = _bands[bandIndex];
            if (band.ItemIndices is { Length: > 0 })
            {
                for (var i = 0; i < band.ItemIndices.Length; i++)
                {
                    var itemIndex = band.ItemIndices[i];
                    if (!unique.Add(itemIndex))
                    {
                        continue;
                    }

                    ordered.Add(itemIndex);
                    if (itemIndex < startIndex)
                    {
                        startIndex = itemIndex;
                    }

                    if (itemIndex + 1 > endIndexExclusive)
                    {
                        endIndexExclusive = itemIndex + 1;
                    }
                }

                continue;
            }

            for (var itemIndex = band.StartIndex; itemIndex < band.EndIndexExclusive; itemIndex++)
            {
                if (!unique.Add(itemIndex))
                {
                    continue;
                }

                ordered.Add(itemIndex);
                if (itemIndex < startIndex)
                {
                    startIndex = itemIndex;
                }

                if (itemIndex + 1 > endIndexExclusive)
                {
                    endIndexExclusive = itemIndex + 1;
                }
            }
        }

        if (ordered.Count == 0)
        {
            return false;
        }

        ordered.Sort();
        selection = new VerticalOcclusionSelection(
            startIndex,
            endIndexExclusive,
            firstBand,
            endBandExclusive,
            [.. ordered]);
        return true;
    }

    private int FindFirstBandEndingAfter(double top)
    {
        var lo = 0;
        var hi = _bands.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_bands[mid].Bottom < top)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    private int FindFirstBandStartingAfter(double bottom)
    {
        var lo = 0;
        var hi = _bands.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_bands[mid].Top <= bottom)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}
