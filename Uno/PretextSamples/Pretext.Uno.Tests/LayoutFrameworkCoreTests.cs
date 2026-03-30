using Pretext.LayoutFramework;
using Pretext.LayoutFramework.Samples;
using Xunit;

namespace Pretext.Uno.Tests;

public sealed class LayoutFrameworkCoreTests
{
    [Fact]
    public void FingerprintBuilder_IsDeterministic_ForSameInputs()
    {
        var first = LayoutFingerprintBuilder.Create();
        first.Add("mail-row");
        first.Add(3);
        first.Add(480d);
        first.Add(true);
        first.Add(new LayoutSize(200, 40));

        var second = LayoutFingerprintBuilder.Create();
        second.Add("mail-row");
        second.Add(3);
        second.Add(480d);
        second.Add(true);
        second.Add(new LayoutSize(200, 40));

        Assert.Equal(first.ToFingerprint(), second.ToFingerprint());
    }

    [Fact]
    public void FingerprintBuilder_Changes_WhenInputOrderChanges()
    {
        var first = LayoutFingerprintBuilder.Create();
        first.Add("mail-row");
        first.Add(3);

        var second = LayoutFingerprintBuilder.Create();
        second.Add(3);
        second.Add("mail-row");

        Assert.NotEqual(first.ToFingerprint(), second.ToFingerprint());
    }

    [Fact]
    public void PreparedItemsModel_Validates_TextHandleLengths()
    {
        var items = new[]
        {
            new PreparedItemMetrics(PreparedItemKind.Text, 120, 30),
            new PreparedItemMetrics(PreparedItemKind.Text, 140, 40),
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            new PreparedItemsModel(
                new LayoutFingerprint(1),
                items,
                textHandles: new PreparedText?[] { null }));

        Assert.Equal("textHandles", exception.ParamName);
    }

    [Fact]
    public void PreparedItemsModel_Exposes_ItemAndHandleData()
    {
        var text = PretextLayout.Prepare("hello world", "16px Test Sans");
        var rich = PretextLayout.PrepareWithSegments("hello world", "16px Test Sans");
        var items = new[]
        {
            new PreparedItemMetrics(PreparedItemKind.Text, 120, 30, 2),
        };

        var model = new PreparedItemsModel(
            new LayoutFingerprint(42),
            items,
            textHandles: new PreparedText?[] { text },
            richTextHandles: new PreparedTextWithSegments?[] { rich });

        Assert.Equal(1, model.Count);
        Assert.Equal(PreparedItemKind.Text, model.GetItem(0).Kind);
        Assert.Same(text, model.GetTextHandleOrDefault(0));
        Assert.Same(rich, model.GetRichTextHandleOrDefault(0));
    }

    [Fact]
    public void VerticalOcclusionIndex_ReturnsExpectedVisibleRange()
    {
        var index = new VerticalOcclusionIndex(
            [
                new VerticalBand(0, 2, 0, 100),
                new VerticalBand(2, 4, 100, 210),
                new VerticalBand(4, 6, 210, 320),
            ]);

        var visible = index.TryQuery(95, 215, out var range);

        Assert.True(visible);
        Assert.Equal(new VerticalOcclusionRange(0, 6, 0, 3), range);
    }

