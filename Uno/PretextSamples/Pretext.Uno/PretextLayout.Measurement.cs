using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Pretext.Uno;

public static partial class PretextLayout
{
    private static readonly Regex FontSizeRegex = new(@"(\d+(?:\.\d+)?)\s*px", RegexOptions.Compiled);

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

    private readonly record struct MeasurementCacheKey(string Text, SegmentBreakKind Kind, bool IsBreakableRun);

    private sealed class FontState : IDisposable
    {
        private FontState(string font, SKFont? skFont, double spaceWidth, double hyphenWidth, Func<string, string, double>? measureTextOverride)
        {
            Font = font;
            SkFont = skFont;
            SpaceWidth = spaceWidth;
            HyphenWidth = hyphenWidth;
            TabStopAdvance = spaceWidth * 8;
            SegmentCache = new Dictionary<MeasurementCacheKey, PreparedSegment>();
            MeasureTextOverride = measureTextOverride;
        }

        public string Font { get; }

        public SKFont? SkFont { get; }

        public double SpaceWidth { get; }

        public double HyphenWidth { get; }

        public double TabStopAdvance { get; }

        public Dictionary<MeasurementCacheKey, PreparedSegment> SegmentCache { get; }

        private Func<string, string, double>? MeasureTextOverride { get; }

        public static FontState Create(string font)
        {
            var measureTextOverride = PretextLayout._measureTextOverride;
            var spec = FontSpec.Parse(font);
            SKFont? skFont = null;
            if (measureTextOverride is null)
            {
                skFont = new SKFont
                {
                    Size = spec.Size,
                    Typeface = SKTypeface.FromFamilyName(spec.PrimaryFamily, spec.FontStyle),
                    Subpixel = true,
                };
            }

            var spaceWidth = measureTextOverride?.Invoke(" ", font) ?? skFont!.MeasureText(" ");
            var hyphenWidth = measureTextOverride?.Invoke("-", font) ?? skFont!.MeasureText("-");
            return new FontState(font, skFont, spaceWidth, hyphenWidth, measureTextOverride);
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
                    prefixWidths[i] = MeasureText(prefix.ToString());
                }
            }

            var width = MeasureText(text);
            var segment = new PreparedSegment(text, kind, isBreakableRun, width, graphemes, prefixWidths);
            SegmentCache[cacheKey] = segment;
            return segment;
        }

        public void Dispose()
        {
            SkFont?.Dispose();
        }

        private double MeasureText(string text)
        {
            return MeasureTextOverride?.Invoke(text, Font) ?? SkFont!.MeasureText(text);
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
