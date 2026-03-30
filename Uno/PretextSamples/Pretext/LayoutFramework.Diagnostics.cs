namespace Pretext.LayoutFramework;

public enum LayoutPassKind
{
    Measure,
    Arrange,
}

public readonly record struct LayoutDiagnosticsSnapshot(
    int PrepareCount,
    int PrepareCacheHits,
    int PrepareCacheMisses,
    int MeasureAccessCount,
    int MeasureCacheHits,
    int MeasureCacheMisses,
    int MeasureSolveCount,
    int ArrangeAccessCount,
    int ArrangeCacheHits,
    int ArrangeCacheMisses,
    int ArrangeSolveCount,
    int ViewportUpdateCount,
    LayoutViewport? LastViewport,
    int RealizationUpdateCount,
    VerticalOcclusionRange? LastRealizedRange,
    int LastRealizedElementCount)
{
    public int SolveAccessCount => MeasureAccessCount + ArrangeAccessCount;

    public int SolveCacheHits => MeasureCacheHits + ArrangeCacheHits;

    public int SolveCacheMisses => MeasureCacheMisses + ArrangeCacheMisses;

    public int SolveCount => MeasureSolveCount + ArrangeSolveCount;

    public double PrepareCacheHitRate => PrepareCount == 0 ? 0d : (double)PrepareCacheHits / PrepareCount;

    public double MeasureCacheHitRate => MeasureAccessCount == 0 ? 0d : (double)MeasureCacheHits / MeasureAccessCount;

    public double ArrangeCacheHitRate => ArrangeAccessCount == 0 ? 0d : (double)ArrangeCacheHits / ArrangeAccessCount;

    public double SolveCacheHitRate => SolveAccessCount == 0 ? 0d : (double)SolveCacheHits / SolveAccessCount;
}

public sealed class LayoutDiagnosticsTracker
{
    private int _prepareCount;
    private int _prepareCacheHits;
    private int _prepareCacheMisses;
    private int _measureAccessCount;
    private int _measureCacheHits;
    private int _measureCacheMisses;
    private int _measureSolveCount;
    private int _arrangeAccessCount;
    private int _arrangeCacheHits;
    private int _arrangeCacheMisses;
    private int _arrangeSolveCount;
    private int _viewportUpdateCount;
    private bool _viewportInitialized;
    private LayoutViewport? _lastViewport;
    private int _realizationUpdateCount;
    private bool _realizationInitialized;
    private VerticalOcclusionRange? _lastRealizedRange;
    private int _lastRealizedElementCount;

    public LayoutDiagnosticsSnapshot Snapshot => new(
        _prepareCount,
        _prepareCacheHits,
        _prepareCacheMisses,
        _measureAccessCount,
        _measureCacheHits,
        _measureCacheMisses,
        _measureSolveCount,
        _arrangeAccessCount,
        _arrangeCacheHits,
        _arrangeCacheMisses,
        _arrangeSolveCount,
        _viewportUpdateCount,
        _lastViewport,
        _realizationUpdateCount,
        _lastRealizedRange,
        _lastRealizedElementCount);

    public void RecordPrepare(bool cacheHit)
    {
        _prepareCount++;
        if (cacheHit)
        {
            _prepareCacheHits++;
        }
        else
        {
            _prepareCacheMisses++;
        }
    }

    public void RecordLayoutAccess(LayoutPassKind pass, bool cacheHit)
    {
        switch (pass)
        {
            case LayoutPassKind.Measure:
                _measureAccessCount++;
                if (cacheHit)
                {
                    _measureCacheHits++;
                }
                else
                {
                    _measureCacheMisses++;
                }

                break;
            case LayoutPassKind.Arrange:
                _arrangeAccessCount++;
                if (cacheHit)
                {
                    _arrangeCacheHits++;
                }
                else
                {
                    _arrangeCacheMisses++;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pass), pass, null);
        }
    }

    public void RecordSolve(LayoutPassKind pass)
    {
        switch (pass)
        {
            case LayoutPassKind.Measure:
                _measureSolveCount++;
                break;
            case LayoutPassKind.Arrange:
                _arrangeSolveCount++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pass), pass, null);
        }
    }

    public void RecordViewportUpdate(LayoutViewport? viewport)
    {
        if (!_viewportInitialized)
        {
            _viewportInitialized = true;
            if (viewport is not null)
            {
                _lastViewport = viewport;
                _viewportUpdateCount++;
            }

            return;
        }

        if (AreEquivalent(_lastViewport, viewport))
        {
            return;
        }

        _lastViewport = viewport;
        _viewportUpdateCount++;
    }

    public void RecordRealization(VerticalOcclusionRange? range, int realizedElementCount)
    {
        if (!_realizationInitialized)
        {
            _realizationInitialized = true;
            _lastRealizedRange = range;
            _lastRealizedElementCount = realizedElementCount;
            _realizationUpdateCount++;
            return;
        }

        if (_lastRealizedRange == range && _lastRealizedElementCount == realizedElementCount)
        {
            return;
        }

        _lastRealizedRange = range;
        _lastRealizedElementCount = realizedElementCount;
        _realizationUpdateCount++;
    }

    private static bool AreEquivalent(LayoutViewport? first, LayoutViewport? second)
    {
        if (first is null && second is null)
        {
            return true;
        }

        if (first is null || second is null)
        {
            return false;
        }

        return first.Value == second.Value;
    }
}
