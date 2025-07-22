using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.DataTransfer;
using System.Text.Json;
using System.IO;
using System.Linq;

namespace general.Views
{
    // Converter for BoolToVisibility
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool boolValue && boolValue) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return (value is Visibility visibility) && visibility == Visibility.Visible;
        }
    }

    // Converter for favorite button background
    public class FavoriteButtonBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool isFavorite && isFavorite) 
                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 185, 0)) // Gold background
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    // Converter for favorite button foreground
    public class FavoriteButtonForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool isFavorite && isFavorite) 
                ? new SolidColorBrush(Microsoft.UI.Colors.White)
                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 102, 102, 102)); // Gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ClipboardItem
    {
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsFavorite { get; set; }
        
        // Enhanced properties for better UI display
        public string DisplayContent => Content.Length > 80 ? Content.Substring(0, 80) + "..." : Content;
        public string TimeAgo
        {
            get
            {
                var timeSpan = DateTime.Now - Timestamp;
                return timeSpan.TotalMinutes < 1 ? "Just now" :
                       timeSpan.TotalMinutes < 60 ? $"{(int)timeSpan.TotalMinutes}m ago" :
                       timeSpan.TotalHours < 24 ? $"{(int)timeSpan.TotalHours}h ago" :
                       timeSpan.TotalDays < 7 ? $"{(int)timeSpan.TotalDays}d ago" :
                       Timestamp.ToString("MMM dd");
            }
        }
        public string ContentType
        {
            get
            {
                if (Content.StartsWith("http://") || Content.StartsWith("https://"))
                    return "URL";
                if (Content.Contains("@") && Content.Contains(".") && Content.Count(c => c == '@') == 1)
                    return "Email";
                if (Content.All(char.IsDigit) && Content.Length > 5)
                    return "Number";
                return "Text";
            }
        }
        public string ContentIcon
        {
            get
            {
                return ContentType switch
                {
                    "URL" => "\uE71B",     // Globe icon
                    "Email" => "\uE715",   // Mail icon
                    "Number" => "\uE7C3",  // Calculator icon
                    _ => "\uE8C8"          // Text icon
                };
            }
        }
        
        public override string ToString() => Content;
    }

    public sealed partial class MainPage : Page
    {
        private const int MaxHistoryCount = 100;
        private static readonly TimeSpan MaxHistoryAge = TimeSpan.FromDays(7);
        private static readonly string HistoryFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardManagerHistory.json");

        private ObservableCollection<ClipboardItem> _history = new();
        private ObservableCollection<ClipboardItem> _filteredHistory = new();
        private ClipboardItem? _selectedItem = null;
        private bool _showingFavoritesOnly = false;

        public MainPage()
        {
            this.InitializeComponent();
            ClipboardListView.ItemsSource = _filteredHistory;
            LoadHistory();
            Clipboard.ContentChanged += Clipboard_ContentChanged;
            BuildTopBar();
            UpdateView();
        }

        private void BuildTopBar()
        {
            TopBar.Children.Clear();
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // App title - minimal
            var titleText = new TextBlock
            {
                Text = "Clipboard",
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 51, 51, 51)),
                Margin = new Thickness(0, 0, 16, 0)
            };
            
            // Status indicator - minimal
            var statusText = new TextBlock
            {
                Text = GetStatusText(),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 153, 153, 153)),
                Margin = new Thickness(0, 0, 16, 0)
            };

            // Spacer
            var spacer = new Border { HorizontalAlignment = HorizontalAlignment.Stretch };
            
            // Minimal action buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            
            var settingsButton = new Button
            {
                Content = new FontIcon { Glyph = "\uE713", FontSize = 16 },
                Width = 32,
                Height = 32,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4)
            };
            ToolTipService.SetToolTip(settingsButton, "Settings");
            settingsButton.Click += OnSettingsClicked;

            var clearButton = new Button
            {
                Content = new FontIcon { Glyph = "\uE74D", FontSize = 16 },
                Width = 32,
                Height = 32,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4)
            };
            ToolTipService.SetToolTip(clearButton, "Clear All");
            clearButton.Click += (s, e) => { 
                _history.Clear(); 
                SaveHistory(); 
                UpdateView(); 
            };

            buttonPanel.Children.Add(settingsButton);
            buttonPanel.Children.Add(clearButton);

            Grid.SetColumn(titleText, 0);
            Grid.SetColumn(statusText, 1);
            Grid.SetColumn(spacer, 2);
            Grid.SetColumn(buttonPanel, 3);

            grid.Children.Add(titleText);
            grid.Children.Add(statusText);
            grid.Children.Add(spacer);
            grid.Children.Add(buttonPanel);
            
            TopBar.Children.Add(grid);
        }

        private string GetStatusText()
        {
            var totalItems = _history.Count;
            var favoriteItems = _history.Count(item => item.IsFavorite);
            
            if (_showingFavoritesOnly && favoriteItems > 0)
                return $"{favoriteItems} favorites";
            else if (totalItems == 0)
                return "Empty";
            else if (favoriteItems > 0)
                return $"{totalItems} items · {favoriteItems} favorites";
            else
                return $"{totalItems} items";
        }

        private void UpdateView()
        {
            // Update filtered view
            _filteredHistory.Clear();
            var itemsToShow = _showingFavoritesOnly 
                ? _history.Where(item => item.IsFavorite) 
                : _history;
            
            var query = SearchBox?.Text ?? "";
            if (!string.IsNullOrWhiteSpace(query))
            {
                itemsToShow = itemsToShow.Where(item => item.Content.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in itemsToShow)
            {
                _filteredHistory.Add(item);
            }

            // Update UI state
            var hasItems = _filteredHistory.Count > 0;
            EmptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            ClipboardListView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
            
            // Update status
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            if (TopBar.Children.FirstOrDefault() is Grid grid && 
                grid.Children.Count > 1 && 
                grid.Children[1] is TextBlock statusText)
            {
                statusText.Text = GetStatusText();
            }
        }

        private void OnSettingsClicked(object sender, RoutedEventArgs e)
        {
            ((Popup)this.FindName("SettingsPopup")).IsOpen = !((Popup)this.FindName("SettingsPopup")).IsOpen;
        }

        private void OnShowFavoritesClicked(object sender, RoutedEventArgs e)
        {
            _showingFavoritesOnly = !_showingFavoritesOnly;
            var button = sender as Button;
            if (button != null)
            {
                button.Content = _showingFavoritesOnly ? "Show all items" : "Show only favorites";
            }
            UpdateView();
            ((Popup)this.FindName("SettingsPopup")).IsOpen = false;
        }

        private void OnClearAllClicked(object sender, RoutedEventArgs e)
        {
            _history.Clear();
            SaveHistory();
            UpdateView();
            ((Popup)this.FindName("SettingsPopup")).IsOpen = false;
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateView();
        }

        private void OnListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedItem = ClipboardListView.SelectedItem as ClipboardItem;
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

        private void OnToggleFavoriteClicked(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is ClipboardItem item)
            {
                item.IsFavorite = !item.IsFavorite;
                SaveHistory();
                UpdateView(); // Refresh to update visual state
            }
        }

        private void OnDeleteClicked(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is ClipboardItem item)
            {
                _history.Remove(item);
                SaveHistory();
                UpdateView();
            }
        }

        private void PruneHistory()
        {
            var cutoff = DateTime.Now - MaxHistoryAge;
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                if (_history[i].Timestamp < cutoff && !_history[i].IsFavorite) // Keep favorites even if old
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
                    UpdateView();
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
