using Avalonia;
using Avalonia.Controls;
using Pretext.LayoutFramework;

namespace Pretext.Avalonia.Controls;

public class PretextControlPanel : Panel
{
    public static readonly StyledProperty<IPretextControlLayoutSource?> SourceProperty =
        AvaloniaProperty.Register<PretextControlPanel, IPretextControlLayoutSource?>(nameof(Source));

    public static readonly AttachedProperty<string?> LayoutKeyProperty =
        AvaloniaProperty.RegisterAttached<PretextControlPanel, Control, string?>("LayoutKey");

    private readonly PreparedLayoutController<PreparedControlModel> _controller = new();

    public LayoutDiagnosticsSnapshot Diagnostics => _controller.Snapshot;

    static PretextControlPanel()
    {
        SourceProperty.Changed.AddClassHandler<PretextControlPanel>((panel, _) => panel.OnSourceChanged());
    }

    public IPretextControlLayoutSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static void SetLayoutKey(Control control, string? value)
    {
        control.SetValue(LayoutKeyProperty, value);
    }

    public static string? GetLayoutKey(Control control)
    {
        return control.GetValue(LayoutKeyProperty);
    }

    protected virtual LayoutConstraints CreateConstraints(Size availableSize)
    {
        return new LayoutConstraints(availableSize.Width, availableSize.Height);
    }

    protected override void ChildrenChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        _controller.Reset();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var source = ResolveSource();
        if (source is null)
        {
            MeasureFallbackChildren();
            return default;
        }

        var solved = _controller.EnsureMeasuredLayout(
            source.GetLayoutFingerprint(),
            CreateConstraints(availableSize),
            source.Prepare,
            source.Solve);

        MeasureChildren(solved);
        return ToSize(solved.Extent);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var source = ResolveSource();
        if (source is null)
        {
            ArrangeFallbackChildren();
            return finalSize;
        }

        var solved = _controller.EnsureArrangedLayout(
            source.GetLayoutFingerprint(),
            CreateConstraints(finalSize),
            source.Prepare,
            source.Solve);

        ArrangeChildren(solved);
        return ToSize(solved.Extent);
    }

    private void OnSourceChanged()
    {
        _controller.Reset();
        InvalidateMeasure();
    }

    private IPretextControlLayoutSource? ResolveSource()
    {
        return Source ?? TemplatedParent as IPretextControlLayoutSource;
    }

    private void MeasureFallbackChildren()
    {
        foreach (var child in Children)
        {
            child.Measure(default);
        }
    }

    private void ArrangeFallbackChildren()
    {
        foreach (var child in Children)
        {
            child.Arrange(default);
        }
    }

    private void MeasureChildren(SolvedLayout solved)
    {
        var touched = new bool[Children.Count];
        var keyedChildren = BuildKeyMap();

        foreach (var placement in solved.Placements)
        {
            var child = ResolveChild(placement, keyedChildren);
            if (child is null)
            {
                continue;
            }

            touched[Children.IndexOf(child)] = true;
            child.Measure(ToSize(placement.Bounds.Size));
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
        var keyedChildren = BuildKeyMap();

        foreach (var placement in solved.Placements)
        {
            var child = ResolveChild(placement, keyedChildren);
            if (child is null)
            {
                continue;
            }

            touched[Children.IndexOf(child)] = true;
            child.Arrange(ToRect(placement.Bounds));
        }

        for (var index = 0; index < Children.Count; index++)
        {
            if (!touched[index])
            {
                Children[index].Arrange(default);
            }
        }
    }

    private Dictionary<string, Control> BuildKeyMap()
    {
        var result = new Dictionary<string, Control>(StringComparer.Ordinal);
        foreach (var child in Children)
        {
            var key = GetLayoutKey(child) ?? child.Name;
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = child;
            }
        }

        return result;
    }

    private Control? ResolveChild(LayoutPlacement placement, IReadOnlyDictionary<string, Control> keyedChildren)
    {
        if (!string.IsNullOrWhiteSpace(placement.Key) && keyedChildren.TryGetValue(placement.Key, out var child))
        {
            return child;
        }

        return placement.Index >= 0 && placement.Index < Children.Count ? Children[placement.Index] : null;
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
