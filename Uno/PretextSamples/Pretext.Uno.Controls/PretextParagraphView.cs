using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pretext.Uno;
using Windows.UI.Text;

namespace Pretext.Uno.Controls;

public sealed class PretextParagraphView : UserControl
{
    private readonly Canvas _canvas = new();
    private readonly List<TextBlock> _linePool = [];
    private readonly string _fontFamily;
    private readonly double _fontSize;
    private readonly double _lineHeight;
    private readonly Brush _brush;
    private readonly FontWeight _weight;

    public PretextParagraphView(string fontFamily, double fontSize, double lineHeight, Brush brush, FontWeight? weight = null)
    {
        _fontFamily = fontFamily;
        _fontSize = fontSize;
        _lineHeight = lineHeight;
        _brush = brush;
        _weight = weight ?? FontWeights.Normal;
        IsHitTestVisible = false;
        Content = _canvas;
    }

    public double Render(PreparedTextWithSegments prepared, double width, int? maxLines = null)
    {
        var lines = PretextLayout.LayoutWithLines(prepared, Math.Max(1, width), _lineHeight).Lines;
        var lineCount = maxLines.HasValue ? Math.Min(maxLines.Value, lines.Count) : lines.Count;

        while (_linePool.Count < lineCount)
        {
            var line = new TextBlock
            {
                Foreground = _brush,
                FontFamily = new FontFamily(_fontFamily),
                FontSize = _fontSize,
                FontWeight = _weight,
                LineHeight = _lineHeight,
                TextWrapping = TextWrapping.NoWrap,
            };
            _linePool.Add(line);
            _canvas.Children.Add(line);
        }

        for (var index = 0; index < _linePool.Count; index++)
        {
            var visible = index < lineCount;
            _linePool[index].Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (!visible)
            {
                continue;
            }

            _linePool[index].Text = lines[index].Text;
            _linePool[index].Width = Math.Max(1, width);
            Canvas.SetLeft(_linePool[index], 0);
            Canvas.SetTop(_linePool[index], index * _lineHeight);
        }

        Width = Math.Max(1, width);
        Height = lineCount * _lineHeight;
        _canvas.Width = Width;
        _canvas.Height = Height;
        return Height;
    }
}
