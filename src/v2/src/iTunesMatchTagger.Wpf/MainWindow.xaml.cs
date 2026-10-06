using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iTunesMatchTagger.Core.Sources;
using iTunesMatchTagger.Wpf.ViewModels;

namespace iTunesMatchTagger.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _updatingPickers;
    private TrackRowViewModel? _watchedRow;

    /// <summary>The four source comparison columns of the detail grid (in DataGrid.Columns order).</summary>
    private static readonly (string SourceId, string Label)[] SourceColumns =
    [
        (TagSources.ITunes, "Apple"),
        (TagSources.MusicBrainz, "MusicBrainz"),
        (TagSources.Discogs, "Discogs"),
        (TagSources.Deezer, "Deezer"),
    ];

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
            WatchRow(_viewModel.SelectedRow);
            RefreshDetail();
            _ = _viewModel.RefreshLyricsPreviewAsync();
        }
    }

    /// <summary>
    /// The selected row keeps changing after selection (lookup outcomes,
    /// candidate picks, update writes) - keep the pickers and the grid in
    /// sync with it.
    /// </summary>
    private void WatchRow(TrackRowViewModel? row)
    {
        if (ReferenceEquals(_watchedRow, row))
        {
            return;
        }

        if (_watchedRow is not null)
        {
            _watchedRow.PropertyChanged -= OnWatchedRowChanged;
        }

        _watchedRow = row;
        if (_watchedRow is not null)
        {
            _watchedRow.PropertyChanged += OnWatchedRowChanged;
        }
    }

    private void OnWatchedRowChanged(object? sender, PropertyChangedEventArgs e) => RefreshDetail();

    private async void RefreshLyrics_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.RefreshLyricsPreviewAsync();

    private void RefreshDetail()
    {
        if (_updatingPickers)
        {
            return;
        }

        var row = _viewModel.SelectedRow;

        _updatingPickers = true;
        RebuildWriteFromPicker(row);
        RebuildSourceHeaders(row);
        _updatingPickers = false;
    }

    // ------------------------------------------------------------------
    // "Write from" picker: which source's picked candidate gets written
    // ------------------------------------------------------------------

    private void RebuildWriteFromPicker(TrackRowViewModel? row)
    {
        WriteFromPicker.Items.Clear();
        if (row is null)
        {
            return;
        }

        foreach (var (sourceId, label) in SourceColumns)
        {
            if (row.CandidatesForSource(sourceId).Count > 0)
            {
                _ = WriteFromPicker.Items.Add(new ComboBoxItem { Content = label, Tag = sourceId });
            }
        }

        var index = 0;
        foreach (ComboBoxItem item in WriteFromPicker.Items)
        {
            if ((string?)item.Tag == row.ActiveSourceId)
            {
                WriteFromPicker.SelectedIndex = index;
                return;
            }

            index++;
        }
    }

    private void WriteFromPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPickers)
        {
            return;
        }

        if (WriteFromPicker.SelectedItem is ComboBoxItem { Tag: { } tag } && _viewModel.SelectedRow is { } row)
        {
            row.SetActiveSource((string)tag);
        }
    }

    // ------------------------------------------------------------------
    // Per-source candidate pickers in the column headers
    // ------------------------------------------------------------------

    private void RebuildSourceHeaders(TrackRowViewModel? row)
    {
        for (var i = 0; i < SourceColumns.Length; i++)
        {
            var (sourceId, label) = SourceColumns[i];
            var column = DetailGrid.Columns[3 + i]; // after Use | Field | Current

            var candidates = row?.CandidatesForSource(sourceId) ?? [];
            if (row is null || candidates.Count == 0)
            {
                column.Header = label;
                continue;
            }

            var picker = new ComboBox
            {
                Margin = new Thickness(2, 2, 4, 2),
                DisplayMemberPath = nameof(SourceCandidateOption.DisplayName),
                Tag = sourceId,
            };
            picker.SetBinding(
                FrameworkElement.ToolTipProperty,
                new System.Windows.Data.Binding("SelectedItem.DisplayName")
                {
                    RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.Self),
                });

            var indexInSource = -1;
            for (var c = 0; c < candidates.Count; c++)
            {
                _ = picker.Items.Add(new SourceCandidateOption(c, candidates[c]));
                if (ReferenceEquals(candidates[c], row.CandidateForSource(sourceId)))
                {
                    indexInSource = c;
                }
            }

            picker.SelectedIndex = indexInSource;
            picker.SelectionChanged += SourceHeaderPicker_SelectionChanged;

            var header = new StackPanel();
            header.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = (Brush)FindResource("Subtle"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(2, 2, 0, 0),
            });
            header.Children.Add(picker);
            column.Header = header;
        }
    }

    private void SourceHeaderPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPickers || sender is not ComboBox { Tag: { } tag } picker)
        {
            return;
        }

        if (picker.SelectedIndex >= 0 && _viewModel.SelectedRow is { } row)
        {
            row.SelectCandidateForSource((string)tag, picker.SelectedIndex);
        }
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
