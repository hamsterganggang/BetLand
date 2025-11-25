using ShowMeTheBet.Models;
using ShowMeTheBet.Services;
using ShowMeTheBet.ViewModels.Base;

namespace ShowMeTheBet.ViewModels.Game;

public class RouletteViewModel : GamePageViewModel
{
    private readonly GameService _gameService;
    private const int SpinDurationMilliseconds = 10000;
    private decimal _betAmount = 1000m;
    private bool _isSpinning;
    private double _wheelRotation;
    private string _lastResultLabel = "READY";
    private decimal _lastMultiplier = 0m;
    private decimal _winAmount = 0m;
    private string _statusMessage = "베팅 금액을 입력하고 스핀을 눌러주세요.";
    private List<GameBet> _bets = new();

    public RouletteViewModel(AuthService authService, AuthHelperService authHelper, GameService gameService)
        : base(authService, authHelper)
    {
        _gameService = gameService;
    }

    public decimal BetAmount
    {
        get => _betAmount;
        set => SetProperty(ref _betAmount, value);
    }

    public bool IsSpinning
    {
        get => _isSpinning;
        private set => SetProperty(ref _isSpinning, value);
    }

    public double WheelRotation
    {
        get => _wheelRotation;
        private set => SetProperty(ref _wheelRotation, value);
    }

    public string LastResultLabel
    {
        get => _lastResultLabel;
        private set => SetProperty(ref _lastResultLabel, value);
    }

    public decimal LastMultiplier
    {
        get => _lastMultiplier;
        private set => SetProperty(ref _lastMultiplier, value);
    }

    public decimal WinAmount
    {
        get => _winAmount;
        private set => SetProperty(ref _winAmount, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public List<GameBet> Bets
    {
        get => _bets;
        private set => SetProperty(ref _bets, value);
    }

    public Task InitializeAsync() => base.InitializeAsync(async userId => await LoadHistoryAsync(userId));

    private async Task LoadHistoryAsync(int? userId = null)
    {
        var targetUser = userId ?? await AuthHelper.GetUserIdAsync();
        if (targetUser.HasValue)
        {
            Bets = await _gameService.GetRouletteBetsAsync(targetUser.Value);
        }
    }

    public async Task SetBetPercentageAsync(decimal percentage)
    {
        if (Balance <= 0)
        {
            return;
        }

        var calculated = Math.Floor(Balance * percentage / 1000m) * 1000m;
        if (calculated < 1000m) calculated = 1000m;
        BetAmount = Math.Min(calculated, Balance);
        await Task.CompletedTask;
    }

    public async Task SpinAsync()
    {
        if (IsSpinning)
        {
            return;
        }

        if (BetAmount < 1000m)
        {
            StatusMessage = "최소 베팅 금액은 1,000원입니다.";
            return;
        }

        if (BetAmount > Balance)
        {
            await RefreshBalanceAsync();
            if (BetAmount > Balance)
            {
                StatusMessage = "잔액이 부족합니다.";
                return;
            }
        }

        IsSpinning = true;
        WheelRotation += 3600 + Random.Shared.NextDouble() * 720;
        OnPropertyChanged(nameof(WheelRotation));
        StatusMessage = "룰렛을 돌리는 중입니다...";

        var result = await _gameService.PlayRouletteAsync(BetAmount);
        await Task.Delay(SpinDurationMilliseconds);
        IsSpinning = false;

        if (!result.Success)
        {
            StatusMessage = result.Message;
            await RefreshBalanceAsync();
            return;
        }

        Balance = result.BalanceAfterSpin;
        LastResultLabel = result.Label;
        LastMultiplier = result.Multiplier;
        WinAmount = result.WinAmount;
        StatusMessage = result.Message;

        BetAmount = 1000m;

        await LoadHistoryAsync();
    }

    public async Task UpdateBetAmountFromInputAsync(string? value)
    {
        if (decimal.TryParse(value, out var amount))
        {
            BetAmount = amount;
        }
        else if (string.IsNullOrWhiteSpace(value))
        {
            BetAmount = 0;
        }

        await Task.CompletedTask;
    }
}

