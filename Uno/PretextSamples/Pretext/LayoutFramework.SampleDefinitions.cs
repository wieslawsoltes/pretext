using Pretext.Uno;

namespace Pretext.LayoutFramework.Samples;

internal static class SampleDefinitionTextMetrics
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
}

public interface IPreparedControlLayoutDefinition
{
    LayoutFingerprint GetLayoutFingerprint();

    PreparedControlModel Prepare(LayoutFingerprint fingerprint);

    SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints);
}

public sealed record MailRowSampleData(
    string Sender,
    string TimeLabel,
    string Subject,
    string Preview,
    string StatusLabel,
    bool IsUnread);

public sealed class MailRowSampleDefinition : IPreparedControlLayoutDefinition
{
    private const string SenderFont = "600 16px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string MetaFont = "13px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string SubjectFont = "700 17px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string PreviewFont = "14px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string TagFont = "600 12px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const double SubjectLineHeight = 22;
    private const double PreviewLineHeight = 20;
    private const double TagHeight = 28;

    public MailRowSampleDefinition(MailRowSampleData data)
    {
        Data = data;
        PreparedSender = PretextLayout.PrepareWithSegments(data.Sender, SenderFont);
        PreparedTimeLabel = PretextLayout.PrepareWithSegments(data.TimeLabel, MetaFont);
        PreparedSubject = PretextLayout.PrepareWithSegments(data.Subject, SubjectFont);
        PreparedPreview = PretextLayout.PrepareWithSegments(data.Preview, PreviewFont);
        PreparedStatusLabel = PretextLayout.PrepareWithSegments(data.StatusLabel, TagFont);
    }

    public MailRowSampleData Data { get; }

    public PreparedTextWithSegments PreparedSender { get; }

    public PreparedTextWithSegments PreparedTimeLabel { get; }

    public PreparedTextWithSegments PreparedSubject { get; }

    public PreparedTextWithSegments PreparedPreview { get; }

    public PreparedTextWithSegments PreparedStatusLabel { get; }

    public LayoutFingerprint GetLayoutFingerprint()
    {
        var builder = LayoutFingerprintBuilder.Create();
        builder.Add("mail-row-sample");
        builder.Add(Data.Sender);
        builder.Add(Data.TimeLabel);
        builder.Add(Data.Subject);
        builder.Add(Data.Preview);
        builder.Add(Data.StatusLabel);
        builder.Add(Data.IsUnread);
        return builder.ToFingerprint();
    }

    public PreparedControlModel Prepare(LayoutFingerprint fingerprint)
    {
        var senderProbe = PretextLayout.Layout(PreparedSender, 220, 20);
        var subjectProbe = PretextLayout.Layout(PreparedSubject, 360, SubjectLineHeight);
        var previewProbe = PretextLayout.Layout(PreparedPreview, 360, PreviewLineHeight);
        var tagWidth = Math.Max(64, Math.Min(112, SampleDefinitionTextMetrics.MeasureMaxLineWidth(PreparedStatusLabel) + 28));

        return new PreparedControlModel(
            fingerprint,
            [
                new PreparedControlSlot("avatar", ControlSlotKind.Icon, PreparedNodeMetrics.Fixed(44, 44)),
                new PreparedControlSlot("sender", ControlSlotKind.Header, PreparedNodeMetrics.Flexible(new LayoutSize(220, Math.Max(20, senderProbe.Height)), new LayoutSize(120, 20)), RichText: PreparedSender),
                new PreparedControlSlot("time", ControlSlotKind.Accessory, PreparedNodeMetrics.Fixed(Math.Max(56, SampleDefinitionTextMetrics.MeasureMaxLineWidth(PreparedTimeLabel) + 8), 18), RichText: PreparedTimeLabel),
                new PreparedControlSlot("subject", ControlSlotKind.Body, PreparedNodeMetrics.Flexible(new LayoutSize(360, subjectProbe.Height), new LayoutSize(180, subjectProbe.Height)), RichText: PreparedSubject),
                new PreparedControlSlot("preview", ControlSlotKind.Body, PreparedNodeMetrics.Flexible(new LayoutSize(360, previewProbe.Height), new LayoutSize(180, previewProbe.Height)), RichText: PreparedPreview),
                new PreparedControlSlot("status", ControlSlotKind.Accessory, PreparedNodeMetrics.Fixed(tagWidth, TagHeight), RichText: PreparedStatusLabel),
            ],
            LayoutSize.Empty,
            Data);
    }

