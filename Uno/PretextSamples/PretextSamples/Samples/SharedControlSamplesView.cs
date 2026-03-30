using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pretext.LayoutFramework;
using Pretext.LayoutFramework.Samples;
using Pretext.Uno;
using Pretext.Uno.Controls;

namespace PretextSamples.Samples;

public sealed class SharedControlSamplesView : UserControl
{
    public SharedControlSamplesView()
    {
        var stack = SampleUi.CreatePageStack();
        stack.Children.Add(SampleUi.CreateHeader(
            "DEMO",
            "Shared sample definitions",
            "These controls and responsive shells are driven by framework-neutral sample definitions in the core Pretext project. Uno only supplies the retained controls and the control host."));

        stack.Children.Add(new MailRowDemoControl());
        stack.Children.Add(new NoteCardDemoControl());
        stack.Children.Add(new MailShellDemoControl());
        stack.Children.Add(new NotesShellDemoControl());
        Content = SampleUi.CreatePageRoot(stack);
    }

    private sealed class MailRowDemoControl : Border, IPretextControlLayoutSource
    {
        private readonly MailRowSampleDefinition _definition = new(
            new MailRowSampleData(
                "Leopold Aschenbrenner",
                "11:08",
                "Situational awareness for the decade ahead",
                "This inbox row is solved from a shared sample definition, then rendered by native Uno controls through the reusable PretextControlLayoutHost.",
                "Pinned",
                true));

        private readonly PretextControlLayoutHost _host;
        private readonly PretextParagraphView _senderView;
        private readonly PretextParagraphView _subjectView;
        private readonly PretextParagraphView _previewView;
        private readonly TextBlock _timeBlock;
        private readonly Border _statusChip;
        private readonly TextBlock _statusText;
        private double _lastSenderWidth = -1;
        private double _lastSubjectWidth = -1;
        private double _lastPreviewWidth = -1;

        public MailRowDemoControl()
        {
            Background = SampleTheme.PanelBrush;
            BorderBrush = SampleTheme.RuleBrush;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(20);
            Padding = new Thickness(0);

            _host = new PretextControlLayoutHost
            {
                Source = this,
                MinHeight = 150,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var avatar = new Border
            {
                Background = SampleTheme.AccentSoftBrush,
                CornerRadius = new CornerRadius(22),
                Child = new TextBlock
                {
                    Text = "LA",
                    Foreground = SampleTheme.AccentBrush,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                },
            };
            PretextControlLayoutHost.SetLayoutKey(avatar, "avatar");

            _senderView = new PretextParagraphView("Helvetica Neue", 16, 20, SampleTheme.InkBrush, FontWeights.SemiBold);
            PretextControlLayoutHost.SetLayoutKey(_senderView, "sender");

            _timeBlock = new TextBlock
            {
                Text = _definition.Data.TimeLabel,
                Foreground = SampleTheme.MutedBrush,
                FontSize = 13,
                FontFamily = new FontFamily("Helvetica Neue"),
                TextWrapping = TextWrapping.NoWrap,
                HorizontalTextAlignment = TextAlignment.Right,
            };
            PretextControlLayoutHost.SetLayoutKey(_timeBlock, "time");

            _subjectView = new PretextParagraphView("Helvetica Neue", 17, 22, SampleTheme.InkBrush, FontWeights.Bold);
            PretextControlLayoutHost.SetLayoutKey(_subjectView, "subject");

            _previewView = new PretextParagraphView("Helvetica Neue", 14, 20, SampleTheme.MutedBrush, FontWeights.Normal);
            PretextControlLayoutHost.SetLayoutKey(_previewView, "preview");

            _statusText = new TextBlock
            {
                Text = _definition.Data.StatusLabel,
                Foreground = SampleTheme.AccentBrush,
                FontSize = 12,
                FontFamily = new FontFamily("Helvetica Neue"),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextAlignment = TextAlignment.Center,
            };
            _statusChip = new Border
            {
                Background = SampleTheme.AccentSoftBrush,
                BorderBrush = SampleTheme.RuleBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Child = _statusText,
            };
            PretextControlLayoutHost.SetLayoutKey(_statusChip, "status");

            _host.Children.Add(avatar);
            _host.Children.Add(_senderView);
            _host.Children.Add(_timeBlock);
            _host.Children.Add(_subjectView);
            _host.Children.Add(_previewView);
            _host.Children.Add(_statusChip);

            Child = _host;
            Loaded += (_, _) => RenderText();
            _host.SizeChanged += (_, _) => RenderText();
        }

        public LayoutFingerprint GetLayoutFingerprint() => _definition.GetLayoutFingerprint();

        public PreparedControlModel Prepare(LayoutFingerprint fingerprint) => _definition.Prepare(fingerprint);

        public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints) => _definition.Solve(prepared, constraints);

