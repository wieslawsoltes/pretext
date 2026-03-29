using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Pretext.Uno;

public enum WhiteSpaceMode
{
    Normal,
    PreWrap,
}

public enum SegmentBreakKind
{
    Text,
    Space,
    PreservedSpace,
    Tab,
    Glue,
    ZeroWidthBreak,
    SoftHyphen,
    HardBreak,
}

public readonly record struct PrepareOptions(WhiteSpaceMode WhiteSpace = WhiteSpaceMode.Normal);

public readonly record struct LayoutCursor(int SegmentIndex, int GraphemeIndex);

public readonly record struct LayoutResult(int LineCount, double Height);

public readonly record struct PrepareProfile(
    double AnalysisMs,
    double MeasureMs,
    double TotalMs,
    int AnalysisSegments,
    int PreparedSegments,
    int BreakableSegments);

public readonly record struct PreparedLineChunk(int StartSegmentIndex, int EndSegmentIndex, int ConsumedEndSegmentIndex);

public sealed record LayoutLine(string Text, double Width, LayoutCursor Start, LayoutCursor End);

public sealed record LayoutLineRange(double Width, LayoutCursor Start, LayoutCursor End);

public sealed record LayoutLinesResult(int LineCount, double Height, IReadOnlyList<LayoutLine> Lines);

internal sealed class PreparedSegment
{
    public PreparedSegment(string text, SegmentBreakKind kind, bool isBreakableRun, double width, string[] graphemes, double[]? prefixWidths)
    {
        Text = text;
        Kind = kind;
        IsBreakableRun = isBreakableRun;
        Width = width;
        Graphemes = graphemes;
        PrefixWidths = prefixWidths;
    }

    public string Text { get; }

    public SegmentBreakKind Kind { get; }

    public bool IsBreakableRun { get; }

    public double Width { get; }

    public string[] Graphemes { get; }

    public double[]? PrefixWidths { get; }

    public int GraphemeCount => Graphemes.Length;

    public double GetWidthFrom(int startIndex)
    {
        if (startIndex <= 0)
        {
            return Width;
        }

        return Width - GetPrefixWidth(startIndex - 1);
    }

    public double GetWidthRange(int startIndex, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        if (PrefixWidths is null)
        {
            return Width;
        }

        var endIndex = startIndex + count - 1;
        var end = GetPrefixWidth(endIndex);
        var start = startIndex > 0 ? GetPrefixWidth(startIndex - 1) : 0;
        return end - start;
    }

    public string GetSlice(int startIndex, int endIndexExclusive)
    {
        if (startIndex <= 0 && endIndexExclusive >= GraphemeCount)
        {
            return Text;
        }

        if (startIndex >= endIndexExclusive)
        {
            return string.Empty;
        }

        return string.Concat(Graphemes.Skip(startIndex).Take(endIndexExclusive - startIndex));
    }

    private double GetPrefixWidth(int index)
    {
        if (PrefixWidths is null)
        {
            return Width;
        }

        return PrefixWidths[index];
    }
}

public class PreparedText
{
    internal PreparedText(
        string font,
        WhiteSpaceMode whiteSpace,
        IReadOnlyList<PreparedSegment> segments,
        double hyphenWidth,
        double tabStopAdvance,
        IReadOnlyList<PreparedLineChunk> chunks,
        bool simpleLineWalkFastPath)
    {
        Font = font;
        WhiteSpace = whiteSpace;
        SegmentsInternal = segments;
        DiscretionaryHyphenWidth = hyphenWidth;
        TabStopAdvance = tabStopAdvance;
        ChunksInternal = chunks;
        SimpleLineWalkFastPathInternal = simpleLineWalkFastPath;
    }

    public string Font { get; }

    public WhiteSpaceMode WhiteSpace { get; }

    internal IReadOnlyList<PreparedSegment> SegmentsInternal { get; }

    internal IReadOnlyList<PreparedLineChunk> ChunksInternal { get; }

    internal bool SimpleLineWalkFastPathInternal { get; }

    public double DiscretionaryHyphenWidth { get; }

    public double TabStopAdvance { get; }
}

