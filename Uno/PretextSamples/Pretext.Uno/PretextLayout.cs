using System.Collections.ObjectModel;
using System.Diagnostics;

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
    private static readonly Dictionary<string, FontState> FontStates = new(StringComparer.Ordinal);
    private static readonly object FontStateGate = new();
    private static string? _locale;
    private static Func<string, string, double>? _measureTextOverride;

    public static PreparedText Prepare(string text, string font, PrepareOptions? options = null)
    {
        return PrepareCore(text, font, options ?? new PrepareOptions(), includeSegments: false);
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

    internal static void SetMeasurementOverrideForTests(Func<string, string, double>? measureText)
    {
        _measureTextOverride = measureText;
        ClearCache();
    }

    internal static int CountPreparedLinesForTests(PreparedText prepared, double maxWidth)
    {
        var lineCount = 0;
        var cursor = new LayoutCursor(0, 0);

        while (TryStepLine(prepared, cursor, maxWidth, out var line))
        {
            lineCount++;
            cursor = line.End;
        }

        return lineCount;
    }

    internal static int WalkPreparedLinesForTests(PreparedText prepared, double maxWidth)
    {
        var walkedLines = 0;
        var cursor = new LayoutCursor(0, 0);

        while (TryStepLine(prepared, cursor, maxWidth, out var line))
        {
            walkedLines++;
            cursor = line.End;
        }

        return walkedLines;
    }
}
