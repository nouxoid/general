using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.DataTransfer;
using System.Text.Json;
using System.IO;
using System.Linq;

namespace general.Views
{
    public class ClipboardItem
    {
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsFavorite { get; set; }
        public override string ToString() => Content;
    }

    public sealed partial class MainPage : Page
    {
        private const int MaxHistoryCount = 100;
        private static readonly TimeSpan MaxHistoryAge = TimeSpan.FromDays(7);
        private static readonly string HistoryFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardManagerHistory.json");

        private ObservableCollection<ClipboardItem> _history = new();
        private ClipboardItem? _selectedItem = null;

        public MainPage()
        {
            this.InitializeComponent();
            ClipboardListView.ItemsSource = _history;
            LoadHistory();
            Clipboard.ContentChanged += Clipboard_ContentChanged;
            BuildTopBar();
            BuildActionBar();
        }

        private void BuildTopBar()
        {
            TopBar.Children.Clear();
            var clearIcon = new FontIcon { Glyph = "\uE74D", FontSize = 20 };
            var clearButton = new Button
            {
                Content = clearIcon,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 4, 0)
            };
            ToolTipService.SetToolTip(clearButton, "Clear History");
            clearButton.Click += (s, e) => { _history.Clear(); SaveHistory(); };

            var settingsButton = new Button
            {
                Content = new FontIcon { Glyph = "\uE713", FontSize = 20 },
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 4, 0)
            };
            ToolTipService.SetToolTip(settingsButton, "Settings");

            var infoButton = new Button
            {
                Content = new FontIcon { Glyph = "\uE946", FontSize = 20 },
                CornerRadius = new CornerRadius(6)
            };
            ToolTipService.SetToolTip(infoButton, "About");

            TopBar.Children.Add(clearButton);
            TopBar.Children.Add(settingsButton);
            TopBar.Children.Add(infoButton);
        }

        private void BuildActionBar()
        {
            ActionBar.Children.Clear();
            var favBtn = new Button { Content = new FontIcon { Glyph = "\uE734", FontSize = 18 }, CornerRadius = new CornerRadius(6) };
            ToolTipService.SetToolTip(favBtn, "Favorite");
            favBtn.Click += (s, e) => { if (_selectedItem != null) { _selectedItem.IsFavorite = !_selectedItem.IsFavorite; SaveHistory(); } };
            var pinBtn = new Button { Content = new FontIcon { Glyph = "\uE718", FontSize = 18 }, CornerRadius = new CornerRadius(6) };
            ToolTipService.SetToolTip(pinBtn, "Pin");
            // Pin logic placeholder
            var delBtn = new Button { Content = new FontIcon { Glyph = "\uE74D", FontSize = 18 }, CornerRadius = new CornerRadius(6) };
            ToolTipService.SetToolTip(delBtn, "Delete");
            delBtn.Click += (s, e) => { if (_selectedItem != null) { _history.Remove(_selectedItem); _selectedItem = null; ActionBar.Visibility = Visibility.Collapsed; SaveHistory(); } };
            var editBtn = new Button { Content = new FontIcon { Glyph = "\uE70F", FontSize = 18 }, CornerRadius = new CornerRadius(6) };
            ToolTipService.SetToolTip(editBtn, "Edit");
            // Edit logic placeholder
            ActionBar.Children.Add(favBtn);
            ActionBar.Children.Add(pinBtn);
            ActionBar.Children.Add(delBtn);
            ActionBar.Children.Add(editBtn);
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text;
            if (string.IsNullOrWhiteSpace(query))
            {
                ClipboardListView.ItemsSource = _history;
            }
            else
            {
                ClipboardListView.ItemsSource = new ObservableCollection<ClipboardItem>(_history.Where(item => item.Content.Contains(query, StringComparison.OrdinalIgnoreCase)));
            }
        }

        private void OnListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedItem = ClipboardListView.SelectedItem as ClipboardItem;
            ActionBar.Visibility = _selectedItem != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnCopyClicked(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is ClipboardItem item)
            {
                var data = new DataPackage();
                data.SetText(item.Content);
                Clipboard.SetContent(data);
            }
        }

        private void OnPasteClicked(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is ClipboardItem item)
            {
                var data = new DataPackage();
                data.SetText(item.Content);
                Clipboard.SetContent(data);
            }
        }

        private void PruneHistory()
        {
            var cutoff = DateTime.Now - MaxHistoryAge;
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                if (_history[i].Timestamp < cutoff)
                    _history.RemoveAt(i);
            }
            while (_history.Count > MaxHistoryCount)
                _history.RemoveAt(_history.Count - 1);
        }

        private async void Clipboard_ContentChanged(object? sender, object e)
        {
            var data = Clipboard.GetContent();
            if (data.Contains(StandardDataFormats.Text))
            {
                var text = await data.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(text) && (_history.Count == 0 || _history[0].Content != text))
                {
                    _history.Insert(0, new ClipboardItem
                    {
                        Content = text,
                        Timestamp = DateTime.Now,
                        IsFavorite = false
                    });
                    PruneHistory();
                    SaveHistory();
                }
            }
        }

        private void SaveHistory()
        {
            try
            {
                var json = JsonSerializer.Serialize(_history.ToList());
                File.WriteAllText(HistoryFilePath, json);
            }
            catch { }
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(HistoryFilePath))
                {
                    var json = File.ReadAllText(HistoryFilePath);
                    var items = JsonSerializer.Deserialize<ClipboardItem[]>(json);
                    if (items != null)
                    {
                        foreach (var item in items)
                            _history.Add(item);
                        PruneHistory();
                    }
                }
            }
            catch { }
        }
    }
}