    [Fact]
    public void VerticalOcclusionIndex_ReturnsExplicitSparseSelection_WhenBandsCarryItemSets()
    {
        var index = new VerticalOcclusionIndex(
            [
                new VerticalBand(0, 3, 0, 100, [0, 2, 4]),
                new VerticalBand(1, 5, 100, 200, [1, 3, 5]),
                new VerticalBand(6, 8, 200, 300, [6, 7]),
            ]);

        var visible = index.TryQuerySelection(90, 210, out var selection);

        Assert.True(visible);
        Assert.Equal(0, selection.StartIndex);
        Assert.Equal(8, selection.EndIndexExclusive);
        Assert.Equal(0, selection.StartBandIndex);
        Assert.Equal(3, selection.EndBandIndexExclusive);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, selection.ItemIndices);
    }

    [Fact]
    public void SolvedLayout_Queries_Placements_AndViewport()
    {
        var occlusion = new VerticalOcclusionIndex(
            [
                new VerticalBand(0, 1, 0, 40),
                new VerticalBand(1, 2, 40, 90),
            ]);

        var layout = new SolvedLayout(
            new LayoutFingerprint(7),
            new LayoutConstraints(320, 200, new LayoutViewport(0, 20, 320, 60)),
            new LayoutSize(320, 90),
            [
                new LayoutPlacement(0, "subject", new LayoutRect(0, 0, 320, 40)),
                new LayoutPlacement(1, "body", new LayoutRect(0, 40, 320, 50)),
            ],
            occlusion);

        Assert.True(layout.TryGetPlacement("body", out var placement));
        Assert.Equal(new LayoutRect(0, 40, 320, 50), placement.Bounds);

        var visible = layout.TryQueryViewport(new LayoutViewport(0, 10, 320, 50), out var range);
        Assert.True(visible);
        Assert.Equal(new VerticalOcclusionRange(0, 2, 0, 2), range);
    }

    [Fact]
    public void PreparedLayoutController_Reuses_Prepared_State_ForGeometry_Only_Changes()
    {
        var controller = new PreparedLayoutController<PreparedPanelModel>();
        var prepareCalls = 0;
        var solveCalls = 0;

        PreparedPanelModel Prepare(LayoutFingerprint fingerprint)
        {
            prepareCalls++;
            return new PreparedPanelModel(
                fingerprint,
                [new PreparedNode("body", PreparedNodeKind.Text, PreparedNodeMetrics.Fixed(120, 30))],
                LayoutSize.Empty);
        }

        SolvedLayout Solve(PreparedPanelModel prepared, LayoutConstraints constraints)
        {
            solveCalls++;
            return new SolvedLayout(
                prepared.Fingerprint,
                constraints,
                new LayoutSize(constraints.AvailableWidth, 40),
                [new LayoutPlacement(0, "body", new LayoutRect(0, 0, constraints.AvailableWidth, 40))]);
        }

        var fingerprint = new LayoutFingerprint(10);
        controller.EnsureMeasuredLayout(fingerprint, new LayoutConstraints(320, 100), Prepare, Solve);
        controller.EnsureMeasuredLayout(fingerprint, new LayoutConstraints(280, 100), Prepare, Solve);
        controller.EnsureArrangedLayout(fingerprint, new LayoutConstraints(280, 100), Prepare, Solve);

        Assert.Equal(1, prepareCalls);
        Assert.Equal(3, solveCalls);
    }

    [Fact]
    public void PreparedLayoutController_Rebuilds_When_Fingerprint_Changes()
    {
        var controller = new PreparedLayoutController<PreparedPanelModel>();
        var prepareCalls = 0;

        PreparedPanelModel Prepare(LayoutFingerprint fingerprint)
        {
            prepareCalls++;
            return new PreparedPanelModel(
                fingerprint,
                [new PreparedNode("body", PreparedNodeKind.Text, PreparedNodeMetrics.Fixed(120, 30))],
                LayoutSize.Empty);
        }

        SolvedLayout Solve(PreparedPanelModel prepared, LayoutConstraints constraints)
        {
            return new SolvedLayout(
                prepared.Fingerprint,
                constraints,
                new LayoutSize(120, 40),
                [new LayoutPlacement(0, "body", new LayoutRect(0, 0, 120, 40))]);
        }

        controller.EnsureMeasuredLayout(new LayoutFingerprint(10), new LayoutConstraints(320, 100), Prepare, Solve);
        controller.EnsureMeasuredLayout(new LayoutFingerprint(11), new LayoutConstraints(320, 100), Prepare, Solve);

        Assert.Equal(2, prepareCalls);
    }

    [Fact]
    public void PreparedLayoutController_Records_Diagnostics_For_Prepare_And_Solve_Cache_Activity()
    {
        var controller = new PreparedLayoutController<PreparedPanelModel>();

        PreparedPanelModel Prepare(LayoutFingerprint fingerprint)
        {
            return new PreparedPanelModel(
                fingerprint,
                [new PreparedNode("body", PreparedNodeKind.Text, PreparedNodeMetrics.Fixed(120, 30))],
                LayoutSize.Empty);
        }

        SolvedLayout Solve(PreparedPanelModel prepared, LayoutConstraints constraints)
        {
            return new SolvedLayout(
                prepared.Fingerprint,
                constraints,
                new LayoutSize(constraints.AvailableWidth, 40),
                [new LayoutPlacement(0, "body", new LayoutRect(0, 0, constraints.AvailableWidth, 40))]);
        }

        var fingerprint = new LayoutFingerprint(10);
        controller.EnsureMeasuredLayout(fingerprint, new LayoutConstraints(320, 100), Prepare, Solve);
        controller.EnsureMeasuredLayout(fingerprint, new LayoutConstraints(320, 100), Prepare, Solve);
        controller.EnsureArrangedLayout(fingerprint, new LayoutConstraints(320, 100), Prepare, Solve);
        controller.EnsureArrangedLayout(fingerprint, new LayoutConstraints(320, 100), Prepare, Solve);

        var snapshot = controller.Snapshot;
        Assert.Equal(4, snapshot.PrepareCount);
        Assert.Equal(3, snapshot.PrepareCacheHits);
        Assert.Equal(1, snapshot.PrepareCacheMisses);
        Assert.Equal(2, snapshot.MeasureAccessCount);
        Assert.Equal(1, snapshot.MeasureCacheHits);
        Assert.Equal(1, snapshot.MeasureCacheMisses);
        Assert.Equal(1, snapshot.MeasureSolveCount);
        Assert.Equal(2, snapshot.ArrangeAccessCount);
        Assert.Equal(1, snapshot.ArrangeCacheHits);
        Assert.Equal(1, snapshot.ArrangeCacheMisses);
        Assert.Equal(1, snapshot.ArrangeSolveCount);
        Assert.Equal(0.75d, snapshot.PrepareCacheHitRate, 3);
        Assert.Equal(0.5d, snapshot.SolveCacheHitRate, 3);
    }

    [Fact]
    public void LayoutDiagnosticsTracker_Deduplicates_Viewport_And_Realization_Updates()
    {
        var tracker = new LayoutDiagnosticsTracker();
        var viewport = new LayoutViewport(0, 40, 320, 120);
        var range = new VerticalOcclusionRange(10, 18, 2, 4);

        tracker.RecordViewportUpdate(viewport);
        tracker.RecordViewportUpdate(viewport);
        tracker.RecordViewportUpdate(new LayoutViewport(0, 80, 320, 120));

        tracker.RecordRealization(range, 8);
        tracker.RecordRealization(range, 8);
        tracker.RecordRealization(new VerticalOcclusionRange(12, 20, 3, 5), 8);
        tracker.RecordRealization(null, 0);

        var snapshot = tracker.Snapshot;
        Assert.Equal(2, snapshot.ViewportUpdateCount);
        Assert.Equal(new LayoutViewport(0, 80, 320, 120), snapshot.LastViewport);
        Assert.Equal(3, snapshot.RealizationUpdateCount);
        Assert.Null(snapshot.LastRealizedRange);
        Assert.Equal(0, snapshot.LastRealizedElementCount);
    }

    [Fact]
    public void MailRowSampleDefinition_Changes_Time_Column_Between_Wide_And_Narrow_Widths()
    {
        var definition = new MailRowSampleDefinition(
            new MailRowSampleData(
                "Mina Chen",
                "09:42",
                "Prepared geometry keeps the inbox row stable",
                "The sender, subject, preview, and accessory chip all come from one shared control model.",
                "Needs review",
                true));

        var fingerprint = definition.GetLayoutFingerprint();
        var prepared = definition.Prepare(fingerprint);
        var wide = definition.Solve(prepared, new LayoutConstraints(640, 220));
        var narrow = definition.Solve(prepared, new LayoutConstraints(340, 260));

        Assert.True(wide.TryGetPlacement("time", out var wideTime));
        Assert.True(narrow.TryGetPlacement("time", out var narrowTime));
        Assert.True(wide.TryGetPlacement("sender", out var sender));
        Assert.True(narrow.TryGetPlacement("status", out var narrowStatus));

        Assert.True(wideTime.Bounds.X > sender.Bounds.Right);
        Assert.True(narrowTime.Bounds.Y > sender.Bounds.Y);
        Assert.True(narrowStatus.Bounds.Right <= narrow.Extent.Width);
    }

    [Fact]
    public void NoteCardSampleDefinition_Changes_Metadata_Region_Between_Wide_And_Narrow_Widths()
    {
        var definition = new NoteCardSampleDefinition(
            new NoteCardSampleData(
                "Editorial",
                "Shared sample classes can drive retained controls",
                "The same prepared slot model can place a category badge, long-form title, excerpt, metadata, and call to action in both Uno and Avalonia hosts.",
                "Updated 6m ago · 3 comments",
                "Open note"));

        var fingerprint = definition.GetLayoutFingerprint();
        var prepared = definition.Prepare(fingerprint);
        var wide = definition.Solve(prepared, new LayoutConstraints(720, 420));
        var narrow = definition.Solve(prepared, new LayoutConstraints(360, 520));

        Assert.True(wide.TryGetPlacement("meta", out var wideMeta));
        Assert.True(wide.TryGetPlacement("title", out var wideTitle));
        Assert.True(narrow.TryGetPlacement("meta", out var narrowMeta));
        Assert.True(narrow.TryGetPlacement("excerpt", out var narrowExcerpt));

        Assert.True(wideMeta.Bounds.X > wideTitle.Bounds.Right);
        Assert.True(narrowMeta.Bounds.Y > narrowExcerpt.Bounds.Bottom);
    }

    [Fact]
    public void MailShellSampleDefinition_Switches_From_Three_Panes_To_Stacked_Mode()
    {
        var definition = new MailShellSampleDefinition(
            new MailShellSampleData(
                "Northwind Mail",
                "Inbox geometry rendered without adaptive Grids",
                5,
                18,
                "Prepared geometry keeps the reader pane stable"));

        var prepared = definition.Prepare(definition.GetLayoutFingerprint());
        var wide = definition.Solve(prepared, new LayoutConstraints(1280, 900));
        var narrow = definition.Solve(prepared, new LayoutConstraints(520, 1400));

        Assert.True(wide.TryGetPlacement("folders", out var wideFolders));
        Assert.True(wide.TryGetPlacement("messages", out var wideMessages));
        Assert.True(wide.TryGetPlacement("reader", out var wideReader));
        Assert.True(narrow.TryGetPlacement("folders", out var narrowFolders));
        Assert.True(narrow.TryGetPlacement("messages", out var narrowMessages));
        Assert.True(narrow.TryGetPlacement("reader", out var narrowReader));

        Assert.True(wideFolders.Bounds.Right < wideMessages.Bounds.X);
        Assert.True(wideMessages.Bounds.Right < wideReader.Bounds.X);
        Assert.True(narrowMessages.Bounds.Y > narrowFolders.Bounds.Bottom);
        Assert.True(narrowReader.Bounds.Y > narrowMessages.Bounds.Bottom);
    }

    [Fact]
    public void NotesShellSampleDefinition_Switches_From_Three_Panes_To_Stacked_Mode()
    {
        var definition = new NotesShellSampleDefinition(
            new NotesShellSampleData(
                "Field Notes",
                "Absolute layout with note cards and a reading editor",
                6,
                "Shared shell geometry keeps the selected note stable"));

        var prepared = definition.Prepare(definition.GetLayoutFingerprint());
        var wide = definition.Solve(prepared, new LayoutConstraints(1280, 1100));
        var narrow = definition.Solve(prepared, new LayoutConstraints(520, 1400));

        Assert.True(wide.TryGetPlacement("collection", out var wideCollection));
        Assert.True(wide.TryGetPlacement("editor", out var wideEditor));
        Assert.True(wide.TryGetPlacement("insight", out var wideInsight));
        Assert.True(narrow.TryGetPlacement("collection", out var narrowCollection));
        Assert.True(narrow.TryGetPlacement("editor", out var narrowEditor));
        Assert.True(narrow.TryGetPlacement("insight", out var narrowInsight));

        Assert.True(wideCollection.Bounds.Right < wideEditor.Bounds.X);
        Assert.True(wideEditor.Bounds.Right < wideInsight.Bounds.X);
        Assert.True(narrowEditor.Bounds.Y > narrowCollection.Bounds.Bottom);
        Assert.True(narrowInsight.Bounds.Y > narrowEditor.Bounds.Bottom);
    }

    [Fact]
    public void WrapItemsSampleDefinition_Builds_RowBands_And_ViewportSelection()
    {
        var definition = WrapItemsSampleDefinition.CreateDemo(itemCount: 240, templateCount: 40);
        var prepared = definition.Prepare(definition.GetLayoutFingerprint());
        var solved = definition.Solve(prepared, new LayoutConstraints(720, 640, new LayoutViewport(0, 0, 720, 240)));

        Assert.True(solved.Extent.Height > 0);
        Assert.Equal(240, prepared.Count);
        Assert.NotNull(solved.VerticalOcclusion);
        Assert.True(solved.TryQueryViewportSelection(new LayoutViewport(0, 0, 720, 240), out var selection));
        Assert.Null(selection.ItemIndices);
        Assert.True(selection.EndIndexExclusive > selection.StartIndex);
        Assert.True(selection.EndBandIndexExclusive > selection.StartBandIndex);
    }

    [Fact]
    public void MasonryItemsSampleDefinition_ProducesSparseViewportSelection()
    {
        var definition = MasonryItemsSampleDefinition.CreateDemo(itemCount: 180);
        var prepared = definition.Prepare(definition.GetLayoutFingerprint());
        var solved = definition.Solve(prepared, new LayoutConstraints(960, 720, new LayoutViewport(0, 180, 960, 260)));

        Assert.True(solved.Extent.Height > 0);
        Assert.Equal(180, prepared.Count);
        Assert.NotNull(solved.VerticalOcclusion);
        Assert.True(solved.TryQueryViewportSelection(new LayoutViewport(0, 180, 960, 260), out var selection));
        Assert.NotNull(selection.ItemIndices);
        Assert.True(selection.ItemIndices!.Length > 0);
        Assert.True(selection.ItemIndices.Length < 180);
    }
}