        private void RenderText()
        {
            RenderParagraph(_senderView, _definition.PreparedSender, ref _lastSenderWidth);
            RenderParagraph(_subjectView, _definition.PreparedSubject, ref _lastSubjectWidth);
            RenderParagraph(_previewView, _definition.PreparedPreview, ref _lastPreviewWidth);
        }
    }

    private sealed class NoteCardDemoControl : Border, IPretextControlLayoutSource
    {
        private readonly NoteCardSampleDefinition _definition = new(
            new NoteCardSampleData(
                "Editorial",
                "Shared sample classes can drive retained note cards",
                "The title, excerpt, metadata, badge, and action all route through a shared prepared-control definition instead of being reauthored separately for each UI framework.",
                "Updated 6m ago · 3 comments",
                "Open note"));

        private readonly PretextControlLayoutHost _host;
        private readonly TextBlock _badgeText;
        private readonly Border _badge;
        private readonly TextBlock _meta;
        private readonly Button _action;
        private readonly PretextParagraphView _titleView;
        private readonly PretextParagraphView _excerptView;
        private double _lastTitleWidth = -1;
        private double _lastExcerptWidth = -1;

        public NoteCardDemoControl()
        {
            Background = SampleTheme.PanelBrush;
            BorderBrush = SampleTheme.RuleBrush;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(20);
            Padding = new Thickness(0);

            _host = new PretextControlLayoutHost
            {
                Source = this,
                MinHeight = 260,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            _badgeText = new TextBlock
            {
                Text = _definition.Data.Category,
                Foreground = SampleTheme.AccentBrush,
                FontSize = 12,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextAlignment = TextAlignment.Center,
            };
            _badge = new Border
            {
                Background = SampleTheme.AccentSoftBrush,
                BorderBrush = SampleTheme.RuleBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(15),
                Child = _badgeText,
            };
            PretextControlLayoutHost.SetLayoutKey(_badge, "badge");

            _titleView = new PretextParagraphView("Georgia", 26, 30, SampleTheme.InkBrush, FontWeights.Bold);
            PretextControlLayoutHost.SetLayoutKey(_titleView, "title");

            _excerptView = new PretextParagraphView("Helvetica Neue", 15, 22, SampleTheme.MutedBrush, FontWeights.Normal);
            PretextControlLayoutHost.SetLayoutKey(_excerptView, "excerpt");

            _meta = new TextBlock
            {
                Text = _definition.Data.MetaLabel,
                Foreground = SampleTheme.MutedBrush,
                FontSize = 13,
                FontFamily = new FontFamily("Helvetica Neue"),
                TextWrapping = TextWrapping.WrapWholeWords,
            };
            PretextControlLayoutHost.SetLayoutKey(_meta, "meta");

            _action = new Button
            {
                Content = _definition.Data.ActionLabel,
                Background = SampleTheme.InkBrush,
                Foreground = SampleTheme.WhiteBrush,
                BorderThickness = new Thickness(0),
            };
            PretextControlLayoutHost.SetLayoutKey(_action, "action");

            _host.Children.Add(_badge);
            _host.Children.Add(_titleView);
            _host.Children.Add(_excerptView);
            _host.Children.Add(_meta);
            _host.Children.Add(_action);

            Child = _host;
            Loaded += (_, _) => RenderText();
            _host.SizeChanged += (_, _) => RenderText();
        }

        public LayoutFingerprint GetLayoutFingerprint() => _definition.GetLayoutFingerprint();

        public PreparedControlModel Prepare(LayoutFingerprint fingerprint) => _definition.Prepare(fingerprint);

        public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints) => _definition.Solve(prepared, constraints);