public sealed class PreparedTextWithSegments : PreparedText
{
    internal PreparedTextWithSegments(
        string font,
        WhiteSpaceMode whiteSpace,
        IReadOnlyList<PreparedSegment> segments,
        double hyphenWidth,
        double tabStopAdvance,
        IReadOnlyList<string> segmentTexts,
        IReadOnlyList<double> widths,
        IReadOnlyList<double> lineEndFitAdvances,
        IReadOnlyList<double> lineEndPaintAdvances,
        IReadOnlyList<SegmentBreakKind> kinds,
        IReadOnlyList<IReadOnlyList<double>?> breakableWidths,
        IReadOnlyList<IReadOnlyList<double>?> breakablePrefixWidths,
        IReadOnlyList<PreparedLineChunk> chunks,
        bool simpleLineWalkFastPath,
        IReadOnlyList<sbyte>? segmentLevels)
        : base(font, whiteSpace, segments, hyphenWidth, tabStopAdvance, chunks, simpleLineWalkFastPath)
    {
        Segments = segmentTexts;
        Widths = widths;
        LineEndFitAdvances = lineEndFitAdvances;
        LineEndPaintAdvances = lineEndPaintAdvances;
        Kinds = kinds;
        BreakableWidths = breakableWidths;
        BreakablePrefixWidths = breakablePrefixWidths;
        Chunks = chunks;
        SimpleLineWalkFastPath = simpleLineWalkFastPath;
        SegmentLevels = segmentLevels;
    }

    public IReadOnlyList<string> Segments { get; }

    public IReadOnlyList<double> Widths { get; }

    public IReadOnlyList<double> LineEndFitAdvances { get; }

    public IReadOnlyList<double> LineEndPaintAdvances { get; }

    public IReadOnlyList<SegmentBreakKind> Kinds { get; }

    public IReadOnlyList<IReadOnlyList<double>?> BreakableWidths { get; }

    public IReadOnlyList<IReadOnlyList<double>?> BreakablePrefixWidths { get; }

    public IReadOnlyList<PreparedLineChunk> Chunks { get; }

    public bool SimpleLineWalkFastPath { get; }

    public IReadOnlyList<sbyte>? SegmentLevels { get; }
}

public static partial class PretextLayout
{
    private static readonly Regex FontSizeRegex = new(@"(\d+(?:\.\d+)?)\s*px", RegexOptions.Compiled);
    private static readonly Dictionary<string, FontState> FontStates = new(StringComparer.Ordinal);
    private static readonly object FontStateGate = new();
    private static string? _locale;

    public static PreparedText Prepare(string text, string font, PrepareOptions? options = null)
    {
        var prepared = PrepareCore(text, font, options ?? new PrepareOptions(), includeSegments: false);
        return prepared;
    }

    public static PreparedTextWithSegments PrepareWithSegments(string text, string font, PrepareOptions? options = null)
    {
        return (PreparedTextWithSegments)PrepareCore(text, font, options ?? new PrepareOptions(), includeSegments: true);
    }

    public static PrepareProfile ProfilePrepare(string text, string font, PrepareOptions? options = null)
    {
        var effectiveOptions = options ?? new PrepareOptions();
        var stopwatch = Stopwatch.StartNew();
        var tokens = AnalyzeTokens(text ?? string.Empty, effectiveOptions.WhiteSpace);
        var analysisMs = stopwatch.Elapsed.TotalMilliseconds;

        var fontState = GetFontState(font);
        var engineProfile = GetEngineProfile();
        var breakableSegments = 0;
        var preparedSegments = 0;
        foreach (var token in tokens)
        {
            foreach (var segment in ExpandPreparedSegments(token, fontState, engineProfile))
            {
                preparedSegments++;
                if (segment.IsBreakableRun && segment.GraphemeCount > 1)
                {
                    breakableSegments++;
                }
            }
        }

        var totalMs = stopwatch.Elapsed.TotalMilliseconds;
        return new PrepareProfile(
            analysisMs,
            totalMs - analysisMs,
            totalMs,
            tokens.Count,
            preparedSegments,
            breakableSegments);
    }

    public static LayoutResult Layout(PreparedText prepared, double maxWidth, double lineHeight)
    {
        var lineCount = 0;
        var cursor = new LayoutCursor(0, 0);

        while (TryStepLine(prepared, cursor, maxWidth, out var line))
        {
            lineCount++;
            cursor = line.End;
        }

        return new LayoutResult(lineCount, lineCount * lineHeight);
    }

