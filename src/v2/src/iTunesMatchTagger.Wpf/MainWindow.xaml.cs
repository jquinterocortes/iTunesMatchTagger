using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using iTunesMatchTagger.Core.Sources;
using iTunesMatchTagger.Wpf.ViewModels;

namespace iTunesMatchTagger.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _updatingPicker;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ((System.Collections.Specialized.INotifyCollectionChanged)_viewModel.LogLines).CollectionChanged += (_, _) =>
        {
            if (LogList.Items.Count > 0)
            {
                LogList.ScrollIntoView(LogList.Items[^1]);
            }
        };
    }

    // ------------------------------------------------------------------
    // Selection -> detail panel
    // ------------------------------------------------------------------

    private void TrackList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _viewModel.SelectedRow = TrackList.SelectedItem as TrackRowViewModel;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedRow))
        {
            RefreshDetail();
        }
    }

    private void RefreshDetail()
    {
        var row = _viewModel.SelectedRow;

        _updatingPicker = true;
        CandidatePicker.ItemsSource = row?.CandidateOptions;
        CandidatePicker.SelectedIndex = row?.ActiveCandidateIndex ?? -1;
        _updatingPicker = false;

        RebuildArtworkStrip(row);
    }

    private void CandidatePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPicker || _viewModel.SelectedRow is not { } row)
        {
            return;
        }

        var index = CandidatePicker.SelectedIndex;
        if (index >= 0)
        {
            row.SelectCandidate(index);
            RebuildArtworkStrip(row);
        }
    }

    // ------------------------------------------------------------------
    // Artwork strip (Current + one preview per source)
    // ------------------------------------------------------------------

    public sealed record ArtworkStripItem(string Label, System.Windows.Media.ImageSource? Image);

    private static readonly (string SourceId, string Label)[] StripSources =
    [
        (TagSources.ITunes, "Apple"),
        (TagSources.MusicBrainz, "MusicBrainz"),
        (TagSources.Discogs, "Discogs"),
        (TagSources.Deezer, "Deezer"),
    ];

    private void RebuildArtworkStrip(TrackRowViewModel? row)
    {
        var items = new List<ArtworkStripItem> { new("Current", Imaging.FromBytes(row?.CurrentArtwork)) };
        if (row is not null)
        {
            foreach (var (sourceId, label) in StripSources)
            {
                var index = row.CandidateIndexForSource(sourceId);
                items.Add(new ArtworkStripItem(label, Imaging.FromBytes(row.GetCandidateArtwork(index))));
            }
        }

        ArtworkStrip.ItemsSource = items;
    }

    // ------------------------------------------------------------------
    // Log drawer + filter
    // ------------------------------------------------------------------

    private void ToggleLog_Click(object sender, RoutedEventArgs e)
    {
        var open = LogDrawer.Visibility != Visibility.Visible;
        LogDrawer.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        ToggleLogButton.Content = open ? "\u25BE Log" : "\u25B8 Log";
        if (open && LogList.Items.Count > 0)
        {
            LogList.ScrollIntoView(LogList.Items[^1]);
        }
    }

    private void FilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null || TrackList is null)
        {
            return; // fires during XAML parse (IsSelected on the first item)
        }

        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(TrackList.ItemsSource);
        if (view is null)
        {
            return;
        }

        view.Filter = FilterBox.SelectedIndex switch
        {
            1 => o => o is TrackRowViewModel { Track.TrackId: 0 }
                    && !IsError(o),
            2 => o => o is TrackRowViewModel { LookupSuccess: true },
            3 => o => !IsError(o) is false,
            _ => null,
        };
    }

    private static bool IsError(object row) =>
        row is TrackRowViewModel v && v.StatusSeverity == StatusKind.Error;
}
