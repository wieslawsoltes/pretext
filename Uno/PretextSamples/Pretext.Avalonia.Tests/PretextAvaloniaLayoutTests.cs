using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Media;
using Pretext.Avalonia.Controls;
using Pretext.LayoutFramework;
using Pretext.LayoutFramework.Samples;

namespace Pretext.Avalonia.Tests;

public sealed class PretextAvaloniaLayoutTests
{
    [AvaloniaFact]
    public void PanelBase_Reuses_Prepared_Model_Across_Geometry_Changes()
    {
        var panel = new TestPanel();
        panel.Children.Add(new Border());
        panel.Children.Add(new Border());

        panel.Measure(new Size(320, 400));
        panel.Arrange(new Rect(0, 0, 320, 400));
        panel.Measure(new Size(280, 400));
        panel.Arrange(new Rect(0, 0, 280, 400));

        Assert.Equal(1, panel.PrepareCalls);
        Assert.Equal(4, panel.SolveCalls);
    }

    [AvaloniaFact]
    public void PanelBase_Rebuilds_Prepared_Model_When_Semantics_Change()
    {
        var panel = new TestPanel();
        panel.Children.Add(new Border());
        panel.Children.Add(new Border());

        panel.Measure(new Size(320, 400));
        panel.Arrange(new Rect(0, 0, 320, 400));

        panel.SemanticVersion++;
        panel.RefreshPreparedLayout();
        panel.Measure(new Size(320, 400));

        Assert.Equal(2, panel.PrepareCalls);
    }

    [AvaloniaFact]
    public void ControlPanel_Arranges_Children_By_Layout_Key()
    {
        var source = new TestControlSource();
        var panel = new PretextControlPanel
        {
            Source = source,
        };

        var header = new Border { Background = Brushes.Red };
        var body = new Border { Background = Brushes.Blue };
        PretextControlPanel.SetLayoutKey(header, "header");
        PretextControlPanel.SetLayoutKey(body, "body");
        panel.Children.Add(header);
        panel.Children.Add(body);

        panel.Measure(new Size(320, 240));
        panel.Arrange(new Rect(0, 0, 320, 240));

        Assert.Equal(new Rect(12, 10, 296, 34), header.Bounds);
        Assert.Equal(new Rect(12, 56, 296, 128), body.Bounds);
        Assert.Equal(1, source.PrepareCalls);
        Assert.Equal(2, source.SolveCalls);
    }

    [AvaloniaFact]
    public void SharedMailRowDefinition_Arranges_Through_ControlPanel()
    {
        var definition = new MailRowSampleDefinition(
            new MailRowSampleData(
                "Leopold Aschenbrenner",
                "11:08",
                "Situational awareness for the next decade",
                "This shared sample definition drives the same keyed control geometry in Avalonia and Uno.",
                "Pinned",
                true));
        var source = new SharedControlSource(definition);
        var panel = new PretextControlPanel { Source = source };

        var avatar = new Border();
        var sender = new Border();
        var time = new Border();
        var subject = new Border();
        var preview = new Border();
        var status = new Border();
        PretextControlPanel.SetLayoutKey(avatar, "avatar");
        PretextControlPanel.SetLayoutKey(sender, "sender");
        PretextControlPanel.SetLayoutKey(time, "time");
        PretextControlPanel.SetLayoutKey(subject, "subject");
        PretextControlPanel.SetLayoutKey(preview, "preview");
        PretextControlPanel.SetLayoutKey(status, "status");
        panel.Children.Add(avatar);
        panel.Children.Add(sender);
        panel.Children.Add(time);
        panel.Children.Add(subject);
        panel.Children.Add(preview);
        panel.Children.Add(status);

        panel.Measure(new Size(560, 220));
        panel.Arrange(new Rect(0, 0, 560, 220));

        Assert.True(time.Bounds.X > sender.Bounds.Right);
        Assert.True(status.Bounds.X > subject.Bounds.Right || status.Bounds.Y >= subject.Bounds.Y);
        Assert.Equal(1, source.PrepareCalls);
        Assert.Equal(2, source.SolveCalls);
    }

