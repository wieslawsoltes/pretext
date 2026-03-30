using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Pretext.Uno;

namespace Pretext.Avalonia.Controls;

public class PretextParagraphControl : Panel
{
    private readonly List<TextBlock> _linePool = [];
    private readonly string _fontFamily;
    private readonly double _fontSize;
    private readonly double _lineHeight;
    private readonly IBrush _foreground;
    private readonly FontWeight _fontWeight;
    private readonly FontStyle _fontStyle;
    private PreparedTextWithSegments? _prepared;
    private IReadOnlyList<LayoutLine>? _lines;
    private double _lastWidth = -1;

    public PretextParagraphControl(
        string fontFamily,
        double fontSize,
        double lineHeight,
        IBrush foreground,
        FontWeight? fontWeight = null,
        FontStyle? fontStyle = null)
    {
        _fontFamily = fontFamily;
        _fontSize = fontSize;
        _lineHeight = lineHeight;
        _foreground = foreground;
        _fontWeight = fontWeight ?? FontWeight.Normal;
        _fontStyle = fontStyle ?? FontStyle.Normal;
        IsHitTestVisible = false;
    }

    public PreparedTextWithSegments? Prepared
    {
        get => _prepared;
        set
        {
            if (ReferenceEquals(_prepared, value))
            {
                return;
            }

            _prepared = value;
            _lastWidth = -1;
            _lines = null;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = ResolveWidth(availableSize.Width);
        if (_prepared is null || width <= 0)
        {
            EnsureLinePool(0);
            return default;
        }

        EnsureLines(width);
        foreach (var line in _linePool)
        {
            if (!line.IsVisible)
            {
                continue;
            }

            line.Measure(new Size(width, _lineHeight));
        }

        return new Size(width, _lines!.Count * _lineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = ResolveWidth(finalSize.Width);
        if (_prepared is null || width <= 0)
        {
            EnsureLinePool(0);
            return finalSize;
        }

        EnsureLines(width);
        for (var index = 0; index < _linePool.Count; index++)
        {
            var visible = _lines is not null && index < _lines.Count;
            var line = _linePool[index];
            line.IsVisible = visible;
            if (!visible)
            {
                line.Arrange(default);
                continue;
            }

            line.Text = _lines![index].Text;
            line.Width = width;
            line.Arrange(new Rect(0, index * _lineHeight, width, _lineHeight));
        }

        return new Size(width, _lines!.Count * _lineHeight);
    }

    private void EnsureLines(double width)
    {
        if (_prepared is null)
        {
            _lines = [];
            EnsureLinePool(0);
            return;
        }

        if (_lines is not null && Math.Abs(width - _lastWidth) <= 0.5)
        {
            return;
        }

        _lastWidth = width;
        _lines = PretextLayout.LayoutWithLines(_prepared, Math.Max(1, width), _lineHeight).Lines;
        EnsureLinePool(_lines.Count);
    }

    private void EnsureLinePool(int count)
    {
        while (_linePool.Count < count)
        {
            var line = new TextBlock
            {
                Foreground = _foreground,
                FontFamily = new FontFamily(_fontFamily),
                FontSize = _fontSize,
                FontWeight = _fontWeight,
                FontStyle = _fontStyle,
                TextWrapping = TextWrapping.NoWrap,
                LineHeight = _lineHeight,
            };
            _linePool.Add(line);
            Children.Add(line);
        }

        for (var index = 0; index < _linePool.Count; index++)
        {
            _linePool[index].IsVisible = index < count;
        }
    }

    private double ResolveWidth(double proposedWidth)
    {
        if (_prepared is null)
        {
            return 0;
        }

        if (!double.IsInfinity(proposedWidth))
        {
            return Math.Max(1, proposedWidth);
        }

        var max = 0d;
        PretextLayout.WalkLineRanges(_prepared, 100_000, line =>
        {
            if (line.Width > max)
            {
                max = line.Width;
            }
        });

        return Math.Max(1, Math.Ceiling(max));
    }
}
