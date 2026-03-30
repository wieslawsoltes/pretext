using Pretext.LayoutFramework;

namespace Pretext.Avalonia.Controls;

public interface IPretextControlLayoutSource
{
    LayoutFingerprint GetLayoutFingerprint();

    PreparedControlModel Prepare(LayoutFingerprint fingerprint);

    SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints);
}
