using Avalonia;
using Avalonia.Controls;
using Pretext.LayoutFramework;

namespace Pretext.Avalonia.Controls;

public abstract class PretextPanelBase : Panel
{
    private readonly PreparedLayoutController<PreparedPanelModel> _controller = new();

    public LayoutDiagnosticsSnapshot Diagnostics => _controller.Snapshot;

    protected abstract LayoutFingerprint GetLayoutFingerprint();

    protected abstract PreparedPanelModel Prepare(LayoutFingerprint fingerprint);

    protected abstract SolvedLayout Solve(PreparedPanelModel prepared, LayoutConstraints constraints);

    protected virtual LayoutConstraints CreateConstraints(Size availableSize)
    {
        return new LayoutConstraints(availableSize.Width, availableSize.Height);
    }

    protected virtual int GetChildIndex(LayoutPlacement placement, IReadOnlyList<Control> children)
    {
        return placement.Index >= 0 && placement.Index < children.Count ? placement.Index : -1;
    }

    protected virtual void MeasureChild(Control child, LayoutPlacement placement)
    {
        child.Measure(ToSize(placement.Bounds.Size));
    }

    protected virtual void ArrangeChild(Control child, LayoutPlacement placement)
    {
        child.Arrange(ToRect(placement.Bounds));
    }

    protected void InvalidatePreparedLayout()
    {
        _controller.Reset();
        InvalidateMeasure();
    }

    protected override void ChildrenChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        _controller.Reset();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var solved = _controller.EnsureMeasuredLayout(
            GetLayoutFingerprint(),
            CreateConstraints(availableSize),
            Prepare,
            Solve);

        MeasureChildren(solved);
        return ToSize(solved.Extent);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var solved = _controller.EnsureArrangedLayout(
            GetLayoutFingerprint(),
            CreateConstraints(finalSize),
            Prepare,
            Solve);

        ArrangeChildren(solved);
        return ToSize(solved.Extent);
    }

    private void MeasureChildren(SolvedLayout solved)
    {
        var touched = new bool[Children.Count];

        foreach (var placement in solved.Placements)
        {
            var childIndex = GetChildIndex(placement, Children);
            if (childIndex < 0)
            {
                continue;
            }

            touched[childIndex] = true;
            MeasureChild(Children[childIndex], placement);
        }

        for (var index = 0; index < Children.Count; index++)
        {
            if (!touched[index])
            {
                Children[index].Measure(default);
            }
        }
    }

    private void ArrangeChildren(SolvedLayout solved)
    {
        var touched = new bool[Children.Count];

        foreach (var placement in solved.Placements)
        {
            var childIndex = GetChildIndex(placement, Children);
            if (childIndex < 0)
            {
                continue;
            }

            touched[childIndex] = true;
            ArrangeChild(Children[childIndex], placement);
        }

        for (var index = 0; index < Children.Count; index++)
        {
            if (!touched[index])
            {
                Children[index].Arrange(default);
            }
        }
    }

    private static Size ToSize(LayoutSize size)
    {
        return new Size(Math.Max(0, size.Width), Math.Max(0, size.Height));
    }

    private static Rect ToRect(LayoutRect rect)
    {
        return new Rect(rect.X, rect.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height));
    }
}
