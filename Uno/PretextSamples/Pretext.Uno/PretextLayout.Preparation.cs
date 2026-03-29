using System.Text;

namespace Pretext.Uno;

public static partial class PretextLayout
{
    private static PreparedText PrepareCore(string text, string font, PrepareOptions options, bool includeSegments)
    {
        var fontState = GetFontState(font);
        var tokens = AnalyzeTokens(text ?? string.Empty, options.WhiteSpace);
        if (tokens.Count == 0)
        {
            return includeSegments
                ? new PreparedTextWithSegments(
                    font,
                    options.WhiteSpace,
                    Array.Empty<PreparedSegment>(),
                    fontState.HyphenWidth,
                    fontState.TabStopAdvance,
                    Array.Empty<string>(),
                    Array.Empty<double>(),
                    Array.Empty<double>(),
                    Array.Empty<double>(),
                    Array.Empty<SegmentBreakKind>(),
                    Array.Empty<IReadOnlyList<double>?>(),
                    Array.Empty<IReadOnlyList<double>?>(),
                    Array.Empty<PreparedLineChunk>(),
                    true,
                    null)
                : new PreparedText(
                    font,
                    options.WhiteSpace,
                    Array.Empty<PreparedSegment>(),
                    fontState.HyphenWidth,
                    fontState.TabStopAdvance,
                    Array.Empty<PreparedLineChunk>(),
                    true);
        }

        var preparedSegments = new List<PreparedSegment>(tokens.Count);
        var segmentTexts = includeSegments ? new List<string>(tokens.Count) : null;
        var widths = includeSegments ? new List<double>(tokens.Count) : null;
        var lineEndFitAdvances = includeSegments ? new List<double>(tokens.Count) : null;
        var lineEndPaintAdvances = includeSegments ? new List<double>(tokens.Count) : null;
        var kinds = includeSegments ? new List<SegmentBreakKind>(tokens.Count) : null;
        var breakableWidths = includeSegments ? new List<IReadOnlyList<double>?>(tokens.Count) : null;
        var breakablePrefixWidths = includeSegments ? new List<IReadOnlyList<double>?>(tokens.Count) : null;
        var normalized = new StringBuilder();
        var starts = new List<int>(tokens.Count);
        var simpleLineWalkFastPath = true;
        var engineProfile = GetEngineProfile();

        foreach (var token in tokens)
        {
            foreach (var segment in ExpandPreparedSegments(token, fontState, engineProfile))
            {
                starts.Add(normalized.Length);
                normalized.Append(segment.Text);
                preparedSegments.Add(segment);

                simpleLineWalkFastPath &= segment.Kind is SegmentBreakKind.Text or SegmentBreakKind.Space or SegmentBreakKind.ZeroWidthBreak;

                if (includeSegments)
                {
                    segmentTexts!.Add(segment.Text);
                    widths!.Add(segment.Width);
                    lineEndFitAdvances!.Add(GetLineEndFitAdvance(segment, fontState.HyphenWidth));
                    lineEndPaintAdvances!.Add(GetLineEndPaintAdvance(segment, fontState.HyphenWidth));
                    kinds!.Add(segment.Kind);
                    breakableWidths!.Add(GetBreakableWidths(segment));
                    breakablePrefixWidths!.Add(GetBreakablePrefixWidths(segment));
                }
            }
        }

        var chunks = BuildChunks(preparedSegments);
        simpleLineWalkFastPath &= chunks.Count <= 1;

        if (includeSegments)
        {
            var levels = BidiHelper.ComputeSegmentLevels(normalized.ToString(), starts);
            return new PreparedTextWithSegments(
                font,
                options.WhiteSpace,
                preparedSegments.AsReadOnly(),
                fontState.HyphenWidth,
                fontState.TabStopAdvance,
                segmentTexts!.AsReadOnly(),
                widths!.AsReadOnly(),
                lineEndFitAdvances!.AsReadOnly(),
                lineEndPaintAdvances!.AsReadOnly(),
                kinds!.AsReadOnly(),
                breakableWidths!.AsReadOnly(),
                breakablePrefixWidths!.AsReadOnly(),
                chunks.AsReadOnly(),
                simpleLineWalkFastPath,
                levels is null ? null : Array.AsReadOnly(levels));
        }

        return new PreparedText(
            font,
            options.WhiteSpace,
            preparedSegments.AsReadOnly(),
            fontState.HyphenWidth,
            fontState.TabStopAdvance,
            chunks.AsReadOnly(),
            simpleLineWalkFastPath);
    }

