using System.Text;
using ShowMeTheBet.Models;
using ShowMeTheBet.Services;
using ShowMeTheBet.ViewModels.Base;
using Microsoft.Extensions.Logging;

namespace ShowMeTheBet.ViewModels.Game;

/// <summary>
/// 그래프 게임(View: Graph.razor)의 ViewModel
/// </summary>
public class GraphViewModel : GamePageViewModel
{
    private readonly GameService _gameService;
    private readonly ILogger<GraphViewModel>? _logger;

    private const decimal MinMultiplierValue = 1.0m;
    private const decimal MaxMultiplierValue = 5.0m;
    private const int GameDurationSeconds = 70;
    private const double FailureChancePerSecond = 0.05;
    private const double GraphWidth = 400.0;
    private const double GraphHeight = 300.0;

    private decimal _betAmount;
    private decimal _currentMultiplier = MinMultiplierValue;
    private bool _isGameRunning;
    private bool _gameFailed;
    private List<GameBet> _bets = new();

    private readonly List<(double x, double y)> _graphPoints = new();
    private readonly Random _random = new();
    private readonly object _timerLock = new();

    private System.Threading.Timer? _graphTimer;
    private System.Threading.Timer? _failureTimer;
    private DateTime _gameStartTime;

    public GraphViewModel(
        AuthService authService,
        AuthHelperService authHelper,
        GameService gameService,
        ILogger<GraphViewModel>? logger = null) : base(authService, authHelper)
    {
        _gameService = gameService;
        _logger = logger;
    }

    public decimal BetAmount
    {
        get => _betAmount;
        set => SetProperty(ref _betAmount, value);
    }

    public decimal CurrentMultiplier
    {
        get => _currentMultiplier;
        private set => SetProperty(ref _currentMultiplier, value);
    }

    public bool IsGameRunning
    {
        get => _isGameRunning;
        private set => SetProperty(ref _isGameRunning, value);
    }

    public bool GameFailed
    {
        get => _gameFailed;
        private set => SetProperty(ref _gameFailed, value);
    }

    public List<GameBet> Bets
    {
        get => _bets;
        private set => SetProperty(ref _bets, value);
    }

    public decimal MinMultiplier => MinMultiplierValue;
    public decimal MaxMultiplier => MaxMultiplierValue;

    public IReadOnlyList<(double x, double y)> GraphPoints => _graphPoints;

    public decimal PotentialWin => BetAmount * CurrentMultiplier;

    public Task InitializeAsync() => base.InitializeAsync(async userId => await LoadBetsAsync(userId));

    private async Task LoadBetsAsync(int? userId = null)
    {
        if (userId.HasValue)
        {
            Bets = await _gameService.GetGraphBetsAsync(userId.Value);
        }
        else
        {
            Bets = await _gameService.GetGraphBetsAsync();
        }
    }

    public async Task UpdateBetAmountFromInputAsync(string? value)
    {
        if (decimal.TryParse(value, out var amount))
        {
            BetAmount = Math.Max(0, amount);
        }
        else if (string.IsNullOrWhiteSpace(value))
        {
            BetAmount = 0;
        }
        await Task.CompletedTask;
    }

    public async Task SetBetAmountPercentageAsync(decimal percentage)
    {
        if (Balance <= 0) return;

        var newAmount = Math.Floor(Balance * percentage / 1000m) * 1000m;
        if (newAmount < 1000m) newAmount = 1000m;

        BetAmount = Math.Min(newAmount, Balance);
        await Task.CompletedTask;
    }

    public async Task StartGameAsync()
    {
        if (IsGameRunning || BetAmount < 1000m)
        {
            return;
        }

        await RefreshBalanceAsync();
        if (Balance < BetAmount)
        {
            BetAmount = Balance;
            return;
        }

        var userId = await AuthHelper.GetUserIdAsync();
        if (!userId.HasValue)
        {
            _logger?.LogWarning("StartGameAsync: 사용자 ID를 찾을 수 없습니다.");
            return;
        }

        var success = await _gameService.StartGraphGameAsync(BetAmount, userId);
        if (!success)
        {
            _logger?.LogWarning("StartGameAsync: GameService.StartGraphGameAsync 실패");
            return;
        }

        Balance -= BetAmount;
        GameFailed = false;
        IsGameRunning = true;
        CurrentMultiplier = MinMultiplierValue;
        _graphPoints.Clear();
        OnPropertyChanged(nameof(GraphPoints));

        StartTimers();
        await LoadBetsAsync(userId);
    }

