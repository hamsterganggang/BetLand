using Microsoft.Extensions.Logging;
using ShowMeTheBet.Models;
using ShowMeTheBet.Services;
using ShowMeTheBet.ViewModels.Base;

namespace ShowMeTheBet.ViewModels.Game;

/// <summary>
/// 홀짝 게임(View: OddEven.razor)의 ViewModel
/// </summary>
public class OddEvenViewModel : GamePageViewModel
{
    private readonly GameService _gameService;
    private readonly ILogger<OddEvenViewModel>? _logger;
    private System.Threading.Timer? _roundTimer;
    private readonly object _animationLock = new();
    private DateTime _revealEndTime = DateTime.MinValue;
    private const int RevealDurationSeconds = 3;

    private int _timeLeft = 30;
    private string _currentResult = "홀";
    private string _selectedChoice = string.Empty;
    private decimal _betAmount;
    private List<GameBet> _bets = new();
    private bool _isPlacingBet;
    private bool _isRevealAnimation;
    private int _animationCupCount;
    private string _animationResult = string.Empty;

    public OddEvenViewModel(
        AuthService authService,
        AuthHelperService authHelper,
        GameService gameService,
        ILogger<OddEvenViewModel>? logger = null) : base(authService, authHelper)
    {
        _gameService = gameService;
        _logger = logger;
    }

    public int TimeLeft
    {
        get => _timeLeft;
        private set => SetProperty(ref _timeLeft, value);
    }

    public string CurrentResult
    {
        get => _currentResult;
        private set => SetProperty(ref _currentResult, value);
    }

    public string SelectedChoice
    {
        get => _selectedChoice;
        private set => SetProperty(ref _selectedChoice, value);
    }

    public decimal BetAmount
    {
        get => _betAmount;
        set => SetProperty(ref _betAmount, value);
    }

    public List<GameBet> Bets
    {
        get => _bets;
        private set => SetProperty(ref _bets, value);
    }

    public bool IsPlacingBet
    {
        get => _isPlacingBet;
        private set => SetProperty(ref _isPlacingBet, value);
    }

    public bool IsRevealAnimation
    {
        get => _isRevealAnimation;
        private set => SetProperty(ref _isRevealAnimation, value);
    }

    public int AnimationCupCount
    {
        get => _animationCupCount;
        private set => SetProperty(ref _animationCupCount, value);
    }

    public string AnimationResult
    {
        get => _animationResult;
        private set => SetProperty(ref _animationResult, value);
    }

    public bool IsBetDisabled =>
        string.IsNullOrEmpty(SelectedChoice) ||
        BetAmount <= 0 ||
        TimeLeft < 5 ||
        IsPlacingBet ||
        IsLoading;

    public Task InitializeAsync() => base.InitializeAsync(async userId =>
    {
        await LoadGameDataAsync(userId);
        StartTimer();
    });

    private async Task LoadGameDataAsync(int? userId = null)
    {
        TimeLeft = await _gameService.GetTimeUntilNextRoundAsync();
        CurrentResult = await _gameService.GetCurrentOddEvenResultAsync();

        var finalUserId = userId ?? await AuthHelper.GetUserIdAsync();
        if (finalUserId.HasValue)
        {
            Bets = await _gameService.GetOddEvenBetsAsync(finalUserId.Value);
        }
    }

    private async Task RefreshRoundDataAsync()
    {
        CurrentResult = await _gameService.GetCurrentOddEvenResultAsync();
        await RefreshBalanceAsync();

        var userId = await AuthHelper.GetUserIdAsync();
        if (userId.HasValue)
        {
            Bets = await _gameService.GetOddEvenBetsAsync(userId.Value);
        }
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

    public Task SetBetAmountPercentageAsync(decimal percentage)
    {
        if (Balance <= 0)
        {
            return Task.CompletedTask;
        }

        var calculated = Math.Floor(Balance * percentage / 1000m) * 1000m;
        if (calculated < 1000m) calculated = 1000m;
        BetAmount = Math.Min(calculated, Balance);
        return Task.CompletedTask;
    }

    public void SelectOdd() => SelectedChoice = "홀";
    public void SelectEven() => SelectedChoice = "짝";

    public async Task PlaceBetAsync()
    {
        if (IsBetDisabled)
        {
            return;
        }

        IsPlacingBet = true;

        try
        {
            if (AuthService.CurrentUser == null)
            {
                await AuthService.LoadUserFromSessionAsync();
            }

            decimal currentBalance = AuthService.CurrentUser?.Balance ?? Balance;
            if (currentBalance < BetAmount)
            {
                await RefreshBalanceAsync();
                currentBalance = Balance;
                if (currentBalance < BetAmount)
                {
                    return;
                }
            }

            var userId = await AuthHelper.GetUserIdAsync();
            var success = await _gameService.PlaceOddEvenBetAsync(SelectedChoice, BetAmount, userId);
            if (success)
            {
                Balance -= BetAmount;
                SelectedChoice = string.Empty;
                BetAmount = 0;

                await RefreshBalanceAsync();
                await LoadGameDataAsync(userId);
            }
            else
            {
                await RefreshBalanceAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "OddEven 베팅 중 오류 발생");
        }
        finally
        {
            IsPlacingBet = false;
        }
    }

    private void StartTimer()
    {
        StopTimer();
        _roundTimer = new System.Threading.Timer(
            _ => _ = TimerTickAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1));
    }

    private async Task TimerTickAsync()
    {
        var now = DateTime.UtcNow;
        var secondsSinceEpoch = (long)(now - new DateTime(1970, 1, 1)).TotalSeconds;
        var currentRound = secondsSinceEpoch / 30;
        var nextRoundTime = (currentRound + 1) * 30;
        var nextRoundDateTime = new DateTime(1970, 1, 1).AddSeconds(nextRoundTime);

        var newTimeLeft = (int)(nextRoundDateTime - now).TotalSeconds;
        if (newTimeLeft <= 0) newTimeLeft = 30;

        var previous = TimeLeft;
        TimeLeft = newTimeLeft;

        if (previous == 30 && newTimeLeft < 30)
        {
            await RefreshRoundDataAsync();
            StartRevealAnimation();
        }

        if (IsRevealAnimation && DateTime.UtcNow >= _revealEndTime)
        {
            StopRevealAnimation();
        }
    }

    private void StopTimer()
    {
        _roundTimer?.Dispose();
        _roundTimer = null;
    }

    public override void Dispose()
    {
        StopTimer();
        base.Dispose();
    }

    private void StartRevealAnimation()
    {
        lock (_animationLock)
        {
            AnimationResult = CurrentResult;
            AnimationCupCount = CurrentResult == "짝" ? 2 : 3;
            IsRevealAnimation = true;
            _revealEndTime = DateTime.UtcNow.AddSeconds(RevealDurationSeconds);
        }
    }

    private void StopRevealAnimation()
    {
        lock (_animationLock)
        {
            IsRevealAnimation = false;
        }
    }
}

