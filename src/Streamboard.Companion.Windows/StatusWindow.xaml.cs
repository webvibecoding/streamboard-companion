using System.ComponentModel;
using System.Windows;
using Streamboard.Companion.Core.Api;
using Streamboard.Companion.Core.Detection;
using Streamboard.Companion.Core.Tracking;

namespace Streamboard.Companion.Windows;

public partial class StatusWindow : Window
{
    private readonly TrackingController _tracking;
    private readonly Action _openMapping;
    private readonly Action _openSavedMappings;
    private bool _allowClose;
    private string? _resultsGameKey;
    private IReadOnlyList<PlatformSyncResult> _results = [];

    public StatusWindow(TrackingController tracking, Action openMapping, Action openSavedMappings)
    {
        InitializeComponent();
        _tracking = tracking;
        _openMapping = openMapping;
        _openSavedMappings = openSavedMappings;
    }

    public ResolvedGame? CurrentGame { get; private set; }

    public void UpdateStatus(CompanionStatus status)
    {
        CurrentGame = status.Game ?? CurrentGame;
        StateText.Text = status.Message;
        GameText.Text = status.Game?.DisplayName ?? "Не обнаружена";
        MappingButton.IsEnabled = status.Game is not null;
        PauseButton.Content = status.State == CompanionRunState.Paused ? "Возобновить" : "Приостановить";

        if (status.Results is not null)
        {
            _resultsGameKey = status.Game?.Key;
            _results = status.Results;
            TwitchStatusText.Text = FormatResult(status.Results.FirstOrDefault(
                static result => result.PlatformId == "twitch"));
            VkStatusText.Text = FormatResult(status.Results.FirstOrDefault(
                static result => result.PlatformId == "vkplaylive"));
        }
    }

    internal IReadOnlyList<PlatformSyncResult> GetResultsFor(string gameKey) =>
        gameKey == _resultsGameKey ? _results : [];

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        _tracking.SetPaused(!_tracking.IsPaused);
        PauseButton.Content = _tracking.IsPaused ? "Возобновить" : "Приостановить";
    }

    private void MappingButton_Click(object sender, RoutedEventArgs e) => _openMapping();

    private void SavedMappingsButton_Click(object sender, RoutedEventArgs e) => _openSavedMappings();

    private void HideButton_Click(object sender, RoutedEventArgs e) => Hide();

    private static string FormatResult(PlatformSyncResult? result) => result?.Status switch
    {
        "updated" => $"Категория изменена: {result.Category?.Name ?? "готово"}",
        "unchanged" => $"Уже установлена: {result.Category?.Name ?? "без изменений"}",
        "offline" => "Эфир не запущен",
        "not_connected" => "Платформа не подключена",
        "needs_mapping" => "Нужно выбрать категорию",
        "error" => $"Ошибка: {result.Error ?? "неизвестная"}",
        "unsupported" => "Не поддерживается в этой версии",
        _ => "Ожидание проверки",
    };
}
