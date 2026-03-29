using System.Text;

namespace Pretext.Uno;

public static partial class PretextLayout
{
    private readonly record struct BreakCandidate(LayoutCursor VisibleEnd, LayoutCursor End, double Width, bool AppendHyphen);

    private readonly record struct InternalLine(LayoutCursor Start, LayoutCursor VisibleEnd, LayoutCursor End, double Width, bool AppendHyphen);

    private static bool TryStepLine(PreparedText prepared, LayoutCursor start, double maxWidth, out InternalLine line)
    {
        line = default;
        var normalized = NormalizeLineStart(prepared, start);
        if (normalized is null)
        {
            return false;
        }

        var cursor = normalized.Value;
        var segments = prepared.SegmentsInternal;
        if (cursor.SegmentIndex >= segments.Count)
        {
            return false;
        }

        var epsilon = GetEngineProfile().LineFitEpsilon;
        var startSegment = segments[cursor.SegmentIndex];
        if (startSegment.Kind == SegmentBreakKind.HardBreak && cursor.GraphemeIndex == 0)
        {
            line = new InternalLine(cursor, cursor, new LayoutCursor(cursor.SegmentIndex + 1, 0), 0, AppendHyphen: false);
            return true;
        }

        var currentWidth = 0d;
        var hasContent = false;
        BreakCandidate? bestBreak = null;
        var segmentIndex = cursor.SegmentIndex;
        var graphemeIndex = cursor.GraphemeIndex;

        while (segmentIndex < segments.Count)
        {
            var segment = segments[segmentIndex];
            switch (segment.Kind)
            {
                case SegmentBreakKind.HardBreak:
                    line = new InternalLine(
                        cursor,
                        new LayoutCursor(segmentIndex, 0),
                        new LayoutCursor(segmentIndex + 1, 0),
                        currentWidth,
                        AppendHyphen: false);
                    return true;

                case SegmentBreakKind.ZeroWidthBreak:
                    if (hasContent)
                    {
                        bestBreak = new BreakCandidate(
                            new LayoutCursor(segmentIndex, 0),
                            new LayoutCursor(segmentIndex + 1, 0),
                            currentWidth,
                            AppendHyphen: false);
                    }

                    segmentIndex++;
                    graphemeIndex = 0;
                    continue;

                case SegmentBreakKind.SoftHyphen:
                    if (hasContent)
                    {
                        bestBreak = new BreakCandidate(
                            new LayoutCursor(segmentIndex, 0),
                            new LayoutCursor(segmentIndex + 1, 0),
                            currentWidth + prepared.DiscretionaryHyphenWidth,
                            AppendHyphen: true);
                    }

                    segmentIndex++;
                    graphemeIndex = 0;
                    continue;

                case SegmentBreakKind.Space:
                {
                    if (!hasContent)
                    {
                        segmentIndex++;
                        graphemeIndex = 0;
                        continue;
                    }

                    var advance = segment.Width;
                    if (currentWidth + advance > maxWidth + epsilon)
                    {
                        line = new InternalLine(
                            cursor,
                            new LayoutCursor(segmentIndex, 0),
                            new LayoutCursor(segmentIndex + 1, 0),
                            currentWidth,
                            AppendHyphen: false);
                        return true;
                    }

                    bestBreak = new BreakCandidate(
                        new LayoutCursor(segmentIndex, 0),
                        new LayoutCursor(segmentIndex + 1, 0),
                        currentWidth,
                        AppendHyphen: false);
                    currentWidth += advance;
                    hasContent = true;
                    segmentIndex++;
                    graphemeIndex = 0;
                    continue;
                }

                case SegmentBreakKind.Tab:
                {
                    var advance = GetTabAdvance(currentWidth, prepared.TabStopAdvance);
                    if (hasContent && currentWidth + advance > maxWidth + epsilon)
                    {
                        line = new InternalLine(
                            cursor,
                            new LayoutCursor(segmentIndex + 1, 0),
                            new LayoutCursor(segmentIndex + 1, 0),
                            currentWidth + advance,
                            AppendHyphen: false);
                        return true;
                    }

                    currentWidth += advance;
                    hasContent = true;
                    bestBreak = new BreakCandidate(
                        new LayoutCursor(segmentIndex + 1, 0),
                        new LayoutCursor(segmentIndex + 1, 0),
                        currentWidth,
                        AppendHyphen: false);
                    segmentIndex++;
                    graphemeIndex = 0;
                    continue;
                }

                case SegmentBreakKind.Glue:
                case SegmentBreakKind.Text:
                {
                    var fullAdvance = segment.GetWidthFrom(graphemeIndex);
                    if (hasContent && bestBreak is not null && currentWidth + fullAdvance > maxWidth + epsilon)
                    {
                        var chosen = bestBreak.Value;
                        if (chosen.AppendHyphen && segment.Kind == SegmentBreakKind.Text && segment.IsBreakableRun)
                        {
                            var (softFitCount, softFittedWidth) = FitSoftHyphenBreak(
                                segment,
                                graphemeIndex,
                                currentWidth,
                                maxWidth,
                                epsilon,
                                prepared.DiscretionaryHyphenWidth);
                            if (softFitCount > 0)
                            {
                                var consumedAll = graphemeIndex + softFitCount >= segment.GraphemeCount;
                                var softEnd = consumedAll
                                    ? new LayoutCursor(segmentIndex + 1, 0)
                                    : new LayoutCursor(segmentIndex, graphemeIndex + softFitCount);
                                line = new InternalLine(
                                    cursor,
                                    softEnd,
                                    softEnd,
                                    softFittedWidth + (consumedAll ? 0 : prepared.DiscretionaryHyphenWidth),
                                    AppendHyphen: !consumedAll);
                                return true;
                            }
                        }

                        line = new InternalLine(cursor, chosen.VisibleEnd, chosen.End, chosen.Width, chosen.AppendHyphen);
                        return true;
                    }

                    if (currentWidth + fullAdvance <= maxWidth + epsilon)
                    {
                        currentWidth += fullAdvance;
                        hasContent = true;
                        segmentIndex++;
                        graphemeIndex = 0;
                        continue;
                    }

                    var fitCount = segment.IsBreakableRun
                        ? FitGraphemes(segment, graphemeIndex, maxWidth - currentWidth, epsilon)
                        : 0;
                    if (fitCount <= 0 && bestBreak is not null)
                    {
                        var chosen = bestBreak.Value;
                        line = new InternalLine(cursor, chosen.VisibleEnd, chosen.End, chosen.Width, chosen.AppendHyphen);
                        return true;
                    }

                    if (fitCount <= 0)
                    {
                        fitCount = 1;
                    }

                    var fittedWidth = currentWidth + segment.GetWidthRange(graphemeIndex, fitCount);
                    var end = new LayoutCursor(segmentIndex, graphemeIndex + fitCount);
                    line = new InternalLine(cursor, end, end, fittedWidth, AppendHyphen: false);
                    return true;
                }

                case SegmentBreakKind.PreservedSpace:
                {
                    var fullAdvance = segment.GetWidthFrom(graphemeIndex);
                    if (hasContent && currentWidth + fullAdvance > maxWidth + epsilon)
                    {
                        line = new InternalLine(
                            cursor,
                            new LayoutCursor(segmentIndex + 1, 0),
                            new LayoutCursor(segmentIndex + 1, 0),
                            currentWidth + fullAdvance,
                            AppendHyphen: false);
                        return true;
                    }

                    currentWidth += fullAdvance;
                    hasContent = true;
                    segmentIndex++;
                    graphemeIndex = 0;
                    bestBreak = new BreakCandidate(
                        new LayoutCursor(segmentIndex, 0),
                        new LayoutCursor(segmentIndex, 0),
                        currentWidth,
                        AppendHyphen: false);
                    continue;
                }

                default:
                    throw new InvalidOperationException($"Unknown segment kind: {segment.Kind}");
            }
        }

        if (!hasContent)
        {
            return false;
        }

        var finalCursor = new LayoutCursor(segments.Count, 0);
        line = new InternalLine(cursor, finalCursor, finalCursor, currentWidth, AppendHyphen: false);
        return true;
    }

