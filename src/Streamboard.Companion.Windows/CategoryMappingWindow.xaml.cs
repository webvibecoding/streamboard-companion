using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Streamboard.Companion.Core.Api;
using Streamboard.Companion.Core.Detection;

namespace Streamboard.Companion.Windows;

public partial class CategoryMappingWindow : Window
{
    private readonly SyncGame _game;
    private readonly CategoryMappings _mappings;
    private readonly IExecutableMappingStore? _executableMappings;
    private readonly IReadOnlyList<PlatformSyncResult> _results;
    private readonly string? _fixedPlatform;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _busy;
    private bool _saving;

    internal CategoryMappingWindow(
        SyncGame game,
        CategoryMappings mappings,
        IExecutableMappingStore? executableMappings = null,
        IReadOnlyList<PlatformSyncResult>? results = null,
        string? fixedPlatform = null,
        CategorySearchItem? currentCategory = null)
    {
        InitializeComponent();
        _game = game;
        _mappings = mappings;
        _executableMappings = executableMappings;
        _results = results ?? [];
        _fixedPlatform = fixedPlatform;
        GameNameBox.Text = game.DisplayName;
        GameNameBox.IsReadOnly = fixedPlatform is not null;
        QueryBox.Text = currentCategory?.Name ?? game.DisplayName;
        if (fixedPlatform is not null)
        {
            PlatformBox.SelectedIndex = fixedPlatform == CategoryMappings.VkPlayLive ? 1 : 0;
            PlatformBox.IsEnabled = false;
        }
        else if (_results.Any(result => result.PlatformId == CategoryMappings.VkPlayLive && result.Status == "needs_mapping")
            && !_results.Any(result => result.PlatformId == CategoryMappings.Twitch && result.Status == "needs_mapping"))
        {
            PlatformBox.SelectedIndex = 1;
        }

        ResultsList.SelectionChanged += (_, _) =>
            SaveButton.IsEnabled = !_busy && ResultsList.SelectedItem is not null;
        PlatformBox.SelectionChanged += (_, _) => ShowCandidates();
        ShowCandidates();
        if (currentCategory is not null)
        {
            FeedbackText.Text = $"Текущая категория: {currentCategory.Name}";
        }
    }

    internal DesktopMapping? SavedMapping { get; private set; }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        var query = QueryBox.Text.Trim();
        if (query.Length < 2)
        {
            FeedbackText.Text = "Введите минимум два символа.";
            return;
        }

        var cancellationToken = _lifetime.Token;
        SetBusy(true, "Ищем категории…");
        try
        {
            var result = await _mappings.SearchAsync(SelectedPlatform(), query, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ResultsList.ItemsSource = result.Items;
            FeedbackText.Text = result.Items.Count == 0
                ? "Категории не найдены."
                : $"Найдено: {result.Items.Count}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            FeedbackText.Text = CategoryMappingErrors.Message(exception, "Не удалось выполнить поиск.");
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                SetBusy(false, FeedbackText.Text);
            }
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is not CategorySearchItem category)
        {
            return;
        }

        var displayName = GameNameBox.Text.Trim();
        if (displayName.Length == 0)
        {
            FeedbackText.Text = "Укажите название игры.";
            return;
        }

        var cancellationToken = _lifetime.Token;
        _saving = true;
        SetBusy(true, "Сохраняем…");
        try
        {
            SavedMapping = await _mappings.SaveAsync(
                _game with { DisplayName = displayName, ExecutableName = Path.GetFileName(_game.ExecutableName) },
                SelectedPlatform(), category, cancellationToken);
            if (_game.Source == "exe" && _executableMappings is not null)
            {
                await _executableMappings.SetAsync(
                    new ExecutableGameMapping(_game.Key, displayName), cancellationToken);
            }

            _saving = false;
            if (!cancellationToken.IsCancellationRequested)
            {
                DialogResult = true;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _saving = false;
            if (!cancellationToken.IsCancellationRequested)
            {
                SetBusy(false, CategoryMappingErrors.Message(exception, "Не удалось сохранить сопоставление."));
            }
        }
    }

    private string SelectedPlatform() =>
        _fixedPlatform ?? (PlatformBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? CategoryMappings.Twitch;

    private void ShowCandidates()
    {
        ResultsList.SelectedItem = null;
        ResultsList.ItemsSource = _results.FirstOrDefault(result => result.PlatformId == SelectedPlatform())
            ?.Candidates ?? [];
        FeedbackText.Text = "";
    }

    private void SetBusy(bool busy, string message)
    {
        _busy = busy;
        PlatformBox.IsEnabled = !busy && _fixedPlatform is null;
        ResultsList.IsEnabled = !busy;
        SearchButton.IsEnabled = !busy;
        SaveButton.IsEnabled = !busy && ResultsList.SelectedItem is not null;
        CancelButton.IsEnabled = !_saving;
        FeedbackText.Text = message;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = _saving;
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        base.OnClosed(e);
    }
}