    public static LayoutLinesResult LayoutWithLines(PreparedTextWithSegments prepared, double maxWidth, double lineHeight)
    {
        var lines = new List<LayoutLine>();
        var cursor = new LayoutCursor(0, 0);

        while (TryStepLine(prepared, cursor, maxWidth, out var line))
        {
            lines.Add(MaterializeLine(prepared, line));
            cursor = line.End;
        }

        return new LayoutLinesResult(lines.Count, lines.Count * lineHeight, new ReadOnlyCollection<LayoutLine>(lines));
    }

    public static LayoutLine? LayoutNextLine(PreparedTextWithSegments prepared, LayoutCursor start, double maxWidth)
    {
        return TryStepLine(prepared, start, maxWidth, out var line)
            ? MaterializeLine(prepared, line)
            : null;
    }

    public static int WalkLineRanges(PreparedTextWithSegments prepared, double maxWidth, Action<LayoutLineRange> onLine)
    {
        ArgumentNullException.ThrowIfNull(onLine);

        var lineCount = 0;
        var cursor = new LayoutCursor(0, 0);
        while (TryStepLine(prepared, cursor, maxWidth, out var line))
        {
            lineCount++;
            onLine(new LayoutLineRange(line.Width, line.Start, line.End));
            cursor = line.End;
        }

        return lineCount;
    }

    public static void ClearCache()
    {
        lock (FontStateGate)
        {
            foreach (var state in FontStates.Values)
            {
                state.Dispose();
            }

            FontStates.Clear();
        }
    }

    public static void SetLocale(string? locale = null)
    {
        _locale = locale;
        ClearCache();
    }

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

    private static readonly HashSet<string> KinsokuStart = new(StringComparer.Ordinal)
    {
        "\uFF0C", "\uFF0E", "\uFF01", "\uFF1A", "\uFF1B", "\uFF1F", "\u3001", "\u3002",
        "\u30FB", "\uFF09", "\u3015", "\u3009", "\u300B", "\u300D", "\u300F", "\u3011",
        "\u3017", "\u3019", "\u301B", "\u30FC", "\u3005", "\u303B", "\u309D", "\u309E",
        "\u30FD", "\u30FE",
    };

    private static readonly HashSet<string> KinsokuEnd = new(StringComparer.Ordinal)
    {
        "\"", "(", "[", "{", "“", "‘", "«", "‹", "\uFF08", "\u3014", "\u3008", "\u300A",
        "\u300C", "\u300E", "\u3010", "\u3016", "\u3018", "\u301A",
    };

    private static readonly HashSet<string> LeftStickyPunctuation = new(StringComparer.Ordinal)
    {
        ".", ",", "!", "?", ":", ";", "\u060C", "\u061B", "\u061F", "\u0964", "\u0965",
        "\u104A", "\u104B", "\u104C", "\u104D", "\u104F", ")", "]", "}", "%", "\"",
        "”", "’", "»", "›", "…",
    };

    private static readonly HashSet<string> ClosingQuotes = new(StringComparer.Ordinal)
    {
        "”", "’", "»", "›", "\u300D", "\u300F", "\u3011", "\u300B", "\u3009", "\u3015", "\uFF09",
    };

    private static readonly HashSet<char> NumericJoiners = ['-', ':', '/', '×', ',', '.', '+', '\u2013', '\u2014'];

    private static void AppendExpandedText(List<Token> tokens, string text)
    {
        foreach (var part in SplitTextRun(text))
        {
            tokens.Add(new Token(part, SegmentBreakKind.Text));
        }
    }

    private static IReadOnlyList<string> SplitTextRun(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<string>();
        }

        if (ContainsCjk(text))
        {
            return SplitCjkTextRun(text);
        }

        if (TrySplitUrlLikeRun(text, out var urlParts))
        {
            return urlParts;
        }

        if (TrySplitNumericRun(text, out var numericParts))
        {
            return numericParts;
        }

