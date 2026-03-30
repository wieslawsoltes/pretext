using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pretext.Uno;
using Windows.Foundation;

namespace PretextSamples.Samples;

internal static class SampleTheme
{
    public static readonly SolidColorBrush PageBrush = Brush(0xF5, 0xF2, 0xEC);
    public static readonly SolidColorBrush PanelBrush = Brush(0xFF, 0xFD, 0xF9);
    public static readonly SolidColorBrush InkBrush = Brush(0x20, 0x1B, 0x18);
    public static readonly SolidColorBrush MutedBrush = Brush(0x6D, 0x64, 0x5D);
    public static readonly SolidColorBrush RuleBrush = Brush(0xD8, 0xCE, 0xC3);
    public static readonly SolidColorBrush AccentBrush = Brush(0x95, 0x5F, 0x3B);
    public static readonly SolidColorBrush AccentSoftBrush = Brush(0xF0, 0xE4, 0xDA);
    public static readonly SolidColorBrush ChatBackgroundBrush = Brush(0x1C, 0x1C, 0x1E);
    public static readonly SolidColorBrush SentBubbleBrush = Brush(0x0B, 0x84, 0xFE);
    public static readonly SolidColorBrush ReceiveBubbleBrush = Brush(0x2C, 0x2C, 0x2E);
    public static readonly SolidColorBrush WhiteBrush = Brush(0xFF, 0xFF, 0xFF);

    public static SolidColorBrush Brush(byte r, byte g, byte b) => new(ColorHelper.FromArgb(255, r, g, b));

    public static SolidColorBrush Brush(byte a, byte r, byte g, byte b) => new(ColorHelper.FromArgb(a, r, g, b));
}

internal readonly record struct WrapMetrics(int LineCount, double Height, double MaxLineWidth);

internal readonly record struct PositionedLine(string Text, double X, double Y, double Width);

