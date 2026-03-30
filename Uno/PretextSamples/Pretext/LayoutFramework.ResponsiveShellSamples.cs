namespace Pretext.LayoutFramework.Samples;

public sealed record MailShellSampleData(
    string Title,
    string Caption,
    int FolderCount,
    int MessageCount,
    string SelectedMessageTitle);

public sealed class MailShellSampleDefinition : IPreparedControlLayoutDefinition
{
    public MailShellSampleDefinition(MailShellSampleData data)
    {
        Data = data;
    }

    public MailShellSampleData Data { get; }

    public LayoutFingerprint GetLayoutFingerprint()
    {
        var builder = LayoutFingerprintBuilder.Create();
        builder.Add("mail-shell-sample");
        builder.Add(Data.Title);
        builder.Add(Data.Caption);
        builder.Add(Data.FolderCount);
        builder.Add(Data.MessageCount);
        builder.Add(Data.SelectedMessageTitle);
        return builder.ToFingerprint();
    }

    public PreparedControlModel Prepare(LayoutFingerprint fingerprint)
    {
        return new PreparedControlModel(
            fingerprint,
            [
                new PreparedControlSlot("toolbar", ControlSlotKind.Header, PreparedNodeMetrics.Flexible(new LayoutSize(960, 76), new LayoutSize(320, 118))),
                new PreparedControlSlot("folders", ControlSlotKind.Content, PreparedNodeMetrics.Flexible(new LayoutSize(186, 636), new LayoutSize(220, 84))),
                new PreparedControlSlot("messages", ControlSlotKind.Body, PreparedNodeMetrics.Flexible(new LayoutSize(336, 636), new LayoutSize(280, 482))),
                new PreparedControlSlot("reader", ControlSlotKind.Body, PreparedNodeMetrics.Flexible(new LayoutSize(420, 636), new LayoutSize(280, 568))),
            ],
            LayoutSize.Empty,
            Data);
    }

    public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints)
    {
        const double wideThreshold = 1180;
        const double mediumThreshold = 820;
        const double compactToolbarHeight = 118;
        const double standardToolbarHeight = 76;
        const double narrowGutter = 18;
        const double standardGutter = 24;
        const double spacing = 18;
        const double paneGap = 16;

        var availableWidth = Math.Max(360, constraints.AvailableWidth);
        var wide = availableWidth >= wideThreshold;
        var medium = !wide && availableWidth >= mediumThreshold;
        var gutter = medium || wide ? standardGutter : narrowGutter;
        var toolbarHeight = medium || wide ? standardToolbarHeight : compactToolbarHeight;

        var toolbarRect = new LayoutRect(gutter, gutter, availableWidth - gutter * 2, toolbarHeight);
        LayoutRect foldersRect;
        LayoutRect messagesRect;
        LayoutRect readerRect;

        if (wide)
        {
            foldersRect = new LayoutRect(gutter, gutter + toolbarHeight + spacing, 186, 636);
            messagesRect = new LayoutRect(foldersRect.Right + paneGap, foldersRect.Y, 336, 636);
            readerRect = new LayoutRect(messagesRect.Right + paneGap, foldersRect.Y, availableWidth - messagesRect.Right - gutter - paneGap, 636);
        }
        else if (medium)
        {
            foldersRect = new LayoutRect(gutter, gutter + toolbarHeight + spacing, availableWidth - gutter * 2, 84);
            messagesRect = new LayoutRect(gutter, foldersRect.Bottom + spacing, 332, 610);
            readerRect = new LayoutRect(messagesRect.Right + paneGap, messagesRect.Y, availableWidth - messagesRect.Right - gutter - paneGap, 610);
        }
        else
        {
            foldersRect = new LayoutRect(gutter, gutter + toolbarHeight + spacing, availableWidth - gutter * 2, 84);
            messagesRect = new LayoutRect(gutter, foldersRect.Bottom + spacing, availableWidth - gutter * 2, 482);
            readerRect = new LayoutRect(gutter, messagesRect.Bottom + spacing, availableWidth - gutter * 2, 568);
        }

        return new SolvedLayout(
            prepared.Fingerprint,
            constraints,
            new LayoutSize(availableWidth, readerRect.Bottom + gutter),
            [
                new LayoutPlacement(0, "toolbar", toolbarRect),
                new LayoutPlacement(1, "folders", foldersRect),
                new LayoutPlacement(2, "messages", messagesRect),
                new LayoutPlacement(3, "reader", readerRect),
            ]);
    }
}

