using System.Windows;
using System.Windows.Controls;
using Streamboard.Companion.Core.Api;
using Streamboard.Companion.Core.Auth;
using Streamboard.Companion.Core.Detection;

namespace Streamboard.Companion.Windows;

public partial class CategoryMappingWindow : Window
{
    private readonly ResolvedGame _game;
    private readonly IStreamboardApiClient _api;
    private readonly DeviceTokenManager _tokens;
    private readonly IExecutableMappingStore _executableMappings;

    public CategoryMappingWindow(
        ResolvedGame game,
        IStreamboardApiClient api,
        DeviceTokenManager tokens,
        IExecutableMappingStore executableMappings)
    {
        InitializeComponent();
        _game = game;
        _api = api;
        _tokens = tokens;
        _executableMappings = executableMappings;
        GameNameBox.Text = game.DisplayName;
        QueryBox.Text = game.DisplayName;
        ResultsList.SelectionChanged += (_, _) => SaveButton.IsEnabled = ResultsList.SelectedItem is not null;
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        var query = QueryBox.Text.Trim();
        if (query.Length < 2)
        {
            FeedbackText.Text = "Введите минимум два символа.";
            return;
        }

        SetBusy(true, "Ищем категории…");
        try
        {
            var token = await _tokens.GetAccessTokenAsync();
            var result = await _api.SearchCategoriesAsync(token, SelectedPlatform(), query);
            ResultsList.ItemsSource = result.Items;
            FeedbackText.Text = result.Items.Count == 0
                ? "Категории не найдены."
                : $"Найдено: {result.Items.Count}";
        }
        catch (Exception exception)
        {
            FeedbackText.Text = exception is CompanionApiException apiException
                ? $"Ошибка: {apiException.Reason}"
                : "Не удалось выполнить поиск.";
        }
        finally
        {
            SetBusy(false, FeedbackText.Text);
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

        SetBusy(true, "Сохраняем…");
        try
        {
            if (_game.Source == "exe")
            {
                await _executableMappings.SetAsync(new ExecutableGameMapping(
                    _game.Key,
                    displayName));
            }

            var token = await _tokens.GetAccessTokenAsync();
            await _api.PutMappingAsync(
                token,
                new DesktopMapping(
                    new SyncGame(
                        _game.Key,
                        _game.Source == "exe" ? "exe" : _game.Source,
                        displayName,
                        Path.GetFileName(_game.ExecutableName)),
                    SelectedPlatform(),
                    category));
            DialogResult = true;
            Close();
        }
        catch (Exception exception)
        {
            FeedbackText.Text = exception is CompanionApiException apiException
                ? $"Ошибка: {apiException.Reason}"
                : "Не удалось сохранить сопоставление.";
            SetBusy(false, FeedbackText.Text);
        }
    }

    private string SelectedPlatform() =>
        (PlatformBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "twitch";

    private void SetBusy(bool busy, string message)
    {
        SearchButton.IsEnabled = !busy;
        SaveButton.IsEnabled = !busy && ResultsList.SelectedItem is not null;
        FeedbackText.Text = message;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