    private static LayoutCursor? NormalizeLineStart(PreparedText prepared, LayoutCursor start)
    {
        var segments = prepared.SegmentsInternal;
        if (start.GraphemeIndex > 0)
        {
            return start;
        }

        var index = start.SegmentIndex;
        while (index < segments.Count)
        {
            var kind = segments[index].Kind;
            if (kind == SegmentBreakKind.Space || kind == SegmentBreakKind.ZeroWidthBreak || kind == SegmentBreakKind.SoftHyphen)
            {
                index++;
                continue;
            }

            return new LayoutCursor(index, 0);
        }

        return null;
    }

    private static int FitGraphemes(PreparedSegment segment, int startIndex, double availableWidth, double epsilon)
    {
        var remaining = segment.GraphemeCount - startIndex;
        if (remaining <= 0)
        {
            return 0;
        }

        var fitCount = 0;
        for (var i = 1; i <= remaining; i++)
        {
            var width = segment.GetWidthRange(startIndex, i);
            if (width > availableWidth + epsilon)
            {
                break;
            }

            fitCount = i;
        }

        return fitCount;
    }

    private static (int FitCount, double FittedWidth) FitSoftHyphenBreak(
        PreparedSegment segment,
        int startIndex,
        double initialWidth,
        double maxWidth,
        double epsilon,
        double discretionaryHyphenWidth)
    {
        var remaining = segment.GraphemeCount - startIndex;
        if (remaining <= 0)
        {
            return (0, initialWidth);
        }

        var fitCount = 0;
        var fittedWidth = initialWidth;
        for (var i = 1; i <= remaining; i++)
        {
            var nextWidth = initialWidth + segment.GetWidthRange(startIndex, i);
            var nextLineWidth = i < remaining ? nextWidth + discretionaryHyphenWidth : nextWidth;
            if (nextLineWidth > maxWidth + epsilon)
            {
                break;
            }

            fitCount = i;
            fittedWidth = nextWidth;
        }

        return (fitCount, fittedWidth);
    }

