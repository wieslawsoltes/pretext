using System.Collections.ObjectModel;
using Pretext.Uno;

namespace Pretext.LayoutFramework;

public enum PreparedNodeKind
{
    Fixed,
    Text,
    Group,
    Spacer,
    Overlay,
    Custom,
}

public enum ControlSlotKind
{
    Content,
    Text,
    Icon,
    Accessory,
    Header,
    Body,
    Footer,
    Overlay,
    Custom,
}

public enum PreparedItemKind
{
    Default,
    Fixed,
    Text,
    GroupHeader,
    Separator,
    Custom,
}

public readonly record struct PreparedNodeMetrics(
    LayoutSize IntrinsicSize,
    LayoutSize MinSize,
    LayoutSize MaxSize)
{
    public static PreparedNodeMetrics Fixed(double width, double height)
    {
        var size = new LayoutSize(width, height);
        return new PreparedNodeMetrics(size, size, size);
    }

    public static PreparedNodeMetrics Flexible(LayoutSize intrinsicSize, LayoutSize minSize)
    {
        return new PreparedNodeMetrics(intrinsicSize, minSize, LayoutSize.Infinite);
    }
}

public sealed record PreparedNode(
    string Key,
    PreparedNodeKind Kind,
    PreparedNodeMetrics Metrics,
    PreparedText? Text = null,
    PreparedTextWithSegments? RichText = null,
    object? DerivedState = null);

public sealed class PreparedPanelModel
{
    private readonly PreparedNode[] _nodes;

    public PreparedPanelModel(
        LayoutFingerprint fingerprint,
        IEnumerable<PreparedNode> nodes,
        LayoutSize chromeSize,
        object? derivedState = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        Fingerprint = fingerprint;
        _nodes = nodes.ToArray();
        Nodes = Array.AsReadOnly(_nodes);
        ChromeSize = chromeSize;
        DerivedState = derivedState;
    }

    public LayoutFingerprint Fingerprint { get; }

    public IReadOnlyList<PreparedNode> Nodes { get; }

    public LayoutSize ChromeSize { get; }

    public object? DerivedState { get; }
}

public sealed record PreparedControlSlot(
    string Key,
    ControlSlotKind Kind,
    PreparedNodeMetrics Metrics,
    PreparedText? Text = null,
    PreparedTextWithSegments? RichText = null,
    bool IsOptional = false,
    object? DerivedState = null);

public sealed class PreparedControlModel
{
    private readonly PreparedControlSlot[] _slots;

    public PreparedControlModel(
        LayoutFingerprint fingerprint,
        IEnumerable<PreparedControlSlot> slots,
        LayoutSize chromeSize,
        object? derivedState = null)
    {
        ArgumentNullException.ThrowIfNull(slots);

        Fingerprint = fingerprint;
        _slots = slots.ToArray();
        Slots = Array.AsReadOnly(_slots);
        ChromeSize = chromeSize;
        DerivedState = derivedState;
    }

    public LayoutFingerprint Fingerprint { get; }

    public IReadOnlyList<PreparedControlSlot> Slots { get; }

    public LayoutSize ChromeSize { get; }

    public object? DerivedState { get; }
}

public readonly record struct PreparedItemMetrics(
    PreparedItemKind Kind,
    double EstimatedWidth,
    double EstimatedHeight,
    int TemplateIndex = -1);

public sealed class PreparedItemsModel
{
    private readonly PreparedItemMetrics[] _items;
    private readonly PreparedText?[] _textHandles;
    private readonly PreparedTextWithSegments?[] _richTextHandles;

    public PreparedItemsModel(
        LayoutFingerprint fingerprint,
        ReadOnlyMemory<PreparedItemMetrics> items,
        ReadOnlyMemory<PreparedText?> textHandles = default,
        ReadOnlyMemory<PreparedTextWithSegments?> richTextHandles = default,
        object? derivedState = null)
    {
        var count = items.Length;
        if (!textHandles.IsEmpty && textHandles.Length != count)
        {
            throw new ArgumentException("Text handle count must match item count.", nameof(textHandles));
        }

        if (!richTextHandles.IsEmpty && richTextHandles.Length != count)
        {
            throw new ArgumentException("Rich text handle count must match item count.", nameof(richTextHandles));
        }

        Fingerprint = fingerprint;
        _items = items.ToArray();
        _textHandles = textHandles.IsEmpty ? [] : textHandles.ToArray();
        _richTextHandles = richTextHandles.IsEmpty ? [] : richTextHandles.ToArray();
        DerivedState = derivedState;
    }

    public LayoutFingerprint Fingerprint { get; }

    public int Count => _items.Length;

    public ReadOnlyMemory<PreparedItemMetrics> Items => _items;

    public ReadOnlyMemory<PreparedText?> TextHandles => _textHandles;

    public ReadOnlyMemory<PreparedTextWithSegments?> RichTextHandles => _richTextHandles;

    public object? DerivedState { get; }

    public PreparedItemMetrics GetItem(int index)
    {
        return _items[index];
    }

    public PreparedText? GetTextHandleOrDefault(int index)
    {
        return _textHandles.Length == 0 ? null : _textHandles[index];
    }

    public PreparedTextWithSegments? GetRichTextHandleOrDefault(int index)
    {
        return _richTextHandles.Length == 0 ? null : _richTextHandles[index];
    }
}
