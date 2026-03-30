using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pretext.LayoutFramework;
using Windows.Foundation;

namespace Pretext.Uno.Controls;

public abstract class PretextVirtualizingLayout : VirtualizingLayout
{
    private LayoutCacheState? _lastState;

    private sealed class LayoutCacheState
    {
        public PreparedLayoutController<PreparedItemsModel> Controller { get; } = new();

        public Dictionary<int, LayoutPlacement> PlacementMap { get; } = [];

        public Dictionary<int, UIElement> RealizedElements { get; } = [];
    }

    public double ViewportOverscan { get; set; } = 240;

    public LayoutDiagnosticsSnapshot Diagnostics => _lastState?.Controller.Snapshot ?? default;

    protected virtual bool IncludeViewportInSolveConstraints => false;

    protected sealed override void UninitializeForContextCore(VirtualizingLayoutContext context)
    {
        if (context.LayoutState is LayoutCacheState state)
        {
            RecycleAll(context, state);
            context.LayoutState = null;
        }
    }

    protected sealed override Size MeasureOverride(VirtualizingLayoutContext context, Size availableSize)
    {
        var state = GetOrCreateState(context);
        var solved = EnsureSolvedLayout(context, availableSize, arrangePhase: false, state);
        context.LayoutOrigin = new Point(0, 0);

        RealizeRange(context, state, solved);
        foreach (var (index, element) in state.RealizedElements)
        {
            if (state.PlacementMap.TryGetValue(index, out var placement))
            {
                MeasureElement(element, placement);
            }
        }

        return ToSize(solved.Extent);
    }

    protected sealed override Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize)
    {
        var state = GetOrCreateState(context);
        var solved = EnsureSolvedLayout(context, finalSize, arrangePhase: true, state);
        context.LayoutOrigin = new Point(0, 0);

        RealizeRange(context, state, solved);
        foreach (var (index, element) in state.RealizedElements)
        {
            if (state.PlacementMap.TryGetValue(index, out var placement))
            {
                ArrangeElement(element, placement);
            }
        }

        return ToSize(solved.Extent);
    }

    protected sealed override void OnItemsChangedCore(VirtualizingLayoutContext context, object source, System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
    {
        if (context.LayoutState is LayoutCacheState state)
        {
            RecycleAll(context, state);
            state.Controller.Reset();
            state.PlacementMap.Clear();
        }

        InvalidateMeasure();
    }

    protected abstract LayoutFingerprint GetLayoutFingerprint(VirtualizingLayoutContext context);

    protected abstract PreparedItemsModel Prepare(VirtualizingLayoutContext context, LayoutFingerprint fingerprint);

    protected abstract SolvedLayout Solve(VirtualizingLayoutContext context, PreparedItemsModel prepared, LayoutConstraints constraints);

    protected virtual LayoutConstraints CreateConstraints(VirtualizingLayoutContext context, Size availableSize, LayoutViewport? viewport)
    {
        var constraints = new LayoutConstraints(availableSize.Width, availableSize.Height, viewport);
        return IncludeViewportInSolveConstraints ? constraints : constraints.WithoutViewport();
    }

    protected virtual bool TryGetRealizationSelection(
        VirtualizingLayoutContext context,
        PreparedItemsModel prepared,
        SolvedLayout solved,
        out VerticalOcclusionSelection selection)
    {
        var viewport = context.RealizationRect;
        if (solved.VerticalOcclusion is { } occlusion
            && viewport.Height >= 0
            && !double.IsInfinity(viewport.Height)
            && occlusion.TryQuerySelection(viewport.Top - ViewportOverscan, viewport.Bottom + ViewportOverscan, out selection))
        {
            return true;
        }

        if (solved.Placements.Count > 0)
        {
            selection = new VerticalOcclusionSelection(0, solved.Placements.Count, 0, solved.VerticalOcclusion?.BandCount ?? 0);
            return true;
        }

        selection = default;
        return false;
    }

    protected virtual void MeasureElement(UIElement element, LayoutPlacement placement)
    {
        element.Measure(ToSize(placement.Bounds.Size));
    }

    protected virtual void ArrangeElement(UIElement element, LayoutPlacement placement)
    {
        element.Arrange(ToRect(placement.Bounds));
    }

    private SolvedLayout EnsureSolvedLayout(VirtualizingLayoutContext context, Size size, bool arrangePhase, LayoutCacheState state)
    {
        var fingerprint = GetLayoutFingerprint(context);
        var viewport = GetActiveViewport(context);
        var constraints = CreateConstraints(context, size, viewport);
        state.Controller.Diagnostics.RecordViewportUpdate(viewport);
        var solved = arrangePhase
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

        RebuildPlacementMap(state, solved);
        return solved;
    }

    private static LayoutViewport? GetActiveViewport(VirtualizingLayoutContext context)
    {
        var realizationRect = context.RealizationRect;
        return realizationRect.Width >= 0 && realizationRect.Height >= 0 && !double.IsInfinity(realizationRect.Height)
            ? new LayoutViewport(realizationRect.X, realizationRect.Y, realizationRect.Width, realizationRect.Height)
            : null;
    }

    private void RealizeRange(VirtualizingLayoutContext context, LayoutCacheState state, SolvedLayout solved)
    {
        var prepared = state.Controller.Prepared ?? throw new InvalidOperationException("Prepared item model is required before realization.");
        if (!TryGetRealizationSelection(context, prepared, solved, out var selection))
        {
            RecycleAll(context, state);
            state.Controller.Diagnostics.RecordRealization(null, 0);
            return;
        }

        var keep = new HashSet<int>();
        if (selection.ItemIndices is { Length: > 0 } sparseIndices)
        {
            for (var i = 0; i < sparseIndices.Length; i++)
            {
                var index = sparseIndices[i];
                keep.Add(index);
                if (!state.RealizedElements.ContainsKey(index))
                {
                    var element = context.GetOrCreateElementAt(
                        index,
                        ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
                    state.RealizedElements[index] = element;
                }
            }
        }
        else
        {
            for (var index = selection.StartIndex; index < selection.EndIndexExclusive; index++)
            {
                keep.Add(index);
                if (!state.RealizedElements.ContainsKey(index))
                {
                    var element = context.GetOrCreateElementAt(
                        index,
                        ElementRealizationOptions.ForceCreate | ElementRealizationOptions.SuppressAutoRecycle);
                    state.RealizedElements[index] = element;
                }
            }
        }

        var toRecycle = state.RealizedElements.Keys.Where(index => !keep.Contains(index)).ToArray();
        foreach (var index in toRecycle)
        {
            context.RecycleElement(state.RealizedElements[index]);
            state.RealizedElements.Remove(index);
        }

        state.Controller.Diagnostics.RecordRealization(selection.ToRange(), state.RealizedElements.Count);
    }

    private LayoutCacheState GetOrCreateState(VirtualizingLayoutContext context)
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

    private static void RebuildPlacementMap(LayoutCacheState state, SolvedLayout solved)
    {
        state.PlacementMap.Clear();
        foreach (var placement in solved.Placements)
        {
            state.PlacementMap[placement.Index] = placement;
        }
    }

    private static void RecycleAll(VirtualizingLayoutContext context, LayoutCacheState state)
    {
        foreach (var element in state.RealizedElements.Values)
        {
            context.RecycleElement(element);
        }

        state.RealizedElements.Clear();
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
