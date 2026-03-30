using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pretext.LayoutFramework;
using Pretext.Uno;
using Pretext.Uno.Controls;

namespace PretextSamples.Samples;

public sealed class TwoPassControlSampleView : UserControl, IPretextControlLayoutSource
{
    private const string BodyFont = "18px Georgia, serif";
    private const string NoteFont = "14px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const double BodyLineHeight = 28;
    private const double NoteLineHeight = 20;

    private const string BodyCopy =
        "Pretext can prepare the semantic shape of a composite control once, then solve exact rectangles for retained children as the host changes width. The title, narrative copy, summary card, and buttons stay as native Uno elements, but their geometry comes from a reusable prepared-control model instead of ad-hoc Grid heuristics or live tree measurement.";

    private const string NoteCopy =
        "Wide widths pin the note alongside the story. Narrow widths promote it above the actions, but the prepared slots stay stable.";

    private readonly PreparedTextWithSegments _preparedBody = PretextLayout.PrepareWithSegments(BodyCopy, BodyFont);
    private readonly PreparedTextWithSegments _preparedNote = PretextLayout.PrepareWithSegments(NoteCopy, NoteFont);

    private readonly PretextParagraphView _bodyView;
    private readonly PretextParagraphView _noteView;
    private double _lastBodyWidth = -1;
    private double _lastNoteWidth = -1;

    public TwoPassControlSampleView()
    {
        var stack = SampleUi.CreatePageStack();
        stack.Children.Add(SampleUi.CreateHeader(
            "DEMO",
            "Two-pass Control Host",
            "A reusable host arranges retained child controls from a prepared control model, so a compound card can switch responsive modes without pushing layout decisions back into nested Grids."));

        var layoutHost = new PretextControlLayoutHost
        {
            Source = this,
            MinHeight = 420,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var header = new StackPanel { Spacing = 10 };
        header.Children.Add(new TextBlock
        {
            Text = "CONTROL GEOMETRY",
            Foreground = SampleTheme.AccentBrush,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            CharacterSpacing = 120,
            TextWrapping = TextWrapping.NoWrap,
        });
        header.Children.Add(new TextBlock
        {
            Text = "Prepared slots, retained children",
            Foreground = SampleTheme.InkBrush,
            FontFamily = new FontFamily("Georgia"),
            FontSize = 34,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.WrapWholeWords,
        });
        PretextControlLayoutHost.SetLayoutKey(header, "header");

        _bodyView = new PretextParagraphView("Georgia", 18, BodyLineHeight, SampleTheme.InkBrush)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        PretextControlLayoutHost.SetLayoutKey(_bodyView, "body");

        _noteView = new PretextParagraphView("Helvetica Neue", 14, NoteLineHeight, SampleTheme.MutedBrush)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var noteStack = new StackPanel { Spacing = 10 };
        noteStack.Children.Add(new TextBlock
        {
            Text = "Prepared once",
            Foreground = SampleTheme.AccentBrush,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            CharacterSpacing = 100,
            TextWrapping = TextWrapping.NoWrap,
        });
        noteStack.Children.Add(_noteView);

        var noteCard = new Border
        {
            Background = SampleTheme.AccentSoftBrush,
            BorderBrush = SampleTheme.RuleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16),
            Child = noteStack,
        };
        PretextControlLayoutHost.SetLayoutKey(noteCard, "note");

        var primaryButton = new Button
        {
            Content = "Route with prepared geometry",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = SampleTheme.InkBrush,
            Foreground = SampleTheme.WhiteBrush,
            BorderThickness = new Thickness(0),
        };
        PretextControlLayoutHost.SetLayoutKey(primaryButton, "primary");

        var secondaryButton = new Button
        {
            Content = "Invalidate solved layout",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = SampleTheme.PanelBrush,
            Foreground = SampleTheme.InkBrush,
            BorderBrush = SampleTheme.RuleBrush,
            BorderThickness = new Thickness(1),
        };
        PretextControlLayoutHost.SetLayoutKey(secondaryButton, "secondary");

        layoutHost.Children.Add(header);
        layoutHost.Children.Add(_bodyView);
        layoutHost.Children.Add(noteCard);
        layoutHost.Children.Add(primaryButton);
        layoutHost.Children.Add(secondaryButton);

        stack.Children.Add(SampleUi.CreateCard(layoutHost, 0));
        Content = SampleUi.CreatePageRoot(stack);

        Loaded += (_, _) => RenderCopy();
        layoutHost.SizeChanged += (_, _) => RenderCopy();
    }

    public LayoutFingerprint GetLayoutFingerprint()
    {
        var builder = LayoutFingerprintBuilder.Create();
        builder.Add("two-pass-control-host");
        builder.Add(BodyCopy);
        builder.Add(NoteCopy);
        builder.Add("Route with prepared geometry");
        builder.Add("Invalidate solved layout");
        return builder.ToFingerprint();
    }

    public PreparedControlModel Prepare(LayoutFingerprint fingerprint)
    {
        var bodyProbe = PretextLayout.Layout(_preparedBody, 420, BodyLineHeight);
        var noteProbe = PretextLayout.Layout(_preparedNote, 260, NoteLineHeight);

        return new PreparedControlModel(
            fingerprint,
            [
                new PreparedControlSlot("header", ControlSlotKind.Header, PreparedNodeMetrics.Flexible(new LayoutSize(420, 110), new LayoutSize(240, 96))),
                new PreparedControlSlot("body", ControlSlotKind.Body, PreparedNodeMetrics.Flexible(new LayoutSize(420, bodyProbe.Height), new LayoutSize(240, bodyProbe.Height)), RichText: _preparedBody),
                new PreparedControlSlot("note", ControlSlotKind.Accessory, PreparedNodeMetrics.Flexible(new LayoutSize(260, 66 + noteProbe.Height), new LayoutSize(220, 80)), RichText: _preparedNote),
                new PreparedControlSlot("primary", ControlSlotKind.Content, PreparedNodeMetrics.Fixed(220, 44)),
                new PreparedControlSlot("secondary", ControlSlotKind.Content, PreparedNodeMetrics.Fixed(180, 44)),
            ],
            LayoutSize.Empty);
    }