        private void RenderText()
        {
            RenderParagraph(_titleView, _definition.PreparedTitle, ref _lastTitleWidth);
            RenderParagraph(_excerptView, _definition.PreparedExcerpt, ref _lastExcerptWidth);
        }
    }

    private static void RenderParagraph(PretextParagraphView view, PreparedTextWithSegments prepared, ref double lastWidth)
    {
        if (view.ActualWidth < 1 || Math.Abs(view.ActualWidth - lastWidth) <= 0.5)
        {
            return;
        }

        lastWidth = view.ActualWidth;
        view.Render(prepared, view.ActualWidth);
    }

    private sealed class MailShellDemoControl : Border, IPretextControlLayoutSource
    {
        private readonly MailShellSampleDefinition _definition = new(
            new MailShellSampleData(
                "Northwind Mail",
                "Inbox geometry rendered without adaptive Grids",
                5,
                18,
                "Prepared geometry keeps the reader pane stable"));

        private readonly PretextControlLayoutHost _host;

        public MailShellDemoControl()
        {
            Background = SampleTheme.PanelBrush;
            BorderBrush = SampleTheme.RuleBrush;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(20);
            Padding = new Thickness(0);

            _host = new PretextControlLayoutHost
            {
                Source = this,
                MinHeight = 560,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            _host.Children.Add(CreatePane("toolbar", "Mail Toolbar", "Search, compose, archive"));
            _host.Children.Add(CreatePane("folders", "Folders", "Inbox, Design, Launch"));
            _host.Children.Add(CreatePane("messages", "Messages", "Exact preview heights, stable rows"));
            _host.Children.Add(CreatePane("reader", "Reader", "Selected message pane"));

            Child = _host;
        }

        public LayoutFingerprint GetLayoutFingerprint() => _definition.GetLayoutFingerprint();

        public PreparedControlModel Prepare(LayoutFingerprint fingerprint) => _definition.Prepare(fingerprint);

        public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints) => _definition.Solve(prepared, constraints);
    }

    private sealed class NotesShellDemoControl : Border, IPretextControlLayoutSource
    {
        private readonly NotesShellSampleDefinition _definition = new(
            new NotesShellSampleData(
                "Field Notes",
                "Absolute layout with note cards and a reading editor",
                6,
                "Shared shell geometry keeps the selected note stable"));

        private readonly PretextControlLayoutHost _host;

        public NotesShellDemoControl()
        {
            Background = SampleTheme.PanelBrush;
            BorderBrush = SampleTheme.RuleBrush;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(20);
            Padding = new Thickness(0);

            _host = new PretextControlLayoutHost
            {
                Source = this,
                MinHeight = 560,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            _host.Children.Add(CreatePane("toolbar", "Notes Toolbar", "Search, new note, share"));
            _host.Children.Add(CreatePane("collection", "Collection", "Cards repack across widths"));
            _host.Children.Add(CreatePane("editor", "Editor", "Selected note body"));
            _host.Children.Add(CreatePane("insight", "Insight Rail", "Tags, backlinks, context"));

            Child = _host;
        }

        public LayoutFingerprint GetLayoutFingerprint() => _definition.GetLayoutFingerprint();

        public PreparedControlModel Prepare(LayoutFingerprint fingerprint) => _definition.Prepare(fingerprint);

        public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints) => _definition.Solve(prepared, constraints);
    }

    private static Border CreatePane(string key, string title, string description)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = SampleTheme.InkBrush,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Georgia"),
            TextWrapping = TextWrapping.NoWrap,
        });
        stack.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = SampleTheme.MutedBrush,
            FontSize = 13,
            TextWrapping = TextWrapping.WrapWholeWords,
        });

        var pane = new Border
        {
            Background = SampleTheme.Brush(0xFB, 0xF7, 0xF0),
            BorderBrush = SampleTheme.RuleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(16),
            Child = stack,
        };
        PretextControlLayoutHost.SetLayoutKey(pane, key);
        return pane;
    }
}