public sealed record NotesShellSampleData(
    string Title,
    string Caption,
    int NoteCount,
    string SelectedNoteTitle);

public sealed class NotesShellSampleDefinition : IPreparedControlLayoutDefinition
{
    public NotesShellSampleDefinition(NotesShellSampleData data)
    {
        Data = data;
    }

    public NotesShellSampleData Data { get; }

    public LayoutFingerprint GetLayoutFingerprint()
    {
        var builder = LayoutFingerprintBuilder.Create();
        builder.Add("notes-shell-sample");
        builder.Add(Data.Title);
        builder.Add(Data.Caption);
        builder.Add(Data.NoteCount);
        builder.Add(Data.SelectedNoteTitle);
        return builder.ToFingerprint();
    }

    public PreparedControlModel Prepare(LayoutFingerprint fingerprint)
    {
        return new PreparedControlModel(
            fingerprint,
            [
                new PreparedControlSlot("toolbar", ControlSlotKind.Header, PreparedNodeMetrics.Flexible(new LayoutSize(960, 76), new LayoutSize(320, 118))),
                new PreparedControlSlot("collection", ControlSlotKind.Body, PreparedNodeMetrics.Flexible(new LayoutSize(320, 660), new LayoutSize(280, 420))),
                new PreparedControlSlot("editor", ControlSlotKind.Body, PreparedNodeMetrics.Flexible(new LayoutSize(520, 660), new LayoutSize(280, 470))),
                new PreparedControlSlot("insight", ControlSlotKind.Accessory, PreparedNodeMetrics.Flexible(new LayoutSize(220, 660), new LayoutSize(220, 182))),
            ],
            LayoutSize.Empty,
            Data);
    }

    public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints)
    {
        const double wideThreshold = 1180;
        const double mediumThreshold = 820;
        const double compactToolbarHeight = 118;
        const double standardToolbarHeight = 76;
        const double narrowGutter = 18;
        const double standardGutter = 24;
        const double spacing = 18;
        const double paneGap = 16;

        var availableWidth = Math.Max(360, constraints.AvailableWidth);
        var wide = availableWidth >= wideThreshold;
        var medium = !wide && availableWidth >= mediumThreshold;
        var gutter = medium || wide ? standardGutter : narrowGutter;
        var toolbarHeight = medium || wide ? standardToolbarHeight : compactToolbarHeight;

        var toolbarRect = new LayoutRect(gutter, gutter, availableWidth - gutter * 2, toolbarHeight);
        LayoutRect collectionRect;
        LayoutRect editorRect;
        LayoutRect insightRect;

        if (wide)
        {
            collectionRect = new LayoutRect(gutter, gutter + toolbarHeight + spacing, 320, 660);
            editorRect = new LayoutRect(collectionRect.Right + paneGap, collectionRect.Y, availableWidth - collectionRect.Right - gutter - 252, 660);
            insightRect = new LayoutRect(editorRect.Right + paneGap, collectionRect.Y, 220, 660);
        }
        else if (medium)
        {
            collectionRect = new LayoutRect(gutter, gutter + toolbarHeight + spacing, 324, 600);
            editorRect = new LayoutRect(collectionRect.Right + paneGap, collectionRect.Y, availableWidth - collectionRect.Right - gutter - paneGap, 420);
            insightRect = new LayoutRect(collectionRect.Right + paneGap, editorRect.Bottom + spacing, editorRect.Width, 162);
        }
        else
        {
            collectionRect = new LayoutRect(gutter, gutter + toolbarHeight + spacing, availableWidth - gutter * 2, 420);
            editorRect = new LayoutRect(gutter, collectionRect.Bottom + spacing, availableWidth - gutter * 2, 470);
            insightRect = new LayoutRect(gutter, editorRect.Bottom + spacing, availableWidth - gutter * 2, 182);
        }

        return new SolvedLayout(
            prepared.Fingerprint,
            constraints,
            new LayoutSize(availableWidth, insightRect.Bottom + gutter),
            [
                new LayoutPlacement(0, "toolbar", toolbarRect),
                new LayoutPlacement(1, "collection", collectionRect),
                new LayoutPlacement(2, "editor", editorRect),
                new LayoutPlacement(3, "insight", insightRect),
            ]);
    }
}