    [AvaloniaFact]
    public void SharedMailShellDefinition_Arranges_Through_ControlPanel()
    {
        var definition = new MailShellSampleDefinition(
            new MailShellSampleData(
                "Northwind Mail",
                "Inbox geometry rendered without adaptive Grids",
                5,
                18,
                "Prepared geometry keeps the reader pane stable"));
        var source = new SharedControlSource(definition);
        var panel = new PretextControlPanel { Source = source };

        var toolbar = new Border();
        var folders = new Border();
        var messages = new Border();
        var reader = new Border();
        PretextControlPanel.SetLayoutKey(toolbar, "toolbar");
        PretextControlPanel.SetLayoutKey(folders, "folders");
        PretextControlPanel.SetLayoutKey(messages, "messages");
        PretextControlPanel.SetLayoutKey(reader, "reader");
        panel.Children.Add(toolbar);
        panel.Children.Add(folders);
        panel.Children.Add(messages);
        panel.Children.Add(reader);

        panel.Measure(new Size(1240, 900));
        panel.Arrange(new Rect(0, 0, 1240, 900));

        Assert.True(folders.Bounds.Right < messages.Bounds.X);
        Assert.True(messages.Bounds.Right < reader.Bounds.X);
    }

    [AvaloniaFact]
    public void SharedNotesShellDefinition_Arranges_Through_ControlPanel()
    {
        var definition = new NotesShellSampleDefinition(
            new NotesShellSampleData(
                "Field Notes",
                "Absolute layout with note cards and a reading editor",
                6,
                "Shared shell geometry keeps the selected note stable"));
        var source = new SharedControlSource(definition);
        var panel = new PretextControlPanel { Source = source };

        var toolbar = new Border();
        var collection = new Border();
        var editor = new Border();
        var insight = new Border();
        PretextControlPanel.SetLayoutKey(toolbar, "toolbar");
        PretextControlPanel.SetLayoutKey(collection, "collection");
        PretextControlPanel.SetLayoutKey(editor, "editor");
        PretextControlPanel.SetLayoutKey(insight, "insight");
        panel.Children.Add(toolbar);
        panel.Children.Add(collection);
        panel.Children.Add(editor);
        panel.Children.Add(insight);

        panel.Measure(new Size(1240, 980));
        panel.Arrange(new Rect(0, 0, 1240, 980));

        Assert.True(collection.Bounds.Right < editor.Bounds.X);
        Assert.True(editor.Bounds.Right < insight.Bounds.X);
    }