internal readonly record struct RectObstacle(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

internal readonly record struct CircleObstacle(double X, double Y, double Radius);

internal readonly record struct Interval(double Left, double Right)
{
    public double Width => Right - Left;
}

internal readonly record struct VerticalBand(int StartIndex, int EndIndexExclusive, double Top, double Bottom);

internal readonly record struct VerticalOcclusionRange(
    int StartIndex,
    int EndIndexExclusive,
    int StartBandIndex,
    int EndBandIndexExclusive);

internal sealed class VerticalOcclusionIndex
{
    private readonly IReadOnlyList<VerticalBand> _bands;

    public VerticalOcclusionIndex(IReadOnlyList<VerticalBand> bands)
    {
        _bands = bands;
    }

    public int BandCount => _bands.Count;

    public bool TryQuery(double top, double bottom, out VerticalOcclusionRange range)
    {
        range = default;
        if (_bands.Count == 0 || bottom < 0)
        {
            return false;
        }

        var firstBand = FindFirstBandEndingAfter(top);
        if (firstBand >= _bands.Count)
        {
            return false;
        }

        var endBandExclusive = FindFirstBandStartingAfter(bottom);
        if (endBandExclusive <= firstBand)
        {
            return false;
        }

        range = new VerticalOcclusionRange(
            _bands[firstBand].StartIndex,
            _bands[endBandExclusive - 1].EndIndexExclusive,
            firstBand,
            endBandExclusive);
        return true;
    }

    private int FindFirstBandEndingAfter(double top)
    {
        var lo = 0;
        var hi = _bands.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_bands[mid].Bottom < top)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    private int FindFirstBandStartingAfter(double bottom)
    {
        var lo = 0;
        var hi = _bands.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_bands[mid].Top <= bottom)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}

internal static class SampleTextMetrics
{
    public static double MeasureMaxLineWidth(PreparedTextWithSegments prepared)
    {
        var max = 0d;
        PretextLayout.WalkLineRanges(prepared, 100_000, line =>
        {
            if (line.Width > max)
            {
                max = line.Width;
            }
        });
        return max;
    }

    public static WrapMetrics CollectWrapMetrics(PreparedTextWithSegments prepared, double maxWidth, double lineHeight)
    {
        var max = 0d;
        var count = PretextLayout.WalkLineRanges(prepared, maxWidth, line =>
        {
            if (line.Width > max)
            {
                max = line.Width;
            }
        });
        return new WrapMetrics(count, count * lineHeight, max);
    }

    public static WrapMetrics FindTightWrapMetrics(PreparedTextWithSegments prepared, double maxWidth, double lineHeight)
    {
        var initial = CollectWrapMetrics(prepared, maxWidth, lineHeight);
        var lo = 1;
        var hi = Math.Max(1, (int)Math.Ceiling(maxWidth));

        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            var result = PretextLayout.Layout(prepared, mid, lineHeight);
            if (result.LineCount <= initial.LineCount)
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return CollectWrapMetrics(prepared, lo, lineHeight);
    }

    public static bool IsEnd(PreparedTextWithSegments prepared, LayoutCursor cursor)
    {
        return cursor.SegmentIndex >= prepared.Segments.Count;
    }
}

internal static class SampleUi
{
    public static FrameworkElement CreatePageRoot(UIElement content)
    {
        return new StretchScrollHost(content);
    }

    public static StackPanel CreatePageStack()
    {
        return new StackPanel
        {
            Spacing = 18,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }

    public static StackPanel CreateHeader(string eyebrow, string title, string description)
    {
        var stack = new StackPanel
        {
            Spacing = 8,
            MaxWidth = 920,
        };

        stack.Children.Add(new TextBlock
        {
            Text = eyebrow,
            Foreground = SampleTheme.AccentBrush,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            CharacterSpacing = 120,
            TextWrapping = TextWrapping.NoWrap,
        });

        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = SampleTheme.InkBrush,
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Georgia"),
            TextWrapping = TextWrapping.WrapWholeWords,
        });

        stack.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = SampleTheme.MutedBrush,
            FontSize = 15,
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxWidth = 720,
        });

        return stack;
    }

    public static Border CreateCard(UIElement content, double padding = 18)
    {
        return new Border
        {
            Background = SampleTheme.PanelBrush,
            BorderBrush = SampleTheme.RuleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(padding),
            Child = content,
        };
    }

    public static TextBlock CreateBodyText(string text, double fontSize = 15)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = SampleTheme.MutedBrush,
            FontSize = fontSize,
            TextWrapping = TextWrapping.WrapWholeWords,
        };
    }

    public static TextBlock CreateCanvasLine(string text, string fontFamily, double fontSize, Brush brush, Windows.UI.Text.FontWeight? weight = null)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = brush,
            FontFamily = new FontFamily(fontFamily),
            FontSize = fontSize,
            FontWeight = weight ?? FontWeights.Normal,
            TextWrapping = TextWrapping.NoWrap,
        };
    }

    public static void EnsurePool<T>(Panel panel, List<T> pool, int count, Func<T> factory) where T : UIElement
    {
        while (pool.Count < count)
        {
            var element = factory();
            pool.Add(element);
            panel.Children.Add(element);
        }

        for (var index = 0; index < pool.Count; index++)
        {
            pool[index].Visibility = index < count ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

internal sealed class StretchScrollHost : Grid
{
    private readonly Border _contentHost;
    private readonly ScrollViewer _scrollViewer;

    internal ScrollViewer ScrollViewer => _scrollViewer;

    internal FrameworkElement ScrollContent => _contentHost;

    public StretchScrollHost(UIElement content)
    {
        Background = SampleTheme.PageBrush;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        _contentHost = new Border
        {
            Padding = new Thickness(28),
            Background = SampleTheme.PageBrush,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = content,
        };

        _scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _contentHost,
        };

        Children.Add(_scrollViewer);

        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _contentHost.Width = Math.Max(0, e.NewSize.Width);
    }

    internal bool TryGetLocalViewportBounds(FrameworkElement target, double overscan, out double top, out double bottom)
    {
        top = 0;
        bottom = 0;
        if (target.ActualHeight <= 0)
        {
            return false;
        }

        var viewportHeight = _scrollViewer.ActualHeight > 0 ? _scrollViewer.ActualHeight : ActualHeight;
        if (viewportHeight <= 0)
        {
            return false;
        }

        try
        {
            var origin = target.TransformToVisual(_contentHost).TransformPoint(new Point(0, 0));
            top = Math.Max(0, _scrollViewer.VerticalOffset - origin.Y - overscan);
            bottom = _scrollViewer.VerticalOffset + viewportHeight - origin.Y + overscan;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class UiRenderScheduler
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Action _action;
    private bool _scheduled;

    public UiRenderScheduler(DispatcherQueue dispatcherQueue, Action action)
    {
        _dispatcherQueue = dispatcherQueue;
        _action = action;
    }

    public void Schedule()
    {
        if (_scheduled)
        {
            return;
        }

        _scheduled = true;
        _dispatcherQueue.TryEnqueue(() =>
        {
            _scheduled = false;
            _action();
        });
    }
}

internal static class FlowLayoutHelper
{
    public static List<PositionedLine> LayoutIntoColumns(
        PreparedTextWithSegments prepared,
        IReadOnlyList<RectObstacle> columns,
        IReadOnlyList<RectObstacle> rectangles,
        IReadOnlyList<CircleObstacle> circles,
        double lineHeight,
        double minSlotWidth = 56)
    {
        var lines = new List<PositionedLine>();
        var cursor = new LayoutCursor(0, 0);

        foreach (var column in columns)
        {
            var y = column.Y;
            while (!SampleTextMetrics.IsEnd(prepared, cursor) && y + lineHeight <= column.Bottom)
            {
                var slot = GetBestSlot(column, y, y + lineHeight, rectangles, circles, minSlotWidth);
                if (slot is null)
                {
                    y += lineHeight;
                    continue;
                }

                var nextLine = PretextLayout.LayoutNextLine(prepared, cursor, slot.Value.Width);
                if (nextLine is null)
                {
                    return lines;
                }

                lines.Add(new PositionedLine(nextLine.Text, slot.Value.Left, y, nextLine.Width));
                cursor = nextLine.End;
                y += lineHeight;
            }
        }

        return lines;
    }

    private static Interval? GetBestSlot(
        RectObstacle column,
        double bandTop,
        double bandBottom,
        IReadOnlyList<RectObstacle> rectangles,
        IReadOnlyList<CircleObstacle> circles,
        double minSlotWidth)
    {
        var slots = new List<Interval> { new(column.X, column.Right) };
        foreach (var rect in rectangles)
        {
            if (bandBottom <= rect.Y || bandTop >= rect.Bottom)
            {
                continue;
            }

            slots = Carve(slots, new Interval(rect.X, rect.Right));
        }

        foreach (var circle in circles)
        {
            var centerY = (bandTop + bandBottom) * 0.5;
            var dy = Math.Abs(centerY - circle.Y);
            if (dy >= circle.Radius)
            {
                continue;
            }

            var dx = Math.Sqrt(circle.Radius * circle.Radius - dy * dy);
            slots = Carve(slots, new Interval(circle.X - dx, circle.X + dx));
        }

        return slots
            .Where(slot => slot.Width >= minSlotWidth)
            .OrderByDescending(slot => slot.Width)
            .FirstOrDefault();
    }

    private static List<Interval> Carve(List<Interval> slots, Interval blocked)
    {
        var next = new List<Interval>();
        foreach (var slot in slots)
        {
            if (blocked.Right <= slot.Left || blocked.Left >= slot.Right)
            {
                next.Add(slot);
                continue;
            }

            if (blocked.Left > slot.Left)
            {
                next.Add(new Interval(slot.Left, blocked.Left));
            }

            if (blocked.Right < slot.Right)
            {
                next.Add(new Interval(blocked.Right, slot.Right));
            }
        }

        return next;
    }
}

internal static class ObstacleLayoutHelper
{
    public static List<Interval> CarveTextLineSlots(Interval baseSlot, IEnumerable<Interval> blocked, double minSlotWidth = 50)
    {
        var slots = new List<Interval> { baseSlot };
        foreach (var interval in blocked)
        {
            var next = new List<Interval>();
            foreach (var slot in slots)
            {
                if (interval.Right <= slot.Left || interval.Left >= slot.Right)
                {
                    next.Add(slot);
                    continue;
                }

                if (interval.Left > slot.Left)
                {
                    next.Add(new Interval(slot.Left, interval.Left));
                }

                if (interval.Right < slot.Right)
                {
                    next.Add(new Interval(interval.Right, slot.Right));
                }
            }

            slots = next;
            if (slots.Count == 0)
            {
                break;
            }
        }

        return slots.Where(slot => slot.Width >= minSlotWidth).ToList();
    }

    public static Interval? CircleIntervalForBand(
        double cx,
        double cy,
        double radius,
        double bandTop,
        double bandBottom,
        double horizontalPadding = 0,
        double verticalPadding = 0)
    {
        var top = bandTop - verticalPadding;
        var bottom = bandBottom + verticalPadding;
        if (top >= cy + radius || bottom <= cy - radius)
        {
            return null;
        }

        var minDy = cy >= top && cy <= bottom ? 0 : cy < top ? top - cy : cy - bottom;
        if (minDy >= radius)
        {
            return null;
        }

        var maxDx = Math.Sqrt(radius * radius - minDy * minDy);
        return new Interval(cx - maxDx - horizontalPadding, cx + maxDx + horizontalPadding);
    }

    public static Interval? EllipseIntervalForBand(
        double cx,
        double cy,
        double radiusX,
        double radiusY,
        double bandTop,
        double bandBottom,
        double horizontalPadding = 0,
        double verticalPadding = 0)
    {
        var top = bandTop - verticalPadding;
        var bottom = bandBottom + verticalPadding;
        if (top >= cy + radiusY || bottom <= cy - radiusY)
        {
            return null;
        }

        var minDy = cy >= top && cy <= bottom ? 0 : cy < top ? top - cy : cy - bottom;
        if (minDy >= radiusY)
        {
            return null;
        }

        var normalizedDy = minDy / radiusY;
        var maxDx = radiusX * Math.Sqrt(1 - normalizedDy * normalizedDy);
        return new Interval(cx - maxDx - horizontalPadding, cx + maxDx + horizontalPadding);
    }

    public static List<Interval> GetRectIntervalsForBand(
        IReadOnlyList<RectObstacle> rects,
        double bandTop,
        double bandBottom,
        double horizontalPadding = 0,
        double verticalPadding = 0)
    {
        var intervals = new List<Interval>(rects.Count);
        foreach (var rect in rects)
        {
            if (bandBottom <= rect.Y - verticalPadding || bandTop >= rect.Bottom + verticalPadding)
            {
                continue;
            }

            intervals.Add(new Interval(rect.X - horizontalPadding, rect.Right + horizontalPadding));
        }

        return intervals;
    }

    public static Interval PickSlot(IReadOnlyList<Interval> slots, bool preferRightOnTie)
    {
        var best = slots[0];
        for (var index = 1; index < slots.Count; index++)
        {
            var candidate = slots[index];
            if (candidate.Width > best.Width)
            {
                best = candidate;
                continue;
            }

            if (candidate.Width < best.Width)
            {
                continue;
            }

            if (preferRightOnTie)
            {
                if (candidate.Left > best.Left)
                {
                    best = candidate;
                }
            }
            else if (candidate.Left < best.Left)
            {
                best = candidate;
            }
        }

        return best;
    }
}
