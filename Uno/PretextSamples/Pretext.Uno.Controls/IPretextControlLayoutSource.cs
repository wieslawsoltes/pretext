using Pretext.LayoutFramework;

namespace Pretext.Uno.Controls;

public interface IPretextControlLayoutSource
{
    LayoutFingerprint GetLayoutFingerprint();

    PreparedControlModel Prepare(LayoutFingerprint fingerprint);

    SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints);
}