        return new[] { text };
    }

    private static IReadOnlyList<string> SplitMeasuredCjkRun(string text, bool carryCjkAfterClosingQuote)
    {
        var elements = GetTextElements(text);
        if (elements.Length <= 1)
        {
            return elements;
        }

        var units = new List<string>();
        var current = elements[0];
        for (var index = 1; index < elements.Length; index++)
        {
            var grapheme = elements[index];
            if (IsForwardStickyCluster(current) ||
                IsCjkLineStartProhibited(grapheme) ||
                IsLeftStickyCluster(grapheme) ||
                (carryCjkAfterClosingQuote && ContainsCjk(grapheme) && EndsWithClosingQuote(current)))
            {
                current += grapheme;
                continue;
            }

            units.Add(current);
            current = grapheme;
        }

        units.Add(current);
        return units;
    }

    private static IReadOnlyList<string> SplitCjkTextRun(string text)
    {
        return SplitMeasuredCjkRun(text, carryCjkAfterClosingQuote: true);
    }

    private static bool TrySplitUrlLikeRun(string text, out IReadOnlyList<string> parts)
    {
        parts = Array.Empty<string>();
        if (!text.StartsWith("www.", StringComparison.OrdinalIgnoreCase) &&
            !text.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        var queryIndex = text.IndexOf('?');
        if (queryIndex <= 0 || queryIndex >= text.Length - 1)
        {
            return false;
        }

        parts =
        [
            text[..(queryIndex + 1)],
            text[(queryIndex + 1)..],
        ];
        return true;
    }

    private static bool TrySplitNumericRun(string text, out IReadOnlyList<string> parts)
    {
        parts = Array.Empty<string>();
        if (!text.Contains('-', StringComparison.Ordinal))
        {
            return false;
        }

        var rawParts = text.Split('-', StringSplitOptions.None);
        if (rawParts.Length <= 1 || rawParts.Any(static part => part.Length == 0 || !IsNumericPart(part)))
        {
            return false;
        }

        var split = new List<string>(rawParts.Length);
        for (var index = 0; index < rawParts.Length; index++)
        {
            split.Add(index < rawParts.Length - 1 ? $"{rawParts[index]}-" : rawParts[index]);
        }

        parts = split;
        return true;
    }

    private static bool IsNumericPart(string text)
    {
        var sawDigit = false;
        foreach (var ch in text)
        {
            if (char.GetUnicodeCategory(ch) == UnicodeCategory.DecimalDigitNumber)
            {
                sawDigit = true;
                continue;
            }

            if (NumericJoiners.Contains(ch))
            {
                continue;
            }

            return false;
        }

        return sawDigit;
    }

    private static bool IsLeftStickyCluster(string text)
    {
        return text.Length > 0 && text.All(ch => LeftStickyPunctuation.Contains(ch.ToString()));
    }

    private static bool IsCjkLineStartProhibited(string text)
    {
        return text.Length > 0 && text.All(ch => KinsokuStart.Contains(ch.ToString()) || LeftStickyPunctuation.Contains(ch.ToString()));
    }

    private static bool IsForwardStickyCluster(string text)
    {
        return text.Length > 0 && text.All(ch => KinsokuEnd.Contains(ch.ToString()) || ch is '\'' or '’');
    }

    private static bool EndsWithClosingQuote(string text)
    {
        for (var index = text.Length - 1; index >= 0; index--)
        {
            var ch = text[index].ToString();
            if (ClosingQuotes.Contains(ch))
            {
                return true;
            }

            if (!LeftStickyPunctuation.Contains(ch))
            {
                return false;
            }
        }

        return false;
    }

    private static bool ContainsCjk(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            var code = rune.Value;
            if ((code >= 0x4E00 && code <= 0x9FFF) ||
                (code >= 0x3400 && code <= 0x4DBF) ||
                (code >= 0x20000 && code <= 0x2A6DF) ||
                (code >= 0x2A700 && code <= 0x2B73F) ||
                (code >= 0x2B740 && code <= 0x2B81F) ||
                (code >= 0x2B820 && code <= 0x2CEAF) ||
                (code >= 0x2CEB0 && code <= 0x2EBEF) ||
                (code >= 0x30000 && code <= 0x3134F) ||
                (code >= 0xF900 && code <= 0xFAFF) ||
                (code >= 0x2F800 && code <= 0x2FA1F) ||
                (code >= 0x3000 && code <= 0x303F) ||
                (code >= 0x3040 && code <= 0x309F) ||
                (code >= 0x30A0 && code <= 0x30FF) ||
                (code >= 0xAC00 && code <= 0xD7AF) ||
                (code >= 0xFF00 && code <= 0xFFEF))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] GetTextElements(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<string>();
        }

        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            elements.Add((string)enumerator.Current!);
        }

        return elements.ToArray();
    }

    private static List<Token> Tokenize(string text, WhiteSpaceMode whiteSpace)
    {
        text = NormalizeNewlines(text);
        var tokens = new List<Token>();
        var builder = new StringBuilder();
        var pendingSpace = false;
        var sawContent = false;

        void FlushText()
        {
            if (builder.Length == 0)
            {
                return;
            }

            AppendExpandedText(tokens, builder.ToString());
            builder.Clear();
            sawContent = true;
        }

        void FlushPendingSpace()
        {
            if (!pendingSpace)
            {
                return;
            }

            tokens.Add(new Token(" ", SegmentBreakKind.Space));
            pendingSpace = false;
        }

        if (whiteSpace == WhiteSpaceMode.Normal)
        {
            foreach (var rune in text.EnumerateRunes())
            {
                var scalar = rune.Value;
                switch (scalar)
                {
                    case 0x200B:
                        FlushText();
                        FlushPendingSpace();
                        tokens.Add(new Token("\u200B", SegmentBreakKind.ZeroWidthBreak));
                        break;
                    case 0x00AD:
                        FlushText();
                        FlushPendingSpace();
                        tokens.Add(new Token("\u00AD", SegmentBreakKind.SoftHyphen));
                        break;
                    default:
                        if (IsOrdinaryWhiteSpace(rune))
                        {
                            FlushText();
                            if (sawContent)
                            {
                                pendingSpace = true;
                            }
                        }
                        else
                        {
                            FlushPendingSpace();
                            builder.Append(rune.ToString());
                        }

                        break;
                }
            }

            FlushText();
            return tokens;
        }

        foreach (var rune in text.EnumerateRunes())
        {
            var scalar = rune.Value;
            switch (scalar)
            {
                case '\n':
                    FlushText();
                    tokens.Add(new Token("\n", SegmentBreakKind.HardBreak));
                    break;
                case '\t':
                    FlushText();
                    tokens.Add(new Token("\t", SegmentBreakKind.Tab));
                    break;
                case ' ':
                    FlushText();
                    AppendRun(tokens, " ", SegmentBreakKind.PreservedSpace);
                    break;
                case 0x200B:
                    FlushText();
                    tokens.Add(new Token("\u200B", SegmentBreakKind.ZeroWidthBreak));
                    break;
                case 0x00AD:
                    FlushText();
                    tokens.Add(new Token("\u00AD", SegmentBreakKind.SoftHyphen));
                    break;
                default:
                    builder.Append(rune.ToString());
                    break;
            }
        }

        FlushText();
        return tokens;
    }

    private static void AppendRun(List<Token> tokens, string scalar, SegmentBreakKind kind)
    {
        if (tokens.Count > 0 && tokens[^1].Kind == kind)
        {
            var last = tokens[^1];
            tokens[^1] = last with { Text = last.Text + scalar };
            return;
        }

        tokens.Add(new Token(scalar, kind));
    }

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
                            new LayoutCursor(segmentIndex, 0),
                            new LayoutCursor(segmentIndex, 0),
                            currentWidth,
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
                case SegmentBreakKind.PreservedSpace:
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
                        if (segment.Kind == SegmentBreakKind.PreservedSpace)
                        {
                            bestBreak = new BreakCandidate(
                                new LayoutCursor(segmentIndex, 0),
                                new LayoutCursor(segmentIndex, 0),
                                currentWidth,
                                AppendHyphen: false);
                        }

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
        if (line.Start == line.VisibleEnd && !line.AppendHyphen)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var segments = prepared.SegmentsInternal;
        var segmentIndex = line.Start.SegmentIndex;
        var graphemeIndex = line.Start.GraphemeIndex;

        while (segmentIndex < segments.Count)
        {
            if (segmentIndex > line.VisibleEnd.SegmentIndex)
            {
                break;
            }

            var segment = segments[segmentIndex];
            if (segmentIndex == line.VisibleEnd.SegmentIndex && graphemeIndex == line.VisibleEnd.GraphemeIndex)
            {
                break;
            }

            if (segment.Kind is SegmentBreakKind.ZeroWidthBreak or SegmentBreakKind.SoftHyphen or SegmentBreakKind.HardBreak)
            {
                segmentIndex++;
                graphemeIndex = 0;
                continue;
            }

            if (segmentIndex == line.VisibleEnd.SegmentIndex)
            {
                builder.Append(segment.GetSlice(graphemeIndex, line.VisibleEnd.GraphemeIndex));
                break;
            }

            builder.Append(segment.GetSlice(graphemeIndex, segment.GraphemeCount));
            segmentIndex++;
            graphemeIndex = 0;
        }

        if (line.AppendHyphen)
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

    private static string NormalizeNewlines(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static bool IsOrdinaryWhiteSpace(Rune rune)
    {
        if (rune.Value is 0x00A0 or 0x202F or 0x200B or 0x00AD or 0x2060)
        {
            return false;
        }

        return Rune.IsWhiteSpace(rune);
    }

    private readonly record struct Token(string Text, SegmentBreakKind Kind);

    private readonly record struct MeasurementCacheKey(string Text, SegmentBreakKind Kind, bool IsBreakableRun);

    private readonly record struct BreakCandidate(LayoutCursor VisibleEnd, LayoutCursor End, double Width, bool AppendHyphen);

    private readonly record struct InternalLine(LayoutCursor Start, LayoutCursor VisibleEnd, LayoutCursor End, double Width, bool AppendHyphen);

    private sealed class FontState : IDisposable
    {
        private FontState(string font, SKFont skFont, double spaceWidth, double hyphenWidth)
        {
            Font = font;
            SkFont = skFont;
            SpaceWidth = spaceWidth;
            HyphenWidth = hyphenWidth;
            TabStopAdvance = spaceWidth * 8;
            SegmentCache = new Dictionary<MeasurementCacheKey, PreparedSegment>();
        }

        public string Font { get; }

        public SKFont SkFont { get; }

        public double SpaceWidth { get; }

        public double HyphenWidth { get; }

        public double TabStopAdvance { get; }

        public Dictionary<MeasurementCacheKey, PreparedSegment> SegmentCache { get; }

        public static FontState Create(string font)
        {
            var spec = FontSpec.Parse(font);
            var skFont = new SKFont
            {
                Size = spec.Size,
                Typeface = SKTypeface.FromFamilyName(spec.PrimaryFamily, spec.FontStyle),
                Subpixel = true,
            };

            return new FontState(font, skFont, skFont.MeasureText(" "), skFont.MeasureText("-"));
        }

        public PreparedSegment MeasureSegment(string text, SegmentBreakKind kind, bool isBreakableRun)
        {
            var cacheKey = new MeasurementCacheKey(text, kind, isBreakableRun);
            if (SegmentCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var graphemes = GetTextElements(text);
            double[]? prefixWidths = null;
            if (graphemes.Length > 1)
            {
                prefixWidths = new double[graphemes.Length];
                var prefix = new StringBuilder();
                for (var i = 0; i < graphemes.Length; i++)
                {
                    prefix.Append(graphemes[i]);
                    prefixWidths[i] = SkFont.MeasureText(prefix.ToString());
                }
            }

            var width = SkFont.MeasureText(text);
            var segment = new PreparedSegment(text, kind, isBreakableRun, width, graphemes, prefixWidths);
            SegmentCache[cacheKey] = segment;
            return segment;
        }

        public void Dispose()
        {
            SkFont.Dispose();
        }

        private static string[] GetTextElements(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<string>();
            }

            var elements = new List<string>();
            var enumerator = StringInfo.GetTextElementEnumerator(text);
            while (enumerator.MoveNext())
            {
                elements.Add((string)enumerator.Current!);
            }

            return elements.ToArray();
        }
    }

    private readonly record struct FontSpec(float Size, string PrimaryFamily, SKFontStyle FontStyle)
    {
        public static FontSpec Parse(string font)
        {
            var match = FontSizeRegex.Match(font);
            if (!match.Success)
            {
                return new FontSpec(16, "Arial", SKFontStyle.Normal);
            }

            var size = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var beforeSize = font[..match.Index];
            var afterSize = font[(match.Index + match.Length)..].Trim();
            if (afterSize.StartsWith("/", StringComparison.Ordinal))
            {
                var nextSpace = afterSize.IndexOf(' ');
                afterSize = nextSpace >= 0 ? afterSize[(nextSpace + 1)..].Trim() : string.Empty;
            }

            var families = afterSize
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static family => family.Trim().Trim('"', '\''))
                .Where(static family => !string.IsNullOrWhiteSpace(family))
                .ToArray();
            var primaryFamily = families.FirstOrDefault() ?? "Arial";

            if (string.Equals(primaryFamily, "sans-serif", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(primaryFamily, "system-ui", StringComparison.OrdinalIgnoreCase))
            {
                primaryFamily = "Arial";
            }
            else if (string.Equals(primaryFamily, "serif", StringComparison.OrdinalIgnoreCase))
            {
                primaryFamily = "Times New Roman";
            }
            else if (string.Equals(primaryFamily, "monospace", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(primaryFamily, "ui-monospace", StringComparison.OrdinalIgnoreCase))
            {
                primaryFamily = "Menlo";
            }

            var lowered = beforeSize.ToLowerInvariant();
            var italic = lowered.Contains("italic", StringComparison.Ordinal) || lowered.Contains("oblique", StringComparison.Ordinal);
            var weight = 400;
            foreach (var token in lowered.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    weight = parsed;
                    break;
                }

                if (token == "bold")
                {
                    weight = 700;
                }
            }

            var styleWeight = weight >= 700 ? SKFontStyleWeight.Bold : weight >= 500 ? SKFontStyleWeight.Medium : SKFontStyleWeight.Normal;
            var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            return new FontSpec(size, primaryFamily, new SKFontStyle(styleWeight, SKFontStyleWidth.Normal, slant));
        }
    }
}

