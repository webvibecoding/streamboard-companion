using Streamboard.Companion.Core.Api;
using Streamboard.Companion.Core.Detection;

namespace Streamboard.Companion.Core.Tracking;

public sealed record GameTrackingOptions(
    TimeSpan SampleInterval,
    TimeSpan DebounceInterval,
    TimeSpan ResyncInterval,
    TimeSpan FailureRetryInterval)
{
    public static GameTrackingOptions Default { get; } = new(
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(5));
}

public interface IGameEventPublisher
{
    Task<SyncResponse> PublishAsync(
        ResolvedGame game,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default);
}

public sealed class GameTrackingCoordinator
{
    private readonly IForegroundProcessProvider _foreground;
    private readonly IGameResolver _resolver;
    private readonly IGameEventPublisher _publisher;
    private readonly ICompanionStatusSink _status;
    private readonly TrackingController _control;
    private readonly TimeProvider _timeProvider;
    private readonly GameTrackingOptions _options;
    private readonly GameDetectionStateMachine _stateMachine;
    private readonly HashSet<string> _notifiedMappings = new(StringComparer.Ordinal);
    private bool _wasPaused;
    private int _lastResumeRevision;
    private CompanionStatus? _lastStatus;

    public GameTrackingCoordinator(
        IForegroundProcessProvider foreground,
        IGameResolver resolver,
        IGameEventPublisher publisher,
        ICompanionStatusSink status,
        TrackingController control,
        GameTrackingOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        _foreground = foreground;
        _resolver = resolver;
        _publisher = publisher;
        _status = status;
        _control = control;
        _options = options ?? GameTrackingOptions.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _stateMachine = new GameDetectionStateMachine(
            _options.DebounceInterval,
            _options.ResyncInterval);
        _lastResumeRevision = _control.ResumeRevision;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await ReportAsync(
            new CompanionStatus(CompanionRunState.Starting, "Запуск отслеживания…"),
            cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _stateMachine.MarkDeliveryFailed(_timeProvider.GetUtcNow(), _options.FailureRetryInterval);
                await ReportAsync(
                    new CompanionStatus(
                        CompanionRunState.Error,
                        $"Ошибка: {SafeErrorMessage(exception)}"),
                    cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(_options.SampleInterval, _timeProvider, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        var resumeRevision = _control.ResumeRevision;
        if (resumeRevision != _lastResumeRevision)
        {
            _lastResumeRevision = resumeRevision;
            _stateMachine.Reset();
        }

        if (_control.IsPaused)
        {
            if (!_wasPaused)
            {
                _stateMachine.Reset();
                _wasPaused = true;
            }

            await ReportAsync(
                new CompanionStatus(CompanionRunState.Paused, "Отслеживание приостановлено"),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (_wasPaused)
        {
            _stateMachine.Reset();
            _wasPaused = false;
        }

        var now = _timeProvider.GetUtcNow();
        var process = await _foreground.GetForegroundProcessAsync(cancellationToken).ConfigureAwait(false);
        var game = process is null
            ? null
            : await _resolver.ResolveAsync(process, cancellationToken).ConfigureAwait(false);

        if (game?.RequiresMapping == true)
        {
            if (_notifiedMappings.Add(game.Key))
            {
                await _status.MappingRequiredAsync(game, cancellationToken).ConfigureAwait(false);
            }

            _stateMachine.Observe(null, now);
            await ReportAsync(
                new CompanionStatus(
                    CompanionRunState.Searching,
                    $"Требуется сопоставить игру: {game.DisplayName}",
                    game),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var action = _stateMachine.Observe(game, now);
        if (action is null)
        {
            await ReportAsync(
                game is null
                    ? new CompanionStatus(CompanionRunState.Searching, "Игра не обнаружена")
                    : new CompanionStatus(
                        CompanionRunState.Debouncing,
                        $"Проверяю игру: {game.DisplayName}",
                        game),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            var response = await _publisher.PublishAsync(action.Game, now, cancellationToken)
                .ConfigureAwait(false);
            if (response.Results.Any(static result => result.Status == "needs_mapping")
                && _notifiedMappings.Add($"server:{action.Game.Key}"))
            {
                await _status.MappingRequiredAsync(action.Game, cancellationToken)
                    .ConfigureAwait(false);
            }
            var suffix = response.Results.Count == 0
                ? string.Empty
                : $" · обновлено: {response.Results.Count(static result => result.Status == "updated")}";
            await ReportAsync(
                new CompanionStatus(
                    CompanionRunState.Tracking,
                    $"Игра: {action.Game.DisplayName}{suffix}",
                    action.Game,
                    response.Results),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _stateMachine.MarkDeliveryFailed(now, _options.FailureRetryInterval);
            throw;
        }
    }

    private async ValueTask ReportAsync(
        CompanionStatus status,
        CancellationToken cancellationToken)
    {
        if (_lastStatus == status)
        {
            return;
        }

        _lastStatus = status;
        await _status.UpdateStatusAsync(status, cancellationToken).ConfigureAwait(false);
    }

    private static string SafeErrorMessage(Exception exception) => exception switch
    {
        CompanionApiException apiException => apiException.Reason,
        _ => exception.GetType().Name,
    };
}

