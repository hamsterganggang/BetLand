using ShowMeTheBet.Components;
using ShowMeTheBet.Models;
using ShowMeTheBet.Services;
using ShowMeTheBet.ViewModels.Base;

namespace ShowMeTheBet.ViewModels.Game;

/// <summary>
/// 스포츠 토토 페이지(View: Sports.razor)의 ViewModel
/// </summary>
public class SportsViewModel : GamePageViewModel
{
    private readonly BettingService _bettingService;

    private List<Match>? _matches;
    private List<BetSlip.BetSlipItem> _selectedBets = new();

    public SportsViewModel(
        AuthService authService,
        AuthHelperService authHelper,
        BettingService bettingService) : base(authService, authHelper)
    {
        _bettingService = bettingService;
    }

    public List<Match>? Matches
    {
        get => _matches;
        private set => SetProperty(ref _matches, value);
    }

    public List<BetSlip.BetSlipItem> SelectedBets
    {
        get => _selectedBets;
        private set => SetProperty(ref _selectedBets, value);
    }

    public Task InitializeAsync() => base.InitializeAsync(async _ => await LoadMatchesAsync());

    private async Task LoadMatchesAsync()
    {
        Matches = await _bettingService.GetMatchesAsync();
    }

    public void SelectBet(int matchId, BetType type, decimal odds, string homeTeam, string awayTeam)
    {
        var bets = SelectedBets.ToList();
        var existing = bets.FirstOrDefault(b => b.MatchId == matchId);
        if (existing != null)
        {
            bets.Remove(existing);
        }

        bets.Add(new BetSlip.BetSlipItem
        {
            MatchId = matchId,
            MatchInfo = $"{homeTeam} vs {awayTeam}",
            Type = type,
            Odds = odds,
            Amount = 0
        });

        SelectedBets = bets;
    }

    public void RemoveBet(int matchId)
    {
        SelectedBets = SelectedBets.Where(b => b.MatchId != matchId).ToList();
    }

    public async Task PlaceBetsAsync()
    {
        if (AuthService.CurrentUser == null)
        {
            await AuthService.LoadUserFromSessionAsync();
        }

        foreach (var bet in SelectedBets.Where(b => b.Amount > 0))
        {
            await _bettingService.PlaceBetAsync(bet.MatchId, bet.Type, bet.Amount);
        }

        SelectedBets = new List<BetSlip.BetSlipItem>();
        await RefreshBalanceAsync();
    }
}