internal static class BidiHelper
{
    private static readonly sbyte[] BaseTypes =
    {
        10,10,10,10,10,10,10,10,10,11,10,11,12,
        10,10,10,10,10,10,10,10,10,10,10,10,10,
        10,10,10,10,10,11,12,9,9,7,7,7,9,
        9,9,9,9,9,8,9,8,9,4,4,4,
        4,4,4,4,4,4,4,9,9,9,9,9,
        9,9,0,0,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,0,0,0,0,0,9,9,
        9,9,9,9,0,0,0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        0,9,9,9,9,10,10,10,10,10,10,10,10,
        10,10,10,10,10,10,10,10,10,10,10,10,
        10,10,10,10,10,10,10,10,10,10,10,10,
        10,8,9,7,7,7,7,9,9,9,9,0,9,
        9,9,9,9,7,7,4,4,9,0,9,9,9,
        4,0,9,9,9,9,9,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        0,9,0,0,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        0,0,0,9,0,0,0,0,0,0,0,0
    };

    private static readonly sbyte[] ArabicTypes =
    {
        2,2,2,2,2,2,2,2,2,2,2,2,
        8,2,9,9,13,13,13,13,13,13,2,
        2,2,2,2,2,2,2,2,2,2,2,2,
        2,2,2,2,2,2,2,2,2,2,2,2,
        2,2,2,2,2,2,2,2,2,2,2,2,
        2,2,2,2,2,2,2,2,2,2,2,2,
        2,2,2,2,13,13,13,13,13,13,13,
        13,13,13,13,13,13,13,2,2,2,2,
        2,2,2,5,5,5,5,5,5,5,5,5,
        5,7,5,5,2,2,2,13,2,2,2,2,
        2,2,2,2,2,2,2,2,2,2,2,2,
        2,2,2,2,2,2,2,2,2,2,2,2,
        2,2,2,2,2,2,2,2,2,2,2,2,
        2,2,2,2,2,2,2,2,2,2,2,2,
        2,2,2,2,2,2,2,2,2,2,2,2,
        2,2,2,2,2,2,2,2,2,2,2,2,
        2,2,2,2,2,2,2,2,2,2,2,2,
        2,13,13,13,13,13,13,13,13,13,13,
        13,13,13,13,13,13,13,13,13,9,13,
        13,13,13,2,2,2,2,2,2,2,2,2,
        2,2,2,2,2,2,2,2,2
    };

