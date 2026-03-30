using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Pretext.LayoutFramework;

namespace Pretext.Avalonia.Controls;

public abstract class PretextVirtualizingPanelBase : VirtualizingPanel, ILogicalScrollable
{
    private static readonly AttachedProperty<object?> RecycleKeyProperty =
        AvaloniaProperty.RegisterAttached<PretextVirtualizingPanelBase, Control, object?>("RecycleKey");

    private static readonly object ItemIsOwnContainerKey = new();

    private readonly PreparedLayoutController<PreparedItemsModel> _controller = new();
    private readonly Dictionary<int, Control> _realizedElements = [];
    private readonly Dictionary<int, LayoutPlacement> _placementMap = [];
    private readonly Dictionary<object, Stack<Control>> _recyclePool = [];

    private Size _extent;
    private Size _viewport;
    private Vector _offset;
    private Size _scrollSizeCache = new(50, 50);
    private bool _scrollSizeCacheValid;
    private bool _canHorizontallyScroll;
    private bool _canVerticallyScroll;
    private bool _scrollAxesConfigured;
    private EventHandler? _scrollInvalidated;
    private Rect _effectiveViewport;
    private bool _hasEffectiveViewport;
    private bool _isLogicalScrollActive;
    private int _forceRealizeIndex = -1;

    protected PretextVirtualizingPanelBase()
    {
        ClipToBounds = true;
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    public double ViewportOverscan { get; set; } = 240;

    public LayoutDiagnosticsSnapshot Diagnostics => _controller.Snapshot;

    protected virtual bool IsLogicalScrollEnabledCore => true;

    protected virtual bool IncludeViewportInSolveConstraints => false;

    protected bool UsesLogicalScrolling => IsLogicalScrollEnabledCore && _isLogicalScrollActive;

    protected abstract LayoutFingerprint GetLayoutFingerprint();

    protected abstract PreparedItemsModel Prepare(LayoutFingerprint fingerprint);

    protected abstract SolvedLayout Solve(PreparedItemsModel prepared, LayoutConstraints constraints);

    protected virtual LayoutConstraints CreateConstraints(Size availableSize, LayoutViewport? viewport)
    {
        var constraints = new LayoutConstraints(availableSize.Width, availableSize.Height, viewport);
        return IncludeViewportInSolveConstraints ? constraints : constraints.WithoutViewport();
    }

    protected virtual bool TryGetRealizationSelection(
        PreparedItemsModel prepared,
        SolvedLayout solved,
        out VerticalOcclusionSelection selection)
    {
        if (TryGetActiveViewport(out var viewport)
            && solved.VerticalOcclusion is { } occlusion
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

    protected virtual void MeasureElement(Control element, LayoutPlacement placement)
    {
        element.Measure(ToSize(placement.Bounds.Size));
    }

    protected virtual void ArrangeElement(Control element, LayoutPlacement placement)
    {
        var bounds = UsesLogicalScrolling
            ? new LayoutRect(
                placement.Bounds.X - _offset.X,
                placement.Bounds.Y - _offset.Y,
                placement.Bounds.Width,
                placement.Bounds.Height)
            : placement.Bounds;

        element.Arrange(ToRect(bounds));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Items.Count == 0)
        {
            RecycleAllRealized();
            return default;
        }

        if (UsesLogicalScrolling)
        {
            SetViewport(availableSize, raiseInvalidated: false, invalidateMeasure: false);
        }

        var solved = EnsureMeasuredLayout(availableSize);
        UpdateExtentAndScrollMetrics(solved);
        RealizeRange(solved);

        foreach (var (index, element) in _realizedElements)
        {
            if (_placementMap.TryGetValue(index, out var placement))
            {
                MeasureElement(element, placement);
            }
        }

        return ToSize(solved.Extent);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Items.Count == 0)
        {
            return default;
        }

        if (UsesLogicalScrolling)
        {
            SetViewport(finalSize, raiseInvalidated: false, invalidateMeasure: false);
        }

        var solved = EnsureArrangedLayout(finalSize);
        UpdateExtentAndScrollMetrics(solved);
        RealizeRange(solved);

        foreach (var (index, element) in _realizedElements)
        {
            if (_placementMap.TryGetValue(index, out var placement))
            {
                ArrangeElement(element, placement);
            }
        }

        return ToSize(solved.Extent);
    }

    protected override void OnItemsControlChanged(ItemsControl? oldValue)
    {
        if (oldValue is not null)
        {
            ResetLayoutState();
        }

        base.OnItemsControlChanged(oldValue);
    }

    protected override void OnItemsChanged(IReadOnlyList<object?> items, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        RecycleAllRealized();
        _controller.Reset();
        _placementMap.Clear();
        _forceRealizeIndex = -1;
        InvalidateMeasure();
        base.OnItemsChanged(items, e);
    }

    protected override Control? ScrollIntoView(int index)
    {
        if (_realizedElements.TryGetValue(index, out var existing))
        {
            if (UsesLogicalScrolling && _placementMap.TryGetValue(index, out var placement))
            {
                _ = BringBoundsIntoView(ToRect(placement.Bounds));
            }
            else
            {
                existing.BringIntoView();
            }

            return existing;
        }

        _forceRealizeIndex = index >= 0 && index < Items.Count ? index : -1;
        if (_forceRealizeIndex >= 0)
        {
            if (UsesLogicalScrolling && _placementMap.TryGetValue(_forceRealizeIndex, out var placement))
            {
                _ = BringBoundsIntoView(ToRect(placement.Bounds));
            }

            InvalidateMeasure();
        }

        return null;
    }

    protected override Control? ContainerFromIndex(int index)
    {
        return _realizedElements.TryGetValue(index, out var element) ? element : null;
    }

    protected override int IndexFromContainer(Control container)
    {
        foreach (var (index, realized) in _realizedElements)
        {
            if (ReferenceEquals(realized, container))
            {
                return index;
            }
        }

        return -1;
    }

    protected override IEnumerable<Control>? GetRealizedContainers()
    {
        return _realizedElements.Values;
    }

    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        if (_realizedElements.Count == 0)
        {
            return null;
        }

        var ordered = _realizedElements.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
        if (from is not Control fromControl)
        {
            return ordered[0];
        }

        var currentIndex = Array.IndexOf(ordered, fromControl);
        if (currentIndex < 0)
        {
            return ordered[0];
        }

        var delta = direction switch
        {
            NavigationDirection.Next or NavigationDirection.Down or NavigationDirection.Right => 1,
            NavigationDirection.Previous or NavigationDirection.Up or NavigationDirection.Left => -1,
            _ => 0,
        };

        if (delta == 0)
        {
            return fromControl;
        }

        var targetIndex = currentIndex + delta;
        if (wrap)
        {
            targetIndex = (targetIndex % ordered.Length + ordered.Length) % ordered.Length;
        }

        return targetIndex >= 0 && targetIndex < ordered.Length ? ordered[targetIndex] : null;
    }