    public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints)
    {
        const double outerPadding = 20;
        const double sectionGap = 18;
        const double buttonGap = 12;
        const double headerHeight = 110;
        const double buttonHeight = 44;
        const double noteChromeHeight = 62;
        const double notePaddingX = 32;

        var availableWidth = Math.Max(320, constraints.AvailableWidth);
        var innerWidth = Math.Max(240, availableWidth - outerPadding * 2);
        var placements = new List<LayoutPlacement>(prepared.Slots.Count);

        if (innerWidth >= 760)
        {
            var noteWidth = Math.Max(240, Math.Min(320, innerWidth * 0.32));
            var mainWidth = Math.Max(260, innerWidth - noteWidth - sectionGap);
            var bodyHeight = PretextLayout.Layout(_preparedBody, mainWidth, BodyLineHeight).Height;
            var noteTextWidth = Math.Max(120, noteWidth - notePaddingX);
            var noteHeight = noteChromeHeight + PretextLayout.Layout(_preparedNote, noteTextWidth, NoteLineHeight).Height;
            var bodyY = outerPadding + headerHeight + 8;
            var buttonY = bodyY + bodyHeight + sectionGap;

            placements.Add(new LayoutPlacement(0, "header", new LayoutRect(outerPadding, outerPadding, mainWidth, headerHeight)));
            placements.Add(new LayoutPlacement(1, "body", new LayoutRect(outerPadding, bodyY, mainWidth, bodyHeight)));
            placements.Add(new LayoutPlacement(2, "note", new LayoutRect(outerPadding + mainWidth + sectionGap, outerPadding, noteWidth, noteHeight)));
            placements.Add(new LayoutPlacement(3, "primary", new LayoutRect(outerPadding, buttonY, 228, buttonHeight)));
            placements.Add(new LayoutPlacement(4, "secondary", new LayoutRect(outerPadding + 228 + buttonGap, buttonY, 196, buttonHeight)));

            var contentHeight = Math.Max(buttonY + buttonHeight, outerPadding + noteHeight) + outerPadding;
            return new SolvedLayout(
                prepared.Fingerprint,
                constraints,
                new LayoutSize(availableWidth, contentHeight),
                placements);
        }

        var headerWidth = innerWidth;
        var noteTextStackWidth = Math.Max(120, innerWidth - notePaddingX);
        var noteHeightStacked = noteChromeHeight + PretextLayout.Layout(_preparedNote, noteTextStackWidth, NoteLineHeight).Height;
        var bodyYStacked = outerPadding + headerHeight + sectionGap + noteHeightStacked + sectionGap;
        var bodyHeightStacked = PretextLayout.Layout(_preparedBody, innerWidth, BodyLineHeight).Height;
        var primaryY = bodyYStacked + bodyHeightStacked + sectionGap;

        placements.Add(new LayoutPlacement(0, "header", new LayoutRect(outerPadding, outerPadding, headerWidth, headerHeight)));
        placements.Add(new LayoutPlacement(2, "note", new LayoutRect(outerPadding, outerPadding + headerHeight + sectionGap, innerWidth, noteHeightStacked)));
        placements.Add(new LayoutPlacement(1, "body", new LayoutRect(outerPadding, bodyYStacked, innerWidth, bodyHeightStacked)));

        if (innerWidth >= 520)
        {
            var buttonWidth = (innerWidth - buttonGap) / 2;
            placements.Add(new LayoutPlacement(3, "primary", new LayoutRect(outerPadding, primaryY, buttonWidth, buttonHeight)));
            placements.Add(new LayoutPlacement(4, "secondary", new LayoutRect(outerPadding + buttonWidth + buttonGap, primaryY, buttonWidth, buttonHeight)));
            return new SolvedLayout(
                prepared.Fingerprint,
                constraints,
                new LayoutSize(availableWidth, primaryY + buttonHeight + outerPadding),
                placements);
        }

        placements.Add(new LayoutPlacement(3, "primary", new LayoutRect(outerPadding, primaryY, innerWidth, buttonHeight)));
        placements.Add(new LayoutPlacement(4, "secondary", new LayoutRect(outerPadding, primaryY + buttonHeight + buttonGap, innerWidth, buttonHeight)));

        return new SolvedLayout(
            prepared.Fingerprint,
            constraints,
            new LayoutSize(availableWidth, primaryY + buttonHeight * 2 + buttonGap + outerPadding),
            placements);
    }

    private void RenderCopy()
    {
        if (_bodyView.ActualWidth >= 1 && Math.Abs(_bodyView.ActualWidth - _lastBodyWidth) > 0.5)
        {
            _lastBodyWidth = _bodyView.ActualWidth;
            _bodyView.Render(_preparedBody, _bodyView.ActualWidth);
        }

        if (_noteView.ActualWidth >= 1 && Math.Abs(_noteView.ActualWidth - _lastNoteWidth) > 0.5)
        {
            _lastNoteWidth = _noteView.ActualWidth;
            _noteView.Render(_preparedNote, _noteView.ActualWidth);
        }
    }
}