    public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints)
    {
        const double outerPadding = 16;
        const double avatarSize = 44;
        const double columnGap = 12;
        const double rowGap = 8;

        var availableWidth = Math.Max(280, constraints.AvailableWidth);
        var innerWidth = Math.Max(220, availableWidth - outerPadding * 2);
        var narrow = innerWidth < 430;
        var statusWidth = prepared.Slots.First(slot => slot.Key == "status").Metrics.IntrinsicSize.Width;
        var timeWidth = prepared.Slots.First(slot => slot.Key == "time").Metrics.IntrinsicSize.Width;
        var mainX = outerPadding + avatarSize + columnGap;
        var textWidth = narrow
            ? Math.Max(140, innerWidth - avatarSize - columnGap)
            : Math.Max(180, innerWidth - avatarSize - columnGap - Math.Max(timeWidth, statusWidth) - columnGap);

        var senderHeight = PretextLayout.Layout(PreparedSender, textWidth, 20).Height;
        var subjectHeight = PretextLayout.Layout(PreparedSubject, textWidth, SubjectLineHeight).Height;
        var previewHeight = PretextLayout.Layout(PreparedPreview, textWidth, PreviewLineHeight).Height;
        var contentY = outerPadding + senderHeight + rowGap;
        var footerY = contentY + subjectHeight + previewHeight + rowGap * 2;
        var placements = new List<LayoutPlacement>(6)
        {
            new(0, "avatar", new LayoutRect(outerPadding, outerPadding + 2, avatarSize, avatarSize)),
            new(1, "sender", new LayoutRect(mainX, outerPadding, textWidth, senderHeight)),
            new(3, "subject", new LayoutRect(mainX, contentY, textWidth, subjectHeight)),
            new(4, "preview", new LayoutRect(mainX, contentY + subjectHeight + rowGap, textWidth, previewHeight)),
        };

        if (narrow)
        {
            placements.Add(new LayoutPlacement(2, "time", new LayoutRect(mainX, footerY, timeWidth, 18)));
            placements.Add(new LayoutPlacement(5, "status", new LayoutRect(availableWidth - outerPadding - statusWidth, footerY - 5, statusWidth, TagHeight)));
            return new SolvedLayout(
                prepared.Fingerprint,
                constraints,
                new LayoutSize(availableWidth, footerY + Math.Max(18, TagHeight) + outerPadding),
                placements);
        }

        placements.Add(new LayoutPlacement(2, "time", new LayoutRect(availableWidth - outerPadding - timeWidth, outerPadding + 1, timeWidth, 18)));
        placements.Add(new LayoutPlacement(5, "status", new LayoutRect(availableWidth - outerPadding - statusWidth, contentY + subjectHeight + rowGap, statusWidth, TagHeight)));

        var rightColumnHeight = Math.Max(avatarSize, TagHeight + 18 + rowGap);
        var contentHeight = Math.Max(footerY, outerPadding + rightColumnHeight);
        return new SolvedLayout(
            prepared.Fingerprint,
            constraints,
            new LayoutSize(availableWidth, contentHeight + outerPadding),
            placements);
    }
}

public sealed record NoteCardSampleData(
    string Category,
    string Title,
    string Excerpt,
    string MetaLabel,
    string ActionLabel);

public sealed class NoteCardSampleDefinition : IPreparedControlLayoutDefinition
{
    private const string CategoryFont = "600 12px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string TitleFont = "700 26px Georgia, serif";
    private const string ExcerptFont = "15px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string MetaFont = "13px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const string ActionFont = "600 13px \"Helvetica Neue\", Helvetica, Arial, sans-serif";
    private const double TitleLineHeight = 30;
    private const double ExcerptLineHeight = 22;

    public NoteCardSampleDefinition(NoteCardSampleData data)
    {
        Data = data;
        PreparedCategory = PretextLayout.PrepareWithSegments(data.Category, CategoryFont);
        PreparedTitle = PretextLayout.PrepareWithSegments(data.Title, TitleFont);
        PreparedExcerpt = PretextLayout.PrepareWithSegments(data.Excerpt, ExcerptFont);
        PreparedMetaLabel = PretextLayout.PrepareWithSegments(data.MetaLabel, MetaFont);
        PreparedActionLabel = PretextLayout.PrepareWithSegments(data.ActionLabel, ActionFont);
    }

    public NoteCardSampleData Data { get; }

    public PreparedTextWithSegments PreparedCategory { get; }

    public PreparedTextWithSegments PreparedTitle { get; }

    public PreparedTextWithSegments PreparedExcerpt { get; }

    public PreparedTextWithSegments PreparedMetaLabel { get; }

    public PreparedTextWithSegments PreparedActionLabel { get; }

    public LayoutFingerprint GetLayoutFingerprint()
    {
        var builder = LayoutFingerprintBuilder.Create();
        builder.Add("note-card-sample");
        builder.Add(Data.Category);
        builder.Add(Data.Title);
        builder.Add(Data.Excerpt);
        builder.Add(Data.MetaLabel);
        builder.Add(Data.ActionLabel);
        return builder.ToFingerprint();
    }