    public async Task StopGameAsync()
    {
        if (!IsGameRunning || BetAmount <= 0 || GameFailed)
        {
            return;
        }

        IsGameRunning = false;
        StopTimers();

        var userId = await AuthHelper.GetUserIdAsync();
        var success = await _gameService.StopGraphGameAsync(BetAmount, CurrentMultiplier, userId);
        if (success)
        {
            BetAmount = 0;
            CurrentMultiplier = MinMultiplierValue;
            await RefreshBalanceAsync();
            await LoadBetsAsync(userId);
        }
    }

    public void ResetGame()
    {
        GameFailed = false;
        BetAmount = 0;
        CurrentMultiplier = MinMultiplierValue;
        _graphPoints.Clear();
        OnPropertyChanged(nameof(GraphPoints));
    }

    public string GetGraphPath()
    {
        lock (_graphPoints)
        {
            if (_graphPoints.Count < 2) return string.Empty;

            var sb = new StringBuilder();
            sb.Append($"M {_graphPoints[0].x} {_graphPoints[0].y}");
            for (int i = 1; i < _graphPoints.Count; i++)
            {
                var prev = _graphPoints[i - 1];
                var curr = _graphPoints[i];
                var cp1x = prev.x + (curr.x - prev.x) / 3;
                var cp1y = prev.y;
                var cp2x = curr.x - (curr.x - prev.x) / 3;
                var cp2y = curr.y;
                sb.Append($" C {cp1x} {cp1y}, {cp2x} {cp2y}, {curr.x} {curr.y}");
            }

            return sb.ToString();
        }
    }

    public double GetCurrentX()
    {
        lock (_graphPoints)
        {
            return _graphPoints.Count == 0 ? 0 : _graphPoints[^1].x;
        }
    }

    public double GetCurrentY()
    {
        lock (_graphPoints)
        {
            return _graphPoints.Count == 0 ? 300 : _graphPoints[^1].y;
        }
    }

    private void StartTimers()
    {
        StopTimers();

        _gameStartTime = DateTime.UtcNow;
        _graphTimer = new System.Threading.Timer(GraphTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(50));
        _failureTimer = new System.Threading.Timer(FailureTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void GraphTick(object? state)
    {
        if (!IsGameRunning || GameFailed) return;

        var elapsed = (DateTime.UtcNow - _gameStartTime).TotalSeconds;
        var progress = Math.Min(elapsed / GameDurationSeconds, 1.0);
        var easedProgress = Math.Log(1 + progress * 9, 10); // 부드러운 로그형 곡선

        CurrentMultiplier = MinMultiplierValue + (MaxMultiplierValue - MinMultiplierValue) * (decimal)easedProgress;

        lock (_graphPoints)
        {
            var x = progress * GraphWidth;
            var y = GraphHeight - (double)CurrentMultiplier * 60;
            _graphPoints.Add((x, y));
        }

        OnPropertyChanged(nameof(GraphPoints));
    }

    private void FailureTick(object? state)
    {
        if (!IsGameRunning || GameFailed) return;

        if (_random.NextDouble() < FailureChancePerSecond)
        {
            _ = HandleGameFailureAsync();
        }
    }

    private async Task HandleGameFailureAsync()
    {
        lock (_timerLock)
        {
            if (!IsGameRunning || GameFailed) return;
            GameFailed = true;
            IsGameRunning = false;
        }

        StopTimers();

        await _gameService.FailGraphGameAsync(BetAmount);
        await RefreshBalanceAsync();
        await LoadBetsAsync(await AuthHelper.GetUserIdAsync());
    }

    private void StopTimers()
    {
        _graphTimer?.Dispose();
        _graphTimer = null;
        _failureTimer?.Dispose();
        _failureTimer = null;
    }

    public override void Dispose()
    {
        StopTimers();
        base.Dispose();
    }
}