    [AvaloniaFact]
    public void SharedWrapItemsDefinition_Realizes_Through_VirtualizingPanel()
    {
        var definition = WrapItemsSampleDefinition.CreateDemo(itemCount: 600, templateCount: 60);
        var panel = new SharedItemsVirtualizingPanel(definition);
        var window = CreateSharedItemsHost(panel, definition.Count);

        try
        {
            window.Show();
            _ = window.CaptureRenderedFrame();

            Assert.True(panel.RealizedCount > 0);
            Assert.True(panel.RealizedCount < definition.Count);
            Assert.True(panel.RealizedIndices.Count > 0);
            Assert.Equal(1, panel.PrepareCalls);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SharedMasonryItemsDefinition_Uses_SparseRealizationSelection()
    {
        var definition = MasonryItemsSampleDefinition.CreateDemo(itemCount: 220);
        var panel = new SharedItemsVirtualizingPanel(definition);
        var (window, scroller) = CreateScrollableSharedItemsHost(panel, definition.Count, width: 760, height: 260);

        try
        {
            window.Show();
            _ = window.CaptureRenderedFrame();
            scroller.Offset = new Vector(0, 900);
            _ = window.CaptureRenderedFrame();

            Assert.True(panel.RealizedCount > 0);
            Assert.True(panel.RealizedCount < definition.Count);
            Assert.NotNull(panel.LastSelection);
            Assert.NotNull(panel.LastSelection!.Value.ItemIndices);
            Assert.Equal(panel.LastSelection.Value.ItemIndices, panel.RealizedIndices);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void VirtualizingPanel_Realizes_Only_Visible_Subset()
    {
        var panel = new TestVirtualizingPanel();
        var window = CreateVirtualizingHost(panel, 120);

        try
        {
            window.Show();
            var frame = window.CaptureRenderedFrame();

            Assert.NotNull(frame);
            Assert.True(panel.RealizedCount > 0);
            Assert.True(panel.RealizedCount < 120);
            Assert.True(panel.HasContainer(0));
            Assert.Equal(1, panel.PrepareCalls);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void VirtualizingPanel_Publishes_Diagnostics_For_Viewport_And_Realization()
    {
        var panel = new TestVirtualizingPanel();
        var window = CreateVirtualizingHost(panel, 120);

        try
        {
            window.Show();
            _ = window.CaptureRenderedFrame();

            var diagnostics = panel.Diagnostics;
            Assert.True(diagnostics.PrepareCount > 0);
            Assert.True(diagnostics.SolveCount > 0);
            Assert.True(diagnostics.ViewportUpdateCount > 0);
            Assert.NotNull(diagnostics.LastViewport);
            Assert.NotNull(diagnostics.LastRealizedRange);
            Assert.Equal(panel.RealizedCount, diagnostics.LastRealizedElementCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void VirtualizingPanel_Reuses_Prepared_State_And_Can_Force_Realize_Index()
    {
        var panel = new TestVirtualizingPanel();
        var window = CreateVirtualizingHost(panel, 120);

        try
        {
            window.Show();
            _ = window.CaptureRenderedFrame();

            window.Width = 320;
            window.Height = 240;
            _ = window.CaptureRenderedFrame();
            var solveCallsAfterResize = panel.SolveCalls;

            panel.ClearRealizationProbe();
            panel.RequestBringIntoView(80);
            _ = window.CaptureRenderedFrame();

            Assert.Equal(1, panel.PrepareCalls);
            Assert.Equal(solveCallsAfterResize, panel.SolveCalls);
            Assert.True(panel.WasIndexRealized(80));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void VirtualizingItemsControl_Scrolling_Reuses_Solved_Geometry_When_Viewport_Changes()
    {
        var panel = new TestVirtualizingPanel();
        var (window, scroller, _) = CreateScrollableVirtualizingHost(panel, 120);

        try
        {
            window.Show();
            _ = window.CaptureRenderedFrame();
            var solveCallsAfterInitialRender = panel.SolveCalls;
            var viewportUpdatesAfterInitialRender = panel.Diagnostics.ViewportUpdateCount;

            scroller.Offset = new Vector(0, 900);
            _ = window.CaptureRenderedFrame();

            var diagnostics = panel.Diagnostics;
            Assert.Equal(solveCallsAfterInitialRender, panel.SolveCalls);
            Assert.True(diagnostics.ViewportUpdateCount > viewportUpdatesAfterInitialRender);
            Assert.True(diagnostics.SolveCacheHits > 0);
            Assert.True(diagnostics.SolveCacheHitRate > 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void VirtualizingItemsControl_LogicalScrollViewer_Writes_Offset_To_Panel()
    {
        var panel = new TestVirtualizingPanel();
        var (window, scroller, host) = CreateScrollableVirtualizingHost(panel, 120);

        try
        {
            window.Show();
            _ = window.CaptureRenderedFrame();

            var logical = (ILogicalScrollable)host;
            Assert.True(logical.Extent.Height > logical.Viewport.Height);
            Assert.True(panel.HasContainer(0));

            scroller.Offset = new Vector(0, 900);
            _ = window.CaptureRenderedFrame();

            Assert.True(((IScrollable)host).Offset.Y > 0);
            Assert.Equal(((IScrollable)host).Offset.Y, scroller.Offset.Y, 3);
            Assert.False(panel.HasContainer(0));
            Assert.True(panel.RealizedCount > 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void VirtualizingItemsControl_LogicalScrollViewer_BringIntoView_Updates_Offset()
    {
        var panel = new TestVirtualizingPanel();
        var (window, scroller, host) = CreateScrollableVirtualizingHost(panel, 120);

        try
        {
            window.Show();
            _ = window.CaptureRenderedFrame();

            panel.ClearRealizationProbe();
            panel.RequestBringIntoView(80);
            _ = window.CaptureRenderedFrame();

            Assert.True(panel.WasIndexRealized(80));
            Assert.True(((IScrollable)host).Offset.Y > 0);
            Assert.Equal(((IScrollable)host).Offset.Y, scroller.Offset.Y, 3);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class TestPanel : PretextPanelBase
    {
        public int SemanticVersion { get; set; } = 1;

        public int PrepareCalls { get; private set; }

        public int SolveCalls { get; private set; }

        public void RefreshPreparedLayout()
        {
            InvalidatePreparedLayout();
        }

        protected override LayoutFingerprint GetLayoutFingerprint()
        {
            var builder = LayoutFingerprintBuilder.Create();
            builder.Add(SemanticVersion);
            builder.Add(Children.Count);
            return builder.ToFingerprint();
        }

        protected override PreparedPanelModel Prepare(LayoutFingerprint fingerprint)
        {
            PrepareCalls++;
            return new PreparedPanelModel(
                fingerprint,
                [
                    new PreparedNode("first", PreparedNodeKind.Fixed, PreparedNodeMetrics.Fixed(120, 48)),
                    new PreparedNode("second", PreparedNodeKind.Fixed, PreparedNodeMetrics.Fixed(120, 48)),
                ],
                LayoutSize.Empty);
        }

        protected override SolvedLayout Solve(PreparedPanelModel prepared, LayoutConstraints constraints)
        {
            SolveCalls++;
            var width = Math.Max(120, constraints.AvailableWidth - 20);

            return new SolvedLayout(
                prepared.Fingerprint,
                constraints,
                new LayoutSize(width + 20, 132),
                [
                    new LayoutPlacement(0, "first", new LayoutRect(10, 10, width, 48)),
                    new LayoutPlacement(1, "second", new LayoutRect(10, 74, width, 48)),
                ]);
        }
    }

    private sealed class TestControlSource : PretextTemplatedControlBase
    {
        public int PrepareCalls { get; private set; }

        public int SolveCalls { get; private set; }

        protected override LayoutFingerprint GetLayoutFingerprintCore()
        {
            var builder = LayoutFingerprintBuilder.Create();
            builder.Add("test-control");
            return builder.ToFingerprint();
        }

        protected override PreparedControlModel PrepareCore(LayoutFingerprint fingerprint)
        {
            PrepareCalls++;
            return new PreparedControlModel(
                fingerprint,
                [
                    new PreparedControlSlot("header", ControlSlotKind.Header, PreparedNodeMetrics.Fixed(220, 34)),
                    new PreparedControlSlot("body", ControlSlotKind.Body, PreparedNodeMetrics.Flexible(new LayoutSize(220, 128), new LayoutSize(160, 64))),
                ],
                LayoutSize.Empty);
        }

        protected override SolvedLayout SolveCore(PreparedControlModel prepared, LayoutConstraints constraints)
        {
            SolveCalls++;
            var width = Math.Max(180, constraints.AvailableWidth - 24);

            return new SolvedLayout(
                prepared.Fingerprint,
                constraints,
                new LayoutSize(width + 24, 196),
                [
                    new LayoutPlacement(0, "header", new LayoutRect(12, 10, width, 34)),
                    new LayoutPlacement(1, "body", new LayoutRect(12, 56, width, 128)),
                ]);
        }
    }

    private sealed class SharedControlSource : IPretextControlLayoutSource
    {
        private readonly IPreparedControlLayoutDefinition _definition;

        public SharedControlSource(IPreparedControlLayoutDefinition definition)
        {
            _definition = definition;
        }

        public int PrepareCalls { get; private set; }

        public int SolveCalls { get; private set; }

        public LayoutFingerprint GetLayoutFingerprint()
        {
            return _definition.GetLayoutFingerprint();
        }

        public PreparedControlModel Prepare(LayoutFingerprint fingerprint)
        {
            PrepareCalls++;
            return _definition.Prepare(fingerprint);
        }

        public SolvedLayout Solve(PreparedControlModel prepared, LayoutConstraints constraints)
        {
            SolveCalls++;
            return _definition.Solve(prepared, constraints);
        }
    }

    private sealed class SharedItemsVirtualizingPanel : PretextVirtualizingPanelBase
    {
        private readonly IPreparedItemsLayoutDefinition _definition;

        public SharedItemsVirtualizingPanel(IPreparedItemsLayoutDefinition definition)
        {
            _definition = definition;
        }

        public int PrepareCalls { get; private set; }

        public int SolveCalls { get; private set; }

        public int RealizedCount => GetRealizedContainers()?.Count() ?? 0;

        public VerticalOcclusionSelection? LastSelection { get; private set; }

        public IReadOnlyList<int> RealizedIndices => GetRealizedContainers()
            ?.Select(control => IndexFromContainer(control))
            .Where(index => index >= 0)
            .OrderBy(index => index)
            .ToArray()
            ?? [];

        protected override LayoutFingerprint GetLayoutFingerprint()
        {
            return _definition.GetLayoutFingerprint();
        }

        protected override PreparedItemsModel Prepare(LayoutFingerprint fingerprint)
        {
            PrepareCalls++;
            return _definition.Prepare(fingerprint);
        }

        protected override SolvedLayout Solve(PreparedItemsModel prepared, LayoutConstraints constraints)
        {
            SolveCalls++;
            return _definition.Solve(prepared, constraints);
        }

        protected override bool TryGetRealizationSelection(PreparedItemsModel prepared, SolvedLayout solved, out VerticalOcclusionSelection selection)
        {
            var realized = base.TryGetRealizationSelection(prepared, solved, out selection);
            LastSelection = realized ? selection : null;
            return realized;
        }
    }

    private sealed class TestVirtualizingPanel : PretextVirtualizingPanelBase
    {
        public int SemanticVersion { get; set; } = 1;

        public int PrepareCalls { get; private set; }

        public int SolveCalls { get; private set; }

        private readonly HashSet<int> _realizedProbe = [];

        public int RealizedCount => GetRealizedContainers()?.Count() ?? 0;

        public bool HasContainer(int index) => ContainerFromIndex(index) is not null;

        public bool WasIndexRealized(int index) => _realizedProbe.Contains(index);

        public void RequestBringIntoView(int index)
        {
            _ = ScrollIntoView(index);
        }

        public void ClearRealizationProbe()
        {
            _realizedProbe.Clear();
        }

        protected override LayoutFingerprint GetLayoutFingerprint()
        {
            var builder = LayoutFingerprintBuilder.Create();
            builder.Add(SemanticVersion);
            builder.Add(Items.Count);
            return builder.ToFingerprint();
        }

        protected override PreparedItemsModel Prepare(LayoutFingerprint fingerprint)
        {
            PrepareCalls++;
            var items = new PreparedItemMetrics[Items.Count];
            for (var i = 0; i < items.Length; i++)
            {
                items[i] = new PreparedItemMetrics(PreparedItemKind.Text, 220, 36, 0);
            }

            return new PreparedItemsModel(fingerprint, items);
        }

        protected override SolvedLayout Solve(PreparedItemsModel prepared, LayoutConstraints constraints)
        {
            SolveCalls++;
            var width = Math.Max(120, constraints.AvailableWidth);
            var placements = new List<LayoutPlacement>(prepared.Count);
            var bands = new List<VerticalBand>(prepared.Count);
            var y = 0d;

            for (var index = 0; index < prepared.Count; index++)
            {
                var height = 36d + (index % 4) * 8d;
                placements.Add(new LayoutPlacement(index, $"item-{index}", new LayoutRect(0, y, width, height)));
                bands.Add(new VerticalBand(index, index + 1, y, y + height));
                y += height + 6;
            }

            return new SolvedLayout(
                prepared.Fingerprint,
                constraints,
                new LayoutSize(width, y),
                placements,
                new VerticalOcclusionIndex(bands));
        }

        protected override void MeasureElement(Control element, LayoutPlacement placement)
        {
            _realizedProbe.Add(placement.Index);
            base.MeasureElement(element, placement);
        }
    }

    private sealed class TestVirtualizingItemsControl : PretextVirtualizingItemsControl
    {
    }

    private static Window CreateVirtualizingHost(TestVirtualizingPanel panel, int itemCount)
    {
        var items = Enumerable.Range(0, itemCount).Select(index => $"Item {index}").ToArray();
        var itemsControl = new ItemsControl
        {
            Width = 240,
            Height = 180,
            ItemsSource = items,
            Template = CreateItemsControlTemplate<ItemsControl>(),
            ItemsPanel = new FuncTemplate<Panel?>(() => panel),
            ItemTemplate = new FuncDataTemplate<string>((text, _) => new Border
            {
                Height = 24,
                Child = new TextBlock { Text = text },
            }),
        };

        return new Window
        {
            Width = 260,
            Height = 220,
            Content = itemsControl,
        };
    }

    private static Window CreateSharedItemsHost(PretextVirtualizingPanelBase panel, int itemCount, double width = 320, double height = 220)
    {
        var items = Enumerable.Range(0, itemCount).ToArray();
        var itemsControl = new ItemsControl
        {
            Width = width - 20,
            Height = height - 40,
            ItemsSource = items,
            Template = CreateItemsControlTemplate<ItemsControl>(),
            ItemsPanel = new FuncTemplate<Panel?>(() => panel),
            ItemTemplate = new FuncDataTemplate<int>((index, _) => new Border
            {
                Height = 24,
                Child = new TextBlock { Text = index.ToString() },
            }),
        };

        return new Window
        {
            Width = width,
            Height = height,
            Content = itemsControl,
        };
    }

    private static (Window window, ScrollViewer scroller) CreateScrollableSharedItemsHost(
        PretextVirtualizingPanelBase panel,
        int itemCount,
        double width = 320,
        double height = 220)
    {
        var items = Enumerable.Range(0, itemCount).ToArray();
        var itemsControl = new ItemsControl
        {
            Width = width - 20,
            Height = height + 1200,
            ItemsSource = items,
            Template = CreateItemsControlTemplate<ItemsControl>(),
            ItemsPanel = new FuncTemplate<Panel?>(() => panel),
            ItemTemplate = new FuncDataTemplate<int>((index, _) => new Border
            {
                Height = 24,
                Child = new TextBlock { Text = index.ToString() },
            }),
        };

        var scroller = new ScrollViewer
        {
            Width = width - 20,
            Height = height - 20,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Template = CreateScrollViewerTemplate(),
            Content = itemsControl,
        };

        var window = new Window
        {
            Width = width,
            Height = height,
            Content = scroller,
        };

        return (window, scroller);
    }

    private static (Window window, ScrollViewer scroller, TestVirtualizingItemsControl host) CreateScrollableVirtualizingHost(
        TestVirtualizingPanel panel,
        int itemCount)
    {
        var items = Enumerable.Range(0, itemCount).Select(index => $"Item {index}").ToArray();
        var host = new TestVirtualizingItemsControl
        {
            Width = 240,
            Height = 180,
            ItemsSource = items,
            Template = CreateItemsControlTemplate<TestVirtualizingItemsControl>(),
            ItemsPanel = new FuncTemplate<Panel?>(() => panel),
            ItemTemplate = new FuncDataTemplate<string>((text, _) => new Border
            {
                Height = 24,
                Child = new TextBlock { Text = text },
            }),
        };

        var scroller = new ScrollViewer
        {
            Width = 240,
            Height = 180,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Template = CreateScrollViewerTemplate(),
            Content = host,
        };

        var window = new Window
        {
            Width = 260,
            Height = 220,
            Content = scroller,
        };

        return (window, scroller, host);
    }

    private static FuncControlTemplate<TItemsControl> CreateItemsControlTemplate<TItemsControl>()
        where TItemsControl : ItemsControl
    {
        return new FuncControlTemplate<TItemsControl>((parent, scope) =>
            new ItemsPresenter
            {
                Name = "PART_ItemsPresenter",
                [~ItemsPresenter.ItemsPanelProperty] = parent[~ItemsControl.ItemsPanelProperty],
            }.RegisterInNameScope(scope));
    }

    private static FuncControlTemplate<ScrollViewer> CreateScrollViewerTemplate()
    {
        return new FuncControlTemplate<ScrollViewer>((parent, scope) =>
            new Panel
            {
                Children =
                {
                    new ScrollContentPresenter
                    {
                        Name = "PART_ContentPresenter",
                    }.RegisterInNameScope(scope),
                },
            });
    }
}
