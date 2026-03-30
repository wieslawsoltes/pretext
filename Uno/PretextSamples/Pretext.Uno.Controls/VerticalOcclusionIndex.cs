namespace Pretext.Uno.Controls;

public readonly record struct VerticalBand(int StartIndex, int EndIndexExclusive, double Top, double Bottom);

public readonly record struct VerticalOcclusionRange(
    int StartIndex,
    int EndIndexExclusive,
    int StartBandIndex,
    int EndBandIndexExclusive);

public sealed class VerticalOcclusionIndex
{
    private readonly IReadOnlyList<VerticalBand> _bands;

    public VerticalOcclusionIndex(IReadOnlyList<VerticalBand> bands)
    {
        _bands = bands;
    }

    public int BandCount => _bands.Count;

    public bool TryQuery(double top, double bottom, out VerticalOcclusionRange range)
    {
        range = default;
        if (_bands.Count == 0 || bottom < 0)
        {
            return false;
        }

        var firstBand = FindFirstBandEndingAfter(top);
        if (firstBand >= _bands.Count)
        {
            return false;
        }

        var endBandExclusive = FindFirstBandStartingAfter(bottom);
        if (endBandExclusive <= firstBand)
        {
            return false;
        }

        range = new VerticalOcclusionRange(
            _bands[firstBand].StartIndex,
            _bands[endBandExclusive - 1].EndIndexExclusive,
            firstBand,
            endBandExclusive);
        return true;
    }

    private int FindFirstBandEndingAfter(double top)
    {
        var lo = 0;
        var hi = _bands.Count;
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
        var hi = _bands.Count;
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
