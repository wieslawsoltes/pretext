using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Pretext.Avalonia.Samples;
using Pretext.LayoutFramework;

namespace Pretext.Avalonia.Tests;

public sealed class PretextAvaloniaBrowserScreenshotTests
{
    private static readonly (string Key, string Title)[] Samples =
    [
        ("mail-row", "Mail Row"),
        ("note-card", "Note Card"),
        ("mail-shell", "Responsive Mail Shell"),
        ("notes-shell", "Responsive Notes Shell"),
        ("wrap", "Shared Wrap Surface"),
        ("masonry", "Shared Masonry Surface"),
    ];

    [AvaloniaFact]
    public void SampleBrowser_Renders_All_Shared_Samples_And_Writes_Screenshots()
    {
        var screenshotRoot = ResolveScreenshotRoot();
        var sampleDir = Path.Combine(screenshotRoot, "avalonia-sample-browser");
        Directory.CreateDirectory(sampleDir);

        var artifacts = new List<ScreenshotArtifact>();
        var window = new MainWindow();

        try
        {
            window.Show();

            foreach (var sample in Samples)
            {
                window.SelectSample(sample.Key);

                var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Assert.Equal(sample.Key, window.SelectedSampleKey);

                var title = window.FindControl<TextBlock>("SampleTitleText");
                var diagnostics = window.FindControl<TextBlock>("DiagnosticsText");
                var sampleScrollViewer = window.FindControl<ScrollViewer>("SampleScrollViewer");
                var sampleScrollHost = window.FindControl<ContentControl>("SampleScrollHost");
                var sampleDirectHost = window.FindControl<ContentControl>("SampleDirectHost");
                Assert.NotNull(title);
                Assert.NotNull(diagnostics);
                Assert.NotNull(sampleScrollViewer);
                Assert.NotNull(sampleScrollHost);
                Assert.NotNull(sampleDirectHost);
                Assert.Equal(sample.Title, title!.Text);
                Assert.False(string.IsNullOrWhiteSpace(diagnostics!.Text));

                var useOuterScrollViewer = sample.Key is not ("wrap" or "masonry");
                Assert.Equal(useOuterScrollViewer, sampleScrollViewer!.IsVisible);
                Assert.Equal(!useOuterScrollViewer, sampleDirectHost!.IsVisible);

                var activeContent = useOuterScrollViewer ? sampleScrollHost!.Content : sampleDirectHost.Content;
                var samplePage = Assert.IsAssignableFrom<IPretextAvaloniaSample>(activeContent);
                Assert.NotEmpty(samplePage.DiagnosticsProbes);
                var snapshot = samplePage.DiagnosticsProbes[0].SnapshotAccessor();

                if (sample.Key is "wrap" or "masonry")
                {
                    snapshot = ExerciseVirtualizedSurface(window, Assert.IsAssignableFrom<Control>(samplePage), samplePage.DiagnosticsProbes[0]);
                }

                AssertSampleDiagnostics(sample.Key, snapshot);

                var screenshotPath = Path.Combine(sampleDir, $"{sample.Key}.png");
                frame.Save(screenshotPath);
                Assert.True(File.Exists(screenshotPath));

                var fileInfo = new FileInfo(screenshotPath);
                artifacts.Add(new ScreenshotArtifact(
                    fileInfo.FullName,
                    Path.Combine("avalonia-sample-browser", fileInfo.Name),
                    frame.PixelSize.Width,
                    frame.PixelSize.Height,
                    fileInfo.Length,
                    fileInfo.LastWriteTimeUtc));
            }

            var manifestPath = Path.Combine(sampleDir, "manifest.json");
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(
                    artifacts,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    }));

            Assert.True(File.Exists(manifestPath));
            Assert.Equal(Samples.Length, artifacts.Count);
        }
        finally
        {
            window.Close();
        }
    }

    private static string ResolveScreenshotRoot()
    {
        var configured = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "artifacts",
                "headless-screenshots"));
    }

    private static void AssertSampleDiagnostics(string sampleKey, LayoutDiagnosticsSnapshot snapshot)
    {
        Assert.True(snapshot.SolveAccessCount > 0 || snapshot.SolveCount > 0, $"Expected layout activity for '{sampleKey}'.");

        if (sampleKey is "wrap" or "masonry")
        {
            Assert.True(snapshot.LastRealizedElementCount > 0, $"Expected realized elements for '{sampleKey}'.");
            Assert.True(snapshot.RealizationUpdateCount > 0, $"Expected realization updates for '{sampleKey}'.");
            Assert.True(snapshot.SolveCacheHitRate > 0, $"Expected geometry solve cache hits for '{sampleKey}'.");
        }
    }

    private static LayoutDiagnosticsSnapshot ExerciseVirtualizedSurface(Window window, Control samplePage, DiagnosticsProbe probe)
    {
        var visibleScrollViewers = samplePage
            .GetVisualDescendants()
            .OfType<ScrollViewer>()
            .Where(scrollViewer => scrollViewer.IsVisible)
            .ToArray();

        Assert.Single(visibleScrollViewers);

        var scrollViewer = visibleScrollViewers[0];
        var initialSnapshot = probe.SnapshotAccessor();
        var initialViewportUpdates = initialSnapshot.ViewportUpdateCount;

        scrollViewer.Offset = new Vector(0, 900);
        _ = window.CaptureRenderedFrame();

        var updatedSnapshot = probe.SnapshotAccessor();
        Assert.True(
            updatedSnapshot.ViewportUpdateCount > initialViewportUpdates,
            "Expected a viewport update after scrolling the virtualized sample surface.");

        return updatedSnapshot;
    }

    private sealed record ScreenshotArtifact(
        string absolute_path,
        string relative_path,
        int width,
        int height,
        long bytes,
        DateTime modified_utc);
}
