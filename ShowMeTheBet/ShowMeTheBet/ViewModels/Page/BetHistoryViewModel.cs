using ShowMeTheBet.Models;
using ShowMeTheBet.Services;
using ShowMeTheBet.ViewModels.Base;

namespace ShowMeTheBet.ViewModels.Page;

/// <summary>
/// 베팅 내역 페이지의 ViewModel.
/// </summary>
public class BetHistoryViewModel : BaseViewModel
{
    private readonly BetHistoryPageService _historyService;
    private readonly AuthService _authService;

    private decimal _balance;
    private List<Bet> _bets = new();

    public BetHistoryViewModel(BetHistoryPageService historyService, AuthService authService)
    {
        _historyService = historyService;
        _authService = authService;
    }

    public bool IsAuthenticated => _authService.IsAuthenticated;

    public decimal Balance
    {
        get => _balance;
        private set => SetProperty(ref _balance, value);
    }

    public List<Bet> Bets
    {
        get => _bets;
        private set => SetProperty(ref _bets, value);
    }

    public async Task InitializeAsync()
    {
        if (!IsAuthenticated)
        {
            Bets = new List<Bet>();
            Balance = 0;
            return;
        }

        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        var state = await _historyService.LoadAsync();
        Bets = state.Bets;
        Balance = state.Balance;
    }

    public async Task CancelBetAsync(int betId)
    {
        var state = await _historyService.CancelAsync(betId);
        Bets = state.Bets;
        Balance = state.Balance;
    }
}