    private static FontState GetFontState(string font)
    {
        lock (FontStateGate)
        {
            if (FontStates.TryGetValue(font, out var cached))
            {
                return cached;
            }

            var state = FontState.Create(font);
            FontStates[font] = state;
            return state;
        }
    }

    private static IEnumerable<PreparedSegment> ExpandPreparedSegments(AnalysisToken token, FontState fontState, EngineProfile profile)
    {
        switch (token.Kind)
        {
            case SegmentBreakKind.HardBreak:
                yield return new PreparedSegment(token.Text, token.Kind, false, 0, Array.Empty<string>(), null);
                yield break;

            case SegmentBreakKind.ZeroWidthBreak:
                yield return new PreparedSegment(token.Text, token.Kind, false, 0, Array.Empty<string>(), null);
                yield break;

            case SegmentBreakKind.SoftHyphen:
                yield return new PreparedSegment(token.Text, token.Kind, false, 0, Array.Empty<string>(), null);
                yield break;

            case SegmentBreakKind.Tab:
                yield return new PreparedSegment(token.Text, token.Kind, false, 0, ["\t"], null);
                yield break;
        }

        if (token.Kind == SegmentBreakKind.Text && ContainsCjk(token.Text))
        {
            foreach (var unit in SplitMeasuredCjkRun(token.Text, profile.CarryCjkAfterClosingQuote))
            {
                yield return fontState.MeasureSegment(unit, token.Kind, isBreakableRun: false);
            }

            yield break;
        }

        yield return fontState.MeasureSegment(
            token.Text,
            token.Kind,
            isBreakableRun: token.Kind == SegmentBreakKind.Text && token.IsWordLike && token.Text.Length > 1);
    }

    private static IReadOnlyList<double>? GetBreakableWidths(PreparedSegment segment)
    {
        if (segment.Kind != SegmentBreakKind.Text || !segment.IsBreakableRun || segment.GraphemeCount <= 1 || segment.PrefixWidths is null)
        {
            return null;
        }

        var widths = new double[segment.GraphemeCount];
        for (var index = 0; index < widths.Length; index++)
        {
            widths[index] = segment.GetWidthRange(index, 1);
        }

        return Array.AsReadOnly(widths);
    }

    private static IReadOnlyList<double>? GetBreakablePrefixWidths(PreparedSegment segment)
    {
        if (segment.Kind != SegmentBreakKind.Text || !segment.IsBreakableRun || segment.GraphemeCount <= 1 || segment.PrefixWidths is null)
        {
            return null;
        }

        return Array.AsReadOnly(segment.PrefixWidths);
    }

    private static double GetLineEndFitAdvance(PreparedSegment segment, double hyphenWidth)
    {
        return segment.Kind switch
        {
            SegmentBreakKind.Space => 0,
            SegmentBreakKind.PreservedSpace => 0,
            SegmentBreakKind.ZeroWidthBreak => 0,
            SegmentBreakKind.SoftHyphen => hyphenWidth,
            SegmentBreakKind.Tab => 0,
            SegmentBreakKind.HardBreak => 0,
            _ => segment.Width,
        };
    }

    private static double GetLineEndPaintAdvance(PreparedSegment segment, double hyphenWidth)
    {
        return segment.Kind switch
        {
            SegmentBreakKind.Space => 0,
            SegmentBreakKind.ZeroWidthBreak => 0,
            SegmentBreakKind.SoftHyphen => hyphenWidth,
            SegmentBreakKind.Tab => 0,
            SegmentBreakKind.HardBreak => 0,
            _ => segment.Width,
        };
    }

    private static List<PreparedLineChunk> BuildChunks(IReadOnlyList<PreparedSegment> segments)
    {
        var chunks = new List<PreparedLineChunk>();
        if (segments.Count == 0)
        {
            return chunks;
        }

        var chunkStart = 0;
        for (var index = 0; index < segments.Count; index++)
        {
            if (segments[index].Kind != SegmentBreakKind.HardBreak)
            {
                continue;
            }

            chunks.Add(new PreparedLineChunk(chunkStart, index, index + 1));
            chunkStart = index + 1;
        }

        chunks.Add(new PreparedLineChunk(chunkStart, segments.Count, segments.Count));
        return chunks;
    }
}
