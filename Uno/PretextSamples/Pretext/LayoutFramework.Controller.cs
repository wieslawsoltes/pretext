namespace Pretext.LayoutFramework;

public sealed class PreparedLayoutController<TPrepared>
    where TPrepared : class
{
    private readonly double _constraintEpsilon;

    public PreparedLayoutController(double constraintEpsilon = 0.05d, LayoutDiagnosticsTracker? diagnostics = null)
    {
        _constraintEpsilon = constraintEpsilon;
        Diagnostics = diagnostics ?? new LayoutDiagnosticsTracker();
    }

    public LayoutDiagnosticsTracker Diagnostics { get; }

    public LayoutFingerprint Fingerprint { get; private set; }

    public TPrepared? Prepared { get; private set; }

    public SolvedLayout? MeasuredLayout { get; private set; }

    public SolvedLayout? ArrangedLayout { get; private set; }

    public LayoutConstraints MeasuredConstraints { get; private set; }

    public LayoutConstraints ArrangedConstraints { get; private set; }

    public LayoutDiagnosticsSnapshot Snapshot => Diagnostics.Snapshot;

    public void Reset()
    {
        Fingerprint = default;
        Prepared = null;
        MeasuredLayout = null;
        ArrangedLayout = null;
        MeasuredConstraints = default;
        ArrangedConstraints = default;
    }

    public void InvalidateSolvedLayouts()
    {
        MeasuredLayout = null;
        ArrangedLayout = null;
        MeasuredConstraints = default;
        ArrangedConstraints = default;
    }

    public TPrepared EnsurePrepared(LayoutFingerprint fingerprint, Func<LayoutFingerprint, TPrepared> prepare)
    {
        ArgumentNullException.ThrowIfNull(prepare);

        var cacheHit = Prepared is not null && Fingerprint == fingerprint;
        Diagnostics.RecordPrepare(cacheHit);

        if (!cacheHit)
        {
            Fingerprint = fingerprint;
            Prepared = prepare(fingerprint);
            InvalidateSolvedLayouts();
        }

        return Prepared!;
    }

    public SolvedLayout EnsureMeasuredLayout(
        LayoutFingerprint fingerprint,
        LayoutConstraints constraints,
        Func<LayoutFingerprint, TPrepared> prepare,
        Func<TPrepared, LayoutConstraints, SolvedLayout> solve)
    {
        var prepared = EnsurePrepared(fingerprint, prepare);

        var cacheHit = MeasuredLayout is not null && HasCompatibleLayout(MeasuredLayout, MeasuredConstraints, fingerprint, constraints);
        Diagnostics.RecordLayoutAccess(LayoutPassKind.Measure, cacheHit);

        if (!cacheHit)
        {
            MeasuredLayout = solve(prepared, constraints);
            MeasuredConstraints = constraints;
            Diagnostics.RecordSolve(LayoutPassKind.Measure);
        }

        return MeasuredLayout!;
    }

    public SolvedLayout EnsureArrangedLayout(
        LayoutFingerprint fingerprint,
        LayoutConstraints constraints,
        Func<LayoutFingerprint, TPrepared> prepare,
        Func<TPrepared, LayoutConstraints, SolvedLayout> solve)
    {
        var prepared = EnsurePrepared(fingerprint, prepare);

        var cacheHit = ArrangedLayout is not null && HasCompatibleLayout(ArrangedLayout, ArrangedConstraints, fingerprint, constraints);
        Diagnostics.RecordLayoutAccess(LayoutPassKind.Arrange, cacheHit);

        if (!cacheHit)
        {
            ArrangedLayout = solve(prepared, constraints);
            ArrangedConstraints = constraints;
            Diagnostics.RecordSolve(LayoutPassKind.Arrange);
        }

        return ArrangedLayout!;
    }

    public bool HasPrepared(LayoutFingerprint fingerprint)
    {
        return Prepared is not null && Fingerprint == fingerprint;
    }

    private bool HasCompatibleLayout(
        SolvedLayout solved,
        LayoutConstraints cachedConstraints,
        LayoutFingerprint fingerprint,
        LayoutConstraints constraints)
    {
        return solved.PreparedFingerprint == fingerprint
               && AreClose(cachedConstraints.AvailableWidth, constraints.AvailableWidth)
               && AreClose(cachedConstraints.AvailableHeight, constraints.AvailableHeight)
               && AreClose(cachedConstraints.Density, constraints.Density)
               && AreEquivalent(cachedConstraints.Viewport, constraints.Viewport);
    }

    private bool AreEquivalent(LayoutViewport? first, LayoutViewport? second)
    {
        if (first is null && second is null)
        {
            return true;
        }

        if (first is null || second is null)
        {
            return false;
        }

        return AreClose(first.Value.X, second.Value.X)
               && AreClose(first.Value.Y, second.Value.Y)
               && AreClose(first.Value.Width, second.Value.Width)
               && AreClose(first.Value.Height, second.Value.Height);
    }

    private bool AreClose(double first, double second)
    {
        return Math.Abs(first - second) <= _constraintEpsilon;
    }
}
