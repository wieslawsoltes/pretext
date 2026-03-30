using Avalonia.Controls.Primitives;
using Pretext.LayoutFramework;

namespace Pretext.Avalonia.Controls;

public abstract class PretextTemplatedControlBase : TemplatedControl, IPretextControlLayoutSource
{
    protected abstract LayoutFingerprint GetLayoutFingerprintCore();

    protected abstract PreparedControlModel PrepareCore(LayoutFingerprint fingerprint);

    protected abstract SolvedLayout SolveCore(PreparedControlModel prepared, LayoutConstraints constraints);

    LayoutFingerprint IPretextControlLayoutSource.GetLayoutFingerprint()
    {
        return GetLayoutFingerprintCore();
    }

    PreparedControlModel IPretextControlLayoutSource.Prepare(LayoutFingerprint fingerprint)
    {
        return PrepareCore(fingerprint);
    }

    SolvedLayout IPretextControlLayoutSource.Solve(PreparedControlModel prepared, LayoutConstraints constraints)
    {
        return SolveCore(prepared, constraints);
    }

    protected void InvalidatePreparedLayout()
    {
        InvalidateMeasure();
    }

    protected void InvalidateSolvedLayout()
    {
        InvalidateArrange();
    }
}