    bool ILogicalScrollable.CanHorizontallyScroll
    {
        get => _canHorizontallyScroll;
        set
        {
            if (_canHorizontallyScroll == value)
            {
                _scrollAxesConfigured = true;
                return;
            }

            _canHorizontallyScroll = value;
            _scrollAxesConfigured = true;
            OnCanScrollChanged();
        }
    }

    bool ILogicalScrollable.CanVerticallyScroll
    {
        get => _canVerticallyScroll;
        set
        {
            if (_canVerticallyScroll == value)
            {
                _scrollAxesConfigured = true;
                return;
            }

            _canVerticallyScroll = value;
            _scrollAxesConfigured = true;
            OnCanScrollChanged();
        }
    }

    bool ILogicalScrollable.IsLogicalScrollEnabled => IsLogicalScrollEnabledCore;

    Size ILogicalScrollable.ScrollSize => GetScrollSize();

    Size ILogicalScrollable.PageScrollSize => _viewport;

    Size IScrollable.Extent => _extent;

    Vector IScrollable.Offset
    {
        get => _offset;
        set => SetLogicalOffset(value, raiseInvalidated: true, invalidateMeasure: true);
    }

    Size IScrollable.Viewport => _viewport;

    event EventHandler? ILogicalScrollable.ScrollInvalidated
    {
        add
        {
            var wasActive = _isLogicalScrollActive;
            _scrollInvalidated += value;
            _isLogicalScrollActive = _scrollInvalidated is not null;
            if (_isLogicalScrollActive != wasActive)
            {
                OnLogicalScrollingStateChanged();
            }
        }
        remove
        {
            var wasActive = _isLogicalScrollActive;
            _scrollInvalidated -= value;
            _isLogicalScrollActive = _scrollInvalidated is not null;
            if (_isLogicalScrollActive != wasActive)
            {
                OnLogicalScrollingStateChanged();
            }
        }
    }

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect)
    {
        if (VisualRoot is null)
        {
            return false;
        }

        Rect rect;
        if (ReferenceEquals(target, this))
        {
            rect = targetRect.Width <= 0 || targetRect.Height <= 0
                ? Bounds
                : targetRect;
        }
        else
        {
            var transform = target.TransformToVisual(this);
            if (transform is null)
            {
                return false;
            }

            rect = targetRect.Width <= 0 || targetRect.Height <= 0
                ? target.Bounds.TransformToAABB(transform.Value)
                : targetRect.TransformToAABB(transform.Value);
        }

        if (UsesLogicalScrolling && (_offset.X != 0 || _offset.Y != 0))
        {
            rect = rect.Translate(_offset);
        }

        return BringBoundsIntoView(rect);
    }

    private bool BringBoundsIntoView(Rect rect)
    {
        var offset = _offset;
        var updated = offset;

        if (rect.Bottom > offset.Y + _viewport.Height)
        {
            updated = updated.WithY(rect.Bottom - _viewport.Height);
        }
        else if (rect.Y < offset.Y)
        {
            updated = updated.WithY(rect.Y);
        }

        if (rect.Right > offset.X + _viewport.Width)
        {
            updated = updated.WithX(rect.Right - _viewport.Width);
        }
        else if (rect.X < offset.X)
        {
            updated = updated.WithX(rect.X);
        }

        return SetLogicalOffset(updated, raiseInvalidated: true, invalidateMeasure: true) != default;
    }

    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from)
    {
        return GetControl(direction, from, wrap: false) as Control;
    }

    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e)
    {
        _scrollInvalidated?.Invoke(this, e);
    }

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        if (UsesLogicalScrolling)
        {
            return;
        }

        var nextViewport = e.EffectiveViewport.Intersect(new Rect(Bounds.Size));
        if (!_hasEffectiveViewport || !AreClose(_effectiveViewport, nextViewport))
        {
            _effectiveViewport = nextViewport;
            _hasEffectiveViewport = true;
            InvalidateMeasure();
        }
    }

    private SolvedLayout EnsureMeasuredLayout(Size availableSize)
    {
        var viewport = GetActiveViewport();
        var constraints = CreateConstraints(availableSize, viewport);
        _controller.Diagnostics.RecordViewportUpdate(viewport);
        var solved = _controller.EnsureMeasuredLayout(
            GetLayoutFingerprint(),
            constraints,
            Prepare,
            Solve);

        RebuildPlacementMap(solved);
        return solved;
    }

    private SolvedLayout EnsureArrangedLayout(Size finalSize)
    {
        var viewport = GetActiveViewport();
        var constraints = CreateConstraints(finalSize, viewport);
        _controller.Diagnostics.RecordViewportUpdate(viewport);
        var solved = _controller.EnsureArrangedLayout(
            GetLayoutFingerprint(),
            constraints,
            Prepare,
            Solve);

        RebuildPlacementMap(solved);
        return solved;
    }

    private bool TryGetActiveViewport(out LayoutViewport viewport)
    {
        if (UsesLogicalScrolling)
        {
            viewport = new LayoutViewport(_offset.X, _offset.Y, _viewport.Width, _viewport.Height);
            return _viewport.Width > 0 || _viewport.Height > 0;
        }

        if (_hasEffectiveViewport)
        {
            viewport = new LayoutViewport(
                _effectiveViewport.X,
                _effectiveViewport.Y,
                _effectiveViewport.Width,
                _effectiveViewport.Height);
            return true;
        }

        viewport = default;
        return false;
    }

    private LayoutViewport? GetActiveViewport()
    {
        return TryGetActiveViewport(out var viewport) ? viewport : null;
    }

    private void RealizeRange(SolvedLayout solved)
    {
        var prepared = _controller.Prepared ?? throw new InvalidOperationException("Prepared item model must exist before realization.");
        if (!TryGetRealizationSelection(prepared, solved, out var selection))
        {
            RecycleAllRealized();
            _controller.Diagnostics.RecordRealization(null, 0);
            return;
        }

        var keep = new HashSet<int>();
        if (selection.ItemIndices is { Length: > 0 } sparseIndices)
        {
            for (var i = 0; i < sparseIndices.Length; i++)
            {
                keep.Add(sparseIndices[i]);
            }
        }
        else
        {
            var start = selection.StartIndex;
            var end = selection.EndIndexExclusive;
            if (_forceRealizeIndex >= 0 && _forceRealizeIndex < prepared.Count)
            {
                start = Math.Min(start, _forceRealizeIndex);
                end = Math.Max(end, _forceRealizeIndex + 1);
            }

            for (var index = start; index < end; index++)
            {
                keep.Add(index);
            }
        }

        if (_forceRealizeIndex >= 0 && _forceRealizeIndex < prepared.Count)
        {
            keep.Add(_forceRealizeIndex);
        }

        foreach (var index in keep)
        {
            if (!_realizedElements.ContainsKey(index))
            {
                _realizedElements[index] = GetOrCreateElement(index);
            }
        }

        var toRecycle = _realizedElements.Keys.Where(index => !keep.Contains(index)).ToArray();
        foreach (var index in toRecycle)
        {
            RecycleElement(_realizedElements[index], index);
            _realizedElements.Remove(index);
        }

        _controller.Diagnostics.RecordRealization(selection.ToRange(), _realizedElements.Count);

        if (_forceRealizeIndex >= 0 && _realizedElements.TryGetValue(_forceRealizeIndex, out var forced))
        {
            if (UsesLogicalScrolling && _placementMap.TryGetValue(_forceRealizeIndex, out var placement))
            {
                _ = BringBoundsIntoView(ToRect(placement.Bounds));
            }
            else
            {
                forced.BringIntoView();
            }

            _forceRealizeIndex = -1;
        }
    }

    private Control GetOrCreateElement(int index)
    {
        var generator = ItemContainerGenerator ?? throw new InvalidOperationException("Virtualizing panel is not attached to an ItemsControl.");
        var item = Items[index];

        if (generator.NeedsContainer(item, index, out var recycleKey))
        {
            return GetRecycledElement(item, index, recycleKey, generator) ?? CreateElement(item, index, recycleKey, generator);
        }

        return GetItemAsOwnContainer(item, index, generator);
    }

    private Control GetItemAsOwnContainer(object? item, int index, ItemContainerGenerator generator)
    {
        var controlItem = (Control)item!;
        if (!controlItem.IsSet(RecycleKeyProperty))
        {
            generator.PrepareItemContainer(controlItem, controlItem, index);
            AddInternalChild(controlItem);
            controlItem.SetValue(RecycleKeyProperty, ItemIsOwnContainerKey);
            generator.ItemContainerPrepared(controlItem, item, index);
        }

        controlItem.IsVisible = true;
        return controlItem;
    }

    private Control? GetRecycledElement(object? item, int index, object? recycleKey, ItemContainerGenerator generator)
    {
        if (recycleKey is null)
        {
            return null;
        }

        if (_recyclePool.TryGetValue(recycleKey, out var pool) && pool.Count > 0)
        {
            var recycled = pool.Pop();
            recycled.IsVisible = true;
            generator.PrepareItemContainer(recycled, item, index);
            generator.ItemContainerPrepared(recycled, item, index);
            return recycled;
        }

        return null;
    }

    private Control CreateElement(object? item, int index, object? recycleKey, ItemContainerGenerator generator)
    {
        var container = generator.CreateContainer(item, index, recycleKey);
        container.SetValue(RecycleKeyProperty, recycleKey);
        generator.PrepareItemContainer(container, item, index);
        AddInternalChild(container);
        generator.ItemContainerPrepared(container, item, index);
        return container;
    }

    private void RecycleElement(Control element, int index)
    {
        var generator = ItemContainerGenerator ?? throw new InvalidOperationException("Virtualizing panel is not attached to an ItemsControl.");
        var recycleKey = element.GetValue(RecycleKeyProperty);

        if (ReferenceEquals(recycleKey, ItemIsOwnContainerKey))
        {
            element.IsVisible = false;
            return;
        }

        if (recycleKey is null)
        {
            generator.ClearItemContainer(element);
            RemoveInternalChild(element);
            return;
        }

        generator.ClearItemContainer(element);
        if (!_recyclePool.TryGetValue(recycleKey, out var pool))
        {
            pool = new Stack<Control>();
            _recyclePool[recycleKey] = pool;
        }

        pool.Push(element);
        element.IsVisible = false;
    }

    private void RecycleAllRealized()
    {
        if (ItemContainerGenerator is null)
        {
            _realizedElements.Clear();
            return;
        }

        var realized = _realizedElements.ToArray();
        foreach (var (index, element) in realized)
        {
            RecycleElement(element, index);
        }

        _realizedElements.Clear();
    }

    private void ResetLayoutState()
    {
        RecycleAllRealized();
        _recyclePool.Clear();
        _placementMap.Clear();
        _controller.Reset();
        _forceRealizeIndex = -1;
    }

    private void RebuildPlacementMap(SolvedLayout solved)
    {
        _placementMap.Clear();
        _scrollSizeCacheValid = false;
        foreach (var placement in solved.Placements)
        {
            _placementMap[placement.Index] = placement;
        }
    }

    private void UpdateExtentAndScrollMetrics(SolvedLayout solved)
    {
        var extentChanged = SetExtent(ToSize(solved.Extent), raiseInvalidated: false);
        if (UsesLogicalScrolling)
        {
            extentChanged |= SetLogicalOffset(_offset, raiseInvalidated: false, invalidateMeasure: false) != default;
        }

        if (extentChanged)
        {
            _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private Size GetScrollSize()
    {
        if (!_scrollSizeCacheValid)
        {
            var widthSum = 0d;
            var heightSum = 0d;
            var count = 0;

            foreach (var placement in _placementMap.Values)
            {
                if (placement.Bounds.Width <= 0 && placement.Bounds.Height <= 0)
                {
                    continue;
                }

                widthSum += placement.Bounds.Width;
                heightSum += placement.Bounds.Height;
                count++;
            }

            if (count == 0 && _controller.Prepared is { } prepared)
            {
                foreach (var item in prepared.Items.Span)
                {
                    if (item.EstimatedWidth <= 0 && item.EstimatedHeight <= 0)
                    {
                        continue;
                    }

                    widthSum += item.EstimatedWidth;
                    heightSum += item.EstimatedHeight;
                    count++;
                }
            }

            _scrollSizeCache = count > 0
                ? new Size(Math.Max(1, widthSum / count), Math.Max(1, heightSum / count))
                : new Size(50, 50);
            _scrollSizeCacheValid = true;
        }

        return _scrollSizeCache;
    }

    private bool SetExtent(Size extent, bool raiseInvalidated)
    {
        if (_extent == extent)
        {
            return false;
        }

        _extent = extent;
        _scrollSizeCacheValid = false;
        if (raiseInvalidated)
        {
            _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    private bool SetViewport(Size viewport, bool raiseInvalidated, bool invalidateMeasure)
    {
        if (_viewport == viewport)
        {
            return false;
        }

        _viewport = viewport;
        _scrollSizeCacheValid = false;
        var offsetChanged = SetLogicalOffset(_offset, raiseInvalidated: false, invalidateMeasure: false) != default;
        if (raiseInvalidated || offsetChanged)
        {
            _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        }

        if (invalidateMeasure)
        {
            InvalidateMeasure();
        }

        return true;
    }

    private Vector SetLogicalOffset(Vector offset, bool raiseInvalidated, bool invalidateMeasure)
    {
        var coerced = CoerceOffset(offset);
        if (coerced == _offset)
        {
            return default;
        }

        var previous = _offset;
        _offset = coerced;
        if (raiseInvalidated)
        {
            _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        }

        if (invalidateMeasure)
        {
            InvalidateMeasure();
        }

        return _offset - previous;
    }

    private Vector CoerceOffset(Vector offset)
    {
        var maxX = Math.Max(_extent.Width - _viewport.Width, 0);
        var maxY = Math.Max(_extent.Height - _viewport.Height, 0);
        var x = Clamp(offset.X, 0, maxX);
        var y = Clamp(offset.Y, 0, maxY);

        if (_scrollAxesConfigured)
        {
            if (!_canHorizontallyScroll)
            {
                x = 0;
            }

            if (!_canVerticallyScroll)
            {
                y = 0;
            }
        }

        return new Vector(x, y);
    }

    private void OnCanScrollChanged()
    {
        _ = SetLogicalOffset(_offset, raiseInvalidated: true, invalidateMeasure: true);
    }

    private void OnLogicalScrollingStateChanged()
    {
        _ = SetLogicalOffset(_offset, raiseInvalidated: false, invalidateMeasure: false);
        _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        InvalidateMeasure();
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static Size ToSize(LayoutSize size)
    {
        return new Size(Math.Max(0, size.Width), Math.Max(0, size.Height));
    }

    private static Rect ToRect(LayoutRect rect)
    {
        return new Rect(rect.X, rect.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height));
    }

    private static bool AreClose(Rect first, Rect second)
    {
        return Math.Abs(first.X - second.X) <= 0.05
               && Math.Abs(first.Y - second.Y) <= 0.05
               && Math.Abs(first.Width - second.Width) <= 0.05
               && Math.Abs(first.Height - second.Height) <= 0.05;
    }
}