    public static sbyte[]? ComputeSegmentLevels(string text, IReadOnlyList<int> starts)
    {
        var bidiLevels = ComputeLevels(text);
        if (bidiLevels is null)
        {
            return null;
        }

        var levels = new sbyte[starts.Count];
        for (var i = 0; i < starts.Count; i++)
        {
            levels[i] = bidiLevels[starts[i]];
        }

        return levels;
    }

    private static sbyte[]? ComputeLevels(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var types = new sbyte[text.Length];
        var bidiCount = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var type = Classify(text[i]);
            if (type is 1 or 2 or 5)
            {
                bidiCount++;
            }

            types[i] = type;
        }

        if (bidiCount == 0)
        {
            return null;
        }

        var startLevel = (text.Length / (double)bidiCount) < 0.3 ? 0 : 1;
        var levels = Enumerable.Repeat((sbyte)startLevel, text.Length).ToArray();
        var embedding = (sbyte)(startLevel % 2 == 1 ? 1 : 0);
        var sor = embedding == 1 ? (sbyte)1 : (sbyte)0;

        var lastType = sor;
        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] == 13)
            {
                types[i] = lastType;
            }
            else
            {
                lastType = types[i];
            }
        }

        lastType = sor;
        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] == 4)
            {
                types[i] = lastType == 2 ? (sbyte)5 : (sbyte)4;
            }
            else if (types[i] is 0 or 1 or 2)
            {
                lastType = types[i];
            }
        }

        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] == 2)
            {
                types[i] = 1;
            }
        }

        for (var i = 1; i < types.Length - 1; i++)
        {
            if (types[i] == 6 && types[i - 1] == 4 && types[i + 1] == 4)
            {
                types[i] = 4;
            }

            if (types[i] == 8 && (types[i - 1] == 4 || types[i - 1] == 5) && types[i + 1] == types[i - 1])
            {
                types[i] = types[i - 1];
            }
        }

        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] != 4)
            {
                continue;
            }

            for (var j = i - 1; j >= 0 && types[j] == 7; j--)
            {
                types[j] = 4;
            }

            for (var j = i + 1; j < types.Length && types[j] == 7; j++)
            {
                types[j] = 4;
            }
        }

        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] is 12 or 6 or 7 or 8)
            {
                types[i] = 9;
            }
        }

        lastType = sor;
        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] == 4)
            {
                types[i] = lastType == 0 ? (sbyte)0 : (sbyte)4;
            }
            else if (types[i] is 1 or 0)
            {
                lastType = types[i];
            }
        }

        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] != 9)
            {
                continue;
            }

            var end = i + 1;
            while (end < types.Length && types[end] == 9)
            {
                end++;
            }

            var before = i > 0 ? types[i - 1] : sor;
            var after = end < types.Length ? types[end] : sor;
            var beforeDir = before != 0 ? (sbyte)1 : (sbyte)0;
            var afterDir = after != 0 ? (sbyte)1 : (sbyte)0;
            if (beforeDir == afterDir)
            {
                for (var j = i; j < end; j++)
                {
                    types[j] = beforeDir;
                }
            }

            i = end - 1;
        }

        for (var i = 0; i < types.Length; i++)
        {
            if (types[i] == 9)
            {
                types[i] = embedding == 1 ? (sbyte)1 : (sbyte)0;
            }
        }

        for (var i = 0; i < types.Length; i++)
        {
            if ((levels[i] & 1) == 0)
            {
                if (types[i] == 1)
                {
                    levels[i]++;
                }
                else if (types[i] is 5 or 4)
                {
                    levels[i] += 2;
                }
            }
            else if (types[i] is 0 or 5 or 4)
            {
                levels[i]++;
            }
        }

        return levels;
    }

    private static sbyte Classify(char ch)
    {
        var code = (int)ch;
        if (code <= 0x00FF)
        {
            return BaseTypes[code];
        }

        if (code is >= 0x0590 and <= 0x05F4)
        {
            return 1;
        }

        if (code is >= 0x0600 and <= 0x06FF)
        {
            return ArabicTypes[code & 0xFF];
        }

        if (code is >= 0x0700 and <= 0x08AC)
        {
            return 2;
        }

        return 0;
    }
}
