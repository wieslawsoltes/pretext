using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pretext.LayoutFramework;
using Windows.Foundation;

namespace Pretext.Uno.Controls;

public sealed class PretextControlLayoutHost : LayoutPanel
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(
            nameof(Source),
            typeof(IPretextControlLayoutSource),
            typeof(PretextControlLayoutHost),
            new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty LayoutKeyProperty =
        DependencyProperty.RegisterAttached(
            "LayoutKey",
            typeof(string),
            typeof(PretextControlLayoutHost),
            new PropertyMetadata(null, OnLayoutKeyChanged));

    private readonly ControlLayout _layout;

    public PretextControlLayoutHost()
    {
        _layout = new ControlLayout(this);
        Layout = _layout;
    }

    public IPretextControlLayoutSource? Source
    {
        get => (IPretextControlLayoutSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public LayoutDiagnosticsSnapshot Diagnostics => _layout.Diagnostics;

    public static void SetLayoutKey(UIElement element, string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(LayoutKeyProperty, value);
    }

    public static string? GetLayoutKey(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (string?)element.GetValue(LayoutKeyProperty);
    }

    protected override void OnChildrenChanged()
    {
        base.OnChildrenChanged();
        ResetLayout();
    }

    internal IPretextControlLayoutSource? ResolveSource()
    {
        return Source ?? TemplatedParent as IPretextControlLayoutSource;
    }

    internal LayoutConstraints CreateConstraints(Size availableSize)
    {
        return new LayoutConstraints(availableSize.Width, availableSize.Height);
    }

    private void ResetLayout()
    {
        _layout.Reset();
        InvalidateMeasure();
    }

    private static void OnSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is PretextControlLayoutHost host)
        {
            host.ResetLayout();
        }
    }

    private static void OnLayoutKeyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is FrameworkElement element && element.Parent is PretextControlLayoutHost host)
        {
            host.ResetLayout();
        }
    }

    private sealed class ControlLayout : NonVirtualizingLayout
    {
        private LayoutCacheState? _lastState;

        private sealed class LayoutCacheState
        {
            public PreparedLayoutController<PreparedControlModel> Controller { get; } = new();
        }

        private readonly PretextControlLayoutHost _owner;

        public ControlLayout(PretextControlLayoutHost owner)
        {
            _owner = owner;
        }

        public void Reset()
        {
            _owner.LayoutState = null;
            _lastState = null;
        }

        public LayoutDiagnosticsSnapshot Diagnostics => _lastState?.Controller.Snapshot ?? default;

        protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
        {
            var source = _owner.ResolveSource();
            if (source is null)
            {
                MeasureFallbackChildren(context.Children);
                return default;
            }

            var solved = EnsureSolvedLayout(context, availableSize, source, arrangePhase: false);
            MeasureChildren(context.Children, solved);
            return ToSize(solved.Extent);
        }

        protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
        {
            var source = _owner.ResolveSource();
            if (source is null)
            {
                ArrangeFallbackChildren(context.Children);
                return finalSize;
            }

            var solved = EnsureSolvedLayout(context, finalSize, source, arrangePhase: true);
            ArrangeChildren(context.Children, solved);
            return ToSize(solved.Extent);
        }

        private SolvedLayout EnsureSolvedLayout(
            NonVirtualizingLayoutContext context,
            Size availableSize,
            IPretextControlLayoutSource source,
            bool arrangePhase)
        {
            var state = GetOrCreateState(context);
            var fingerprint = source.GetLayoutFingerprint();
            var constraints = _owner.CreateConstraints(availableSize);

            return arrangePhase
                ? state.Controller.EnsureArrangedLayout(fingerprint, constraints, source.Prepare, source.Solve)
                : state.Controller.EnsureMeasuredLayout(fingerprint, constraints, source.Prepare, source.Solve);
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

        private static void MeasureFallbackChildren(IReadOnlyList<UIElement> children)
        {
            foreach (var child in children)
            {
                child.Measure(new Size(0, 0));
            }
        }

        private static void ArrangeFallbackChildren(IReadOnlyList<UIElement> children)
        {
            foreach (var child in children)
            {
                child.Arrange(new Rect(0, 0, 0, 0));
            }
        }

        private static void MeasureChildren(IReadOnlyList<UIElement> children, SolvedLayout solved)
        {
            var touched = new bool[children.Count];
            var keyedChildren = BuildKeyMap(children);

            foreach (var placement in solved.Placements)
            {
                var childIndex = ResolveChildIndex(placement, children, keyedChildren);
                if (childIndex < 0)
                {
                    continue;
                }

                touched[childIndex] = true;
                children[childIndex].Measure(ToSize(placement.Bounds.Size));
            }

            for (var index = 0; index < children.Count; index++)
            {
                if (!touched[index])
                {
                    children[index].Measure(new Size(0, 0));
                }
            }
        }

        private static void ArrangeChildren(IReadOnlyList<UIElement> children, SolvedLayout solved)
        {
            var touched = new bool[children.Count];
            var keyedChildren = BuildKeyMap(children);

            foreach (var placement in solved.Placements)
            {
                var childIndex = ResolveChildIndex(placement, children, keyedChildren);
                if (childIndex < 0)
                {
                    continue;
                }

                touched[childIndex] = true;
                children[childIndex].Arrange(ToRect(placement.Bounds));
            }

            for (var index = 0; index < children.Count; index++)
            {
                if (!touched[index])
                {
                    children[index].Arrange(new Rect(0, 0, 0, 0));
                }
            }
        }

        private static Dictionary<string, int> BuildKeyMap(IReadOnlyList<UIElement> children)
        {
            var result = new Dictionary<string, int>(children.Count, StringComparer.Ordinal);

            for (var index = 0; index < children.Count; index++)
            {
                var child = children[index];
                var key = GetLayoutKey(child);
                if (string.IsNullOrWhiteSpace(key) && child is FrameworkElement frameworkElement)
                {
                    key = frameworkElement.Name;
                }

                if (!string.IsNullOrWhiteSpace(key))
                {
                    result[key] = index;
                }
            }

            return result;
        }

        private static int ResolveChildIndex(
            LayoutPlacement placement,
            IReadOnlyList<UIElement> children,
            IReadOnlyDictionary<string, int> keyedChildren)
        {
            if (!string.IsNullOrWhiteSpace(placement.Key)
                && keyedChildren.TryGetValue(placement.Key, out var keyedIndex)
                && keyedIndex >= 0
                && keyedIndex < children.Count)
            {
                return keyedIndex;
            }

            return placement.Index >= 0 && placement.Index < children.Count ? placement.Index : -1;
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
}
