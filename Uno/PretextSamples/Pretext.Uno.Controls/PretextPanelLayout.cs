using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pretext.LayoutFramework;
using Windows.Foundation;

namespace Pretext.Uno.Controls;

public abstract class PretextPanelLayout : NonVirtualizingLayout
{
    private LayoutCacheState? _lastState;

    private sealed class LayoutCacheState
    {
        public PreparedLayoutController<PreparedPanelModel> Controller { get; } = new();
    }

    public LayoutDiagnosticsSnapshot Diagnostics => _lastState?.Controller.Snapshot ?? default;

    protected sealed override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
    {
        var solved = EnsureSolvedLayout(context, availableSize, arrangePhase: false);
        MeasureChildren(context.Children, solved);
        return ToSize(solved.Extent);
    }

    protected sealed override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
    {
        var solved = EnsureSolvedLayout(context, finalSize, arrangePhase: true);
        ArrangeChildren(context.Children, solved);
        return ToSize(solved.Extent);
    }

    protected abstract LayoutFingerprint GetLayoutFingerprint(NonVirtualizingLayoutContext context);

    protected abstract PreparedPanelModel Prepare(NonVirtualizingLayoutContext context, LayoutFingerprint fingerprint);

    protected abstract SolvedLayout Solve(NonVirtualizingLayoutContext context, PreparedPanelModel prepared, LayoutConstraints constraints);

    protected virtual LayoutConstraints CreateConstraints(Size availableSize)
    {
        return new LayoutConstraints(availableSize.Width, availableSize.Height);
    }

    protected virtual int GetChildIndex(LayoutPlacement placement, IReadOnlyList<UIElement> children)
    {
        return placement.Index >= 0 && placement.Index < children.Count ? placement.Index : -1;
    }

    protected virtual void MeasureChild(UIElement child, LayoutPlacement placement)
    {
        child.Measure(ToSize(placement.Bounds.Size));
    }

    protected virtual void ArrangeChild(UIElement child, LayoutPlacement placement)
    {
        child.Arrange(ToRect(placement.Bounds));
    }

    private SolvedLayout EnsureSolvedLayout(NonVirtualizingLayoutContext context, Size size, bool arrangePhase)
    {
        var state = GetOrCreateState(context);
        var fingerprint = GetLayoutFingerprint(context);
        var constraints = CreateConstraints(size);
        return arrangePhase
            ? state.Controller.EnsureArrangedLayout(
                fingerprint,
                constraints,
                fp => Prepare(context, fp),
                (prepared, currentConstraints) => Solve(context, prepared, currentConstraints))
            : state.Controller.EnsureMeasuredLayout(
                fingerprint,
                constraints,
                fp => Prepare(context, fp),
                (prepared, currentConstraints) => Solve(context, prepared, currentConstraints));
    }

    private LayoutCacheState GetOrCreateState(NonVirtualizingLayoutContext context)
    {
        if (context.LayoutState is LayoutCacheState state)
        {
            _lastState = state;
            return state;
        }

        state = new LayoutCacheState();
        context.LayoutState = state;
        _lastState = state;
        return state;
    }
    private void MeasureChildren(IReadOnlyList<UIElement> children, SolvedLayout solved)
    {
        var touched = new bool[children.Count];

        foreach (var placement in solved.Placements)
        {
            var childIndex = GetChildIndex(placement, children);
            if (childIndex < 0)
            {
                continue;
            }

            touched[childIndex] = true;
            MeasureChild(children[childIndex], placement);
        }

        for (var index = 0; index < children.Count; index++)
        {
            if (!touched[index])
            {
                children[index].Measure(new Size(0, 0));
            }
        }
    }

    private void ArrangeChildren(IReadOnlyList<UIElement> children, SolvedLayout solved)
    {
        var touched = new bool[children.Count];

        foreach (var placement in solved.Placements)
        {
            var childIndex = GetChildIndex(placement, children);
            if (childIndex < 0)
            {
                continue;
            }

            touched[childIndex] = true;
            ArrangeChild(children[childIndex], placement);
        }

        for (var index = 0; index < children.Count; index++)
        {
            if (!touched[index])
            {
                children[index].Arrange(new Rect(0, 0, 0, 0));
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
