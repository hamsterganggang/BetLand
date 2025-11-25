using ShowMeTheBet.Models;

namespace ShowMeTheBet.Services;

/// <summary>
/// 베팅 내역 페이지에서 사용하는 간단한 데이터 로딩/취소 서비스.
/// </summary>
public class BetHistoryPageService
{
    private readonly BettingService _bettingService;

    public BetHistoryPageService(BettingService bettingService)
    {
        _bettingService = bettingService;
    }

    public async Task<BetHistoryState> LoadAsync()
    {
        var bets = await _bettingService.GetBetsAsync();
        var balance = await _bettingService.GetBalanceAsync();
        return new BetHistoryState(bets, balance);
    }

    public async Task<BetHistoryState> CancelAsync(int betId)
    {
        await _bettingService.RemoveBetAsync(betId);
        return await LoadAsync();
    }
}

public record BetHistoryState(List<Bet> Bets, decimal Balance);

