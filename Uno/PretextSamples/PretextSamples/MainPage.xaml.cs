using Microsoft.UI.Xaml.Controls;
using PretextSamples.Samples;

namespace PretextSamples;

public sealed partial class MainPage : Page
{
    private readonly Dictionary<string, Func<FrameworkElement>> _sampleFactories = new(StringComparer.Ordinal)
    {
        ["overview"] = static () => new OverviewSampleView(),
        ["two-pass-panel"] = static () => new TwoPassLayoutSampleView(),
        ["two-pass-control"] = static () => new TwoPassControlSampleView(),
        ["shared-controls"] = static () => new SharedControlSamplesView(),
        ["shared-items"] = static () => new SharedItemSamplesView(),
        ["two-pass-repeater"] = static () => new TwoPassRepeaterSampleView(),
        ["accordion"] = static () => new AccordionSampleView(),
        ["bubbles"] = static () => new BubblesSampleView(),
        ["masonry"] = static () => new MasonrySampleView(),
        ["virtual-wrap"] = static () => new VirtualWrapTilesSampleView(),
        ["virtual-list"] = static () => new VirtualListTilesSampleView(),
        ["todo-app"] = static () => new TodoResponsiveSampleView(),
        ["mail-app"] = static () => new MailResponsiveSampleView(),
        ["notes-app"] = static () => new NotesResponsiveSampleView(),
        ["rich"] = static () => new RichNoteSampleView(),
        ["dynamic"] = static () => new DynamicLayoutSampleView(),
        ["editorial"] = static () => new EditorialEngineSampleView(),
        ["justification"] = static () => new JustificationComparisonSampleView(),
        ["ascii"] = static () => new VariableAsciiSampleView(),
    };

    private readonly Dictionary<string, FrameworkElement> _sampleCache = new(StringComparer.Ordinal);

    public MainPage()
    {
        this.InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (SampleNavigation.SelectedItem is null && SampleNavigation.MenuItems.FirstOrDefault() is NavigationViewItem first)
        {
            SampleNavigation.SelectedItem = first;
            ShowSample(first.Tag as string ?? "overview");
        }
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string tag)
        {
            ShowSample(tag);
            sender.Header = args.SelectedItemContainer.Content;
        }
    }

    private void ShowSample(string tag)
    {
        if (!_sampleFactories.TryGetValue(tag, out var factory))
        {
            return;
        }

        if (!_sampleCache.TryGetValue(tag, out var sample))
        {
            sample = factory();
            sample.HorizontalAlignment = HorizontalAlignment.Stretch;
            sample.VerticalAlignment = VerticalAlignment.Stretch;
            _sampleCache[tag] = sample;
        }

        ContentHost.Content = sample;
    }
}
