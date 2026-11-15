using System.Collections.ObjectModel;
using System.Windows;
using Streamboard.Companion.Core.Api;

namespace Streamboard.Companion.Windows;

public partial class SavedMappingsWindow : Window
{
    private readonly CategoryMappings _mappings;
    private readonly ObservableCollection<CategoryMappingRow> _rows = [];
    private readonly CancellationTokenSource _lifetime = new();

    internal SavedMappingsWindow(CategoryMappings mappings)
    {
        InitializeComponent();
        _mappings = mappings;
        MappingsGrid.ItemsSource = _rows;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        _ = RefreshAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        base.OnClosed(e);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (!RefreshButton.IsEnabled)
        {
            return;
        }

        var cancellationToken = _lifetime.Token;
        RefreshButton.IsEnabled = false;
        MappingsGrid.IsEnabled = false;
        FeedbackText.Text = "Загружаем сопоставления…";
        try
        {
            var rows = await _mappings.LoadAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _rows.Clear();
            foreach (var row in rows)
            {
                _rows.Add(row);
            }

            FeedbackText.Text = rows.Count == 0 ? "Сохранённых сопоставлений пока нет." : "";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (CategoryMappingErrors.ConnectionRequired(exception))
            {
                _rows.Clear();
            }

            FeedbackText.Text = CategoryMappingErrors.Message(exception, "Не удалось загрузить сопоставления.");
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                RefreshButton.IsEnabled = true;
                MappingsGrid.IsEnabled = true;
            }
        }
    }

    private void TwitchCategory_Click(object sender, RoutedEventArgs e) => EditCategory(sender, CategoryMappings.Twitch);

    private void VkCategory_Click(object sender, RoutedEventArgs e) => EditCategory(sender, CategoryMappings.VkPlayLive);

    private void EditCategory(object sender, string platformId)
    {
        if (sender is not FrameworkElement { DataContext: CategoryMappingRow row })
        {
            return;
        }

        var current = platformId == CategoryMappings.Twitch ? row.Twitch : row.VkPlayLive;
        var editor = new CategoryMappingWindow(
            row.Game, _mappings, fixedPlatform: platformId, currentCategory: current?.Category) { Owner = this };
        if (editor.ShowDialog() == true && editor.SavedMapping is { } saved)
        {
            var index = _rows.IndexOf(row);
            if (index >= 0)
            {
                _rows[index] = platformId == CategoryMappings.Twitch
                    ? row with { Twitch = saved }
                    : row with { VkPlayLive = saved };
                FeedbackText.Text = "Сопоставление сохранено.";
            }
        }
    }
}