    private static LayoutLine MaterializeLine(PreparedTextWithSegments prepared, InternalLine line)
    {
        var text = BuildLineText(prepared, line);
        return new LayoutLine(text, line.Width, line.Start, line.End);
    }

    private static string BuildLineText(PreparedText prepared, InternalLine line)
    {
        if (line.Start == line.End && !line.AppendHyphen)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var segments = prepared.SegmentsInternal;
        var segmentIndex = line.Start.SegmentIndex;
        var graphemeIndex = line.Start.GraphemeIndex;
        var endSegmentIndex = line.End.SegmentIndex;
        var endGraphemeIndex = line.End.GraphemeIndex;

        while (segmentIndex < segments.Count)
        {
            if (segmentIndex > endSegmentIndex)
            {
                break;
            }

            var segment = segments[segmentIndex];
            if (segmentIndex == endSegmentIndex && graphemeIndex == endGraphemeIndex)
            {
                break;
            }

            if (segment.Kind is SegmentBreakKind.ZeroWidthBreak or SegmentBreakKind.SoftHyphen or SegmentBreakKind.HardBreak)
            {
                segmentIndex++;
                graphemeIndex = 0;
                continue;
            }

            if (segmentIndex == endSegmentIndex)
            {
                if (line.AppendHyphen)
                {
                    builder.Append('-');
                }

                builder.Append(segment.GetSlice(graphemeIndex, endGraphemeIndex));
                break;
            }

            builder.Append(segment.GetSlice(graphemeIndex, segment.GraphemeCount));
            segmentIndex++;
            graphemeIndex = 0;
        }

        if (line.AppendHyphen && endGraphemeIndex == 0)
        {
            builder.Append('-');
        }

        return builder.ToString();
    }

    private static double GetTabAdvance(double lineWidth, double tabStopAdvance)
    {
        if (tabStopAdvance <= 0)
        {
            return 0;
        }

        var remainder = lineWidth % tabStopAdvance;
        if (Math.Abs(remainder) <= 1e-6)
        {
            return tabStopAdvance;
        }

        return tabStopAdvance - remainder;
    }
}