    public PreparedControlModel Prepare(LayoutFingerprint fingerprint)
    {
        var titleProbe = PretextLayout.Layout(PreparedTitle, 320, TitleLineHeight);
        var excerptProbe = PretextLayout.Layout(PreparedExcerpt, 320, ExcerptLineHeight);
        var metaProbe = PretextLayout.Layout(PreparedMetaLabel, 220, 18);
        var actionWidth = Math.Max(92, SampleDefinitionTextMetrics.MeasureMaxLineWidth(PreparedActionLabel) + 28);
        var badgeWidth = Math.Max(72, SampleDefinitionTextMetrics.MeasureMaxLineWidth(PreparedCategory) + 30);

        return new PreparedControlModel(
            fingerprint,
            [
                new PreparedControlSlot("badge", ControlSlotKind.Accessory, PreparedNodeMetrics.Fixed(badgeWidth, 30), RichText: PreparedCategory),
                new PreparedControlSlot("title", ControlSlotKind.Header, PreparedNodeMetrics.Flexible(new LayoutSize(320, titleProbe.Height), new LayoutSize(220, titleProbe.Height)), RichText: PreparedTitle),
                new PreparedControlSlot("excerpt", ControlSlotKind.Body, PreparedNodeMetrics.Flexible(new LayoutSize(320, excerptProbe.Height), new LayoutSize(220, excerptProbe.Height)), RichText: PreparedExcerpt),
                new PreparedControlSlot("meta", ControlSlotKind.Footer, PreparedNodeMetrics.Flexible(new LayoutSize(220, Math.Max(18, metaProbe.Height)), new LayoutSize(140, 18)), RichText: PreparedMetaLabel),
                new PreparedControlSlot("action", ControlSlotKind.Content, PreparedNodeMetrics.Fixed(actionWidth, 34), RichText: PreparedActionLabel),
            ],
            LayoutSize.Empty,
            Data);
    }

    public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints)
    {
        const double outerPadding = 18;
        const double rowGap = 12;
        const double actionHeight = 34;

        var availableWidth = Math.Max(280, constraints.AvailableWidth);
        var innerWidth = Math.Max(220, availableWidth - outerPadding * 2);
        var wide = innerWidth >= 500;
        var badgeWidth = prepared.Slots.First(slot => slot.Key == "badge").Metrics.IntrinsicSize.Width;
        var actionWidth = prepared.Slots.First(slot => slot.Key == "action").Metrics.IntrinsicSize.Width;
        var textWidth = wide
            ? Math.Max(220, innerWidth - 168)
            : innerWidth;

        var titleHeight = PretextLayout.Layout(PreparedTitle, textWidth, TitleLineHeight).Height;
        var excerptHeight = PretextLayout.Layout(PreparedExcerpt, textWidth, ExcerptLineHeight).Height;
        var metaHeight = Math.Max(18, PretextLayout.Layout(PreparedMetaLabel, Math.Max(140, textWidth), 18).Height);
        var placements = new List<LayoutPlacement>(5)
        {
            new(0, "badge", new LayoutRect(outerPadding, outerPadding, badgeWidth, 30)),
            new(1, "title", new LayoutRect(outerPadding, outerPadding + 42, textWidth, titleHeight)),
            new(2, "excerpt", new LayoutRect(outerPadding, outerPadding + 42 + titleHeight + rowGap, textWidth, excerptHeight)),
        };

        if (wide)
        {
            var sideX = outerPadding + textWidth + rowGap;
            var sideWidth = Math.Max(116, availableWidth - sideX - outerPadding);
            placements.Add(new LayoutPlacement(3, "meta", new LayoutRect(sideX, outerPadding + 6, sideWidth, metaHeight + 20)));
            placements.Add(new LayoutPlacement(4, "action", new LayoutRect(sideX, outerPadding + 44 + metaHeight, Math.Min(sideWidth, actionWidth), actionHeight)));

            var contentHeight = Math.Max(
                outerPadding + 44 + metaHeight + rowGap + actionHeight,
                outerPadding + 42 + titleHeight + rowGap + excerptHeight);

            return new SolvedLayout(
                prepared.Fingerprint,
                constraints,
                new LayoutSize(availableWidth, contentHeight + outerPadding),
                placements);
        }

        var footerY = outerPadding + 42 + titleHeight + rowGap + excerptHeight + rowGap;
        placements.Add(new LayoutPlacement(3, "meta", new LayoutRect(outerPadding, footerY, innerWidth, metaHeight)));
        placements.Add(new LayoutPlacement(4, "action", new LayoutRect(outerPadding, footerY + metaHeight + rowGap, Math.Min(innerWidth, actionWidth + 12), actionHeight)));

        return new SolvedLayout(
            prepared.Fingerprint,
            constraints,
            new LayoutSize(availableWidth, footerY + metaHeight + rowGap + actionHeight + outerPadding),
            placements);
    }
}
